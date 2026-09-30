using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.UI;
using static Artti.Editor.ProfileAvatarStepBuilder;

namespace Artti.Editor
{
    // 프로필 등록 씬 (다크 네온). 두 단계를 한 씬에 겹쳐 둔다.
    //   1단계 아바타 선택: ProfileAvatarStepBuilder가 만든 패널. 처음에 켜져 있고 '다음'을 누르면 닫힌다.
    //   2단계 프로필 설정: 이 빌더가 만든다. 이름(필수) / 생년월일(선택) / 한마디 소개(선택).
    // 2단계 배경은 시안 한 장(profiles/step2_bg.png, 1463x1075)에 입력 칸과 글자가 그려져 있고,
    // 그 위에 투명한 입력 필드/드롭다운을 칸 자리에 맞춰 얹는다. 좌표는 전부 시안 px(좌상단 기준).
    // 3단계(완료)는 에셋을 받으면 추가한다. 지금은 2단계 '다음'에서 저장 후 ProfileSelectScene으로 간다.
    public static class ProfileCreateSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";
        const string Step2Path = Dir + "step2_bg.png";

        // 시안 크기와 Stage 배치: 세로 1080에 맞추고 가로 가운데 정렬 (좌우 225는 10.png 배경이 채운다)
        const float ImgW = 1463f, ImgH = 1075f;
        const float Sc = 1080f / ImgH;                  // 1.00465
        const float OffX = (1920f - ImgW * Sc) / 2f;    // 225.1
        static Vector2 P(float x, float y) => new Vector2(OffX + x * Sc, -y * Sc);   // 시안 px -> Stage 좌상단 기준
        static Vector2 L(float w, float h) => new Vector2(w * Sc, h * Sc);

        static readonly Color FieldText   = new Color(0.96f, 0.97f, 1.00f, 1.00f);
        static readonly Color Placeholder = new Color(0.62f, 0.68f, 0.88f, 0.70f);
        static readonly Color ListBg      = new Color(0.07f, 0.11f, 0.26f, 0.98f);
        static readonly Color ListItem    = new Color(0.14f, 0.21f, 0.45f, 1.00f);

