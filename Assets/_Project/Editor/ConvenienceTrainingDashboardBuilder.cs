using Artti.AAC;
using Artti.Training;
using Artti.UI;
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
