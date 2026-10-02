using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;
using static Artti.Editor.ARFieldResultLayout;

namespace Artti.Editor
{
    // AR 현장모드 AAC 문장 화면(ARFieldScene / Canvas/GreetingPanel, PaymentPanel, OtherPanel)을 인사하기 시안(1672x941)처럼 맞춘다.
    // 세 패널은 같은 손배치 구조(Background + NeonLayout 보드 안에 큰 틀/카드 틀/안내 바/글/CardArea/Back/AACCardTemplate)라
    // 구조는 그대로 두고 자리와 그림만 바꾼다. 패널마다 다른 것은 카드 아이콘/색/낱말(아래 PanelSpec)과 글(손배치 그대로)뿐이다.
    // CardGenerator(Gemini 문장 -> AACCardTemplate 복제 -> 누르면 TTS) 연결은 그대로 둔다.
    //   보드: 시안 px 그대로(1672x941). BoardFitter(fillSafeRect)가 큰 틀 양옆 ~ 안내 바 아래를 여백 32px만 남기고 화면에 맞춘다
    //         (도움 화면 ARFieldHelpDetailLayout과 같은 여백). 배경(ui_aac_background_neon)은 화면 cover.
    //   큰 틀/카드 틀: 안쪽을 반투명하게 한 그림(_glass)으로 바꿔 틀 안으로 배경이 비치게 한다.
    //   카드 템플릿: 파랑/보라 바탕 + 아이콘 둘레 원 + 아이콘 + 문장 + 다음 화살표. AACPhraseCardView가 문장 낱말로 아이콘/색을 고른다.
    //   세 패널 모두(변경점: 결제/편의도): 카드 틀 아래에 문장 직접 입력 줄(도움 화면 TextInputArea를 복제 -> 입력/지우기/전송(TTS) 연결 그대로)을 두고,
    //             큰 틀을 입력 줄 아래까지 늘린 뒤 안내 바를 그 아래로 내린다.
    //   그림 위치는 각 그림 속 흰 테두리 선을 시안의 선 자리에 맞춘다(빛 번짐은 밖으로 넘친다).
    //   파생 그림/아이콘은 Art_Source/ui_arfield/make_aac_greeting_art.py가 만든다.
    // 여러 번 실행해도 같은 결과가 되게(새로 만든 오브젝트는 이름으로 다시 찾는다) 만들었다.
    public static class ARFieldGreetingLayout
    {
        const string Generated = "Assets/_Project/Art/UI/Generated/";
        const string Derived = Generated + "derived/";
        const string BackgroundPath = Generated + "ui_aac_background_neon.png";
        const string PanelFramePath = Derived + "ui_aac_panel_frame_glass.png";
        const string CardFramePath = Derived + "ui_aac_card_frame_glass.png";
        const string CardBluePath = Generated + "ui_aac_card_blue.png";
        const string CardPurplePath = Generated + "ui_aac_card_purple.png";
        const string InfoBarPath = Generated + "ui_aac_info_bar.png";
        const string ArrowPath = Generated + "ui_neon_arrow_right_blue.png";
        const string RingPath = Derived + "ui_aac_icon_ring.png";
        const string InfoIconPath = Derived + "ui_aac_icon_info.png";

        // ===== 그림 크기 + 그림 속 흰 테두리 선 자리(px, 좌상단 기준) =====
        static readonly (Vector2 size, Rect line) PanelArt = (new Vector2(1728f, 910f), Rect.MinMaxRect(56.5f, 93.5f, 1671.5f, 816f));
        static readonly (Vector2 size, Rect line) CardFrameArt = (new Vector2(1942f, 809f), Rect.MinMaxRect(101f, 109.5f, 1841f, 694.5f));
        static readonly (Vector2 size, Rect line) CardBlueArt = (new Vector2(2172f, 724f), Rect.MinMaxRect(125f, 145f, 2047f, 569f));
        static readonly (Vector2 size, Rect line) CardPurpleArt = (new Vector2(2159f, 728f), Rect.MinMaxRect(138.5f, 163f, 2020f, 556.5f));
        static readonly (Vector2 size, Rect line) InfoBarArt = (new Vector2(2172f, 724f), Rect.MinMaxRect(179.5f, 261f, 1991.5f, 457f));
        static readonly Vector2 ArrowSize = new Vector2(1254f, 1254f);
        static readonly Rect ArrowBody = Rect.MinMaxRect(369f, 371f, 882f, 877f);   // 화살표 그림 속 파란 원

