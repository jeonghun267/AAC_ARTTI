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

        // 새 글래스/파스텔 디자인 (_selectfull.png)
        static readonly Color SkyLav      = new Color32(232, 230, 248, 255); // 하늘 (연라벤더)
        static readonly Color TitlePurple = new Color32(122, 92, 210, 255);  // 타이틀 보라
        static readonly Color AddPurple   = new Color32(150, 120, 230, 255); // 새로등록 버튼 보라

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

            // 배경: 하늘(연라벤더) + 파스텔 글로우 + 반짝이 별 (ProfileCreate와 톤 통일)
            var sky = SceneBuilderUtils.CreatePanel("Sky", canvasGo.transform);
            sky.AddComponent<Image>().color = SkyLav;
            BuildPastelGlow(canvasGo.transform);
            BuildSparkles(canvasGo.transform);

            // 뒤로 (좌상단)
            // 메인에서 프로필 버튼으로 들어오므로 뒤로가기는 메인으로
            SceneBuilderUtils.CreateBackButton("MainScene", canvasGo.transform, "← 뒤로");

            // + 새로 등록하기 (우상단)
            var addBtn = MakeButton("AddButton", canvasGo.transform, "+ 새로 등록하기", 36, AddPurple, White, font);
            var addRect = addBtn.GetComponent<RectTransform>();
            addRect.anchorMin = addRect.anchorMax = new Vector2(1, 1);
            addRect.pivot = new Vector2(1, 1);
            addRect.anchoredPosition = new Vector2(-40, -40);
            addRect.sizeDelta = new Vector2(380, 96);

            // ===== 목록 루트 (제목 + 스크롤) =====
            var listRoot = MakeRect("ListRoot", canvasGo.transform, Vector2.zero, Vector2.zero);
            StretchFull(listRoot, 0);

            var listTitle = MakeText("Title", listRoot.transform, "사용자를 선택하세요", 72, TitlePurple, font);
            listTitle.rectTransform.anchorMin = listTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            listTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            listTitle.rectTransform.anchoredPosition = new Vector2(0, -60);
            listTitle.rectTransform.sizeDelta = new Vector2(1000, 100);

            var listSub = MakeText("Subtitle", listRoot.transform, "프로필을 선택하면 바로 시작할 수 있어요", 34, DateGray, font);
            listSub.rectTransform.anchorMin = listSub.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            listSub.rectTransform.pivot = new Vector2(0.5f, 1f);
            listSub.rectTransform.anchoredPosition = new Vector2(0, -148);
            listSub.rectTransform.sizeDelta = new Vector2(1000, 56);

            // 카드 300 높이 → 2장(약 644)이 잘리지 않게 뷰포트를 키움. 제목/부제와 하단 팁바 사이에 배치.
            var scroll = MakeRect("CardScroll", listRoot.transform, new Vector2(0, 0), new Vector2(1340, 700));
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

            // ===== 하단 팁바 (전구 + 문구) =====
            MakeTipBar(canvasGo.transform, font);

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
            so.FindProperty("proceedScene").stringValue = "MainScene"; // 프로필 선택 → 메인
            so.FindProperty("backScene").stringValue = "MainScene";
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileSelectSceneBuilder] 완료 (글래스 ProBack + 풀 + 카드: 모드줄/버튼아래/기본칩/셰브론 + 하단 팁바)");
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
            rootRect.sizeDelta = new Vector2(1100, 400);
            var rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(0, 0, 0, 0); // 투명 레이캐스트 (카드 선택)
            var selectBtn = root.AddComponent<Button>();
            selectBtn.targetGraphic = rootImg;
            var le = root.AddComponent<LayoutElement>();
            le.preferredHeight = 400;

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
            bodyImg.color = new Color(1f, 1f, 1f, 0.55f); bodyImg.raycastTarget = false; // 프로스트 글래스

            // 아바타 박스 (연한 틴트 둥근 사각 + 큰 캐릭터)
            var avatarBox = ChildRect("AvatarBox", body.transform);
            avatarBox.anchorMin = avatarBox.anchorMax = new Vector2(0, 0.5f);
            avatarBox.pivot = new Vector2(0, 0.5f);
            avatarBox.anchoredPosition = new Vector2(46, 0);
            avatarBox.sizeDelta = new Vector2(184, 184);
            var boxImg = avatarBox.gameObject.AddComponent<Image>();
            boxImg.sprite = Rounded(); boxImg.type = Image.Type.Sliced; boxImg.pixelsPerUnitMultiplier = 1f;
            boxImg.color = new Color32(237, 239, 250, 255); // 연한 라벤더 틴트
            boxImg.raycastTarget = false;

            var avatar = ChildRect("Avatar", avatarBox.transform);
            StretchFull(avatar, 10);
            var avatarImg = avatar.gameObject.AddComponent<Image>();
            avatarImg.preserveAspect = true; avatarImg.raycastTarget = false;

            // 별 배지 (아바타 박스 좌상단 위에 살짝 걸침)
            var star = ChildRect("StarBadge", avatarBox.transform);
            star.anchorMin = star.anchorMax = new Vector2(0, 1);
            star.pivot = new Vector2(0.5f, 0.5f);
            star.anchoredPosition = new Vector2(12, -12);
            star.sizeDelta = new Vector2(58, 58);
            var starImg = star.gameObject.AddComponent<Image>();
            starImg.sprite = LoadProfileSprite("deco_star_purple.png");
            starImg.preserveAspect = true; starImg.raycastTarget = false;

            // 이름 + "기본 프로필" 칩 (칩은 활성 프로필에만)
            var nameText = MakeText("Name", body.transform, "닉네임", 48, Slate, font);
            nameText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(nameText.rectTransform, new Vector2(268, 140), new Vector2(340, 66));

            var defaultPill = MakePill("DefaultPill", body.transform, "기본 프로필", font, new Vector2(556, 144), new Vector2(196, 52));

            // 날짜 / 모드 (좌측 정렬 두 줄)
            var dateText = MakeText("Date", body.transform, "마지막 사용: 오늘", 30, DateGray, font);
            dateText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(dateText.rectTransform, new Vector2(270, 80), new Vector2(680, 46));

            var modeText = MakeText("Mode", body.transform, "가장 많이 사용한 모드: 말하기 훈련", 30, DateGray, font);
            modeText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(modeText.rectTransform, new Vector2(270, 26), new Vector2(760, 46));

            // 수정 / 삭제 (모드 줄 아래, 좌측) - 이미지 버튼 1.5배 (88=수정, 87=삭제)
            var editBtn = MakeImageCardButton("EditButton", body.transform, LoadProfileSprite("btn_edit.png"),
                new Vector2(268, -90), new Vector2(278, 180));
            var deleteBtn = MakeImageCardButton("DeleteButton", body.transform, LoadProfileSprite("btn_delete.png"),
                new Vector2(566, -90), new Vector2(272, 180));

            // 체크 (좌측 상단) — 카드 안쪽으로 들여서 스크롤 뷰포트 마스크에 안 잘리게
            var check = ChildRect("CheckMark", root.transform);
            check.anchorMin = check.anchorMax = new Vector2(0, 1);
            check.pivot = new Vector2(0.5f, 0.5f);
            check.anchoredPosition = new Vector2(40, -40);
            check.sizeDelta = new Vector2(56, 56);
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
            pcv.modeText = modeText;
            pcv.avatarImage = avatarImg;
            pcv.starBadge = starImg;
            pcv.defaultPill = defaultPill;
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

        // 좌측 정렬 카드 버튼 (수정/삭제를 텍스트 줄 아래에 둘 때)
        static Button MakeCardButtonLeft(string name, Transform parent, string label, Color textColor, TMP_FontAsset font, Vector2 pos, Vector2 size)
        {
            var rect = ChildRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
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

        // "기본 프로필" 칩 (연한 블루 알약). 기본 숨김 → 활성 프로필에만 표시.
        static GameObject MakePill(string name, Transform parent, string label, TMP_FontAsset font, Vector2 pos, Vector2 size)
        {
            var rect = ChildRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = new Color32(224, 232, 252, 255);
            img.raycastTarget = false;
            var t = MakeText("Text", rect, label, 26, BtnBlue, font);
            StretchFull(t.rectTransform, 6);
            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        // 하단 팁바 (반투명 흰 알약 + 전구 + 두 줄 문구)
        static void MakeTipBar(Transform parent, TMP_FontAsset font)
        {
            var bar = ChildRect("TipBar", parent);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0, 56);
            bar.sizeDelta = new Vector2(900, 132);
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.sprite = Rounded(); barImg.type = Image.Type.Sliced; barImg.pixelsPerUnitMultiplier = 1f;
            barImg.color = new Color(1f, 1f, 1f, 0.78f); barImg.raycastTarget = false;

            var bulb = ChildRect("Bulb", bar.transform);
            bulb.anchorMin = bulb.anchorMax = new Vector2(0, 0.5f);
            bulb.pivot = new Vector2(0, 0.5f);
            bulb.anchoredPosition = new Vector2(-3, 3);   // 손배치 (바 좌측끝)
            bulb.sizeDelta = new Vector2(139, 130);        // scale 1.548 흡수
            var bulbImg = bulb.gameObject.AddComponent<Image>();
            bulbImg.sprite = LoadProfileSprite("icon_bulb.png");
            bulbImg.preserveAspect = true; bulbImg.raycastTarget = false;

            var tip = MakeText("TipText", bar.transform, "더 나은 의사소통을 위해\n매일 조금씩 연습해 보세요!", 32, Slate, font);
            tip.alignment = TextAlignmentOptions.Left;
            tip.textWrappingMode = TextWrappingModes.Normal;
            AnchorLeft(tip.rectTransform, new Vector2(168, 0), new Vector2(540, 110));

            // 말풍선 (글 오른쪽 끝, 팁바 우측). 이미지 308x251
            var chat = ChildRect("ChatBubble", bar.transform);
            chat.anchorMin = chat.anchorMax = new Vector2(1, 0.5f);
            chat.pivot = new Vector2(1, 0.5f);
            chat.anchoredPosition = new Vector2(-17, -5);  // 손배치 (바 우측끝)
            chat.sizeDelta = new Vector2(157, 127);         // scale 1.477 흡수
            var chatImg = chat.gameObject.AddComponent<Image>();
            chatImg.sprite = LoadProfileSprite("icon_chat.png");
            chatImg.preserveAspect = true; chatImg.raycastTarget = false;
        }

        // ===== 배경 효과 (하늘 위, ProfileCreate와 톤 통일) =====

        // 전체화면 stretch 컨테이너
        static RectTransform FullContainer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        // 장식 이미지 (레이캐스트 X, 비율 유지)
        static Image AddDecor(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var rect = MakeRect(name, parent, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
            return img;
        }

        // 파스텔 글로우: 화면 둘레에 따뜻한 파스텔 빛덩이 (PastelGlow 와이어링)
        static void BuildPastelGlow(Transform parent)
        {
            var container = FullContainer("PastelGlow", parent);
            var glow = SceneBuilderUtils.EnsureGlowSprite();
            var defs = new (Vector2 pos, float size, Color color)[]
            {
                (new Vector2(-780f, 360f), 520f, new Color(1.00f, 0.85f, 0.72f, 0.18f)),
                (new Vector2(790f, 400f), 480f, new Color(1.00f, 0.78f, 0.86f, 0.18f)),
                (new Vector2(-800f, -360f), 560f, new Color(0.90f, 0.80f, 1.00f, 0.18f)),
                (new Vector2(810f, -380f), 500f, new Color(1.00f, 0.92f, 0.74f, 0.18f)),
                (new Vector2(-860f, 20f), 420f, new Color(0.82f, 0.96f, 0.90f, 0.15f)),
                (new Vector2(860f, 40f), 440f, new Color(1.00f, 0.84f, 0.78f, 0.16f)),
            };
            var list = new List<Graphic>();
            foreach (var d in defs)
            {
                var rt = MakeRect("Glow", container, d.pos, new Vector2(d.size, d.size));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = glow; img.type = Image.Type.Simple; img.raycastTarget = false;
                img.color = d.color;
                list.Add(img);
            }
            var pg = container.gameObject.AddComponent<PastelGlow>();
            WireGraphicArray(pg, "orbs", list);
        }

        // 반짝이 별: 배경 측면/코너에 4색 별을 흩뿌려 반짝임 (SparkleField 와이어링)
        static void BuildSparkles(Transform parent)
        {
            var container = FullContainer("Sparkles", parent);
            var defs = new (string sprite, Vector2 pos, float size)[]
            {
                ("deco_star_yellow.png", new Vector2(-760f, 200f), 70f),
                ("deco_star_blue.png",   new Vector2(760f, 260f), 60f),
                ("deco_star_purple.png", new Vector2(-820f, -120f), 50f),
                ("deco_star_orange.png", new Vector2(820f, -200f), 64f),
                ("deco_star_yellow.png", new Vector2(700f, 430f), 44f),
                ("deco_star_blue.png",   new Vector2(-700f, 440f), 48f),
                ("deco_star_purple.png", new Vector2(840f, -380f), 56f),
                ("deco_star_orange.png", new Vector2(-840f, -360f), 52f),
            };
            var list = new List<Graphic>();
            foreach (var d in defs)
            {
                var rt = MakeRect("Star", container, d.pos, new Vector2(d.size, d.size));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = LoadProfileSprite(d.sprite); img.preserveAspect = true; img.raycastTarget = false;
                list.Add(img);
            }
            var sf = container.gameObject.AddComponent<SparkleField>();
            WireGraphicArray(sf, "sparkles", list);
        }

        static void WireGraphicArray(Component comp, string prop, List<Graphic> list)
        {
            var so = new SerializedObject(comp);
            var arr = so.FindProperty(prop);
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedProperties();
        }

        // 카드용 이미지 버튼 (좌측 정렬, 스프라이트 그대로)
        static Button MakeImageCardButton(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var rect = ChildRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }

        // 풀 (Home 스티커 재사용). anchor=(0,0) 좌하단 / (1,0) 우하단(flip).
        static void AddPlant(Transform parent, Vector2 anchor, Vector2 offset, float width, bool flip)
        {
            var rt = ChildRect("Plant", parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            var sp = LoadHomeSticker("plant_transparent.png");
            float ratio = (sp != null && sp.rect.width > 0) ? sp.rect.height / sp.rect.width : 1.63f;
            rt.sizeDelta = new Vector2(width, width * ratio);
            if (flip) rt.localScale = new Vector3(-1f, 1f, 1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
        }

        static Sprite LoadHomeSticker(string file)
        {
            string p = "Assets/_Project/Art/UI/Home/Stickers/" + file;
            EnsureSprite(p);
            return AssetDatabase.LoadAssetAtPath<Sprite>(p);
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

        // Art/UI/Profile/ 의 PNG를 Sprite로 로드 (미임포트면 강제 임포트 + 설정 보정)
        static Sprite LoadProfileSprite(string file)
        {
            string p = "Assets/_Project/Art/UI/Profile/" + file;
            if (AssetImporter.GetAtPath(p) == null)
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
            EnsureSprite(p);
            return AssetDatabase.LoadAssetAtPath<Sprite>(p);
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
