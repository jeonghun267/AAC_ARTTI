using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.Common;
using Artti.Report;
using Artti.UI;
using static Artti.Editor.ModeReportSceneKit;
using static Artti.Editor.ProfileAvatarStepBuilder;

namespace Artti.Editor
{
    // 대화 모드 리포트(REPORT 시안 1672x941). 새 씬이고 손배치가 없어 이 빌더가 원본이다.
    // 공통 틀(배경·왼쪽 메뉴·탭·스탯 카드·막대·종합 평가·다시 하기)은 ModeReportSceneKit, 이 빌더는 대화 기록 판과 AI 코멘트·이번 주 변화를 더한다.
    // - 값은 실행하면 ConversationReportView가 기록(JSONL)으로 채운다. 씬에는 시안과 같은 예시 값이 들어 있다.
    public static class ConversationReportSceneBuilder
    {
        static readonly float[] MessageY = { 473f, 521f, 570f, 617f };
        static readonly (bool user, string text, string time)[] MessageSample =
        {
            (false, "어서 오세요. 무엇을 찾으세요?", "오전 10:14"), (true, "생수 어디에 있나요?", "오전 10:15"),
            (false, "오른쪽 음료 코너에 있어요.", "오전 10:16"), (true, "감사합니다!", "오전 10:17"),
        };
        const float TimelineX0 = 1139f, TimelinePitch = 46.4f, TimelineY = 400f;
        static readonly float[] CommentY = { 731f, 759f, 787f };
        static readonly string[] CommentSample =
        {
            "전반적으로 자연스러운 대화를 잘 이어갔어요.", "질문을 정확히 이해하고 적절하게 응답했어요.", "조금 더 다양한 표현을 사용하면 더 좋아질 수 있어요.",
        };
        static readonly Rect ChartRect = Rect.MinMaxRect(902, 730, 1480, 786);
        static readonly string[] Days = { "월", "화", "수", "목", "금", "토", "일" };

        static FrameSpec Spec() => new FrameSpec
        {
            mode = TrainingMode.Conversation,
            headerIcon = "icon_chat",
            title = "대화 모드 리포트",
            subtitle = "오늘의 대화 연습 결과를 확인해보세요.",
            cards = new[]
            {
                new CardSpec("icon_clock", "총 대화 시간", "18<size=62%> 분</size>", "28%", "어제보다 4분 더 대화했어요!"),
                new CardSpec("icon_chat", "대화 자연스러움", "88<size=62%> %</size>", "12%", "더 자연스러운 대화를 보여줬어요!"),
                new CardSpec("icon_sparkle", "응답 적절성", "84<size=62%> %</size>", "9%", "상황에 맞는 답변을 잘했어요!"),
                new CardSpec("icon_flag", "완료한 상황", "5<size=62%> 개</size>", "2개", "다양한 상황을 경험했어요!"),
            },
            analysisTitle = "주요 대화 분석",
            // 아이콘 = 15~18.png에서 뗀 것(사방 여백 30px). 글자 그림은 작게 줄이면 흐려져서 이름은 TMP
            rows = new[]
            {
                new RowSpec(ArtDer + "icon_row0.png", new Vector2(491, 455), 395f, "자연스러움", "fill_blue", 0.88f),
                new RowSpec(ArtDer + "icon_row1.png", new Vector2(349, 394), 334f, "응답 속도", "fill_blue", 0.82f),
                new RowSpec(ArtDer + "icon_row2.png", new Vector2(392, 485), 425f, "질문 이해", "fill_violet", 0.86f),
                new RowSpec(ArtDer + "icon_row3.png", new Vector2(432, 364), 304f, "대화 지속력", "fill_cyan", 0.80f),
            },
            rowY = new[] { 397f, 452f, 507f, 563f },
            radarNames = new[] { "이해", "자연스러움", "속도", "표현력", "지속력" },
            radarSamples = new[] { "86%", "88%", "82%", "78%", "80%" },
            retryLabel = "다시 대화하기",
        };

        [MenuItem("Artti/Build ConversationReportScene Hierarchy")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ConversationReportSceneBuilder] Play 모드에서는 실행하지 않는다."); return; }
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            AssetDatabase.Refresh();
            OpenOrCreate(ScenePaths.ConversationReport);
            var r = BuildFrame(Spec());
            var stage = r.stage;
            var font = r.font;