        [MenuItem("Artti/Build ProfileCreateScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            OpenOrCreateScene(ScenePaths.ProfileCreate);
            SceneBuilderUtils.ClearRootObjects();
            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();

            // Expand: 16:9보다 긴 기기(20:9)에서 세로가 잘리지 않게. 16:9에서는 1920x1080 그대로.
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution, CanvasScaler.ScreenMatchMode.Expand);
            var canvas = canvasGo.transform;
            var font = LoadFont();
            var res = BuildTmpResources();

            // ===== 2단계: 프로필 설정 =====
            var step = ChildRect("ProfileStep", canvas);
            StretchFull(step);
            var bg = ChildRect("Background", step);
            StretchFull(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = LoadSprite(Dir + "10.png", 2048);
            bgImg.raycastTarget = false;
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1448f / 1086f;

            var stage = ChildRect("Stage", step);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = ReferenceResolution;

            var sheet = ChildRect("Step2Sheet", stage);
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.sizeDelta = L(ImgW, ImgH);
            var sheetImg = sheet.gameObject.AddComponent<Image>();
            sheetImg.sprite = LoadSprite(Step2Path, 2048);
            sheetImg.preserveAspect = true;
            sheetImg.raycastTarget = false;

            // 시안 좌우 경계가 배경과 딱 끊겨 보이지 않게 양옆을 어둡게 덮고 경계 120px에 걸쳐 흐리게 풀어 준다.
            // 넓은 화면(20:9)에서도 양끝까지 덮이도록 Stage 밖으로 길게 늘인다.
            SideShade(stage, left: true);
            SideShade(stage, left: false);

            // 왼쪽 카드(시안 30~595, 300~995): 1단계에서 고른 아바타. 아래 인용문(y 860~) 위까지만 채운다.
            var avatarRt = ChildRect("SelectedAvatar", stage);
            avatarRt.anchorMin = avatarRt.anchorMax = new Vector2(0f, 1f);
            avatarRt.pivot = new Vector2(0.5f, 0f);
            avatarRt.anchoredPosition = P(312.5f, 850f);
            avatarRt.sizeDelta = L(520f, 530f);
            var avatarImg = avatarRt.gameObject.AddComponent<Image>();
            avatarImg.sprite = LoadSprite(Dir + "19.png", 1024);
            avatarImg.preserveAspect = true;
            avatarImg.raycastTarget = false;
            // 카드 전체를 누르면 1단계로 돌아가 다시 고른다
            var changeAvatar = HitButton("ChangeAvatarBtn", stage, P(312.5f, 647.5f), L(565f, 695f));

            // 닫기(X, 시안 1403,48에 그려져 있음)
            var closeBtn = HitButton("CloseBtn", stage, P(1403f, 48f), L(84f, 84f));

            // 입력 칸 (시안에 그려진 칸 자리)
            var nameInput = DarkInput("NameInput", stage, res, font, "이름을 입력하세요", P(1068.5f, 417f), L(671f, 60f), multiline: false);
            var yearDd  = DarkDropdown("YearDropdown",  stage, res, font, P(834f, 566f),  L(206f, 60f));
            var monthDd = DarkDropdown("MonthDropdown", stage, res, font, P(1071f, 566f), L(206f, 60f));
            var dayDd   = DarkDropdown("DayDropdown",   stage, res, font, P(1306f, 566f), L(202f, 60f));
            var introInput = DarkInput("IntroInput", stage, res, font, "나를 한마디로 소개해 보세요", P(1069f, 751.5f), L(676f, 123f), multiline: true);

            // 다음 (폼 패널 오른쪽 아래, 본체 높이 76)
            const float nextH = 76f;
            float nextW = nextH * NextBody.width / NextBody.height;
            var nextBtn = MakeImageButton("NextBtn", stage, Dir + "17.png", NextBody, PillW, PillH,
                P(1435f, 975f) - new Vector2(nextW / 2f, 0f), nextW);

            // ===== 유효성 팝업 (이름을 입력해 주세요) =====
            var validation = MakePopup("ValidationPopup", canvas, font, "이름을 입력해 주세요", out var valOk, out var valClose);

            // ===== View 와이어링 =====
            var view = canvasGo.AddComponent<ProfileCreateView>();

            // ===== 1단계: 아바타 선택 (맨 위에 덮는다) =====
            var avatarStep = ProfileAvatarStepBuilder.Build(canvas, view, font);

            var so = new SerializedObject(view);
            so.FindProperty("nameInput").objectReferenceValue = nameInput;
            so.FindProperty("yearDropdown").objectReferenceValue = yearDd;
            so.FindProperty("monthDropdown").objectReferenceValue = monthDd;
            so.FindProperty("dayDropdown").objectReferenceValue = dayDd;
            so.FindProperty("introInput").objectReferenceValue = introInput;
            so.FindProperty("createButton").objectReferenceValue = nextBtn;
            so.FindProperty("backButton").objectReferenceValue = closeBtn;
            so.FindProperty("previewAvatar").objectReferenceValue = avatarImg;
            so.FindProperty("changeAvatarButton").objectReferenceValue = changeAvatar;
            so.FindProperty("avatarStep").objectReferenceValue = avatarStep.gameObject;
            so.FindProperty("validationPopup").objectReferenceValue = validation;
            so.FindProperty("validationOkButton").objectReferenceValue = valOk;
            so.FindProperty("validationCloseButton").objectReferenceValue = valClose;
            so.ApplyModifiedProperties();

            // 팝업은 1단계보다 위에 그려지도록 맨 뒤로
            validation.transform.SetAsLastSibling();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileCreateSceneBuilder] 완료 (1단계 아바타 선택 + 2단계 프로필 설정)");
        }

        static readonly Color ShadeColor = new Color(0.03f, 0.05f, 0.14f, 0.8f);