        // ===== 시안 속 자리 (보드 px = 시안 px) =====
        static readonly Vector2 BoardSize = new Vector2(1672f, 941f);
        static readonly Rect BoardRect = new Rect(Vector2.zero, BoardSize);
        const float SafeMargin = 32f;

        // 안내 바 자리: 바 테두리 선, 아이콘 가운데/크기, 구분선, 글 칸, 글 크기
        struct InfoLayout
        {
            public Rect line, divider, text;
            public Vector2 iconCenter;
            public float iconSize, textSize;
        }

        // 패널 아래쪽 자리: 큰 틀 테두리 선, 화면에 맞출 영역(큰 틀 양옆/위 ~ 맨 아래), 안내 바
        struct BottomLayout
        {
            public Rect panelLine, safe;
            public InfoLayout info;
        }

        // 시안 그대로(입력 줄 없음): 큰 틀 아래 선은 카드 틀 아래(764)와 안내 바 위(793) 사이
        static readonly BottomLayout Plain = new BottomLayout
        {
            panelLine = Rect.MinMaxRect(49.5f, 59f, 1625f, 785f),
            safe = Rect.MinMaxRect(49f, 59f, 1625f, 862f),
            info = new InfoLayout
            {
                line = Rect.MinMaxRect(507f, 793f, 1163f, 862f),
                iconCenter = new Vector2(630f, 827.5f), iconSize = 44f,
                divider = Rect.MinMaxRect(677f, 810f, 679f, 845f),
                text = Rect.MinMaxRect(703f, 793f, 1150f, 862f), textSize = 20f,
            },
        };

        // 변경점: 입력 줄이 있는 패널(인사/결제/편의): 카드 틀 아래에 입력 줄, 큰 틀은 그 아래까지, 안내 바는 조금 얇게 맨 아래로
        static readonly BottomLayout WithInput = new BottomLayout
        {
            panelLine = Rect.MinMaxRect(49.5f, 59f, 1625f, 876f),
            safe = Rect.MinMaxRect(49f, 59f, 1625f, 936f),
            info = new InfoLayout
            {
                line = Rect.MinMaxRect(532f, 888f, 1140f, 936f),
                iconCenter = new Vector2(645f, 912f), iconSize = 34f,
                divider = Rect.MinMaxRect(686f, 900f, 688f, 924f),
                text = Rect.MinMaxRect(708f, 888f, 1130f, 936f), textSize = 18f,
            },
        };
        // 입력 줄: 카드 틀 양끝(221.5 ~ 1449)에 맞춘다. 입력창 높이 80 (도움 화면 92의 비율로 아이콘/글자도 줄어든다)
        static readonly Rect InputRow = Rect.MinMaxRect(221.5f, 780f, 1449f, 860f);
        static readonly Rect InputBody = Rect.MinMaxRect(221.5f, 780f, 1226f, 860f);
        static readonly Rect SendBody = Rect.MinMaxRect(1238f, 780f, 1449f, 860f);

        static readonly Rect CardFrameLine = Rect.MinMaxRect(221.5f, 259.5f, 1449f, 763.5f);
        static readonly Rect CardAreaBox = Rect.MinMaxRect(266.5f, 286.5f, 1411.5f, 739.5f);
        static readonly Vector2 CardCell = new Vector2(558f, 138f);   // 카드 테두리 선 크기
        static readonly Vector2 CardSpacing = new Vector2(29f, 19.5f);
        // 카드 안(카드 좌상단 기준)
        static readonly Vector2 CardIconCenter = new Vector2(91f, 69f);
        const float CardRingSize = 122f, CardIconSize = 84f;
        static readonly Rect CardTextBox = Rect.MinMaxRect(182f, 0f, 452f, 138f);
        static readonly Vector2 CardArrowCenter = new Vector2(489f, 69f);
        const float CardArrowRing = 56f;

