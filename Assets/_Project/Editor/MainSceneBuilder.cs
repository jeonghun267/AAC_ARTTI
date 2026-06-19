using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Artti.UI;

namespace Artti.Editor
{
    // 메인(모드 선택) 화면 — "살아있는" 버전.
    // 시안: 바탕화면 Open/Close.png. 캐릭터 컷아웃: Art/UI/Home/{Girl,GirlClose,Man,ManClose}.png
    //
    // 핵심 연출
    //  - 캔버스를 Screen Space - Camera + URP 카메라(HDR/PostProcessing)로 구성 → URP Bloom 적용 가능
    //  - AAC 글자에만 HDR FaceColor → Bloom으로 빛남(HomeBloomText)
    //  - 배경/타이틀/카드/버튼/장식은 전부 오브젝트로 분해(나중에 수정 쉽게), 장식은 에셋 emoji(openmoji) 사용
    //  - 캐릭터 Idle(호흡/흔들림/끄덕임) + 눈 깜빡임(Open/Close 스프라이트 교체)
    //  - 버튼 Hover Glow, 진입 애니메이션(AAC 왼쪽 슬라이드 / 카드 위에서 / 캐릭터 확대)
    //  - AR 카드 뒤 OCR 아이콘(돋보기 emoji)
    //  - 패럴랙스는 선택: HomeParallax 컴포넌트로 넣되 기본 비활성
    public static class MainSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color32 Primary    = new Color32(26, 86, 219, 255);    // #1A56DB (AAC)
        static readonly Color32 TrainAccent= new Color32(124, 58, 237, 255);   // #7C3AED 보라
        static readonly Color32 ARAccent   = new Color32(37, 99, 235, 255);    // #2563EB 파랑
        static readonly Color32 BgColor    = new Color32(244, 244, 251, 255);  // 연보라 배경
        static readonly Color32 TitleColor = new Color32(33, 41, 60, 255);
        static readonly Color32 SubColor   = new Color32(110, 118, 135, 255);
        static readonly Color   White      = Color.white;

        const string RoundedPath  = "Assets/_Project/Art/UI/RoundedRect.png";
        const string HomeDir      = "Assets/_Project/Art/UI/Home/";
        const string GirlOpen     = HomeDir + "Girl.png";
        const string GirlClose    = HomeDir + "GirlClose.png";
        const string ManOpen      = HomeDir + "Man.png";
        const string ManClose     = HomeDir + "ManClose.png";
        const string GirlBack     = HomeDir + "GirlBack.png";
        const string ManBack      = HomeDir + "ManBack.png";
        const string VolumePath   = HomeDir + "HomeVolume.asset";
        const string StickerDir   = HomeDir + "Stickers/";

        const string EmojiDir   = "Assets/_Project/openmoji-master/color/svg/";
        const string Sparkle    = EmojiDir + "2728.svg";   // ✨
        const string Leaf       = EmojiDir + "1F343.svg";  // 🍃
        const string Herb       = EmojiDir + "1F33F.svg";  // 🌿
        const string Bubble     = EmojiDir + "1F4AC.svg";  // 💬
        const string OcrIcon    = EmojiDir + "1F50D.svg";  // 🔍 OCR(스캔)
        const string Bulb       = EmojiDir + "1F4A1.svg";  // 💡
        const string IconExit   = EmojiDir + "E0A8.svg";   // 비상구

        // 세로로 긴 카드 (배경 이미지는 약간 세로로 늘어남)
        static readonly Vector2 CardSize = new Vector2(500, 690);

