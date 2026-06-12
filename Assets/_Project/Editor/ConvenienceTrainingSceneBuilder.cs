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
        const string BgPath        = "Assets/_Project/Art/UI/Scenario/bg_convenience.png";
        const string CamilaFbxPath = "Assets/Convai SDK For Unity/Samples/LipSyncSample/Characters/Camila/Camila.Fbx";
        const string IdleAnimFbxPath = "Assets/Convai SDK For Unity/Samples/BasicSample/Art/Animations/Convai_Anim_Sample_Locomotion_Idle_Loop.FBX";
        const string IdleControllerPath = "Assets/_Project/Art/CamilaIdle.controller";
        const string CharacterRTPath = "Assets/_Project/Art/UI/RT_Character.renderTexture";
        const string LipSyncProfilePath = "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Female.asset";
        const string PauseIconPath = "Assets/_Project/Art/UI/icon_pause.svg";
        const string SpeakerIconPath = "Assets/_Project/Art/UI/icon_volume_up.svg";
        const string AacDbPath     = "Assets/_Project/_Data/AAC/AACDatabase.asset";

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

            // ===== 배경 사진 (po.png) =====
            var bgPanel = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            var bgImg = bgPanel.AddComponent<Image>();
            bgImg.sprite = LoadSceneSprite(BgPath);
            bgImg.color = Color.white;

            // ===== 캐릭터 자리 (RenderTexture 수동 연결 후 활성화) =====
            var charView = ChildRect("CharacterView", canvasGo.transform);
            PlaceCenter(charView, new Vector2(-170, -110), new Vector2(700, 880));
            var charRaw = charView.gameObject.AddComponent<RawImage>();
            charRaw.raycastTarget = false;
            charView.gameObject.SetActive(false); // 캐릭터 리그 성공 시 활성화

            var camila = BuildCharacterRig(charView.gameObject, charRaw);
            if (camila != null)
                WireLipSync(sceneRootGo, camila);

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
            soRoot.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ConvenienceTrainingSceneBuilder] 완료 — Camila/카메라/RenderTexture 자동 구성됨");
        }

        // ===== 캐릭터 리그: Camila + Idle 애니 + RT 전용 카메라 + RawImage 연결 =====
        static GameObject BuildCharacterRig(GameObject charViewGo, RawImage charRaw)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CamilaFbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] Camila FBX 없음: {CamilaFbxPath} — CharacterView 비활성 유지");
                return null;
            }

            var camila = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            camila.name = "Camila";
            camila.transform.position = Vector3.zero;
            camila.transform.rotation = Quaternion.identity;

            // Idle 애니메이션 (T포즈 방지)
            var controller = EnsureIdleController();
            var animator = camila.GetComponentInChildren<Animator>();
            if (animator == null) animator = camila.AddComponent<Animator>();
            if (controller != null) animator.runtimeAnimatorController = controller;

            // RenderTexture 에셋 (1회 생성 후 재사용)
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(CharacterRTPath);
            if (rt == null)
            {
                rt = new RenderTexture(1024, 1280, 24) { name = "RT_Character" };
                AssetDatabase.CreateAsset(rt, CharacterRTPath);
            }

            // RT 전용 카메라 — 화면 출력 없음, AudioListener 미부착(씬에 이미 존재)
            var camGo = new GameObject("CharacterCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 알파 0 — 배경 사진 위 합성
            cam.targetTexture = rt;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            camGo.transform.position = new Vector3(0f, 1.45f, 1.15f);
            camGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            charRaw.texture = rt;
            charViewGo.SetActive(true);
            return camila;
        }

        // ===== 립싱크: TTS AudioSource(uLipSync 분석) → Camila 비짐 블렌드셰이프 =====
        static void WireLipSync(GameObject ttsGo, GameObject camila)
        {
            // 블렌드셰이프가 가장 많은 SkinnedMeshRenderer = 얼굴 메시
            SkinnedMeshRenderer face = null;
            foreach (var smr in camila.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0 &&
                    (face == null || smr.sharedMesh.blendShapeCount > face.sharedMesh.blendShapeCount))
                    face = smr;
            if (face == null)
            {
                Debug.LogWarning("[ConvenienceTrainingSceneBuilder] Camila에 블렌드셰이프 메시 없음 — 립싱크 생략");
                return;
            }

            var ls = ttsGo.AddComponent<uLipSync.uLipSync>();
            ls.profile = AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(LipSyncProfilePath);
            if (ls.profile == null)
                Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] uLipSync 프로파일 없음: {LipSyncProfilePath} — Inspector에서 수동 지정 필요");

            var bs = camila.AddComponent<uLipSync.uLipSyncBlendShape>();
            bs.skinnedMeshRenderer = face;

            // CC(Character Creator) 비짐 우선, 일반 명칭 폴백
            MapPhoneme(bs, face, "A", "V_Open", "Jaw_Open", "Mouth_Open", "A");
            MapPhoneme(bs, face, "I", "V_Wide", "Mouth_Smile", "I");
            MapPhoneme(bs, face, "U", "V_Tight_O", "Mouth_Pucker", "U");
            MapPhoneme(bs, face, "E", "V_Dental_Lip", "V_Wide", "E");
            MapPhoneme(bs, face, "O", "V_Tight_O", "V_Open", "O");

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

        static RuntimeAnimatorController EnsureIdleController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleControllerPath);
            if (existing != null) return existing;

            AnimationClip idle = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(IdleAnimFbxPath))
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview"))
                {
                    idle = clip;
                    break;
                }
            if (idle == null)
            {
                Debug.LogWarning($"[ConvenienceTrainingSceneBuilder] Idle 클립 없음: {IdleAnimFbxPath}");
                return null;
            }
            return UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(IdleControllerPath, idle);
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
