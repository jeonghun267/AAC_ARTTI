using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.UI;
using static Artti.Editor.ProfileAvatarStepBuilder;

namespace Artti.Editor
{
    // 사용자 선택 화면 (다크 네온). 가로 1920x1080 Stage 기준, 좌상단 원점(y 아래가 -).
    // 에셋: Art/UI/Profile_C/
    //   48 배경(1672x941, 점원 캐릭터 / 제목 "사용자를 선택하세요" / 오른쪽 안내 패널이 그려져 있음)
    //   49 빈 카드 틀(971x1619)   50 "프로필 설정" 버튼
    // 좌표는 시안(1672x941)을 1920 폭으로 환산(x1.1483)한 값.
    // 구성: 왼쪽 메뉴(사용자 선택=지금 화면 / 프로필 설정) + 가운데 가로 카드 줄(< >로 넘김, 맨 끝 "새 사용자")
    //       + 다음 + 우상단 닫기. 시안의 STEP 표시, 분류 탭, 랜덤 선택, 완료 메뉴는 넣지 않는다.
    public static class ProfileSelectSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";
        const string CardPrefabPath = "Assets/_Project/Prefabs/Profile/ProfileCard.prefab";
        const string SelDir = "Assets/_Project/Art/UI/Profile_C/";

        // 카드: 180x300, 간격 10 (피치 190), 한 화면 5장. 첫 카드 가운데 x 708.5, 줄 가운데 y -631
        const float CardW = 180f, CardH = 300f, CardGap = 10f, RowPad = 20f;
        const int VisibleCards = 5;
        static readonly Vector2 RowCenter = new Vector2(1088f, -631f);
        const float RowH = 400f;   // 선택 카드가 1.08배로 커져도 잘리지 않게 여유
        static readonly Rect FrameBody = Rect.MinMaxRect(129, 217, 842, 1401);   // 49 본체(알파>200)
        const float FrameImgW = 971f, FrameImgH = 1619f;
        static readonly Rect MenuBtnBody = Rect.MinMaxRect(122, 340, 1328, 729);  // 50 본체
        static readonly Rect CheckBody = Rect.MinMaxRect(258, 257, 997, 987);     // profiles/33 (체크)
        static readonly Rect CloseBody = Rect.MinMaxRect(423, 421, 832, 824);     // profiles/18 (X)