        // 한쪽 옆 가림: 단색(화면 끝 ~ 시안 경계-20) + 페이드(경계-20 ~ 경계+100)
        static void SideShade(Transform stage, bool left)
        {
            const float far = 1000f, fadeW = 120f, inset = 20f, h = 1680f;
            float edge = left ? OffX : 1920f - OffX;
            float solidW = far + OffX - inset;

            var solid = ChildRect(left ? "ShadeL" : "ShadeR", stage);
            solid.anchorMin = solid.anchorMax = new Vector2(0f, 1f);
            solid.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            solid.anchoredPosition = new Vector2(left ? edge - inset : edge + inset, -540f);
            solid.sizeDelta = new Vector2(solidW, h);
            var sImg = solid.gameObject.AddComponent<Image>();
            sImg.color = ShadeColor;
            sImg.raycastTarget = false;

            var fade = ChildRect(left ? "FadeL" : "FadeR", stage);
            fade.anchorMin = fade.anchorMax = new Vector2(0f, 1f);
            fade.pivot = new Vector2(0f, 0.5f);
            fade.anchoredPosition = new Vector2(left ? edge - inset : edge + inset, -540f);
            fade.sizeDelta = new Vector2(fadeW, h);
            fade.localScale = new Vector3(left ? 1f : -1f, 1f, 1f);   // 스프라이트는 왼쪽이 진하고 오른쪽으로 사라진다
            var fImg = fade.gameObject.AddComponent<Image>();
            fImg.sprite = LoadSprite(Dir + "edge_fade.png", 256);
            fImg.color = ShadeColor;
            fImg.raycastTarget = false;
        }