            // ===== 대화 기록 =====
            var historyBox = RightBox;
            var history = Box("HistoryPanel", stage, Vector2.zero, historyBox);
            var ho = new Vector2(historyBox.xMin, historyBox.yMin);
            Sliced("Frame", history, ho, ArtDir + "7.png", new Vector2(1448f, 1086f), Rect.MinMaxRect(58, 196, 1390, 876),
                new Vector4(324, 346, 218, 366), historyBox, 537f / 1332f, 2048);
            // 안쪽 유리판 왼쪽 위가 비스듬히 잘려 있어(시안 (1109,362)~(1150,326)) 제목 줄은 그 안쪽에, 오른쪽 끝도 안쪽 판(~1603) 안에
            Img("TitleIcon", history, ho, ArtDer + "icon_chat_fill.png", Rect.MinMaxRect(1148.2f, 330.6f, 1185.8f, 365.4f), 512);
            Label("Title", history, ho, "대화 기록", font, 22f, Ink, true, 1194, 1360, 348, TextAlignmentOptions.MidlineLeft);
            var viewAll = HitBoxByBody("ViewAllButton", history, ho, ArtDir + "12.png", S2172, Rect.MinMaxRect(315, 168, 1857, 554),
                new Vector2(1543f, 348f), 110f);
            Dot("LegendUser", history, ho, new Vector2(1442, 377), 20f, new Color32(52, 232, 250, 255));
            Label("LegendUserText", history, ho, "내 발화  /", font, 13f, SubInk, false, 1452, 1512, 377, TextAlignmentOptions.MidlineLeft);
            Dot("LegendNpc", history, ho, new Vector2(1522, 377), 20f, new Color32(66, 132, 255, 255));
            Label("LegendNpcText", history, ho, "NPC 발화", font, 13f, SubInk, false, 1532, 1600, 377, TextAlignmentOptions.MidlineLeft);
            var wave = ImageByBody("Wave", history, ho, ArtDir + "25.png", new Vector2(1068f, 103f), Rect.MinMaxRect(21, 20, 1050, 87),
                new Vector2((TimelineX0 + TimelineX0 + 9 * TimelinePitch) / 2f, TimelineY), 24f, 2048);
            wave.color = new Color(1f, 1f, 1f, 0.7f);
            var dots = new Image[10];
            var numbers = new TMP_Text[10];
            for (int i = 0; i < 10; i++)
            {
                float x = TimelineX0 + i * TimelinePitch;
                dots[i] = Dot("Dot" + (i + 1), history, ho, new Vector2(x, TimelineY), 28f,
                    i % 2 == 0 ? new Color32(66, 132, 255, 255) : new Color32(52, 232, 250, 255));
                numbers[i] = Label("Num" + (i + 1), history, ho, (i + 1).ToString(), font, 14f, SubInk, false, x - 14, x + 14, 425,
                    TextAlignmentOptions.Center);
            }
            var rowRefs = new (GameObject npcRoot, RectTransform npcBubble, TMP_Text npcText, TMP_Text npcTime,
                GameObject userRoot, RectTransform userBubble, TMP_Text userText, TMP_Text userTime)[4];
            for (int i = 0; i < 4; i++)
            {
                float y = MessageY[i];
                var sample = MessageSample[i];
                // 점원 줄: 동그라미 NPC + 어두운 말풍선(왼쪽 고정, 폭은 글자에 맞춤) + 시각(말풍선 오른쪽)
                var npc = Box("Npc" + i, history, ho, Rect.MinMaxRect(1110, y - 22, 1620, y + 22));
                var no = new Vector2(1110, y - 22);
                Img("Avatar", npc, no, ArtDir + "24.png", Rect.MinMaxRect(1115, y - 18, 1151, y + 18), 64);
                Label("AvatarText", npc, no, "NPC", font, 11f, Ink, false, 1115, 1151, y, TextAlignmentOptions.Center);
                var (nb, nt, ntime) = Bubble("Bubble", npc, no, ArtDer + "bubble_npc.png", new Vector2(640f, 208f), Rect.MinMaxRect(20, 20, 620, 188),
                    new Vector4(80, 0, 72, 0), 1167f, y, 245f, 40f, false, sample.user ? "오른쪽 음료 코너에 있어요." : sample.text,
                    sample.time, font);
                // 내 줄: 밝은 말풍선(오른쪽 고정) + 시각
                var user = Box("User" + i, history, ho, Rect.MinMaxRect(1300, y - 22, 1620, y + 22));
                var uo = new Vector2(1300, y - 22);
                var (ub, ut, utime) = Bubble("Bubble", user, uo, ArtDer + "bubble_user.png", new Vector2(656f, 224f), Rect.MinMaxRect(28, 28, 628, 196),
                    UserBubbleBorder, 1530f, y, 188f, 38f, true, sample.user ? sample.text : "생수 어디에 있나요?", sample.time, font);
                npc.gameObject.SetActive(!sample.user);
                user.gameObject.SetActive(sample.user);
                rowRefs[i] = (npc.gameObject, nb, nt, ntime, user.gameObject, ub, ut, utime);
            }
            var empty = Label("Empty", history, ho, "아직 대화 기록이 없어요", font, 17f, Faint, false, 1100, 1612, 545, TextAlignmentOptions.Center);
            empty.gameObject.SetActive(false);

