using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.UI;

namespace Artti.Editor
{
    // 사용자 선택 화면. 가로 1920x1080.
    // 카드 목록(선택 카드 맨 위 + primary 테두리 + 좌상단 체크) / 빈 상태(0명).
    public static class ProfileSelectSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color Slate    = new Color32(63, 74, 94, 255);
        static readonly Color BtnBlue  = new Color32(42, 109, 224, 255); // primary
        static readonly Color DateGray = new Color32(140, 150, 165, 255);
        static readonly Color CardBtnBg = new Color32(238, 240, 244, 255);
        static readonly Color BgColor  = new Color32(245, 246, 250, 255);
        static readonly Color Danger   = new Color32(224, 64, 64, 255); // 삭제
        static readonly Color White    = Color.white;

        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";
        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";
        const string CardPrefabPath = "Assets/_Project/Prefabs/Profile/ProfileCard.prefab";

        [MenuItem("Artti/Build ProfileSelectScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.ProfileSelect);
            SceneBuilderUtils.ClearRootObjects();

            var bootstrap = new GameObject("[AppBootstrap]");
            bootstrap.AddComponent<AppBootstrap>();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);

            var font = LoadFont();
            EnsureAvatarLibrary();
            var cardPrefab = EnsureProfileCardPrefab(font);

            var bg = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bg.AddComponent<Image>().color = BgColor;

            // 뒤로 (좌상단)
            SceneBuilderUtils.CreateBackButton("SplashScene", canvasGo.transform, "← 뒤로");

            // + 새로 등록하기 (우상단)
            var addBtn = MakeButton("AddButton", canvasGo.transform, "+ 새로 등록하기", 36, BtnBlue, White, font);
            var addRect = addBtn.GetComponent<RectTransform>();
            addRect.anchorMin = addRect.anchorMax = new Vector2(1, 1);
            addRect.pivot = new Vector2(1, 1);
            addRect.anchoredPosition = new Vector2(-40, -40);
            addRect.sizeDelta = new Vector2(380, 96);

            // ===== 목록 루트 (제목 + 스크롤) =====
            var listRoot = MakeRect("ListRoot", canvasGo.transform, Vector2.zero, Vector2.zero);
            StretchFull(listRoot, 0);

            var listTitle = MakeText("Title", listRoot.transform, "사용자를 선택하세요", 72, Slate, font);
            listTitle.rectTransform.anchorMin = listTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            listTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            listTitle.rectTransform.anchoredPosition = new Vector2(0, -70);
            listTitle.rectTransform.sizeDelta = new Vector2(1000, 100);

            var scroll = MakeRect("CardScroll", listRoot.transform, new Vector2(0, -70), new Vector2(1320, 760));
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30;

            var viewport = ChildRect("Viewport", scroll.transform);
            StretchFull(viewport, 0);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = ChildRect("Content", viewport.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 24;
            vlg.padding = new RectOffset(20, 20, 10, 10);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            // ===== 빈 상태 (0명) =====
            var emptyState = MakeRect("EmptyState", canvasGo.transform, Vector2.zero, Vector2.zero);
            StretchFull(emptyState, 0);

            var emptyTitle = MakeText("EmptyTitle", emptyState.transform, "등록된 사용자가 없습니다.", 64, Slate, font);
            emptyTitle.rectTransform.anchorMin = emptyTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            emptyTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            emptyTitle.rectTransform.anchoredPosition = new Vector2(0, -90);
            emptyTitle.rectTransform.sizeDelta = new Vector2(1100, 100);

            var promptCard = MakeRect("PromptCard", emptyState.transform, new Vector2(0, 200), new Vector2(900, 200));
            var pcImg = promptCard.gameObject.AddComponent<Image>();
            pcImg.sprite = Rounded(); pcImg.type = Image.Type.Sliced; pcImg.pixelsPerUnitMultiplier = 1f;
            pcImg.color = White;
            var pAvatar = ChildRect("Avatar", promptCard.transform);
            pAvatar.anchorMin = pAvatar.anchorMax = new Vector2(0, 0.5f);
            pAvatar.pivot = new Vector2(0, 0.5f);
            pAvatar.anchoredPosition = new Vector2(48, 0);
            pAvatar.sizeDelta = new Vector2(110, 110);
            var pAvImg = pAvatar.gameObject.AddComponent<Image>();
            pAvImg.sprite = Builtin("UI/Skin/Knob.psd"); pAvImg.color = new Color32(200, 206, 214, 255);
            var pText = MakeText("Text", promptCard.transform, "새로운 사용자를 등록해보세요!", 38, Slate, font);
            pText.alignment = TextAlignmentOptions.Left;
            pText.rectTransform.anchorMin = pText.rectTransform.anchorMax = new Vector2(0, 0.5f);
            pText.rectTransform.pivot = new Vector2(0, 0.5f);
            pText.rectTransform.anchoredPosition = new Vector2(190, 0);
            pText.rectTransform.sizeDelta = new Vector2(660, 80);

            // 기본 상태: 목록 ON / 빈 상태 OFF (런타임에 View가 프로필 수에 따라 토글)
            emptyState.gameObject.SetActive(false);

            // ===== 삭제 확인 팝업 (가운데 모달, "동의한다" 타이핑 확인) =====
            var delRes = BuildTmpResources();
            var deletePopup = MakePopup("DeleteConfirmPopup", canvasGo.transform, new Vector2(860, 460), out var delCard);
            var delMsg = MakeText("Message", delCard, "정말 삭제할까요?\n동의하시면 아래에 '동의한다'를 입력하세요.", 38, Slate, font);
            delMsg.alignment = TextAlignmentOptions.Center;
            delMsg.textWrappingMode = TextWrappingModes.Normal;
            Place(delMsg.rectTransform, new Vector2(0, 120), new Vector2(760, 180));
            var delInput = MakeInput("DeleteInput", delCard, delRes, font, "동의한다", new Vector2(0, -10), new Vector2(620, 88));
            var delCancel = MakeButton("CancelButton", delCard, "취소", 38, CardBtnBg, Slate, font);
            Place(delCancel.GetComponent<RectTransform>(), new Vector2(-180, -150), new Vector2(250, 96));
            var delConfirm = MakeButton("ConfirmDeleteButton", delCard, "삭제", 38, Danger, White, font);
            Place(delConfirm.GetComponent<RectTransform>(), new Vector2(180, -150), new Vector2(250, 96));
            var delX = MakeIconButton("Close", delCard, "×", new Vector2(380, 180), new Vector2(64, 64), font);
            deletePopup.SetActive(false);

            // ===== View 와이어링 =====
            var view = canvasGo.AddComponent<ProfileSelectView>();
            var so = new SerializedObject(view);
            so.FindProperty("cardContainer").objectReferenceValue = content;
            so.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            so.FindProperty("cardScroll").objectReferenceValue = listRoot.gameObject;
            so.FindProperty("emptyState").objectReferenceValue = emptyState.gameObject;
            so.FindProperty("addButton").objectReferenceValue = addBtn;
            so.FindProperty("deletePopup").objectReferenceValue = deletePopup;
            so.FindProperty("deleteMessageText").objectReferenceValue = delMsg;
            so.FindProperty("deleteInput").objectReferenceValue = delInput;
            so.FindProperty("deleteConfirmButton").objectReferenceValue = delConfirm;
            so.FindProperty("deleteCancelButton").objectReferenceValue = delCancel;
            so.FindProperty("deleteCloseButton").objectReferenceValue = delX;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileSelectSceneBuilder] 완료");
        }