        // 투명 클릭 영역 (가운데 기준)
        static Button HitButton(string name, Transform parent, Vector2 center, Vector2 size)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            return btn;
        }

        // 시안에 칸이 그려져 있어 배경은 투명. 글자만 흰색으로 얹는다.
        static TMP_InputField DarkInput(string name, Transform parent, TMP_DefaultControls.Resources res, TMP_FontAsset font,
            string placeholder, Vector2 center, Vector2 size, bool multiline)
        {
            var go = TMP_DefaultControls.CreateInputField(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            if (img != null) img.color = new Color(1f, 1f, 1f, 0f);

            var input = go.GetComponent<TMP_InputField>();
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.characterLimit = multiline ? 60 : 12;
            input.caretColor = FieldText;
            input.customCaretColor = true;
            input.selectionColor = new Color(0.5f, 0.72f, 1f, 0.4f);
            var area = input.textViewport;
            if (area != null) { area.offsetMin = new Vector2(22f, 8f); area.offsetMax = new Vector2(-22f, -8f); }
            var align = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
            input.textComponent.fontSize = 30;
            input.textComponent.color = FieldText;
            input.textComponent.alignment = align;
            if (input.placeholder is TMP_Text ph)
            {
                ph.text = placeholder;
                ph.fontSize = 28;
                ph.color = Placeholder;
                ph.fontStyle = FontStyles.Normal;
                ph.alignment = align;
            }
            ApplyFont(go, font);
            return input;
        }

        // 칸과 펼침 화살표는 시안에 그려져 있어 배경/화살표를 숨기고 글자만 보인다. 펼친 목록은 어두운 톤.
        static TMP_Dropdown DarkDropdown(string name, Transform parent, TMP_DefaultControls.Resources res, TMP_FontAsset font,
            Vector2 center, Vector2 size)
        {
            var go = TMP_DefaultControls.CreateDropdown(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            if (img != null) img.color = new Color(1f, 1f, 1f, 0f);
            var arrow = go.transform.Find("Arrow");
            if (arrow != null) arrow.gameObject.SetActive(false);

            var dd = go.GetComponent<TMP_Dropdown>();
            if (dd.captionText != null)
            {
                dd.captionText.fontSize = 28;
                dd.captionText.color = FieldText;
                dd.captionText.alignment = TextAlignmentOptions.Left;
                var cr = dd.captionText.rectTransform;
                cr.offsetMin = new Vector2(24f, 0f);
                cr.offsetMax = new Vector2(-56f, 0f);   // 오른쪽 화살표 자리 비움
            }

            var template = dd.template;
            if (template != null)
            {
                template.sizeDelta = new Vector2(template.sizeDelta.x, 420f);
                var tImg = template.GetComponent<Image>();
                if (tImg != null) { tImg.sprite = Rounded(); tImg.type = Image.Type.Sliced; tImg.color = ListBg; }
                var item = template.Find("Viewport/Content/Item") as RectTransform;
                if (item != null) item.sizeDelta = new Vector2(item.sizeDelta.x, 64f);
                var itemBg = template.Find("Viewport/Content/Item/Item Background")?.GetComponent<Image>();
                if (itemBg != null) itemBg.color = ListItem;
                var check = template.Find("Viewport/Content/Item/Item Checkmark")?.GetComponent<Image>();
                if (check != null) check.color = Accent;
                var bar = template.Find("Scrollbar")?.GetComponent<Image>();
                if (bar != null) bar.color = new Color(0.10f, 0.15f, 0.32f, 1f);
                var handle = template.Find("Scrollbar/Sliding Area/Handle")?.GetComponent<Image>();
                if (handle != null) handle.color = new Color(0.45f, 0.60f, 0.95f, 0.8f);
            }
            if (dd.itemText != null)
            {
                dd.itemText.fontSize = 28;
                dd.itemText.color = FieldText;
                dd.itemText.alignment = TextAlignmentOptions.Left;
                var lr = dd.itemText.rectTransform;
                lr.offsetMin = new Vector2(44f, 0f);
                lr.offsetMax = new Vector2(-20f, 0f);
            }
            ApplyFont(go, font);
            return dd;
        }

        // 가운데 모달: 반투명 오버레이 + 어두운 카드 + 문구 + 확인 + X
        static GameObject MakePopup(string name, Transform parent, TMP_FontAsset font, string message, out Button ok, out Button close)
        {
            var overlay = ChildRect(name, parent);
            StretchFull(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var center = ChildRect("Center", overlay);
            center.anchorMin = center.anchorMax = center.pivot = new Vector2(0.5f, 0.5f);
            center.sizeDelta = new Vector2(720f, 320f);
            RoundedBox("Line", center, new Vector2(360f, -160f), new Vector2(726f, 326f), Accent);
            RoundedBox("Card", center, new Vector2(360f, -160f), new Vector2(720f, 320f), ListBg);
            AddText(center, "Message", message, font, 38, FieldText, true, new Vector2(0f, -70f), new Vector2(720f, 60f), TextAlignmentOptions.Center);

            var okRt = RoundedBox("OkButton", center, new Vector2(360f, -225f), new Vector2(240f, 84f), new Color(0.35f, 0.55f, 1f, 1f));
            okRt.GetComponent<Image>().raycastTarget = true;
            ok = okRt.gameObject.AddComponent<Button>();
            ok.targetGraphic = okRt.GetComponent<Image>();
            var okText = AddText(okRt, "Text", "확인", font, 32, FieldText, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            StretchFull(okText.rectTransform);

            var xRt = ChildRect("Close", center);
            xRt.anchorMin = xRt.anchorMax = new Vector2(0f, 1f);
            xRt.pivot = new Vector2(0.5f, 0.5f);
            xRt.anchoredPosition = new Vector2(676f, -44f);
            xRt.sizeDelta = new Vector2(72f, 72f);
            var xImg = xRt.gameObject.AddComponent<Image>();
            xImg.color = new Color(1f, 1f, 1f, 0f);
            close = xRt.gameObject.AddComponent<Button>();
            close.targetGraphic = xImg;
            var xText = AddText(xRt, "Text", "×", font, 44, FieldText, false, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            StretchFull(xText.rectTransform);

            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        static void ApplyFont(GameObject go, TMP_FontAsset font)
        {
            if (font == null) return;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.font = font;
        }

        static TMP_DefaultControls.Resources BuildTmpResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard   = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob       = Builtin("UI/Skin/Knob.psd"),
                checkmark  = Builtin("UI/Skin/Checkmark.psd"),
                dropdown   = Builtin("UI/Skin/DropdownArrow.psd"),
                mask       = Builtin("UI/Skin/UIMask.psd"),
            };
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null) font = SceneBuilderUtils.GetKoreanFont();
            return font;
        }

        static void OpenOrCreateScene(string path)
        {
            if (System.IO.File.Exists(path)) { SceneBuilderUtils.OpenScene(path); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
