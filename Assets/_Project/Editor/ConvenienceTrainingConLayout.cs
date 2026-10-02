using System.Collections.Generic;
using System.IO;
using System.Linq;
using Artti.Training;
using Artti.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Artti.Editor
{
    // 편의점 훈련 화면을 TrainingCon 시안(1448x1086)으로 바꾼다. 점원 3D·완료 화면·[Managers]는 그대로 두고
    // 옛 대시보드 조각만 지운 뒤 새로 만들어 TrainingUIView / ConvenienceHudView / ConvenienceDashboardView에 다시 연결한다.
    // - 씬에 손배치가 있어 빌더를 다시 돌리지 않고 이 메뉴로 그 자리에서 고친다. 빌더도 마지막에 Apply를 불러 결과가 같다.
    // - 좌표는 시안 그림 px(좌상단 기준, y 아래 +). 캔버스(1536x1024 Expand) 단위 = 시안 px x (1024/1086).
    //   왼쪽 열은 화면 왼쪽, 오른쪽 열은 화면 오른쪽, 가운데(배너·점원·상품·말하기)는 화면 가운데에 붙어 폰(20:9)에서도 겹치지 않는다.
    // - 시안에 없는 그림(빈 말풍선, 아이콘, 주황 선택 버튼, 상품 칸)은 Art_Source/ui_training_con/make_derived.py가 만든다.
    // - 완료 화면(ending 시안)은 ConvenienceTrainingConLayout.Ending.cs (Apply 끝에서 함께 다시 만든다)
    public static partial class ConvenienceTrainingConLayout
    {
        const string Dir = "Assets/_Project/Art/UI/TrainingCon/";
        const string Der = Dir + "derived/";
        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";

        const float MockW = 1448f, MockH = 1086f;
        const float K = 1024f / 1086f;   // 시안 px -> 캔버스 단위 (세로 1024 안에 시안 세로 전체가 들어가게)

        enum Anchor { TL, TR, TC, BL, BR, BC }

        // ===== 시안 속 자리 (본체 = 빛 번짐을 뺀 판/버튼 몸통) =====
        static readonly Rect FieldModeBody = Rect.MinMaxRect(14f, 12f, 202f, 68f);
        static readonly Rect FieldCaption = Rect.MinMaxRect(226f, 22f, 470f, 58f);
        static readonly Rect MissionBody = Rect.MinMaxRect(12f, 90f, 412f, 258f);
        static readonly Rect MissionTitle = Rect.MinMaxRect(36f, 104f, 404f, 134f);
        static readonly Rect MissionText = Rect.MinMaxRect(36f, 134f, 404f, 166f);
        // 진행 물결 = boatwater.png(2170x725). 그림 속 별 5개 중심 x(px), 별 줄 y, 별 원 지름. 간격이 고르지 않아 별마다 따로 둔다
        const string WavePath = Dir + "boatwater.png";
        static readonly Vector2 WaveSize = new Vector2(2170f, 725f);
        static readonly float[] WaveNodeX = { 217f, 647f, 1095f, 1538f, 1955f };
        const float WaveNodeY = 350f, WaveNodeDiameter = 84f;
        const float WaveX0 = 56f, WaveX1 = 368f, WaveY = 226f;   // 시안 px: 첫 별 x ~ 끝 별 x, 별 줄 높이
        // 진행 배 = boat.png(1536x1024), 배 몸통 (413,181)-(1136,841). 배 바닥(y 841)을 현재 별 바로 위에 띄운다
        const string BoatPath = Dir + "boat.png";
        static readonly Vector2 BoatSize = new Vector2(1536f, 1024f);
        const float BoatBottomY = 841f, BoatVisibleH = 660f;
        const float BoatHeight = 48f;      // 시안 px: 돛 끝 ~ 배 바닥
        const float BoatLift = 4f;         // 시안 px: 배 바닥을 별 중심보다 이만큼 위에
        const float BoatMoveSeconds = 1f;  // 다음 별까지 스르륵 가는 시간

        static readonly Rect LeftBody = Rect.MinMaxRect(12f, 275f, 461f, 881f);
        static readonly Rect LeftIcon = Rect.MinMaxRect(45f, 296f, 95f, 346f);
        static readonly Rect LeftTitle = Rect.MinMaxRect(110f, 298f, 452f, 344f);
        static readonly Rect LeftSub = Rect.MinMaxRect(42f, 352f, 454f, 412f);
        const float BubbleX0 = 40f, BubbleX1 = 430f, BubbleTop = 431f, BubblePitch = 88.2f, BubbleH = 75f;
        const float ActiveX0 = 34f, ActiveX1 = 442f;
        const float IconCx = 83f, IconSize = 46f, LabelX0 = 124f, LabelX1 = 426f;

        static readonly Rect HintBody = Rect.MinMaxRect(1128f, 93f, 1442f, 835f);
        static readonly Rect HintIcon = Rect.MinMaxRect(1153f, 108f, 1221f, 176f);
        static readonly Rect HintTitle = Rect.MinMaxRect(1234f, 116f, 1438f, 162f);
        static readonly Rect HintSub = Rect.MinMaxRect(1234f, 161f, 1440f, 189f);
        const float HintX0 = 1146f, HintX1 = 1422f, HintTop = 205f, HintPitch = 90f, HintH = 76f;
        static readonly Rect TipBody = Rect.MinMaxRect(1146f, 669f, 1424f, 818f);
        static readonly Rect TipStar = Rect.MinMaxRect(1164f, 689f, 1200f, 725f);
        static readonly Rect TipTitle = Rect.MinMaxRect(1208f, 689f, 1418f, 725f);
        static readonly Rect TipText = Rect.MinMaxRect(1166f, 733f, 1420f, 808f);
        const float TopBtnY0 = 14f, TopBtnY1 = 63f, TopBtnRight = 1440f, TopBtnGap = 6f;

        // 배너 = banner.png(2171x724, ARTTI 글자가 그림에 들어 있다). 판 몸통(알파 245 이상) x, y, 너비, 높이(px).
        //   가로는 시안 폭(435)에, 세로는 그림 비율대로. 위쪽 턱(LED 줄)이 보이게 화면 맨 위에 붙인다
        const string BannerPath = Dir + "banner.png";
        static readonly Rect BannerSpriteBody = new Rect(61f, 130f, 2051f, 387f);
        static readonly Vector2 BannerSpriteSize = new Vector2(2171f, 724f);
        static readonly Rect BannerBody = Rect.MinMaxRect(505f, 0f, 940f, 435f * 387f / 2051f);

        static readonly Rect ProductBody = Rect.MinMaxRect(479f, 706f, 1115f, 905f);
        static readonly Rect CartIcon = Rect.MinMaxRect(492f, 719f, 520f, 747f);
        static readonly Rect ProductTitle = Rect.MinMaxRect(528f, 716f, 720f, 750f);
        static readonly Rect ViewportBox = Rect.MinMaxRect(531f, 752f, 1073f, 892f);
        const float TileW = 80f, TileH = 132f, TileGap = 11.6f;
        static readonly Rect NavLeftBody = Rect.MinMaxRect(485f, 798f, 525f, 838f);
        static readonly Rect NavRightBody = Rect.MinMaxRect(1071f, 798f, 1111f, 838f);

        static readonly Rect SpeakBody = Rect.MinMaxRect(532f, 934f, 915f, 1051f);
        static readonly Rect KeyboardBody = Rect.MinMaxRect(1245f, 985f, 1422f, 1050f);

        // 시안에 없는 자리: 점원(가운데 열, 아래쪽은 상품 판 뒤로 숨는다), 점원 말풍선, 듣는 중 표시
        static readonly Rect ClerkBox = Rect.MinMaxRect(564f, 116f, 1030f, 816f);
        // 점원 말풍선 = Malpong.png(1623x969, 몸통 (117,142)-(1505,762), 꼬리 끝 (1420,824) = 오른쪽 아래).
        //   그림을 좌우로 뒤집어(꼬리 = 왼쪽 아래) 점원 머리 오른쪽 위에 두고, 꼬리 끝이 얼굴에서 조금 떨어진 곳(시안 860,268)을 가리킨다.
        //   (얼굴 오른쪽 가장자리는 시안 x 약 830. 더 붙이면 말풍선이 얼굴에 닿아 보인다)
        //   뒤집기는 그림 자식(Art)의 배율 -1로 한다(글자는 뒤집지 않음, 원본 PNG를 바꿔 넣어도 그대로 동작).
        //   몸통 폭은 시안 300px: 4:3·3:2(가장 좁은 화면)에서도 오른쪽 대화 힌트 패널과 약 50 떨어진다.
        const string NpcBubblePath = Dir + "Malpong.png";
        static readonly Vector2 NpcBubbleSize = new Vector2(1623f, 969f);
        static readonly Rect NpcBubbleBody = Rect.MinMaxRect(117f, 142f, 1505f, 762f);
        static readonly Vector2 NpcBubbleTailTip = new Vector2(1420f, 824f);
        static readonly Vector2 NpcTailTarget = new Vector2(860f, 268f);
        const float NpcBodyWidth = 300f;

        // 뒤집은 그림 기준(좌우 거울) 몸통과 꼬리 끝
        static Rect NpcBodyShown => Rect.MinMaxRect(NpcBubbleSize.x - NpcBubbleBody.xMax, NpcBubbleBody.yMin,
            NpcBubbleSize.x - NpcBubbleBody.xMin, NpcBubbleBody.yMax);
        static Vector2 NpcTailShown => new Vector2(NpcBubbleSize.x - NpcBubbleTailTip.x, NpcBubbleTailTip.y);

        // 팝업(도움말, 나가기/현장모드 확인) = popup.png 판 + yes.png(확인)/no.png(취소) 버튼. 글자는 TMP.
        //   popup.png 1129x1393, 판 몸통 (42,230)-(1087,1169) / yes·no 2172x724, 몸통 (135,162)-(2039,559)
        //   판은 그림 비율 그대로 쓰고(늘리면 안쪽 빛줄기가 찌그러진다) 카드 크기를 그 비율에 맞춘다.
        const string PopupPath = Dir + "popup.png";
        const string YesPath = Dir + "yes.png";
        const string NoPath = Dir + "no.png";
        static readonly Vector2 PopupSize = new Vector2(1129f, 1393f);
        static readonly Rect PopupBody = Rect.MinMaxRect(42f, 230f, 1087f, 1169f);
        static readonly Vector2 ChoiceSize = new Vector2(2172f, 724f);
        static readonly Rect ChoiceBody = Rect.MinMaxRect(135f, 162f, 2039f, 559f);
        static readonly Color ChoiceText = new Color32(20, 30, 74, 255);
        const float PopupCorner = 112f;   // popup.png 판 모서리 반지름(px)
        const float PopupAlpha = 0.92f;   // 팝업 판만 살짝 비치게(글자·버튼은 불투명). 선형 색 공간이라 0.8대면 꽤 많이 비친다
        static readonly Rect MicBox = Rect.MinMaxRect(564f, 612f, 1030f, 694f);

        // 옛 대시보드 조각 ([Canvas] 바로 아래). 점원(Clerk/Clerk3D), 말풍선, 듣는 중 표시, 팝업, 완료 화면은 남긴다.
        static readonly string[] OldNames =
        {
            "StoreBackground", "TopShade", "BottomShade", "ARTTILogo", "Profile", "Mission", "DialogueHints",
            "SpeechPracticeCard", "RecommendedProducts", "ProductViewport", "Previous", "Next", "VoiceButton",
            "HelpButton", "SettingsButton",
            // 이 메뉴가 만든 것 (다시 실행할 때 지우고 새로 만든다)
            "TrainingConBackground", "TrainingCon", "KeyboardModal", "TrainingSettingsModal", "Onboarding",
            "OnboardingWelcome",
            // 글자 도움말 창은 화면 둘러보기(온보딩)로 바꿨다
            "HelpModal",
        };

        static readonly string[] HintNormal =
        {
            Der + "hint_request_item.png", Dir + "btn_ask_price.png", Dir + "btn_pay.png",
            Dir + "btn_additional_request.png", Dir + "btn_greeting_finish.png",
        };

        // 버튼 그림 속 몸통(알파 245 이상, px, 좌상단 기준 x, y, 너비, 높이). make_derived.py와 같은 측정값
        static readonly Dictionary<string, Rect> SpriteBodies = new Dictionary<string, Rect>
        {
            { "btn_speak_now.png", new Rect(17f, 18f, 508f, 155f) },
            { "btn_keyboard_input.png", new Rect(11f, 12f, 297f, 109f) },
            { "btn_field_mode.png", new Rect(11f, 11f, 338f, 101f) },
            { "btn_help.png", new Rect(11f, 10f, 213f, 92f) },
            { "btn_exit.png", new Rect(11f, 10f, 200f, 92f) },
        };

        static readonly string[] IconKeys = { "basket", "question", "card", "won", "bag", "pin", "smile" };

        // ===== 화면 둘러보기(온보딩): 짚을 곳([Canvas] 기준 경로)과 문장. 실제 대화 순서 -> 보조 기능 -> 위쪽 버튼 =====
        //   대화 모드 문장이 비어 있으면 정확도 모드 문장을 쓴다(말하기 단계만 모드별로 다르다).
        static readonly string[] TourTargets =
        {
            "NPCBubble/BodyArea", "TrainingCon/SpeakButton", "TrainingCon/PhrasePanel", "TrainingCon/HintPanel",
            "TrainingCon/ProductPanel", "TrainingCon/MissionPanel", "TrainingCon/KeyboardButton",
            "TrainingCon/FieldModeButton", "TrainingCon/TopButtonsArea",
        };
        static readonly string[] TourAccuracy =
        {
            "점원이 하는 말이 여기에 나와요.\n말풍선을 누르면 다시 들을 수 있어요.",
            "이 버튼을 누르고 또박또박 말해 보세요.\n잘 안 들리면 점원이 다시 말해 달라고 해요.",
            "말하기 어려우면 이 문장을 눌러도 돼요.\n빛나는 문장이 지금 하면 좋은 말이에요.",
            "하고 싶은 말의 종류를 누르면\n맞는 문장이 왼쪽 맨 위에 나와요.",
            "상품을 누르면 그 물건을 달라고 말해요.\n화살표로 다른 상품도 볼 수 있어요.",
            "지금 해야 할 일이 여기에 나와요.\n한 단계씩 해낼 때마다 배가 다음 별로 나아가요.",
            "말 대신 글자로 써서\n점원에게 보낼 수 있어요.",
            "현장모드로 가면 실제 가게에서\n카메라로 간판을 비춰 볼 수 있어요.",
            "이 설명은 ‘도움말’을 누르면 다시 볼 수 있어요.\n‘나가기’를 누르면 훈련을 마쳐요.",
        };
        static readonly string[] TourConversation =
        {
            "",
            "이 버튼을 누르고 편하게 말해 보세요.\n조금 틀려도 점원이 알아들어요.",
            "이 문장을 누르면 점원에게 대신 말해 줘요.\n빛나는 문장이 지금 하면 좋은 말이에요.",
            "", "", "", "", "", "",
        };
        // 설명 창 그림 = Boarding.png(1448x1086). 몸통(알파 245 이상, 위쪽 가운데 탭 포함) (91,71)-(1358,990),
        //   탭 아래 판 윗변 y 122, 모서리 반지름 약 110px. 탭 안쪽 (470,80)-(980,175)에 단계 번호를 넣는다
        const string TourCardPath = Dir + "Boarding.png";
        static readonly Vector2 TourCardSize = new Vector2(1448f, 1086f);
        static readonly Rect TourCardBody = Rect.MinMaxRect(91f, 71f, 1358f, 990f);
        const float TourCardPanelTop = 122f, TourCardCorner = 112f;
        // 버튼 그림(2172x724): 100.png = 건너뛰기(진한 파랑), 101.png = 다음(밝게 빛나는 파랑). 몸통 = 알파 245 이상
        const string TourSkipPath = Dir + "100.png", TourNextPath = Dir + "101.png";
        static readonly Vector2 TourButtonSize = new Vector2(2172f, 724f);
        static readonly Rect TourSkipBody = Rect.MinMaxRect(162f, 148f, 2010f, 553f);
        static readonly Rect TourNextBody = Rect.MinMaxRect(210f, 147f, 1959f, 558f);
        const float TourCardW = 560f, TourCardH = 270f;   // Card 사각형 = 탭까지 포함한 몸통

        // 처음 들어왔을 때 환영 창 = BoardingStart.png(1448x1086, 글·그림이 다 들어 있다). 몸통 (99,66)-(1356,1037),
        //   그림 속 X 가운데 (1261,212) 반지름 약 43px. 아래 빈 칸(y 820~1012) 가운데에 바로 시작하기(100) / 화면 둘러보기(101)
        const string WelcomePath = Dir + "BoardingStart.png";
        static readonly Vector2 WelcomeSize = new Vector2(1448f, 1086f);
        static readonly Rect WelcomeBody = Rect.MinMaxRect(99f, 66f, 1356f, 1037f);
        static readonly Vector2 WelcomeClose = new Vector2(1261f, 212f);
        const float WelcomeButtonY = 916f;
        const float WelcomeCardW = 640f, WelcomeButtonW = 230f, WelcomeButtonH = 52f, WelcomeCloseHit = 64f;
        const string WelcomeSpeech = "환영해요! 편의점 대화를 쉽고 재미있게 연습해 볼까요? 천천히 따라가면 누구나 할 수 있어요.";

        static readonly string[] QuickPhrases = { "물 주세요", "어디에 있어요?", "얼마예요?", "계산할게요", "감사합니다" };

        // 시안 순서(생수, 삼각김밥, 컵라면, 과자, 초콜릿, 캔커피) 다음에 나머지 상품
        static readonly string[] ProductIds =
        {
            "water", "triangle_gimbap", "cup_ramen", "snack", "chocolate", "can_coffee",
            "milk", "sandwich", "gum", "mask", "tissue", "umbrella", "charger",
        };
        static readonly string[] ProductNames =
        {
            "생수", "삼각김밥", "컵라면", "과자", "초콜릿", "캔커피",
            "우유", "샌드위치", "껌", "마스크", "휴지", "우산", "충전기",
        };
        static readonly string[] ProductCategories =
        {
            "drinks", "meals", "meals", "snacks", "snacks", "drinks",
            "drinks", "meals", "snacks", "daily", "daily", "daily", "daily",
        };

        static readonly Color Ink = new Color32(22, 30, 58, 255);
        static readonly Color TitleCyan = new Color32(172, 234, 255, 255);
        static readonly Color SubText = new Color32(214, 224, 245, 255);
        static readonly Color CardNavy = new Color32(10, 22, 66, 250);

        static TMP_FontAsset _font;

        [MenuItem("Artti/Fix/TrainingConvenience - Apply TrainingCon Design")]
        static void ApplyMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ConvenienceTrainingConLayout] Play 모드에서는 실행하지 않는다.");
                return;
            }
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePaths.TrainingConvenience)
                scene = EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
            var canvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "[Canvas]");
            if (canvas == null)
            {
                Debug.LogError("[ConvenienceTrainingConLayout] [Canvas] 없음");
                return;
            }
            Apply(canvas);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SceneBuilderUtils.SaveActiveSceneAgainAfterEditorTicks();
            Debug.Log("[ConvenienceTrainingConLayout] 완료 — TrainingCon 시안 적용 후 씬 저장");
        }

        public static void Apply(GameObject canvasGo)
        {
            AssetDatabase.Refresh();
            _font = SceneBuilderUtils.GetKoreanFont();
            var canvas = canvasGo.transform;
            foreach (var name in OldNames)
                for (int i = canvas.childCount - 1; i >= 0; i--)
                    if (canvas.GetChild(i).name == name) Object.DestroyImmediate(canvas.GetChild(i).gameObject);

            var uiView = canvasGo.GetComponent<TrainingUIView>();
            var hud = canvasGo.GetComponent<ConvenienceHudView>();
            var dashboard = canvasGo.GetComponent<ConvenienceDashboardView>();

            // 배경: 화면을 꽉 채우고(넘치는 쪽은 잘림) 맨 뒤에 둔다
            var bg = NewRect("TrainingConBackground", canvas);
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0.5f);
            bg.sizeDelta = new Vector2(MockW, MockH);
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.sprite = Sprite(Dir + "background_convenience_store.png");
            bgImage.raycastTarget = false;
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = MockW / MockH;
            bg.SetAsFirstSibling();

            // 점원 3D(RawImage)는 배경 바로 위, 패널들 뒤
            var clerk3D = canvas.Find("Clerk3D") as RectTransform;
            if (clerk3D != null)
            {
                Place(clerk3D, Anchor.BC, ClerkBox);
                clerk3D.SetSiblingIndex(bg.GetSiblingIndex() + 1);
                // 보이지 않는 칸 = 점원 얼굴(머리카락 포함, 점원 그림 안 비율). 둘러보기 설명 창이 피한다
                var oldFace = clerk3D.Find("FaceArea");
                if (oldFace != null) Object.DestroyImmediate(oldFace.gameObject);
                var face = NewRect("FaceArea", clerk3D);
                face.anchorMin = new Vector2(0.33f, 0.55f);
                face.anchorMax = new Vector2(0.72f, 0.86f);
                face.offsetMin = face.offsetMax = Vector2.zero;
            }
            var oldClerk = canvas.Find("Clerk") as RectTransform;   // 꺼 둔 옛 그림 점원(되돌리기용)
            if (oldClerk != null) Place(oldClerk, Anchor.BC, ClerkBox);

            var root = NewRect("TrainingCon", canvas);
            Stretch(root);
            root.SetSiblingIndex((clerk3D != null ? clerk3D.GetSiblingIndex() : bg.GetSiblingIndex()) + 1);

            // 위쪽 그늘: 밝은 천장 위 글자·버튼이 읽히게 (시안의 어두운 위쪽 가장자리)
            var shade = Img("TopShade", root, Der + "shade_top.png");
            shade.anchorMin = new Vector2(0f, 1f);
            shade.anchorMax = Vector2.one;
            shade.pivot = new Vector2(0.5f, 1f);
            shade.anchoredPosition = Vector2.zero;
            shade.sizeDelta = new Vector2(0f, 120f * K);

            // 아래 유리 띠: 화면 폭 전체, 가운데가 살짝 솟은 곡선(그림 = 시안 y 994~1086). 버튼들은 모두 이 위에 그린다
            var bottomGlass = Img("BottomGlass", root, Der + "bottom_glass.png");
            bottomGlass.anchorMin = Vector2.zero;
            bottomGlass.anchorMax = new Vector2(1f, 0f);
            bottomGlass.pivot = new Vector2(0.5f, 0f);
            bottomGlass.anchoredPosition = Vector2.zero;
            bottomGlass.sizeDelta = new Vector2(0f, (MockH - 994f) * K);

            var mission = BuildMission(root);
            var fieldMode = BuildFieldMode(root);
            var left = BuildLeftPanel(root);
            var hints = BuildHintPanel(root);
            var top = BuildTopButtons(root);
            BuildBanner(root);
            var products = BuildProducts(root);
            var speak = SpriteButton("SpeakButton", root, Anchor.BC, Dir + "btn_speak_now.png", SpeakBody, true);
            var keyboard = SpriteButton("KeyboardButton", root, Anchor.BR, Dir + "btn_keyboard_input.png", KeyboardBody, false);

            RestyleNpcBubble(canvas, root);
            var mic = canvas.Find("MicListeningIndicator") as RectTransform;
            if (mic != null)
            {
                Place(mic, Anchor.BC, MicBox);
                mic.SetSiblingIndex(root.GetSiblingIndex() + 2);
            }

            var exitModal = canvas.Find("SettingsModal");
            StyleConfirmPopup(exitModal);
            var keyboardModal = BuildKeyboardModal(canvas);
            var tour = BuildOnboarding(canvas);
            var welcome = BuildWelcome(canvas);
            ApplyEnding(canvasGo);   // 변경점: 완료 화면(ending 시안)도 같은 메뉴·빌더에서 다시 만든다
            var completion = canvas.Find("CompletionRoot");
            if (completion != null) completion.SetAsLastSibling();

            // ===== 연결 =====
            if (uiView != null)
            {
                var so = new SerializedObject(uiView);
                so.FindProperty("freeTalkButton").objectReferenceValue = speak;
                so.FindProperty("bubbleTailPoint").vector2Value = new Vector2(
                    NpcTailShown.x / NpcBubbleSize.x, 1f - NpcTailShown.y / NpcBubbleSize.y);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (hud != null)
            {
                var so = new SerializedObject(hud);
                SetObjects(so.FindProperty("stepDots"), mission.dots);
                so.FindProperty("stepLabel").objectReferenceValue = mission.label;
                so.FindProperty("stepCounter").objectReferenceValue = null;   // 원형 차트(1/5)는 뺐다: 배가 진행을 보여 준다
                so.FindProperty("stepFillMask").objectReferenceValue = null;
                so.FindProperty("stepFillWidth").floatValue = (WaveX1 - WaveX0) * K;
                so.FindProperty("stepNodeOnSprite").objectReferenceValue = Sprite(Der + "node_on.png");
                so.FindProperty("stepNodeOffSprite").objectReferenceValue = Sprite(Der + "node_off.png");
                so.FindProperty("stepBoat").objectReferenceValue = mission.boat;
                so.FindProperty("stepBoatOffset").vector2Value = new Vector2(0f, BoatLift * K);
                so.FindProperty("stepMoveSeconds").floatValue = BoatMoveSeconds;
                so.FindProperty("pauseBtn").objectReferenceValue = null;
                so.FindProperty("exitBtn").objectReferenceValue = top.exit;
                so.FindProperty("fieldModeBtn").objectReferenceValue = fieldMode;
                if (exitModal != null)
                {
                    so.FindProperty("pauseModal").objectReferenceValue = exitModal.gameObject;
                    so.FindProperty("pauseTitleText").objectReferenceValue = FindText(exitModal, "Card/Title");
                    so.FindProperty("pauseMessageText").objectReferenceValue = FindText(exitModal, "Card/Message");
                    so.FindProperty("pauseConfirmLabel").objectReferenceValue = FindText(exitModal, "Card/Confirm/Label");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (dashboard != null)
            {
                var so = new SerializedObject(dashboard);
                so.FindProperty("productScroll").objectReferenceValue = products.scroll;
                SetObjects(so.FindProperty("categoryButtons"), new Object[0]);
                SetStrings(so.FindProperty("categoryIds"), new string[0]);
                so.FindProperty("categoryContent").objectReferenceValue = null;
                SetObjects(so.FindProperty("productButtons"), products.buttons);
                SetStrings(so.FindProperty("productIds"), ProductIds);
                SetStrings(so.FindProperty("productNames"), ProductNames);
                SetStrings(so.FindProperty("productUtterances"), ProductNames.Select(n => n + " 주세요").ToArray());
                SetStrings(so.FindProperty("productCategoryIds"), ProductCategories);
                so.FindProperty("productContent").objectReferenceValue = products.content;
                so.FindProperty("previousButton").objectReferenceValue = products.previous;
                so.FindProperty("nextButton").objectReferenceValue = products.next;
                so.FindProperty("productTilePitch").floatValue = (TileW + TileGap) * K;

                SetObjects(so.FindProperty("quickPhraseButtons"), left.buttons);
                SetStrings(so.FindProperty("quickPhrases"), QuickPhrases);
                SetObjects(so.FindProperty("quickPhraseLabels"), left.labels);
                so.FindProperty("quickPhraseGuide").objectReferenceValue = null;
                SetObjects(so.FindProperty("quickPhraseActiveBubbles"), left.active);
                SetObjects(so.FindProperty("quickPhraseIcons"), left.icons);
                SetStrings(so.FindProperty("iconKeys"), IconKeys);
                SetObjects(so.FindProperty("iconSprites"), IconKeys.Select(k => (Object)Sprite(Der + "icon_" + k + ".png")).ToArray());

                SetObjects(so.FindProperty("hintCategoryButtons"), hints.buttons);
                SetObjects(so.FindProperty("hintCategoryNormalSprites"), HintNormal.Select(p => (Object)Sprite(p)).ToArray());
                // 선택 상태 그림은 쓰지 않는다: 디자이너 그림대로 다섯 버튼 모두 같은 색(파랑). 선택 그림이 생기면 여기에 넣는다
                SetObjects(so.FindProperty("hintCategoryActiveSprites"), new Object[0]);
                so.FindProperty("speechPracticeText").objectReferenceValue = null;

                // 도움말 버튼은 화면 둘러보기가 맡는다(아래 TrainingOnboardingView.replayButton)
                so.FindProperty("helpButton").objectReferenceValue = null;
                so.FindProperty("helpPanel").objectReferenceValue = null;
                so.FindProperty("helpCloseButton").objectReferenceValue = null;
                so.FindProperty("keyboardButton").objectReferenceValue = keyboard;
                so.FindProperty("keyboardPanel").objectReferenceValue = keyboardModal.root;
                so.FindProperty("keyboardField").objectReferenceValue = keyboardModal.field;
                so.FindProperty("keyboardSendButton").objectReferenceValue = keyboardModal.send;
                so.FindProperty("keyboardCloseButton").objectReferenceValue = keyboardModal.close;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 화면 둘러보기: 뷰는 캔버스에 붙인다(둘러보기 화면은 꺼 둔 채 저장하므로 그 안에 두면 Awake가 안 불린다)
            if (!canvasGo.TryGetComponent<TrainingOnboardingView>(out var tourView))
                tourView = canvasGo.AddComponent<TrainingOnboardingView>();
            {
                var so = new SerializedObject(tourView);
                so.FindProperty("root").objectReferenceValue = tour.root;
                so.FindProperty("dimTop").objectReferenceValue = tour.dims[0];
                so.FindProperty("dimBottom").objectReferenceValue = tour.dims[1];
                so.FindProperty("dimLeft").objectReferenceValue = tour.dims[2];
                so.FindProperty("dimRight").objectReferenceValue = tour.dims[3];
                so.FindProperty("frame").objectReferenceValue = tour.frame;
                so.FindProperty("card").objectReferenceValue = tour.card;
                so.FindProperty("counterText").objectReferenceValue = tour.counter;
                so.FindProperty("bodyText").objectReferenceValue = tour.body;
                so.FindProperty("nextButton").objectReferenceValue = tour.next;
                so.FindProperty("nextLabel").objectReferenceValue = tour.next.GetComponentInChildren<TMP_Text>(true);
                so.FindProperty("skipButton").objectReferenceValue = tour.skip;
                so.FindProperty("replayButton").objectReferenceValue = top.help;
                var targets = TourTargets.Select(path => (Object)(canvas.Find(path) as RectTransform)).ToArray();
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i] == null) Debug.LogWarning($"[ConvenienceTrainingConLayout] 둘러보기 대상 없음: {TourTargets[i]}");
                SetObjects(so.FindProperty("targets"), targets);
                SetStrings(so.FindProperty("accuracyTexts"), TourAccuracy);
                SetStrings(so.FindProperty("conversationTexts"), TourConversation);
                var faceArea = canvas.Find("Clerk3D/FaceArea") as RectTransform;
                SetObjects(so.FindProperty("avoidAreas"), faceArea != null ? new Object[] { faceArea } : new Object[0]);
                so.FindProperty("welcomeRoot").objectReferenceValue = welcome.root;
                so.FindProperty("welcomeCard").objectReferenceValue = welcome.card;
                so.FindProperty("welcomeTourButton").objectReferenceValue = welcome.tour;
                so.FindProperty("welcomeStartButton").objectReferenceValue = welcome.start;
                so.FindProperty("welcomeCloseButton").objectReferenceValue = welcome.close;
                so.FindProperty("welcomeSpeech").stringValue = WelcomeSpeech;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var sceneRoot = Object.FindFirstObjectByType<TrainingSceneRoot>(FindObjectsInactive.Include);
            if (sceneRoot != null)
            {
                var so = new SerializedObject(sceneRoot);
                so.FindProperty("onboarding").objectReferenceValue = tourView;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            keyboardModal.root.SetActive(false);
            tour.root.SetActive(false);
            welcome.root.SetActive(false);
            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
        }

        // ===== 왼쪽 위: 현장모드 + 오늘의 미션 =====

        static Button BuildFieldMode(RectTransform root)
        {
            var button = SpriteButton("FieldModeButton", root, Anchor.TL, Dir + "btn_field_mode.png", FieldModeBody, false);
            var caption = Text("FieldModeCaption", root, "편의점에서 직접 말해봐요", 18f, SubText, false);
            Place(caption.rectTransform, Anchor.TL, FieldCaption);
            return button;
        }

        // 오늘의 미션: 제목 + 지금 할 일 + 진행 물결(별 5개) 위를 움직이는 배
        static (Image[] dots, TMP_Text label, RectTransform boat) BuildMission(RectTransform root)
        {
            var panel = Panel("MissionPanel", root, Anchor.TL, MissionBody, Dir + "glass_top_left_info_panel.png",
                new Rect(13f, 13f, 413f, 161f), new Vector2(438f, 189f));

            var title = Text("Title", panel, "오늘의 미션", 20f, Color.white, true);
            Child(title.rectTransform, MissionBody, MissionTitle);
            var label = Text("Objective", panel, "점원에게 인사하기", 19f, new Color32(235, 242, 255, 255), false);
            label.overflowMode = TextOverflowModes.Overflow;
            Child(label.rectTransform, MissionBody, MissionText);

            // 물결 그림: 첫 별 ~ 끝 별이 시안 WaveX0 ~ WaveX1에 오게 크기를 맞춘다
            float s = (WaveX1 - WaveX0) / (WaveNodeX[WaveNodeX.Length - 1] - WaveNodeX[0]);
            float left = WaveX0 - WaveNodeX[0] * s, top = WaveY - WaveNodeY * s;
            Child(Img("Wave", panel, WavePath), MissionBody, new Rect(left, top, WaveSize.x * s, WaveSize.y * s));

            // 별 표시 5개(그림 속 별 자리) + 배. 배와 별은 같은 부모라 배가 별 자리를 그대로 따라간다
            var progress = NewRect("Progress", panel);
            Child(progress, MissionBody, Rect.MinMaxRect(WaveX0, WaveY - 15f, WaveX1, WaveY + 15f));
            var dots = new Image[WaveNodeX.Length];
            float nodeSize = WaveNodeDiameter * s * 64f / 38f;   // node_on/off 그림 64px 중 별 원 지름 38px
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = NewRect($"Step_{i + 1}", progress);
                dot.anchorMin = dot.anchorMax = new Vector2(0f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.anchoredPosition = new Vector2((WaveNodeX[i] - WaveNodeX[0]) * s * K, 0f);
                dot.sizeDelta = new Vector2(nodeSize * K, nodeSize * K);
                var image = dot.gameObject.AddComponent<Image>();
                image.sprite = Sprite(Der + (i == 0 ? "node_on.png" : "node_off.png"));
                image.raycastTarget = false;
                dots[i] = image;
            }

            float bs = BoatHeight / BoatVisibleH;
            var boat = Img("Boat", progress, BoatPath);
            boat.anchorMin = boat.anchorMax = new Vector2(0f, 0.5f);
            boat.pivot = new Vector2(0.5f, (BoatSize.y - BoatBottomY) / BoatSize.y);   // 피벗 = 배 바닥
            boat.sizeDelta = new Vector2(BoatSize.x * bs, BoatSize.y * bs) * K;
            boat.anchoredPosition = new Vector2(0f, BoatLift * K);
            return (dots, label, boat);
        }

        // ===== 왼쪽: 무엇을 말해볼까요? (말풍선 5개) =====

        static (Button[] buttons, TMP_Text[] labels, GameObject[] active, Image[] icons) BuildLeftPanel(RectTransform root)
        {
            var panel = Panel("PhrasePanel", root, Anchor.TL, LeftBody, Dir + "glass_left_main_panel.png",
                new Rect(13f, 16f, 446f, 602f), new Vector2(472f, 632f));
            Child(Img("TitleIcon", panel, Der + "title_chat.png", true), LeftBody, LeftIcon);
            var title = Text("Title", panel, "무엇을 말해볼까요?", 28f, TitleCyan, true);
            Child(title.rectTransform, LeftBody, LeftTitle);
            var sub = Text("Subtitle", panel, "아래 문장을 누르면 점원에게 그대로 말해요.\n그대로 말하거나, 나만의 말로 바꿔서 말해보세요!",
                16.5f, SubText, false);
            sub.lineSpacing = 8f;
            sub.textWrappingMode = TextWrappingModes.Normal;
            sub.overflowMode = TextOverflowModes.Overflow;
            Child(sub.rectTransform, LeftBody, LeftSub);

            var normal = Sprite(Der + "bubble_blank.png");
            var glow = Sprite(Der + "bubble_blank_on.png");
            var buttons = new Button[5];
            var labels = new TMP_Text[5];
            var active = new GameObject[5];
            var icons = new Image[5];
            for (int i = 0; i < buttons.Length; i++)
            {
                float top = BubbleTop + i * BubblePitch;
                var slotBox = Rect.MinMaxRect(BubbleX0, top, BubbleX1, top + BubbleH);
                var slot = NewRect($"Phrase_{i + 1}", panel);
                Child(slot, LeftBody, slotBox);

                // 보통 말풍선: 그림 속 몸통(10,11)-(568,118) / 579x137
                var bubble = Img("Bubble", slot, Der + "bubble_blank.png");
                bubble.GetComponent<Image>().sprite = normal;
                bubble.GetComponent<Image>().raycastTarget = true;
                FitBody(bubble, slotBox, slotBox, new Rect(10f, 11f, 558f, 107f), new Vector2(579f, 137f));

                // 강조 말풍선: 몸통(13,45)-(711,187) / 724x241, 시안에서 보통보다 조금 넓다
                var on = Img("Active", slot, Der + "bubble_blank_on.png");
                on.GetComponent<Image>().sprite = glow;
                float s = (ActiveX1 - ActiveX0) / 698f;
                float bodyH = 142f * s;
                var activeBody = Rect.MinMaxRect(ActiveX0, top + (BubbleH - bodyH) / 2f, ActiveX1, top + (BubbleH + bodyH) / 2f);
                FitBody(on, slotBox, activeBody, new Rect(13f, 45f, 698f, 142f), new Vector2(724f, 241f));
                on.gameObject.SetActive(i == 0);

                // 런타임에는 ConvenienceDashboardView가 문장에 맞춰 바꾼다. 에디터에서도 같은 모습이 보이게 미리 넣어 둔다
                string iconKey = ConvenienceHintCatalog.IconOf(QuickPhrases[i]) ?? "question";
                var icon = Img("Icon", slot, Der + "icon_" + iconKey + ".png", true);
                Child(icon, slotBox, Rect.MinMaxRect(IconCx - IconSize / 2f, top + BubbleH / 2f - IconSize / 2f - 1f,
                    IconCx + IconSize / 2f, top + BubbleH / 2f + IconSize / 2f - 1f));
                var label = Text("Label", slot, QuickPhrases[i], 24f, Ink, true);
                label.overflowMode = TextOverflowModes.Truncate;   // 한글 폰트에 말줄임표 글자가 없다
                Child(label.rectTransform, slotBox, Rect.MinMaxRect(LabelX0, top, LabelX1, top + BubbleH - 3f));

                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = bubble.GetComponent<Image>();
                buttons[i] = button;
                labels[i] = label;
                active[i] = on.gameObject;
                icons[i] = icon.GetComponent<Image>();
            }
            return (buttons, labels, active, icons);
        }

        // ===== 오른쪽: 대화 힌트 + 한마디 팁 =====

        static (Button[] buttons, int count) BuildHintPanel(RectTransform root)
        {
            var panel = Panel("HintPanel", root, Anchor.TR, HintBody, Dir + "glass_right_hint_panel.png",
                new Rect(14f, 14f, 325f, 551f), new Vector2(352f, 578f));
            Child(Img("TitleIcon", panel, Der + "title_bulb.png", true), HintBody, HintIcon);
            var title = Text("Title", panel, "대화 힌트", 30f, TitleCyan, true);
            Child(title.rectTransform, HintBody, HintTitle);
            var sub = Text("Subtitle", panel, "필요한 말을 선택해 보세요.", 15.5f, SubText, false);
            Child(sub.rectTransform, HintBody, HintSub);

            string[] names = { "RequestItem", "AskPrice", "Pay", "ExtraRequest", "GreetingFinish" };
            Rect[] bodies =
            {
                new Rect(11f, 15f, 446f, 122f), new Rect(10f, 11f, 446f, 117f), new Rect(10f, 11f, 446f, 117f),
                new Rect(10f, 11f, 446f, 117f), new Rect(10f, 11f, 446f, 117f),
            };
            var buttons = new Button[5];
            for (int i = 0; i < buttons.Length; i++)
            {
                float top = HintTop + i * HintPitch;
                var body = Rect.MinMaxRect(HintX0, top, HintX1, top + HintH);
                var sprite = Sprite(HintNormal[i]);
                var rt = Img("Hint_" + names[i], panel, HintNormal[i]);
                var image = rt.GetComponent<Image>();
                image.raycastTarget = true;
                FitBodyWidth(rt, HintBody, body, bodies[i], new Vector2(sprite.rect.width, sprite.rect.height));
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                buttons[i] = button;
            }

            var tip = NewRect("TipPanel", panel);
            Child(tip, HintBody, TipBody);
            AddFrame(tip, Dir + "glass_tip_panel_purple.png", TipBody, new Rect(14f, 13f, 320f, 167f), new Vector2(348f, 193f));
            Child(Img("Star", tip, Der + "title_star.png", true), TipBody, TipStar);
            var tipTitle = Text("Title", tip, "한마디 팁!", 22f, new Color32(255, 224, 242, 255), true);
            Child(tipTitle.rectTransform, TipBody, TipTitle);
            var tipText = Text("Body", tip, "천천히, 또박또박 말해보세요.\n상대방이 더 잘 이해할 수 있어요.", 16.5f, Color.white, false);
            tipText.lineSpacing = 10f;
            tipText.textWrappingMode = TextWrappingModes.Normal;
            tipText.overflowMode = TextOverflowModes.Overflow;
            Child(tipText.rectTransform, TipBody, TipText);
            return (buttons, buttons.Length);
        }

        static (Button help, Button exit, RectTransform area) BuildTopButtons(RectTransform root)
        {
            // 도움말 / 나가기: 높이를 맞추고 오른쪽 끝에서부터 붙여 놓는다
            string[] paths = { Dir + "btn_help.png", Dir + "btn_exit.png" };
            Rect[] bodies = { new Rect(11f, 10f, 213f, 92f), new Rect(11f, 10f, 200f, 92f) };
            string[] names = { "HelpButton2", "ExitButton" };
            float h = TopBtnY1 - TopBtnY0;
            float x1 = TopBtnRight;
            var result = new Button[2];
            for (int i = 1; i >= 0; i--)
            {
                float w = bodies[i].width * h / bodies[i].height;
                result[i] = SpriteButton(names[i], root, Anchor.TR, paths[i], Rect.MinMaxRect(x1 - w, TopBtnY0, x1, TopBtnY1), false);
                x1 -= w + TopBtnGap;
            }
            var area = NewRect("TopButtonsArea", root);   // 보이지 않는 칸: 도움말+나가기를 한 번에 짚는다
            Place(area, Anchor.TR, Rect.MinMaxRect(x1 + TopBtnGap, TopBtnY0, TopBtnRight, TopBtnY1));
            return (result[0], result[1], area);
        }

        static void BuildBanner(RectTransform root)
        {
            var banner = NewRect("Banner", root);
            Place(banner, Anchor.TC, BannerBody);
            AddFrame(banner, BannerPath, BannerBody, BannerSpriteBody, BannerSpriteSize, sliced: false);
        }

        // ===== 가운데 아래: 추천 상품 =====

        static (ScrollRect scroll, Button[] buttons, RectTransform content, Button previous, Button next)
            BuildProducts(RectTransform root)
        {
            var panel = Panel("ProductPanel", root, Anchor.BC, ProductBody, Dir + "glass_bottom_product_panel.png",
                new Rect(13f, 13f, 612f, 203f), new Vector2(638f, 230f));
            Child(Img("CartIcon", panel, Der + "title_cart.png", true), ProductBody, CartIcon);
            var title = Text("Title", panel, "추천 상품", 20f, Color.white, true);
            Child(title.rectTransform, ProductBody, ProductTitle);

            var viewport = NewRect("ProductViewport", panel);
            Child(viewport, ProductBody, ViewportBox);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);   // 끌어 넘기기용 받침
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("ProductContent", viewport);
            content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(ProductIds.Length * (TileW + TileGap) * K, TileH * K);
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = TileGap * K;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var tileSprite = Sprite(Der + "product_tile.png");
            var buttons = new Button[ProductIds.Length];
            for (int i = 0; i < ProductIds.Length; i++)
            {
                var tile = NewRect($"Product_{i + 1}_{ProductIds[i]}", content);
                tile.sizeDelta = new Vector2(TileW * K, TileH * K);
                var element = tile.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = TileW * K;
                element.preferredHeight = TileH * K;
                var image = tile.gameObject.AddComponent<Image>();
                image.sprite = tileSprite;
                var thumb = Img("Thumb", tile, Der + "thumb_" + ProductIds[i] + ".png", true);
                thumb.anchorMin = thumb.anchorMax = new Vector2(0.5f, 1f);
                thumb.pivot = new Vector2(0.5f, 0.5f);
                thumb.anchoredPosition = new Vector2(0f, -51f * K);
                thumb.sizeDelta = new Vector2(74f * K, 74f * K);
                var label = Text("Label", tile, ProductNames[i], 16.5f, Color.white, true);
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 12f;
                label.fontSizeMax = 16.5f;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                label.rectTransform.anchoredPosition = new Vector2(0f, 6f * K);
                label.rectTransform.sizeDelta = new Vector2((TileW - 6f) * K, 28f * K);
                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                buttons[i] = button;
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

            var previous = PanelSpriteButton("Previous", panel, ProductBody, Dir + "btn_nav_left.png", NavLeftBody, new Rect(11f, 11f, 94f, 95f), new Vector2(116f, 116f));
            var next = PanelSpriteButton("Next", panel, ProductBody, Dir + "btn_nav_right.png", NavRightBody, new Rect(11f, 11f, 94f, 95f), new Vector2(116f, 116f));
            return (scroll, buttons, content, previous, next);
        }

        // ===== 점원 말풍선 (시안에 없음: 점원 머리 오른쪽, 꼬리는 왼쪽 아래) =====

        static void RestyleNpcBubble(Transform canvas, RectTransform root)
        {
            var bubble = canvas.Find("NPCBubble") as RectTransform;
            if (bubble == null) return;
            float s = NpcBodyWidth / NpcBubbleBody.width;   // 시안 px / 그림 px
            var tail = NpcTailShown;
            var body = NpcBodyShown;
            float left = NpcTailTarget.x - tail.x * s;
            float top = NpcTailTarget.y - tail.y * s;
            Place(bubble, Anchor.BC, new Rect(left, top, NpcBubbleSize.x * s, NpcBubbleSize.y * s));
            bubble.SetSiblingIndex(root.GetSiblingIndex() + 1);

            // 말풍선 판(루트 Image)은 끄고, 뒤집은 그림 자식이 보이며 다시 듣기 버튼의 누름 대상이 된다
            if (bubble.TryGetComponent<Image>(out var rootImage)) rootImage.enabled = false;
            if (bubble.TryGetComponent<Outline>(out var outline)) Object.DestroyImmediate(outline);
            var oldTail = bubble.Find("Tail");
            if (oldTail != null) oldTail.gameObject.SetActive(false);   // 꼬리는 그림에 들어 있다
            var oldArt = bubble.Find("Art");
            if (oldArt != null) Object.DestroyImmediate(oldArt.gameObject);
            var art = Img("Art", bubble, NpcBubblePath);
            Stretch(art);
            art.SetAsFirstSibling();
            art.localScale = new Vector3(-1f, 1f, 1f);
            var artImage = art.GetComponent<Image>();
            artImage.raycastTarget = true;
            if (bubble.TryGetComponent<Button>(out var replay)) replay.targetGraphic = artImage;

            var oldArea = bubble.Find("BodyArea");
            if (oldArea != null) Object.DestroyImmediate(oldArea.gameObject);
            var area = NewRect("BodyArea", bubble);   // 보이지 않는 칸 = 말풍선 몸통(둘러보기가 짚는 곳)
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(body.xMin * s, (NpcBubbleSize.y - body.yMax) * s) * K;
            area.offsetMax = -new Vector2((NpcBubbleSize.x - body.xMax) * s, body.yMin * s) * K;

            var text = bubble.Find("Text") as RectTransform;
            if (text != null)
            {
                // 글자 = 몸통 안쪽(가장자리 여백 시안 26px / 18px)
                const float padX = 26f, padY = 18f;
                text.anchorMin = Vector2.zero;
                text.anchorMax = Vector2.one;
                text.pivot = new Vector2(0.5f, 0.5f);
                text.offsetMin = new Vector2(body.xMin * s + padX, (NpcBubbleSize.y - body.yMax) * s + padY) * K;
                text.offsetMax = -new Vector2((NpcBubbleSize.x - body.xMax) * s + padX, body.yMin * s + padY) * K;
                var tmp = text.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.color = Ink;
                    tmp.alignment = TextAlignmentOptions.MidlineLeft;
                    tmp.enableAutoSizing = true;   // 긴 대사(3줄)도 말풍선 안에 들어가게
                    tmp.fontSizeMin = 14f;
                    tmp.fontSizeMax = 21f;
                }
            }
        }

        // ===== 팝업 =====

        // 나가기 / 현장모드 확인: 제목 + 문구(런타임에 바뀜) + 계속하기(no) / 확인(yes)
        static void StyleConfirmPopup(Transform modal)
        {
            var card = PopupCard(modal, 560f);
            if (card == null) return;
            // 문구는 누른 버튼(나가기/현장모드)에 따라 ConvenienceHudView가 바꾼다. 에디터에서는 나가기 문구로 보여 둔다
            var title = FindText(card, "Title");
            if (title != null)
            {
                title.text = "나가기";
                PopupText(title, 40f, 62f, 480f, 56f, 34f, Color.white, true, TextAlignmentOptions.Center);
            }
            var confirmLabel = FindText(card, "Confirm/Label");
            if (confirmLabel != null) confirmLabel.text = "훈련 종료";
            var message = FindText(card, "Message");
            if (message != null)
            {
                message.text = "훈련을 그만하고 시나리오 선택으로 돌아갈까요?";
                PopupText(message, 40f, 140f, 480f, 150f, 22f, new Color32(228, 237, 255, 255), false, TextAlignmentOptions.Center);
                message.lineSpacing = 12f;
                message.textWrappingMode = TextWrappingModes.Normal;
            }
            ChoiceButton(card, "Cancel", NoPath, 40f, 372f, 230f);
            ChoiceButton(card, "Confirm", YesPath, 290f, 372f, 230f);
        }

        // 팝업 판: Card(UIPopupMotion이 키우는 판)는 popup.png 몸통 크기가 되고, 그림은 빛 번짐까지 덮는 Frame 자식으로 붙인다.
        // height를 주면(그림 비율과 다른 납작한 판) 모서리는 그대로 두고 가운데만 늘리는 9-slice로 붙인다.
        static RectTransform PopupCard(Transform modal, float width, float height = 0f)
        {
            var card = modal != null ? modal.Find("Card") as RectTransform : null;
            if (card == null) return null;
            bool sliced = height > 0f;
            if (!sliced) height = width * PopupBody.height / PopupBody.width;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(width, height);
            if (card.TryGetComponent<Image>(out var plain)) plain.enabled = false;   // 옛 남색 둥근 판
            if (card.TryGetComponent<Outline>(out var outline)) Object.DestroyImmediate(outline);

            var old = card.Find("Frame");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var frame = NewRect("Frame", card);
            frame.SetAsFirstSibling();
            float s = width / PopupBody.width;
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(-PopupBody.xMin * s, -(PopupSize.y - PopupBody.yMax) * s);
            frame.offsetMax = new Vector2((PopupSize.x - PopupBody.xMax) * s, PopupBody.yMin * s);
            var image = frame.gameObject.AddComponent<Image>();
            image.sprite = Sprite(PopupPath);
            if (sliced)
            {
                // 테두리 = 그림 바깥 여백 + 모서리 반지름(약 110px). 보통 팝업(Simple)에는 영향 없음
                var popupSprite = SpriteWithBorder(PopupPath, new Vector4(PopupBody.xMin + PopupCorner,
                    PopupSize.y - PopupBody.yMax + PopupCorner, PopupSize.x - PopupBody.xMax + PopupCorner, PopupBody.yMin + PopupCorner));
                image.sprite = popupSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f / s;
            }
            image.color = new Color(1f, 1f, 1f, PopupAlpha);
            image.raycastTarget = true;   // 판을 눌러도 뒤로 지나가지 않게
            return card;
        }

        // 카드 안 버튼(이미 있는 Button + Label)을 yes/no 그림으로 바꾼다. x, y, width = 몸통(캔버스 단위, 카드 좌상단 기준)
        static void ChoiceButton(RectTransform card, string name, string path, float x, float y, float width)
        {
            var rt = card.Find(name) as RectTransform;
            if (rt == null) return;
            float s = width / ChoiceBody.width;
            Local(rt, x - ChoiceBody.xMin * s, y - ChoiceBody.yMin * s, ChoiceSize.x * s, ChoiceSize.y * s);
            if (rt.TryGetComponent<Image>(out var image))
            {
                image.sprite = Sprite(path);
                image.type = Image.Type.Simple;
                image.color = Color.white;
            }
            var label = rt.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = ChoiceText;
                label.fontSize = 22f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.offsetMin = new Vector2(ChoiceBody.xMin * s, (ChoiceSize.y - ChoiceBody.yMax) * s);
                lr.offsetMax = -new Vector2((ChoiceSize.x - ChoiceBody.xMax) * s, ChoiceBody.yMin * s);
            }
        }

        static void PopupText(TMP_Text text, float x, float y, float width, float height, float size, Color color, bool bold,
            TextAlignmentOptions alignment)
        {
            Local(text.rectTransform, x, y, width, height);
            text.fontSize = size;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = alignment;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        // 화면 둘러보기: 입력 막 + 어두운 판 4장(짚은 곳만 뚫림) + 빛 테두리 + 설명 창(Boarding.png, 다음=101 / 건너뛰기=100).
        //   자리는 런타임에 TrainingOnboardingView가 단계마다 맞춘다. 판들은 가운데 앵커여야 한다.
        static (GameObject root, RectTransform[] dims, RectTransform frame, RectTransform card, TMP_Text counter, TMP_Text body,
            Button next, Button skip) BuildOnboarding(Transform canvas)
        {
            var root = NewRect("Onboarding", canvas);
            Stretch(root);
            var completion = canvas.Find("CompletionRoot");
            root.SetSiblingIndex(completion != null ? completion.GetSiblingIndex() : canvas.childCount - 1);
            var blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.001f);   // 둘러보는 동안 뒤 버튼이 눌리지 않게

            string[] dimNames = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
            var dims = new RectTransform[4];
            for (int i = 0; i < dims.Length; i++)
            {
                dims[i] = NewRect(dimNames[i], root);
                dims[i].anchorMin = dims[i].anchorMax = new Vector2(0.5f, 0.5f);
                dims[i].pivot = new Vector2(0.5f, 0.5f);
                var dim = dims[i].gameObject.AddComponent<Image>();
                dim.color = new Color(0f, 0.01f, 0.05f, 0.72f);
            }

            var frame = Img("Frame", root, Der + "spot_frame.png");
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            var frameImage = frame.GetComponent<Image>();
            frameImage.sprite = SpriteWithBorder(Der + "spot_frame.png", new Vector4(40f, 40f, 40f, 40f));
            frameImage.type = Image.Type.Sliced;

            var card = Card(root.gameObject, TourCardW, TourCardH);
            float s = TourCardW / TourCardBody.width;
            var counter = Text("Counter", card, "1 / 9", 19f / K, Color.white, true);   // 위쪽 탭 안
            counter.alignment = TextAlignmentOptions.Center;
            Local(counter.rectTransform, (470f - TourCardBody.xMin) * s, (80f - TourCardBody.yMin) * s, 510f * s, 95f * s);
            var body = Text("Body", card, TourAccuracy[0], 23f / K, Color.white, true);
            body.alignment = TextAlignmentOptions.Center;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 10f;
            body.enableAutoSizing = true;   // 긴 문장(세 줄)도 버튼을 덮지 않게
            body.fontSizeMin = 19f;
            body.fontSizeMax = 23f;
            Local(body.rectTransform, 36f, 60f, TourCardW - 72f, 116f);
            var skip = TextButton("Skip", card, "건너뛰기", 0f, 0f, 10f, 10f, Color.white);
            var next = TextButton("Next", card, "다음", 0f, 0f, 10f, 10f, Color.white);
            TourCardFrame(card);
            TourButton(card, "Skip", TourSkipPath, TourSkipBody, 50f, 192f, 210f, 46f);
            TourButton(card, "Next", TourNextPath, TourNextBody, 300f, 192f, 210f, 46f);
            return (root.gameObject, dims, frame, card, counter, body, next, skip);
        }

        // 처음 들어왔을 때 환영 창: 어두운 막 + BoardingStart 그림(그림 비율 그대로) + 아래 빈 칸에 버튼 둘 + 그림 속 X 자리의 보이지 않는 버튼
        static (GameObject root, RectTransform card, Button tour, Button start, Button close) BuildWelcome(Transform canvas)
        {
            var overlay = Overlay("OnboardingWelcome", canvas);
            float s = WelcomeCardW / WelcomeBody.width;
            var card = NewRect("Card", overlay.transform);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(WelcomeCardW, WelcomeBody.height * s);
            var art = Img("Frame", card, WelcomePath);
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(-WelcomeBody.xMin * s, -(WelcomeSize.y - WelcomeBody.yMax) * s);
            art.offsetMax = new Vector2((WelcomeSize.x - WelcomeBody.xMax) * s, WelcomeBody.yMin * s);
            art.GetComponent<Image>().raycastTarget = true;   // 판을 눌러도 뒤로 지나가지 않게

            float y = (WelcomeButtonY - WelcomeBody.yMin) * s - WelcomeButtonH / 2f;
            float gap = WelcomeCardW - 2f * WelcomeButtonW - 2f * 70f;
            var start = TextButton("Start", card, "바로 시작하기", 0f, 0f, 10f, 10f, Color.white);
            var tour = TextButton("Tour", card, "화면 둘러보기", 0f, 0f, 10f, 10f, Color.white);
            TourButton(card, "Start", TourSkipPath, TourSkipBody, 70f, y, WelcomeButtonW, WelcomeButtonH);
            TourButton(card, "Tour", TourNextPath, TourNextBody, 70f + WelcomeButtonW + gap, y, WelcomeButtonW, WelcomeButtonH);

            // 그림 속 X: 보이지 않는 누름 칸(손가락에 맞게 그림보다 조금 넉넉히)
            var closeRt = NewRect("Close", card);
            Local(closeRt, (WelcomeClose.x - WelcomeBody.xMin) * s - WelcomeCloseHit / 2f,
                (WelcomeClose.y - WelcomeBody.yMin) * s - WelcomeCloseHit / 2f, WelcomeCloseHit, WelcomeCloseHit);
            var closeImage = closeRt.gameObject.AddComponent<Image>();
            closeImage.color = new Color(1f, 1f, 1f, 0f);
            var close = closeRt.gameObject.AddComponent<Button>();
            close.targetGraphic = closeImage;
            close.transition = Selectable.Transition.None;
            return (overlay, card, tour, start, close);
        }

        // 설명 창 판: 가로는 그림 비율 그대로라 탭이 늘어나지 않고, 세로만 9-slice로 줄인다(탭은 위쪽 테두리 칸에 들어 있다)
        static void TourCardFrame(RectTransform card)
        {
            if (card.TryGetComponent<Image>(out var plain)) plain.enabled = false;   // 옛 남색 둥근 판
            if (card.TryGetComponent<Outline>(out var outline)) Object.DestroyImmediate(outline);
            float s = TourCardW / TourCardBody.width;
            var frame = NewRect("Frame", card);
            frame.SetAsFirstSibling();
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(-TourCardBody.xMin * s, -(TourCardSize.y - TourCardBody.yMax) * s);
            frame.offsetMax = new Vector2((TourCardSize.x - TourCardBody.xMax) * s, TourCardBody.yMin * s);
            var image = frame.gameObject.AddComponent<Image>();
            image.sprite = SpriteWithBorder(TourCardPath, new Vector4(TourCardBody.xMin + TourCardCorner,
                TourCardSize.y - TourCardBody.yMax + TourCardCorner, TourCardSize.x - TourCardBody.xMax + TourCardCorner,
                TourCardPanelTop + TourCardCorner));
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / s;
            image.color = Color.white;   // 다른 팝업과 달리 불투명: 패널 위에 자주 겹쳐서 뒤 글씨가 비치면 설명이 읽기 어렵다
            image.raycastTarget = true;   // 판을 눌러도 뒤로 지나가지 않게
        }

        // 둘러보기 버튼: 그림 속 몸통(body, px)이 카드 안 상자(x, y, 너비, 높이)에 꼭 맞게 놓고, 흰 글자는 몸통 가운데에
        static void TourButton(RectTransform card, string name, string path, Rect body, float x, float y, float width, float height)
        {
            var rt = card.Find(name) as RectTransform;
            if (rt == null) return;
            float sx = width / body.width, sy = height / body.height;
            Local(rt, x - body.xMin * sx, y - body.yMin * sy, TourButtonSize.x * sx, TourButtonSize.y * sy);
            if (rt.TryGetComponent<Image>(out var image))
            {
                image.sprite = Sprite(path);
                image.type = Image.Type.Simple;
                image.color = Color.white;
            }
            var label = rt.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = Color.white;
                label.fontSize = 22f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.offsetMin = new Vector2(body.xMin * sx, (TourButtonSize.y - body.yMax) * sy);
                lr.offsetMax = -new Vector2((TourButtonSize.x - body.xMax) * sx, body.yMin * sy);
            }
        }

        // 키보드로 말하기: 다른 팝업과 같은 popup.png 판 + 보내기(yes)/닫기(no).
        //   화면 자판 위(태블릿에서 화면 위쪽 약 45%)에 들어가야 해서 판을 납작하게(620x340) 쓴다. 그림 비율과 달라 이 창만 9-slice.
        const float KeyboardCardW = 620f, KeyboardCardH = 340f;

        static (GameObject root, TMP_InputField field, Button send, Button close) BuildKeyboardModal(Transform canvas)
        {
            var overlay = Overlay("KeyboardModal", canvas);
            var card = Card(overlay, KeyboardCardW, KeyboardCardH);
            var title = Text("Title", card, "키보드로 말하기", 30f / K, Color.white, true);
            title.alignment = TextAlignmentOptions.Center;
            Local(title.rectTransform, 30f, 30f, KeyboardCardW - 60f, 46f);
            var hint = Text("Hint", card, "다 쓰면 ‘보내기’나 자판의 ‘완료’를 누르세요.", 18f / K, new Color32(228, 237, 255, 255), false);
            hint.alignment = TextAlignmentOptions.Center;
            Local(hint.rectTransform, 30f, 80f, KeyboardCardW - 60f, 30f);

            var fieldRect = NewRect("Input", card);
            Local(fieldRect, 50f, 124f, KeyboardCardW - 100f, 70f);
            var fieldImage = fieldRect.gameObject.AddComponent<Image>();
            fieldImage.sprite = Rounded();
            fieldImage.type = Image.Type.Sliced;
            fieldImage.color = new Color32(238, 244, 255, 255);
            var area = NewRect("Text Area", fieldRect);
            Stretch(area);
            area.offsetMin = new Vector2(22f, 8f);
            area.offsetMax = new Vector2(-22f, -8f);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = Text("Placeholder", area, "예: 물 주세요", 24f, new Color32(130, 142, 170, 255), false);
            Stretch(placeholder.rectTransform);
            placeholder.fontStyle = FontStyles.Italic;
            var text = Text("Text", area, string.Empty, 24f, Ink, true);
            Stretch(text.rectTransform);
            var field = fieldRect.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.fontAsset = _font;
            field.pointSize = 24f;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 40;
            field.targetGraphic = fieldImage;

            var close = TextButton("Close", card, "닫기", 0f, 0f, 10f, 10f, Color.white);
            var send = TextButton("Send", card, "보내기", 0f, 0f, 10f, 10f, Color.white);
            PopupCard(overlay.transform, KeyboardCardW, KeyboardCardH);
            ChoiceButton(card, "Close", NoPath, 70f, 238f, 220f);
            ChoiceButton(card, "Send", YesPath, 330f, 238f, 220f);
            return (overlay, field, send, close);
        }

        // ===== 만들기 도우미 =====

        static RectTransform Panel(string name, RectTransform parent, Anchor anchor, Rect body, string framePath, Rect spriteBody, Vector2 spriteSize)
        {
            var panel = NewRect(name, parent);
            Place(panel, anchor, body);
            AddFrame(panel, framePath, body, spriteBody, spriteSize);
            return panel;
        }

        // 유리 판 그림을 본체(panel 사각형) 바깥 빛 번짐까지 덮도록 붙인다. 9-slice라 시안 비율이 조금 달라도 모서리가 그대로다.
        static void AddFrame(RectTransform panel, string path, Rect body, Rect spriteBody, Vector2 spriteSize, bool sliced = true)
        {
            var frame = NewRect("Frame", panel);
            frame.SetAsFirstSibling();
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            float sx = body.width * K / spriteBody.width;
            float sy = body.height * K / spriteBody.height;
            float s = sliced ? Mathf.Min(sx, sy) : sx;
            frame.offsetMin = new Vector2(-spriteBody.x * sx, -(spriteSize.y - spriteBody.yMax) * sy);
            frame.offsetMax = new Vector2((spriteSize.x - spriteBody.xMax) * sx, spriteBody.y * sy);
            var image = frame.gameObject.AddComponent<Image>();
            image.sprite = Sprite(path, sliced ? 46 : 0);
            image.raycastTarget = false;
            if (sliced)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f / s;
            }
        }

        // 버튼 그림 몸통이 시안 몸통에 맞게(가로 기준, 비율 유지) 놓는다
        static Button SpriteButton(string name, RectTransform parent, Anchor anchor, string path, Rect body, bool fitWidth)
        {
            var sprite = Sprite(path);
            var bodyPx = SpriteBodies.TryGetValue(Path.GetFileName(path), out var known) ? known : new Rect(0f, 0f, sprite.rect.width, sprite.rect.height);
            float s = fitWidth ? body.width / bodyPx.width : body.height / bodyPx.height;
            var outer = Rect.MinMaxRect(body.xMin - bodyPx.xMin * s, body.yMin - bodyPx.yMin * s,
                body.xMin + (sprite.rect.width - bodyPx.xMin) * s, body.yMin + (sprite.rect.height - bodyPx.yMin) * s);
            var rt = NewRect(name, parent);
            Place(rt, anchor, outer);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        static Button PanelSpriteButton(string name, RectTransform panel, Rect panelBody, string path, Rect body, Rect spriteBody, Vector2 spriteSize)
        {
            var rt = Img(name, panel, path);
            var image = rt.GetComponent<Image>();
            image.raycastTarget = true;
            FitBodyWidth(rt, panelBody, body, spriteBody, spriteSize);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        // 그림 속 몸통(spriteBody, px)이 시안 body 상자에 꼭 맞도록 rt(부모 = parentBox 상자) 자리를 정한다(가로세로 따로)
        static void FitBody(RectTransform rt, Rect parentBox, Rect body, Rect spriteBody, Vector2 spriteSize)
        {
            float sx = body.width / spriteBody.width, sy = body.height / spriteBody.height;
            var outer = Rect.MinMaxRect(body.xMin - spriteBody.x * sx, body.yMin - spriteBody.y * sy,
                body.xMin + (spriteSize.x - spriteBody.x) * sx, body.yMin + (spriteSize.y - spriteBody.y) * sy);
            Child(rt, parentBox, outer);
        }

        // 가로 기준으로 비율을 지키고 세로는 body 가운데에 맞춘다
        static void FitBodyWidth(RectTransform rt, Rect parentBox, Rect body, Rect spriteBody, Vector2 spriteSize)
        {
            float s = body.width / spriteBody.width;
            float top = body.center.y - spriteBody.height * s / 2f;
            var outer = Rect.MinMaxRect(body.xMin - spriteBody.x * s, top - spriteBody.y * s,
                body.xMin + (spriteSize.x - spriteBody.x) * s, top + (spriteSize.y - spriteBody.y) * s);
            Child(rt, parentBox, outer);
        }

        static RectTransform Img(string name, Transform parent, string path, bool preserveAspect = false)
        {
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Sprite(path);
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return rt;
        }

        static TMP_Text Text(string name, Transform parent, string value, float size, Color color, bool bold)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size * K;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static GameObject Overlay(string name, Transform canvas)
        {
            var root = NewRect(name, canvas);
            Stretch(root);
            var image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0.01f, 0.05f, 0.7f);
            var completion = canvas.Find("CompletionRoot");
            root.SetSiblingIndex(completion != null ? completion.GetSiblingIndex() : canvas.childCount - 1);
            return root.gameObject;
        }

        static RectTransform Card(GameObject overlay, float width, float height)
        {
            var card = NewRect("Card", overlay.transform);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(width, height);
            var image = card.gameObject.AddComponent<Image>();
            image.sprite = Rounded();
            image.type = Image.Type.Sliced;
            image.color = CardNavy;
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(90, 200, 255, 170);
            outline.effectDistance = new Vector2(2f, -2f);
            return card;
        }

        static Button TextButton(string name, Transform parent, string label, float x, float y, float width, float height, Color color)
        {
            var rt = NewRect(name, parent);
            Local(rt, x, y, width, height);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Rounded();
            image.type = Image.Type.Sliced;
            image.color = color;
            var text = Text("Label", rt, label, 22f / K, Color.white, true);
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        // 팝업 카드 안 좌표(캔버스 단위, 좌상단 기준)
        static void Local(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        // 시안 상자를 화면 가장자리/가운데에 붙인다
        static void Place(RectTransform rt, Anchor anchor, Rect box)
        {
            Vector2 a;
            Vector2 pos;
            switch (anchor)
            {
                case Anchor.TL: a = new Vector2(0f, 1f); pos = new Vector2(box.xMin, -box.yMin); break;
                case Anchor.TR: a = new Vector2(1f, 1f); pos = new Vector2(box.xMax - MockW, -box.yMin); break;
                case Anchor.TC: a = new Vector2(0.5f, 1f); pos = new Vector2(box.center.x - MockW / 2f, -box.yMin); break;
                case Anchor.BL: a = new Vector2(0f, 0f); pos = new Vector2(box.xMin, MockH - box.yMax); break;
                case Anchor.BR: a = new Vector2(1f, 0f); pos = new Vector2(box.xMax - MockW, MockH - box.yMax); break;
                default: a = new Vector2(0.5f, 0f); pos = new Vector2(box.center.x - MockW / 2f, MockH - box.yMax); break;
            }
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = a;
            rt.anchoredPosition = pos * K;
            rt.sizeDelta = box.size * K;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        // 부모 상자(parentBox, 시안 px) 안의 자식 상자를 좌상단 기준으로 놓는다
        static RectTransform Child(RectTransform rt, Rect parentBox, Rect box)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(box.xMin - parentBox.xMin, -(box.yMin - parentBox.yMin)) * K;
            rt.sizeDelta = box.size * K;
            return rt;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        static TMP_Text FindText(Transform root, string path) => root.Find(path)?.GetComponent<TMP_Text>();

        static Sprite Rounded() => AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath)
                                   ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // 스프라이트 가져오기 설정을 맞춘다(Sprite/Single, 밉맵 없음, 압축 없음). border > 0이면 9-slice 테두리도 넣는다.
        static Sprite Sprite(string path, int border = 0)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer != null)
            {
                var wantBorder = new Vector4(border, border, border, border);
                bool dirty = importer.textureType != TextureImporterType.Sprite
                             || importer.spriteImportMode != SpriteImportMode.Single
                             || importer.mipmapEnabled
                             || importer.textureCompression != TextureImporterCompression.Uncompressed
                             || (border > 0 && importer.spriteBorder != wantBorder);
                if (dirty)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100f;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 4096;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    if (border > 0) importer.spriteBorder = wantBorder;
                    importer.SaveAndReimport();
                }
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[ConvenienceTrainingConLayout] Sprite 없음: {path}");
            return sprite;
        }

        static Sprite SpriteWithBorder(string path, Vector4 border)
        {
            var sprite = Sprite(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sprite;
        }

        static void SetObjects(SerializedProperty property, IList<Object> values)
        {
            property.arraySize = values?.Count ?? 0;
            for (int i = 0; values != null && i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void SetObjects<T>(SerializedProperty property, T[] values) where T : Object =>
            SetObjects(property, values?.Cast<Object>().ToList());

        static void SetStrings(SerializedProperty property, string[] values)
        {
            property.arraySize = values?.Length ?? 0;
            for (int i = 0; values != null && i < values.Length; i++)
                property.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        // ===== 확인용 캡처 (씬은 저장하지 않는다) =====

        [MenuItem("Artti/Debug/Capture TrainingConvenience - Tab + Phone + 4x3")]
        static void CaptureMenu() => Capture(null);

        [MenuItem("Artti/Debug/Capture TrainingConvenience Popups - Tab")]
        static void CapturePopupsMenu()
        {
            Capture("KeyboardModal");
            Capture("KeyboardModal", liftForKeyboard: true);
            Capture("SettingsModal");
        }

        // 화면 둘러보기 단계별 캡처(태블릿). 캔버스 크기가 기기 해상도로 바뀐 뒤 자리를 맞춰야 해서 같은 장면을 두 번 찍는다.
        [MenuItem("Artti/Debug/Capture TrainingConvenience Onboarding - Tab")]
        static void CaptureOnboardingMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("[ConvenienceTrainingConLayout] 저장 안 한 씬 변경이 있어 캡처하지 않는다.");
                return;
            }
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            try
            {
                {
                    var scene = EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
                    var canvas = scene.GetRootGameObjects().First(g => g.name == "[Canvas]");
                    var welcome = canvas.transform.Find("OnboardingWelcome");
                    if (welcome != null)
                    {
                        RenderClerkCameras();
                        string file = Path.Combine(outDir, "tab_TrainingCon_Welcome.png");
                        welcome.gameObject.SetActive(true);
                        DeviceLayoutCapture.CaptureActiveScene(2560, 1600, file);
                        DeviceLayoutCapture.CaptureActiveScene(2560, 1600, file);
                    }
                }
                for (int i = 0; i < TourTargets.Length; i++)
                {
                    var scene = EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
                    var canvas = scene.GetRootGameObjects().First(g => g.name == "[Canvas]");
                    var view = canvas.GetComponent<TrainingOnboardingView>();
                    if (view == null) return;
                    RenderClerkCameras();
                    string file = Path.Combine(outDir, $"tab_TrainingCon_Onboarding_{i + 1}.png");
                    DeviceLayoutCapture.CaptureActiveScene(2560, 1600, file);   // 캔버스를 태블릿 크기로 맞춘다
                    view.PreviewStep(i, false);
                    DeviceLayoutCapture.CaptureActiveScene(2560, 1600, file);
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
            }
            Debug.Log($"[ConvenienceTrainingConLayout] 둘러보기 캡처 완료 -> {outDir}");
        }

        // 미션 진행 배를 단계(1~5)마다 찍는다(태블릿). HUD의 ApplyObjective를 에디터에서 불러 그 단계 모습으로 맞춘다.
        [MenuItem("Artti/Debug/Capture TrainingConvenience Mission Steps - Tab")]
        static void CaptureMissionStepsMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("[ConvenienceTrainingConLayout] 저장 안 한 씬 변경이 있어 캡처하지 않는다.");
                return;
            }
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            var apply = typeof(ConvenienceHudView).GetMethod("ApplyObjective",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            string[] labels = { "점원에게 인사하기", "원하는 상품 고르기", "봉투 여부 답하기", "결제 방법 말하기", "감사 인사하기" };
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    var scene = EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
                    var hud = scene.GetRootGameObjects().First(g => g.name == "[Canvas]").GetComponent<ConvenienceHudView>();
                    // 에디터에서는 Awake가 안 돌아 글자 제자리(_labelHome)가 비어 있다 -> 지금 자리로 채운다
                    var so = new SerializedObject(hud);
                    if (so.FindProperty("stepLabel").objectReferenceValue is TMP_Text stepLabel)
                        typeof(ConvenienceHudView).GetField("_labelHome", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            ?.SetValue(hud, stepLabel.rectTransform.anchoredPosition);
                    apply?.Invoke(hud, new object[] { i, labels[i] });
                    RenderClerkCameras();
                    DeviceLayoutCapture.CaptureActiveScene(2560, 1600, Path.Combine(outDir, $"tab_TrainingCon_Mission_{i + 1}.png"));
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
            }
            Debug.Log($"[ConvenienceTrainingConLayout] 미션 단계 캡처 완료 -> {outDir}");
        }

        // liftForKeyboard: 화면 자판이 올라온 상태(UIKeyboardLift가 판을 화면 위로 올린 위치)를 흉내 낸다. 에디터에는 자판이 없다.
        static void Capture(string openPopup, bool liftForKeyboard = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePaths.TrainingConvenience)
            {
                if (scene.isDirty)
                {
                    Debug.LogWarning("[ConvenienceTrainingConLayout] 열린 씬에 저장 안 한 변경이 있어 캡처하지 않는다.");
                    return;
                }
                scene = EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
            }
            else if (scene.isDirty)
            {
                Debug.LogWarning("[ConvenienceTrainingConLayout] 편의점 씬에 저장 안 한 변경이 있어 캡처하지 않는다.");
                return;
            }

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            var sizes = openPopup == null
                ? new[] { (2560, 1600, "tab"), (2400, 1080, "phone"), (1920, 1440, "43") }
                : new[] { (2560, 1600, "tab") };
            try
            {
                foreach (var (w, h, tag) in sizes)
                {
                    // 매번 디스크 상태에서 다시 연다(캡처가 캔버스 설정을 바꾸기 때문)
                    scene = EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
                    if (openPopup != null)
                    {
                        var canvas = scene.GetRootGameObjects().First(g => g.name == "[Canvas]").transform;
                        var popup = canvas.Find(openPopup);
                        popup?.gameObject.SetActive(true);
                        if (liftForKeyboard && popup?.Find("Card") is RectTransform card)
                        {
                            Canvas.ForceUpdateCanvases();
                            // UIKeyboardLift와 같은 식: 판 위 가장자리를 화면 위에서 24 아래로 (캡처 해상도의 캔버스 높이 기준)
                            var scaler = canvas.GetComponent<CanvasScaler>();
                            float canvasH = h / Mathf.Min(w / scaler.referenceResolution.x, h / scaler.referenceResolution.y);
                            card.anchoredPosition = new Vector2(card.anchoredPosition.x, canvasH / 2f - 24f - card.rect.height / 2f);
                        }
                    }
                    RenderClerkCameras();
                    string name = openPopup == null ? $"{tag}_TrainingCon.png"
                        : $"{tag}_TrainingCon_{openPopup}{(liftForKeyboard ? "_Lifted" : "")}.png";
                    DeviceLayoutCapture.CaptureActiveScene(w, h, Path.Combine(outDir, name));
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
            }
            Debug.Log($"[ConvenienceTrainingConLayout] 캡처 완료 -> {outDir}");
        }

        // 점원 3D는 RenderTexture 카메라라 화면 캡처 전에 한 번 그려 둔다
        static void RenderClerkCameras()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!cam.enabled || cam.targetTexture == null) continue;
                var request = new RenderPipeline.StandardRequest { destination = cam.targetTexture };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else cam.Render();
            }
        }
    }
}