        // ===== 카드 프리팹 =====
        static GameObject EnsureProfileCardPrefab(TMP_FontAsset font)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) != null)
                AssetDatabase.DeleteAsset(CardPrefabPath);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Profile");

            var root = new GameObject("ProfileCard");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1100, 200);
            var rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(0, 0, 0, 0); // 투명 레이캐스트 (카드 선택)
            var selectBtn = root.AddComponent<Button>();
            selectBtn.targetGraphic = rootImg;
            var le = root.AddComponent<LayoutElement>();
            le.preferredHeight = 200;

            // 선택 테두리 (primary, 전체)
            var border = ChildRect("SelectedBorder", root.transform);
            StretchFull(border, 0);
            var bImg = border.gameObject.AddComponent<Image>();
            bImg.sprite = Rounded(); bImg.type = Image.Type.Sliced; bImg.pixelsPerUnitMultiplier = 1f;
            bImg.color = BtnBlue; bImg.raycastTarget = false;

            // 본문 (흰, 6px inset → 테두리가 프레임)
            var body = ChildRect("Body", root.transform);
            StretchFull(body, 6);
            var bodyImg = body.gameObject.AddComponent<Image>();
            bodyImg.sprite = Rounded(); bodyImg.type = Image.Type.Sliced; bodyImg.pixelsPerUnitMultiplier = 1f;
            bodyImg.color = White; bodyImg.raycastTarget = false;

            // 아바타
            var avatar = ChildRect("Avatar", body.transform);
            avatar.anchorMin = avatar.anchorMax = new Vector2(0, 0.5f);
            avatar.pivot = new Vector2(0, 0.5f);
            avatar.anchoredPosition = new Vector2(48, 0);
            avatar.sizeDelta = new Vector2(140, 140);
            var avatarImg = avatar.gameObject.AddComponent<Image>();
            avatarImg.preserveAspect = true; avatarImg.raycastTarget = false;

            // 이름 / 날짜
            var nameText = MakeText("Name", body.transform, "닉네임", 48, Slate, font);
            nameText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(nameText.rectTransform, new Vector2(220, 30), new Vector2(520, 70));

            var dateText = MakeText("Date", body.transform, "마지막 사용: 오늘", 30, DateGray, font);
            dateText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(dateText.rectTransform, new Vector2(222, -34), new Vector2(520, 46));

            // 수정 / 삭제
            var editBtn = MakeCardButton("EditButton", body.transform, "수정", Slate, font, new Vector2(-210, 0), new Vector2(160, 84));
            var deleteBtn = MakeCardButton("DeleteButton", body.transform, "삭제", Danger, font, new Vector2(-30, 0), new Vector2(160, 84));

            // 체크 (좌측 상단)
            var check = ChildRect("CheckMark", root.transform);
            check.anchorMin = check.anchorMax = new Vector2(0, 1);
            check.pivot = new Vector2(0.5f, 0.5f);
            check.anchoredPosition = new Vector2(12, -12);
            check.sizeDelta = new Vector2(58, 58);
            var checkBg = check.gameObject.AddComponent<Image>();
            checkBg.sprite = Builtin("UI/Skin/Knob.psd"); checkBg.color = BtnBlue; checkBg.raycastTarget = false;
            var checkIcon = ChildRect("Icon", check.transform);
            checkIcon.anchorMin = new Vector2(0.5f, 0.5f); checkIcon.anchorMax = new Vector2(0.5f, 0.5f);
            checkIcon.pivot = new Vector2(0.5f, 0.5f); checkIcon.anchoredPosition = Vector2.zero;
            checkIcon.sizeDelta = new Vector2(34, 34);
            var checkImg = checkIcon.gameObject.AddComponent<Image>();
            checkImg.sprite = Builtin("UI/Skin/Checkmark.psd"); checkImg.color = White; checkImg.raycastTarget = false;

            var pcv = root.AddComponent<ProfileCardView>();
            pcv.nameText = nameText;
            pcv.dateText = dateText;
            pcv.avatarImage = avatarImg;
            pcv.selectedBorder = border.gameObject;
            pcv.checkMark = check.gameObject;
            pcv.selectButton = selectBtn;
            pcv.editButton = editBtn;
            pcv.deleteButton = deleteBtn;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Button MakeCardButton(string name, Transform parent, string label, Color textColor, TMP_FontAsset font, Vector2 pos, Vector2 size)
        {
            var rect = ChildRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
            rect.pivot = new Vector2(1, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = CardBtnBg;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, label, 32, textColor, font);
            StretchFull(t.rectTransform, 6);
            return btn;
        }

        // ===== 공통 UI 헬퍼 =====
        static Button MakeButton(string name, Transform parent, string label, int fontSize, Color bg, Color textColor, TMP_FontAsset font)
        {
            var rect = MakeRect(name, parent, Vector2.zero, new Vector2(300, 100));
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = bg;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, label, fontSize, textColor, font);
            StretchFull(t.rectTransform, 6);
            return btn;
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

        static RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var rect = ChildRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void Place(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        // 가운데 모달: 반투명 오버레이 + 둥근 흰 카드
        static GameObject MakePopup(string name, Transform parent, Vector2 cardSize, out RectTransform card)
        {
            var overlay = SceneBuilderUtils.CreatePanel(name, parent);
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            card = MakeRect("Card", overlay.transform, Vector2.zero, cardSize);
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = White;
            return overlay;
        }

        static Button MakeIconButton(string name, Transform parent, string glyph, Vector2 pos, Vector2 size, TMP_FontAsset font)
        {
            var rect = MakeRect(name, parent, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // 투명 (레이캐스트만)
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, glyph, 44, Slate, font);
            StretchFull(t.rectTransform, 6);
            return btn;
        }

        static TMP_InputField MakeInput(string name, Transform parent, TMP_DefaultControls.Resources res,
            TMP_FontAsset font, string placeholder, Vector2 pos, Vector2 size)
        {
            var go = TMP_DefaultControls.CreateInputField(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), pos, size);

            var img = go.GetComponent<Image>();
            if (img != null) { img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; img.color = CardBtnBg; }

            var input = go.GetComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.textComponent.fontSize = 42;
            input.textComponent.color = Slate;
            input.textComponent.alignment = TextAlignmentOptions.Center;
            if (input.placeholder is TMP_Text ph) { ph.text = placeholder; ph.fontSize = 42; ph.alignment = TextAlignmentOptions.Center; }
            ApplyFont(go, font);
            return input;
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

        static void ApplyFont(GameObject go, TMP_FontAsset font)
        {
            if (font == null) return;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.font = font;
        }

        static void AnchorLeft(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void StretchFull(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : Builtin("UI/Skin/UISprite.psd");
        }

        // ===== 아바타 라이브러리 (Resources) =====
        static void EnsureAvatarLibrary()
        {
            const string dir = "Assets/_Project/Resources";
            const string path = dir + "/AvatarLibrary.asset";
            EnsureFolder(dir);

            var lib = AssetDatabase.LoadAssetAtPath<AvatarLibrary>(path);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AvatarLibrary>();
                AssetDatabase.CreateAsset(lib, path);
            }

            var sprites = new List<Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/Avatars" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                EnsureSprite(p);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sp != null) sprites.Add(sp);
            }
            lib.sprites = sprites.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
        }

        static void EnsureSprite(string p)
        {
            if (AssetImporter.GetAtPath(p) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.SaveAndReimport();
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null) font = SceneBuilderUtils.GetKoreanFont();
            return font;
        }
    }
}
