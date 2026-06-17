using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.AAC;
using Artti.Training;

namespace Artti.Editor
{
    // 편의점 훈련 씬 전용 빌더. 가로 1920x1080 태블릿. (시안: 바탕화면 1~10.png)
    // 배경 사진 + 캐릭터 RenderTexture 자리 + 상단 스테퍼/일시정지 + NPC 말풍선(스피커) +
    // 하단 AAC 카드 가로 풀 + 사용자 발화 버블 + 생각중/칭찬 + 완료 화면.
    // 공유 TrainingSceneBuilder(약국 동결)는 건드리지 않는다.
    public static class ConvenienceTrainingSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color32 Primary    = new Color32(26, 86, 219, 255);   // #1A56DB
        static readonly Color32 Accent     = new Color32(255, 138, 61, 255);  // 진행 주황
        static readonly Color32 TitleColor = new Color32(33, 41, 60, 255);
        static readonly Color32 SubColor   = new Color32(110, 118, 135, 255);
        static readonly Color32 LightGray  = new Color32(238, 240, 244, 255);
        static readonly Color32 DotPending = new Color32(210, 216, 226, 255);
        static readonly Color   White      = Color.white;

        const string RoundedPath   = "Assets/_Project/Art/UI/RoundedRect.png";
        // 점원: Blender 제작 ARTTI_Clerk.fbx (리깅+애니 포함, Generic 임포트). 기존 VRoid Clerk_Rigged.prefab 대체.
        const string ClerkFbxPath        = "Assets/_Project/Models/Clerk/ARTTI_Clerk.fbx";
        const string ClerkControllerPath = "Assets/_Project/Art/ARTTIClerkController.controller";
        // Blender 제작 편의점 전체 매장. 기존 Env_Convenience(Tripo) + 프리미티브 카운터/POS를 이걸로 대체.
        const string StoreFbxPath    = "Assets/_Project/Models/Props/ConvenienceStore/ARTTI_Store.fbx";
        const string LipSyncProfilePath = "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Female.asset";
        const string PauseIconPath = "Assets/_Project/Art/UI/icon_pause.svg";
        const string SpeakerIconPath = "Assets/_Project/Art/UI/icon_volume_up.svg";
        const string AacDbPath     = "Assets/_Project/_Data/AAC/AACDatabase.asset";
        const string StageMatDir   = "Assets/_Project/Art/Stage";

        // 3D 무대 배치값 (첫 추정 — Unity에서 보고 조정한 뒤 이 값들을 갱신해 재빌드)
        static readonly Vector3 ClerkPos    = new Vector3(0f, 0f, 0f);
        static readonly Vector3 ClerkEuler  = new Vector3(0f, 0f, 0f);  // 손님(카메라) 쪽을 바라보게 (입구 쪽 향함)
        // 매장 모델(ARTTI_Store.fbx) 배치 — 원점 기준. Blender 기준 정면 입구(-Y)가 Unity로 오면서
        // 회전이 어긋날 수 있으니 Unity에서 보고 StoreEuler/Scale 조정 후 이 값 갱신.
        static readonly Vector3 StorePos    = new Vector3(0f, 0f, 0f);
        static readonly Vector3 StoreEuler  = new Vector3(0f, 0f, 0f);
        static readonly Vector3 StoreScale  = new Vector3(1f, 1f, 1f);
        // 사용자가 Unity에서 수동 배치한 카메라 (간판·매장 전면이 다 보이는 와이드 샷). 입구 쪽에서 매장 안 -Z를 8.5° 내려다봄.
        static readonly Vector3 CamPos      = new Vector3(-0.04f, 2.75f, 11.66f);
        static readonly Vector3 CamEuler    = new Vector3(8.5f, 180f, 0f);

        const int PoolSlotCount = 4;   // 풀 모드 카드 수 (TrainingSceneRoot.PoolSize와 동일)
        const int ExtraSlotCount = 10;
        const int ConfettiCount = 18;
        const int StepCount = 5;       // 인사 → 물건 찾기 → 계산 → 후속 처리 → 작별