            // ===== 아래 판: AI 코멘트 + 이번 주 변화 =====
            var bottomBox = BottomBox;
            var bottom = Box("BottomPanel", stage, Vector2.zero, bottomBox);
            var bo = new Vector2(bottomBox.xMin, bottomBox.yMin);
            Sliced("Frame", bottom, bo, ArtDir + "8.png", S2172, Rect.MinMaxRect(85, 137, 2092, 570),
                new Vector4(446, 284, 281, 267), bottomBox, 174f / 433f, 4096);
            // 안쪽 유리판 왼쪽 위가 비스듬히 잘려 있어(시안 약 (295,716)~(346,682)) 제목 줄은 그 안쪽에
            Img("Bulb", bottom, bo, ArtDer + "icon_bulb.png", Rect.MinMaxRect(337, 677, 383, 723), 256);
            Label("CommentTitle", bottom, bo, "AI 코멘트", font, 22f, Ink, true, 384, 620, 700, TextAlignmentOptions.MidlineLeft);
            var comments = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                float y = CommentY[i];
                Img("Check" + i, bottom, bo, ArtDer + "icon_check.png", Rect.MinMaxRect(314, y - 17, 348, y + 17), 256);
                comments[i] = Label("Comment" + i, bottom, bo, CommentSample[i], font, 16.5f, SubInk, false, 362, 795, y,
                    TextAlignmentOptions.MidlineLeft);
                comments[i].overflowMode = TextOverflowModes.Ellipsis;
            }
            Line("Divider", bottom, bo, Rect.MinMaxRect(804.25f, 690, 805.75f, 806), LineColor);
            PanelTitle(bottom, bo, ArtDer + "icon_bars.png", new Vector2(344f, 301f), 241f, new Vector2(848f, 700f), "이번 주 변화", font);
            string[] yLabels = { "100%", "50%", "0%" };
            for (int i = 0; i < 3; i++)
            {
                float y = ChartRect.yMin + ChartRect.height * i / 2f;
                Label("Y" + i, bottom, bo, yLabels[i], font, 12.5f, Faint, false, 832, 878, y, TextAlignmentOptions.MidlineRight);
            }
            Line("Baseline", bottom, bo, Rect.MinMaxRect(884, ChartRect.yMax - 0.6f, 1490, ChartRect.yMax + 0.6f), LineColor);
            var chartRt = Box("WeekChart", bottom, bo, ChartRect);
            var chart = chartRt.gameObject.AddComponent<UILineChart>();
            chart.raycastTarget = false;
            for (int i = 0; i < 7; i++)
            {
                float x = ChartRect.xMin + ChartRect.width * i / 6f;
                Label("Day" + i, bottom, bo, Days[i], font, 14f, SubInk, false, x - 16, x + 16, 805, TextAlignmentOptions.Center);
            }
            var weekChipBox = Rect.MinMaxRect(1496, 685, 1576, 715);
            var weekChip = Box("WeekChip", bottom, bo, weekChipBox);
            var wco = new Vector2(weekChipBox.xMin, weekChipBox.yMin);
            Sliced("Frame", weekChip, wco, ArtDer + "chip.png", new Vector2(400f, 160f), Rect.MinMaxRect(20, 20, 380, 140),
                new Vector4(80, 0, 80, 0), weekChipBox, 30f / 120f, 512);
            var weekChipText = Label("Text", weekChip, wco, "+14%", font, 17f, Cyan, true, weekChipBox.xMin, weekChipBox.xMax, 700,
                TextAlignmentOptions.Center);

