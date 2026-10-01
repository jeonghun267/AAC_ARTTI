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
    // 프로필 등록 씬 (다크 네온). 세 단계를 한 씬에 겹쳐 두고, 셋 다 같은 틀(BuildFrame: 패널 + 로고 + 단계 표시 + 닫기)을 쓴다.
    //   1단계 아바타 선택: ProfileAvatarStepBuilder가 만든 패널. 처음에 켜져 있고 '다음'을 누르면 닫힌다.
    //   2단계 프로필 설정: 이 빌더가 만든다. 왼쪽 고른 아바타 카드 + 오른쪽 폼(이름 필수 / 생년월일·한마디 소개 선택).
    //   3단계 완료: ProfileDoneStepBuilder가 만든 패널. 2단계 '다음'에서 저장이 끝나면 켜진다.
    // 좌표는 1920x1080 Stage 좌상단 기준(y 아래가 -).
    // 겹침 순서(아래 -> 위): 2단계, 3단계, 1단계, 유효성 팝업
    public static class ProfileCreateSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";

        // 2단계 배치 (Stage px)
        static readonly Vector2 CardCenter = new Vector2(400f, -665f), CardSize = new Vector2(520f, 690f);   // x 140~660, y 320~1010
        static readonly Vector2 FormCenter = new Vector2(1250f, -615f), FormSize = new Vector2(1060f, 590f); // x 720~1780, y 320~910
        const float IconX = 790f, FieldL = 830f, FieldR = 1740f, FieldH = 70f;

        static readonly Color FormFill  = new Color(0.07f, 0.11f, 0.26f, 0.85f);
        static readonly Color FormLine  = new Color(0.35f, 0.55f, 1.00f, 0.45f);
        static readonly Color FieldFill = new Color(0.10f, 0.16f, 0.36f, 0.95f);
        static readonly Color FieldLine = new Color(0.35f, 0.55f, 1.00f, 0.55f);
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

            // ===== 2단계: 프로필 설정 (1·3단계와 같은 틀, 단계 표시 2) =====
            var stage = BuildFrame(canvas, "ProfileStep", font, 1, out var closeBtn);
            var title = AddHeading(stage, font, "프로필을 꾸며보세요", "이름과 스타일을 설정하면 더 특별해져요.");

            // 왼쪽: 1단계에서 고른 아바타 카드. 카드 전체를 누르면 1단계로 돌아가 다시 고른다.
            var avatarImg = AvatarCard(stage, CardCenter, CardSize, "작은 변화가\n새로운 나를 만들어가요.", font);
            var changeAvatar = HitButton("ChangeAvatarBtn", stage, CardCenter, CardSize);

            // 오른쪽: 폼 패널 + 항목(아이콘 / 제목 / 입력 칸)
            RoundedBox("FormLine", stage, FormCenter, FormSize + new Vector2(6f, 6f), FormLine);
            RoundedBox("Form", stage, FormCenter, FormSize, FormFill);

            FieldLabel(stage, font, "icon_name", "이름", 375f);
            var nameInput = DarkInput("NameInput", stage, res, font, "이름을 입력하세요",
                FieldBox(stage, "NameBox", FieldL, FieldR, 440f, FieldH), new Vector2(FieldR - FieldL, FieldH), multiline: false);

            FieldLabel(stage, font, "icon_birth", "생년월일 <color=#7FB2FF>(선택)</color>", 525f);
            const float ddGap = 24f;
            float ddW = (FieldR - FieldL - ddGap * 2f) / 3f;
            var yearDd  = DarkDropdown("YearDropdown",  stage, res, font, FieldBox(stage, "YearBox",  FieldL, FieldL + ddW, 590f, FieldH), new Vector2(ddW, FieldH));
            var monthDd = DarkDropdown("MonthDropdown", stage, res, font, FieldBox(stage, "MonthBox", FieldL + ddW + ddGap, FieldL + ddW * 2f + ddGap, 590f, FieldH), new Vector2(ddW, FieldH));
            var dayDd   = DarkDropdown("DayDropdown",   stage, res, font, FieldBox(stage, "DayBox",   FieldR - ddW, FieldR, 590f, FieldH), new Vector2(ddW, FieldH));

            FieldLabel(stage, font, "icon_intro", "한마디 소개 <color=#7FB2FF>(선택)</color>", 675f);
            var introInput = DarkInput("IntroInput", stage, res, font, "나를 한마디로 소개해 보세요",
                FieldBox(stage, "IntroBox", FieldL, FieldR, 785f, 160f), new Vector2(FieldR - FieldL, 160f), multiline: true);

            // 다음 (폼 패널 오른쪽 끝에 맞춤, 본체 높이 76)
            const float nextH = 76f;
            float nextW = nextH * NextBody.width / NextBody.height;
            var nextBtn = MakeImageButton("NextBtn", stage, Dir + "17.png", NextBody, PillW, PillH,
                new Vector2(FormCenter.x + FormSize.x / 2f - nextW / 2f, -965f), nextW);

            // ===== 유효성 팝업 (이름을 입력해 주세요) =====
            var validation = MakePopup("ValidationPopup", canvas, font, "이름을 입력해 주세요", out var valOk, out var valClose);

            // ===== View 와이어링 =====
            var view = canvasGo.AddComponent<ProfileCreateView>();

            // ===== 3단계: 완료 (2단계 위, 1단계 아래) =====
            var done = ProfileDoneStepBuilder.Build(canvas, font);

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
            so.FindProperty("doneStep").objectReferenceValue = done.root.gameObject;
            so.FindProperty("doneName").objectReferenceValue = done.name;
            so.FindProperty("doneAvatar").objectReferenceValue = done.avatar;
            so.FindProperty("doneBirth").objectReferenceValue = done.birth;
            so.FindProperty("doneStartButton").objectReferenceValue = done.start;
            so.FindProperty("doneEditButton").objectReferenceValue = done.edit;
            so.FindProperty("doneCloseButton").objectReferenceValue = done.close;
            so.FindProperty("validationPopup").objectReferenceValue = validation;
            so.FindProperty("validationOkButton").objectReferenceValue = valOk;
            so.FindProperty("validationCloseButton").objectReferenceValue = valClose;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.ApplyModifiedProperties();

            // 팝업은 1단계보다 위에 그려지도록 맨 뒤로
            validation.transform.SetAsLastSibling();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileCreateSceneBuilder] 완료 (1단계 아바타 선택 + 2단계 프로필 설정 + 3단계 완료)");
        }

        // 입력 칸 테두리 + 바탕. 칸 가운데(Stage px)를 돌려준다.
        static Vector2 FieldBox(Transform stage, string name, float left, float right, float centerY, float h)
        {
            var center = new Vector2((left + right) / 2f, -centerY);
            var size = new Vector2(right - left, h);
            RoundedBox(name + "Line", stage, center, size + new Vector2(4f, 4f), FieldLine);
            RoundedBox(name, stage, center, size, FieldFill);
            return center;
        }

        // 항목 제목: 왼쪽 아이콘(29.png에서 따낸 흰 아이콘) + 글자
        static void FieldLabel(Transform stage, TMP_FontAsset font, string icon, string text, float centerY)
        {
            var rt = ChildRect(icon, stage);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(IconX, -centerY);
            rt.sizeDelta = new Vector2(40f, 40f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(Dir + icon + ".png", 256);
            img.preserveAspect = true;
            img.raycastTarget = false;
            AddText(stage, icon + "Label", text, font, 28, FieldText, true,
                new Vector2(FieldL, -centerY + 22f), new Vector2(FieldR - FieldL, 44f), TextAlignmentOptions.Left);
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

        // 칸 바탕은 FieldBox가 그려 입력 필드 자체 배경은 투명. 글자만 흰색으로 얹는다.
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

        // 칸 바탕은 FieldBox가 그려 드롭다운 자체 배경은 투명. 펼친 목록은 어두운 톤.
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
            // 펼침 화살표: 칸이 코드로 그려지므로 보이게 두고 밝은 파랑으로
            if (go.transform.Find("Arrow") is RectTransform arrow)
            {
                arrow.anchoredPosition = new Vector2(-30f, 0f);
                arrow.sizeDelta = new Vector2(28f, 28f);
                var arrowImg = arrow.GetComponent<Image>();
                if (arrowImg != null) arrowImg.color = Accent;
            }

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