        [MenuItem("Artti/Build TrainingConvenienceScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.TrainingConvenience);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();

            // 3D 캐릭터(RenderTexture 합성)용 라이트 — 없으면 캐릭터가 검게 렌더됨
            var lightGo = new GameObject("[Directional Light]");
            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            var font = SceneBuilderUtils.GetKoreanFont();

            // [Managers] — TrainingSceneRoot + TTS AudioSource
            var managers = new GameObject("[Managers]");
            var sceneRootGo = new GameObject("TrainingSceneRoot");
            sceneRootGo.transform.SetParent(managers.transform);
            var ttsSource = sceneRootGo.AddComponent<AudioSource>();
            var sceneRoot = sceneRootGo.AddComponent<TrainingSceneRoot>();

            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);

            // ===== 3D 무대 (메인 카메라가 직접 렌더 — 평면 사진 + RT 합성 폐기) =====
            var clerkView = Build3DStage(sceneRootGo);

            // ===== 하단 AAC 카드 가로 풀 =====
            var cardRow = ChildRect("CardRow", canvasGo.transform);
            cardRow.anchorMin = new Vector2(0.5f, 0f);
            cardRow.anchorMax = new Vector2(0.5f, 0f);
            cardRow.pivot = new Vector2(0.5f, 0f);
            cardRow.anchoredPosition = new Vector2(0, 36);
            cardRow.sizeDelta = new Vector2(1860, 180);
            var rowHlg = cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowHlg.spacing = 24;
            rowHlg.childControlWidth = false;
            rowHlg.childControlHeight = false;
            rowHlg.childForceExpandWidth = false;
            rowHlg.childForceExpandHeight = false;
            rowHlg.childAlignment = TextAnchor.MiddleCenter;

            var poolSlots = new AACCardButton[PoolSlotCount];
            for (int i = 0; i < PoolSlotCount; i++)
                poolSlots[i] = CreateCardSlot($"CardSlot_{i + 1:00}", cardRow, new Vector2(430, 170), font);

            // 기타 버튼 (카드 풀에 없는 카드 모달)
            var extraBtn = MakePillButton("ExtraBtn", canvasGo.transform, "기타", 30, White, TitleColor, font);
            var extraRect = extraBtn.GetComponent<RectTransform>();
            extraRect.anchorMin = extraRect.anchorMax = new Vector2(1f, 0f);
            extraRect.pivot = new Vector2(1f, 0f);
            extraRect.anchoredPosition = new Vector2(-44, 236);
            extraRect.sizeDelta = new Vector2(170, 72);

            // ===== 사용자 발화 버블 (선택 카드 + "말해볼까요?") =====
            var userBubble = ChildRect("UserBubble", canvasGo.transform);
            userBubble.anchorMin = userBubble.anchorMax = new Vector2(0f, 0f);
            userBubble.pivot = new Vector2(0f, 0f);
            userBubble.anchoredPosition = new Vector2(60, 250);
            userBubble.sizeDelta = new Vector2(480, 130);
            var ubBorder = userBubble.gameObject.AddComponent<Image>();
            ubBorder.sprite = Rounded(); ubBorder.type = Image.Type.Sliced; ubBorder.pixelsPerUnitMultiplier = 1f;
            ubBorder.color = Primary;
            var ubBody = ChildRect("Body", userBubble);
            StretchFull(ubBody, 3);
            var ubBodyImg = ubBody.gameObject.AddComponent<Image>();
            ubBodyImg.sprite = Rounded(); ubBodyImg.type = Image.Type.Sliced; ubBodyImg.pixelsPerUnitMultiplier = 1f;
            ubBodyImg.color = White;
            var ubText = MakeText("Text", ubBody, "", 36, TitleColor, font, bold: true);
            PlaceTop(ubText.rectTransform, new Vector2(0, -16), new Vector2(440, 56));
            var ubSub = MakeText("Sub", ubBody, "말해볼까요?", 24, SubColor, font, bold: false);
            PlaceTop(ubSub.rectTransform, new Vector2(0, -78), new Vector2(440, 38));

            // ===== NPC 말풍선 (우상단) + 스피커 + STT 진행 테두리 =====
            var npcBubble = ChildRect("NPCBubble", canvasGo.transform);
            npcBubble.anchorMin = npcBubble.anchorMax = new Vector2(1f, 1f);
            npcBubble.pivot = new Vector2(1f, 1f);
            npcBubble.anchoredPosition = new Vector2(-48, -132);
            npcBubble.sizeDelta = new Vector2(600, 210);
            var npcImg = npcBubble.gameObject.AddComponent<Image>();
            npcImg.sprite = Rounded(); npcImg.type = Image.Type.Sliced; npcImg.pixelsPerUnitMultiplier = 1f;
            npcImg.color = White;

            var npcText = MakeText("NPCText", npcBubble, "안녕하세요!", 38, TitleColor, font, bold: false);
            npcText.alignment = TextAlignmentOptions.MidlineLeft;
            npcText.textWrappingMode = TextWrappingModes.Normal;
            npcText.rectTransform.anchorMin = Vector2.zero;
            npcText.rectTransform.anchorMax = Vector2.one;
            npcText.rectTransform.offsetMin = new Vector2(28, 20);
            npcText.rectTransform.offsetMax = new Vector2(-88, -20);

            var speakerBtn = ChildRect("SpeakerBtn", npcBubble);
            speakerBtn.anchorMin = speakerBtn.anchorMax = new Vector2(1f, 1f);
            speakerBtn.pivot = new Vector2(1f, 1f);
            speakerBtn.anchoredPosition = new Vector2(-14, -14);
            speakerBtn.sizeDelta = new Vector2(56, 56);
            var speakerHit = speakerBtn.gameObject.AddComponent<Image>();
            speakerHit.color = new Color(0, 0, 0, 0);
            var speakerButton = speakerBtn.gameObject.AddComponent<Button>();
            speakerButton.targetGraphic = speakerHit;
            var speakerIconRect = ChildRect("Icon", speakerBtn);
            StretchFull(speakerIconRect, 4);
            var speakerIconImg = speakerIconRect.gameObject.AddComponent<Image>();
            speakerIconImg.sprite = LoadSvgSprite(SpeakerIconPath);
            speakerIconImg.preserveAspect = true;
            speakerIconImg.raycastTarget = false;
            speakerIconImg.color = SubColor;

            var (borderRoot, borderTop, borderRight, borderBottom, borderLeft) = BuildNpcBorder(npcBubble);

            // ===== 생각하는 중 / 칭찬 (NPC 말풍선 아래) =====
            var thinking = ChildRect("ThinkingPill", canvasGo.transform);
            thinking.anchorMin = thinking.anchorMax = new Vector2(1f, 1f);
            thinking.pivot = new Vector2(1f, 1f);
            thinking.anchoredPosition = new Vector2(-48, -362);
            thinking.sizeDelta = new Vector2(320, 76);
            var thinkImg = thinking.gameObject.AddComponent<Image>();
            thinkImg.sprite = Rounded(); thinkImg.type = Image.Type.Sliced; thinkImg.pixelsPerUnitMultiplier = 1f;
            thinkImg.color = White;
            var thinkText = MakeText("Text", thinking, "생각하는 중", 30, TitleColor, font, bold: false);
            thinkText.alignment = TextAlignmentOptions.MidlineLeft;
            AnchorLeft(thinkText.rectTransform, new Vector2(28, 0), new Vector2(200, 50));
            var thinkDots = MakeText("Dots", thinking, "...", 30, TitleColor, font, bold: true);
            thinkDots.alignment = TextAlignmentOptions.MidlineLeft;
            AnchorLeft(thinkDots.rectTransform, new Vector2(228, 0), new Vector2(70, 50));

            // ===== 상단 스테퍼 (objective 5단계) =====
            var stepper = ChildRect("Stepper", canvasGo.transform);
            PlaceTop(stepper, new Vector2(0, -28), new Vector2(620, 76));
            var stepperImg = stepper.gameObject.AddComponent<Image>();
            stepperImg.sprite = Rounded(); stepperImg.type = Image.Type.Sliced; stepperImg.pixelsPerUnitMultiplier = 1f;
            stepperImg.color = White;

            var lineRect = ChildRect("Line", stepper);
            PlaceCenter(lineRect, new Vector2(0, 0), new Vector2(460, 4));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.color = DotPending;
            lineImg.raycastTarget = false;

            var dots = new Image[StepCount];
            for (int i = 0; i < StepCount; i++)
            {
                var dotRect = ChildRect($"Dot_{i + 1}", stepper);
                PlaceCenter(dotRect, new Vector2(-230 + i * 115, 0), new Vector2(24, 24));
                var dotImg = dotRect.gameObject.AddComponent<Image>();
                dotImg.sprite = Builtin("UI/Skin/Knob.psd");
                dotImg.color = i == 0 ? (Color)Accent : (Color)DotPending;
                dotImg.raycastTarget = false;
                dots[i] = dotImg;
            }

            var stepLabelChip = ChildRect("StepLabelChip", canvasGo.transform);
            PlaceTop(stepLabelChip, new Vector2(0, -114), new Vector2(240, 54));
            var chipImg = stepLabelChip.gameObject.AddComponent<Image>();
            chipImg.sprite = Rounded(); chipImg.type = Image.Type.Sliced; chipImg.pixelsPerUnitMultiplier = 2f;
            chipImg.color = White;
            var stepLabel = MakeText("Text", stepLabelChip, "인사하기", 28, Accent, font, bold: true);
            StretchFull(stepLabel.rectTransform, 4);

            // ===== 좌상단 일시정지 =====
            var pauseBtnRect = ChildRect("PauseBtn", canvasGo.transform);
            pauseBtnRect.anchorMin = pauseBtnRect.anchorMax = new Vector2(0f, 1f);
            pauseBtnRect.pivot = new Vector2(0f, 1f);
            pauseBtnRect.anchoredPosition = new Vector2(40, -36);
            pauseBtnRect.sizeDelta = new Vector2(92, 92);
            var pauseBg = pauseBtnRect.gameObject.AddComponent<Image>();
            pauseBg.sprite = Builtin("UI/Skin/Knob.psd");
            pauseBg.color = White;
            var pauseButton = pauseBtnRect.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pauseBg;
            var pauseIconRect = ChildRect("Icon", pauseBtnRect);
            StretchFull(pauseIconRect, 22);
            var pauseIconImg = pauseIconRect.gameObject.AddComponent<Image>();
            pauseIconImg.sprite = LoadSvgSprite(PauseIconPath);
            pauseIconImg.preserveAspect = true;
            pauseIconImg.raycastTarget = false;

            // ===== 기타 모달 =====
            var (extraModal, extraSlots, extraCloseBtn) = BuildExtraModal(canvasGo.transform, font);

            // ===== 일시정지 모달 (2.png) =====
            var pauseModal = SceneBuilderUtils.CreatePanel("PauseModal", canvasGo.transform);
            pauseModal.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var pmCard = ChildRect("Card", pauseModal.transform);
            PlaceCenter(pmCard, Vector2.zero, new Vector2(640, 360));
            var pmCardImg = pmCard.gameObject.AddComponent<Image>();
            pmCardImg.sprite = Rounded(); pmCardImg.type = Image.Type.Sliced; pmCardImg.pixelsPerUnitMultiplier = 1f;
            pmCardImg.color = White;
            var pmTitle = MakeText("Title", pmCard, "일시정지", 44, TitleColor, font, bold: true);
            PlaceCenter(pmTitle.rectTransform, new Vector2(0, 110), new Vector2(400, 60));
            var pmMsg = MakeText("Message", pmCard, "훈련 시나리오를 종료할까요?", 34, SubColor, font, bold: false);
            PlaceCenter(pmMsg.rectTransform, new Vector2(0, 30), new Vector2(540, 50));
            var pmCancel = MakePillButton("CancelBtn", pmCard, "취소", 34, LightGray, TitleColor, font);
            PlaceCenter(pmCancel.GetComponent<RectTransform>(), new Vector2(-140, -90), new Vector2(220, 84));
            var pmConfirm = MakePillButton("ConfirmBtn", pmCard, "종료", 34, Primary, White, font);
            PlaceCenter(pmConfirm.GetComponent<RectTransform>(), new Vector2(140, -90), new Vector2(220, 84));
            pauseModal.SetActive(false);

            // ===== 완료 화면 (컨페티 + 요약 + 버튼 3) =====
            var completion = SceneBuilderUtils.CreatePanel("CompletionPanel", canvasGo.transform);
            completion.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var confettiRoot = ChildRect("ConfettiRoot", completion.transform);
            StretchFull(confettiRoot, 0);
            BuildConfetti(confettiRoot);

            var cpCard = ChildRect("Card", completion.transform);
            PlaceCenter(cpCard, new Vector2(0, -10), new Vector2(880, 620));
            var cpCardImg = cpCard.gameObject.AddComponent<Image>();
            cpCardImg.sprite = Rounded(); cpCardImg.type = Image.Type.Sliced; cpCardImg.pixelsPerUnitMultiplier = 1f;
            cpCardImg.color = White;
            var cpTitle = MakeText("Title", cpCard, "참 잘했어요!", 56, TitleColor, font, bold: true);
            PlaceCenter(cpTitle.rectTransform, new Vector2(0, 220), new Vector2(600, 80));

            var cpScenario = MakeSummaryRow(cpCard, "ScenarioRow", "시나리오", 100, font);
            var cpDuration = MakeSummaryRow(cpCard, "DurationRow", "걸린 시간", 20, font);
            var cpSteps    = MakeSummaryRow(cpCard, "StepsRow", "진행 단계", -60, font);

            var cpRetry = MakePillButton("RetryBtn", cpCard, "다시 하기", 32, Primary, White, font);
            PlaceCenter(cpRetry.GetComponent<RectTransform>(), new Vector2(-290, -210), new Vector2(260, 88));
            var cpHub = MakePillButton("HubBtn", cpCard, "다른 시나리오", 32, LightGray, TitleColor, font);
            PlaceCenter(cpHub.GetComponent<RectTransform>(), new Vector2(0, -210), new Vector2(260, 88));
            var cpHome = MakePillButton("HomeBtn", cpCard, "홈으로", 32, LightGray, TitleColor, font);
            PlaceCenter(cpHome.GetComponent<RectTransform>(), new Vector2(290, -210), new Vector2(260, 88));
            completion.SetActive(false);

            // ===== TrainingUIView 와이어링 =====
            var uiView = canvasGo.AddComponent<TrainingUIView>();
            var soView = new SerializedObject(uiView);
            soView.FindProperty("npcDialoguePanel").objectReferenceValue = npcText;
            var slotsProp = soView.FindProperty("pharmacyCardSlots");
            slotsProp.arraySize = poolSlots.Length;
            for (int i = 0; i < poolSlots.Length; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = poolSlots[i];
            soView.FindProperty("extraButton").objectReferenceValue = extraBtn;
            soView.FindProperty("extraModal").objectReferenceValue = extraModal;
            soView.FindProperty("extraCloseButton").objectReferenceValue = extraCloseBtn;
            var exSlotsProp = soView.FindProperty("extraCardSlots");
            exSlotsProp.arraySize = extraSlots.Length;
            for (int i = 0; i < extraSlots.Length; i++)
                exSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = extraSlots[i];
            soView.FindProperty("npcBorderRoot").objectReferenceValue = borderRoot;
            soView.FindProperty("npcBorderTop").objectReferenceValue = borderTop;
            soView.FindProperty("npcBorderRight").objectReferenceValue = borderRight;
            soView.FindProperty("npcBorderBottom").objectReferenceValue = borderBottom;
            soView.FindProperty("npcBorderLeft").objectReferenceValue = borderLeft;
            soView.ApplyModifiedProperties();

            // ===== ConvenienceHudView 와이어링 =====
            var hud = canvasGo.AddComponent<ConvenienceHudView>();
            var soHud = new SerializedObject(hud);
            var dotsProp = soHud.FindProperty("stepDots");
            dotsProp.arraySize = dots.Length;
            for (int i = 0; i < dots.Length; i++)
                dotsProp.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
            soHud.FindProperty("stepLabel").objectReferenceValue = stepLabel;
            soHud.FindProperty("pauseBtn").objectReferenceValue = pauseButton;
            soHud.FindProperty("pauseModal").objectReferenceValue = pauseModal;
            soHud.FindProperty("pauseConfirmBtn").objectReferenceValue = pmConfirm;
            soHud.FindProperty("pauseCancelBtn").objectReferenceValue = pmCancel;
            soHud.FindProperty("speakerBtn").objectReferenceValue = speakerButton;
            soHud.FindProperty("speakerIcon").objectReferenceValue = speakerIconImg;
            soHud.FindProperty("ttsSource").objectReferenceValue = ttsSource;
            soHud.FindProperty("userBubble").objectReferenceValue = userBubble.gameObject;
            soHud.FindProperty("userBubbleText").objectReferenceValue = ubText;
            soHud.FindProperty("thinkingRoot").objectReferenceValue = thinking.gameObject;
            soHud.FindProperty("thinkingText").objectReferenceValue = thinkText;
            soHud.FindProperty("thinkingDots").objectReferenceValue = thinkDots;
            soHud.FindProperty("completionRoot").objectReferenceValue = completion;
            soHud.FindProperty("completionScenarioText").objectReferenceValue = cpScenario;
            soHud.FindProperty("completionDurationText").objectReferenceValue = cpDuration;
            soHud.FindProperty("completionStepsText").objectReferenceValue = cpSteps;
            soHud.FindProperty("retryBtn").objectReferenceValue = cpRetry;
            soHud.FindProperty("hubBtn").objectReferenceValue = cpHub;
            soHud.FindProperty("homeBtn").objectReferenceValue = cpHome;
            soHud.FindProperty("confettiRoot").objectReferenceValue = confettiRoot;
            soHud.ApplyModifiedProperties();

            // ===== TrainingSceneRoot 와이어링 =====
            var soRoot = new SerializedObject(sceneRoot);
            soRoot.FindProperty("scenarioId").stringValue = "convenience";
            soRoot.FindProperty("uiView").objectReferenceValue = uiView;
            soRoot.FindProperty("hud").objectReferenceValue = hud;
            var aacDb = AssetDatabase.LoadAssetAtPath<AACDatabase>(AacDbPath);
            if (aacDb != null)
                soRoot.FindProperty("aacDatabase").objectReferenceValue = aacDb;
            else
                Debug.LogWarning("[ConvenienceTrainingSceneBuilder] AACDatabase.asset 없음 — Tools/AAC/Import Seed Data 먼저 실행");
            if (clerkView != null)
                soRoot.FindProperty("clerkView").objectReferenceValue = clerkView;
            soRoot.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ConvenienceTrainingSceneBuilder] 완료 — ARTTI_Store 매장 + 점원 + 카메라 구성됨");
        }

