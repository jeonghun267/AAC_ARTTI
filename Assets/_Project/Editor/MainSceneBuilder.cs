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
        static readonly Color32 AacDescColor  = new Color32(0x3B, 0x47, 0x70, 255);  // #3B4770 좌측 설명문
        static readonly Color32 AacLabelColor = new Color32(0x0C, 0x21, 0x4E, 255);  // #0C214E 프로필 버튼 글자

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

        // 훈련 카드 v2 시안. card_body는 원본 1086x1448에서 카드 외곽(x131~955, y151~1313)만
        // 잘라낸 824x1162. character/title_icon은 자르지 않은 원본 크기 그대로.
        const string TrainBodyPath = HomeDir + "training_card_body.png";
        const string TrainCharPath = HomeDir + "training_character.png";  // 단일 레이어 구버전(미사용, 보존)
        const string TrainIconPath = HomeDir + "training_title_icon.png";
        // 캐릭터를 배경(고정) + 누끼(흔들림) 2레이어로 분리. 둘 다 774x670 같은 캔버스라
        // 같은 rect에 겹쳐 두면 TrainCharPath 원본과 동일하게 합성된다.
        const string TrainCharBgPath = HomeDir + "training_character_bg.png";
        const string TrainCharFgPath = HomeDir + "training_character_only.png";

        // AR 카드 v2 시안. card_body는 카드 외곽만 잘라낸 865x1227 크롭본(실측).
        // character는 810x756 같은 캔버스 2장(배경/누끼), title_icon은 136x164.
        const string ArBodyPath   = HomeDir + "ar_card_body_cropped.png";
        const string ArIconPath   = HomeDir + "ar_title_icon.png";
        const string ArCharBgPath = HomeDir + "ar_character_bg.png";
        const string ArCharFgPath = HomeDir + "ar_character_only.png";

        // 좌측 AAC 블록 v2 시안. 원본 캔버스 1122x1402 (카드와 다름).
        // 주의: aac_body.png의 구름 외곽선은 설명 문구("대화가 어려울 때, / AAC와 함께 소통해요")
        //       모양을 따라 만들어져 있다. Desc1/Desc2 문구를 바꾸면 이미지를 다시 만들어야 한다.
        // 구름 잉크는 y 196~1016까지고 CTA(y 1026~1257)는 구름 바깥 아래에 떠 있는 구조다.
        const string AacBodyPath    = HomeDir + "aac_body.png";
        const string AacTitlePath   = HomeDir + "aac_title.png";
        const string AacLogoPath    = HomeDir + "aac_logo.png";
        const string AacSparklePath = HomeDir + "aac_sparkle.png";
        const string AacCtaPath     = HomeDir + "aac_cta.png";
        const string AacProfileIcon = HomeDir + "aac_profile_icon.png";
        const string AacChevronPath = HomeDir + "aac_chevron_icon.png";
        // 주의: Stickers/settings_button.png(240x128, 구버전)와 파일명이 같다.
        //       이쪽은 Home/ 직하의 865x384 신규 시안이다. 경로를 혼동하지 않도록 상수로 고정한다.
        const string AacSettingsPath = HomeDir + "settings_button.png";

        const string EmojiDir   = "Assets/_Project/openmoji-master/color/svg/";
        const string Sparkle    = EmojiDir + "2728.svg";   // ✨
        const string Leaf       = EmojiDir + "1F343.svg";  // 🍃
        const string Herb       = EmojiDir + "1F33F.svg";  // 🌿
        const string Bubble     = EmojiDir + "1F4AC.svg";  // 💬
        const string OcrIcon    = EmojiDir + "1F50D.svg";  // 🔍 OCR(스캔)
        const string Bulb       = EmojiDir + "1F4A1.svg";  // 💡
        const string IconExit   = EmojiDir + "E0A8.svg";   // 비상구

        // 좌측 AAC 블록의 화면 크기와 위치. 보고 나서 조정할 수 있게 한 곳에 모아 둔다.
        // 이 둘만 바꾸면 내부 요소는 배율에 따라 함께 움직인다(AacScale 참조).
        static readonly Vector2 AacBodySize = new Vector2(600, 750);
        static readonly Vector2 AacBodyPos  = new Vector2(-638f, 0f);
        // 원본 1122x1402 -> 화면 배율. sx = 600/1122 = 0.53476, sy = 750/1402 = 0.53495 (차이 0.04%)
        static Vector2 AacScale => new Vector2(AacBodySize.x / 1122f, AacBodySize.y / 1402f);
        // 생성 이미지라 원본 폰트 메트릭이 일관되지 않는다. 화면에서 보고 조정할 시작값이다.
        // 원본 설명문 60~70px -> 32.1~37.4, 버튼 글자 65~70px -> 34.8~37.4 (배율 0.535)
        const int AacDescFont  = 34;
        const int AacLabelFont = 36;
        // NotoSansKR-Medium SDF의 줄 높이는 130.32 / 90 = 1.448em이다(폰트 에셋 m_LineHeight / m_PointSize).
        // TMP는 줄 높이가 박스보다 크면 세로 오버플로로 판정하는데(TextMeshProUGUI.cs:3393),
        // Ellipsis 모드에서는 말줄임 후보 스택이 비어 있으면 m_characterCount를 0으로 만들어
        // 글자를 통째로 지운다(:3460). 그래서 텍스트 박스 높이는 반드시 줄 높이보다 커야 한다.
        // 높이를 손으로 적으면 폰트를 조정할 때 같은 함정에 다시 빠지므로 폰트에서 유도한다.
        const float AacLineHeight = 1.448f;
        static int AacTextBoxH(int font) => Mathf.CeilToInt(font * AacLineHeight) + 4;

        // 훈련 카드 시안(824x1162)을 폭 500에 맞춘 크기. 배율 500/824 = 0.6068, 1162x0.6068 = 705.1
        // 두 카드는 항상 같은 크기를 유지한다 (AR 시안이 나오면 같은 규격으로 맞춤).
        static readonly Vector2 CardSize = new Vector2(500, 705);

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

            // 구름 스티커 본체. 내부 요소는 전부 이 rect 기준 좌상단 앵커로 배치한다.
            // 크기/위치는 AacBodySize, AacBodyPos 두 상수만 바꾸면 내부가 함께 따라온다.
            var aacBody = ChildRect("AacBody", leftPanel);
            aacBody.anchorMin = aacBody.anchorMax = new Vector2(0.5f, 0.5f);
            aacBody.pivot = new Vector2(0.5f, 0.5f);
            aacBody.anchoredPosition = AacBodyPos;
            aacBody.sizeDelta = AacBodySize;
            var aacBodyImg = aacBody.gameObject.AddComponent<Image>();
            aacBodyImg.sprite = LoadPngSprite(AacBodyPath);
            aacBodyImg.preserveAspect = true;
            aacBodyImg.raycastTarget = false;

            AacChild("Title", aacBody, AacTitlePath, 189, 242, 727, 180);
            AacChild("Logo",  aacBody, AacLogoPath,  113, 449, 860, 331);

            // 기존 노란 별(StarGlow + Sticker_02_yellow_star)을 대체. 같은 Twinkle 모션을 붙인다.
            var sparkleRT = AacChild("Sparkle", aacBody, AacSparklePath, 924, 407, 154, 224);
            var sparkleMotion = sparkleRT.gameObject.AddComponent<HomeDecorMotion>();
            sparkleMotion.Configure(HomeDecorMotion.Mode.Twinkle);
            // 기본 Twinkle은 크기 ±14% + 알파 0.5~1.0 + 회전 ±2.4도를 0.9초 주기로 반복해
            // 배경 장식으로는 과하다. 회전과 투명도를 끄고 크기만 아주 미세하게, 느리게 남긴다.
            // 네 필드 모두 [SerializeField] private이라 이 인스턴스의 직렬화 값만 덮어쓴다.
            // 배경 스티커 11개(파란 별 2개 포함)와 SplashScene의 9개는 영향받지 않는다.
            var soSparkle = new SerializedObject(sparkleMotion);
            soSparkle.FindProperty("rotAmp").floatValue          = 0f;     // 회전 제거 (:76이 rotAmp x 0.4 사용)
            soSparkle.FindProperty("twinkleAlphaMin").floatValue = 1f;     // 알파 1.0 고정 (:73 Lerp(1,1,x)=1)
            soSparkle.FindProperty("twinkleScaleAmp").floatValue = 0.03f;  // ±14% -> ±3% (세로 ±16.8px -> ±3.6px)
            soSparkle.FindProperty("twinkleSpeed").floatValue    = 0.25f;  // 1.1Hz -> 0.25Hz (주기 0.9초 -> 4초)
            soSparkle.ApplyModifiedProperties();

            // 문구 확정. aac_body.png의 구름 외곽선이 이 두 줄 모양을 따라 만들어져 있어,
            // 문구를 바꾸면 배경 이미지를 다시 만들어야 한다.
            var descBoxH = AacTextBoxH(AacDescFont);   // 폰트 34 -> 54 (줄 높이 49.23보다 커야 함)

            var desc1 = MakeText("Desc1", aacBody, "대화가 어려울 때,", AacDescFont, AacDescColor, font, bold: false);
            desc1.alignment = TextAlignmentOptions.Left;
            Anchor(desc1.rectTransform, new Vector2(0f, 1f), AacTextPos(211, 815, 75, descBoxH), new Vector2(290, descBoxH));

            var desc2 = MakeText("Desc2", aacBody, "AAC와 함께 소통해요", AacDescFont, AacDescColor, font, bold: false);
            desc2.alignment = TextAlignmentOptions.Left;
            // 원본 x는 212지만 두 행을 같은 기준(211)으로 왼쪽 정렬한다.
            Anchor(desc2.rectTransform, new Vector2(0f, 1f), AacTextPos(211, 907, 69, descBoxH), new Vector2(350, descBoxH));

            // 프로필 버튼 (아바타 + 프로필 이름 + 꺾쇠) → 프로필 선택 화면.
            // 구름 바깥 아래에 떠 있는 알약 버튼. 배경은 aac_cta.png 한 장으로 끝난다.
            var chip = AacChild("ProfileBtn", aacBody, AacCtaPath, 189, 1026, 731, 231);
            var chipImg = chip.GetComponent<Image>();
            chipImg.raycastTarget = true;   // 버튼이므로 레이캐스트를 받아야 한다
            var profileBtn = chip.gameObject.AddComponent<Button>();
            profileBtn.targetGraphic = chipImg;
            var pcolors = profileBtn.colors;
            pcolors.highlightedColor = new Color(0.95f, 0.97f, 1f, 1f);
            pcolors.pressedColor = new Color(0.88f, 0.92f, 1f, 1f);
            pcolors.fadeDuration = 0.08f;
            profileBtn.colors = pcolors;

            // 아바타 자리. 활성 프로필이 있으면 MainSceneView가 sprite를 덮어쓰고,
            // 없으면 MainSceneView.defaultAvatar(aac_profile_icon.png)로 폴백한다.
            var icon = AacChildIn("Icon", chip, AacProfileIcon, 189, 1026, 330, 1088, 108, 136);
            var iconImg = icon.GetComponent<Image>();

            var profileLabel = MakeText("Label", chip, "프로필", AacLabelFont, AacLabelColor, font, bold: true);
            profileLabel.alignment = TextAlignmentOptions.Left;
            // 닉네임이 길어도 꺾쇠를 침범하지 않도록 박스 폭에서 말줄임 처리한다.
            // Ellipsis는 박스가 줄 높이보다 낮으면 글자를 통째로 지우므로 높이를 폰트에서 유도한다.
            profileLabel.overflowMode = TextOverflowModes.Ellipsis;
            var labelBoxH = AacTextBoxH(AacLabelFont);  // 폰트 36 -> 57 (줄 높이 52.13보다 커야 함)
            Anchor(profileLabel.rectTransform, new Vector2(0f, 1f),
                AacTextPos(501 - 189, 1108 - 1026, 74, labelBoxH), new Vector2(150, labelBoxH));

            AacChildIn("Chevron", chip, AacChevronPath, 189, 1026, 767, 1113, 37, 62);

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
                TrainAccent, "훈련모드", "다양한 상황에서 말을 연습해요", "훈련 시작하기",
                TrainCharFgPath, null, null, font, ocrEmoji: null,
                style: CardStyle.TrainingV2);

            var ar = MakeCharacterCard(cards, "ARFieldModeBtn", new Vector2(520, 0),
                ARAccent, "현장모드", "실생활에서 도움을 받아요", "도움 시작하기",
                ArCharFgPath, null, null, font, ocrEmoji: null,
                style: CardStyle.ARFieldV2);

            // ===== 좌상단 토스트바(햄버거 메뉴) — 레포트 보기 / 종료하기 =====
            MakeToastMenu(canvasGo.transform, font);

            // ===== 하단 중앙 팁 바 =====
            MakeTipBar(canvasGo.transform, font);

            // ===== 우상단 설정 버튼 =====
            // 설정씬 미구현. 비주얼만 있고 onClick 리스너가 없다(ScenePaths에도 SettingsScene이 없음).
            // SettingsScene이 생기면 ProfileBtn처럼 SceneBackButton + AddPersistentListener로 연결한다.
            //
            // [72dp 터치 영역 미달 기록 — 나중에 이 주석을 "72dp"로 검색할 것]
            // PLAN.md:642는 "카드 터치 영역은 모바일 기준 72dp 이상"을 요구한다. 카드 2장은
            // 705단위 = 약 320dp로 충족하지만, 화면 크롬 버튼 3종은 전부 미달이다.
            // (2400x1080 폰 / density 2.75 / 가로 기준 스케일 환산, 1 캔버스 단위 = 0.455dp)
            //   ProfileBtn  124단위 = 56.4dp
            //   SettingsBtn 112단위 = 50.9dp   <- 여기
            //   ToastBar     88단위 = 40.0dp
            // PLAN.md:642의 기준 대상은 "카드"이고, PLAN.md:886에 보호자용 UI를 예외로 둔 선례가
            // 있어 당장 위반은 아니다. 다만 설정 버튼만 160단위(72dp)로 키우면 rect가 y 62~222가
            // 되어 AR 카드 상단(187.5)과 x 1660~1730에서 겹치고, 생성 순서상 위에 있어 카드 클릭을
            // 가로챈다. 고치려면 크롬 3종의 배치를 함께 재검토하는 별도 작업이 필요하다.
            var settings = ChildRect("SettingsBtn", canvasGo.transform);
            settings.anchorMin = settings.anchorMax = new Vector2(1f, 1f); settings.pivot = new Vector2(1f, 1f);
            settings.anchoredPosition = new Vector2(-48, -62);
            // rect는 그대로 두고 preserveAspect가 맞춘다. 865x384(비율 2.2526)가 210x93.2로 표시되고
            // 위아래 9.4씩 레터박스된다. 터치 영역을 우선해 세로 112를 유지한다.
            settings.sizeDelta = new Vector2(210, 112);
            var setImg = settings.gameObject.AddComponent<Image>();
            setImg.sprite = LoadPngSprite(AacSettingsPath);
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
            // 기존 AAC 글자 레이어는 제거. 좌측 블록은 통이미지라 패럴랙스 대상에서 뺀다.
            parallax.enabled = false;

            // ===== MainSceneView 와이어링 =====
            var view = canvasGo.AddComponent<MainSceneView>();
            var so = new SerializedObject(view);
            so.FindProperty("trainingModeBtn").objectReferenceValue = train.button;
            so.FindProperty("arFieldModeBtn").objectReferenceValue = ar.button;
            so.FindProperty("greetingAvatar").objectReferenceValue = iconImg;
            so.FindProperty("profileNameLabel").objectReferenceValue = profileLabel;
            so.FindProperty("profileButtonAvatar").objectReferenceValue = iconImg;
            // 활성 프로필이 없을 때 쓸 기본 아이콘. 비워 두면 MainSceneView는 기존처럼 숨기기만 한다.
            so.FindProperty("defaultAvatar").objectReferenceValue = LoadPngSprite(AacProfileIcon);
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

        // 카드 레이아웃 프리셋. AR 카드는 Legacy를 그대로 써서 기존 산출물이 바뀌지 않게 한다.
        // AR 시안이 나오면 ARFieldV2 프리셋을 추가하는 방식으로 확장한다.
        struct CardStyle
        {
            public bool useAccentColor;   // true면 Title/CTA 색에 accent 사용 (기존 동작)
            public string bodyPath;       // null이면 호출부의 backPath 사용
            public string titleIconPath;  // null이면 제목 아이콘 없음
            public Vector2 titleIconPos, titleIconSize;
            // 기록: 카드 제목은 폰트 62에 박스 87이라 줄 높이(62 x 1.448 = 89.8)를 넘는 세로 오버플로
            // 상태다. overflowMode가 기본 Overflow라 TMP가 세로 경계를 무시해 지금은 정상 렌더링된다.
            // 다만 카드 제목에 Ellipsis를 켜면 좌측 Label과 같은 증상(글자 전부 사라짐)이 난다.
            // 그때는 AacTextBoxH와 같은 방식으로 박스 높이를 폰트에서 유도해야 한다.
            public Vector2 titlePos, titleSize;
            public Vector2 descPos,  descSize;
            public Vector2 charPos,  charSize;
            public Vector2 ctaPos,   ctaSize;
            public Color32 titleColor, descColor, ctaColor;
            public int titleFont, descFont, ctaFont;
            public float ctaPpu, ctaPad;
            public string ctaSuffix;
            public string charBgPath;      // null이면 캐릭터 단일 레이어(기존 동작)
            // 배경 레이어(CharacterBg)만 세로로 줄일 때 쓴다. 누끼(CharacterFg)는 건드리지 않는다.
            // 0이면 미지정으로 보고 1배를 적용한다 -> Legacy/TrainingV2는 값을 두지 않아도 기존 동작 유지.
            public float charBgScaleY;
            // HomeCharacterIdle의 [SerializeField] private 모션 값을 인스턴스 단위로 덮어쓸지.
            // false면 스크립트 기본값 그대로 -> AR 카드는 지금과 완전히 동일하게 동작한다.
            public bool overrideIdleMotion;
            public float idleBreathAmplitude, idleBreathScale, idleSwayAmplitude, idleNodAngle;

            // 기존 하드코딩 값을 1:1로 옮긴 것. 값을 바꾸면 AR 카드 씬 산출물이 달라진다.
            public static CardStyle Legacy => new CardStyle
            {
                useAccentColor = true,
                bodyPath = null, titleIconPath = null,
                charBgPath = null, overrideIdleMotion = false,   // 단일 레이어 + 스크립트 기본 모션
                titlePos = new Vector2(0, -28),  titleSize = new Vector2(460, 56), titleFont = 40,
                descPos  = new Vector2(0, -84),  descSize  = new Vector2(460, 38), descFont  = 24,
                charPos  = new Vector2(0, 150),  charSize  = new Vector2(380, 360),
                ctaPos   = new Vector2(0, 26),   ctaSize   = new Vector2(400, 82), ctaFont   = 32,
                descColor = SubColor,
                ctaPpu = 1f, ctaPad = 6f, ctaSuffix = "   →",
            };

            // 크롭본 824x1162 기준, 배율 500/824 = 0.6068.
            // 아이콘/제목 x는 [아이콘 82][간격 18.2][제목 글리프 G] 묶음을 카드 중심에 맞춘 값.
            //   제목 중심 = (82 + 18.2) / 2 = 50.1  (묶음 오른쪽 끝 요소라 G와 무관한 상수)
            //   아이콘 중심 = -9.1 - G/2
            // "훈련모드"(한글 4자)의 G는 에디터 실측 약 244 -> 아이콘 -9.1 - 122 = -131.
            // NotoSansKR은 한글을 거의 전각으로 그린다(폰트 62 기준 자당 약 61px = 0.98em).
            // 제목 문구를 바꾸면 G = 61 x 한글자수로 잡고 아이콘 x만 위 식으로 다시 계산하면 된다.
            public static CardStyle TrainingV2 => new CardStyle
            {
                useAccentColor = false,
                bodyPath = TrainBodyPath, titleIconPath = TrainIconPath,
                charBgPath = TrainCharBgPath,
                // 캐릭터 잉크가 rect 아래·오른쪽 끝에 닿아 있어 세로로 움직이면 잘린 단면이 뜨고,
                // 스케일이 1 아래로 내려가도 바닥이 들린다. 좌우 흔들림과 미세 회전만 남긴다.
                overrideIdleMotion = true,
                idleBreathAmplitude = 0f,    // 상하 이동 끔 (스크립트 기본 6px)
                idleBreathScale     = 0f,    // 스케일 호흡 끔 (스크립트 기본 0.012)
                idleSwayAmplitude   = 3f,    // 좌우 흔들림 유지 (기본값과 동일)
                idleNodAngle        = 1.5f,  // 미세 회전 (스크립트 기본 3.5도)
                titleIconPos = new Vector2(-131f, -40.0f), titleIconSize = new Vector2(82, 86),
                titlePos = new Vector2(50.1f, -41.5f),  titleSize = new Vector2(310, 87), titleFont = 62,
                descPos  = new Vector2(0f, -119.1f),    descSize  = new Vector2(350, 50), descFont  = 28,
                charPos  = new Vector2(0f, 140.8f),     charSize  = new Vector2(470, 407),
                ctaPos   = new Vector2(0f, 32.8f),      ctaSize   = new Vector2(440, 105), ctaFont  = 47,
                titleColor = new Color32(0x58, 0x1B, 0xEE, 255),
                descColor  = new Color32(0x69, 0x69, 0x9A, 255),
                ctaColor   = new Color32(0x7B, 0x43, 0xF8, 255),  // #8A4DFE~#6C39F2 중간값(단색 근사)
                ctaPpu = 88f / 105f,   // 반경 = 높이/2 (완전 pill)
                ctaPad = 8f, ctaSuffix = "   →",
            };

            // 크롭본 865x1227(실측) 기준. sx = 500/865 = 0.5780, sy = 705/1227 = 0.5746.
            // 이미지 비율 0.7050이 카드 rect 0.7092보다 0.6% 낮아 세로가 4.3px 압축된다(육안 무시 가능).
            // 아이콘/제목 x는 훈련 카드와 같은 식:
            //   제목 중심  = (아이콘폭 78.6 + 간격 15.6) / 2 = +47.1  (G와 무관한 상수)
            //   아이콘 중심 = -7.8 - G/2,  "현장모드"(한글 4자) G = 244 -> -129.8
            // 설명 폰트/박스는 훈련 카드에서 눈으로 맞춘 값(28, 350x50)을 그대로 쓴다.
            public static CardStyle ARFieldV2 => new CardStyle
            {
                useAccentColor = false,
                bodyPath = ArBodyPath, titleIconPath = ArIconPath,
                charBgPath = ArCharBgPath,
                charBgScaleY = 0.88333f,   // 배경만 세로 축소(에디터 실측). 434 -> 383.4, 위아래 25.3px씩
                // 누끼 잉크가 rect 바닥에 닿아 있어(여백 0) 세로 이동·축소는 금지.
                // 좌우는 오른쪽 31.2px / 왼쪽 18.5px 여백이 있어 3px 흔들림에 잘리지 않는다.
                overrideIdleMotion = true,
                idleBreathAmplitude = 0f,
                idleBreathScale     = 0f,
                idleSwayAmplitude   = 3f,
                idleNodAngle        = 1.5f,
                titleIconPos = new Vector2(-129.8f, -37.9f), titleIconSize = new Vector2(79, 94),
                titlePos = new Vector2(47.1f, -41.0f),  titleSize = new Vector2(310, 87), titleFont = 62,
                descPos  = new Vector2(0f, -114.9f),    descSize  = new Vector2(350, 50), descFont  = 28,
                charPos  = new Vector2(4.9f, 133.3f),   charSize  = new Vector2(468, 434),
                ctaPos   = new Vector2(0f, 27.2f),      ctaSize   = new Vector2(433, 105), ctaFont  = 47,
                titleColor = new Color32(0x03, 0x59, 0xFA, 255),
                descColor  = new Color32(0x52, 0x68, 0xA7, 255),
                ctaColor   = new Color32(0x2B, 0x7C, 0xFC, 255),  // #409CFB~#165CFC 중간값(단색 근사)
                ctaPpu = 88f / 105f,   // 반경 52.5 = 높이/2 (완전 pill)
                ctaPad = 8f, ctaSuffix = "   →",
            };
        }

        static CardRefs MakeCharacterCard(Transform parent, string goName, Vector2 pos,
            Color32 accent, string cardTitle, string desc, string cta,
            string charOpenPath, string charClosePath, string backPath, TMP_FontAsset font, string ocrEmoji,
            CardStyle style)
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
            // 크롭본(824x1162)은 카드 외곽과 크기가 같으므로 StretchFull 그대로 두면 된다.
            var bodySpritePath = style.bodyPath ?? backPath;
            var backSprite = string.IsNullOrEmpty(bodySpritePath) ? null : LoadPngSprite(bodySpritePath);
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

            // 제목 왼쪽 말풍선 아이콘 (v2 전용). 앵커 (0.5,1)이라 x가 곧 박스 중심 오프셋이 되어
            // 박스 폭에 여유를 둬도 글리프 중심이 어긋나지 않는다.
            if (!string.IsNullOrEmpty(style.titleIconPath))
            {
                var titleIcon = ChildRect("TitleIcon", body);
                Anchor(titleIcon, new Vector2(0.5f, 1f), style.titleIconPos, style.titleIconSize);
                var titleIconImg = titleIcon.gameObject.AddComponent<Image>();
                titleIconImg.sprite = LoadPngSprite(style.titleIconPath);
                titleIconImg.preserveAspect = true;
                titleIconImg.raycastTarget = false;
            }

            var titleColor = style.useAccentColor
                ? new Color32(accent.r, accent.g, accent.b, 255)
                : style.titleColor;
            var titleText = MakeText("Title", body, cardTitle, style.titleFont, titleColor, font, bold: true);
            Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), style.titlePos, style.titleSize);

            var descText = MakeText("Desc", body, desc, style.descFont, style.descColor, font, bold: false);
            Anchor(descText.rectTransform, new Vector2(0.5f, 1f), style.descPos, style.descSize);

            // 캐릭터
            var charRT = ChildRect("Character", body);
            charRT.anchorMin = charRT.anchorMax = new Vector2(0.5f, 0f);
            charRT.pivot = new Vector2(0.5f, 0f);
            // 높이 기준 캡 — 가로형(남자)/세로형(여자) 컷아웃 모두 글자 영역 안 넘게
            charRT.anchoredPosition = style.charPos;
            charRT.sizeDelta = style.charSize;

            RectTransform motionRT;  // HomeCharacterIdle이 실제로 움직일 대상
            Image charImg;           // 눈 깜빡임(faceImage) 대상
            if (!string.IsNullOrEmpty(style.charBgPath))
            {
                // 2레이어: 배경은 고정, 누끼만 흔들린다. 누끼가 사각 영역을 벗어나지 못하도록
                // 부모에 클리핑을 건다. 클립 경계는 원본 이미지의 사각 경계와 같은 선이라
                // 새로운 잘린 면이 생기지 않는다.
                charRT.gameObject.AddComponent<RectMask2D>();

                var charBgRT = ChildRect("CharacterBg", charRT);
                StretchFull(charBgRT, 0);
                charBgRT.pivot = new Vector2(0.5f, 0.5f);
                // 배경만 세로로 줄이는 경우. pivot이 Center라 위아래로 균등하게 안쪽으로 당겨진다.
                float bgScaleY = style.charBgScaleY > 0f ? style.charBgScaleY : 1f;
                if (bgScaleY != 1f) charBgRT.localScale = new Vector3(1f, bgScaleY, 1f);
                var charBgImg = charBgRT.gameObject.AddComponent<Image>();
                charBgImg.sprite = LoadPngSprite(style.charBgPath);
                charBgImg.preserveAspect = true;
                charBgImg.raycastTarget = false;

                var charFgRT = ChildRect("CharacterFg", charRT);
                StretchFull(charFgRT, 0);
                // 발을 고정하고 상체만 기울도록 회전 원점을 바닥 중앙에 둔다.
                // 풀 스트레치(offsetMin=offsetMax=0)라 sizeDelta와 anchoredPosition이 모두 (0,0)이고,
                // pivot을 바꿔도 그 값이 달라지지 않으므로 위치 보정은 필요 없다.
                charFgRT.pivot = new Vector2(0.5f, 0f);
                charImg = charFgRT.gameObject.AddComponent<Image>();
                charImg.sprite = LoadPngSprite(charOpenPath);
                charImg.preserveAspect = true;
                charImg.raycastTarget = false;
                motionRT = charFgRT;
            }
            else
            {
                charImg = charRT.gameObject.AddComponent<Image>();
                charImg.sprite = LoadPngSprite(charOpenPath);
                charImg.preserveAspect = true;
                charImg.raycastTarget = false;
                motionRT = charRT;
            }

            // CTA 알약 버튼(시각용 — 클릭은 카드 전체가 받음)
            var ctaRT = ChildRect("CTA", body);
            ctaRT.anchorMin = ctaRT.anchorMax = new Vector2(0.5f, 0f);
            ctaRT.pivot = new Vector2(0.5f, 0f);
            ctaRT.anchoredPosition = style.ctaPos;
            ctaRT.sizeDelta = style.ctaSize;
            var ctaImg = ctaRT.gameObject.AddComponent<Image>();
            ctaImg.sprite = Rounded(); ctaImg.type = Image.Type.Sliced;
            ctaImg.pixelsPerUnitMultiplier = style.ctaPpu;
            ctaImg.color = style.useAccentColor ? (Color)accent : (Color)style.ctaColor;
            ctaImg.raycastTarget = false;
            var ctaText = MakeText("Text", ctaRT, cta + style.ctaSuffix, style.ctaFont, White, font, bold: true);
            StretchFull(ctaText.rectTransform, style.ctaPad);

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
            // Close 변형이 없는 캐릭터는 null을 넘긴다. HomeCharacterIdle.cs:58이 eyesClosed==null이면
            // BlinkLoop를 시작하지 않으므로 Image.sprite가 null로 덮여 캐릭터가 사라지는 일이 없다.
            // LoadPngSprite(null)은 AssetDatabase 호출에서 예외를 던질 수 있어 호출 자체를 막는다.
            // 2레이어일 때 motionRT는 CharacterFg다. 배경(CharacterBg)은 움직이지 않는다.
            idle.Setup(motionRT, charImg,
                LoadPngSprite(charOpenPath),
                string.IsNullOrEmpty(charClosePath) ? null : LoadPngSprite(charClosePath));

            // 모션 값은 [SerializeField] private이라 Setup으로 못 넘긴다. 스크립트를 고치는 대신
            // 이 인스턴스의 직렬화 값만 덮어쓴다(MainSceneView 와이어링과 같은 방식).
            if (style.overrideIdleMotion)
            {
                var soIdle = new SerializedObject(idle);
                soIdle.FindProperty("breathAmplitude").floatValue = style.idleBreathAmplitude;
                soIdle.FindProperty("breathScale").floatValue     = style.idleBreathScale;
                soIdle.FindProperty("swayAmplitude").floatValue   = style.idleSwayAmplitude;
                soIdle.FindProperty("nodAngle").floatValue        = style.idleNodAngle;
                soIdle.ApplyModifiedProperties();
            }
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

        // ===== 좌측 AAC 블록 헬퍼 (원본 1122x1402 좌표 -> 화면) =====

        // 원본 좌표의 이미지 자식을 만든다. 앵커/피벗은 좌상단이라 좌표 변환이 그대로 대응된다.
        static RectTransform AacChild(string name, Transform parent, string pngPath,
            float x, float y, float w, float h)
        {
            var rt = ChildRect(name, parent);
            var s = AacScale;
            Anchor(rt, new Vector2(0f, 1f), new Vector2(x * s.x, -y * s.y), new Vector2(w * s.x, h * s.y));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadPngSprite(pngPath);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return rt;
        }

        // 부모가 AacBody가 아닌 중간 요소(예: CTA)일 때. 부모의 원본 좌상단(px, py)을 빼서 상대 좌표로 만든다.
        static RectTransform AacChildIn(string name, Transform parent, string pngPath,
            float px, float py, float x, float y, float w, float h)
            => AacChild(name, parent, pngPath, x - px, y - py, w, h);

        // 설계 글리프 박스(glyphTop ~ glyphTop+glyphH)의 세로 중심에 TMP 박스(boxH)의 중심을 맞춘다.
        // 폰트를 바꿔도 글자가 설계 위치에 머무르도록 하기 위한 계산이다.
        static Vector2 AacTextPos(float x, float glyphTop, float glyphH, float boxH)
        {
            var s = AacScale;
            return new Vector2(x * s.x, -((glyphTop + glyphH * 0.5f) * s.y - boxH * 0.5f));
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