        static readonly Vector2 BackCenter = new Vector2(133.5f, 133.5f);
        const float BackRing = 74f, BackHit = 88f;
        static readonly Rect LogoBox = Rect.MinMaxRect(213f, 106f, 420f, 146f);
        static readonly Rect TitleBox = Rect.MinMaxRect(536f, 114f, 1136f, 194f);
        static readonly Rect SubtitleBox = Rect.MinMaxRect(436f, 198f, 1236f, 232f);

        // ===== 글자 (보드 px) =====
        const float LogoSize = 27f, LogoSpacing = 35f, TitleSize = 64f, SubtitleSize = 26f;
        const float CardTextMax = 32f, CardTextMin = 22f;
        static readonly Color TextWhite = new Color(0.96f, 0.98f, 1f, 1f);
        static readonly Color TextSub = new Color(0.86f, 0.9f, 1f, 0.95f);
        static readonly Color DividerColor = new Color(0.45f, 0.6f, 1f, 0.7f);

        // ===== 패널별 카드 모양: 아이콘, 보라 바탕, 문장 낱말. 낱말이 없는 문장은 카드 순서대로 돌려 쓴다. =====
        static readonly (string icon, bool purple, string[] keywords)[] GreetingStyles =
        {
            (Derived + "ui_aac_icon_hello.png", false, new[] { "안녕", "처음" }),
            (Derived + "ui_aac_icon_nice.png", true, new[] { "반가", "만나" }),
            (Derived + "ui_aac_icon_help.png", false, new[] { "도와", "도움", "부탁" }),
            (Derived + "ui_aac_icon_thanks.png", false, new[] { "감사", "고맙", "고마" }),
            (Derived + "ui_aac_icon_sorry.png", true, new[] { "죄송", "미안" }),
            (Derived + "ui_aac_icon_again.png", false, new[] { "다시", "천천히", "못 들" }),
        };
        // 변경점: 결제(PaymentPanel, CardGenerator 카테고리 '계산')
        static readonly (string icon, bool purple, string[] keywords)[] PaymentStyles =
        {
            (Derived + "ui_aac_icon_pay_card.png", false, new[] { "카드" }),
            (Derived + "ui_aac_icon_pay_cash.png", true, new[] { "현금", "거스름", "잔돈" }),
            (Derived + "ui_aac_icon_pay_receipt.png", false, new[] { "영수증" }),
            (Derived + "ui_aac_icon_pay_point.png", false, new[] { "포인트", "적립", "쿠폰", "할인", "멤버십" }),
            (Derived + "ui_aac_icon_pay_price.png", true, new[] { "얼마", "가격", "금액", "계산" }),
            (Derived + "ui_aac_icon_pay_bag.png", false, new[] { "봉투", "포장", "가방", "담아" }),
        };
        // 변경점: 편의(OtherPanel, CardGenerator 카테고리 '도움')
        static readonly (string icon, bool purple, string[] keywords)[] ConvenienceStyles =
        {
            (Derived + "ui_aac_icon_conv_restroom.png", false, new[] { "화장실" }),
            (Derived + "ui_aac_icon_conv_wifi.png", true, new[] { "와이파이", "인터넷", "비밀번호" }),
            (Derived + "ui_aac_icon_conv_charge.png", false, new[] { "충전", "콘센트", "배터리" }),
            (Derived + "ui_aac_icon_conv_water.png", false, new[] { "물 ", "물을", "물이", "컵", "빨대", "냅킨", "휴지" }),
            (Derived + "ui_aac_icon_conv_seat.png", true, new[] { "자리", "좌석", "앉" }),
            (Derived + "ui_aac_icon_conv_question.png", false, new[] { "도와", "어디", "모르", "천천히", "다시" }),
        };

        sealed class PanelSpec
        {
            public readonly string Name;
            public readonly (string icon, bool purple, string[] keywords)[] Styles;
            public readonly string[] Samples;   // 캡처용 예시 문장
            public readonly bool WithInput;
            public PanelSpec(string name, (string icon, bool purple, string[] keywords)[] styles, string[] samples, bool withInput)
            {
                Name = name;
                Styles = styles;
                Samples = samples;
                WithInput = withInput;
            }
        }

