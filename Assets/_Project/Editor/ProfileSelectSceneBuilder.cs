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
    //   49 빈 카드 틀(971x1619)   50 "프로필 설정" 버튼   51 "사용자 선택"(지금 화면, 켜진 상태)
    //   52 삭제 확인 판(제목/안내/입력칸이 그려져 있음)  53 취소  54 삭제(카드의 삭제 칩도 이 이미지)  55 닫기(X)
    // 좌표는 시안(1672x941)을 1920 폭으로 환산(x1.1483)한 값.
    // 구성: 왼쪽 메뉴(사용자 선택=지금 화면 / 프로필 설정) + 가운데 가로 카드 줄(< >로 넘김, 맨 끝 "새 사용자")
    //       + 다음 + 우상단 닫기. 시안의 STEP 표시, 분류 탭, 랜덤 선택, 완료 메뉴는 넣지 않는다.
    // 변경점(2026-10-01): 사용자 삭제는 프로필 설정 화면으로 옮겼다. 이 화면에는 삭제 칩/삭제 팝업을 만들지 않는다.
    //   삭제 팝업(52~55) 만드는 코드는 프로필 설정 빌더가 그대로 쓴다(BuildDeletePopup).
    public static class ProfileSelectSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";
        const string CardPrefabPath = "Assets/_Project/Prefabs/Profile/ProfileCard.prefab";
        const string SelDir = "Assets/_Project/Art/UI/Profile_C/";

        // 카드: 180x300, 간격 10 (피치 190), 한 화면 3장(뷰포트 600). 줄 가운데 = 그림 속 패널(x 712~1332) 가운데 x 1022, y -631.
        // 카드가 3장보다 적으면 View가 가운데 정렬(새 사용자 카드가 가운데에서 시작해 왼쪽으로), 넘치면 끌어서 넘긴다.
        const float CardW = 180f, CardH = 300f, CardGap = 10f, RowPad = 20f;
        const int VisibleCards = 3;
        static readonly Vector2 RowCenter = new Vector2(1022f, -631f);
        static float RowW => CardW * VisibleCards + CardGap * (VisibleCards - 1) + RowPad * 2f;
        const float RowH = 400f;   // 선택 카드가 1.08배로 커져도 잘리지 않게 여유
        static readonly Rect FrameBody = Rect.MinMaxRect(129, 217, 842, 1401);   // 49 본체(알파>200)
        const float FrameImgW = 971f, FrameImgH = 1619f;
        static readonly Rect MenuBtnBody = Rect.MinMaxRect(122, 340, 1328, 729);  // 50 본체
        static readonly Rect UserMenuBody = Rect.MinMaxRect(113, 275, 1559, 726); // 51 본체(알파>200)
        const float UserMenuImgW = 1672f, UserMenuImgH = 941f;
        static readonly Rect CheckBody = Rect.MinMaxRect(258, 257, 997, 987);     // profiles/33 (체크)
        static readonly Rect CloseBody = Rect.MinMaxRect(423, 421, 832, 824);     // profiles/18 (X)

        // 삭제 확인 팝업: 52 판(1672x941)을 본체 폭 960(Stage 절반)으로 놓고, 52 px 좌표(위에서부터 y)로 배치한다.
        const float PopupImgW = 1672f, PopupImgH = 941f;
        const float PopupBodyW = 1456f;                                                   // 52 본체(108~1564)
        const float PopupWidth = 960f;
        static float PopupScale => PopupWidth / PopupBodyW;
        static readonly Rect PopupFieldPx = Rect.MinMaxRect(201, 434, 1469, 572);         // 52 입력칸 테두리
        static readonly RectInt FieldPatchPx = new RectInt(700, 470, 280, 75);            // 입력칸 속 '동의한다' 글자를 가릴 범위
        const string FieldPatchPath = SelDir + "52_field_clean.png";
        static readonly Rect CancelBody = Rect.MinMaxRect(93, 315, 1444, 705);            // 53 (1536x1024) 알파>250
        static readonly Rect DeleteBody = Rect.MinMaxRect(146, 327, 1389, 710);           // 54 (1536x1024) 알파>250
        static readonly Rect PopupCloseBody = Rect.MinMaxRect(377, 418, 891, 922);        // 55 (1254x1254) 알파>250
        const float PopupBtnH = 156f, PopupBtnGap = 70f, PopupBtnY = 692f;                // 52 px. 입력칸 아래 42 띄워 두 버튼을 같은 높이로
        static readonly Color HintColor = new Color(1.00f, 0.72f, 0.74f, 0.95f);

        static readonly Color White     = new Color(0.96f, 0.97f, 1.00f, 1.00f);
        static readonly Color TextSoft  = new Color(0.76f, 0.82f, 0.98f, 0.92f);
        static readonly Color Glow      = new Color(0.45f, 0.65f, 1.00f, 0.60f);

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

            // ===== 배경 (48, 화면 꽉 채움) + Stage (배경을 따라 같이 늘어남) =====
            var bg = BuildBackground(canvas);
            var stage = ChildRect("Stage", bg);
            AttachStage(bg, stage);

            // ===== 우상단: 문구 + 닫기 (화면 모서리 기준) =====
            var topRight = ChildRect("TopRight", canvas);
            StretchFull(topRight);
            var topText = AddText(topRight, "TopRightText", "당신의 일상에, ARTTI", font, 22, White, false,
                new Vector2(1408f, -42f), new Vector2(400f, 36f), TextAlignmentOptions.Right);
            AnchorTopRight(topText.rectTransform);
            var closeBtn = MakeImageButton("CloseBtn", topRight, Dir + "18.png", CloseBody, 1254f, 1254f, new Vector2(1864f, -60f), 48f);
            AnchorTopRight((RectTransform)closeBtn.transform);

            // ===== 왼쪽 메뉴: 사용자 선택(51, 지금 화면) / 프로필 설정(50) =====
            BuildUserMenu(stage, new Vector2(186f, -272f), 244f);
            var settingsBtn = MakeImageButton("ProfileSettingsBtn", stage, SelDir + "50.png", MenuBtnBody, 1448f, 1086f,
                new Vector2(186f, -377f), 244f);

            // ===== 가운데: 카드 줄 (뷰포트 마스크 + 가로 레이아웃) =====
            float rowW = RowW;
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

            var addCard = BuildAddCard(content, font, out var addBtn);   // 첫 자식 = 줄 맨 왼쪽
            var cardScroll = SetupCardScroll(viewport, content);

            // ===== 다음 (17) =====
            var nextBtn = MakeImageButton("NextBtn", stage, Dir + "17.png", NextBody, PillW, PillH, new Vector2(1442f, -886f), 330f);

            // ===== View 와이어링 =====
            var view = canvasGo.AddComponent<ProfileSelectView>();
            var so = new SerializedObject(view);
            so.FindProperty("cardContainer").objectReferenceValue = content;
            so.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            so.FindProperty("addCard").objectReferenceValue = addCard;
            so.FindProperty("addCardButton").objectReferenceValue = addBtn;
            so.FindProperty("cardScroll").objectReferenceValue = cardScroll;
            so.FindProperty("nextButton").objectReferenceValue = nextBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("backButton").objectReferenceValue = closeBtn;
            so.FindProperty("proceedScene").stringValue = "MainScene";
            so.FindProperty("backScene").stringValue = "MainScene";
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileSelectSceneBuilder] 완료 (배경 48 꽉 채움 + 왼쪽 메뉴(51/50) + 끌어서 넘기는 카드 줄(49) + 다음)");
        }

        // 배경(48): 화면을 빈틈 없이 꽉 채운다(양옆/위아래 빈 띠 없음). 48에는 로고/인용구/간판 글자가 가장자리 가까이
        // 그려져 있어, 그냥 Envelope로 채우면 16:10 태블릿에서 좌우가 106씩 잘려 글자가 잘린다.
        // CoverImageFitter로 비율을 최대 6%만 바꿔 잘리는 양을 줄이고(태블릿 좌우 46씩), 위아래를 자를 때(20:9 폰)는
        // 로고가 있는 위쪽을 덜 자르도록 기준점을 위(0.72)로 둔다.
        static RectTransform BuildBackground(Transform canvas)
        {
            var bg = ChildRect("Background", canvas);
            var img = bg.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(SelDir + "48.png", 2048);
            img.raycastTarget = false;
            bg.gameObject.AddComponent<CoverImageFitter>().Setup(1672f / 941f, 1.06f, new Vector2(0.5f, 0.72f));
            return bg;
        }

        // Stage를 배경의 자식으로 두고 배경과 같은 비율로 늘린다(CoverImageFitter follower). 배경이 화면을 채우려고
        // 커진 만큼 UI도 같이 커지고 움직여, 그림 속 로고/패널과 UI(메뉴, 카드 줄, 다음)의 자리가 16:9 시안 그대로 맞는다.
        static void AttachStage(RectTransform bg, RectTransform stage)
        {
            stage.SetParent(bg, false);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = ReferenceResolution;
            bg.GetComponent<CoverImageFitter>().SetFollower(stage, ReferenceResolution);
        }

        // Stage 좌상단 기준(0,1) 좌표를 화면 우상단 기준(1,1)으로 옮긴다(16:9에서는 같은 자리).
        // 폰(20:9)은 배경 위쪽이 잘려 Stage 맨 위가 화면 밖으로 나가므로, 우상단 문구/닫기는 Stage가 아니라 화면에 붙인다.
        // 배경을 화면에 꽉 채우면 태블릿에서 그림 속 간판이 Stage보다 위로 올라가, Stage 기준이면 문구/닫기가 간판을 덮는다.
        static void AnchorTopRight(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition -= new Vector2(ReferenceResolution.x, 0f);
        }

        // 카드 줄을 옆으로 끌어서 넘긴다(ScrollRect). 카드 사이 빈 곳을 끌어도 되도록 뷰포트에 투명 이미지를 깐다.
        // 콘텐츠는 왼쪽 기준(pivot x 0)이라 카드가 적어도 왼쪽부터 채워진다.
        static ScrollRect SetupCardScroll(RectTransform viewport, RectTransform content)
        {
            if (!viewport.TryGetComponent<Image>(out var hit)) hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            if (!viewport.TryGetComponent<ScrollRect>(out var scroll)) scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        // 사용자 선택(51): 지금 화면이라 켜진 상태 이미지만 놓는다(클릭 없음).
        // 루트 = 본체 크기(50 프로필 설정 버튼과 같은 폭), Visual = 글로우 여백 포함 이미지.
        static RectTransform BuildUserMenu(Transform stage, Vector2 bodyCenter, float bodyWidth)
        {
            float s = bodyWidth / UserMenuBody.width;
            var root = ChildRect("UserSelect", stage);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = bodyCenter;
            root.sizeDelta = new Vector2(bodyWidth, UserMenuBody.height * s);
            var visual = ChildRect("Visual", root);
            visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(0.5f, 0.5f);
            visual.sizeDelta = new Vector2(UserMenuImgW, UserMenuImgH) * s;
            visual.anchoredPosition = new Vector2((UserMenuImgW / 2f - UserMenuBody.center.x) * s,
                (UserMenuBody.center.y - UserMenuImgH / 2f) * s);
            var img = visual.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(SelDir + "51.png", 2048);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return root;
        }

        // 손배치가 있는 씬에 빌더 변경분만 다시 적용한다. 손배치한 위치/크기는 그대로 둔다.
        //   배경(48 꽉 채움) + Stage가 배경을 따라 늘어나게 / 사용자 선택(51, 지금 자리 유지) / 우상단 문구+닫기를 화면 모서리 기준으로 /
        //   카드 줄을 끌어서 넘기기로(< > 삭제, 새 사용자 카드 맨 앞, 한 화면 3장, 패널 가운데) / 삭제 확인 팝업(52~55) /
        //   카드 프리팹 삭제 칩(54, 캐릭터 아래 받침)
        [MenuItem("Artti/Fix/ProfileSelect - Apply Updates (keep hand placement)")]
        public static void ApplyUpdates()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ProfileSelectSceneBuilder] Play 모드에서는 실행하지 않는다."); return; }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ProfileSelect)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = SceneBuilderUtils.OpenScene(ScenePaths.ProfileSelect);
            }

            Transform canvas = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "[Canvas]") canvas = root.transform;
            var stage = canvas != null ? canvas.Find("Stage") as RectTransform : null;
            if (stage == null && canvas != null) stage = canvas.Find("Background/Stage") as RectTransform;
            if (stage == null) { Debug.LogError("[ProfileSelectSceneBuilder] [Canvas]/Stage 없음"); return; }

            // 배경 + Stage를 배경의 자식으로. 배경을 다시 만들기 전에 Stage를 캔버스로 빼 둔다(배경 자식이면 같이 지워진다).
            stage.SetParent(canvas, false);
            foreach (var n in new[] { "Background", "BackgroundStage", "BackgroundFill" })
                DestroyChildren(canvas, n);
            var bg = BuildBackground(canvas);
            bg.SetAsFirstSibling();
            AttachStage(bg, stage);

            // 사용자 선택(51): 코드로 그린 UserMenu 또는 이전에 적용한 UserSelect 자리를 그대로 쓴다
            var anchor = stage.Find("UserSelect");
            if (anchor == null) anchor = stage.Find("UserMenu");
            if (anchor is RectTransform place)
            {
                var glow = stage.Find("UserMenuGlow");
                int index = (glow != null ? glow : place).GetSiblingIndex();
                Vector2 center = place.anchoredPosition;
                float width = place.sizeDelta.x;
                foreach (var n in new[] { "UserMenuGlow", "UserMenuLine", "UserMenu", "UserSelect" })
                    DestroyChildren(stage, n);
                BuildUserMenu(stage, center, width).SetSiblingIndex(Mathf.Min(index, stage.childCount - 1));
            }
            else Debug.LogWarning("[ProfileSelectSceneBuilder] 사용자 선택 자리(UserMenu/UserSelect)를 찾지 못함");

            // 우상단: Stage 안에 있으면 화면 모서리 묶음(TopRight)으로 옮긴다. 묶음은 배경 바로 뒤(삭제 팝업보다 앞).
            var topRight = canvas.Find("TopRight") as RectTransform;
            if (topRight == null)
            {
                topRight = ChildRect("TopRight", canvas);
                StretchFull(topRight);
            }
            topRight.SetSiblingIndex(bg.GetSiblingIndex() + 1);
            foreach (var n in new[] { "TopRightText", "CloseBtn" })
            {
                if (!(stage.Find(n) is RectTransform item)) continue;
                item.SetParent(topRight, false);
                AnchorTopRight(item);
            }

            canvas.TryGetComponent<ProfileSelectView>(out var view);

            // 카드 줄: 끌어서 넘기기 + 새 사용자 카드 맨 앞, < > 삭제
            var viewport = stage.Find("CardViewport") as RectTransform;
            if (viewport != null)
            {
                // 한 화면 3장 + 그림 속 패널 가운데(요청으로 손배치 값 대신 빌더 값)
                viewport.anchoredPosition = RowCenter;
                viewport.sizeDelta = new Vector2(RowW, viewport.sizeDelta.y);
            }
            var content = viewport != null ? viewport.Find("Content") as RectTransform : null;
            if (content != null)
            {
                var scroll = SetupCardScroll(viewport, content);
                content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
                var addCard = content.Find("AddCard");
                if (addCard != null) addCard.SetAsFirstSibling();
                if (view != null)
                {
                    var so = new SerializedObject(view);
                    so.FindProperty("cardScroll").objectReferenceValue = scroll;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else Debug.LogWarning("[ProfileSelectSceneBuilder] CardViewport/Content 없음");
            DestroyChildren(stage, "PrevBtn");
            DestroyChildren(stage, "NextPageBtn");

            // 변경점: 삭제는 프로필 설정 화면으로 옮겼다. 예전에 만든 삭제 팝업과 카드 프리팹의 삭제 칩을 지운다.
            DestroyChildren(canvas, "DeleteConfirmPopup");
            var card = PrefabUtility.LoadPrefabContents(CardPrefabPath);
            try
            {
                if (card.transform.Find("DeleteChip") is Transform chip)
                {
                    if (card.TryGetComponent<ProfileCardView>(out var pcvOld)) pcvOld.deleteButton = null;
                    Object.DestroyImmediate(chip.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(card, CardPrefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(card); }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileSelectSceneBuilder] 변경분 적용 완료 (배경 꽉 채움 / 사용자 선택 51 / 우상단 모서리 / 끌어서 넘기기 / 삭제 팝업·칩 지움) - 손배치 유지");
        }

        static void DestroyChildren(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name == name) Object.DestroyImmediate(parent.GetChild(i).gameObject);
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

            var pcv = root.gameObject.AddComponent<ProfileCardView>();
            pcv.nameText = nameText;
            pcv.introText = introText;
            pcv.avatarImage = avatarImg;
            pcv.selectedBorder = border.gameObject;
            pcv.checkMark = check;
            pcv.selectButton = selectBtn;
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

        // ===== 삭제 확인 팝업 (52 판 + 53 취소 + 54 삭제 + 55 닫기) =====
        // 52에 제목/안내 문구/입력칸('동의한다' 안내 글자 포함)이 그려져 있어, 입력칸 위에는 투명한 입력창만 얹는다.
        // 글자를 치는 중이거나 틀렸다는 안내를 띄울 때는 52 속 '동의한다'를 가림 조각(FieldCover)으로 덮는다(View가 켜고 끈다).
        internal static GameObject BuildDeletePopup(Transform canvas, TMP_FontAsset font, out TMP_InputField input, out GameObject fieldCover,
            out Button confirm, out Button cancel, out Button close)
        {
            var overlay = ChildRect("DeleteConfirmPopup", canvas);
            StretchFull(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            float s = PopupScale;
            var panel = ChildRect("Panel", overlay);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PopupImgW, PopupImgH) * s;
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = LoadSprite(SelDir + "52.png", 2048);
            panelImg.preserveAspect = true;

            var cover = PopupRect("FieldCover", panel, new Rect(FieldPatchPx.x, FieldPatchPx.y, FieldPatchPx.width, FieldPatchPx.height));
            var coverImg = cover.gameObject.AddComponent<Image>();
            coverImg.sprite = EnsureFieldPatch();
            coverImg.raycastTarget = false;
            cover.gameObject.SetActive(false);
            fieldCover = cover.gameObject;

            // 입력창 = 입력칸 테두리 안쪽(좌우 24, 위아래 8 여유)
            var inner = Rect.MinMaxRect(PopupFieldPx.xMin + 24f, PopupFieldPx.yMin + 8f, PopupFieldPx.xMax - 24f, PopupFieldPx.yMax - 8f);
            input = MakeInput(panel, font, "", PopupPx(inner.center.x, inner.center.y), inner.size * s);

            // 취소(53) / 삭제(54): 같은 높이, 가운데 간격 PopupBtnGap, 둘을 합친 폭이 입력칸 가운데 오게
            float wCancel = CancelBody.width * PopupBtnH / CancelBody.height;
            float wDelete = DeleteBody.width * PopupBtnH / DeleteBody.height;
            float left = PopupFieldPx.center.x - (wCancel + PopupBtnGap + wDelete) / 2f;
            cancel = MakeImageButton("CancelButton", panel, SelDir + "53.png", CancelBody, 1536f, 1024f,
                PopupPx(left + wCancel / 2f, PopupBtnY), wCancel * s);
            confirm = MakeImageButton("ConfirmDeleteButton", panel, SelDir + "54.png", DeleteBody, 1536f, 1024f,
                PopupPx(left + wCancel + PopupBtnGap + wDelete / 2f, PopupBtnY), wDelete * s);

            // 닫기(55): 제목 오른쪽 위. 보이는 X는 42, 누르는 영역은 96으로 넉넉히
            close = MakeImageButton("Close", panel, SelDir + "55.png", PopupCloseBody, 1254f, 1254f, PopupPx(1470f, 172f), 42f);
            ((RectTransform)close.transform).sizeDelta = new Vector2(96f, 96f);

            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        // 52 px 좌표(위에서부터 y) -> 판 좌상단 기준 Stage 단위
        static Vector2 PopupPx(float x, float y) => new Vector2(x * PopupScale, -y * PopupScale);

        static RectTransform PopupRect(string name, RectTransform panel, Rect px)
        {
            var rt = ChildRect(name, panel);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = PopupPx(px.center.x, px.center.y);
            rt.sizeDelta = px.size * PopupScale;
            return rt;
        }

        // 52 입력칸 속 '동의한다' 글자를 가리는 조각을 52에서 만든다. 줄마다 글자 왼쪽/오른쪽 깨끗한 칸 색(6px 평균)을
        // 이어 채운다(입력칸은 가로로 색이 거의 같다). 52를 바꾸면 이 파일을 지우고 적용 메뉴를 다시 실행한다.
        static Sprite EnsureFieldPatch()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(FieldPatchPath) == null)
            {
                const int k = 6;
                var src = new Texture2D(2, 2);
                src.LoadImage(System.IO.File.ReadAllBytes(SelDir + "52.png"));
                int w = FieldPatchPx.width, h = FieldPatchPx.height;
                var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int row = 0; row < h; row++)
                {
                    int sy = src.height - 1 - (FieldPatchPx.y + row);   // Texture2D는 아래가 0
                    Color l = Color.clear, r = Color.clear;
                    for (int i = 1; i <= k; i++)
                    {
                        l += src.GetPixel(FieldPatchPx.x - i, sy) / k;
                        r += src.GetPixel(FieldPatchPx.x + w - 1 + i, sy) / k;
                    }
                    for (int col = 0; col < w; col++)
                        dst.SetPixel(col, h - 1 - row, Color.Lerp(l, r, (col + 0.5f) / w));
                }
                dst.Apply();
                System.IO.File.WriteAllBytes(FieldPatchPath, dst.EncodeToPNG());
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(dst);
                AssetDatabase.ImportAsset(FieldPatchPath, ImportAssetOptions.ForceSynchronousImport);
                // 부드러운 색이라 압축하면 블록이 보여 판과 어긋난다
                if (AssetImporter.GetAtPath(FieldPatchPath) is TextureImporter ti)
                {
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SaveAndReimport();
                }
            }
            return LoadSprite(FieldPatchPath, 512);
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
            input.textComponent.fontSize = 40;
            input.textComponent.color = White;
            input.textComponent.alignment = TextAlignmentOptions.Center;
            // 안내 글자('동의한다')는 52에 그려져 있어 비워 둔다. 틀렸을 때 View가 안내 문구를 넣는다.
            if (input.placeholder is TMP_Text ph)
            {
                ph.text = placeholder;
                ph.fontSize = 28;
                ph.fontStyle = FontStyles.Normal;
                ph.color = HintColor;
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
