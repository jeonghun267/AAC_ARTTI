using Artti.AAC;
using Artti.Training;
using Artti.UI;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.Editor
{
    /// <summary>Builds the 3:2 convenience dashboard from the approved raster artwork.</summary>
    public static class ConvenienceTrainingDashboardBuilder
    {
        private static readonly Vector2 ReferenceResolution = new Vector2(1536f, 1024f);
        private static readonly Color Navy = new Color32(8, 20, 63, 242);
        private static readonly Color White = Color.white;
        private static readonly Color Ink = new Color32(26, 35, 58, 255);
        private const string Art = "Assets/_Project/Art/UI/Training/Dashboard/";
        private const string CompletionArt = "Assets/_Project/Art/UI/Training/Completion/";
        private const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";
        private const string AacDbPath = "Assets/_Project/_Data/AAC/AACDatabase.asset";

        private static readonly string[] ProductPaths =
        {
            Art + "product_can_coffee.png", Art + "product_water.png",
            Art + "product_milk.png", Art + "product_chocolate.png",
            Art + "product_gum.png", Art + "product_triangle_gimbap.png",
            Art + "product_sandwich.png", Art + "product_cup_ramen.png"
        };

        private static readonly string[] ProductIds =
        {
            "can_coffee", "water", "milk", "chocolate",
            "gum", "triangle_gimbap", "sandwich", "cup_ramen"
        };

        private static readonly string[] ProductNames =
        {
            "캔커피", "생수", "우유", "초콜릿", "껌", "삼각김밥", "샌드위치", "컵라면"
        };

        private static readonly string[] ProductUtterances =
        {
            "캔커피 주세요", "생수 주세요", "우유 주세요", "초콜릿 주세요",
            "껌 주세요", "삼각김밥 주세요", "샌드위치 주세요", "컵라면 주세요"
        };

        private static readonly string[] QuickPhrases =
        {
            "물 주세요", "어디에 있어요?", "얼마예요?", "계산할게요", "감사합니다"
        };

        [MenuItem("Artti/Preview Training Completion")]
        private static void PreviewCompletion()
        {
            foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rect.name != "CompletionRoot" || !rect.gameObject.scene.IsValid()) continue;
                rect.gameObject.SetActive(true);
                var help = rect.Find("CompletionHelpPanel");
                if (help != null) help.gameObject.SetActive(false);
                Selection.activeGameObject = rect.gameObject;
                SceneView.RepaintAll();
                Debug.Log("[ConvenienceTrainingDashboardBuilder] 완료 화면 미리보기 활성화 (씬 저장 전용 상태 아님)");
                return;
            }
            Debug.LogWarning("[ConvenienceTrainingDashboardBuilder] CompletionRoot 없음 — 씬 빌더를 먼저 실행하세요.");
        }

        [MenuItem("Artti/Capture Training Completion QA")]
        private static void CaptureCompletionQa()
        {
            Canvas canvas = null;
            RectTransform completion = null;
            foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (!rect.gameObject.scene.IsValid()) continue;
                if (rect.name == "CompletionRoot") completion = rect;
                if (rect.GetComponent<Canvas>() != null) canvas = rect.GetComponent<Canvas>();
            }
            if (canvas == null || completion == null)
            {
                Debug.LogWarning("[ConvenienceTrainingDashboardBuilder] 캡처 대상 없음 — 씬 빌더를 먼저 실행하세요.");
                return;
            }

            completion.gameObject.SetActive(true);
            var help = completion.Find("CompletionHelpPanel");
            if (help != null) help.gameObject.SetActive(false);

            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldPlaneDistance = canvas.planeDistance;
            var cameraGo = new GameObject("[CompletionQaCamera]");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var texture = new RenderTexture(1536, 1024, 24, RenderTextureFormat.ARGB32);
            texture.Create();
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
                text.ForceMeshUpdate(true, true);
            camera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = texture;
            var capture = new Texture2D(1536, 1024, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, 1536, 1024), 0, 0);
            capture.Apply();
            var outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../completion-implementation.png"));
            File.WriteAllBytes(outputPath, capture.EncodeToPNG());

            RenderTexture.active = previousActive;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldPlaneDistance;
            camera.targetTexture = null;
            texture.Release();
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraGo);
            Canvas.ForceUpdateCanvases();
            AssetDatabase.Refresh();
            Debug.Log($"[ConvenienceTrainingDashboardBuilder] 완료 화면 QA 캡처: {outputPath}");
        }

        public static void Build()
        {
            AssetDatabase.Refresh();
            SceneBuilderUtils.OpenScene(ScenePaths.TrainingConvenience);
            SceneBuilderUtils.ClearRootObjects();
            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();

            var font = SceneBuilderUtils.GetKoreanFont();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            BuildCamera();

            var managers = new GameObject("[Managers]");
            var rootGo = new GameObject("TrainingSceneRoot");
            rootGo.transform.SetParent(managers.transform, false);
            var ttsSource = rootGo.AddComponent<AudioSource>();
            var sceneRoot = rootGo.AddComponent<TrainingSceneRoot>();

            var background = CreateRaster("StoreBackground", canvasGo.transform, Art + "store_background.png", 0, 0, 1536, 1024, false);
            Stretch(background.rectTransform);
            var topShade = CreateColorPanel("TopShade", canvasGo.transform, 0, 0, 1536, 170, new Color(0.015f, 0.035f, 0.13f, 0.66f));
            StretchHorizontalTop(topShade.rectTransform, 170f);
            var bottomShade = CreateColorPanel("BottomShade", canvasGo.transform, 0, 760, 1536, 264, new Color(0.015f, 0.025f, 0.10f, 0.48f));
            StretchHorizontalBottom(bottomShade.rectTransform, 264f);
            var clerk = CreateRaster("Clerk", canvasGo.transform, Art + "clerk.png", 548, 182, 440, 660, true);
            PlaceTopCenter(clerk.rectTransform, 0, 182, 440, 660);
            var logo = CreateRaster("ARTTILogo", canvasGo.transform, Art + "artti_logo.png", 443, -38, 650, 325, true);
            PlaceTopCenter(logo.rectTransform, 0, -38, 650, 325);

            BuildProfile(canvasGo.transform, font);
            var mission = BuildMission(canvasGo.transform, font);
            var npc = BuildNpcBubble(canvasGo.transform, font);
            var dialogueHints = BuildDialogueHints(canvasGo.transform, font);
            var tip = CreateRaster("TodayTip", canvasGo.transform, Art + "tip_panel.png", 1190, 568, 310, 232, true);
            PlaceTopRight(tip.rectTransform, 36, 568, 310, 232);
            var recommended = BuildRecommendedProducts(canvasGo.transform, font);
            var voiceButton = BuildVoiceButton(canvasGo.transform);
            var chrome = BuildTopChrome(canvasGo.transform);
            var help = BuildHelpModal(canvasGo.transform, font);
            var pause = BuildPauseModal(canvasGo.transform, font);
            var completion = BuildCompletion(canvasGo.transform, font);

            var uiView = canvasGo.AddComponent<TrainingUIView>();
            var uiSo = new SerializedObject(uiView);
            uiSo.FindProperty("npcDialoguePanel").objectReferenceValue = npc.text;
            uiSo.FindProperty("freeTalkButton").objectReferenceValue = voiceButton;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = canvasGo.AddComponent<ConvenienceHudView>();
            var hudSo = new SerializedObject(hud);
            SetObjectArray(hudSo.FindProperty("stepDots"), mission.nodes);
            hudSo.FindProperty("stepLabel").objectReferenceValue = mission.label;
            hudSo.FindProperty("stepCounter").objectReferenceValue = mission.counter;
            hudSo.FindProperty("stepFillMask").objectReferenceValue = mission.fillMask;
            hudSo.FindProperty("stepFillWidth").floatValue = 240f;
            hudSo.FindProperty("stepNodeOnSprite").objectReferenceValue = LoadSprite(Art + "mission_step_on.png");
            hudSo.FindProperty("stepNodeOffSprite").objectReferenceValue = LoadSprite(Art + "mission_step_off.png");
            hudSo.FindProperty("pauseBtn").objectReferenceValue = chrome.settingsButton;
            hudSo.FindProperty("pauseModal").objectReferenceValue = pause.root;
            hudSo.FindProperty("pauseConfirmBtn").objectReferenceValue = pause.confirm;
            hudSo.FindProperty("pauseCancelBtn").objectReferenceValue = pause.cancel;
            hudSo.FindProperty("speakerBtn").objectReferenceValue = npc.replayButton;
            hudSo.FindProperty("ttsSource").objectReferenceValue = ttsSource;
            hudSo.FindProperty("completionRoot").objectReferenceValue = completion.root;
            hudSo.FindProperty("completionScenarioText").objectReferenceValue = completion.scenarioText;
            hudSo.FindProperty("completionDurationText").objectReferenceValue = completion.durationText;
            hudSo.FindProperty("completionProfileNameText").objectReferenceValue = completion.profileNameText;
            hudSo.FindProperty("retryBtn").objectReferenceValue = completion.retryButton;
            hudSo.FindProperty("hubBtn").objectReferenceValue = completion.nextButton;
            hudSo.FindProperty("homeBtn").objectReferenceValue = completion.homeButton;
            hudSo.FindProperty("completionHistoryBtn").objectReferenceValue = completion.historyButton;
            hudSo.FindProperty("completionTopHomeBtn").objectReferenceValue = completion.topHomeButton;
            hudSo.FindProperty("completionHelpBtn").objectReferenceValue = completion.helpButton;
            hudSo.FindProperty("completionHelpPanel").objectReferenceValue = completion.helpPanel;
            hudSo.FindProperty("completionHelpCloseBtn").objectReferenceValue = completion.helpCloseButton;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var dashboard = canvasGo.AddComponent<ConvenienceDashboardView>();
            var dashSo = new SerializedObject(dashboard);
            dashSo.FindProperty("productScroll").objectReferenceValue = recommended.scroll;
            SetObjectArray(dashSo.FindProperty("productButtons"), recommended.productButtons);
            SetStringArray(dashSo.FindProperty("productIds"), ProductIds);
            SetStringArray(dashSo.FindProperty("productNames"), ProductNames);
            SetStringArray(dashSo.FindProperty("productUtterances"), ProductUtterances);
            dashSo.FindProperty("previousButton").objectReferenceValue = recommended.previous;
            dashSo.FindProperty("nextButton").objectReferenceValue = recommended.next;
            SetObjectArray(dashSo.FindProperty("quickPhraseButtons"), dialogueHints.buttons);
            SetStringArray(dashSo.FindProperty("quickPhrases"), QuickPhrases);
            dashSo.FindProperty("helpButton").objectReferenceValue = chrome.helpButton;
            dashSo.FindProperty("helpCloseButton").objectReferenceValue = help.close;
            dashSo.FindProperty("helpPanel").objectReferenceValue = help.root;
            dashSo.ApplyModifiedPropertiesWithoutUndo();

            var rootSo = new SerializedObject(sceneRoot);
            rootSo.FindProperty("scenarioId").stringValue = "convenience";
            rootSo.FindProperty("uiView").objectReferenceValue = uiView;
            rootSo.FindProperty("hud").objectReferenceValue = hud;
            rootSo.FindProperty("dashboardView").objectReferenceValue = dashboard;
            rootSo.FindProperty("aacDatabase").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AACDatabase>(AacDbPath);
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            help.root.SetActive(false);
            pause.root.SetActive(false);
            completion.helpPanel.SetActive(false);
            completion.root.SetActive(false);
            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ConvenienceTrainingDashboardBuilder] PNG 대시보드 화면 생성 완료");
        }

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            camera.cullingMask = 0;
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static (
            GameObject root,
            TMP_Text scenarioText,
            TMP_Text durationText,
            TMP_Text profileNameText,
            Button retryButton,
            Button nextButton,
            Button homeButton,
            Button historyButton,
            Button helpButton,
            Button topHomeButton,
            GameObject helpPanel,
            Button helpCloseButton) BuildCompletion(Transform parent, TMP_FontAsset font)
        {
            var root = ChildRect("CompletionRoot", parent);
            Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.025f, 0.09f, 0.42f);

            // 완료 화면은 기존 대시보드 UI를 가리고 매장 배경만 보여야 한다.
            var completionBackground = CreateRaster("CompletionBackground", root,
                Art + "store_background.png", 0, 0, 1536, 1024, false);
            Stretch(completionBackground.rectTransform);
            var completionTint = CreateColorPanel("CompletionTint", root, 0, 0, 1536, 1024,
                new Color(0.015f, 0.035f, 0.12f, 0.48f));
            Stretch(completionTint.rectTransform);

            var frame = CreateRoundedPanel("CompletionFrame", root, 32, 36, 1472, 940,
                new Color(0.025f, 0.05f, 0.15f, 0.42f));
            frame.GetComponent<Image>().raycastTarget = false;
            var frameOutline = frame.AddComponent<Outline>();
            frameOutline.effectColor = new Color(0.72f, 0.82f, 1f, 0.24f);
            frameOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var headerShade = CreateRoundedPanel("HeaderShade", root, 34, 44, 1468, 108,
                new Color(0.02f, 0.045f, 0.14f, 0.48f));
            headerShade.GetComponent<Image>().raycastTarget = false;

            // 프로필 칩 — 실제 활성 프로필 닉네임은 ShowCompletion에서 덮어쓴다.
            var profile = CreateRoundedPanel("CompletionProfile", root, 49, 46, 286, 80,
                new Color(0.035f, 0.075f, 0.20f, 0.88f));
            var avatarMask = ChildRect("Avatar", profile.transform);
            Place(avatarMask, 10, 8, 64, 64);
            var maskImage = avatarMask.gameObject.AddComponent<Image>();
            maskImage.sprite = Builtin("UI/Skin/Knob.psd");
            maskImage.color = White;
            var avatarMaskComponent = avatarMask.gameObject.AddComponent<Mask>();
            avatarMaskComponent.showMaskGraphic = false;
            var avatarArt = ChildRect("ProfileArtwork", avatarMask);
            avatarArt.anchorMin = avatarArt.anchorMax = new Vector2(0.5f, 0.5f);
            avatarArt.pivot = new Vector2(0.5f, 0.5f);
            avatarArt.anchoredPosition = new Vector2(140f, -8f);
            avatarArt.sizeDelta = new Vector2(421f, 140f);
            var avatarImage = avatarArt.gameObject.AddComponent<Image>();
            avatarImage.sprite = LoadSprite(Art + "profile_panel.png");
            avatarImage.raycastTarget = false;
            var profileName = MakeText("ProfileName", profile.transform, "김연영님", 22, White, font,
                88, 8, 190, 36, true);
            profileName.overflowMode = TextOverflowModes.Overflow;
            MakeText("ProfileGreeting", profile.transform, "오늘도 고생했어요!", 15,
                new Color(0.86f, 0.90f, 1f), font, 88, 42, 180, 24, false);

            var historyButton = CreateRasterButton("HistoryButton", root,
                CompletionArt + "button_history.png", 1124, 23, 124, 165);
            var helpButton = CreateRasterButton("HelpButton", root,
                CompletionArt + "button_help.png", 1248, 23, 124, 165);
            var topHomeButton = CreateRasterButton("TopHomeButton", root,
                CompletionArt + "button_home_top.png", 1370, 23, 124, 165);

            // 축하 제목과 점원.
            CreateRaster("CompletionBadge", root, CompletionArt + "completion_badge.png",
                535, 86, 132, 132, true);
            CreateRaster("CompletionTitle", root, CompletionArt + "completion_title.png",
                695, 103, 310, 103, true);
            var clerkClip = ChildRect("CompletionClerkClip", root);
            Place(clerkClip, 0, 215, 495, 577);
            clerkClip.gameObject.AddComponent<RectMask2D>();
            CreateRaster("CompletionClerk", clerkClip, CompletionArt + "completion_clerk.png",
                -125, 20, 920, 1227, true);

            CreateRaster("SpeechBubble", root, CompletionArt + "speech_bubble.png",
                12, 290, 242, 194, true);
            var speech = MakeText("Speech", root, "정말 잘했어요!\n다음에도 함께해요", 18, Ink, font,
                61, 317, 168, 58, true);
            speech.textWrappingMode = TextWrappingModes.Normal;

            // 중앙 결과 카드.
            var card = CreateRoundedPanel("ResultCard", root, 495, 260, 620, 530,
                new Color(0.965f, 0.975f, 1f, 0.97f));
            card.GetComponent<Image>().raycastTarget = false;
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0.03f, 0.12f, 0.32f);
            cardShadow.effectDistance = new Vector2(0f, -8f);

            // 제공된 요약 스트립의 라벨/아이콘/상태는 그대로 쓰고 값만 런타임 텍스트로 올린다.
            CreateRaster("SummaryStrip", root, CompletionArt + "summary_strip.png",
                489, 220, 632, 211, true);
            var scenarioText = MakeText("ScenarioValue", root, "편의점", 27, Ink, font,
                578, 325, 150, 52, true);
            scenarioText.alignment = TextAlignmentOptions.MidlineLeft;
            scenarioText.overflowMode = TextOverflowModes.Overflow;
            var durationText = MakeText("DurationValue", root, "1분 이내", 27, Ink, font,
                785, 325, 170, 52, true);
            durationText.alignment = TextAlignmentOptions.MidlineLeft;
            durationText.overflowMode = TextOverflowModes.Overflow;

            var scorePanel = CreateRoundedPanel("ScorePanel", root, 520, 400, 570, 88,
                new Color(0.92f, 0.94f, 1f, 0.82f));
            scorePanel.GetComponent<Image>().raycastTarget = false;
            MakeText("ScoreLabel", root, "나의 점수", 22, Ink, font, 539, 421, 110, 40, true);
            for (int i = 0; i < 5; i++)
                CreateRaster($"ScoreStar_{i + 1}", root, CompletionArt + "score_star.png",
                    646 + i * 54, 407, 58, 58, true);
            var scoreText = MakeText("ScoreValue", root, "100점", 29, new Color32(45, 86, 225, 255),
                font, 978, 410, 110, 52, true);
            scoreText.alignment = TextAlignmentOptions.MidlineLeft;
            scoreText.overflowMode = TextOverflowModes.Overflow;

            var expCard = CreateRoundedPanel("ExperienceCard", root, 520, 505, 180, 165,
                new Color(0.95f, 0.96f, 1f, 0.92f));
            expCard.GetComponent<Image>().raycastTarget = false;
            CreateRaster("ExperienceArtwork", root, CompletionArt + "experience_panel.png",
                510, 520, 200, 94, true);
            var expTitle = MakeText("ExperienceTitle", root, "획득 경험치", 18, Ink, font,
                536, 521, 148, 30, true);
            expTitle.alignment = TextAlignmentOptions.Center;
            var expValue = MakeText("ExperienceValue", root, "+150", 24, Ink, font,
                618, 609, 70, 36, true);
            expValue.alignment = TextAlignmentOptions.Center;

            var levelCard = CreateRoundedPanel("LevelCard", root, 710, 505, 180, 165,
                new Color(0.95f, 0.96f, 1f, 0.92f));
            levelCard.GetComponent<Image>().raycastTarget = false;
            var levelTitle = MakeText("LevelTitle", root, "시나리오 레벨", 18, Ink, font,
                726, 521, 148, 30, true);
            levelTitle.alignment = TextAlignmentOptions.Center;
            var levelPill = CreateRoundedPanel("LevelPill", root, 762, 560, 76, 46,
                new Color32(64, 195, 139, 255));
            var levelText = MakeText("LevelValue", levelPill.transform, "Lv.2", 23, White, font,
                0, 0, 76, 46, true);
            levelText.alignment = TextAlignmentOptions.Center;
            CreateRaster("LevelMaster", root, CompletionArt + "level_master.png",
                718, 595, 164, 60, true);

            var badgeCard = CreateRoundedPanel("BadgeCard", root, 900, 505, 190, 165,
                new Color(0.95f, 0.96f, 1f, 0.92f));
            badgeCard.GetComponent<Image>().raycastTarget = false;
            CreateRaster("FirstPurchaseBadge", root, CompletionArt + "badge_first_purchase.png",
                900, 505, 190, 143, true);
            var badgeTitle = MakeText("BadgeTitle", root, "새 배지 획득!", 18, Ink, font,
                916, 521, 158, 30, true);
            badgeTitle.alignment = TextAlignmentOptions.Center;

            MakeText("ProgressTitle", root, "전체 시나리오 진행도", 18, Ink, font,
                535, 704, 200, 30, true);
            var progressTrack = CreateRoundedPanel("ProgressTrack", root, 535, 741, 405, 15,
                new Color32(190, 202, 236, 255));
            progressTrack.GetComponent<Image>().raycastTarget = false;
            var progressFill = CreateRoundedPanel("ProgressFill", root, 535, 741, 162, 15,
                new Color32(67, 103, 238, 255));
            progressFill.GetComponent<Image>().raycastTarget = false;
            MakeText("ProgressValue", root, "40%", 24, Ink, font, 952, 726, 65, 40, true);
            CreateRaster("ProgressGift", root, CompletionArt + "progress_gift.png",
                1002, 682, 92, 92, true);

            // 오른쪽 미션 하이라이트와 팁.
            CreateRaster("MissionHighlightPanel", root, CompletionArt + "mission_highlight_panel.png",
                1155, 217, 330, 396, true);
            CreateHighlight(root, font, "FindItem", CompletionArt + "highlight_search.png",
                1184, 292, "물건 찾기 성공!", "필요한 물건을 정확히 찾았어요");
            CreateHighlight(root, font, "Conversation", CompletionArt + "highlight_chat.png",
                1184, 386, "대화하기 성공!", "편의점 직원과 잘 대화했어요");
            CreateHighlight(root, font, "Payment", CompletionArt + "highlight_payment.png",
                1184, 480, "결제하기 성공!", "스스로 계산까지 완료했어요");
            CreateRaster("TodayTip", root, CompletionArt + "today_tip.png",
                1155, 532, 330, 248, true);

            // 화면 전체에 제공된 컨페티 에셋을 정적으로 배치한다.
            CreateRaster("ConfettiYellowRibbon", root, CompletionArt + "confetti_ribbon_yellow.png",
                394, 49, 70, 73, true);
            CreateRaster("ConfettiPurpleRibbon", root, CompletionArt + "confetti_ribbon_purple.png",
                1387, 779, 62, 62, true);
            CreateRaster("ConfettiPinkRibbon", root, CompletionArt + "confetti_ribbon_pink.png",
                1058, 172, 56, 56, true);
            CreateRaster("ConfettiCyanRibbon", root, CompletionArt + "confetti_ribbon_cyan.png",
                1005, 76, 58, 63, true);
            CreateRaster("ConfettiSparkle", root, CompletionArt + "confetti_sparkle.png",
                621, 44, 58, 58, true);
            CreateRaster("ConfettiDiamond", root, CompletionArt + "confetti_diamond.png",
                1032, 218, 48, 48, true);
            CreateRaster("ConfettiDiamondSmall", root, CompletionArt + "confetti_diamond_small.png",
                455, 218, 44, 44, true);

            var bottomShade = CreateRoundedPanel("BottomShade", root, 34, 792, 1468, 182,
                new Color(0.025f, 0.05f, 0.15f, 0.76f));
            bottomShade.GetComponent<Image>().raycastTarget = false;

            // 하단 핵심 동작 3개.
            var retryButton = CreateRasterButton("RetryButton", root,
                CompletionArt + "button_retry.png", 130, 735, 380, 285);
            var nextButton = CreateRasterButton("NextScenarioButton", root,
                CompletionArt + "button_next.png", 485, 700, 500, 368);
            var homeButton = CreateRasterButton("HomeButton", root,
                CompletionArt + "button_home.png", 955, 728, 450, 330);

            // 완료 화면 전용 도움말. 메인 완료 화면보다 나중 형제로 생성해 항상 위에 뜬다.
            var helpPanel = ChildRect("CompletionHelpPanel", root);
            Stretch(helpPanel);
            var helpDim = helpPanel.gameObject.AddComponent<Image>();
            helpDim.color = new Color(0f, 0f, 0f, 0.72f);
            var helpCard = CreateRoundedPanel("Card", helpPanel, 443, 285, 650, 430,
                new Color(0.035f, 0.075f, 0.23f, 0.98f));
            var helpTitle = MakeText("Title", helpCard.transform, "완료 화면 도움말", 32, White, font,
                50, 42, 550, 50, true);
            helpTitle.alignment = TextAlignmentOptions.Center;
            var helpBody = MakeText("Body", helpCard.transform,
                "• 다시 도전하기: 편의점 훈련을 처음부터 다시 시작해요.\n\n" +
                "• 다음 시나리오: 다른 훈련을 선택하는 화면으로 이동해요.\n\n" +
                "• 기록 보기: 방금 완료한 훈련 기록을 확인해요.",
                21, new Color(0.88f, 0.92f, 1f), font, 60, 120, 530, 190, false);
            helpBody.textWrappingMode = TextWrappingModes.Normal;
            var helpCloseButton = MakeTextButton("Close", helpCard.transform, "닫기", 23, font,
                225, 338, 200, 62, new Color32(45, 105, 230, 255));

            return (root.gameObject, scenarioText, durationText, profileName, retryButton, nextButton,
                homeButton, historyButton, helpButton, topHomeButton, helpPanel.gameObject, helpCloseButton);
        }

        private static void CreateHighlight(Transform parent, TMP_FontAsset font, string name, string iconPath,
            float x, float y, string title, string description)
        {
            CreateRaster(name + "Icon", parent, iconPath, x, y, 68, 68, true);
            MakeText(name + "Title", parent, title, 18, White, font, x + 78, y + 8, 205, 28, true);
            MakeText(name + "Description", parent, description, 13,
                new Color(0.88f, 0.92f, 1f), font, x + 78, y + 38, 205, 24, false);
        }

        private static void BuildProfile(Transform parent, TMP_FontAsset font)
        {
            var root = CreateRoundedPanel("Profile", parent, 28, 30, 300, 84, new Color(0.035f, 0.075f, 0.23f, 0.94f));
            var avatarMask = ChildRect("Avatar", root.transform);
            Place(avatarMask, 12, 10, 64, 64);
            var maskImage = avatarMask.gameObject.AddComponent<Image>();
            maskImage.sprite = Builtin("UI/Skin/Knob.psd");
            maskImage.color = White;
            var mask = avatarMask.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // The source has a baked checkerboard. Only its avatar is shown through a circular mask.
            var avatarArt = ChildRect("ProfileArtwork", avatarMask);
            avatarArt.anchorMin = avatarArt.anchorMax = new Vector2(0.5f, 0.5f);
            avatarArt.pivot = new Vector2(0.5f, 0.5f);
            avatarArt.anchoredPosition = new Vector2(140f, -8f);
            avatarArt.sizeDelta = new Vector2(421f, 140f);
            var avatarImage = avatarArt.gameObject.AddComponent<Image>();
            avatarImage.sprite = LoadSprite(Art + "profile_panel.png");
            avatarImage.raycastTarget = false;

            MakeText("Name", root.transform, "김연영님", 22, White, font, 88, 17, 150, 30, true);
            MakeText("Greeting", root.transform, "오늘도 반가워요!", 15, new Color(0.78f, 0.85f, 1f), font, 88, 46, 160, 24, false);
            var arrow = MakeText("Arrow", root.transform, "›", 42, White, font, 252, 17, 32, 50, false);
            arrow.alignment = TextAlignmentOptions.Center;
        }

        private static (Image[] nodes, TMP_Text label, TMP_Text counter, RectTransform fillMask) BuildMission(Transform parent, TMP_FontAsset font)
        {
            var root = ChildRect("Mission", parent);
            Place(root, 34, 166, 332, 222);
            var art = root.gameObject.AddComponent<Image>();
            art.sprite = LoadSprite(Art + "mission_panel.png");
            art.preserveAspect = true;
            art.raycastTarget = false;
            var label = MakeText("Objective", root, "점원에게 인사하기", 24, White, font, 38, 70, 250, 34, true);
            var counter = MakeText("Counter", root, "1 / 5 단계", 17, new Color(0.84f, 0.90f, 1f), font, 38, 103, 180, 26, false);

            var progress = ChildRect("Progress", root);
            Place(progress, 43, 130, 240, 28);
            var track = ChildRect("Track", progress);
            Stretch(track);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = LoadSprite(Art + "mission_progress_track.png");
            trackImage.raycastTarget = false;

            var fillMask = ChildRect("FillMask", progress);
            fillMask.anchorMin = fillMask.anchorMax = new Vector2(0f, 0.5f);
            fillMask.pivot = new Vector2(0f, 0.5f);
            fillMask.anchoredPosition = Vector2.zero;
            fillMask.sizeDelta = new Vector2(48f, 28f);
            fillMask.gameObject.AddComponent<RectMask2D>();
            AddFillPiece(fillMask, "Left", Art + "mission_progress_fill_left.png", 0, 0, 34, 28);
            AddFillPiece(fillMask, "Middle", Art + "mission_progress_fill_middle.png", 25, 0, 190, 28);
            AddFillPiece(fillMask, "Right", Art + "mission_progress_fill_right.png", 206, 0, 34, 28);

            var nodes = new Image[5];
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = ChildRect($"Step_{i + 1}", progress);
                node.anchorMin = node.anchorMax = new Vector2(0f, 0.5f);
                node.pivot = new Vector2(0.5f, 0.5f);
                node.anchoredPosition = new Vector2(i * 60f, 0f);
                node.sizeDelta = new Vector2(30f, 30f);
                var image = node.gameObject.AddComponent<Image>();
                image.sprite = LoadSprite(i == 0 ? Art + "mission_step_on.png" : Art + "mission_step_off.png");
                image.preserveAspect = true;
                image.raycastTarget = false;
                nodes[i] = image;
            }
            return (nodes, label, counter, fillMask);
        }

        private static (TMP_Text text, Button replayButton) BuildNpcBubble(Transform parent, TMP_FontAsset font)
        {
            var root = CreateRoundedPanel("NPCBubble", parent, 882, 236, 286, 118, new Color(1f, 1f, 1f, 0.96f));
            PlaceTopCenter(root.GetComponent<RectTransform>(), 257, 236, 286, 118);
            var text = MakeText("Text", root.transform, "찾으시는 물건이\n있으신가요?", 22, Ink, font, 24, 21, 235, 75, true);
            text.textWrappingMode = TextWrappingModes.Normal;
            var tail = ChildRect("Tail", root.transform);
            Place(tail, 18, 103, 30, 22);
            var tailImage = tail.gameObject.AddComponent<Image>();
            tailImage.color = new Color(1f, 1f, 1f, 0.96f);
            tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tailImage.raycastTarget = false;
            var replay = root.AddComponent<Button>();
            replay.targetGraphic = root.GetComponent<Image>();
            return (text, replay);
        }

        private static (Button[] buttons, string[] phrases) BuildDialogueHints(Transform parent, TMP_FontAsset font)
        {
            var root = ChildRect("DialogueHints", parent);
            PlaceTopRight(root, 36, 164, 310, 414);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(Art + "dialogue_hint_panel.png");
            image.preserveAspect = true;
            image.raycastTarget = false;
            string[] hints = QuickPhrases;
            var buttons = new Button[hints.Length];
            for (int i = 0; i < hints.Length; i++)
            {
                var dot = ChildRect($"AvatarDot_{i + 1}", root);
                Place(dot, 28, 91 + i * 61, 28, 28);
                var dotImage = dot.gameObject.AddComponent<Image>();
                dotImage.sprite = Builtin("UI/Skin/Knob.psd");
                dotImage.color = new Color32(35, 76, 143, 255);
                dotImage.raycastTarget = false;
                var initial = MakeText("Initial", dot, "A", 13, White, font, 0, 1, 28, 27, true);
                initial.alignment = TextAlignmentOptions.Center;
                var hint = MakeText($"Hint_{i + 1}", root, hints[i], 19, Ink, font, 68, 88 + i * 61, 208, 34, true);
                hint.alignment = TextAlignmentOptions.MidlineLeft;
                buttons[i] = MakeHitButton($"HintButton_{i + 1}", root, 16, 79 + i * 61, 278, 50);
            }
            return (buttons, hints);
        }

        private static (ScrollRect scroll, Button[] productButtons, Button previous, Button next) BuildRecommendedProducts(Transform parent, TMP_FontAsset font)
        {
            var panel = ChildRect("RecommendedProducts", parent);
            PlaceBottomCenter(panel, 0, -99, 780, 439);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = LoadSprite(Art + "recommended_panel.png");
            panelImage.preserveAspect = true;
            panelImage.raycastTarget = false;
            var viewport = ChildRect("ProductViewport", parent);
            // 네 장만 보이게 잘라 화살표 영역과 상품 버튼이 겹치지 않도록 한다.
            PlaceBottomCenter(viewport, 0, 34, 526, 166);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = ChildRect("Content", viewport);
            content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(ProductPaths.Length * 134f, 150f);
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var productButtons = new Button[ProductPaths.Length];
            for (int i = 0; i < ProductPaths.Length; i++)
            {
                var slot = ChildRect($"Product_{i + 1}_{ProductIds[i]}", content);
                slot.sizeDelta = new Vector2(120, 150);
                var size = slot.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = 120f;
                size.preferredHeight = 150f;
                var slotImage = slot.gameObject.AddComponent<Image>();
                slotImage.sprite = LoadSprite(ProductPaths[i]);
                slotImage.preserveAspect = true;
                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slotImage;
                productButtons[i] = button;
            }

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.12f;
            scroll.scrollSensitivity = 35f;

            var previous = MakeHitButton("Previous", parent, 0, 0, 58, 90);
            PlaceBottomCenter(previous.GetComponent<RectTransform>(), -342, 102, 58, 90);
            var next = MakeHitButton("Next", parent, 0, 0, 58, 90);
            PlaceBottomCenter(next.GetComponent<RectTransform>(), 341, 102, 58, 90);
            return (scroll, productButtons, previous, next);
        }

        private static Button BuildVoiceButton(Transform parent)
        {
            var root = ChildRect("VoiceButton", parent);
            PlaceBottomRight(root, 26, 74, 340, 114);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(Art + "voice_button.png");
            image.preserveAspect = true;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static (Button helpButton, Button settingsButton) BuildTopChrome(Transform parent)
        {
            var helpRoot = ChildRect("HelpButton", parent);
            PlaceTopRight(helpRoot, 88, 30, 66, 66);
            var helpImage = helpRoot.gameObject.AddComponent<Image>();
            helpImage.sprite = LoadSprite(Art + "button_help.png");
            helpImage.preserveAspect = true;
            var help = helpRoot.gameObject.AddComponent<Button>();
            help.targetGraphic = helpImage;
            var settingsRoot = ChildRect("SettingsButton", parent);
            PlaceTopRight(settingsRoot, 14, 30, 66, 66);
            var settingsImage = settingsRoot.gameObject.AddComponent<Image>();
            settingsImage.sprite = LoadSprite(Art + "button_settings.png");
            settingsImage.preserveAspect = true;
            var settings = settingsRoot.gameObject.AddComponent<Button>();
            settings.targetGraphic = settingsImage;
            return (help, settings);
        }

        private static (GameObject root, Button close) BuildHelpModal(Transform parent, TMP_FontAsset font)
        {
            var overlay = CreateOverlay("HelpModal", parent);
            var card = CreateRoundedPanel("Card", overlay.transform, 443, 287, 650, 450, new Color(0.035f, 0.075f, 0.23f, 0.98f));
            PlaceCenter(card.GetComponent<RectTransform>(), 0, 0, 650, 450);
            var title = MakeText("Title", card.transform, "화면 도움말", 34, White, font, 50, 42, 550, 52, true);
            title.alignment = TextAlignmentOptions.Center;
            var body = MakeText("Body", card.transform,
                "• 대화 힌트를 참고해 점원에게 말해보세요.\n\n• 음성으로 말하기 버튼을 누르면 자유 대화를 시작합니다.\n\n• 추천 상품은 좌우로 밀거나 화살표로 넘길 수 있어요.",
                22, new Color(0.86f, 0.91f, 1f), font, 64, 125, 522, 200, false);
            body.textWrappingMode = TextWrappingModes.Normal;
            var close = MakeTextButton("Close", card.transform, "닫기", 24, font, 225, 350, 200, 66, new Color32(45, 105, 230, 255));
            return (overlay, close);
        }

        private static (GameObject root, Button confirm, Button cancel) BuildPauseModal(Transform parent, TMP_FontAsset font)
        {
            var overlay = CreateOverlay("SettingsModal", parent);
            var card = CreateRoundedPanel("Card", overlay.transform, 493, 332, 550, 360, new Color(0.035f, 0.075f, 0.23f, 0.98f));
            PlaceCenter(card.GetComponent<RectTransform>(), 0, 0, 550, 360);
            var title = MakeText("Title", card.transform, "훈련 설정", 34, White, font, 50, 48, 450, 52, true);
            title.alignment = TextAlignmentOptions.Center;
            var message = MakeText("Message", card.transform, "훈련을 종료하고 시나리오 선택으로 돌아갈까요?", 21, new Color(0.86f, 0.91f, 1f), font, 54, 126, 442, 82, false);
            message.alignment = TextAlignmentOptions.Center;
            message.textWrappingMode = TextWrappingModes.Normal;
            var cancel = MakeTextButton("Cancel", card.transform, "계속하기", 22, font, 62, 250, 190, 64, new Color32(47, 65, 122, 255));
            var confirm = MakeTextButton("Confirm", card.transform, "훈련 종료", 22, font, 298, 250, 190, 64, new Color32(45, 105, 230, 255));
            return (overlay, confirm, cancel);
        }

        private static GameObject CreateOverlay(string name, Transform parent)
        {
            var root = ChildRect(name, parent);
            Stretch(root);
            var image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.68f);
            return root.gameObject;
        }

        private static Button MakeTextButton(string name, Transform parent, string text, float fontSize, TMP_FontAsset font,
            float x, float y, float width, float height, Color color)
        {
            var root = CreateRoundedPanel(name, parent, x, y, width, height, color);
            var button = root.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            var label = MakeText("Label", root.transform, text, fontSize, White, font, 0, 0, width, height, true);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static Button MakeHitButton(string name, Transform parent, float x, float y, float width, float height)
        {
            var root = ChildRect(name, parent);
            Place(root, x, y, width, height);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            return button;
        }

        private static GameObject CreateRoundedPanel(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var root = ChildRect(name, parent);
            Place(root, x, y, width, height);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = Rounded();
            image.type = Image.Type.Sliced;
            image.color = color;
            return root.gameObject;
        }

        private static Image CreateColorPanel(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var root = ChildRect(name, parent);
            Place(root, x, y, width, height);
            var image = root.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateRaster(string name, Transform parent, string path, float x, float y, float width, float height, bool preserveAspect)
        {
            var root = ChildRect(name, parent);
            Place(root, x, y, width, height);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(path);
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateRasterButton(string name, Transform parent, string path,
            float x, float y, float width, float height)
        {
            var image = CreateRaster(name, parent, path, x, y, width, height, true);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static void AddFillPiece(Transform parent, string name, string path, float x, float y, float width, float height)
        {
            var root = ChildRect(name, parent);
            Place(root, x, y, width, height);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(path);
            image.raycastTarget = false;
        }

        private static TMP_Text MakeText(string name, Transform parent, string value, float fontSize, Color color,
            TMP_FontAsset font, float x, float y, float width, float height, bool bold)
        {
            var root = ChildRect(name, parent);
            Place(root, x, y, width, height);
            var text = root.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceTopCenter(RectTransform rect, float xOffset, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(xOffset, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceTopRight(RectTransform rect, float right, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceBottomCenter(RectTransform rect, float xOffset, float bottom, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(xOffset, bottom);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceBottomRight(RectTransform rect, float right, float bottom, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-right, bottom);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceCenter(RectTransform rect, float xOffset, float yOffset, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xOffset, yOffset);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void StretchHorizontalTop(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void StretchHorizontalBottom(RectTransform rect, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static Sprite[] LoadSprites(string[] paths)
        {
            var result = new Sprite[paths.Length];
            for (int i = 0; i < paths.Length; i++) result[i] = LoadSprite(paths[i]);
            return result;
        }

        private static Sprite LoadSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = importer.textureType != TextureImporterType.Sprite
                             || importer.spriteImportMode != SpriteImportMode.Single
                             || importer.mipmapEnabled
                             || importer.maxTextureSize < 4096;
                if (dirty)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100f;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 4096;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[ConvenienceTrainingDashboardBuilder] Sprite 없음: {path}");
            return sprite;
        }

        private static Sprite Rounded()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return sprite != null ? sprite : Builtin("UI/Skin/UISprite.psd");
        }

        private static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        private static void SetObjectArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; values != null && i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void SetStringArray(SerializedProperty property, string[] values)
        {
            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; values != null && i < values.Length; i++)
                property.GetArrayElementAtIndex(i).stringValue = values[i];
        }

    }
}
