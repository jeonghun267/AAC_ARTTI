using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.Common;
using Artti.UI;
using static Artti.Editor.ProfileAvatarStepBuilder;

namespace Artti.Editor
{
    // 모드별 리포트(정확도 / 대화) 씬 빌더가 같이 쓰는 틀. 시안은 둘 다 1672x941(REPORT 조각)이고 같은 자리에
    // 배경 + 왼쪽 유리 판·모드 메뉴 + 머리말·기간 탭·날짜 + 스탯 카드 4장 + 막대 항목·종합 평가 판 + 오른쪽 판 + 아래 판 + 다시 하기가 온다.
    // - 배경(Splash/42.png)이 화면을 꽉 채우고(CoverImageFitter, 비율 그대로), UI는 배경을 따라 커지는 무대(Stage, 좌표 = 시안 px,
    //   위에서부터 y)에 놓는다. 잘리는 화면(태블릿 좌우, 폰 위아래)에서는 무대만 같은 비율로 줄여 SafeRect가 다 보이게 한다.
    // - 조각 그림은 Art/UI/Home/REPORT, 시안에 있지만 조각이 없는 것은 derived/(Art_Source/ui_report_conv/make_derived.py).
    // - 시안과 비율이 다른 판은 9-slice로 늘린다(모서리 윤기는 늘어나지 않는 쪽에 둔다).
    public static class ModeReportSceneKit
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        public static readonly Vector2 StageSize = new Vector2(1672f, 941f);
        static readonly Rect SafeRect = Rect.MinMaxRect(12f, 4f, 1640f, 924f);
        const string BgPath = "Assets/_Project/Art/UI/Home/Splash/42.png";
        public const string ArtDir = "Assets/_Project/Art/UI/Home/REPORT/";
        public const string ArtDer = ArtDir + "derived/";

        public static readonly Vector2 S2172 = new Vector2(2172f, 724f);
        public static readonly Vector2 S1881 = new Vector2(1881f, 836f);
        public static readonly Vector2 S1774 = new Vector2(1774f, 887f);
        public static readonly Vector2 Icon256 = new Vector2(256f, 256f);

        public static readonly Color Ink = new Color32(240, 245, 255, 255);
        public static readonly Color SubInk = new Color32(214, 224, 248, 255);
        public static readonly Color Faint = new Color32(170, 188, 228, 255);
        public static readonly Color Cyan = new Color32(84, 228, 255, 255);
        public static readonly Color LineColor = new Color(0.47f, 0.66f, 1f, 0.28f);
        public static readonly Color HeaderIconColor = new Color32(120, 205, 255, 255);

        // 판 자리 (시안 px)
        public static readonly Rect AnalysisBox = Rect.MinMaxRect(270, 305, 1075, 640);
        public static readonly Rect RightBox = Rect.MinMaxRect(1088, 305, 1625, 652);
        public static readonly Rect BottomBox = Rect.MinMaxRect(268, 656, 1625, 830);

        // 스탯 카드: 판 그림(2~5), 본체(px), 시안 자리
        static readonly (string file, Rect body, Rect box)[] CardFrames =
        {
            ("2", Rect.MinMaxRect(209, 161, 1679, 677), Rect.MinMaxRect(290, 150, 618, 293)),
            ("3", Rect.MinMaxRect(136, 153, 1749, 672), Rect.MinMaxRect(627, 150, 945, 293)),
            ("4", Rect.MinMaxRect(161, 166, 1722, 654), Rect.MinMaxRect(957, 150, 1278, 293)),
            ("5", Rect.MinMaxRect(172, 161, 1712, 673), Rect.MinMaxRect(1290, 150, 1620, 293)),
        };

        const float BarX0 = 459f, BarX1 = 675f, BarH = 18f, BarValueRight = 727f;
        const float RowIconCx = 332f, RowIconH = 26f, RowNameX = 360f;

