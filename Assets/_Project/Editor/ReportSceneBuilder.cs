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
        const string BackgroundPath = "Assets/_Project/Art/UI/ReportBackground.png";
        const string CharacterPath  = "Assets/_Project/Art/UI/ReportCH.png";
        const string SpeechBoxPath  = "Assets/_Project/Art/UI/ReportSpeechBox.png";
        const string GlassTabPath   = "Assets/_Project/Art/UI/ReportGlassTab.png";
        const string GlassPillPath  = "Assets/_Project/Art/UI/ReportGlassPill.png";
        const string PeriodTrackPath = "Assets/_Project/Art/UI/ReportPeriodTrack.png"; // 기간 필터 바탕(최근 7일/3일 baked)
        const string TabTrackPath   = "Assets/_Project/Art/UI/ReportTabTrack.png";  // 탭 밑바탕 트랙
        const string TabPillPath    = "Assets/_Project/Art/UI/ReportTabPill.png";   // 탭 슬라이드 pill
        const string SummaryPanelPath = "Assets/_Project/Art/UI/ReportSummaryPanel.png";
        const string GraphGlassPath   = "Assets/_Project/Art/UI/Profile/CenterGlassPanel.png"; // 다른 씬 글래스 재사용
        const string GlassCardPath    = "Assets/_Project/Art/UI/ReportGlassCard.png";          // L-Photoroom 글래스 카드(3D)
        const string RingPath       = "Assets/_Project/Art/UI/DonutRing.png";
        const string DeleteIconPath = "Assets/_Project/Art/UI/delete_forever.svg";
        const string StoreIconPath  = "Assets/_Project/Art/UI/Report/Icons/ic_store.png"; // 편의점 카드 아이콘
        const string OpenmojiDir    = "Assets/_Project/openmoji-master/color/svg/";        // 약국/음식점 등 이모지

        // 캐릭터 프레임 애니(컷아웃). blink는 클로즈업 크롭이라 제외.
        const string AnimDir = "Assets/_Project/Art/UI/Report/Anim";
        static readonly Vector2 CharHomeFeet = new Vector2(-620f, -470f); // 정위치 발 좌표(좌측 하단)
        const float CharOffscreenX  = -1150f; // walk 입장 시작점(화면 왼쪽 밖). 짧게 입장해 스텝 수↓
        const float CharTargetHeight = 720f;  // 클립별 높이를 이 값으로 정규화해 크기 튐 방지

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
            bgPanel.transform.localScale = new Vector3(1.8f, 1f, 1f); // 가로로 넓혀 꽉 차게
            var bgImg = bgPanel.AddComponent<Image>();
            var bgSprite = LoadPhotoSprite(BackgroundPath);
            if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = White; bgImg.preserveAspect = false; }
            else bgImg.color = BgColor;

            // 인사하는 캐릭터 (좌측). 정적 이미지 대신 프레임 애니 + 연출 director.
            // 흐름: walk 입장 -> idle -> 말풍선/TTS(talk) -> full_motion -> idle. 발 기준(bottom pivot) 정렬.
            var idleFrames = LoadFrames("idle", "idle", 5);
            var walkFrames = LoadFrames("walk_loop", "walk_loop", 8);
            var talkFrames = LoadFrames("gesture_talk", "gesture_talk", 6);
            var fullFrames = LoadFrames("full_motion", "full_motion", 12);

            ReportCharacterDirector director = null;
            AudioSource charAudio = null;
            if (idleFrames.Length > 0)
            {
                var ch = ChildRect("ReportCharacter", canvasGo.transform);
                ch.anchorMin = ch.anchorMax = new Vector2(0.5f, 0.5f);
                ch.pivot = new Vector2(0.5f, 0f); // 발 기준
                ch.anchoredPosition = CharHomeFeet;
                var chImg = ch.gameObject.AddComponent<Image>();
                chImg.sprite = idleFrames[0]; chImg.preserveAspect = true; chImg.raycastTarget = false;
                ch.sizeDelta = SizeFor(idleFrames);
                ch.gameObject.AddComponent<SpriteSequencePlayer>();
                charAudio = ch.gameObject.AddComponent<AudioSource>();
                charAudio.playOnAwake = false;
                director = ch.gameObject.AddComponent<ReportCharacterDirector>();
            }

            // 캐릭터 말풍선 (box 프레임). 문구는 런타임에 director가 레포트 기반 LLM 결과로 채움.
            // 평소 숨김 -> talk 단계에서만 표시. 꼬리 좌하단이 캐릭터를 가리킴. 이미지 1024x659
            GameObject speechGo = null;
            TMP_Text speechLabel = null;
            var speechSprite = LoadPhotoSprite(SpeechBoxPath);
            if (speechSprite != null)
            {
                var speech = ChildRect("CharacterSpeech", canvasGo.transform);
                speech.anchorMin = speech.anchorMax = new Vector2(0.5f, 0.5f);
                speech.pivot = new Vector2(0.5f, 0.5f);
                speech.anchoredPosition = new Vector2(-433, 250); // 손배치
                speech.sizeDelta = new Vector2(480, 309); // 비율 1024:659
                var speechImg = speech.gameObject.AddComponent<Image>();
                speechImg.sprite = speechSprite; speechImg.preserveAspect = true; speechImg.raycastTarget = false;

                var speechText = MakeText("Text", speech.transform, "정말 잘하고 있어요!\n계속 파이팅해요!", 30, TitleColor, font, bold: true);
                speechText.alignment = TextAlignmentOptions.Center;
                speechText.textWrappingMode = TextWrappingModes.Normal;
                var str = speechText.rectTransform;
                str.anchorMin = str.anchorMax = new Vector2(0.5f, 0.5f);
                str.pivot = new Vector2(0.5f, 0.5f);
                str.anchoredPosition = new Vector2(0, 22); // 꼬리 영역 피해 위쪽
                str.sizeDelta = new Vector2(400, 180);

                speechGo = speech.gameObject;
                speechLabel = speechText;
                speechGo.SetActive(false); // director가 talk 단계에서 표시
            }

            // ===== 캐릭터 director 와이어링 =====
            if (director != null)
            {
                var dso = new SerializedObject(director);
                SetClip(dso, "walk", walkFrames, 12f, SizeFor(walkFrames));
                SetClip(dso, "idle", idleFrames, 8f,  SizeFor(idleFrames));
                SetClip(dso, "talk", talkFrames, 10f, SizeFor(talkFrames));
                SetClip(dso, "full", fullFrames, 12f, SizeFor(fullFrames));
                dso.FindProperty("homeFeet").vector2Value = CharHomeFeet;
                dso.FindProperty("offscreenX").floatValue = CharOffscreenX;
                if (speechGo != null) dso.FindProperty("speechBubble").objectReferenceValue = speechGo;
                if (speechLabel != null) dso.FindProperty("speechLabel").objectReferenceValue = speechLabel;
                if (charAudio != null) dso.FindProperty("audioSource").objectReferenceValue = charAudio;
                dso.ApplyModifiedProperties();
            }

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

            // 탭 토글 (밑바탕 트랙 image-Photoroom(2) + 슬라이드 pill image-Photoroom(1))
            var tabBar = ChildRect("TabBar", listPanel.transform);
            PlaceTop(tabBar, new Vector2(0, -166), new Vector2(600, 116)); // 트랙 비율 5.18
            var tabBarImg = tabBar.gameObject.AddComponent<Image>();
            // 밑바닥 트랙 (은은한 글래스 외곽선 pill)
            var trackSprite = LoadPhotoSprite(TabTrackPath);
            if (trackSprite != null) { tabBarImg.sprite = trackSprite; tabBarImg.preserveAspect = false; tabBarImg.color = White; }
            else { tabBarImg.sprite = Rounded(); tabBarImg.type = Image.Type.Sliced; tabBarImg.pixelsPerUnitMultiplier = 1f; tabBarImg.color = White; }

            // 슬라이드 pill (흰 채움). 클릭한 탭으로 왔다갔다 하는 표시기. 텍스트 아래
            var tabPill = ChildRect("TabPill", tabBar);
            PlaceCenter(tabPill, new Vector2(-150, 0), new Vector2(290, 92)); // pill 비율 3.15
            var tabPillImg = tabPill.gameObject.AddComponent<Image>();
            var pillSprite = LoadPhotoSprite(TabPillPath);
            if (pillSprite != null) { tabPillImg.sprite = pillSprite; tabPillImg.preserveAspect = false; }
            else { tabPillImg.sprite = Rounded(); tabPillImg.type = Image.Type.Sliced; tabPillImg.pixelsPerUnitMultiplier = 1f; }
            tabPillImg.color = White; // 또렷하게
            tabPillImg.raycastTarget = false;

            // 탭 버튼 (bg 투명 = 흰 pill이 표시기. 글자는 둘 다 어둡게 고정)
            var speechTab = MakeTabButton("SpeechTab", tabBar, "말하기 훈련", new Vector2(-150, 0), font, out _, out _);
            var arTab = MakeTabButton("ArTab", tabBar, "AR 음성도우미", new Vector2(150, 0), font, out _, out _);

            // 슬라이드 컴포넌트 와이어링
            var tabSlider = tabBar.gameObject.AddComponent<TabSlider>();
            var tso = new SerializedObject(tabSlider);
            tso.FindProperty("pill").objectReferenceValue = tabPill;
            var tabsProp = tso.FindProperty("tabs");
            tabsProp.arraySize = 2;
            tabsProp.GetArrayElementAtIndex(0).objectReferenceValue = speechTab;
            tabsProp.GetArrayElementAtIndex(1).objectReferenceValue = arTab;
            var posProp = tso.FindProperty("positions");
            posProp.arraySize = 2;
            posProp.GetArrayElementAtIndex(0).vector2Value = new Vector2(-150, 0);
            posProp.GetArrayElementAtIndex(1).vector2Value = new Vector2(150, 0);
            tso.ApplyModifiedProperties();

            // ----- 말하기 훈련 콘텐츠 -----
            var speechRoot = SceneBuilderUtils.CreatePanel("SpeechRoot", listPanel.transform);

            // 옛 요약카드/도넛/세션리스트 UI 제거. RE.png 레이아웃으로 새로 구성.

            // ===== 한눈에 요약 패널 (timeglass, 우상단) =====
            var summary = ChildRect("SummaryPanel", speechRoot.transform);
            PlaceCenter(summary, new Vector2(599, 161), new Vector2(690, 499)); // 손배치, 비율 1216:879
            var summaryImg = summary.gameObject.AddComponent<Image>();
            var summarySprite = LoadPhotoSprite(SummaryPanelPath);
            if (summarySprite != null) { summaryImg.sprite = summarySprite; summaryImg.preserveAspect = true; }
            else { summaryImg.sprite = Rounded(); summaryImg.type = Image.Type.Sliced; summaryImg.pixelsPerUnitMultiplier = 1f; summaryImg.color = White; }
            summaryImg.raycastTarget = false;

            // 글자는 패널에서 분리 = speechRoot 직속(독립). 패널을 옮겨도 글자는 안 따라감.
            // 위치 = 기존 패널기준값 + 패널위치(545,175) → 화면상 위치 그대로 유지.
            var sumTitle = MakeText("HeaderLabel", speechRoot.transform, "한눈에 요약", 30, TitleColor, font, bold: true);
            sumTitle.alignment = TextAlignmentOptions.Left;
            PlaceCenter(sumTitle.rectTransform, new Vector2(513, 323), new Vector2(260, 44));
            var sumMore = MakeText("MoreLabel", speechRoot.transform, "전체 보기", 22, SubColor, font, bold: false);
            PlaceCenter(sumMore.rectTransform, new Vector2(811, 322), new Vector2(150, 40));
            // "전체 보기" 클릭 영역
            var moreBtn = MakeHitButton("MoreBtn", (RectTransform)speechRoot.transform, new Vector2(811, 322), new Vector2(150, 44));

            // 4 스탯 (값 + 라벨, 각자 클릭 가능) - 손배치 위치 그대로, 패널과 독립. 값은 런타임에 ReportView가 채움.
            var stat1Btn = AddSummaryStat(speechRoot.transform, "0회",    "완료 시나리오", new Vector2(499, 240), font, out var stat1Val, out _);
            var stat2Btn = AddSummaryStat(speechRoot.transform, "0시간",  "총 학습 시간", new Vector2(773, 235), font, out var stat2Val, out _);
            var stat3Btn = AddSummaryStat(speechRoot.transform, "0일",    "연속 학습",   new Vector2(499, 81),  font, out var stat3Val, out _);
            var stat4Btn = AddSummaryStat(speechRoot.transform, "Level 1", "AAC Beginner", new Vector2(780, 79), font, out var stat4Val, out var stat4Title);

            // ===== 중앙 그래프 2개 (전체 학습 세션 / 전체 출현) - 클릭 가능, 선은 런타임에 실데이터로 갱신 =====
            var graph1Btn = BuildGraphPanel(speechRoot.transform, "전체 학습 세션", new Vector2(-130, 175), new Vector2(720, 350),
                new float[] { 0.2f, 0.32f, 0.28f, 0.45f, 0.6f, 0.78f }, new Color(0.16f, 0.45f, 0.9f, 1f),
                new float[] { 0.12f, 0.22f, 0.35f, 0.4f, 0.55f, 0.68f }, new Color(0.95f, 0.55f, 0.3f, 1f), false, font,
                out var g1Line1, out var g1Line2);
            var graph2Btn = BuildGraphPanel(speechRoot.transform, "전체 출현", new Vector2(-130, -210), new Vector2(720, 350),
                new float[] { 0.15f, 0.3f, 0.42f, 0.5f, 0.62f, 0.72f }, new Color(0.16f, 0.45f, 0.9f, 1f),
                null, Color.clear, true, font, out var g2Line1, out _);

            // ===== 우하단 최근 학습 기록 (클릭 가능, 값은 런타임에 ReportView가 채움) =====
            var recordRows = BuildRecordsPanel(speechRoot.transform, new Vector2(599, -270), new Vector2(690, 330), font);

            // ===== 우상단 기간 필터 (세로 슬라이더) =====
            // 바탕 = image-Photoroom(5): "최근 7일 / 최근 3일" 텍스트가 baked된 베이지 카드(2단).
            // 그 위에 pill = image-Photoroom(1)이 클릭한 행으로 세로 이동(TabSlider). 텍스트가 비치도록 pill 반투명.
            var filterRoot = ChildRect("PeriodFilter", listPanel.transform);
            filterRoot.anchorMin = filterRoot.anchorMax = new Vector2(1f, 1f);
            filterRoot.pivot = new Vector2(1f, 1f);
            filterRoot.anchoredPosition = new Vector2(-44, -40);
            filterRoot.sizeDelta = new Vector2(300, 124); // (5) 비율 2.42 -> 폭300 높이124 꽉맞음
            var filterBgImg = filterRoot.gameObject.AddComponent<Image>();
            var filterBg = LoadPhotoSprite(PeriodTrackPath);
            if (filterBg != null) { filterBgImg.sprite = filterBg; filterBgImg.preserveAspect = true; }
            else { filterBgImg.sprite = Rounded(); filterBgImg.type = Image.Type.Sliced; filterBgImg.pixelsPerUnitMultiplier = 1f; filterBgImg.color = White; }

            // baked 텍스트 행 중심: 위(최근 7일) y=+31, 아래(최근 3일) y=-36 (124 높이 기준)
            var topPos = new Vector2(0, 31);
            var botPos = new Vector2(0, -36);

            // 슬라이드 pill (선택 행 강조). baked 텍스트가 비치도록 반투명.
            var filterPill = ChildRect("FilterPill", filterRoot);
            PlaceCenter(filterPill, topPos, new Vector2(252, 54));
            var filterPillImg = filterPill.gameObject.AddComponent<Image>();
            var fPillSprite = LoadPhotoSprite(TabPillPath); // image-Photoroom(1) 재사용
            if (fPillSprite != null) { filterPillImg.sprite = fPillSprite; filterPillImg.preserveAspect = false; }
            else { filterPillImg.sprite = Rounded(); filterPillImg.type = Image.Type.Sliced; filterPillImg.pixelsPerUnitMultiplier = 1f; }
            filterPillImg.color = new Color(1f, 1f, 1f, 0.85f); // 반투명 강조 (진하게)
            filterPillImg.raycastTarget = false;

            // 행 클릭 영역 (투명 버튼, 텍스트는 바탕에 baked라 생략)
            var f7 = MakeHitButton("Filter7", filterRoot, topPos, new Vector2(260, 58));
            var f3 = MakeHitButton("Filter3", filterRoot, botPos, new Vector2(260, 58));

            var fSlider = filterRoot.gameObject.AddComponent<TabSlider>();
            var fso = new SerializedObject(fSlider);
            fso.FindProperty("pill").objectReferenceValue = filterPill;
            var fTabs = fso.FindProperty("tabs"); fTabs.arraySize = 2;
            fTabs.GetArrayElementAtIndex(0).objectReferenceValue = f7;
            fTabs.GetArrayElementAtIndex(1).objectReferenceValue = f3;
            var fPos = fso.FindProperty("positions"); fPos.arraySize = 2;
            fPos.GetArrayElementAtIndex(0).vector2Value = topPos;
            fPos.GetArrayElementAtIndex(1).vector2Value = botPos;
            fso.FindProperty("defaultIndex").intValue = 0; // 기본 최근 7일
            fso.ApplyModifiedProperties();

            // ===== 하단 중앙 "계속 학습하기" (단독) =====
            var continueBtn = MakePillButton("ContinueBtn", listPanel.transform, "계속 학습하기", 36, Primary, White, font);
            var contRect = continueBtn.GetComponent<RectTransform>();
            contRect.anchorMin = contRect.anchorMax = new Vector2(0.5f, 0f);
            contRect.pivot = new Vector2(0.5f, 0f);
            contRect.anchoredPosition = new Vector2(0, 44);
            contRect.sizeDelta = new Vector2(440, 100);
            var contNav = continueBtn.gameObject.AddComponent<Artti.Common.SceneBackButton>();
            contNav.SetTarget("MainScene");
            var contMethod = typeof(Artti.Common.SceneBackButton).GetMethod(nameof(Artti.Common.SceneBackButton.GoBack));
            var contAction = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), contNav, contMethod);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(continueBtn.onClick, contAction);

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

            // ================= 전체 학습 기록 목록 (전체 보기 오버레이) =================
            // 배경은 캔버스 직속 "Background"(넓은 이미지)가 담당 - AllSessionsPanel은 자체 배경 없음(중복 제거)
            var allSessionsPanel = SceneBuilderUtils.CreatePanel("AllSessionsPanel", canvasGo.transform);

            var allTitle = MakeText("Title", allSessionsPanel.transform, "전체 학습 기록", 56, TitleColor, font, bold: true);
            PlaceTop(allTitle.rectTransform, new Vector2(0, -48), new Vector2(900, 80));
            var allSubtitle = MakeText("Subtitle", allSessionsPanel.transform, "지금까지의 학습 여정을 한눈에 확인해 보세요.", 28, TitleColor, font, bold: true);
            PlaceTop(allSubtitle.rectTransform, new Vector2(0, -124), new Vector2(1000, 44));

            var allSessionsEmpty = MakeText("EmptyState", allSessionsPanel.transform, "아직 학습 기록이 없어요.", 36, SubColor, font, bold: false);
            PlaceCenter(allSessionsEmpty.rectTransform, Vector2.zero, new Vector2(900, 60));

            // ScrollRect (세로 스크롤)
            var scrollRect = ChildRect("ScrollView", allSessionsPanel.transform);
            PlaceCenter(scrollRect, new Vector2(0, -64), new Vector2(1040, 760));
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = ChildRect("Viewport", scrollRect);
            StretchFull(viewport, 0);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.001f); // 마스크용 거의 투명
            viewport.gameObject.AddComponent<RectMask2D>();

            var allSessionsContent = ChildRect("Content", viewport);
            allSessionsContent.anchorMin = new Vector2(0f, 1f);
            allSessionsContent.anchorMax = new Vector2(1f, 1f);
            allSessionsContent.pivot = new Vector2(0.5f, 1f);
            allSessionsContent.anchoredPosition = Vector2.zero;
            allSessionsContent.sizeDelta = new Vector2(0, 0);
            SceneBuilderUtils.AddVerticalLayout(allSessionsContent.gameObject, spacing: 20,
                padding: new RectOffset(20, 20, 20, 20), alignment: TextAnchor.UpperCenter,
                expandWidth: true, expandHeight: false);
            var fitter = allSessionsContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = allSessionsContent;
            allSessionsPanel.SetActive(false);

            // ===== View 와이어링 =====
            var view = canvasGo.AddComponent<ReportView>();
            var so = new SerializedObject(view);
            so.FindProperty("backBtn").objectReferenceValue = backBtn.GetComponent<Button>();
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("listPanel").objectReferenceValue = listPanel;
            so.FindProperty("detailPanel").objectReferenceValue = detailPanel;
            so.FindProperty("speechTabBtn").objectReferenceValue = speechTab;
            so.FindProperty("arTabBtn").objectReferenceValue = arTab;
            // 탭 bg/텍스트색은 미사용(흰 pill이 표시기, 글자는 어둡게 고정) - 와이어링 생략(null, ReportView 가드됨)
            so.FindProperty("speechRoot").objectReferenceValue = speechRoot;
            so.FindProperty("arPlaceholder").objectReferenceValue = arPlaceholder;
            so.FindProperty("sessionCardPrefab").objectReferenceValue = sessionCardPrefab; // 레거시, 미사용

            // 한눈에 요약 4스탯 (값 텍스트 + 레벨 칭호)
            so.FindProperty("statCompletedText").objectReferenceValue = stat1Val;
            so.FindProperty("statStudyTimeText").objectReferenceValue = stat2Val;
            so.FindProperty("statStreakText").objectReferenceValue = stat3Val;
            so.FindProperty("statLevelText").objectReferenceValue = stat4Val;
            so.FindProperty("statLevelTitleText").objectReferenceValue = stat4Title;
            WireArray(so, "summaryButtons", new UnityEngine.Object[] { stat1Btn, stat2Btn, stat3Btn, stat4Btn });

            // 전체 보기 -> 전체 세션 목록 패널
            so.FindProperty("moreBtn").objectReferenceValue = moreBtn;
            so.FindProperty("allSessionsPanel").objectReferenceValue = allSessionsPanel;
            so.FindProperty("allSessionsContainer").objectReferenceValue = allSessionsContent;
            so.FindProperty("allSessionsEmpty").objectReferenceValue = allSessionsEmpty.gameObject;

            // 그래프 선
            so.FindProperty("sessionTrendLine").objectReferenceValue = g1Line1;
            so.FindProperty("completedTrendLine").objectReferenceValue = g1Line2;
            so.FindProperty("appearanceTrendLine").objectReferenceValue = g2Line1;
            WireArray(so, "graphButtons", new UnityEngine.Object[] { graph1Btn, graph2Btn });

            // 최근 학습 기록 행
            WireArray(so, "recordRows", recordRows);

            // 전체보기 세션 카드의 시나리오 아이콘 (편의점=매장, 약국=💊, 음식점=🍽)
            so.FindProperty("convenienceIcon").objectReferenceValue = LoadPhotoSprite(StoreIconPath);
            so.FindProperty("pharmacyIcon").objectReferenceValue = LoadEmoji("1F48A");
            so.FindProperty("restaurantIcon").objectReferenceValue = LoadEmoji("1F37D");
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
            if (director != null) so.FindProperty("characterDirector").objectReferenceValue = director;
            if (speechGo != null) so.FindProperty("characterSpeechBubble").objectReferenceValue = speechGo;
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
            colors.highlightedColor = new Color(0.95f, 0.97f, 1f, 1f);
            colors.pressedColor = new Color(0.90f, 0.94f, 1f, 1f);  // 전체 파랑 대신 은은한 연파랑
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.1f;
            btn.colors = colors;
            SceneBuilderUtils.AddLayoutElement(root, preferredHeight: 150);

            // 상태 배지 (좌측, 세로 중앙)
            var badge = ChildRect("StatusBadge", root.transform);
            AnchorLeft(badge, new Vector2(32, 0), new Vector2(108, 46));
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = Rounded(); badgeImg.type = Image.Type.Sliced; badgeImg.pixelsPerUnitMultiplier = 2f;
            badgeImg.raycastTarget = false;
            var badgeText = MakeText("Text", badge, "완료", 24, TitleColor, font, bold: true);
            StretchFull(badgeText.rectTransform, 2);

            // 시나리오 아이콘 (둥근 연파랑 박스 + 이모지)
            var iconBox = ChildRect("IconBox", root.transform);
            AnchorLeft(iconBox, new Vector2(156, 0), new Vector2(84, 84));
            var iconBoxImg = iconBox.gameObject.AddComponent<Image>();
            iconBoxImg.sprite = Rounded(); iconBoxImg.type = Image.Type.Sliced; iconBoxImg.pixelsPerUnitMultiplier = 2f;
            iconBoxImg.color = new Color32(232, 238, 250, 255); iconBoxImg.raycastTarget = false;
            var icon = ChildRect("Icon", iconBox);
            StretchFull(icon, 14);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true; iconImg.raycastTarget = false;

            // 이름 + 날짜
            var nameText = MakeText("Name", root.transform, "약국", 40, TitleColor, font, bold: true);
            nameText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(nameText.rectTransform, new Vector2(268, 22), new Vector2(440, 54));

            var dateText = MakeText("Date", root.transform, "", 26, SubColor, font, bold: false);
            dateText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(dateText.rectTransform, new Vector2(270, -30), new Vector2(500, 40));

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
            view.iconImage = iconImg;
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

        // 요약 패널 스탯: 값(굵게) + 라벨(작게)을 아이콘 오른쪽에 세로로. (아이콘은 패널 이미지에 박힘)
        // 클릭 영역(투명 버튼)을 깔아 카드 전체를 누를 수 있게 하고, 값/라벨 텍스트와 버튼을 반환.
        static Button AddSummaryStat(Transform parent, string value, string label, Vector2 valuePos, TMP_FontAsset font,
            out TMP_Text valueText, out TMP_Text labelText)
        {
            var hit = ChildRect("StatHit", parent);
            PlaceCenter(hit, new Vector2(valuePos.x, valuePos.y - 16), new Vector2(240, 110));
            var bg = hit.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            var btn = hit.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;

            valueText = MakeText("StatValue", parent, value, 34, TitleColor, font, bold: true);
            PlaceCenter(valueText.rectTransform, valuePos, new Vector2(220, 46));
            labelText = MakeText("StatLabel", parent, label, 22, SubColor, font, bold: false);
            PlaceCenter(labelText.rectTransform, new Vector2(valuePos.x, valuePos.y - 33), new Vector2(240, 36));
            return btn;
        }

        // 글래스 3D 패널: 뒤에 부드러운 그림자(깊이감) + L-Photoroom 글래스 카드(9-slice) + 좌상단 제목.
        static RectTransform MakeGlassPanel(Transform parent, string name, string title, Vector2 pos, Vector2 size, TMP_FontAsset font)
        {
            // 그림자 (패널보다 크고 아래로 살짝 -> 떠있는 3D 느낌)
            var shadow = ChildRect(name + "Shadow", parent);
            PlaceCenter(shadow, pos + new Vector2(0f, -12f), size + new Vector2(34f, 34f));
            var sImg = shadow.gameObject.AddComponent<Image>();
            sImg.sprite = SceneBuilderUtils.EnsureGlowSprite();
            sImg.color = new Color(0.10f, 0.16f, 0.30f, 0.18f);
            sImg.raycastTarget = false;

            // 글래스 카드
            var panel = ChildRect(name, parent);
            PlaceCenter(panel, pos, size);
            var img = panel.gameObject.AddComponent<Image>();
            var card = LoadGlassCard();
            if (card != null) { img.sprite = card; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; img.color = White; }
            else { img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; img.color = new Color(1f, 1f, 1f, 0.7f); }
            img.raycastTarget = false;

            var t = MakeText("Title", panel, title, 28, TitleColor, font, bold: true);
            t.alignment = TextAlignmentOptions.Left;
            PlaceCenter(t.rectTransform, new Vector2(-size.x / 2f + 132, size.y / 2f - 40), new Vector2(420, 44));
            return panel;
        }

        // L-Photoroom 글래스 카드 로드. 둥근 모서리 유지 위해 9-slice 테두리 설정 후 Sprite Single.
        static Sprite LoadGlassCard()
        {
            if (AssetImporter.GetAtPath(GlassCardPath) == null)
                AssetDatabase.ImportAsset(GlassCardPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(GlassCardPath) is TextureImporter ti)
            {
                bool changed = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; changed = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; changed = true; }
                var border = new Vector4(110, 110, 110, 110); // 모서리 반경 영역 고정
                if (ti.spriteBorder != border) { ti.spriteBorder = border; changed = true; }
                if (changed) ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(GlassCardPath);
        }

        // 꺾은선 그래프 패널: 글래스 + 제목 + 1~2개 선. 패널 전체 클릭 버튼과 LineChart 참조 반환.
        static Button BuildGraphPanel(Transform parent, string title, Vector2 pos, Vector2 size,
            float[] s1, Color c1, float[] s2, Color c2, bool fill, TMP_FontAsset font,
            out LineChart line1, out LineChart line2)
        {
            var panel = MakeGlassPanel(parent, "GraphPanel", title, pos, size, font);
            var plot = ChildRect("Plot", panel);
            plot.anchorMin = new Vector2(0, 0); plot.anchorMax = new Vector2(1, 1);
            plot.offsetMin = new Vector2(44, 34); plot.offsetMax = new Vector2(-44, -82);
            line1 = AddLine(plot, s1, c1, fill);
            line2 = s2 != null ? AddLine(plot, s2, c2, false) : null;

            // 패널 전체 클릭 영역 (선 위에 투명 버튼)
            var hit = ChildRect("GraphHit", panel);
            StretchFull(hit, 0);
            var bg = hit.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            var btn = hit.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            return btn;
        }

        static LineChart AddLine(RectTransform plot, float[] vals, Color c, bool fill)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(plot, false);
            StretchFull(go.GetComponent<RectTransform>(), 0);
            var lc = go.AddComponent<LineChart>();
            var so = new SerializedObject(lc);
            if (fill) so.FindProperty("fillUnder").boolValue = true;
            var arr = so.FindProperty("values");
            arr.arraySize = vals.Length;
            for (int i = 0; i < vals.Length; i++) arr.GetArrayElementAtIndex(i).floatValue = vals[i];
            so.ApplyModifiedProperties();
            lc.color = c;
            lc.raycastTarget = false;
            return lc;
        }

        // 최근 학습 기록 패널: 글래스 + 제목 + 클릭 가능한 행 3개. 값은 런타임에 ReportView가 채움.
        static ReportRecordRow[] BuildRecordsPanel(Transform parent, Vector2 pos, Vector2 size, TMP_FontAsset font)
        {
            var panel = MakeGlassPanel(parent, "RecordsPanel", "최근 학습 기록", pos, size, font);
            float top = size.y / 2f - 108;
            return new[]
            {
                AddRecordRow(panel, top,        size.x, font),
                AddRecordRow(panel, top - 72,   size.x, font),
                AddRecordRow(panel, top - 144,  size.x, font),
            };
        }

        // 기록 행: 텍스트 3개(이름/날짜/포인트)를 행 컨테이너에 담고 행 전체를 투명 버튼으로. ReportRecordRow 반환.
        static ReportRecordRow AddRecordRow(RectTransform panel, float y, float panelW, TMP_FontAsset font)
        {
            float rw = panelW - 40f;
            var row = ChildRect("RecordRow", panel);
            PlaceCenter(row, new Vector2(0, y - 14), new Vector2(rw, 64));
            var rowBg = row.gameObject.AddComponent<Image>();
            rowBg.color = new Color(0f, 0f, 0f, 0f);
            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = rowBg;

            float left = -rw / 2f + 40f;
            var nm = MakeText("RecName", row, "", 24, TitleColor, font, bold: true);
            nm.alignment = TextAlignmentOptions.Left;
            PlaceCenter(nm.rectTransform, new Vector2(left + 200, 14), new Vector2(400, 34));
            var dt = MakeText("RecDate", row, "", 18, SubColor, font, bold: false);
            dt.alignment = TextAlignmentOptions.Left;
            PlaceCenter(dt.rectTransform, new Vector2(left + 200, -14), new Vector2(400, 26));
            var p = MakeText("RecPts", row, "", 24, new Color(0.16f, 0.66f, 0.4f, 1f), font, bold: true);
            PlaceCenter(p.rectTransform, new Vector2(rw / 2f - 60, 0), new Vector2(110, 38));

            var view = row.gameObject.AddComponent<ReportRecordRow>();
            view.nameText = nm; view.dateText = dt; view.pointsText = p; view.button = btn;
            return view;
        }

        // 작은 텍스트 버튼 (bg 투명, 필터용)
        static Button MakeSmallTextButton(string name, RectTransform parent, string label, Vector2 pos, TMP_FontAsset font)
        {
            var rect = ChildRect(name, parent);
            PlaceCenter(rect, pos, new Vector2(150, 56));
            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var t = MakeText("Text", rect, label, 24, TitleColor, font, bold: true);
            StretchFull(t.rectTransform, 2);
            return btn;
        }

        // 투명 클릭 영역 버튼 (표시 그래픽 없음, 레이캐스트만). 슬라이더 행 선택용.
        static Button MakeHitButton(string name, RectTransform parent, Vector2 pos, Vector2 size)
        {
            var rect = ChildRect(name, parent);
            PlaceCenter(rect, pos, size);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f); // 완전 투명, 레이캐스트 대상
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            return btn;
        }

        static Button MakeTabButton(string name, RectTransform parent, string label, Vector2 pos, TMP_FontAsset font, out Image bg, out TMP_Text text)
        {
            var rect = ChildRect(name, parent);
            PlaceCenter(rect, pos, new Vector2(290, 76));
            bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f); // 투명 (레이캐스트만) - 글래스 pill이 표시기
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            text = MakeText("Text", rect, label, 30, TitleColor, font, bold: true);
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

        // openmoji 컬러 SVG → Sprite (code = 유니코드, 예: "1F48A"). 없으면 null.
        static Sprite LoadEmoji(string code)
        {
            string path = OpenmojiDir + code + ".svg";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is Sprite s) return s;
            Debug.LogWarning($"[ReportSceneBuilder] 이모지 없음: {path}");
            return null;
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

        // SerializedObject 배열 프로퍼티를 한 번에 채움.
        static void WireArray(SerializedObject so, string prop, UnityEngine.Object[] items)
        {
            var p = so.FindProperty(prop);
            p.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
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

        // 클립 폴더에서 prefix_01..NN.png 프레임을 순서대로 Sprite로 로드. 빠진 건 건너뜀.
        static Sprite[] LoadFrames(string sub, string prefix, int count)
        {
            var list = new System.Collections.Generic.List<Sprite>(count);
            for (int i = 1; i <= count; i++)
            {
                var s = LoadPhotoSprite($"{AnimDir}/{sub}/{prefix}_{i:D2}.png");
                if (s != null) list.Add(s);
            }
            if (list.Count == 0)
                Debug.LogWarning($"[ReportSceneBuilder] 프레임 없음: {AnimDir}/{sub} (임포트 확인 필요)");
            return list.ToArray();
        }

        // 첫 프레임 높이를 CharTargetHeight로 정규화한 표시 크기. 클립 간 캐릭터 크기 일관성 확보.
        static Vector2 SizeFor(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
                return new Vector2(300f, CharTargetHeight);
            var r = frames[0].rect;
            float s = r.height > 1f ? CharTargetHeight / r.height : 1f;
            return new Vector2(r.width * s, r.height * s);
        }

        // 직렬화된 ReportCharacterDirector.Clip(frames/fps/size) 한 개 채우기.
        static void SetClip(SerializedObject so, string name, Sprite[] frames, float fps, Vector2 size)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            var framesP = p.FindPropertyRelative("frames");
            framesP.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                framesP.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            p.FindPropertyRelative("fps").floatValue = fps;
            p.FindPropertyRelative("size").vector2Value = size;
        }

        // 사진 PNG를 Sprite로 로드 (미임포트/타입 불일치면 Sprite Single로 보정).
        static Sprite LoadPhotoSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