        // ===== 3D 무대: 메인 카메라 + 편의점 환경 + 점원(Clerk) + 데스크/포스기 + 립싱크 =====
        // 점원에 ClerkView를 붙여 반환 (TrainingSceneRoot 와이어링용). 프리팹 없으면 null.
        static ClerkView Build3DStage(GameObject ttsGo)
        {
            // 메인 카메라 (URP가 UniversalAdditionalCameraData 자동 추가). AudioListener는 씬에 이미 존재
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.89f, 0.94f, 1f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            camGo.transform.position = CamPos;
            camGo.transform.rotation = Quaternion.Euler(CamEuler);

            // 편의점 매장 (Blender 제작 ARTTI_Store.fbx — 계산대/곤돌라/냉장고/상품/조명 전부 포함).
            // 기존 Env_Convenience 프리팹 + 프리미티브 카운터/POS는 실제 매장 모델로 대체.
            GameObject store = null;
            var storeFbx = AssetDatabase.LoadAssetAtPath<GameObject>(StoreFbxPath);
            if (storeFbx != null)
            {
                store = (GameObject)PrefabUtility.InstantiatePrefab(storeFbx);
                store.name = "ARTTI_Store";
                store.transform.position = StorePos;
                store.transform.rotation = Quaternion.Euler(StoreEuler);
                store.transform.localScale = StoreScale;
            }
            else Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] 매장 FBX 없음: {StoreFbxPath} — Blender에서 내보냈는지 확인");