        // 레이더 격자(21.png 1254px, 축 이름을 지운 derived/radar_grid.png): 격자 가운데(620,648) 반지름 340.
        //   축 이름은 TMP로 원래 그림 속 이름 자리에(배율 0.255), 격자는 이름과 꼭짓점이 붙지 않게 조금 작게(0.235)
        const float RadarScale = 0.255f;
        const float RadarGridScale = 0.22f;
        static readonly Vector2 RadarCenter = new Vector2(897f, 487f);
        static readonly Vector2 RadarGridCenterPx = new Vector2(620f, 648f);
        const float RadarGridRadiusPx = 340f;
        static readonly Vector2[] RadarLabelPx =
        {
            new Vector2(618, 232), new Vector2(1103.5f, 528.5f), new Vector2(914, 967), new Vector2(304, 971.5f), new Vector2(159, 531.5f),
        };

        public struct CardSpec
        {
            public string icon, label, value, chip, note;
            public bool chipDown;
            public CardSpec(string icon, string label, string value, string chip, string note, bool chipDown = false)
            {
                this.icon = icon; this.label = label; this.value = value; this.chip = chip; this.note = note; this.chipDown = chipDown;
            }
        }

        public struct RowSpec
        {
            public string icon;       // 아이콘 그림 경로
            public Vector2 png;       // 그림 크기
            public float bodyH;       // 그림 속 아이콘 높이(px)
            public string label, fill;
            public float sample;      // 씬 예시 값 0~1
            public RowSpec(string icon, Vector2 png, float bodyH, string label, string fill, float sample)
            {
                this.icon = icon; this.png = png; this.bodyH = bodyH; this.label = label; this.fill = fill; this.sample = sample;
            }
        }

        public class FrameSpec
        {
            public TrainingMode mode;
            public string headerIcon, title, subtitle;
            public CardSpec[] cards;
            public string analysisTitle;
            public RowSpec[] rows;
            public float[] rowY;
            public string[] radarNames;
            public string[] radarSamples;
            public string retryLabel;
        }

        public class FrameRefs
        {
            public GameObject canvasGo;
            public RectTransform stage;
            public TMP_FontAsset font;
            public Button back, otherMode, retry;
            public Button[] tabButtons;
            public TMP_Text[] tabLabels;
            public RectTransform highlight;
            public TMP_Text date;
            public TMP_Text[] statValues, statNotes, chipTexts;
            public GameObject[] chips;
            public Image[] chipArrows;
            public RectTransform[] fills;
            public TMP_Text[] barValues, radarValues;
            public UIRadarChart radar;
            public Image badge;
            public TMP_Text gradeText;
        }

        // ===== 씬 열기·등록·저장 =====