        [MenuItem("Artti/Build MainScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.Main);
            SceneBuilderUtils.ClearRootObjects();
            SceneBuilderUtils.CreateEventSystem();

            // ===== URP 카메라 (Bloom용 PostProcessing + HDR) =====
            var camGo = new GameObject("[UICamera]") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = (Color)BgColor;
            cam.allowHDR = true;
            cam.cullingMask = ~0;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            // ===== 글로벌 Volume (Bloom / Vignette / Color / Tonemapping) =====
            var volGo = new GameObject("[PostFX Volume]");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.sharedProfile = EnsureVolumeProfile();

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && !urp.supportsHDR)
                Debug.LogWarning("[MainSceneBuilder] URP Asset의 HDR이 꺼져 있어 AAC Bloom이 약하게 보일 수 있습니다. " +
                                 "Project Settings > Quality(또는 URP Asset) > HDR 체크 권장.");

            // ===== 캔버스 (Screen Space - Camera) =====
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            var font = SceneBuilderUtils.GetKoreanFont();

            // ===== 배경 (베이스) =====
            var bg = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color32(233, 236, 248, 255); // 베이스(밝게 — 어둡지 않게). 경계는 드롭섀도우로
            bgImg.raycastTarget = false;

            // ===== 떠 있는 글래스 패널 (베이스 위에 한 겹 떠 있는 느낌, 90.PNG) =====
            // 그림자는 아래쪽으로만 은은하게 (가장자리 전체를 어둡게 만들지 않도록)
            // 그림자는 패널보다 좌우로 살짝 "안쪽"으로(가장자리 세로 진한선 방지) + 아래로만 떨어지는 드롭섀도우.
            var panelShadow = ChildRect("GlassPanelShadow", canvasGo.transform);
            panelShadow.anchorMin = Vector2.zero; panelShadow.anchorMax = Vector2.one;
            panelShadow.offsetMin = new Vector2(58, 14); panelShadow.offsetMax = new Vector2(-58, -50);
            var psImg = panelShadow.gameObject.AddComponent<Image>();
            psImg.sprite = SceneBuilderUtils.EnsureGlowSprite(); psImg.type = Image.Type.Sliced;
            psImg.color = new Color(0.16f, 0.20f, 0.42f, 0.18f); psImg.raycastTarget = false; // 떠 보이는 드롭섀도우(연하게)

            var panel = ChildRect("GlassPanel", canvasGo.transform);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(42, 42); panel.offsetMax = new Vector2(-42, -42);
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = Rounded(); panelImg.type = Image.Type.Sliced; panelImg.pixelsPerUnitMultiplier = 0.7f; // 코너 더 둥글게
            panelImg.color = new Color(1f, 1f, 1f, 0.96f); // 밝은 흰 패널(베이스와 대비로 경계 또렷)
            panelImg.raycastTarget = false;

            // 배경 장식 레이어(패럴랙스 대상) — 부드러운 후광 + 살아있는 스티커
            var decor = ChildRect("BackgroundDecor", canvasGo.transform);
            StretchFull(decor, 0);
            // 사용자 손배치 값(월드 -5.264,0.556 → 화면중앙기준 px, 1u=108px) 적용. 가운데 앵커로 고정.
            AddGlowBlob(decor, new Vector2(0.5f, 0.5f), new Vector2(-568, 60), new Vector2(760, 760), new Color32(124, 58, 237, 28)); // 좌: AAC 뒤 보라(손배치)
            AddGlowBlob(decor, new Vector2(0.5f, 0.5f), new Vector2(568, 60),  new Vector2(760, 760), new Color32(37, 99, 235, 26));  // 우: 카드 뒤 파랑(좌우 대칭)

            var F = HomeDecorMotion.Mode.Float;
            var T = HomeDecorMotion.Mode.Twinkle;
            // 두둥실 떠다니는 것들 (구름/하트/말풍선)
            AddSticker(decor, "01_thought_cloud.png",    new Vector2(0f, 1f), new Vector2(770, -95),  230, F, 0.95f);
            AddSticker(decor, "05_speech_bubbles.png",   new Vector2(1f, 1f), new Vector2(-360, -110),240, F, 0.95f);
            AddSticker(decor, "03_pink_heart_large.png", new Vector2(1f, 0.5f),new Vector2(-95, 215), 130, F, 0.9f);
            AddSticker(decor, "04_pink_heart_small.png", new Vector2(0f, 0f), new Vector2(150, 250),  80,  F, 0.9f);
            AddSticker(decor, "08_blue_bubble_large.png",new Vector2(0f, 0f), new Vector2(430, 150),  100, F, 0.9f);
            AddSticker(decor, "09_blue_bubble_small.png",new Vector2(0f, 0f), new Vector2(250, 300), 70,  F, 0.85f);
            AddSticker(decor, "10_purple_bubble.png",    new Vector2(1f, 0f), new Vector2(-260, 185), 90,  F, 0.9f);
            AddSticker(decor, "11_red_bubble.png",       new Vector2(1f, 0f), new Vector2(-520, 110), 80,  F, 0.85f);
            AddSticker(decor, "12_purple_bubble_small.png",new Vector2(0.5f,1f),new Vector2(150, -80), 60, F, 0.85f);
            // 제자리 고정 + 반짝이는 것들 (별)
            AddSticker(decor, "07_blue_star_large.png",  new Vector2(1f, 1f), new Vector2(-130, -300),110, T, 0.95f);
            AddSticker(decor, "06_blue_star_small.png",  new Vector2(1f, 0f), new Vector2(-150, 360), 80,  T, 0.9f);

            // ===== 좌측 패널 (타이틀 + AAC + 부제 + 인사칩) =====
            var leftPanel = ChildRect("LeftPanel", canvasGo.transform);
            StretchFull(leftPanel, 0);

            // 좌상단 토스트바 자리를 비우려 타이틀 블록을 아래로 (open.png 배치)
            var kicker = MakeText("Kicker", leftPanel, "내 손 안의", 50, TitleColor, font, bold: true);
            kicker.alignment = TextAlignmentOptions.TopLeft;
            Anchor(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(64, -432), new Vector2(540, 64));

            // AAC: 파란 세로 그라데이션 + Bloom (중앙, 크게)
            var aac = MakeText("AAC", leftPanel, "AAC", 185, Color.white, font, bold: true);
            aac.alignment = TextAlignmentOptions.TopLeft;
            aac.enableVertexGradient = true;
            aac.colorGradient = new VertexGradient(
                new Color32(206, 228, 255, 255), new Color32(206, 228, 255, 255), // 위: 거의 흰 하늘파랑
                new Color32(13, 52, 180, 255),   new Color32(13, 52, 180, 255));  // 아래: 아주 진한 파랑
            Anchor(aac.rectTransform, new Vector2(0f, 1f), new Vector2(60, -490), new Vector2(510, 220));
            aac.gameObject.AddComponent<HomeBloomText>().SetIntensity(0.9f); // 노출 올린 만큼 AAC HDR은 낮춰 그라데이션 보존

            // AAC 옆 노란 별 — 뒤에 따뜻한 후광부터 깔고(먼저 추가=뒤), 그 위에 별을 얹어 반짝이게
            AddGlowHalo(leftPanel, new Vector2(0f, 1f), new Vector2(470, -498), 170f, new Color32(255, 214, 92, 255), 0.45f);
            AddSticker(leftPanel, "02_yellow_star.png", new Vector2(0f, 1f), new Vector2(470, -498), 108, HomeDecorMotion.Mode.Twinkle, 1f);

            var subtitle = MakeText("Subtitle", leftPanel, "대화가 어려울 때,\nAAC와 함께 소통해요", 33, SubColor, font, bold: false);
            subtitle.alignment = TextAlignmentOptions.TopLeft;
            subtitle.textWrappingMode = TextWrappingModes.Normal;
            Anchor(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(66, -712), new Vector2(500, 120));

            // 프로필 버튼 (아바타 + "프로필") → 프로필 선택 화면 (open.png)
            var chip = ChildRect("ProfileBtn", leftPanel);
            Anchor(chip, new Vector2(0f, 1f), new Vector2(64, -832), new Vector2(300, 88));
            // 바깥 테두리(링): chip 자체 Image = 테두리색, 안쪽 Fill을 살짝 inset 해서 글래스 채움 → 또렷한 테두리
            var chipImg = chip.gameObject.AddComponent<Image>();
            chipImg.sprite = Rounded(); chipImg.type = Image.Type.Sliced; chipImg.pixelsPerUnitMultiplier = 1f;
            chipImg.color = new Color(Primary.r / 255f, Primary.g / 255f, Primary.b / 255f, 0.55f); // 파란 테두리
            var profileBtn = chip.gameObject.AddComponent<Button>();
            profileBtn.targetGraphic = chipImg;

            var chipFill = ChildRect("Fill", chip);
            chipFill.anchorMin = Vector2.zero; chipFill.anchorMax = Vector2.one;
            chipFill.offsetMin = new Vector2(3f, 3f); chipFill.offsetMax = new Vector2(-3f, -3f); // 3px 테두리 두께
            var chipFillImg = chipFill.gameObject.AddComponent<Image>();
            chipFillImg.sprite = Rounded(); chipFillImg.type = Image.Type.Sliced; chipFillImg.pixelsPerUnitMultiplier = 1f;
            chipFillImg.color = new Color(1f, 1f, 1f, 0.82f); // 글래스 채움(조금 더 또렷하게)
            chipFillImg.raycastTarget = false;
            var pcolors = profileBtn.colors;
            pcolors.highlightedColor = new Color(0.93f, 0.96f, 1f, 1f);
            pcolors.pressedColor = new Color(0.85f, 0.9f, 1f, 1f);
            pcolors.fadeDuration = 0.08f;
            profileBtn.colors = pcolors;

            var avatar = ChildRect("Avatar", chip);
            Anchor(avatar, new Vector2(0f, 0.5f), new Vector2(20, 0), new Vector2(58, 58));
            var avatarImg = avatar.gameObject.AddComponent<Image>();
            avatarImg.preserveAspect = true; avatarImg.raycastTarget = false;

            var profileLabel = MakeText("Label", chip, "프로필", 32, TitleColor, font, bold: true);
            profileLabel.alignment = TextAlignmentOptions.Left;
            Anchor(profileLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(96, 0), new Vector2(180, 56));

            var pback = chip.gameObject.AddComponent<Artti.Common.SceneBackButton>();
            pback.SetTarget("ProfileSelectScene");
            var pmethod = typeof(Artti.Common.SceneBackButton).GetMethod(nameof(Artti.Common.SceneBackButton.GoBack));
            var paction = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), pback, pmethod);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(profileBtn.onClick, paction);

            // ===== 카드 2개 =====
            var cards = ChildRect("Cards", canvasGo.transform);
            StretchFull(cards, 0);

            // open.png 배치: 카드 2개를 더 붙여서 우측에(좌측 패널과 균형)
            var train = MakeCharacterCard(cards, "TrainingModeBtn", new Vector2(-40, 0),
                TrainAccent, "말하기 훈련모드", "다양한 상황에서 말을 연습해요", "훈련 시작하기",
                GirlOpen, GirlClose, GirlBack, font, ocrEmoji: null);

            var ar = MakeCharacterCard(cards, "ARFieldModeBtn", new Vector2(520, 0),
                ARAccent, "AR 현장도우미", "실생활에서 도움을 받아요", "도움 시작하기",
                ManOpen, ManClose, ManBack, font, ocrEmoji: OcrIcon);

            // ===== 좌상단 토스트바(햄버거 메뉴) — 레포트 보기 / 종료하기 =====
            MakeToastMenu(canvasGo.transform, font);

            // ===== 하단 중앙 팁 바 =====
            MakeTipBar(canvasGo.transform, font);

            // ===== 우상단 설정 버튼 (설정씬 미구현 — 비주얼/추후 연결) =====
            var settings = ChildRect("SettingsBtn", canvasGo.transform);
            settings.anchorMin = settings.anchorMax = new Vector2(1f, 1f); settings.pivot = new Vector2(1f, 1f);
            settings.anchoredPosition = new Vector2(-48, -62);
            settings.sizeDelta = new Vector2(210, 112); // 240x128 비율
            var setImg = settings.gameObject.AddComponent<Image>();
            setImg.sprite = LoadPngSprite(StickerDir + "settings_button.png");
            setImg.preserveAspect = true;
            var setBtn = settings.gameObject.AddComponent<Button>();
            setBtn.targetGraphic = setImg;
            var setColors = setBtn.colors;
            setColors.highlightedColor = new Color(0.9f, 0.94f, 1f, 1f);
            setColors.fadeDuration = 0.08f;
            setBtn.colors = setColors;

            // ===== 양쪽 하단 풀(plant) — 전경 장식 =====
            AddPlant(canvasGo.transform, new Vector2(0f, 0f), false); // 좌하단
            AddPlant(canvasGo.transform, new Vector2(1f, 0f), true);  // 우하단(좌우 반전)

            // ===== 진입 애니메이션 =====
            var intro = canvasGo.AddComponent<HomeIntroAnimator>();
            intro.AddItem(leftPanel, new Vector2(-160, 0), 0f, 0.00f, 0.55f, fade: true);   // AAC 왼쪽 슬라이드
            intro.AddItem(train.card, new Vector2(0, 300), 0f, 0.12f, 0.50f, fade: true);   // 말하기 카드 위에서
            intro.AddItem(ar.card,    new Vector2(0, 300), 0f, 0.22f, 0.50f, fade: true);   // AR 카드 위에서
            intro.AddItem(train.charRT, Vector2.zero, 0.82f, 0.45f, 0.45f, fade: false);    // 캐릭터 확대
            intro.AddItem(ar.charRT,    Vector2.zero, 0.82f, 0.52f, 0.45f, fade: false);
            intro.AddEnableAfter(train.idle);   // 진입 끝난 뒤 Idle 시작
            intro.AddEnableAfter(ar.idle);

            // ===== 패럴랙스 (선택, 기본 비활성) =====
            var parallax = canvasGo.AddComponent<HomeParallax>();
            parallax.AddLayer(decor, 15f);      // 배경
            parallax.AddLayer(cards, 10f);      // 캐릭터(카드)
            parallax.AddLayer(aac.rectTransform, 2f); // AAC
            parallax.enabled = false;

            // ===== MainSceneView 와이어링 =====
            var view = canvasGo.AddComponent<MainSceneView>();
            var so = new SerializedObject(view);
            so.FindProperty("trainingModeBtn").objectReferenceValue = train.button;
            so.FindProperty("arFieldModeBtn").objectReferenceValue = ar.button;
            so.FindProperty("greetingAvatar").objectReferenceValue = avatarImg;
            so.FindProperty("profileNameLabel").objectReferenceValue = profileLabel;
            so.FindProperty("profileButtonAvatar").objectReferenceValue = avatarImg;
            // reportBtn은 햄버거 메뉴(HomeMenu)가 처리
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();

            // URP 포스트프로세싱(노출/Bloom)은 빌드 직후엔 재적용 안 돼서 글래스가 어둡게 보인다.
            // 도메인 리로드가 한 번 일어나야 밝아진다(=수동 Ctrl+R). 그걸 자동으로 돌려서
            // 빌드 직후부터 바로 밝은 상태가 되도록 한다.
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[MainSceneBuilder] 완료 v14 (글래스 그림자 가장자리선 제거+코너 더 둥글게, GlowBlob 손배치값 반영)");
        }

        // ===== 캐릭터 카드 =====
        struct CardRefs
        {
            public Button button;
            public RectTransform card;
            public RectTransform charRT;
            public HomeCharacterIdle idle;
        }

        static CardRefs MakeCharacterCard(Transform parent, string goName, Vector2 pos,
            Color32 accent, string cardTitle, string desc, string cta,
            string charOpenPath, string charClosePath, string backPath, TMP_FontAsset font, string ocrEmoji)
        {
            var root = ChildRect(goName, parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = pos;
            root.sizeDelta = CardSize;

            // 카드 뒤 후광(Glow) — Hover 시 강해짐. 평상시 은은한 후광 + URP Bloom으로 입체감.
            var glow = ChildRect("Glow", root);
            glow.anchorMin = Vector2.zero; glow.anchorMax = Vector2.one;
            glow.offsetMin = new Vector2(-46, -58); glow.offsetMax = new Vector2(46, 34);
            var glowImg = glow.gameObject.AddComponent<Image>();
            glowImg.sprite = SceneBuilderUtils.EnsureGlowSprite();
            glowImg.type = Image.Type.Sliced;
            glowImg.color = new Color(accent.r / 255f, accent.g / 255f, accent.b / 255f, 0.16f);
            glowImg.raycastTarget = false;

            // AR 카드 뒤 OCR 아이콘(돋보기 emoji) — 카드 뒤에서 살짝 보임
            if (!string.IsNullOrEmpty(ocrEmoji))
            {
                var ocr = ChildRect("OcrIcon", root);
                ocr.anchorMin = ocr.anchorMax = new Vector2(1f, 1f);
                ocr.pivot = new Vector2(0.5f, 0.5f);
                ocr.anchoredPosition = new Vector2(-36, -8);
                ocr.sizeDelta = new Vector2(220, 220);
                ocr.localRotation = Quaternion.Euler(0, 0, -12f);
                var ocrImg = ocr.gameObject.AddComponent<Image>();
                ocrImg.sprite = LoadSvgSprite(ocrEmoji);
                ocrImg.preserveAspect = true;
                ocrImg.raycastTarget = false;
                ocrImg.color = new Color(1f, 1f, 1f, 0.22f);
            }

            // 본체 = 카드 배경 장면(GirlBack/ManBack, 라운드 베이크됨). 캐릭터/글자/CTA가 그 위에 렌더.
            var body = ChildRect("Body", root);
            StretchFull(body, 0);
            var bodyImg = body.gameObject.AddComponent<Image>();
            var backSprite = LoadPngSprite(backPath);
            if (backSprite != null)
            {
                bodyImg.sprite = backSprite; bodyImg.type = Image.Type.Simple; bodyImg.preserveAspect = false;
                bodyImg.color = White;
            }
            else
            {
                bodyImg.sprite = Rounded(); bodyImg.type = Image.Type.Sliced; bodyImg.pixelsPerUnitMultiplier = 1f;
                bodyImg.color = White;
            }

            // (카드 위 프로스트 오버레이는 제거 — 장면이 또렷하게 보이도록)

            var titleText = MakeText("Title", body, cardTitle, 40,
                new Color32(accent.r, accent.g, accent.b, 255), font, bold: true);
            Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(460, 56));

            var descText = MakeText("Desc", body, desc, 24, SubColor, font, bold: false);
            Anchor(descText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -84), new Vector2(460, 38));

            // 캐릭터
            var charRT = ChildRect("Character", body);
            charRT.anchorMin = charRT.anchorMax = new Vector2(0.5f, 0f);
            charRT.pivot = new Vector2(0.5f, 0f);
            charRT.anchoredPosition = new Vector2(0, 150);
            // 높이 기준 캡 — 가로형(남자)/세로형(여자) 컷아웃 모두 글자 영역 안 넘게
            charRT.sizeDelta = new Vector2(380, 360);
            var charImg = charRT.gameObject.AddComponent<Image>();
            charImg.sprite = LoadPngSprite(charOpenPath);
            charImg.preserveAspect = true;
            charImg.raycastTarget = false;

            // CTA 알약 버튼(시각용 — 클릭은 카드 전체가 받음)
            var ctaRT = ChildRect("CTA", body);
            ctaRT.anchorMin = ctaRT.anchorMax = new Vector2(0.5f, 0f);
            ctaRT.pivot = new Vector2(0.5f, 0f);
            ctaRT.anchoredPosition = new Vector2(0, 26);
            ctaRT.sizeDelta = new Vector2(400, 82);
            var ctaImg = ctaRT.gameObject.AddComponent<Image>();
            ctaImg.sprite = Rounded(); ctaImg.type = Image.Type.Sliced; ctaImg.pixelsPerUnitMultiplier = 1f;
            ctaImg.color = accent; ctaImg.raycastTarget = false;
            var ctaText = MakeText("Text", ctaRT, cta + "   →", 32, White, font, bold: true);
            StretchFull(ctaText.rectTransform, 6);

            // 카드 버튼
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = bodyImg;
            var colors = btn.colors;
            colors.normalColor = White;
            colors.highlightedColor = new Color(0.97f, 0.98f, 1f, 1f);
            colors.pressedColor = new Color(0.93f, 0.96f, 1f, 1f);
            colors.selectedColor = White;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            // Hover Glow
            var glowComp = root.gameObject.AddComponent<HomeButtonGlow>();
            glowComp.Setup(glowImg, root);

            // Idle + 눈 깜빡임 (진입 끝난 뒤 활성)
            var idle = charRT.gameObject.AddComponent<HomeCharacterIdle>();
            idle.Setup(charRT, charImg, LoadPngSprite(charOpenPath), LoadPngSprite(charClosePath));
            idle.enabled = false;

            return new CardRefs { button = btn, card = root, charRT = charRT, idle = idle };
        }

        // ===== 좌상단 토스트바(햄버거) 메뉴 — 클릭 시 레포트 보기/종료하기 팝업 =====
        static void MakeToastMenu(Transform parent, TMP_FontAsset font)
        {
            var rect = ChildRect("ToastBar", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(48, -62);
            rect.sizeDelta = new Vector2(136, 88); // 100x65 비율
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = LoadPngSprite(StickerDir + "menu_button_transparent.png"); // 흰 알약+햄버거 통이미지
            img.preserveAspect = true;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.9f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.88f, 1f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var menu = rect.gameObject.AddComponent<HomeMenu>();

            // 팝업 패널 (햄버거 아래로 펼쳐짐)
            var popup = ChildRect("MenuPopup", rect);
            popup.anchorMin = popup.anchorMax = new Vector2(0f, 0f);
            popup.pivot = new Vector2(0f, 1f);
            popup.anchoredPosition = new Vector2(0, -12);
            popup.sizeDelta = new Vector2(264, 180);
            var pimg = popup.gameObject.AddComponent<Image>();
            pimg.sprite = Rounded(); pimg.type = Image.Type.Sliced; pimg.pixelsPerUnitMultiplier = 1f;
            pimg.color = new Color(1f, 1f, 1f, 0.92f);

            var reportItem = MakeMenuItem(popup, "레포트 보기", new Vector2(0, -14), font);
            var quitItem = MakeMenuItem(popup, "종료하기", new Vector2(0, -98), font);

            menu.SetPopup(popup.gameObject);
            WireClick(btn, menu, nameof(HomeMenu.Toggle));
            WireClick(reportItem, menu, nameof(HomeMenu.OpenReport));
            WireClick(quitItem, menu, nameof(HomeMenu.Quit));
        }

        static Button MakeMenuItem(Transform parent, string label, Vector2 pos, TMP_FontAsset font)
        {
            var rect = ChildRect("Item_" + label, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(236, 72);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = new Color(0.95f, 0.97f, 1f, 1f);
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, label, 28, TitleColor, font, bold: false);
            StretchFull(t.rectTransform, 6);
            return btn;
        }

        static void WireClick(Button btn, MonoBehaviour target, string method)
        {
            var m = target.GetType().GetMethod(method);
            var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, m);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        // ===== 하단 중앙 팁 바 (전구 + 안내문) =====
        static void MakeTipBar(Transform parent, TMP_FontAsset font)
        {
            var rect = ChildRect("TipBar", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 84);
            rect.sizeDelta = new Vector2(830, 88);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = new Color(1f, 1f, 1f, 0.55f); // 글래스
            img.raycastTarget = false;

            var bulb = ChildRect("Bulb", rect);
            bulb.anchorMin = bulb.anchorMax = new Vector2(0f, 0.5f); bulb.pivot = new Vector2(0f, 0.5f);
            bulb.anchoredPosition = new Vector2(160, 0); bulb.sizeDelta = new Vector2(44, 56);
            var bimg = bulb.gameObject.AddComponent<Image>();
            bimg.sprite = LoadPngSprite(StickerDir + "lightbulb_transparent.png"); bimg.preserveAspect = true; bimg.raycastTarget = false;

            var tip = MakeText("Tip", rect, "더 나은 의사소통을 위해 매일 조금씩 연습해요", 33, SubColor, font, bold: false);
            tip.alignment = TextAlignmentOptions.Left;
            Anchor(tip.rectTransform, new Vector2(0f, 0.5f), new Vector2(216, 0), new Vector2(580, 56));
        }

        // ===== URP Volume Profile =====
        static VolumeProfile EnsureVolumeProfile()
        {
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (prof == null)
            {
                prof = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(prof, VolumePath);
            }

            var bloom = GetOrAdd<Bloom>(prof);
            bloom.active = true;
            bloom.threshold.Override(1.1f);   // 카드(휘도~1.0)는 안 번지고 HDR AAC 글자만 번지게
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(Color.white);
            bloom.highQualityFiltering.Override(true);

            var vig = GetOrAdd<Vignette>(prof);
            vig.active = true;
            vig.intensity.Override(0f);      // 비네트 제거(어둡지 않게)
            vig.smoothness.Override(0.6f);
            vig.color.Override(new Color(0.15f, 0.15f, 0.24f, 1f));

            var col = GetOrAdd<ColorAdjustments>(prof);
            col.active = true;
            col.postExposure.Override(0.55f); // 지금 좋은 상태 그대로
            col.contrast.Override(0f);
            col.saturation.Override(10f);

            var tone = GetOrAdd<Tonemapping>(prof);
            tone.active = true;
            tone.mode.Override(TonemappingMode.Neutral);

            EditorUtility.SetDirty(prof);
            AssetDatabase.SaveAssets();
            return prof;
        }

        static T GetOrAdd<T>(VolumeProfile p) where T : VolumeComponent
        {
            return p.TryGet<T>(out var c) ? c : p.Add<T>(false);
        }

        // ===== 공통 UI 헬퍼 =====
        static void AddGlowBlob(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color32 color)
        {
            var rt = ChildRect("GlowBlob", parent);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SceneBuilderUtils.EnsureGlowSprite();
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
        }

        static void AddSticker(Transform parent, string file, Vector2 anchor, Vector2 pos, float width, HomeDecorMotion.Mode mode, float alpha)
        {
            var rt = ChildRect("Sticker_" + System.IO.Path.GetFileNameWithoutExtension(file), parent);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            var sp = LoadPngSprite(StickerDir + file);
            float ratio = (sp != null && sp.rect.width > 0) ? sp.rect.height / sp.rect.width : 1f;
            rt.sizeDelta = new Vector2(width, width * ratio);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, alpha);
            rt.gameObject.AddComponent<HomeDecorMotion>().Configure(mode);
        }

        // 별/포인트 뒤에 깔리는 부드러운 후광. 글로우 스프라이트(중앙 밝고 가장자리 사라짐) + Twinkle로 은은히 맥동.
        static void AddGlowHalo(Transform parent, Vector2 anchor, Vector2 pos, float size, Color32 tint, float alpha)
        {
            var rt = ChildRect("StarGlow", parent);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SceneBuilderUtils.EnsureGlowSprite();
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = new Color(tint.r / 255f, tint.g / 255f, tint.b / 255f, alpha);
            img.raycastTarget = false;
            // 맥동(Twinkle) 대신 아주 작게 둥둥 떠다니게(회전 없음)
            rt.gameObject.AddComponent<HomeDecorMotion>().ConfigureFloat(new Vector2(6f, 9f), 0.16f);
        }

        static void AddPlant(Transform parent, Vector2 anchor, bool flip)
        {
            var rt = ChildRect("Plant", parent);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = Vector2.zero;
            var sp = LoadPngSprite(StickerDir + "plant_transparent.png");
            float ratio = (sp != null && sp.rect.width > 0) ? sp.rect.height / sp.rect.width : 1.63f;
            float w = 152f;
            rt.sizeDelta = new Vector2(w, w * ratio);
            if (flip) rt.localScale = new Vector3(-1f, 1f, 1f); // 우하단은 좌우 반전
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
        }

        static void AddEmoji(Transform parent, string svgPath, Vector2 anchor, Vector2 pos, float size, float alpha, float rotZ)
        {
            var rt = ChildRect("Emoji", parent);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0, 0, rotZ);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSvgSprite(svgPath);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, alpha);
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

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // openmoji는 SVG. Vector Graphics 임포터가 생성한 Sprite 서브에셋을 사용.
        static Sprite LoadSvgSprite(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite s) return s;
            AssetDatabase.ImportAsset(path);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite s) return s;
            Debug.LogWarning($"[MainSceneBuilder] SVG Sprite 없음: {path} (Importer의 Generated Asset Type=Sprite 확인 필요)");
            return null;
        }

        static Sprite LoadPngSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (ti.maxTextureSize < 1024) { ti.maxTextureSize = 1024; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[MainSceneBuilder] 캐릭터 이미지 없음: {path}");
            return sprite;
        }

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void StretchFull(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }
    }
}