            var overlay = BuildHistoryOverlay(stage, font);   // 무대 맨 위(마지막 자식)

            // ===== View 연결 =====
            var view = r.canvasGo.AddComponent<ConversationReportView>();
            var so = new SerializedObject(view);
            WireCommon(so, r);
            so.FindProperty("viewAllBtn").objectReferenceValue = viewAll;
            SetArray(so, "timelineDots", dots);
            SetArray(so, "timelineNumbers", numbers);
            var rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = rowRefs.Length;
            for (int i = 0; i < rowRefs.Length; i++)
            {
                var e = rowsProp.GetArrayElementAtIndex(i);
                var rr = rowRefs[i];
                e.FindPropertyRelative("npcRoot").objectReferenceValue = rr.npcRoot;
                e.FindPropertyRelative("npcBubble").objectReferenceValue = rr.npcBubble;
                e.FindPropertyRelative("npcText").objectReferenceValue = rr.npcText;
                e.FindPropertyRelative("npcTime").objectReferenceValue = rr.npcTime;
                e.FindPropertyRelative("userRoot").objectReferenceValue = rr.userRoot;
                e.FindPropertyRelative("userBubble").objectReferenceValue = rr.userBubble;
                e.FindPropertyRelative("userText").objectReferenceValue = rr.userText;
                e.FindPropertyRelative("userTime").objectReferenceValue = rr.userTime;
            }
            so.FindProperty("historyEmpty").objectReferenceValue = empty;
            SetArray(so, "comments", comments);
            so.FindProperty("weekChart").objectReferenceValue = chart;
            so.FindProperty("weekChip").objectReferenceValue = weekChip.gameObject;
            so.FindProperty("weekChipText").objectReferenceValue = weekChipText;
            so.FindProperty("historyOverlay").objectReferenceValue = overlay.root;
            so.FindProperty("historyCloseBtn").objectReferenceValue = overlay.close;
            so.FindProperty("historyDimBtn").objectReferenceValue = overlay.dim;
            so.FindProperty("historySubtitle").objectReferenceValue = overlay.subtitle;
            so.FindProperty("historyScroll").objectReferenceValue = overlay.scroll;
            so.FindProperty("historyContent").objectReferenceValue = overlay.content;
            so.FindProperty("npcRowTemplate").objectReferenceValue = overlay.npcTemplate;
            so.FindProperty("userRowTemplate").objectReferenceValue = overlay.userTemplate;
            so.FindProperty("historyOverlayEmpty").objectReferenceValue = overlay.empty;
            so.FindProperty("npcBubbleX").floatValue = OverlayNpcBubbleX;
            so.FindProperty("timeWidth").floatValue = OverlayTimeWidth;
            so.FindProperty("rowMinHeight").floatValue = OverlayRowH;
            so.ApplyModifiedPropertiesWithoutUndo();

