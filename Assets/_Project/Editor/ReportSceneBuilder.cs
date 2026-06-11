using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 레포트 화면. 가로 1920x1080. (시안: 401.png 목록 / 402.png 학습 상세)
    // 목록: 탭 토글 + 한눈에 요약(도넛, halo 강조) + 전체 학습 세션 리스트
    // 상세: 요약 카드 + 연습이 필요해요 chip + 진행 흐름 타임라인 + delete_forever 삭제
    public static class ReportSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color32 Primary    = new Color32(26, 86, 219, 255);   // #1A56DB
        static readonly Color32 BgColor    = new Color32(247, 248, 252, 255);
        static readonly Color32 TitleColor = new Color32(33, 41, 60, 255);
        static readonly Color32 SubColor   = new Color32(110, 118, 135, 255);
        static readonly Color32 LightGray  = new Color32(238, 240, 244, 255);
        static readonly Color32 RingGray   = new Color32(229, 233, 240, 255);
        static readonly Color32 RetryBg    = new Color32(253, 243, 215, 255);
        static readonly Color32 RetryText  = new Color32(176, 122, 31, 255);
        static readonly Color32 NpcBubble  = new Color32(244, 246, 250, 255);
        static readonly Color32 UserBubble = new Color32(190, 227, 248, 255);
        static readonly Color32 Danger     = new Color32(224, 64, 64, 255);
        static readonly Color   White      = Color.white;

        const string RoundedPath    = "Assets/_Project/Art/UI/RoundedRect.png";
        const string RingPath       = "Assets/_Project/Art/UI/DonutRing.png";
        const string DeleteIconPath = "Assets/_Project/Art/UI/delete_forever.svg";

        const string PrefabDir            = "Assets/_Project/Prefabs/Report";
        const string SessionCardPrefabPath = PrefabDir + "/SessionCard.prefab";
        const string PracticeChipPrefabPath = PrefabDir + "/PracticeChip.prefab";
        const string StepPrefabPath        = PrefabDir + "/ReportStep.prefab";
        const string NpcBubblePrefabPath   = PrefabDir + "/NpcBubble.prefab";
        const string UserBubblePrefabPath  = PrefabDir + "/UserBubble.prefab";

        [MenuItem("Artti/Build ReportScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            EnsureSceneAsset(ScenePaths.Report);
            SceneBuilderUtils.OpenScene(ScenePaths.Report);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);

            var font = SceneBuilderUtils.GetKoreanFont();

            var sessionCardPrefab = EnsureSessionCardPrefab(font);
            var practiceChipPrefab = EnsurePracticeChipPrefab(font);
            var npcBubblePrefab = EnsureNpcBubblePrefab(font);
            var userBubblePrefab = EnsureUserBubblePrefab(font);
            var stepPrefab = EnsureStepPrefab(font);

            var bgPanel = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bgPanel.AddComponent<Image>().color = BgColor;

            // ===== 공통: 뒤로가기 (상세 열려있으면 목록으로 — ReportView가 처리) =====
            var backBtn = MakeCircleButton("BackButton", canvasGo.transform, "←", font);
            var backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(48, -40);
            backRect.sizeDelta = new Vector2(96, 96);

            // ================= 목록 패널 (401.png) =================
            var listPanel = SceneBuilderUtils.CreatePanel("ListPanel", canvasGo.transform);

            var title = MakeText("Title", listPanel.transform, "레포트", 60, TitleColor, font, bold: true);
            PlaceTop(title.rectTransform, new Vector2(0, -52), new Vector2(1100, 90));

            // 탭 토글
            var tabBar = ChildRect("TabBar", listPanel.transform);
            PlaceTop(tabBar, new Vector2(0, -166), new Vector2(660, 100));
            var tabBarImg = tabBar.gameObject.AddComponent<Image>();
            tabBarImg.sprite = Rounded(); tabBarImg.type = Image.Type.Sliced; tabBarImg.pixelsPerUnitMultiplier = 1f;
            tabBarImg.color = White;

            var speechTab = MakeTabButton("SpeechTab", tabBar, "말하기 훈련", new Vector2(-162, 0), font, out var speechTabBg, out var speechTabText);
            var arTab = MakeTabButton("ArTab", tabBar, "AR 음성도우미", new Vector2(162, 0), font, out var arTabBg, out var arTabText);

            // ----- 말하기 훈련 콘텐츠 -----
            var speechRoot = SceneBuilderUtils.CreatePanel("SpeechRoot", listPanel.transform);

            var summaryLabel = MakeText("SummaryLabel", speechRoot.transform, "한눈에 요약", 36, TitleColor, font, bold: false);
            summaryLabel.alignment = TextAlignmentOptions.Left;
            PlaceTopLeft(summaryLabel.rectTransform, new Vector2(110, -292), new Vector2(400, 50));

            // 요약 카드 — primary stroke + halo 강조
            var summaryCard = ChildRect("SummaryCard", speechRoot.transform);
            PlaceCenter(summaryCard, new Vector2(-480, -60), new Vector2(760, 500));

            var halo = ChildRect("Halo", summaryCard);
            StretchFull(halo, -56);
            var haloImg = halo.gameObject.AddComponent<Image>();
            haloImg.sprite = SceneBuilderUtils.EnsureGlowSprite();
            haloImg.type = Image.Type.Sliced;
            haloImg.color = new Color(Primary.r / 255f, Primary.g / 255f, Primary.b / 255f, 0.55f);
            haloImg.raycastTarget = false;

            var sumBorder = ChildRect("Border", summaryCard);
            StretchFull(sumBorder, 0);
            var sumBorderImg = sumBorder.gameObject.AddComponent<Image>();
            sumBorderImg.sprite = Rounded(); sumBorderImg.type = Image.Type.Sliced; sumBorderImg.pixelsPerUnitMultiplier = 1f;
            sumBorderImg.color = Primary;

            var sumBody = ChildRect("Body", summaryCard);
            StretchFull(sumBody, 3);
            var sumBodyImg = sumBody.gameObject.AddComponent<Image>();
            sumBodyImg.sprite = Rounded(); sumBodyImg.type = Image.Type.Sliced; sumBodyImg.pixelsPerUnitMultiplier = 1f;
            sumBodyImg.color = White;

            // 도넛 차트 (좌측)
            var donutLabel = MakeText("DonutLabel", sumBody, "전체 완료율", 30, SubColor, font, bold: false);
            PlaceCenter(donutLabel.rectTransform, new Vector2(-210, 190), new Vector2(300, 44));

            var donutBg = ChildRect("DonutBg", sumBody);
            PlaceCenter(donutBg, new Vector2(-210, -10), new Vector2(280, 280));
            var donutBgImg = donutBg.gameObject.AddComponent<Image>();
            donutBgImg.sprite = EnsureRingSprite();
            donutBgImg.color = RingGray;
            donutBgImg.raycastTarget = false;

            var donutFillRect = ChildRect("DonutFill", sumBody);
            PlaceCenter(donutFillRect, new Vector2(-210, -10), new Vector2(280, 280));
            var donutFill = donutFillRect.gameObject.AddComponent<Image>();
            donutFill.sprite = EnsureRingSprite();
            donutFill.color = Primary;
            donutFill.type = Image.Type.Filled;
            donutFill.fillMethod = Image.FillMethod.Radial360;
            donutFill.fillOrigin = (int)Image.Origin360.Top;
            donutFill.fillClockwise = true;
            donutFill.fillAmount = 0f;
            donutFill.raycastTarget = false;

            var donutPercent = MakeText("DonutPercent", sumBody, "0<size=60%>%</size>", 64, TitleColor, font, bold: true);
            PlaceCenter(donutPercent.rectTransform, new Vector2(-210, -10), new Vector2(220, 100));

            // 우측 횟수 목록
            var totalCount = MakeStatRow(sumBody, "TotalRow", "전체 시나리오 완료", new Vector2(0, 150), font);
            var convCount  = MakeStatRow(sumBody, "ConvRow", "편의점", new Vector2(0, 55), font);
            var pharmCount = MakeStatRow(sumBody, "PharmRow", "약국", new Vector2(0, -40), font);
            var restCount  = MakeStatRow(sumBody, "RestRow", "음식점", new Vector2(0, -135), font);

            // 세션 리스트 (우측)
            var sessionLabel = MakeText("SessionLabel", speechRoot.transform, "전체 학습 세션", 36, TitleColor, font, bold: false);
            sessionLabel.alignment = TextAlignmentOptions.Left;
            PlaceTopLeft(sessionLabel.rectTransform, new Vector2(950, -292), new Vector2(400, 50));

            var scroll = ChildRect("SessionScroll", speechRoot.transform);
            PlaceCenter(scroll, new Vector2(430, -145), new Vector2(920, 670));
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30;

            var viewport = ChildRect("Viewport", scroll);
            StretchFull(viewport, 0);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = ChildRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 24;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            var emptyState = MakeText("EmptyState", scroll, "아직 학습 기록이 없어요.", 36, SubColor, font, bold: false);
            PlaceCenter(emptyState.rectTransform, Vector2.zero, new Vector2(600, 80));
            emptyState.gameObject.SetActive(false);

            // ----- AR 탭 placeholder (미구현) -----
            var arPlaceholder = SceneBuilderUtils.CreatePanel("ArPlaceholder", listPanel.transform);
            var arText = MakeText("Text", arPlaceholder.transform, "AR 음성도우미 레포트는 준비 중이에요.", 40, SubColor, font, bold: false);
            PlaceCenter(arText.rectTransform, new Vector2(0, -100), new Vector2(1000, 80));
            arPlaceholder.SetActive(false);

            // ================= 상세 패널 (402.png) =================
            var detailPanel = SceneBuilderUtils.CreatePanel("DetailPanel", canvasGo.transform);

            var dTitle = MakeText("Title", detailPanel.transform, "학습 상세", 60, TitleColor, font, bold: true);
            PlaceTop(dTitle.rectTransform, new Vector2(0, -52), new Vector2(800, 90));

            var dSub = MakeText("Subtitle", detailPanel.transform, "이번 연습이 어떻게 진행됐는지 한눈에 볼 수 있어요.", 32, SubColor, font, bold: false);
            PlaceTop(dSub.rectTransform, new Vector2(0, -148), new Vector2(1200, 50));

            // 우상단 삭제 (delete_forever)
            var deleteBtn = MakeIconButton("DeleteBtn", detailPanel.transform, LoadDeleteIcon());
            var delRect = deleteBtn.GetComponent<RectTransform>();
            delRect.anchorMin = delRect.anchorMax = new Vector2(1f, 1f);
            delRect.pivot = new Vector2(1f, 1f);
            delRect.anchoredPosition = new Vector2(-64, -44);
            delRect.sizeDelta = new Vector2(88, 88);

            // 요약 카드 (시나리오/상태/날짜/걸린 시간)
            var dCard = ChildRect("SummaryCard", detailPanel.transform);
            PlaceCenter(dCard, new Vector2(0, 240), new Vector2(1560, 170));
            var dCardImg = dCard.gameObject.AddComponent<Image>();
            dCardImg.sprite = Rounded(); dCardImg.type = Image.Type.Sliced; dCardImg.pixelsPerUnitMultiplier = 1f;
            dCardImg.color = White;

            var dScenario = MakeSummaryColumn(dCard, "ScenarioCol", "시나리오", -580, font);
            var dDate     = MakeSummaryColumn(dCard, "DateCol", "날짜", 180, font);
            var dDuration = MakeSummaryColumn(dCard, "DurationCol", "걸린 시간", 560, font);

            var dStatusHeader = MakeText("StatusHeader", dCard, "상태", 26, SubColor, font, bold: false);
            PlaceCenter(dStatusHeader.rectTransform, new Vector2(-200, 45), new Vector2(300, 40));
            var dStatusBadge = ChildRect("StatusBadge", dCard);
            PlaceCenter(dStatusBadge, new Vector2(-200, -25), new Vector2(150, 56));
            var dStatusBg = dStatusBadge.gameObject.AddComponent<Image>();
            dStatusBg.sprite = Rounded(); dStatusBg.type = Image.Type.Sliced; dStatusBg.pixelsPerUnitMultiplier = 2f;
            var dStatusText = MakeText("Text", dStatusBadge, "완료", 28, TitleColor, font, bold: true);
            StretchFull(dStatusText.rectTransform, 4);

            // 스크롤 (연습이 필요해요 + 진행 흐름)
            var dScroll = ChildRect("DetailScroll", detailPanel.transform);
            PlaceCenter(dScroll, new Vector2(0, -175), new Vector2(1560, 620));
            var dScrollRect = dScroll.gameObject.AddComponent<ScrollRect>();
            dScrollRect.horizontal = false;
            dScrollRect.vertical = true;
            dScrollRect.scrollSensitivity = 30;

            var dViewport = ChildRect("Viewport", dScroll);
            StretchFull(dViewport, 0);
            dViewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            dViewport.gameObject.AddComponent<RectMask2D>();

            var dContent = ChildRect("Content", dViewport);
            dContent.anchorMin = new Vector2(0, 1);
            dContent.anchorMax = new Vector2(1, 1);
            dContent.pivot = new Vector2(0.5f, 1f);
            dContent.anchoredPosition = Vector2.zero;
            dContent.sizeDelta = Vector2.zero;
            var dVlg = dContent.gameObject.AddComponent<VerticalLayoutGroup>();
            dVlg.spacing = 24;
            dVlg.padding = new RectOffset(8, 8, 8, 24);
            dVlg.childControlWidth = true;
            dVlg.childControlHeight = true;
            dVlg.childForceExpandWidth = true;
            dVlg.childForceExpandHeight = false;
            dVlg.childAlignment = TextAnchor.UpperLeft;
            var dFitter = dContent.gameObject.AddComponent<ContentSizeFitter>();
            dFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            dScrollRect.viewport = dViewport;
            dScrollRect.content = dContent;

            // 연습이 필요해요 섹션
            var practiceSection = new GameObject("PracticeSection");
            practiceSection.transform.SetParent(dContent, false);
            practiceSection.AddComponent<RectTransform>();
            var psVlg = practiceSection.AddComponent<VerticalLayoutGroup>();
            psVlg.spacing = 12;
            psVlg.padding = new RectOffset(0, 0, 0, 0);
            psVlg.childControlWidth = true;
            psVlg.childControlHeight = true;
            psVlg.childForceExpandWidth = false;
            psVlg.childForceExpandHeight = false;
            psVlg.childAlignment = TextAnchor.UpperLeft;

            var psLabel = MakeText("Label", practiceSection.transform, "연습이 필요해요", 34, TitleColor, font, bold: false);
            psLabel.alignment = TextAlignmentOptions.Left;
            SceneBuilderUtils.AddLayoutElement(psLabel.gameObject, preferredHeight: 48);

            var chipRow = new GameObject("ChipRow");
            chipRow.transform.SetParent(practiceSection.transform, false);
            chipRow.AddComponent<RectTransform>();
            var chipHlg = chipRow.AddComponent<HorizontalLayoutGroup>();
            chipHlg.spacing = 16;
            chipHlg.padding = new RectOffset(0, 0, 0, 0);
            chipHlg.childControlWidth = true;
            chipHlg.childControlHeight = true;
            chipHlg.childForceExpandWidth = false;
            chipHlg.childForceExpandHeight = false;
            chipHlg.childAlignment = TextAnchor.MiddleLeft;

            // 진행 흐름 섹션
            var flowLabel = MakeText("FlowLabel", dContent, "진행 흐름", 34, TitleColor, font, bold: false);
            flowLabel.alignment = TextAlignmentOptions.Left;
            SceneBuilderUtils.AddLayoutElement(flowLabel.gameObject, preferredHeight: 48);

            var stepContainer = new GameObject("StepContainer");
            stepContainer.transform.SetParent(dContent, false);
            stepContainer.AddComponent<RectTransform>();
            var stepVlg = stepContainer.AddComponent<VerticalLayoutGroup>();
            stepVlg.spacing = 28;
            stepVlg.padding = new RectOffset(0, 0, 0, 0);
            stepVlg.childControlWidth = true;
            stepVlg.childControlHeight = true;
            stepVlg.childForceExpandWidth = true;
            stepVlg.childForceExpandHeight = false;
            stepVlg.childAlignment = TextAnchor.UpperLeft;

            detailPanel.SetActive(false);

            // ===== 삭제 확인 팝업 =====
            var confirmPopup = SceneBuilderUtils.CreatePanel("DeleteConfirmPopup", canvasGo.transform);
            confirmPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            var confirmCard = ChildRect("Card", confirmPopup.transform);
            PlaceCenter(confirmCard, Vector2.zero, new Vector2(720, 330));
            var confirmCardImg = confirmCard.gameObject.AddComponent<Image>();
            confirmCardImg.sprite = Rounded(); confirmCardImg.type = Image.Type.Sliced; confirmCardImg.pixelsPerUnitMultiplier = 1f;
            confirmCardImg.color = White;
            var confirmMsg = MakeText("Message", confirmCard, "이 학습 기록을 삭제할까요?\n삭제하면 되돌릴 수 없어요.", 36, TitleColor, font, bold: false);
            confirmMsg.textWrappingMode = TextWrappingModes.Normal;
            PlaceCenter(confirmMsg.rectTransform, new Vector2(0, 50), new Vector2(620, 140));
            var confirmCancel = MakePillButton("CancelBtn", confirmCard, "취소", 34, LightGray, TitleColor, font);
            PlaceCenter(confirmCancel.GetComponent<RectTransform>(), new Vector2(-150, -90), new Vector2(240, 88));
            var confirmDelete = MakePillButton("ConfirmBtn", confirmCard, "삭제", 34, Danger, White, font);
            PlaceCenter(confirmDelete.GetComponent<RectTransform>(), new Vector2(150, -90), new Vector2(240, 88));
            confirmPopup.SetActive(false);

            // ===== View 와이어링 =====
            var view = canvasGo.AddComponent<ReportView>();
            var so = new SerializedObject(view);
            so.FindProperty("backBtn").objectReferenceValue = backBtn.GetComponent<Button>();
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("listPanel").objectReferenceValue = listPanel;
            so.FindProperty("detailPanel").objectReferenceValue = detailPanel;
            so.FindProperty("speechTabBtn").objectReferenceValue = speechTab;
            so.FindProperty("arTabBtn").objectReferenceValue = arTab;
            so.FindProperty("speechTabBg").objectReferenceValue = speechTabBg;
            so.FindProperty("arTabBg").objectReferenceValue = arTabBg;
            so.FindProperty("speechTabText").objectReferenceValue = speechTabText;
            so.FindProperty("arTabText").objectReferenceValue = arTabText;
            so.FindProperty("speechRoot").objectReferenceValue = speechRoot;
            so.FindProperty("arPlaceholder").objectReferenceValue = arPlaceholder;
            so.FindProperty("donutFill").objectReferenceValue = donutFill;
            so.FindProperty("donutPercentText").objectReferenceValue = donutPercent;
            so.FindProperty("totalCompletedText").objectReferenceValue = totalCount;
            so.FindProperty("convenienceCountText").objectReferenceValue = convCount;
            so.FindProperty("pharmacyCountText").objectReferenceValue = pharmCount;
            so.FindProperty("restaurantCountText").objectReferenceValue = restCount;
            so.FindProperty("sessionListContainer").objectReferenceValue = content;
            so.FindProperty("sessionCardPrefab").objectReferenceValue = sessionCardPrefab;
            so.FindProperty("emptyState").objectReferenceValue = emptyState.gameObject;
            so.FindProperty("detailScenarioText").objectReferenceValue = dScenario;
            so.FindProperty("detailStatusBg").objectReferenceValue = dStatusBg;
            so.FindProperty("detailStatusText").objectReferenceValue = dStatusText;
            so.FindProperty("detailDateText").objectReferenceValue = dDate;
            so.FindProperty("detailDurationText").objectReferenceValue = dDuration;
            so.FindProperty("practiceChipContainer").objectReferenceValue = chipRow.GetComponent<RectTransform>();
            so.FindProperty("practiceChipPrefab").objectReferenceValue = practiceChipPrefab;
            so.FindProperty("practiceSection").objectReferenceValue = practiceSection;
            so.FindProperty("stepContainer").objectReferenceValue = stepContainer.GetComponent<RectTransform>();
            so.FindProperty("stepPrefab").objectReferenceValue = stepPrefab;
            so.FindProperty("npcBubblePrefab").objectReferenceValue = npcBubblePrefab;
            so.FindProperty("userBubblePrefab").objectReferenceValue = userBubblePrefab;
            so.FindProperty("deleteBtn").objectReferenceValue = deleteBtn.GetComponent<Button>();
            so.FindProperty("deleteConfirmPopup").objectReferenceValue = confirmPopup;
            so.FindProperty("deleteConfirmBtn").objectReferenceValue = confirmDelete;
            so.FindProperty("deleteCancelBtn").objectReferenceValue = confirmCancel;
            so.ApplyModifiedProperties();

            EnsureSceneInBuildSettings(ScenePaths.Report);

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ReportSceneBuilder] 완료");
        }

        // ===== 프리팹 =====

        static GameObject EnsureSessionCardPrefab(TMP_FontAsset font)
        {
            DeleteIfExists(SessionCardPrefabPath);
            EnsureFolder(PrefabDir);

            var root = new GameObject("SessionCard");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(880, 150);
            var bodyImg = root.AddComponent<Image>();
            bodyImg.sprite = Rounded(); bodyImg.type = Image.Type.Sliced; bodyImg.pixelsPerUnitMultiplier = 1f;
            bodyImg.color = White;
            var btn = root.AddComponent<Button>();
            btn.targetGraphic = bodyImg;
            // 탭 시 primary 액션 효과
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.93f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(Primary.r / 255f, Primary.g / 255f, Primary.b / 255f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            SceneBuilderUtils.AddLayoutElement(root, preferredHeight: 150);

            var badge = ChildRect("StatusBadge", root.transform);
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
            badge.pivot = new Vector2(0f, 0.5f);
            badge.anchoredPosition = new Vector2(36, 26);
            badge.sizeDelta = new Vector2(110, 48);
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = Rounded(); badgeImg.type = Image.Type.Sliced; badgeImg.pixelsPerUnitMultiplier = 2f;
            badgeImg.raycastTarget = false;
            var badgeText = MakeText("Text", badge, "완료", 24, TitleColor, font, bold: true);
            StretchFull(badgeText.rectTransform, 2);

            var nameText = MakeText("Name", root.transform, "약국", 42, TitleColor, font, bold: true);
            nameText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(nameText.rectTransform, new Vector2(170, 26), new Vector2(420, 60));

            var dateText = MakeText("Date", root.transform, "", 28, SubColor, font, bold: false);
            dateText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(dateText.rectTransform, new Vector2(172, -34), new Vector2(480, 44));

            var chevron = MakeText("Chevron", root.transform, ">", 44, SubColor, font, bold: true);
            chevron.rectTransform.anchorMin = chevron.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            chevron.rectTransform.pivot = new Vector2(1f, 0.5f);
            chevron.rectTransform.anchoredPosition = new Vector2(-36, 0);
            chevron.rectTransform.sizeDelta = new Vector2(50, 60);

            var view = root.AddComponent<SessionCardView>();
            view.statusBg = badgeImg;
            view.statusText = badgeText;
            view.nameText = nameText;
            view.dateText = dateText;
            view.selectButton = btn;

            return SaveAsPrefab(root, SessionCardPrefabPath);
        }

        static GameObject EnsurePracticeChipPrefab(TMP_FontAsset font)
        {
            DeleteIfExists(PracticeChipPrefabPath);
            EnsureFolder(PrefabDir);

            var root = new GameObject("PracticeChip");
            root.AddComponent<RectTransform>();
            var img = root.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.5f;
            img.color = White;
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(28, 28, 12, 12);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            MakeText("Text", root.transform, "인사하기", 28, TitleColor, font, bold: false);

            return SaveAsPrefab(root, PracticeChipPrefabPath);
        }

        static GameObject EnsureNpcBubblePrefab(TMP_FontAsset font)
        {
            DeleteIfExists(NpcBubblePrefabPath);
            EnsureFolder(PrefabDir);

            var root = MakeBubbleRow("NpcBubble", TextAnchor.MiddleLeft);
            MakeBubble(root.transform, NpcBubble, 640, font, out var text);
            text.alignment = TextAlignmentOptions.Left;

            return SaveAsPrefab(root, NpcBubblePrefabPath);
        }

        static GameObject EnsureUserBubblePrefab(TMP_FontAsset font)
        {
            DeleteIfExists(UserBubblePrefabPath);
            EnsureFolder(PrefabDir);

            var root = MakeBubbleRow("UserBubble", TextAnchor.MiddleRight);
            MakeBubble(root.transform, UserBubble, 420, font, out var text);
            text.alignment = TextAlignmentOptions.Center;

            // 우측 아바타 (ReportView가 프로필 아바타 주입)
            var avatar = new GameObject("Avatar");
            avatar.transform.SetParent(root.transform, false);
            avatar.AddComponent<RectTransform>();
            var avatarImg = avatar.AddComponent<Image>();
            avatarImg.preserveAspect = true;
            avatarImg.raycastTarget = false;
            var avatarLe = avatar.AddComponent<LayoutElement>();
            avatarLe.preferredWidth = 52;
            avatarLe.preferredHeight = 52;

            return SaveAsPrefab(root, UserBubblePrefabPath);
        }

        static GameObject EnsureStepPrefab(TMP_FontAsset font)
        {
            DeleteIfExists(StepPrefabPath);
            EnsureFolder(PrefabDir);

            var root = new GameObject("ReportStep");
            root.AddComponent<RectTransform>();
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.UpperLeft;

            // 타임라인 점
            var dot = new GameObject("Dot");
            dot.transform.SetParent(root.transform, false);
            dot.AddComponent<RectTransform>();
            var dotImg = dot.AddComponent<Image>();
            dotImg.sprite = Builtin("UI/Skin/Knob.psd");
            dotImg.color = Primary;
            dotImg.raycastTarget = false;
            var dotLe = dot.AddComponent<LayoutElement>();
            dotLe.preferredWidth = 22;
            dotLe.preferredHeight = 22;

            // 좌측: 목표 이름 + 재시도 배지
            var leftCol = new GameObject("LeftCol");
            leftCol.transform.SetParent(root.transform, false);
            leftCol.AddComponent<RectTransform>();
            var leftVlg = leftCol.AddComponent<VerticalLayoutGroup>();
            leftVlg.spacing = 10;
            leftVlg.padding = new RectOffset(0, 0, 0, 0);
            leftVlg.childControlWidth = true;
            leftVlg.childControlHeight = true;
            leftVlg.childForceExpandWidth = false;
            leftVlg.childForceExpandHeight = false;
            leftVlg.childAlignment = TextAnchor.UpperLeft;
            var leftLe = leftCol.AddComponent<LayoutElement>();
            leftLe.preferredWidth = 300;
            leftLe.flexibleWidth = 0;

            var nameText = MakeText("Name", leftCol.transform, "인사하기", 34, TitleColor, font, bold: true);
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.textWrappingMode = TextWrappingModes.Normal;

            var retryBadge = new GameObject("RetryBadge");
            retryBadge.transform.SetParent(leftCol.transform, false);
            retryBadge.AddComponent<RectTransform>();
            var retryImg = retryBadge.AddComponent<Image>();
            retryImg.sprite = Rounded(); retryImg.type = Image.Type.Sliced; retryImg.pixelsPerUnitMultiplier = 2f;
            retryImg.color = RetryBg;
            retryImg.raycastTarget = false;
            var retryHlg = retryBadge.AddComponent<HorizontalLayoutGroup>();
            retryHlg.padding = new RectOffset(16, 16, 8, 8);
            retryHlg.childControlWidth = true;
            retryHlg.childControlHeight = true;
            retryHlg.childForceExpandWidth = false;
            retryHlg.childForceExpandHeight = false;
            retryHlg.childAlignment = TextAnchor.MiddleCenter;
            var retryText = MakeText("Text", retryBadge.transform, "1회 다시 시도했어요.", 24, RetryText, font, bold: false);

            // 우측: 대화 버블 카드
            var bubbleCard = new GameObject("BubbleCard");
            bubbleCard.transform.SetParent(root.transform, false);
            bubbleCard.AddComponent<RectTransform>();
            var cardImg = bubbleCard.AddComponent<Image>();
            cardImg.sprite = Rounded(); cardImg.type = Image.Type.Sliced; cardImg.pixelsPerUnitMultiplier = 1f;
            cardImg.color = White;
            cardImg.raycastTarget = false;
            var cardVlg = bubbleCard.AddComponent<VerticalLayoutGroup>();
            cardVlg.spacing = 14;
            cardVlg.padding = new RectOffset(28, 28, 20, 20);
            cardVlg.childControlWidth = true;
            cardVlg.childControlHeight = true;
            cardVlg.childForceExpandWidth = true;
            cardVlg.childForceExpandHeight = false;
            cardVlg.childAlignment = TextAnchor.UpperLeft;
            var cardLe = bubbleCard.AddComponent<LayoutElement>();
            cardLe.flexibleWidth = 1;

            var view = root.AddComponent<ReportStepView>();
            view.objectiveText = nameText;
            view.retryBadge = retryBadge;
            view.retryText = retryText;
            view.bubbleContainer = bubbleCard.GetComponent<RectTransform>();

            return SaveAsPrefab(root, StepPrefabPath);
        }

        static GameObject MakeBubbleRow(string name, TextAnchor alignment)
        {
            var root = new GameObject(name);
            root.AddComponent<RectTransform>();
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = alignment;
            return root;
        }

        static GameObject MakeBubble(Transform parent, Color bg, float width, TMP_FontAsset font, out TMP_Text text)
        {
            var bubble = new GameObject("Bubble");
            bubble.transform.SetParent(parent, false);
            bubble.AddComponent<RectTransform>();
            var img = bubble.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.5f;
            img.color = bg;
            img.raycastTarget = false;
            var vlg = bubble.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 14, 14);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleLeft;
            var le = bubble.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = 0;

            text = MakeText("Text", bubble.transform, "", 30, TitleColor, font, bold: false);
            text.textWrappingMode = TextWrappingModes.Normal;
            return bubble;
        }

        // ===== 도넛 링 스프라이트 (1회 생성) =====
        static Sprite EnsureRingSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
            if (existing != null) return existing;

            const int size = 256;
            const float outer = 120f;
            const float inner = 92f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // 바깥/안쪽 경계 1.5px 안티앨리어싱
                    float a = Mathf.Min(Mathf.Clamp01((outer - r) / 1.5f), Mathf.Clamp01((r - inner) / 1.5f));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(RingPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(RingPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(RingPath);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
        }

        // ===== 공통 헬퍼 =====

        static void EnsureSceneAsset(string path)
        {
            if (File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }

        static void EnsureSceneInBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == path)) return;
            EditorBuildSettings.scenes = scenes
                .Concat(new[] { new EditorBuildSettingsScene(path, true) })
                .ToArray();
            Debug.Log($"[ReportSceneBuilder] Build Settings에 추가: {path}");
        }

        static TMP_Text MakeSummaryColumn(RectTransform card, string name, string header, float x, TMP_FontAsset font)
        {
            var headerText = MakeText(name + "Header", card, header, 26, SubColor, font, bold: false);
            PlaceCenter(headerText.rectTransform, new Vector2(x, 45), new Vector2(320, 40));
            var valueText = MakeText(name + "Value", card, "-", 40, TitleColor, font, bold: true);
            PlaceCenter(valueText.rectTransform, new Vector2(x, -25), new Vector2(360, 60));
            return valueText;
        }

        static TMP_Text MakeStatRow(RectTransform parent, string name, string label, Vector2 pos, TMP_FontAsset font)
        {
            var row = ChildRect(name, parent);
            PlaceCenter(row, new Vector2(160, pos.y), new Vector2(420, 70));

            var pill = ChildRect("LabelPill", row);
            pill.anchorMin = pill.anchorMax = new Vector2(0f, 0.5f);
            pill.pivot = new Vector2(0f, 0.5f);
            pill.anchoredPosition = new Vector2(0, 0);
            pill.sizeDelta = new Vector2(280, 56);
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.sprite = Rounded(); pillImg.type = Image.Type.Sliced; pillImg.pixelsPerUnitMultiplier = 2f;
            pillImg.color = LightGray;
            pillImg.raycastTarget = false;
            var pillText = MakeText("Text", pill, label, 26, TitleColor, font, bold: false);
            StretchFull(pillText.rectTransform, 4);

            var count = MakeText("Count", row, "0 회", 34, TitleColor, font, bold: true);
            count.rectTransform.anchorMin = count.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            count.rectTransform.pivot = new Vector2(1f, 0.5f);
            count.rectTransform.anchoredPosition = new Vector2(0, 0);
            count.rectTransform.sizeDelta = new Vector2(160, 56);
            return count;
        }

        static Button MakeTabButton(string name, RectTransform parent, string label, Vector2 pos, TMP_FontAsset font, out Image bg, out TMP_Text text)
        {
            var rect = ChildRect(name, parent);
            PlaceCenter(rect, pos, new Vector2(310, 80));
            bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = Rounded(); bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1f;
            bg.color = White;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            text = MakeText("Text", rect, label, 32, TitleColor, font, bold: true);
            StretchFull(text.rectTransform, 4);
            return btn;
        }

        static GameObject MakeCircleButton(string name, Transform parent, string glyph, TMP_FontAsset font)
        {
            var rect = ChildRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Builtin("UI/Skin/Knob.psd");
            img.color = White;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, glyph, 44, TitleColor, font, bold: true);
            StretchFull(t.rectTransform, 4);
            return rect.gameObject;
        }

        static GameObject MakeIconButton(string name, Transform parent, Sprite icon)
        {
            var rect = ChildRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var iconRect = ChildRect("Icon", rect);
            StretchFull(iconRect, 8);
            var iconImg = iconRect.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            return rect.gameObject;
        }

        static Button MakePillButton(string name, Transform parent, string label, int fontSize, Color bg, Color textColor, TMP_FontAsset font)
        {
            var rect = ChildRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = bg;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, label, fontSize, textColor, font, bold: true);
            StretchFull(t.rectTransform, 6);
            return btn;
        }

        // delete_forever.svg → Sprite. 없으면 openmoji wastebasket(1F5D1)로 폴백
        static Sprite LoadDeleteIcon()
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(DeleteIconPath))
                if (obj is Sprite s) return s;
            const string fallback = "Assets/_Project/openmoji-master/color/svg/1F5D1.svg";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fallback))
                if (obj is Sprite s) return s;
            Debug.LogWarning($"[ReportSceneBuilder] 삭제 아이콘 Sprite 없음: {DeleteIconPath}");
            return null;
        }

        static TMP_Text MakeText(string name, Transform parent, string text, int fontSize, Color color, TMP_FontAsset font, bool bold)
        {
            var tmp = SceneBuilderUtils.CreateTMPText(name, parent, text, fontSize);
            tmp.color = color;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        static GameObject SaveAsPrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void PlaceCenter(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void PlaceTop(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void PlaceTopLeft(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void AnchorLeft(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
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
    }
}