        public static void OpenOrCreate(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                SceneBuilderUtils.OpenScene(scenePath);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        public static void RegisterInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == scenePath)) return;
            EditorBuildSettings.scenes = scenes.Concat(new[] { new EditorBuildSettingsScene(scenePath, true) }).ToArray();
        }

        public static void Finish(GameObject canvasGo, string scenePath)
        {
            RegisterInBuildSettings(scenePath);
            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            SceneBuilderUtils.SaveActiveSceneAgainAfterEditorTicks();   // 배경 맞춤이 다음 틱에 씬을 수정됨으로 만들어서
        }

        // ===== 공통 틀 =====

        public static FrameRefs BuildFrame(FrameSpec spec)
        {
            var r = new FrameRefs();
            SceneBuilderUtils.ClearRootObjects();
            SceneBuilderUtils.CreateEventSystem();
            if (spec.mode == TrainingMode.Accuracy) SceneBuilderUtils.EnsureAudioListener();   // 모범 발음 듣기
            r.canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution, CanvasScaler.ScreenMatchMode.Expand);
            r.font = SceneBuilderUtils.GetKoreanFont();
            var font = r.font;
            var o = Vector2.zero;   // 무대 좌상단 = 시안 (0,0)

            // 배경 + 어둡게 + 무대
            var bg = ChildRect("Background", r.canvasGo.transform);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = LoadSprite(BgPath, 2048);
            bgImg.raycastTarget = false;
            var fitter = bg.gameObject.AddComponent<CoverImageFitter>();
            var dim = ChildRect("Dim", bg);
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0.01f, 0.03f, 0.12f, 0.38f);
            dimImg.raycastTarget = false;
            var stage = ChildRect("Stage", bg);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = StageSize;
            fitter.Setup(StageSize.x / StageSize.y, 1f, new Vector2(0.5f, 0.5f));
            fitter.SetFollower(stage, StageSize);
            fitter.SetSafeRect(SafeRect, 16f);
            r.stage = stage;

            // 왼쪽 유리 판 + 뒤로 + 모드 메뉴(지금 모드 = 켜진 판, 다른 모드 = 누르면 그 리포트로)
            ImageByBody("Sidebar", stage, o, ArtDir + "22.png", new Vector2(941f, 1672f), Rect.MinMaxRect(252, 4, 691, 1666),
                new Vector2(138.5f, 463.5f), 917f, 2048);
            var back = HitBox("BackButton", stage, o, Rect.MinMaxRect(58, 41, 114, 97));
            Img("Icon", back, new Vector2(58, 41), ArtDer + "icon_back.png", Rect.MinMaxRect(55, 38, 117, 100), 256);
            r.back = back.GetComponent<Button>();
            bool accuracy = spec.mode == TrainingMode.Accuracy;
            var accBtn = NavButton("AccuracyModeButton", stage, new Vector2(136f, 290f), accuracy, "icon_target", "정확도 모드", font);
            var convBtn = NavButton("ConversationModeButton", stage, new Vector2(136f, 378f), !accuracy, "icon_chat", "대화 모드", font);
            r.otherMode = accuracy ? convBtn : accBtn;

            // 머리말: 아이콘 + 제목 + 부제 + 기간 탭 + 날짜
            var headerIcon = Img("HeaderIcon", stage, o, ArtDer + spec.headerIcon + ".png", Rect.MinMaxRect(294, 38, 399, 143), 256);
            headerIcon.color = HeaderIconColor;
            Label("Title", stage, o, spec.title, font, 38f, Ink, true, 408, 800, 79, TextAlignmentOptions.MidlineLeft);
            Label("Subtitle", stage, o, spec.subtitle, font, 20f, SubInk, false, 408, 900, 122, TextAlignmentOptions.MidlineLeft);
            BuildTabs(stage, font, r);
            r.date = Label("Date", stage, o, "2026년 10월 1일 (목)", font, 19f, SubInk, false, 1400, 1618, 88, TextAlignmentOptions.MidlineRight);

            BuildCards(stage, spec.cards, font, r);
            BuildAnalysis(stage, spec, font, r);

            // 다시 하기: 13.png에서 글자를 지운 버튼(화살표는 그림) + TMP
            r.retry = HitBoxByBody("RetryButton", stage, o, ArtDer + "btn_retry_blank.png", S2172, Rect.MinMaxRect(224, 213, 1946, 553),
                new Vector2(919f, 873f), 340f);
            Label("Label", r.retry.transform, new Vector2(749f, 839.4f), spec.retryLabel, font, 24f, Color.white, true,
                800, 1016, 873, TextAlignmentOptions.Center);
            return r;
        }

        // 왼쪽 메뉴 판: 켜짐 = nav_active(11에서 글자 지움), 꺼짐 = nav_normal(10에서 글자 지움, 누르는 버튼) + 아이콘 + 이름
        static Button NavButton(string name, RectTransform stage, Vector2 center, bool active, string icon, string label, TMP_FontAsset font)
        {
            const float bodyW = 196f;
            var body = active ? Rect.MinMaxRect(418, 143, 1755, 575) : Rect.MinMaxRect(498, 161, 1678, 556);
            string plate = ArtDer + (active ? "nav_active.png" : "nav_normal.png");
            float bodyH = bodyW * body.height / body.width;
            var box = Rect.MinMaxRect(center.x - bodyW / 2f, center.y - bodyH / 2f, center.x + bodyW / 2f, center.y + bodyH / 2f);
            RectTransform root;
            Button button = null;
            if (active) root = Box(name, stage, Vector2.zero, box);
            else
            {
                root = HitBox(name, stage, Vector2.zero, box);
                button = root.GetComponent<Button>();
            }
            var ro = new Vector2(box.xMin, box.yMin);
            var plateImg = ImageByBody("Plate", root, ro, plate, S2172, body, center, bodyH, 4096);
            if (!active) plateImg.color = new Color(0.72f, 0.78f, 1f, 0.72f);   // 다른 모드 = 조금 어둡고 비치게(지금 모드가 눈에 띄게)
            var ic = Img("Icon", root, ro, ArtDer + icon + ".png", Rect.MinMaxRect(box.xMin + 8, center.y - 24, box.xMin + 56, center.y + 24), 256);
            ic.color = active ? Color.white : new Color(0.85f, 0.9f, 1f, 0.85f);
            Label("Label", root, ro, label, font, 21f, active ? Color.white : SubInk, active, box.xMin + 60, box.xMax - 8, center.y,
                TextAlignmentOptions.MidlineLeft);
            return button;
        }

        // 기간 탭: 막대(tab_bar = 알약 모양, 선택 표시 1.png와 같은 곡률)를 정확히 3등분하고, 선택 표시는 한 칸 크기로
        static void BuildTabs(RectTransform stage, TMP_FontAsset font, FrameRefs r)
        {
            var bar = Rect.MinMaxRect(926, 62, 1375, 112);
            float cell = bar.width / 3f;
            var tabBar = Box("Tabs", stage, Vector2.zero, bar);
            var to = new Vector2(bar.xMin, bar.yMin);
            Sliced("Bar", tabBar, to, ArtDer + "tab_bar.png", new Vector2(948f, 148f), Rect.MinMaxRect(24, 24, 924, 124),
                new Vector4(74, 0, 74, 0), bar, bar.height / 100f, 1024);
            for (int i = 1; i < 3; i++)
            {
                float dx = bar.xMin + cell * i;
                Line("Divider", tabBar, to, Rect.MinMaxRect(dx - 0.75f, 74, dx + 0.75f, 100), LineColor);
            }
            r.highlight = Sliced("Highlight", tabBar, to, ArtDir + "1.png", S2172, Rect.MinMaxRect(273, 114, 1900, 596),
                new Vector4(514, 0, 513, 0), Rect.MinMaxRect(bar.xMin, bar.yMin, bar.xMin + cell, bar.yMax), bar.height / 482f, 4096).rectTransform;
            string[] names = { "오늘", "이번 주", "전체" };
            r.tabButtons = new Button[3];
            r.tabLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                float x0 = bar.xMin + cell * i, x1 = x0 + cell;
                var tb = HitBox("Tab" + i, tabBar, to, Rect.MinMaxRect(x0, bar.yMin, x1, bar.yMax));
                r.tabButtons[i] = tb.GetComponent<Button>();
                r.tabLabels[i] = Label("Label", tb, new Vector2(x0, bar.yMin), names[i], font, 19f, i == 0 ? Color.white : Faint,
                    i == 0, x0, x1, bar.center.y, TextAlignmentOptions.Center);
            }
        }

        static void BuildCards(RectTransform stage, CardSpec[] cards, TMP_FontAsset font, FrameRefs r)
        {
            var o = Vector2.zero;
            int n = cards.Length;
            r.statValues = new TMP_Text[n];
            r.statNotes = new TMP_Text[n];
            r.chips = new GameObject[n];
            r.chipArrows = new Image[n];
            r.chipTexts = new TMP_Text[n];
            for (int i = 0; i < n; i++)
            {
                var c = cards[i];
                var f = CardFrames[i];
                float s = f.box.width / f.body.width;
                var border = new Vector4(f.body.xMin + 0.22f * f.body.width, S1881.y - f.body.yMax + 0.25f * f.body.height,
                    S1881.x - f.body.xMax + 0.10f * f.body.width, f.body.yMin + 0.32f * f.body.height);
                var card = Box("Card" + i, stage, o, f.box);
                var co = new Vector2(f.box.xMin, f.box.yMin);
                Sliced("Frame", card, co, ArtDir + f.file + ".png", S1881, f.body, border, f.box, s, 2048);
                // 아이콘 원: 카드 안쪽 유리판의 비스듬히 잘린 왼쪽 위 모서리에 걸치지 않게 시안보다 오른쪽·아래로
                float x0 = f.box.xMin, cy = 210f, cx = x0 + 70f;
                Img("IconRing", card, co, ArtDer + "icon_ring.png", Rect.MinMaxRect(cx - 40, cy - 40, cx + 40, cy + 40), 256);
                Img("Icon", card, co, ArtDer + c.icon + ".png", Rect.MinMaxRect(cx - 32, cy - 32, cx + 32, cy + 32), 256);
                Label("Label", card, co, c.label, font, 15f, SubInk, false, x0 + 117, x0 + 228, 188, TextAlignmentOptions.MidlineLeft);
                r.statValues[i] = Label("Value", card, co, c.value, font, 40f, Color.white, true, x0 + 115, x0 + 300, 227, TextAlignmentOptions.MidlineLeft);
                r.statNotes[i] = Label("Note", card, co, c.note, font, 14f, SubInk, false, x0 + 117, x0 + 318, 267, TextAlignmentOptions.MidlineLeft);
                var chipBox = Rect.MinMaxRect(x0 + 232, 172, x0 + 312, 202);
                var chip = Box("Chip", card, co, chipBox);
                var cho = new Vector2(chipBox.xMin, chipBox.yMin);
                Sliced("Frame", chip, cho, ArtDer + "chip.png", new Vector2(400f, 160f), Rect.MinMaxRect(20, 20, 380, 140),
                    new Vector4(80, 0, 80, 0), chipBox, 30f / 120f, 512);
                r.chipArrows[i] = Img("Arrow", chip, cho, ArtDer + (c.chipDown ? "arrow_down.png" : "arrow_up.png"),
                    Rect.MinMaxRect(chipBox.xMin + 10, 177, chipBox.xMin + 30, 197), 256);
                r.chipArrows[i].color = Cyan;
                r.chipTexts[i] = Label("Text", chip, cho, c.chip, font, 17f, Cyan, false, chipBox.xMin + 32, chipBox.xMax - 6, 187,
                    TextAlignmentOptions.Center);
                r.chips[i] = chip.gameObject;
            }
        }

        // 막대 항목 판(6) + 종합 평가(19 제목, 20 등급, 레이더)
        static void BuildAnalysis(RectTransform stage, FrameSpec spec, TMP_FontAsset font, FrameRefs r)
        {
            var box = AnalysisBox;
            var analysis = Box("AnalysisPanel", stage, Vector2.zero, box);
            var ao = new Vector2(box.xMin, box.yMin);
            Sliced("Frame", analysis, ao, ArtDir + "6.png", S1774, Rect.MinMaxRect(66, 138, 1708, 756),
                new Vector4(312, 255, 216, 292), box, 805f / 1642f, 2048);
            // 판 안쪽 유리판의 왼쪽 위가 비스듬히 잘려 있어(시안 (299,374)~(349,328)) 제목 줄은 그 안쪽(아래·오른쪽)에 둔다
            PanelTitle(analysis, ao, ArtDer + "icon_bars.png", new Vector2(344f, 301f), 241f, new Vector2(364f, 352f), spec.analysisTitle, font);

            int n = spec.rows.Length;
            r.fills = new RectTransform[n];
            r.barValues = new TMP_Text[n];
            for (int i = 0; i < n; i++)
            {
                var row = spec.rows[i];
                float y = spec.rowY[i];
                float iconS = RowIconH / row.bodyH;
                Img("RowIcon" + i, analysis, ao, row.icon,
                    new Rect(RowIconCx - row.png.x * iconS / 2f, y - row.png.y * iconS / 2f, row.png.x * iconS, row.png.y * iconS), 512);
                Label("RowName" + i, analysis, ao, row.label, font, 17f, Ink, false, RowNameX, BarX0 - 6f, y, TextAlignmentOptions.MidlineLeft);
                Sliced("Track" + i, analysis, ao, ArtDir + "23.png", S2172, Rect.MinMaxRect(209, 260, 1964, 451),
                    new Vector4(304, 0, 303, 0), Rect.MinMaxRect(BarX0, y - BarH / 2f, BarX1, y + BarH / 2f), BarH / 191f, 4096);
                var fill = Sliced("Fill" + i, analysis, ao, ArtDer + row.fill + ".png", new Vector2(512f, 72f), new Rect(0, 0, 512, 72),
                    new Vector4(36, 0, 36, 0), Rect.MinMaxRect(BarX0, y - 8, BarX0 + (BarX1 - BarX0) * row.sample, y + 8), 16f / 72f, 512);
                r.fills[i] = fill.rectTransform;
                r.fills[i].pivot = new Vector2(0f, 0.5f);
                r.fills[i].anchoredPosition = new Vector2(BarX0 - ao.x, -(y - ao.y));
                r.barValues[i] = Label("Value" + i, analysis, ao, $"{Mathf.RoundToInt(row.sample * 100)}%", font, 18f, Ink, false,
                    BarValueRight - 60, BarValueRight, y, TextAlignmentOptions.MidlineRight);
            }
            Line("Divider", analysis, ao, Rect.MinMaxRect(747.25f, 342, 748.75f, 612), LineColor);
            TitleImage("SummaryTitle", analysis, ao, ArtDir + "19.png", 452, 217, 500, 771f, 352f, 21f);
            var badgeBox = Rect.MinMaxRect(992 - 44.5f, 352 - 15.7f, 992 + 44.5f, 352 + 15.7f);   // 오른쪽 위도 안쪽 판 안으로
            r.badge = ImageByBody("GradeBadge", analysis, ao, ArtDir + "20.png", S1774, Rect.MinMaxRect(455, 289, 1319, 594),
                badgeBox.center, badgeBox.height, 2048);
            r.gradeText = Label("GradeText", analysis, ao, "좋아요", font, 17f, Cyan, true, badgeBox.xMin, badgeBox.xMax, 352,
                TextAlignmentOptions.Center);
            r.gradeText.gameObject.SetActive(false);

            float gs = RadarScale, ggs = RadarGridScale;
            var labelBox = new Rect(RadarCenter.x - RadarGridCenterPx.x * gs, RadarCenter.y - RadarGridCenterPx.y * gs, 1254f * gs, 1254f * gs);
            Img("RadarGrid", analysis, ao, ArtDer + "radar_grid.png",
                new Rect(RadarCenter.x - RadarGridCenterPx.x * ggs, RadarCenter.y - RadarGridCenterPx.y * ggs, 1254f * ggs, 1254f * ggs), 2048);
            float rr = RadarGridRadiusPx * ggs;
            var radarRt = Box("Radar", analysis, ao, Rect.MinMaxRect(RadarCenter.x - rr, RadarCenter.y - rr, RadarCenter.x + rr, RadarCenter.y + rr));
            r.radar = radarRt.gameObject.AddComponent<UIRadarChart>();
            r.radar.raycastTarget = false;
            r.radarValues = new TMP_Text[5];
            for (int i = 0; i < 5; i++)
            {
                var p = new Vector2(labelBox.xMin, labelBox.yMin) + RadarLabelPx[i] * gs;
                var name = Label("RadarName" + i, analysis, ao, spec.radarNames[i], font, 15f, Ink, true, p.x - 60, p.x + 60, p.y,
                    TextAlignmentOptions.Center);
                float half = name.GetPreferredValues(spec.radarNames[i]).x / 2f;
                // 오른쪽·왼쪽 축 이름은 글자 폭을 재서 꼭짓점 점에서 8만큼 띄운다(긴 이름도 판 안쪽 경계·가운데 선에 닿지 않게)
                float vx = rr * Mathf.Cos(18f * Mathf.Deg2Rad);
                if (i == 1) p.x = RadarCenter.x + vx + 8f + half;
                else if (i == 4) p.x = RadarCenter.x - vx - 8f - half;
                name.rectTransform.anchoredPosition = new Vector2(p.x - ao.x, name.rectTransform.anchoredPosition.y);
                // 위쪽 축은 이름과 꼭짓점 사이가 좁아 값을 이름 오른쪽에, 나머지는 이름 아래에
                r.radarValues[i] = i == 0
                    ? Label("RadarValue" + i, analysis, ao, spec.radarSamples[i], font, 18f, Cyan, true, p.x + half + 4, p.x + half + 70, p.y,
                        TextAlignmentOptions.MidlineLeft)
                    : Label("RadarValue" + i, analysis, ao, spec.radarSamples[i], font, 18f, Cyan, true, p.x - 40, p.x + 40, p.y + 22,
                        TextAlignmentOptions.Center);
            }
        }

        // 판 제목: 아이콘(그림 속 아이콘 높이 iconBodyH px를 30 시안 px로) + 흰 글자
        public static void PanelTitle(RectTransform parent, Vector2 origin, string icon, Vector2 png, float iconBodyH, Vector2 iconCenter,
            string title, TMP_FontAsset font)
        {
            float s = 30f / iconBodyH;
            Img("TitleIcon", parent, origin, icon, new Rect(iconCenter.x - png.x * s / 2f, iconCenter.y - png.y * s / 2f, png.x * s, png.y * s), 512);
            Label("Title", parent, origin, title, font, 22f, Ink, true, iconCenter.x + 30f, iconCenter.x + 330f, iconCenter.y,
                TextAlignmentOptions.MidlineLeft);
        }

        // 공통 칸을 View(ModeReportView) 필드에 연결
        public static void WireCommon(SerializedObject so, FrameRefs r)
        {
            so.FindProperty("backBtn").objectReferenceValue = r.back;
            so.FindProperty("otherModeBtn").objectReferenceValue = r.otherMode;
            so.FindProperty("retryBtn").objectReferenceValue = r.retry;
            SetArray(so, "tabButtons", r.tabButtons);
            SetArray(so, "tabLabels", r.tabLabels);
            so.FindProperty("tabHighlight").objectReferenceValue = r.highlight;
            so.FindProperty("dateText").objectReferenceValue = r.date;
            SetArray(so, "statValues", r.statValues);
            SetArray(so, "statNotes", r.statNotes);
            SetArray(so, "statChips", r.chips);
            SetArray(so, "statChipArrows", r.chipArrows);
            SetArray(so, "statChipTexts", r.chipTexts);
            so.FindProperty("arrowUp").objectReferenceValue = LoadSprite(ArtDer + "arrow_up.png", 256);
            so.FindProperty("arrowDown").objectReferenceValue = LoadSprite(ArtDer + "arrow_down.png", 256);
            SetArray(so, "barFills", r.fills);
            so.FindProperty("barFullWidth").floatValue = BarX1 - BarX0;
            SetArray(so, "barValues", r.barValues);
            so.FindProperty("radar").objectReferenceValue = r.radar;
            SetArray(so, "radarValues", r.radarValues);
            so.FindProperty("gradeBadge").objectReferenceValue = r.badge;
            so.FindProperty("gradeBestSprite").objectReferenceValue = LoadSprite(ArtDir + "20.png", 2048);
            so.FindProperty("gradePlainSprite").objectReferenceValue = LoadSprite(ArtDer + "badge_blank.png", 2048);
            so.FindProperty("gradeText").objectReferenceValue = r.gradeText;
        }

        // ===== 배치 헬퍼 (좌표는 모두 시안 px, origin = 부모 좌상단의 시안 좌표, 위에서부터 y) =====

        public static RectTransform Box(string name, Transform parent, Vector2 origin, Rect r)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(r.center.x - origin.x, -(r.center.y - origin.y));
            rt.sizeDelta = r.size;
            return rt;
        }

        public static Image Img(string name, Transform parent, Vector2 origin, string path, Rect r, int maxSize)
        {
            var img = Box(name, parent, origin, r).gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path, maxSize);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        // 이미지 속 본체(body, px)의 가운데가 center, 높이가 bodyH가 되게 놓는다(빛 여백은 본체 밖으로 넘친다)
        public static Image ImageByBody(string name, Transform parent, Vector2 origin, string path, Vector2 imgSize, Rect body,
            Vector2 center, float bodyH, int maxSize)
        {
            float s = bodyH / body.height;
            var c = center + new Vector2((imgSize.x / 2f - body.center.x) * s, (imgSize.y / 2f - body.center.y) * s);
            return Img(name, parent, origin, path, new Rect(c.x - imgSize.x * s / 2f, c.y - imgSize.y * s / 2f, imgSize.x * s, imgSize.y * s), maxSize);
        }

        // 본체 테두리가 target에 오게 9-slice로 늘린다. s = 시안 px / 이미지 px(테두리 쪽 배율), border = 이미지 px (왼, 아래, 오른, 위)
        public static Image Sliced(string name, Transform parent, Vector2 origin, string path, Vector2 imgSize, Rect body, Vector4 border,
            Rect target, float s, int maxSize)
        {
            var sprite = SlicedSprite(path, border, maxSize);
            var r = Rect.MinMaxRect(target.xMin - body.xMin * s, target.yMin - body.yMin * s,
                target.xMax + (imgSize.x - body.xMax) * s, target.yMax + (imgSize.y - body.yMax) * s);
            var img = Box(name, parent, origin, r).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / s;   // 스프라이트·캔버스 모두 100 px/unit: 테두리(무대 px) = 테두리 px * s
            img.raycastTarget = false;
            return img;
        }

        public static Sprite SlicedSprite(string path, Vector4 border, int maxSize)
        {
            LoadSprite(path, maxSize);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                if (ti.spriteBorder != border || st.spriteMeshType != SpriteMeshType.FullRect)
                {
                    st.spriteMeshType = SpriteMeshType.FullRect;   // 9-slice는 사각 메시여야 테두리 경고 없이 늘어난다
                    ti.SetTextureSettings(st);
                    ti.spriteBorder = border;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 글자 조각(아이콘 + 글자): 글자 높이가 textH, 아이콘 왼쪽이 x, 글자 세로 가운데가 yc가 되게
        public static Image TitleImage(string name, Transform parent, Vector2 origin, string path, float iconXpx, float textY0px, float textY1px,
            float x, float yc, float textH)
        {
            float s = textH / (textY1px - textY0px);
            float left = x - iconXpx * s, top = yc - (textY0px + textY1px) / 2f * s;
            return Img(name, parent, origin, path, new Rect(left, top, S2172.x * s, S2172.y * s), 4096);
        }

        public static Image Dot(string name, Transform parent, Vector2 origin, Vector2 c, float size, Color color)
        {
            var img = Img(name, parent, origin, ArtDer + "dot.png", new Rect(c.x - size / 2f, c.y - size / 2f, size, size), 64);
            img.color = color;
            return img;
        }

        public static Image Line(string name, Transform parent, Vector2 origin, Rect r, Color color)
        {
            var img = Box(name, parent, origin, r).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // 투명 누르는 영역(버튼). 그림은 자식으로 따로 넣는다
        public static RectTransform HitBox(string name, Transform parent, Vector2 origin, Rect r)
        {
            var rt = Box(name, parent, origin, r);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            return rt;
        }

        // 본체 폭 bodyW의 이미지 버튼(누르는 영역 = 본체)
        public static Button HitBoxByBody(string name, Transform parent, Vector2 origin, string path, Vector2 imgSize, Rect body,
            Vector2 center, float bodyW)
        {
            float bodyH = bodyW * body.height / body.width;
            var r = Rect.MinMaxRect(center.x - bodyW / 2f, center.y - bodyH / 2f, center.x + bodyW / 2f, center.y + bodyH / 2f);
            var rt = HitBox(name, parent, origin, r);
            ImageByBody("Image", rt, new Vector2(r.xMin, r.yMin), path, imgSize, body, center, bodyH, 4096);
            return rt.GetComponent<Button>();
        }

        // 가운데 세로 정렬 글자. 가로 x0~x1, 세로 가운데 yc. size = 시안 px
        public static TMP_Text Label(string name, Transform parent, Vector2 origin, string text, TMP_FontAsset font, float size, Color color,
            bool bold, float x0, float x1, float yc, TextAlignmentOptions align)
        {
            var rt = Box(name, parent, origin, Rect.MinMaxRect(x0, yc - size, x1, yc + size));
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static void SetArray<T>(SerializedObject so, string prop, T[] items) where T : Object
        {
            var p = so.FindProperty(prop);
            p.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // 확인용 캡처 (씬은 저장하지 않는다). 씬에 든 예시 값(시안과 같은 값)으로 찍힌다
        public static void Capture(string scenePath, string tag)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("[ModeReportSceneKit] 저장 안 한 씬 변경이 있어 캡처하지 않는다.");
                return;
            }
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            var sizes = new[] { (2560, 1600, "tab"), (2400, 1080, "phone"), (1920, 1440, "43"), (1920, 1080, "169") };
            try
            {
                foreach (var (w, h, size) in sizes)
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    string file = Path.Combine(outDir, $"{size}_{tag}.png");
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);   // 캔버스 크기를 기기 해상도로 맞춘 뒤
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);   // 배경 맞춤이 다시 계산된 모습으로 한 번 더
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            Debug.Log($"[ModeReportSceneKit] {tag} 캡처 완료 -> {outDir}");
        }
    }
}