            Finish(r.canvasGo, ScenePaths.ConversationReport);
            Debug.Log("[ConversationReportSceneBuilder] 완료 (공통 틀 + 대화 기록 + AI 코멘트/이번 주 변화)");
        }

        // ===== 전체 대화 기록 창 ('전체 보기') =====
        // 화면 전체 어두운 막(누르면 닫힘) + 가운데 유리 판(7.png, 대화 기록 판과 같은 배율) + 제목·부제·닫기 + 세로 스크롤.
        //   줄 원본(점원 / 나)은 Content 아래 꺼 둔 채로 두고, View가 열 때마다 복제해 다시 쓴다.
        //   판 안쪽 유리판은 바깥 테두리에서 약 21 안, 왼쪽 위는 41x36만큼 비스듬히 잘려 있어 내용은 그 안에 둔다.
        static readonly Rect OverlayCard = Rect.MinMaxRect(436, 92, 1236, 862);
        static readonly Rect OverlayViewport = Rect.MinMaxRect(470, 210, 1200, 834);
        const float OverlayNpcBubbleX = 46f, OverlayTimeWidth = 76f, OverlayRowH = 40f;
        const float OverlayPad = 3000f;   // 무대가 줄어든 폰에서도 어두운 막이 화면 전체를 덮게
        static readonly Vector4 UserBubbleBorder = new Vector4(72, 62, 72, 62);
        // 꼬리가 위쪽에 있는 점원 말풍선: 위 테두리(96px)가 꼬리(이미지 y 56~88)를 덮어 여러 줄로 늘어나도 꼬리는 그대로
        static readonly Vector4 NpcTopBubbleBorder = new Vector4(72, 72, 72, 96);

        static (GameObject root, Button close, Button dim, TMP_Text subtitle, ScrollRect scroll, RectTransform content,
            RectTransform npcTemplate, RectTransform userTemplate, TMP_Text empty) BuildHistoryOverlay(RectTransform stage, TMP_FontAsset font)
        {
            var root = Box("HistoryOverlay", stage, Vector2.zero,
                Rect.MinMaxRect(-OverlayPad, -OverlayPad, StageSize.x + OverlayPad, StageSize.y + OverlayPad));
            var dimImg = root.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0.01f, 0.02f, 0.08f, 0.85f);   // 선형 색 공간이라 0.7대면 뒤 화면이 꽤 비친다
            var dim = root.gameObject.AddComponent<Button>();
            dim.targetGraphic = dimImg;
            dim.transition = Selectable.Transition.None;
            var ro = new Vector2(-OverlayPad, -OverlayPad);

            var card = Box("Card", root, ro, OverlayCard);
            card.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 판 위를 눌러도 닫히지 않게
            var co = new Vector2(OverlayCard.xMin, OverlayCard.yMin);
            // 유리 판이 비쳐 뒤 화면 글자가 겹쳐 보이지 않게 판 안쪽에 어두운 바탕을 깐다
            var fill = Box("Fill", card, co, Rect.MinMaxRect(OverlayCard.xMin + 14, OverlayCard.yMin + 14, OverlayCard.xMax - 14, OverlayCard.yMax - 14))
                .gameObject.AddComponent<Image>();
            fill.sprite = Rounded();
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1.5f;
            fill.color = new Color(0.02f, 0.05f, 0.16f, 0.9f);
            fill.raycastTarget = false;
            Sliced("Frame", card, co, ArtDir + "7.png", new Vector2(1448f, 1086f), Rect.MinMaxRect(58, 196, 1390, 876),
                new Vector4(324, 346, 218, 366), OverlayCard, 537f / 1332f, 2048);
            Img("TitleIcon", card, co, ArtDer + "icon_chat_fill.png", Rect.MinMaxRect(501.2f, 123.6f, 538.8f, 158.4f), 512);
            Label("Title", card, co, "전체 대화 기록", font, 24f, Ink, true, 548, 900, 141, TextAlignmentOptions.MidlineLeft);
            var close = HitBox("CloseButton", card, co, Rect.MinMaxRect(1162, 116, 1210, 164));
            Img("Icon", close, new Vector2(1162, 116), ArtDer + "icon_close.png", Rect.MinMaxRect(1160, 114, 1212, 166), 256);
            var subtitle = Label("Subtitle", card, co, "2026년 10월 1일 (목) 오전 10:14 · 말 4개", font, 15f, SubInk, false, 502, 1150, 176,
                TextAlignmentOptions.MidlineLeft);
            Line("Divider", card, co, Rect.MinMaxRect(480, 197.4f, 1192, 198.6f), LineColor);

            var viewport = Box("Viewport", card, co, OverlayViewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 빈 곳을 끌어도 스크롤되게
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = ChildRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 300f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            float w = OverlayViewport.width;
            var npcTemplate = OverlayRow("NpcRowTemplate", content, w, false, font);
            var userTemplate = OverlayRow("UserRowTemplate", content, w, true, font);
            npcTemplate.gameObject.SetActive(false);
            userTemplate.gameObject.SetActive(false);

            var empty = Label("Empty", card, co, "아직 대화 기록이 없어요", font, 18f, Faint, false, 480, 1192, 500, TextAlignmentOptions.Center);
            empty.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            return (root.gameObject, close.GetComponent<Button>(), dim, subtitle, scroll, content, npcTemplate, userTemplate, empty);
        }

        // 줄 원본: 루트(Content 왼쪽 위 기준) 아래 Bubble(Frame + Text), Time. 점원 줄은 NPC 동그라미도. 자리·크기는 View가 정한다
        static RectTransform OverlayRow(string name, RectTransform content, float width, bool user, TMP_FontAsset font)
        {
            var row = ChildRect(name, content);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(width, OverlayRowH);
            if (!user)
            {
                var avatar = Img("Avatar", row, Vector2.zero, ArtDir + "24.png", Rect.MinMaxRect(0, 2, 36, 38), 64);
                Label("Text", avatar.transform, new Vector2(0, 2), "NPC", font, 11f, Ink, false, 0, 36, 20, TextAlignmentOptions.Center);
            }

            var bubble = ChildRect("Bubble", row);
            bubble.anchorMin = bubble.anchorMax = new Vector2(0f, 1f);
            bubble.pivot = new Vector2(user ? 1f : 0f, 1f);
            bubble.anchoredPosition = new Vector2(user ? width - OverlayTimeWidth - 8f : OverlayNpcBubbleX, 0f);
            bubble.sizeDelta = new Vector2(user ? 160f : 260f, OverlayRowH);
            var imgSize = user ? new Vector2(656f, 224f) : new Vector2(640f, 208f);
            var body = user ? Rect.MinMaxRect(28, 28, 628, 196) : Rect.MinMaxRect(20, 20, 620, 188);
            float s = OverlayRowH / body.height;
            var frame = ChildRect("Frame", bubble);
            StretchFull(frame);
            frame.offsetMin = new Vector2(-body.xMin * s, -(imgSize.y - body.yMax) * s);
            frame.offsetMax = new Vector2((imgSize.x - body.xMax) * s, body.yMin * s);
            var img = frame.gameObject.AddComponent<Image>();
            img.sprite = SlicedSprite(ArtDer + (user ? "bubble_user.png" : "bubble_npc_top.png"), user ? UserBubbleBorder : NpcTopBubbleBorder, 1024);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / s;
            img.raycastTarget = false;

            // 글자 칸 여백(왼, 아래 / 오른, 위)은 View가 말풍선 크기를 잴 때도 그대로 쓴다
            var textRt = ChildRect("Text", bubble);
            StretchFull(textRt);
            textRt.offsetMin = new Vector2(user ? 16f : 20f, 9f);
            textRt.offsetMax = new Vector2(user ? -16f : -14f, -9f);
            var t = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = user ? "생수 어디에 있나요?" : "어서 오세요. 무엇을 찾으세요?";
            t.fontSize = 17f;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;

            var timeRt = ChildRect("Time", row);
            timeRt.anchorMin = timeRt.anchorMax = new Vector2(0f, 1f);
            timeRt.pivot = Vector2.zero;
            timeRt.anchoredPosition = new Vector2(user ? width - OverlayTimeWidth + 2f : OverlayNpcBubbleX + 270f, -OverlayRowH);
            timeRt.sizeDelta = new Vector2(OverlayTimeWidth, 20f);
            var tt = timeRt.gameObject.AddComponent<TextMeshProUGUI>();
            tt.font = font;
            tt.text = "오전 10:14";
            tt.fontSize = 13f;
            tt.color = Faint;
            tt.alignment = TextAlignmentOptions.BottomLeft;
            tt.textWrappingMode = TextWrappingModes.NoWrap;
            tt.overflowMode = TextOverflowModes.Overflow;
            tt.raycastTarget = false;
            return row;
        }

        // 말풍선: 본체 rect(View가 폭을 바꾼다) + 그림 Frame(빛 여백만큼 바깥으로) + 글자 + 시각(말풍선 오른쪽 바깥)
        static (RectTransform bubble, TMP_Text text, TMP_Text time) Bubble(string name, Transform parent, Vector2 origin, string path,
            Vector2 imgSize, Rect body, Vector4 border, float anchorX, float yc, float width, float height, bool rightAnchored,
            string text, string time, TMP_FontAsset font)
        {
            var bubble = ChildRect(name, parent);
            bubble.anchorMin = bubble.anchorMax = new Vector2(0f, 1f);
            bubble.pivot = new Vector2(rightAnchored ? 1f : 0f, 0.5f);
            bubble.anchoredPosition = new Vector2(anchorX - origin.x, -(yc - origin.y));
            bubble.sizeDelta = new Vector2(width, height);
            float s = height / body.height;
            var frame = ChildRect("Frame", bubble);
            StretchFull(frame);
            frame.offsetMin = new Vector2(-body.xMin * s, -(imgSize.y - body.yMax) * s);
            frame.offsetMax = new Vector2((imgSize.x - body.xMax) * s, body.yMin * s);
            var img = frame.gameObject.AddComponent<Image>();
            img.sprite = SlicedSprite(path, border, 1024);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / s;
            img.raycastTarget = false;

            var label = ChildRect("Text", bubble);
            StretchFull(label);
            label.offsetMin = new Vector2(16f, 0f);
            label.offsetMax = new Vector2(-14f, 0f);
            var t = label.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = 16f;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Midline;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;

            var timeRt = ChildRect("Time", bubble);
            timeRt.anchorMin = timeRt.anchorMax = new Vector2(1f, 0.5f);
            timeRt.pivot = new Vector2(0f, 0.5f);
            timeRt.anchoredPosition = new Vector2(rightAnchored ? 9f : 14f, 0f);
            timeRt.sizeDelta = new Vector2(80f, 24f);
            var tt = timeRt.gameObject.AddComponent<TextMeshProUGUI>();
            tt.font = font;
            tt.text = time;
            tt.fontSize = 13f;
            tt.color = Faint;
            tt.alignment = TextAlignmentOptions.MidlineLeft;
            tt.textWrappingMode = TextWrappingModes.NoWrap;
            tt.overflowMode = TextOverflowModes.Overflow;
            tt.raycastTarget = false;
            return (bubble, t, tt);
        }

        [MenuItem("Artti/Debug/Capture ConversationReportScene - Tab + Phone + 4x3")]
        static void CaptureMenu() => Capture(ScenePaths.ConversationReport, "ConversationReport");

        // '전체 보기' 창을 예시 대화(긴 문장 포함)로 채워 찍는다(씬은 저장하지 않는다)
        [MenuItem("Artti/Debug/Capture ConversationReportScene History - Tab + Phone")]
        static void CaptureHistoryMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("[ConversationReportSceneBuilder] 저장 안 한 씬 변경이 있어 캡처하지 않는다.");
                return;
            }
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            var start = System.DateTime.Today.AddHours(10).AddMinutes(14);
            string[] say =
            {
                "어서 오세요. 무엇을 찾으세요?", "생수 어디에 있나요?", "오른쪽 음료 코너에 있어요.", "감사합니다!",
                "찾으시는 물건 있으세요?", "생수 한 병이랑 삼각김밥 하나 주세요.", "봉투 필요하세요?",
                "아니요, 봉투는 필요 없어요. 손에 들고 갈게요. 그리고 카드로 계산할게요.", "결제는 어떻게 하시겠어요?",
                "카드로 할게요.", "감사합니다. 안녕히 가세요!", "안녕히 계세요!",
            };
            var sample = new ConversationTranscript { startMs = new System.DateTimeOffset(start).ToUnixTimeMilliseconds() };
            for (int i = 0; i < say.Length; i++)
                sample.lines.Add(new ConversationLine
                {
                    isUser = i % 2 == 1, text = say[i],
                    timeMs = new System.DateTimeOffset(start.AddSeconds(25 * i)).ToUnixTimeMilliseconds(),
                });
            var fill = typeof(ConversationReportView).GetMethod("FillHistory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            try
            {
                foreach (var (w, h, tag) in new[] { (2560, 1600, "tab"), (2400, 1080, "phone") })
                {
                    EditorSceneManager.OpenScene(ScenePaths.ConversationReport, OpenSceneMode.Single);
                    var view = Object.FindFirstObjectByType<ConversationReportView>();
                    var overlay = new SerializedObject(view).FindProperty("historyOverlay").objectReferenceValue as GameObject;
                    if (overlay == null || fill == null) return;
                    overlay.SetActive(true);
                    string file = Path.Combine(outDir, $"{tag}_ConversationReport_History.png");
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);   // 캔버스를 기기 크기로 맞춘 뒤
                    fill.Invoke(view, new object[] { sample });
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(ScenePaths.ConversationReport, OpenSceneMode.Single);
            }
            Debug.Log($"[ConversationReportSceneBuilder] 전체 보기 캡처 완료 -> {outDir}");
        }
    }
}