            // 점원 (Blender 제작 ARTTI_Clerk.fbx — 리깅+애니 포함, Generic 임포트)
            var clerkFbx = AssetDatabase.LoadAssetAtPath<GameObject>(ClerkFbxPath);
            if (clerkFbx == null)
            {
                Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] 점원 FBX 없음: {ClerkFbxPath} — Blender에서 내보냈는지 확인");
                return null;
            }
            var clerk = (GameObject)PrefabUtility.InstantiatePrefab(clerkFbx);
            clerk.name = "Clerk";
            clerk.transform.position = ClerkPos;
            clerk.transform.rotation = Quaternion.Euler(ClerkEuler);

            // 블렌더에서 구운 애니메이션(Generic 클립)을 Animator 컨트롤러로 재생
            EnsureClerkAnimator(clerk);
            WireLipSync(ttsGo, clerk);

            // 점원 애니메이션 뷰 — 프로덕션 씬에서는 디버그 버튼 숨김
            var clerkView = clerk.AddComponent<ClerkView>();
            var soClerk = new SerializedObject(clerkView);
            soClerk.FindProperty("showDebugButtons").boolValue = false;
            soClerk.ApplyModifiedProperties();

            return clerkView;
        }

        // 블렌더 점원 FBX에 임베드된 Generic 애니메이션을 재생할 Animator + 컨트롤러 구성.
        static void EnsureClerkAnimator(GameObject clerk)
        {
            var animator = clerk.GetComponent<Animator>();
            if (animator == null) animator = clerk.AddComponent<Animator>();

            AnimationClip idle = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(ClerkFbxPath))
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__")) { idle = clip; break; }

            if (idle == null)
            {
                Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] 점원 FBX에 애니메이션 클립 없음 — Blender에서 액션을 구워 내보냈는지 확인: {ClerkFbxPath}");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ClerkControllerPath) != null)
                AssetDatabase.DeleteAsset(ClerkControllerPath);
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(ClerkControllerPath, idle);
            animator.runtimeAnimatorController = controller;

            // Generic 아바타가 있으면 연결 (없어도 Generic 클립은 재생됨)
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(ClerkFbxPath))
                if (obj is Avatar av) { animator.avatar = av; break; }
        }

        static void CreateBox(string name, Vector3 pos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = GetStageMaterial(name, color);
        }

        // URP/Lit 머티리얼을 에셋으로 1회 생성·재사용 (씬에 임베드되면 리로드 시 깨지므로 에셋화)
        static Material GetStageMaterial(string key, Color color)
        {
            string path = $"{StageMatDir}/{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            if (!AssetDatabase.IsValidFolder(StageMatDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Art");
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Stage");
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ===== 립싱크: TTS AudioSource(uLipSync 분석) → Clerk 얼굴 viseme 블렌드셰이프 =====
        static void WireLipSync(GameObject ttsGo, GameObject clerk)
        {
            // 블렌드셰이프가 가장 많은 SkinnedMeshRenderer = 얼굴 메시
            SkinnedMeshRenderer face = null;
            foreach (var smr in clerk.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0 &&
                    (face == null || smr.sharedMesh.blendShapeCount > face.sharedMesh.blendShapeCount))
                    face = smr;
            if (face == null)
            {
                Debug.LogWarning("[ConvenienceTrainingSceneBuilder] Clerk에 블렌드셰이프 메시 없음 — 립싱크 생략");
                return;
            }

            var ls = ttsGo.GetComponent<uLipSync.uLipSync>();
            if (ls == null) ls = ttsGo.AddComponent<uLipSync.uLipSync>();
            ls.profile = AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(LipSyncProfilePath);
            if (ls.profile == null)
                Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] uLipSync 프로파일 없음: {LipSyncProfilePath} — Inspector에서 수동 지정 필요");

            var bs = face.gameObject.GetComponent<uLipSync.uLipSyncBlendShape>();
            if (bs == null) bs = face.gameObject.AddComponent<uLipSync.uLipSyncBlendShape>();
            bs.skinnedMeshRenderer = face;
            // glTFast가 VRoid 모프를 과장 스케일로 임포트 → 기본 100/55면 입이 얼굴을 덮음.
            // 실측 적정값 3 (2026-06-15). 모델/임포터 바뀌면 Inspector에서 재조정.
            bs.maxBlendShapeValue = 3f;
            bs.smoothness = 0.1f;

            // VRoid viseme(Fcl_MTH_*) 우선, CC/일반 명칭 폴백
            MapPhoneme(bs, face, "A", "Fcl_MTH_A", "V_Open", "Jaw_Open", "Mouth_Open", "A");
            MapPhoneme(bs, face, "I", "Fcl_MTH_I", "V_Wide", "Mouth_Smile", "I");
            MapPhoneme(bs, face, "U", "Fcl_MTH_U", "V_Tight_O", "Mouth_Pucker", "U");
            MapPhoneme(bs, face, "E", "Fcl_MTH_E", "V_Dental_Lip", "E");
            MapPhoneme(bs, face, "O", "Fcl_MTH_O", "V_Tight_O", "O");

            UnityEditor.Events.UnityEventTools.AddPersistentListener(ls.onLipSyncUpdate, bs.OnLipSyncUpdate);
        }

        static void MapPhoneme(uLipSync.uLipSyncBlendShape bs, SkinnedMeshRenderer face, string phoneme, params string[] candidates)
        {
            var mesh = face.sharedMesh;
            foreach (var cand in candidates)
            {
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    var bsName = mesh.GetBlendShapeName(i);
                    if (bsName == cand || bsName.EndsWith("." + cand))
                    {
                        bs.AddBlendShape(phoneme, bsName);
                        return;
                    }
                }
            }
            Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] '{phoneme}' 음소 매핑 실패 — 후보 블렌드셰이프 없음: {string.Join(", ", candidates)}");
        }

        // ===== 가로형 AAC 카드 슬롯 (아이콘 좌 + 문구 우) =====
        static AACCardButton CreateCardSlot(string name, RectTransform parent, Vector2 size, TMP_FontAsset font)
        {
            var row = ChildRect(name, parent);
            row.sizeDelta = size;

            var cardBox = ChildRect("CardBox", row);
            StretchFull(cardBox, 0);
            var bg = cardBox.gameObject.AddComponent<Image>();
            bg.sprite = Rounded(); bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1f;
            bg.color = White;
            var btn = cardBox.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.78f, 0.86f, 1f, 1f);
            colors.highlightedColor = new Color(0.93f, 0.96f, 1f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var iconRect = ChildRect("Icon", row);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(20, 0);
            iconRect.sizeDelta = new Vector2(size.y - 50, size.y - 50);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var labelRect = ChildRect("Label", row);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(size.y - 14, 8);
            labelRect.offsetMax = new Vector2(-16, -8);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "";
            label.fontSize = 36;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = TitleColor;
            label.raycastTarget = false;
            if (font != null) label.font = font;

            var cardButton = row.gameObject.AddComponent<AACCardButton>();
            var so = new SerializedObject(cardButton);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("phraseText").objectReferenceValue = label;
            so.FindProperty("button").objectReferenceValue = btn;
            so.ApplyModifiedProperties();
            return cardButton;
        }

        // ===== 기타 모달 (세로 리스트) =====
        static (GameObject modal, AACCardButton[] slots, Button closeBtn) BuildExtraModal(Transform canvasParent, TMP_FontAsset font)
        {
            var modal = SceneBuilderUtils.CreatePanel("ExtraModal", canvasParent);
            modal.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var card = ChildRect("Card", modal.transform);
            PlaceCenter(card, Vector2.zero, new Vector2(920, 820));
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = Rounded(); cardImg.type = Image.Type.Sliced; cardImg.pixelsPerUnitMultiplier = 1f;
            cardImg.color = White;
            SceneBuilderUtils.AddVerticalLayout(card.gameObject, spacing: 16, padding: new RectOffset(28, 28, 28, 28), alignment: TextAnchor.UpperCenter);

            var title = MakeText("Title", card, "다른 카드", 44, TitleColor, font, bold: true);
            SceneBuilderUtils.AddLayoutElement(title.gameObject, preferredHeight: 80);

            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(card, false);
            scrollGo.AddComponent<RectTransform>();
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 30f;
            var scrollLE = SceneBuilderUtils.AddLayoutElement(scrollGo);
            scrollLE.flexibleHeight = 1;
            scrollLE.flexibleWidth = 1;

            var viewport = ChildRect("Viewport", scrollGo.transform);
            StretchFull(viewport, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            scroll.viewport = viewport;

            var content = ChildRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            var slots = new AACCardButton[ExtraSlotCount];
            for (int i = 0; i < ExtraSlotCount; i++)
                slots[i] = CreateCardSlot($"ExtraSlot_{i + 1:00}", content, new Vector2(800, 150), font);

            var closeBtn = MakePillButton("CloseBtn", card, "닫기", 36, LightGray, TitleColor, font);
            SceneBuilderUtils.AddLayoutElement(closeBtn.gameObject, preferredHeight: 100);

            modal.SetActive(false);
            return (modal, slots, closeBtn);
        }

        // ===== NPC 말풍선 STT 진행 테두리 (시계방향 4변) =====
        const int BorderThickness = 6;

        static (GameObject root, Image top, Image right, Image bottom, Image left) BuildNpcBorder(RectTransform npcPanel)
        {
            var root = new GameObject("NPCSttBorder");
            root.transform.SetParent(npcPanel, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var top = MakeBorderEdge(root.transform, "Top",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0.5f),
                new Vector2(0, BorderThickness), Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);
            var right = MakeBorderEdge(root.transform, "Right",
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(BorderThickness, 0), Image.FillMethod.Vertical, (int)Image.OriginVertical.Top);
            var bottom = MakeBorderEdge(root.transform, "Bottom",
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0.5f),
                new Vector2(0, BorderThickness), Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Right);
            var left = MakeBorderEdge(root.transform, "Left",
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0),
                new Vector2(BorderThickness, 0), Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom);

            root.SetActive(false);
            return (root, top, right, bottom, left);
        }

        static Image MakeBorderEdge(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size,
            Image.FillMethod fillMethod, int fillOrigin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = Builtin("UI/Skin/UISprite.psd");
            img.color = Primary;
            img.raycastTarget = false;
            img.type = Image.Type.Filled;
            img.fillMethod = fillMethod;
            img.fillOrigin = fillOrigin;
            img.fillAmount = 0f;
            return img;
        }

        // ===== 컨페티 사각형들 (애니메이션은 ConvenienceHudView가 담당) =====
        static void BuildConfetti(RectTransform root)
        {
            Color32[] palette =
            {
                new Color32(26, 86, 219, 255), new Color32(255, 138, 61, 255),
                new Color32(46, 184, 114, 255), new Color32(250, 204, 21, 255),
                new Color32(236, 72, 153, 255)
            };
            for (int i = 0; i < ConfettiCount; i++)
            {
                var piece = ChildRect($"Confetti_{i + 1:00}", root);
                piece.anchorMin = piece.anchorMax = new Vector2(0.5f, 0.5f);
                piece.pivot = new Vector2(0.5f, 0.5f);
                piece.sizeDelta = new Vector2(18, 18);
                piece.anchoredPosition = new Vector2(0, 2000); // 첫 프레임 화면 밖
                var img = piece.gameObject.AddComponent<Image>();
                img.color = palette[i % palette.Length];
                img.raycastTarget = false;
            }
        }

        // ===== 공통 헬퍼 =====

        static TMP_Text MakeSummaryRow(RectTransform card, string name, string label, float y, TMP_FontAsset font)
        {
            var labelText = MakeText(name + "Label", card, label, 30, SubColor, font, bold: false);
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            PlaceCenter(labelText.rectTransform, new Vector2(-220, y), new Vector2(240, 50));
            var valueText = MakeText(name + "Value", card, "-", 36, TitleColor, font, bold: true);
            valueText.alignment = TextAlignmentOptions.MidlineRight;
            PlaceCenter(valueText.rectTransform, new Vector2(160, y), new Vector2(400, 50));
            return valueText;
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

        static Sprite LoadSceneSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] 배경 이미지 없음: {path}");
            return sprite;
        }

        static Sprite LoadSvgSprite(string path)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is Sprite s) return s;
            Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] SVG Sprite 없음: {path}");
            return null;
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