        static readonly Color White     = new Color(0.96f, 0.97f, 1.00f, 1.00f);
        static readonly Color TextSoft  = new Color(0.76f, 0.82f, 0.98f, 0.92f);
        static readonly Color Glow      = new Color(0.45f, 0.65f, 1.00f, 0.60f);
        static readonly Color MenuFill  = new Color(0.10f, 0.22f, 0.62f, 0.92f);
        static readonly Color MenuLine  = new Color(0.55f, 0.75f, 1.00f, 1.00f);
        static readonly Color Danger    = new Color(0.90f, 0.28f, 0.34f, 0.95f);
        static readonly Color PopupFill = new Color(0.07f, 0.11f, 0.26f, 0.98f);
        static readonly Color FieldFill = new Color(0.12f, 0.18f, 0.40f, 1.00f);
        static readonly Color Neutral   = new Color(0.22f, 0.30f, 0.55f, 1.00f);

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

            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution, CanvasScaler.ScreenMatchMode.Expand);
            var canvas = canvasGo.transform;
            var font = LoadFont();
            EnsureAvatarLibrary();
            var cardPrefab = EnsureProfileCardPrefab(font);

            // ===== 배경 (48) =====
            var bg = ChildRect("Background", canvas);
            StretchFull(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = LoadSprite(SelDir + "48.png", 2048);
            bgImg.raycastTarget = false;
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1672f / 941f;

            var stage = ChildRect("Stage", canvas);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = ReferenceResolution;

            // ===== 우상단: 문구 + 닫기 =====
            AddText(stage, "TopRightText", "당신의 일상에, ARTTI", font, 22, White, false,
                new Vector2(1408f, -42f), new Vector2(400f, 36f), TextAlignmentOptions.Right);
            var closeBtn = MakeImageButton("CloseBtn", stage, Dir + "18.png", CloseBody, 1254f, 1254f, new Vector2(1864f, -60f), 48f);

            // ===== 왼쪽 메뉴: 사용자 선택(지금 화면, 켜진 상태) / 프로필 설정(50) =====
            BuildCurrentMenu(stage, font, new Vector2(186f, -272f), new Vector2(244f, 88f));
            var settingsBtn = MakeImageButton("ProfileSettingsBtn", stage, SelDir + "50.png", MenuBtnBody, 1448f, 1086f,
                new Vector2(186f, -377f), 244f);

            // ===== 가운데: 카드 줄 (뷰포트 마스크 + 가로 레이아웃) =====
            float rowW = CardW * VisibleCards + CardGap * (VisibleCards - 1) + RowPad * 2f;
            var viewport = ChildRect("CardViewport", stage);
            viewport.anchorMin = viewport.anchorMax = new Vector2(0f, 1f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = RowCenter;
            viewport.sizeDelta = new Vector2(rowW, RowH);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = ChildRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = CardGap;
            hlg.padding = new RectOffset((int)RowPad, (int)RowPad, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var addCard = BuildAddCard(content, font, out var addBtn);

            // < > (한 칸씩 넘김. 카드가 화면보다 많을 때만 보인다)
            float rowLeft = RowCenter.x - rowW / 2f, rowRight = RowCenter.x + rowW / 2f;
            var prevBtn = ArrowButton("PrevBtn", stage, font, "<", new Vector2(rowLeft - 26f, RowCenter.y + 10f));
            var nextPageBtn = ArrowButton("NextPageBtn", stage, font, ">", new Vector2(rowRight + 26f, RowCenter.y + 10f));

            // ===== 다음 (17) =====
            var nextBtn = MakeImageButton("NextBtn", stage, Dir + "17.png", NextBody, PillW, PillH, new Vector2(1442f, -886f), 330f);

            // ===== 삭제 확인 팝업 (가운데 모달, "동의한다" 타이핑 확인) =====
            var deletePopup = BuildDeletePopup(canvas, font, out var delMsg, out var delInput,
                out var delConfirm, out var delCancel, out var delX);

            // ===== View 와이어링 =====
            var view = canvasGo.AddComponent<ProfileSelectView>();
            var so = new SerializedObject(view);
            so.FindProperty("cardContainer").objectReferenceValue = content;
            so.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            so.FindProperty("addCard").objectReferenceValue = addCard;
            so.FindProperty("addCardButton").objectReferenceValue = addBtn;
            so.FindProperty("prevButton").objectReferenceValue = prevBtn;
            so.FindProperty("nextPageButton").objectReferenceValue = nextPageBtn;
            so.FindProperty("visibleCount").intValue = VisibleCards;
            so.FindProperty("cardPitch").floatValue = CardW + CardGap;
            so.FindProperty("nextButton").objectReferenceValue = nextBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("backButton").objectReferenceValue = closeBtn;
            so.FindProperty("deletePopup").objectReferenceValue = deletePopup;
            so.FindProperty("deleteMessageText").objectReferenceValue = delMsg;
            so.FindProperty("deleteInput").objectReferenceValue = delInput;
            so.FindProperty("deleteConfirmButton").objectReferenceValue = delConfirm;
            so.FindProperty("deleteCancelButton").objectReferenceValue = delCancel;
            so.FindProperty("deleteCloseButton").objectReferenceValue = delX;
            so.FindProperty("proceedScene").stringValue = "MainScene";
            so.FindProperty("backScene").stringValue = "MainScene";
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileSelectSceneBuilder] 완료 (배경 48 + 왼쪽 메뉴 + 카드 줄(49) + 다음 + 삭제 팝업)");
        }

        // 지금 화면 메뉴(사용자 선택): 이미지가 없어 50과 같은 크기의 켜진 알약을 코드로 그린다.
        static void BuildCurrentMenu(Transform stage, TMP_FontAsset font, Vector2 center, Vector2 size)
        {
            RoundedBox("UserMenuGlow", stage, center, size + new Vector2(14f, 14f), new Color(Glow.r, Glow.g, Glow.b, 0.35f));
            RoundedBox("UserMenuLine", stage, center, size + new Vector2(4f, 4f), MenuLine);
            var box = RoundedBox("UserMenu", stage, center, size, MenuFill);
            var icon = ChildRect("Icon", box);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(52f, 0f);
            icon.sizeDelta = new Vector2(40f, 40f);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = LoadSprite(Dir + "icon_name.png", 256);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            var t = AddText(box, "Label", "사용자 선택", font, 28, White, true,
                new Vector2(88f, 0f), new Vector2(size.x - 100f, size.y), TextAlignmentOptions.Left);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.rectTransform.anchoredPosition = new Vector2(88f, 0f);
        }

        // ===== 카드 =====

        // 카드 틀(49) + 둥근 마스크 안 아바타 + 이름 + 한마디 소개. 선택 빛/체크/삭제 칩은 선택 시에만 켜진다.
        static GameObject EnsureProfileCardPrefab(TMP_FontAsset font)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) != null)
                AssetDatabase.DeleteAsset(CardPrefabPath);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Profile");

            var root = CardRoot("ProfileCard", out var selectBtn);

            var border = RoundedBox("SelectedBorder", root, new Vector2(CardW / 2f, -CardH / 2f), new Vector2(CardW + 10f, CardH + 10f), Glow);
            border.GetComponent<Image>().pixelsPerUnitMultiplier = 2f;
            CardFrame(root);

            // 아바타: 틀 안쪽 위(y 5~178)를 둥글게 잘라 채운다. 글자는 틀 아래 받침 타원(y 258~)에 걸리지 않게 250 위에서 끝낸다.
            var mask = RoundedBox("AvatarMask", root, new Vector2(CardW / 2f, -91.5f), new Vector2(CardW - 12f, 173f), Color.white);
            var maskImg = mask.GetComponent<Image>();
            maskImg.pixelsPerUnitMultiplier = 2.4f;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var av = ChildRect("Avatar", mask);
            av.anchorMin = av.anchorMax = av.pivot = new Vector2(0.5f, 1f);
            av.anchoredPosition = new Vector2(0f, -4f);
            av.sizeDelta = new Vector2(176f, 176f);
            var avatarImg = av.gameObject.AddComponent<Image>();
            avatarImg.preserveAspect = true;
            avatarImg.raycastTarget = false;

            var nameText = AddText(root, "Name", "이름", font, 26, White, true,
                new Vector2(8f, -182f), new Vector2(CardW - 16f, 34f), TextAlignmentOptions.Center);
            var introText = AddText(root, "Intro", "한마디 소개", font, 15, TextSoft, false,
                new Vector2(12f, -216f), new Vector2(CardW - 24f, 36f), TextAlignmentOptions.Top);
            introText.textWrappingMode = TextWrappingModes.Normal;
            introText.overflowMode = TextOverflowModes.Ellipsis;

            // 우상단 체크 (33)
            var check = new GameObject("CheckMark", typeof(RectTransform));
            check.transform.SetParent(root, false);
            var checkRt = (RectTransform)check.transform;
            checkRt.anchorMin = checkRt.anchorMax = new Vector2(0f, 1f);
            checkRt.pivot = new Vector2(0.5f, 0.5f);
            checkRt.anchoredPosition = new Vector2(CardW - 24f, -24f);
            float checkScale = 30f / CheckBody.width;
            checkRt.sizeDelta = new Vector2(1254f, 1254f) * checkScale;
            var checkImg = check.AddComponent<Image>();
            checkImg.sprite = LoadSprite(Dir + "33.png", 512);
            checkImg.preserveAspect = true;
            checkImg.raycastTarget = false;

            // 좌상단 삭제 칩
            var del = RoundedBox("DeleteChip", root, new Vector2(34f, -24f), new Vector2(50f, 28f), Danger);
            del.GetComponent<Image>().pixelsPerUnitMultiplier = 3f;
            var delImg = del.GetComponent<Image>();
            delImg.raycastTarget = true;
            var deleteBtn = del.gameObject.AddComponent<Button>();
            deleteBtn.targetGraphic = delImg;
            var delText = AddText(del, "Text", "삭제", font, 15, White, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            StretchFull(delText.rectTransform);

            var pcv = root.gameObject.AddComponent<ProfileCardView>();
            pcv.nameText = nameText;
            pcv.introText = introText;
            pcv.avatarImage = avatarImg;
            pcv.selectedBorder = border.gameObject;
            pcv.checkMark = check;
            pcv.selectButton = selectBtn;
            pcv.deleteButton = deleteBtn;
            pcv.SetSelected(false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, CardPrefabPath);
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        // "새 사용자" 카드: 같은 틀 + 큰 + 표시. 누르면 프로필 등록으로.
        static RectTransform BuildAddCard(Transform content, TMP_FontAsset font, out Button button)
        {
            var root = CardRoot("AddCard", out button);
            root.SetParent(content, false);
            CardFrame(root);
            var plus = AddText(root, "Plus", "+", font, 110, new Color(0.62f, 0.78f, 1f, 0.95f), false,
                new Vector2(0f, -20f), new Vector2(CardW, 150f), TextAlignmentOptions.Center);
            plus.raycastTarget = false;
            AddText(root, "Name", "새 사용자", font, 26, White, true,
                new Vector2(8f, -182f), new Vector2(CardW - 16f, 34f), TextAlignmentOptions.Center);
            var sub = AddText(root, "Intro", "눌러서 등록하기", font, 15, TextSoft, false,
                new Vector2(12f, -216f), new Vector2(CardW - 24f, 36f), TextAlignmentOptions.Top);
            sub.textWrappingMode = TextWrappingModes.Normal;
            return root;
        }

        // 카드 루트: 투명 클릭 영역 + 레이아웃 크기. 좌상단 기준 자식 좌표(y 아래가 -)를 쓰도록 크기만 정한다.
        static RectTransform CardRoot(string name, out Button button)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(CardW, CardH);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = CardW;
            le.preferredHeight = CardH;
            return rt;
        }

        // 49 틀: 본체가 카드(180x300)에 맞도록 이미지 전체를 키워 가운데에 놓는다.
        static void CardFrame(RectTransform root)
        {
            float s = CardW / FrameBody.width;
            var rt = ChildRect("Frame", root);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(CardW / 2f, -CardH / 2f)
                + new Vector2((FrameImgW / 2f - FrameBody.center.x) * s, (FrameBody.center.y - FrameImgH / 2f) * s);
            rt.sizeDelta = new Vector2(FrameImgW, FrameImgH) * s;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(SelDir + "49.png", 1024);
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        static Button ArrowButton(string name, Transform stage, TMP_FontAsset font, string glyph, Vector2 center)
        {
            var rt = ChildRect(name, stage);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(64f, 110f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = AddText(rt, "Glyph", glyph, font, 64, MenuLine, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            StretchFull(t.rectTransform);
            return btn;
        }

        // ===== 삭제 확인 팝업 (다크) =====
        static GameObject BuildDeletePopup(Transform canvas, TMP_FontAsset font, out TMP_Text message, out TMP_InputField input,
            out Button confirm, out Button cancel, out Button close)
        {
            var overlay = ChildRect("DeleteConfirmPopup", canvas);
            StretchFull(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var box = ChildRect("Center", overlay);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(860f, 460f);
            RoundedBox("Line", box, new Vector2(430f, -230f), new Vector2(866f, 466f), MenuLine);
            RoundedBox("Card", box, new Vector2(430f, -230f), new Vector2(860f, 460f), PopupFill);

            message = AddText(box, "Message", "정말 삭제할까요?", font, 32, White, true,
                new Vector2(50f, -50f), new Vector2(760f, 130f), TextAlignmentOptions.Center);
            message.textWrappingMode = TextWrappingModes.Normal;

            RoundedBox("InputBox", box, new Vector2(430f, -245f), new Vector2(620f, 84f), FieldFill);
            input = MakeInput(box, font, "동의한다", new Vector2(430f, -245f), new Vector2(620f, 84f));

            cancel = PopupButton("CancelButton", box, font, "취소", Neutral, new Vector2(250f, -370f));
            confirm = PopupButton("ConfirmDeleteButton", box, font, "삭제", Danger, new Vector2(610f, -370f));

            close = MakeImageButton("Close", box, Dir + "18.png", CloseBody, 1254f, 1254f, new Vector2(812f, -48f), 44f);

            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        static Button PopupButton(string name, Transform parent, TMP_FontAsset font, string label, Color color, Vector2 center)
        {
            var rt = RoundedBox(name, parent, center, new Vector2(250f, 88f), color);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = AddText(rt, "Text", label, font, 32, White, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            StretchFull(t.rectTransform);
            return btn;
        }

        static TMP_InputField MakeInput(Transform parent, TMP_FontAsset font, string placeholder, Vector2 center, Vector2 size)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources
            {
                standard = Builtin("UI/Skin/UISprite.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
            });
            go.name = "DeleteInput";
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            if (img != null) img.color = new Color(1f, 1f, 1f, 0f);

            var input = go.GetComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = White;
            input.customCaretColor = true;
            input.textComponent.fontSize = 34;
            input.textComponent.color = White;
            input.textComponent.alignment = TextAlignmentOptions.Center;
            if (input.placeholder is TMP_Text ph)
            {
                ph.text = placeholder;
                ph.fontSize = 34;
                ph.color = new Color(TextSoft.r, TextSoft.g, TextSoft.b, 0.5f);
                ph.alignment = TextAlignmentOptions.Center;
            }
            if (font != null) foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.font = font;
            return input;
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

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
            // 프로필 등록 1단계(아바타 선택)의 새 아바타. 여기서 빠지면 이 빌더를 돌릴 때 라이브러리에서 사라진다.
            foreach (var ap in ProfileAvatarStepBuilder.AvatarPaths())
            {
                EnsureSprite(ap);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(ap);
                if (sp != null && !sprites.Contains(sp)) sprites.Add(sp);
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
