using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 프로필 생성 폼 씬. 가로 1920x1080, 흰 배경.
    // 이름 입력(네이티브 키보드) + 생년/월 드롭다운 + 대표 아바타(동물 얼굴 10종) 선택.
    public static class ProfileCreateSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color Slate    = new Color32(63, 74, 94, 255);
        static readonly Color BtnBlue  = new Color32(42, 109, 224, 255);
        static readonly Color FieldBg  = new Color32(238, 240, 244, 255);
        static readonly Color White    = Color.white;

        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";
        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";

        // 동물 얼굴 아바타 10종 (OpenMoji hex)
        static readonly string[] AvatarHex =
        {
            "1F436", "1F431", "1F42D", "1F439", "1F430",
            "1F98A", "1F43B", "1F43C", "1F428", "1F981"
        };

        [MenuItem("Artti/Build ProfileCreateScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            OpenOrCreateScene(ScenePaths.ProfileCreate);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);

            var bg = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bg.AddComponent<Image>().color = White;

            var font = LoadFont();
            var res = BuildTmpResources();

            // 뒤로 가기 (← Splash)
            SceneBuilderUtils.CreateBackButton("SplashScene", canvasGo.transform, "← 뒤로");

            // 타이틀
            var title = MakeText("Title", canvasGo.transform, "프로필 만들기", 80, Slate, font);
            Place(title.rectTransform, new Vector2(0, 470), new Vector2(900, 110));

            // 이름 (라벨과 칸 사이 ~36px 간격)
            MakeLabel("Label_Name", canvasGo.transform, "이름", font, new Vector2(0, 335));
            var input = MakeInput("NameInput", canvasGo.transform, res, font, "이름 입력",
                new Vector2(0, 225), new Vector2(720, 84));

            // 생년 / 월
            MakeLabel("Label_Birth", canvasGo.transform, "생년 / 월", font, new Vector2(0, 120));
            var yearDd = MakeDropdown("YearDropdown", canvasGo.transform, res, font,
                new Vector2(-210, 10), new Vector2(340, 84));
            var monthDd = MakeDropdown("MonthDropdown", canvasGo.transform, res, font,
                new Vector2(210, 10), new Vector2(340, 84));

            // 대표 이미지
            MakeLabel("Label_Avatar", canvasGo.transform, "대표 이미지", font, new Vector2(0, -100));
            var gridRect = MakeRect("AvatarGrid", canvasGo.transform, new Vector2(0, -290), new Vector2(760, 270));
            SceneBuilderUtils.AddGridLayout(gridRect.gameObject, new Vector2(120, 120), new Vector2(28, 28), 5,
                new RectOffset(0, 0, 0, 0));
            for (int i = 0; i < AvatarHex.Length; i++)
                MakeAvatarItem(AvatarHex[i], gridRect.transform);

            // 만들기 버튼
            var createBtn = MakeButton("CreateButton", canvasGo.transform, "만들기", 52,
                new Vector2(0, -485), new Vector2(360, 110), BtnBlue, White, font);

            // 생성 확인 팝업 (프로필을 생성할까요?)
            var confirmPopup = MakePopup("ConfirmPopup", canvasGo.transform, new Vector2(760, 360), out var confirmCard);
            var confirmTitle = MakeText("Title", confirmCard, "프로필을 생성할까요?", 48, Slate, font);
            Place(confirmTitle.rectTransform, new Vector2(0, 75), new Vector2(680, 90));
            var confirmNo = MakeButton("CancelButton", confirmCard, "취소", 40,
                new Vector2(-150, -85), new Vector2(220, 100), FieldBg, Slate, font);
            var confirmYes = MakeButton("ConfirmButton", confirmCard, "확인", 40,
                new Vector2(150, -85), new Vector2(220, 100), BtnBlue, White, font);
            var confirmX = MakeIconButton("Close", confirmCard, "×", new Vector2(330, 130), new Vector2(64, 64), font);

            // 유효성 팝업 (입력 정보를 확인해주세요!)
            var validationPopup = MakePopup("ValidationPopup", canvasGo.transform, new Vector2(760, 320), out var valCard);
            var valTitle = MakeText("Title", valCard, "입력 정보를 확인해주세요!", 46, Slate, font);
            Place(valTitle.rectTransform, new Vector2(0, 55), new Vector2(700, 90));
            var valOk = MakeButton("OkButton", valCard, "확인", 40,
                new Vector2(0, -75), new Vector2(240, 100), BtnBlue, White, font);
            var valX = MakeIconButton("Close", valCard, "×", new Vector2(330, 110), new Vector2(64, 64), font);

            confirmPopup.SetActive(false);
            validationPopup.SetActive(false);

            // View 와이어링
            var view = canvasGo.AddComponent<ProfileCreateView>();
            var so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("confirmTitleText").objectReferenceValue = confirmTitle;
            so.FindProperty("nameInput").objectReferenceValue = input;
            so.FindProperty("yearDropdown").objectReferenceValue = yearDd;
            so.FindProperty("monthDropdown").objectReferenceValue = monthDd;
            so.FindProperty("avatarGrid").objectReferenceValue = gridRect;
            so.FindProperty("createButton").objectReferenceValue = createBtn;
            so.FindProperty("confirmPopup").objectReferenceValue = confirmPopup;
            so.FindProperty("confirmYesButton").objectReferenceValue = confirmYes;
            so.FindProperty("confirmNoButton").objectReferenceValue = confirmNo;
            so.FindProperty("confirmCloseButton").objectReferenceValue = confirmX;
            so.FindProperty("validationPopup").objectReferenceValue = validationPopup;
            so.FindProperty("validationOkButton").objectReferenceValue = valOk;
            so.FindProperty("validationCloseButton").objectReferenceValue = valX;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileCreateSceneBuilder] 완료");
        }

        static TMP_InputField MakeInput(string name, Transform parent, TMP_DefaultControls.Resources res,
            TMP_FontAsset font, string placeholder, Vector2 pos, Vector2 size)
        {
            var go = TMP_DefaultControls.CreateInputField(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Anchor(rect, pos, size);

            var img = go.GetComponent<Image>();
            if (img != null) { img.sprite = Rounded(); img.type = Image.Type.Sliced; img.color = FieldBg; }

            var input = go.GetComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.textComponent.fontSize = 44;
            input.textComponent.color = Slate;
            if (input.placeholder is TMP_Text ph) { ph.text = placeholder; ph.fontSize = 44; }
            ApplyFont(go, font);
            return input;
        }

        static TMP_Dropdown MakeDropdown(string name, Transform parent, TMP_DefaultControls.Resources res,
            TMP_FontAsset font, Vector2 pos, Vector2 size)
        {
            var go = TMP_DefaultControls.CreateDropdown(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Anchor(rect, pos, size);

            var img = go.GetComponent<Image>();
            if (img != null) { img.sprite = Rounded(); img.type = Image.Type.Sliced; img.color = FieldBg; }

            var dd = go.GetComponent<TMP_Dropdown>();
            if (dd.captionText != null) { dd.captionText.fontSize = 40; dd.captionText.color = Slate; }
            StyleDropdownList(dd, itemHeight: 96, listHeight: 520);
            ApplyFont(go, font);
            return dd;
        }

        // 드롭다운 펼친 목록: 항목 높이/목록 박스 키우고 라벨 좌측 여백 (글자 겹침 방지)
        static void StyleDropdownList(TMP_Dropdown dd, int itemHeight, int listHeight)
        {
            var template = dd.template;
            if (template != null)
                template.sizeDelta = new Vector2(template.sizeDelta.x, listHeight);

            var item = template != null ? template.Find("Viewport/Content/Item") as RectTransform : null;
            if (item != null)
                item.sizeDelta = new Vector2(item.sizeDelta.x, itemHeight);

            if (dd.itemText != null)
            {
                dd.itemText.fontSize = 36;
                dd.itemText.color = Slate;
                dd.itemText.alignment = TextAlignmentOptions.Left;
                var lr = dd.itemText.rectTransform;
                lr.offsetMin = new Vector2(44, 0);
                lr.offsetMax = new Vector2(-30, 0);
            }
        }

        // 아바타 한 칸: SelectedBorder(선택 시) + Icon(이모지) + Button + AvatarItem
        static void MakeAvatarItem(string hex, Transform parent)
        {
            var item = MakeRect($"Avatar_{hex}", parent, Vector2.zero, new Vector2(140, 140));

            var border = MakeRect("Selected", item.transform, Vector2.zero, new Vector2(152, 152));
            var borderImg = border.gameObject.AddComponent<Image>();
            borderImg.sprite = Rounded();
            borderImg.type = Image.Type.Sliced;
            borderImg.color = BtnBlue;
            border.gameObject.SetActive(false);

            var icon = MakeRect("Icon", item.transform, Vector2.zero, new Vector2(140, 140));
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = LoadAvatar(hex);
            iconImg.preserveAspect = true;

            var btn = item.gameObject.AddComponent<Button>();
            btn.targetGraphic = iconImg;

            var ai = item.gameObject.AddComponent<AvatarItem>();
            ai.avatarId = hex;
            ai.button = btn;
            ai.selectedMark = border.gameObject;
        }

        static Button MakeButton(string name, Transform parent, string label, int fontSize,
            Vector2 pos, Vector2 size, Color bg, Color textColor, TMP_FontAsset font)
        {
            var rect = MakeRect(name, parent, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            img.color = bg;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, label, fontSize, textColor, font);
            SceneBuilderUtils.FillStretch(t.rectTransform, 8);
            return btn;
        }

        // 모달 팝업: 전체화면 반투명 오버레이 + 둥근 흰 카드
        static GameObject MakePopup(string name, Transform parent, Vector2 cardSize, out RectTransform card)
        {
            var overlay = SceneBuilderUtils.CreatePanel(name, parent);
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            card = MakeRect("Card", overlay.transform, Vector2.zero, cardSize);
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            img.color = White;
            return overlay;
        }

        // 아이콘 버튼 (투명 배경 + 글리프). X 닫기 버튼용.
        static Button MakeIconButton(string name, Transform parent, string glyph, Vector2 pos, Vector2 size, TMP_FontAsset font)
        {
            var rect = MakeRect(name, parent, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // 투명 (레이캐스트만)
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, glyph, 44, Slate, font);
            SceneBuilderUtils.FillStretch(t.rectTransform, 6);
            return btn;
        }

        static void MakeLabel(string name, Transform parent, string text, TMP_FontAsset font, Vector2 pos)
        {
            var t = MakeText(name, parent, text, 36, Slate, font);
            t.alignment = TextAlignmentOptions.Left;
            Place(t.rectTransform, pos, new Vector2(800, 50));
        }

        static TMP_Text MakeText(string name, Transform parent, string text, int fontSize, Color color, TMP_FontAsset font)
        {
            var tmp = SceneBuilderUtils.CreateTMPText(name, parent, text, fontSize);
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        static void ApplyFont(GameObject go, TMP_FontAsset font)
        {
            if (font == null) return;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.font = font;
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            Anchor(rect, pos, size);
            return rect;
        }

        static void Anchor(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void Place(RectTransform rect, Vector2 pos, Vector2 size) => Anchor(rect, pos, size);

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

        static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : Builtin("UI/Skin/UISprite.psd");
        }

        static Sprite LoadAvatar(string hex)
        {
            string p = $"Assets/_Project/Art/Avatars/{hex}.png";
            var importer = AssetImporter.GetAtPath(p) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(p) as TextureImporter;
            }
            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single; // Multiple → Single (슬라이스 없으면 null이 됨)
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }

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