        static readonly PanelSpec[] Panels =
        {
            new PanelSpec("GreetingPanel", GreetingStyles,
                new[] { "안녕하세요", "반가워요", "도와주세요", "감사합니다", "죄송합니다", "다시 말해 주세요" }, true),
            new PanelSpec("PaymentPanel", PaymentStyles,
                new[] { "카드로 결제할게요", "현금으로 할게요", "영수증 주세요", "포인트 적립할게요", "얼마예요?", "봉투 주세요" }, true),
            new PanelSpec("OtherPanel", ConvenienceStyles,
                new[] { "화장실 어디예요?", "와이파이 비밀번호 알려주세요", "충전할 수 있나요?", "물 좀 주세요", "자리 있나요?", "도와주세요" }, true),
        };

        [MenuItem("Artti/Fix/ARField - AAC Panels Neon Design (Greeting + Payment + Other)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ARFieldGreetingLayout] Play 모드에서는 실행하지 않는다."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ARField)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
            }

            int done = 0;
            foreach (var spec in Panels)
            {
                if (ApplyPanel(spec)) done++;
                else Debug.LogError($"[ARFieldGreetingLayout] {spec.Name} 적용 실패 - 위 로그 확인");
            }
            if (done == 0) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ARFieldGreetingLayout] 완료 - AAC 문장 화면 {done}/{Panels.Length}개를 시안대로 맞춤, CardGenerator/버튼 연결은 그대로");
        }

        static bool ApplyPanel(PanelSpec spec)
        {
            var panel = FindPanel(spec.Name);
            if (panel == null) { Debug.LogError($"[ARFieldGreetingLayout] Canvas/{spec.Name} 없음"); return false; }
            var bottom = spec.WithInput ? WithInput : Plain;

            // ===== 배경(화면 cover) + 보드 =====
            var bg = Need(panel, "Background");
            var board = Need(panel, "NeonLayout");
            if (bg == null || board == null) return false;
            var bgImg = GetOrAdd<Image>(bg);
            bgImg.sprite = LoadSprite(BackgroundPath, 2048);
            bgImg.color = Color.white;
            GetOrAdd<CoverImageFitter>(bg).Setup(BoardSize.x / BoardSize.y, 1f, new Vector2(0.5f, 0.5f));
            GetOrAdd<BoardFitter>(board).Setup(BoardSize, bottom.safe, SafeMargin, true);

            // ===== 큰 틀 / 카드 틀 / 안내 바 =====
            var frame = Need(board, "PanelFrame");
            var cardFrame = Need(board, "CardFrame");
            var info = Need(board, "InfoBar");
            if (frame == null || cardFrame == null || info == null) return false;
            ArtImage(frame, board, PanelFramePath, PanelArt, bottom.panelLine, BoardRect);
            ArtImage(cardFrame, board, CardFramePath, CardFrameArt, CardFrameLine, BoardRect);
            if (!InfoBar(info, board, bottom.info)) return false;

            // ===== 머리: ARTTI / 제목 / 안내 글 / 뒤로 가기 (글 내용은 손배치 그대로) =====
            var logo = Need(board, "LogoText");
            var title = Need(board, "HeaderTitle");
            var subtitle = Need(board, "Subtitle");
            var back = Need(board, "Back");
            if (logo == null || title == null || subtitle == null || back == null) return false;
            Place(logo, board, LogoBox, BoardRect);
            Style(logo, LogoSize, false, TextWhite, TextAlignmentOptions.MidlineLeft).characterSpacing = LogoSpacing;
            Place(title, board, TitleBox, BoardRect);
            Style(title, TitleSize, true, TextWhite, TextAlignmentOptions.Center);
            Place(subtitle, board, SubtitleBox, BoardRect);
            Style(subtitle, SubtitleSize, false, TextSub, TextAlignmentOptions.Center);
            BackButton(back, board);

            // ===== 카드 자리(GridLayout) + 카드 템플릿 =====
            var area = Need(board, "CardArea");
            if (area == null) return false;
            Place(area, board, CardAreaBox, BoardRect);
            var grid = GetOrAdd<GridLayoutGroup>(area);
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.cellSize = CardCell;
            grid.spacing = CardSpacing;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            EditorUtility.SetDirty(grid);

            var template = FindTemplate(area, board);
            if (template == null) { Debug.LogError($"[ARFieldGreetingLayout] {spec.Name}: AACCardTemplate 없음"); return false; }
            if (!CardTemplate(template, spec.Styles)) return false;

            // ===== 문장 직접 입력 줄(WithInput인 패널) =====
            if (spec.WithInput)
            {
                var input = EnsureInputArea(board);
                if (input == null) return false;
                input.SetSiblingIndex(info.GetSiblingIndex() + 1);
                if (!ARFieldHelpDetailLayout.LayoutInputRow(input, board, BoardRect, InputRow, InputBody, SendBody)) return false;
            }
            return true;
        }

        static bool InfoBar(RectTransform info, RectTransform board, InfoLayout layout)
        {
            var infoText = Need(info, "InfoText");
            if (infoText == null) return false;
            var infoRect = ArtImage(info, board, InfoBarPath, InfoBarArt, layout.line, BoardRect);
            Place(infoText, info, layout.text, infoRect);
            Style(infoText, layout.textSize, false, TextSub, TextAlignmentOptions.MidlineLeft);
            var infoIcon = (RectTransform)info.Find("InfoIcon") ?? NewRect("InfoIcon", info);
            Place(infoIcon, info, Square(layout.iconCenter, layout.iconSize), infoRect);
            Icon(infoIcon, InfoIconPath);
            var divider = (RectTransform)info.Find("InfoDivider") ?? NewRect("InfoDivider", info);
            Place(divider, info, layout.divider, infoRect);
            var dividerImg = GetOrAdd<Image>(divider);
            dividerImg.sprite = null;
            dividerImg.color = DividerColor;
            dividerImg.raycastTarget = false;
            return true;
        }

        // 변경점: 패널의 입력 줄. 처음에는 도움 화면(HelpDetailPanel)의 TextInputArea를 복제한다.
        //   복제하면 안쪽 연결(입력 칸/전송/지우기 버튼)은 복제본끼리로, TTSManager 연결은 그대로 따라온다.
        static RectTransform EnsureInputArea(RectTransform board)
        {
            var area = board.Find("TextInputArea") as RectTransform;
            if (area != null) return area;
            var help = FindPanel("HelpDetailPanel");
            var src = help != null ? (help.Find("HelpBoard/TextInputArea") ?? help.Find("TextInputArea")) as RectTransform : null;
            if (src == null) { Debug.LogError("[ARFieldGreetingLayout] HelpDetailPanel의 TextInputArea 없음 - 도움 화면 Fix를 먼저 실행"); return null; }
            area = (RectTransform)Object.Instantiate(src.gameObject, board).transform;
            area.name = "TextInputArea";
            area.gameObject.SetActive(true);
            var field = area.GetComponentInChildren<TMP_InputField>(true);
            if (field != null) field.text = string.Empty;
            return area;
        }

        static bool CardTemplate(RectTransform template, (string icon, bool purple, string[] keywords)[] styleSet)
        {
            template.sizeDelta = CardCell;
            var cardRect = new Rect(Vector2.zero, CardCell);

            var blue = Need(template, "CardBackground");
            var icon = Need(template, "IconImage");
            var text = Need(template, "Text (TMP)");
            var arrow = Need(template, "ArrowImage");
            if (blue == null || icon == null || text == null || arrow == null) return false;

            ArtImage(blue, template, CardBluePath, CardBlueArt, cardRect, cardRect);
            blue.gameObject.SetActive(true);
            var purple = (RectTransform)template.Find("CardBackgroundPurple");
            if (purple == null)
            {
                purple = (RectTransform)Object.Instantiate(blue.gameObject, template).transform;
                purple.name = "CardBackgroundPurple";
            }
            ArtImage(purple, template, CardPurplePath, CardPurpleArt, cardRect, cardRect);
            purple.gameObject.SetActive(false);   // AACPhraseCardView가 문장에 맞춰 켠다

            var ring = (RectTransform)template.Find("IconRing") ?? NewRect("IconRing", template);
            Place(ring, template, Square(CardIconCenter, CardRingSize), cardRect);
            Icon(ring, RingPath);
            Place(icon, template, Square(CardIconCenter, CardIconSize), cardRect);
            Icon(icon, styleSet[0].icon);

            Place(text, template, CardTextBox, cardRect);
            var label = Style(text, CardTextMax, true, TextWhite, TextAlignmentOptions.MidlineLeft);
            label.enableAutoSizing = true;   // 긴 문장은 줄이고, 그래도 길면 두 줄
            label.fontSizeMin = CardTextMin;
            label.fontSizeMax = CardTextMax;
            label.textWrappingMode = TextWrappingModes.Normal;

            PlaceArrow(arrow, template, CardArrowCenter, CardArrowRing, cardRect);

            // 그리는 순서: 바탕(파랑/보라) -> 둘레 원 -> 아이콘 -> 글 -> 화살표
            blue.SetSiblingIndex(0);
            purple.SetSiblingIndex(1);
            ring.SetSiblingIndex(2);
            icon.SetSiblingIndex(3);
            text.SetSiblingIndex(4);
            arrow.SetSiblingIndex(5);

            var view = GetOrAdd<AACPhraseCardView>(template);
            var so = new SerializedObject(view);
            so.FindProperty("label").objectReferenceValue = text.GetComponent<TMP_Text>();
            so.FindProperty("icon").objectReferenceValue = icon.GetComponent<Image>();
            so.FindProperty("ring").objectReferenceValue = ring.GetComponent<Image>();
            so.FindProperty("blueBackground").objectReferenceValue = blue.gameObject;
            so.FindProperty("purpleBackground").objectReferenceValue = purple.gameObject;
            var styles = so.FindProperty("styles");
            styles.arraySize = styleSet.Length;
            for (int i = 0; i < styleSet.Length; i++)
            {
                var s = styles.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("icon").objectReferenceValue = LoadSprite(styleSet[i].icon, 256);
                s.FindPropertyRelative("purple").boolValue = styleSet[i].purple;
                var words = s.FindPropertyRelative("keywords");
                words.arraySize = styleSet[i].keywords.Length;
                for (int k = 0; k < styleSet[i].keywords.Length; k++)
                    words.GetArrayElementAtIndex(k).stringValue = styleSet[i].keywords[k];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // 뒤로 가기 = 다음 화살표를 좌우로 뒤집어 놓는다. 루트는 투명 누름 칸, 버튼 연결은 그대로.
        static void BackButton(RectTransform back, RectTransform board)
        {
            var backRect = Square(BackCenter, BackHit);
            Place(back, board, backRect, BoardRect);
            DisableOutline(back);
            var hit = GetOrAdd<Image>(back);
            hit.sprite = null;
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            foreach (Transform child in back)
                if (child.name != "ArrowImage") child.gameObject.SetActive(false);   // 예전 ← 글자
            var arrow = (RectTransform)back.Find("ArrowImage") ?? NewRect("ArrowImage", back);
            PlaceArrow(arrow, back, BackCenter, BackRing, backRect);
            arrow.pivot = new Vector2(0.5f, 0.5f);   // 가운데를 축으로 뒤집는다(Place는 좌상단 pivot)
            arrow.anchoredPosition += new Vector2(arrow.sizeDelta.x * 0.5f, -arrow.sizeDelta.y * 0.5f);
            arrow.localScale = new Vector3(-1f, 1f, 1f);
            if (back.TryGetComponent<Button>(out var button))
            {
                button.targetGraphic = arrow.GetComponent<Image>();
                EditorUtility.SetDirty(button);
            }
        }

        // 그림 속 흰 테두리 선(art.line)이 line(보드 px)에 오도록 그림 전체를 놓는다(가로세로 따로 늘림). 놓인 자리를 돌려준다.
        static Rect ArtImage(RectTransform rt, RectTransform parent, string path, (Vector2 size, Rect line) art, Rect line, Rect parentRect)
        {
            float kx = line.width / art.line.width, ky = line.height / art.line.height;
            var rect = new Rect(line.x - art.line.x * kx, line.y - art.line.y * ky, art.size.x * kx, art.size.y * ky);
            Place(rt, parent, rect, parentRect);
            var img = GetOrAdd<Image>(rt);
            img.sprite = LoadSprite(path, 2048);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
            img.raycastTarget = false;
            return rect;
        }

        // 화살표 그림 속 파란 원의 가운데를 center, 지름을 ring(보드 px)으로
        static void PlaceArrow(RectTransform rt, RectTransform parent, Vector2 center, float ring, Rect parentRect)
        {
            float k = ring / ArrowBody.width;
            Place(rt, parent, new Rect(center.x - ArrowBody.center.x * k, center.y - ArrowBody.center.y * k, ArrowSize.x * k, ArrowSize.y * k), parentRect);
            Icon(rt, ArrowPath, 512);
        }

        static void Icon(RectTransform rt, string path, int maxSize = 256)
        {
            rt.gameObject.SetActive(true);
            var img = GetOrAdd<Image>(rt);
            img.sprite = LoadSprite(path, maxSize);
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
            img.raycastTarget = false;   // 카드/버튼 루트가 눌리게
        }

        static Rect Square(Vector2 center, float size) => new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);

        static RectTransform FindPanel(string name)
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                var t = root.transform.Find(name);
                if (t != null) return (RectTransform)t;
            }
            return null;
        }

        // CardGenerator가 복제하는 템플릿(없으면 보드 아래 AACCardTemplate)
        static RectTransform FindTemplate(RectTransform area, RectTransform board)
        {
            var gen = area.GetComponent<CardGenerator>();
            if (gen != null && new SerializedObject(gen).FindProperty("cardPrefab").objectReferenceValue is GameObject go)
                return (RectTransform)go.transform;
            return board.Find("AACCardTemplate") as RectTransform;
        }

        static RectTransform Need(Transform root, string name)
        {
            var t = root.Find(name) as RectTransform;
            if (t == null) Debug.LogError($"[ARFieldGreetingLayout] {root.name} 아래 {name} 없음");
            return t;
        }

        // 세 화면은 실행 중에만 켜지고 카드는 Gemini가 만든다. 패널마다 켜고 예시 문장 6장을 템플릿으로 채워 태블릿·폰·16:9로 찍는다.
        // 결과: Temp/LayoutCaptures/<tab|phone|169>_<패널 이름>.png. 씬은 저장하지 않는다.
        [MenuItem("Artti/Debug/Capture ARField AAC Panels - Tab + Phone + 16x9")]
        static void CapturePanels()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = EditorSceneManager.GetActiveScene().path;
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            try
            {
                foreach (var spec in Panels)
                {
                    foreach (var (w, h, tag) in new[] { (2560, 1600, "tab"), (2400, 1080, "phone"), (1920, 1080, "169") })
                    {
                        EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
                        var panel = FindPanel(spec.Name);
                        if (panel == null) { Debug.LogError($"[ARFieldGreetingLayout] Canvas/{spec.Name} 없음"); break; }
                        foreach (Transform sib in panel.parent)
                            if (sib is RectTransform) sib.gameObject.SetActive(sib == panel);
                        var board = panel.Find("NeonLayout") as RectTransform;
                        var area = board != null ? board.Find("CardArea") as RectTransform : null;
                        var template = area != null ? FindTemplate(area, board) : null;
                        if (template != null)
                        {
                            for (int i = area.childCount - 1; i >= 0; i--) Object.DestroyImmediate(area.GetChild(i).gameObject);
                            for (int i = 0; i < spec.Samples.Length; i++)
                            {
                                var card = Object.Instantiate(template.gameObject, area);
                                card.SetActive(true);
                                if (card.TryGetComponent<AACPhraseCardView>(out var view)) view.Show(spec.Samples[i], i);
                            }
                        }
                        string file = Path.Combine(outDir, $"{tag}_{spec.Name}.png");
                        DeviceLayoutCapture.CaptureActiveScene(w, h, file);
                        DeviceLayoutCapture.CaptureActiveScene(w, h, file);   // 첫 번은 캔버스 크기 맞추기
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            Debug.Log($"[ARFieldGreetingLayout] AAC 문장 화면 캡처 -> {outDir}");
        }
    }
}
