using System.Collections.Generic;
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
    // AR 현장모드 주문하기 화면(ARFieldScene / Canvas/OrderPanel)을 주문하기 시안(1672x941, 한 화면)처럼 바꾼다.
    //   예전: 온도(TempPanel) -> 음료(MenuPanel) -> 사이즈(SizePanel)를 한 화면씩 넘기고, 사이즈를 고르면 바로 말했다.
    //   지금: 큰 틀 안에 1 온도 / 2 메뉴 / 3 사이즈 + '주문 완료하기'를 한 화면에 두고, 아래에 문장 직접 입력 줄을 둔다.
    //         OrderManager.singlePage를 켜서 고른 카드만 선택 표시(OrderChoiceView)하고 '주문 완료하기'에서 주문 문장을 말한다.
    // OrderPanel은 손배치 씬이라 다시 만들지 않는다. 버튼(Hot/Ice/Mild, Drink*, Small/Medium/Large, Back)은 OnClick 연결째 새 자리로 옮기고,
    // 예전 단계 패널/장식은 지우지 않고 끈다.
    //   보드: 시안 px 그대로(OrderBoard, 1672x941). BoardFitter(fillSafeRect)가 큰 틀을 여백 32px만 남기고 화면에 맞춘다.
    //   그림: Art_Source/ui_arfield/make_order_art.py(카드 유리/배지/버튼), 입력 줄은 도움 화면 TextInputArea를 복제.
    //   음료 아이콘: Generated/ui_order_icon_drink_<americano|latte|cappuccino|orange|lemonade|choco>.png가 있으면 쓰고,
    //               없으면 임시 아이콘(네온 컵). 배경: Generated/ui_order_background_cafe.png가 있으면 쓰고 없으면 ui_aac_background_neon.
    // 여러 번 실행해도 같은 결과가 되게(새로 만든 오브젝트는 이름으로 다시 찾는다) 만들었다.
    public static class ARFieldOrderLayout
    {
        const string Generated = "Assets/_Project/Art/UI/Generated/";
        const string Derived = Generated + "derived/";
        const string CafeBackgroundPath = Generated + "ui_order_background_cafe.png";   // 있으면 사용(시안의 카페 배경)
        const string FallbackBackgroundPath = Generated + "ui_aac_background_neon.png";
        // 변경점: 큰 틀/단계 틀은 선이 하나인 주문 화면 전용 틀(인사 화면 큰 틀과 카테고리 틀은 안쪽 선이 하나 더 있는 이중 테두리)
        const string PanelFramePath = Derived + "ui_order_panel_frame.png";             // 9-slice, 가장자리 112px, 바깥 빛 12(보드)
        const string SectionFramePath = Derived + "ui_order_section_frame.png";         // 9-slice, 가장자리 112px, 바깥 빛 12(보드)
        const string GlassBlue = Derived + "ui_order_glass_blue.png";
        const string GlassGlow = Derived + "ui_order_glass_glow.png";
        const string BadgePath = Derived + "ui_order_badge.png";
        const string CompletePath = Derived + "ui_order_complete.png";
        const string TitleIconPath = Derived + "ui_order_title_icon.png";
        const string ArrowIconPath = Derived + "ui_order_icon_back.png";
        const string DrinkPlaceholderPath = Derived + "ui_helpdetail_icon_coffee.png";
        const string DrinkIconFormat = Generated + "ui_order_icon_drink_{0}.png";

        // ===== 9-slice 그림 규격 (make_order_art.py와 같은 값, 그림 px. 보드에는 Image.pixelsPerUnitMultiplier=2로 절반 크기) =====
        const float GlassBorderPx = 88f, GlassGlow9 = 10f;     // 카드: 가장자리 88px, 테두리 선 밖 빛 10(보드)
        const float BadgeBorderPx = 32f, BadgeGlow9 = 4f;
        const float SectionBorderPx = 112f, SectionGlow9 = 12f;   // 큰 틀도 같은 규격
        static readonly Vector2 CompleteArt = new Vector2(754f, 208f);   // 주문 완료 그림 전체, 선은 안쪽 26px
        const float CompleteOut = 26f;
        const float TitleIconDisc = 352f / 512f;                         // 제목 아이콘 그림 폭 대비 파란 원 지름

        // ===== 시안 속 자리 (보드 px = 시안 px, 좌상단 기준) =====
        static readonly Vector2 BoardSize = new Vector2(1672f, 941f);
        static readonly Rect BoardRect = new Rect(Vector2.zero, BoardSize);
        static readonly Rect PanelLine = Rect.MinMaxRect(139f, 41f, 1533f, 908f);   // 큰 틀 = 화면에 맞출 영역
        const float SafeMargin = 32f;

        // 변경점: '돌아가기' 알약 대신 다른 탭과 같은 파란 원 뒤로 가기 + 같은 줄 오른쪽 끝에 홈/종료(ARFieldGreetingLayout과 같은 버튼).
        //   큰 틀 왼쪽/오른쪽 선(139/1533)에서 84 안쪽이 원 가운데, 원 지름 74, 누름 칸 88, 원 사이 20.
        static readonly Vector2 BackCenter = new Vector2(223f, 106.5f);
        static readonly Vector2 HomeCenter = new Vector2(1355f, 106.5f);
        static readonly Vector2 ExitCenter = new Vector2(1449f, 106.5f);
        const float NavRing = 74f, NavHit = 88f;
        static readonly Vector2 TitleIconCenter = new Vector2(670.5f, 102f);
        const float TitleIconDiameter = 84f;
        static readonly Rect TitleBox = Rect.MinMaxRect(736f, 52f, 1180f, 150f);
        static readonly Rect SubtitleBox = Rect.MinMaxRect(440f, 148f, 1240f, 180f);
        const string SubtitleText = "원하는 음료를 선택하고, 나만의 한 잔을 주문해보세요.";

        // 단계 틀(테두리 선), 번호 배지, 단계 제목
        static readonly Rect[] SectionLines =
        {
            Rect.MinMaxRect(167f, 191f, 1507f, 394.5f),
            Rect.MinMaxRect(167f, 410f, 1507f, 629.5f),
            Rect.MinMaxRect(167f, 645f, 1507f, 800.5f),
        };
        static readonly string[] SectionTitles = { "온도를 골라주세요", "메뉴를 선택해주세요", "사이즈를 골라주세요" };
        static readonly Vector2 BadgeOffset = new Vector2(32f, 3.5f);   // 단계 틀 좌상단 -> 배지 좌상단
        static readonly Vector2 BadgeSize = new Vector2(57.5f, 37f);
        const float SectionTitleX = 280f;

        // 1 온도: 카드 테두리 선(보드 px), 카드 안 아이콘 가운데/보이는 높이, 글 칸(카드 좌상단 기준)
        static readonly Rect[] TempCards = { Rect.MinMaxRect(200f, 242.5f, 613f, 385f), Rect.MinMaxRect(632f, 242.5f, 1041f, 385f), Rect.MinMaxRect(1061f, 242.5f, 1477f, 385f) };
        static readonly Vector2 TempIconCenter = new Vector2(147f, 71f);
        const float TempIconHeight = 118f;
        static readonly Rect TempTextBox = Rect.MinMaxRect(205f, 0f, 375f, 142.5f);
        // 2 메뉴: 여섯 칸(198 ~ 1479, 사이 14)
        const float MenuX0 = 198f, MenuX1 = 1479f, MenuGap = 14f, MenuY0 = 463.5f, MenuY1 = 621f;
        static readonly Vector2 MenuIconCenter = new Vector2(101f, 64f);
        const float MenuIconHeight = 100f;
        static readonly Rect MenuTextBox = Rect.MinMaxRect(4f, 116f, 198f, 154f);
        // 3 사이즈 + 주문 완료
        static readonly Rect[] SizeCards = { Rect.MinMaxRect(198f, 688f, 487f, 795f), Rect.MinMaxRect(502f, 688f, 791f, 795f), Rect.MinMaxRect(806f, 688f, 1097f, 795f) };
        static readonly float[] SizeIconHeights = { 78f, 87f, 92f };   // 작게 < 보통 < 크게
        static readonly Vector2 SizeIconCenter = new Vector2(107f, 56f);
        static readonly Rect SizeTextBox = Rect.MinMaxRect(140f, 0f, 248f, 107f);
        static readonly Rect CompleteLine = Rect.MinMaxRect(1134f, 706f, 1485f, 784f);
        static readonly Rect CompleteTextBox = Rect.MinMaxRect(1180f, 706f, 1410f, 784f);
        static readonly Vector2 CompleteArrowCenter = new Vector2(1436f, 745f);
        const float CompleteArrowSize = 40f;
        // 입력 줄
        static readonly Rect InputRow = Rect.MinMaxRect(164f, 817f, 1507f, 894f);   // 변경점: 전송 버튼 없이 입력창이 줄 전체

        // ===== 글자 (보드 px) =====
        const float TitleSize = 80f, SubtitleSize = 24f, BadgeTextSize = 26f, SectionTitleSize = 30f;
        const float TempTextSize = 38f, MenuTextSize = 25f, SizeTextSize = 32f, CompleteTextSize = 30f;
        static readonly Color TextWhite = new Color(0.97f, 0.98f, 1f, 1f);
        static readonly Color TextSub = new Color(0.86f, 0.9f, 1f, 0.95f);

        // 버튼 이름 -> (아이콘, 보이는 글). 변경점: 카드는 모두 같은 파란 유리(GlassBlue), 고르면 빛 테두리만 켜진다.
        static readonly Dictionary<string, (string icon, string label)> Temps = new Dictionary<string, (string, string)>
        {
            { "Hot", (Generated + "ui_order_icon_warm.png", "따뜻한") },
            { "Ice", (Generated + "ui_order_icon_cold.png", "차가운") },
            { "Mild", (Generated + "ui_order_icon_mild.png", "미지근한") },
        };
        static readonly (string name, string key)[] Drinks =
        {
            ("DrinkAmericano", "americano"), ("DrinkLatte", "latte"), ("DrinkCappuccino", "cappuccino"),
            ("DrinkOrange", "orange"), ("DrinkLemonade", "lemonade"), ("DrinkChoco", "choco"),
        };
        static readonly (string name, string icon, string label)[] Sizes =
        {
            ("Small", Generated + "ui_order_size_icon_small.png", "작게"),
            ("Medium", Generated + "ui_order_size_icon_medium.png", "보통"),
            ("Large", Generated + "ui_order_size_icon_large.png", "크게"),
        };

        [MenuItem("Artti/Fix/ARField - Order Panel One Page Design (keep hand placement)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ARFieldOrderLayout] Play 모드에서는 실행하지 않는다."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ARField)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
            }
            var panel = FindPanel("OrderPanel");
            if (panel == null) { Debug.LogError("[ARFieldOrderLayout] Canvas/OrderPanel 없음"); return; }
            var manager = panel.GetComponent<OrderManager>();
            if (manager == null) { Debug.LogError("[ARFieldOrderLayout] OrderPanel에 OrderManager 없음"); return; }

            // 예전 단계 패널 안의 버튼을 먼저 찾아 둔다(옮기면 경로가 바뀐다)
            var buttons = new Dictionary<string, RectTransform>();
            foreach (var t in panel.GetComponentsInChildren<RectTransform>(true))
                if (t.GetComponent<Button>() != null && !buttons.ContainsKey(t.name)) buttons[t.name] = t;
            var title = FindDeep(panel, "HeaderTitle");
            var titleIcon = FindDeep(panel, "OrderHeaderIcon");
            if (title == null) { Debug.LogError("[ARFieldOrderLayout] HeaderTitle 없음"); return; }
            var titleText = title.GetComponent<TMP_Text>();
            var font = titleText != null ? titleText.font : null;

            // ===== 배경(화면 cover) + 보드 =====
            var bg = Need(panel, "OrderBackground");
            if (bg == null) return;
            var bgImg = GetOrAdd<Image>(bg);
            bool cafe = File.Exists(CafeBackgroundPath);
            bgImg.sprite = LoadSprite(cafe ? CafeBackgroundPath : FallbackBackgroundPath, 2048);
            bgImg.color = Color.white;
            bgImg.preserveAspect = false;
            var bgSize = bgImg.sprite != null ? bgImg.sprite.rect.size : BoardSize;
            GetOrAdd<CoverImageFitter>(bg).Setup(bgSize.x / bgSize.y, 1f, new Vector2(0.5f, 0.5f));
            bg.SetAsFirstSibling();

            var board = (RectTransform)panel.Find("OrderBoard") ?? NewRect("OrderBoard", panel);
            board.SetSiblingIndex(1);
            GetOrAdd<BoardFitter>(board).Setup(BoardSize, PanelLine, SafeMargin, true);

            var frame = (RectTransform)board.Find("PanelFrame") ?? NewRect("PanelFrame", board);
            frame.SetAsFirstSibling();
            Place(frame, board, Expand(PanelLine, SectionGlow9), BoardRect);
            SlicedImage(frame, PanelFramePath, SectionBorderPx);

            // ===== 머리: 돌아가기 / 제목 아이콘 / 제목 / 안내 글 =====
            if (buttons.TryGetValue("Back", out var back)) ARFieldGreetingLayout.ArrowBackButton(back, board, BoardRect, BackCenter, NavRing, NavHit);
            else Debug.LogWarning("[ARFieldOrderLayout] Back 버튼 없음");
            ARFieldGreetingLayout.NavButtons(board, BoardRect, HomeCenter, ExitCenter, NavRing, NavHit);
            title.SetParent(board, false);
            Place(title, board, TitleBox, BoardRect);
            Style(title, TitleSize, true, TextWhite, TextAlignmentOptions.MidlineLeft).text = "주문하기";
            if (titleIcon != null)
            {
                titleIcon.SetParent(board, false);
                float size = TitleIconDiameter / TitleIconDisc;
                Place(titleIcon, board, Square(TitleIconCenter, size), BoardRect);
                SimpleImage(titleIcon, TitleIconPath, true, 256);
            }
            var subtitle = (RectTransform)board.Find("Subtitle") ?? NewText("Subtitle", board, font);
            Place(subtitle, board, SubtitleBox, BoardRect);
            Style(subtitle, SubtitleSize, false, TextSub, TextAlignmentOptions.Center).text = SubtitleText;

            // ===== 단계 틀 1~3 =====
            var sections = new RectTransform[3];
            for (int i = 0; i < 3; i++) sections[i] = Section(board, i, font);

            // 1 온도
            for (int i = 0; i < 3; i++)
            {
                string name = i == 0 ? "Hot" : i == 1 ? "Ice" : "Mild";
                if (!buttons.TryGetValue(name, out var card)) { Debug.LogError($"[ARFieldOrderLayout] {name} 버튼 없음"); return; }
                var t = Temps[name];
                Choice(card, sections[0], TempCards[i], OrderChoiceGroup.Temperature, t.icon, TempIconCenter, TempIconHeight, TempTextBox, t.label, TempTextSize, font);
            }
            // 2 메뉴
            float menuW = (MenuX1 - MenuX0 - MenuGap * (Drinks.Length - 1)) / Drinks.Length;
            var missingDrinks = new List<string>();
            for (int i = 0; i < Drinks.Length; i++)
            {
                if (!buttons.TryGetValue(Drinks[i].name, out var card)) { Debug.LogError($"[ARFieldOrderLayout] {Drinks[i].name} 버튼 없음"); return; }
                string icon = string.Format(DrinkIconFormat, Drinks[i].key);
                if (!File.Exists(icon)) { missingDrinks.Add(Path.GetFileName(icon)); icon = DrinkPlaceholderPath; }
                float x = MenuX0 + i * (menuW + MenuGap);
                Choice(card, sections[1], Rect.MinMaxRect(x, MenuY0, x + menuW, MenuY1), OrderChoiceGroup.Drink,
                    icon, MenuIconCenter, MenuIconHeight, MenuTextBox, null, MenuTextSize, font);
            }
            // 3 사이즈 + 주문 완료
            for (int i = 0; i < Sizes.Length; i++)
            {
                if (!buttons.TryGetValue(Sizes[i].name, out var card)) { Debug.LogError($"[ARFieldOrderLayout] {Sizes[i].name} 버튼 없음"); return; }
                Choice(card, sections[2], SizeCards[i], OrderChoiceGroup.Size,
                    Sizes[i].icon, SizeIconCenter, SizeIconHeights[i], SizeTextBox, Sizes[i].label, SizeTextSize, font);
            }
            var complete = CompleteButton(sections[2], font);

            // ===== 문장 직접 입력 줄(도움 화면 TextInputArea 복제) =====
            var input = EnsureInputArea(board);
            if (input == null) return;
            input.SetAsLastSibling();
            if (!ARFieldHelpDetailLayout.LayoutInputRow(input, board, BoardRect, InputRow)) return;
            if (!ARFieldHelpDetailLayout.ChatDrawer(panel, board, input)) return;   // 변경점: 누르면 오른쪽 채팅창

            // 그리는 순서: 큰 틀 -> 머리 -> 단계 틀 -> 입력 줄
            if (buttons.TryGetValue("Back", out back)) back.SetSiblingIndex(1);

            // ===== 예전 단계 패널/장식은 끈다(지우지 않음) =====
            foreach (var name in new[] { "DecorationLayer", "TempPanel", "MenuPanel", "SizePanel", "Scroll View" })
            {
                var old = panel.Find(name);
                if (old != null) old.gameObject.SetActive(false);
            }

            // ===== OrderManager: 한 화면 모드 + 주문 완료 버튼 =====
            var so = new SerializedObject(manager);
            so.FindProperty("singlePage").boolValue = true;
            so.FindProperty("completeButton").objectReferenceValue = complete;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ARFieldOrderLayout] 완료 - 주문하기를 한 화면 시안으로 맞춤(버튼 OnClick 연결 그대로)" +
                      (cafe ? "" : $"\n  배경: {Path.GetFileName(CafeBackgroundPath)} 없음 -> 네온 도시 배경 사용") +
                      (missingDrinks.Count == 0 ? "" : $"\n  음료 아이콘 없음 -> 임시 아이콘 사용: {string.Join(", ", missingDrinks)}"));
        }

        // 단계 틀 하나: Frame(9-slice) + Badge(번호) + Title. 컨테이너 자리 = 틀 그림 자리(선 + 바깥 빛)
        static RectTransform Section(RectTransform board, int index, TMP_FontAsset font)
        {
            string name = $"Section{index + 1}";
            var line = SectionLines[index];
            var rect = Expand(line, SectionGlow9);
            var sec = (RectTransform)board.Find(name) ?? NewRect(name, board);
            Place(sec, board, rect, BoardRect);

            var frame = (RectTransform)sec.Find("Frame") ?? NewRect("Frame", sec);
            frame.SetAsFirstSibling();
            Place(frame, sec, rect, rect);
            SlicedImage(frame, SectionFramePath, SectionBorderPx);

            var badgeLine = new Rect(line.x + BadgeOffset.x, line.y + BadgeOffset.y, BadgeSize.x, BadgeSize.y);
            var badgeRect = Expand(badgeLine, BadgeGlow9);
            var badge = (RectTransform)sec.Find("Badge") ?? NewRect("Badge", sec);
            Place(badge, sec, badgeRect, rect);
            SlicedImage(badge, BadgePath, BadgeBorderPx);
            var number = (RectTransform)badge.Find("Number") ?? NewText("Number", badge, font);
            Place(number, badge, badgeLine, badgeRect);
            Style(number, BadgeTextSize, true, TextWhite, TextAlignmentOptions.Center).text = (index + 1).ToString();

            var title = (RectTransform)sec.Find("Title") ?? NewText("Title", sec, font);
            Place(title, sec, Rect.MinMaxRect(SectionTitleX, badgeLine.y - 6f, SectionTitleX + 520f, badgeLine.yMax + 6f), rect);
            Style(title, SectionTitleSize, true, TextWhite, TextAlignmentOptions.MidlineLeft).text = SectionTitles[index];
            return sec;
        }

        // 선택 카드 하나: 루트 = 파란 유리 바탕(9-slice) + Button, 자식 = 아이콘 / 글 / 선택 빛 테두리. OnClick 연결은 그대로.
        static void Choice(RectTransform card, RectTransform section, Rect line, OrderChoiceGroup group,
            string iconPath, Vector2 iconCenter, float iconHeight, Rect textBox, string label, float textSize, TMP_FontAsset font)
        {
            var sectionRect = new Rect(section.anchoredPosition.x, -section.anchoredPosition.y, section.sizeDelta.x, section.sizeDelta.y);
            var rect = Expand(line, GlassGlow9);
            Place(card, section, rect, sectionRect);
            DisableOutline(card);
            if (card.TryGetComponent<Mask>(out var mask)) mask.enabled = false;
            foreach (var layout in card.GetComponents<LayoutGroup>()) layout.enabled = false;
            var bg = SlicedImage(card, GlassBlue, GlassBorderPx);
            bg.raycastTarget = true;
            var button = card.GetComponent<Button>();
            if (button != null) { button.targetGraphic = bg; EditorUtility.SetDirty(button); }
            string value = FixClickArgument(button);

            // 아이콘: 예전 아이콘 자식(IconImage1/MildIcon)이 있으면 그것, 없으면 새로
            RectTransform icon = null;
            foreach (Transform c in card)
                if (c.GetComponent<Image>() != null && c.GetComponent<TMP_Text>() == null && c.name != "SelectedGlow") { icon = (RectTransform)c; break; }
            if (icon == null) icon = NewRect("Icon", card);
            PlaceByBody(icon, card, iconPath, new Vector2(line.x + iconCenter.x, line.y + iconCenter.y), iconHeight, rect);

            var text = card.GetComponentInChildren<TMP_Text>(true) is TMP_Text t ? (RectTransform)t.transform : NewText("Label", card, font);
            text.gameObject.SetActive(true);
            Place(text, card, Rect.MinMaxRect(line.x + textBox.xMin, line.y + textBox.yMin, line.x + textBox.xMax, line.y + textBox.yMax), rect);
            var tmp = Style(text, textSize, true, TextWhite, TextAlignmentOptions.Center);
            tmp.text = label ?? value;
            tmp.enableAutoSizing = true;   // 긴 이름은 줄여서 한 줄
            tmp.fontSizeMin = textSize * 0.7f;
            tmp.fontSizeMax = textSize;

            var glow = (RectTransform)card.Find("SelectedGlow") ?? NewRect("SelectedGlow", card);
            Place(glow, card, rect, rect);
            var glowImg = SlicedImage(glow, GlassGlow, GlassBorderPx);
            glowImg.raycastTarget = false;
            glow.SetAsLastSibling();
            glow.gameObject.SetActive(false);

            var view = GetOrAdd<OrderChoiceView>(card);
            var so = new SerializedObject(view);
            so.FindProperty("group").enumValueIndex = (int)group;
            so.FindProperty("value").stringValue = value;
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("normalSprite").objectReferenceValue = bg.sprite;
            so.FindProperty("selectedSprite").objectReferenceValue = null;   // 고르면 색은 그대로, 빛 테두리(SelectedGlow)만
            so.FindProperty("selectedGlow").objectReferenceValue = glow.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 버튼 OnClick이 OrderManager에 넘기는 글(예: "따뜻한"). 앞뒤 빈칸이 있으면 지워 저장한다("중간 " -> "중간").
        static string FixClickArgument(Button button)
        {
            if (button == null) return "";
            var so = new SerializedObject(button);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            for (int i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                string method = call.FindPropertyRelative("m_MethodName").stringValue;
                if (!method.StartsWith("Select")) continue;
                var arg = call.FindPropertyRelative("m_Arguments.m_StringArgument");
                string trimmed = arg.stringValue.Trim();
                if (trimmed != arg.stringValue)
                {
                    arg.stringValue = trimmed;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                return trimmed;
            }
            return "";
        }

        static Button CompleteButton(RectTransform section, TMP_FontAsset font)
        {
            var sectionRect = new Rect(section.anchoredPosition.x, -section.anchoredPosition.y, section.sizeDelta.x, section.sizeDelta.y);
            var k = CompleteLine.width / (CompleteArt.x - 2f * CompleteOut);
            var rect = new Rect(CompleteLine.x - CompleteOut * k, CompleteLine.y - CompleteOut * (CompleteLine.height / (CompleteArt.y - 2f * CompleteOut)),
                CompleteArt.x * k, CompleteArt.y * (CompleteLine.height / (CompleteArt.y - 2f * CompleteOut)));
            var root = (RectTransform)section.Find("CompleteButton") ?? NewRect("CompleteButton", section);
            Place(root, section, rect, sectionRect);
            var img = SimpleImage(root, CompletePath, false);
            img.raycastTarget = true;
            var button = GetOrAdd<Button>(root);
            button.targetGraphic = img;
            var label = (RectTransform)root.Find("Label") ?? NewText("Label", root, font);
            Place(label, root, CompleteTextBox, rect);
            Style(label, CompleteTextSize, true, TextWhite, TextAlignmentOptions.Center).text = "주문 완료하기";
            var arrow = (RectTransform)root.Find("Arrow") ?? NewRect("Arrow", root);
            Place(arrow, root, Square(CompleteArrowCenter, CompleteArrowSize), rect);
            SimpleImage(arrow, ArrowIconPath, true, 256);
            arrow.pivot = new Vector2(0.5f, 0.5f);   // 왼쪽 화살표를 좌우로 뒤집어 오른쪽으로
            arrow.anchoredPosition += new Vector2(arrow.sizeDelta.x * 0.5f, -arrow.sizeDelta.y * 0.5f);
            arrow.localScale = new Vector3(-1f, 1f, 1f);
            EditorUtility.SetDirty(button);
            return button;
        }

        // 도움 화면(HelpDetailPanel)의 TextInputArea를 복제(입력 칸/전송/지우기 연결은 복제본끼리, TTSManager는 그대로)
        static RectTransform EnsureInputArea(RectTransform board)
        {
            var area = board.Find("TextInputArea") as RectTransform;
            if (area != null) return area;
            var help = FindPanel("HelpDetailPanel");
            var src = help != null ? (help.Find("HelpBoard/TextInputArea") ?? help.Find("TextInputArea")) as RectTransform : null;
            if (src == null) { Debug.LogError("[ARFieldOrderLayout] HelpDetailPanel의 TextInputArea 없음 - 도움 화면 Fix를 먼저 실행"); return null; }
            area = (RectTransform)Object.Instantiate(src.gameObject, board).transform;
            area.name = "TextInputArea";
            area.gameObject.SetActive(true);
            var field = area.GetComponentInChildren<TMP_InputField>(true);
            if (field != null) field.text = string.Empty;
            return area;
        }

        // 아이콘을 보이는 부분(알파 > 40) 기준으로 가운데 center, 높이 height(보드 px)에 맞춘다(가로세로 같은 배율)
        static void PlaceByBody(RectTransform rt, RectTransform parent, string path, Vector2 center, float height, Rect parentRect)
        {
            var (size, body) = OpaqueBounds(path);
            float k = height / body.height;
            Place(rt, parent, new Rect(center.x - body.center.x * k, center.y - body.center.y * k, size.x * k, size.y * k), parentRect);
            SimpleImage(rt, path, true, size.x <= 256f ? 256 : 1024);
        }

        static readonly Dictionary<string, (Vector2, Rect)> BoundsCache = new Dictionary<string, (Vector2, Rect)>();

        // PNG 파일을 직접 읽어 보이는 부분의 범위(그림 px, 좌상단 기준)를 구한다. 임포트 설정(Read/Write)과 상관없다.
        static (Vector2 size, Rect body) OpaqueBounds(string path)
        {
            if (BoundsCache.TryGetValue(path, out var cached)) return cached;
            var tex = new Texture2D(2, 2);
            try
            {
                tex.LoadImage(File.ReadAllBytes(path));
                var px = tex.GetPixels32();
                int w = tex.width, h = tex.height, x0 = w, x1 = -1, y0 = h, y1 = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 40)
                        {
                            if (x < x0) x0 = x;
                            if (x > x1) x1 = x;
                            if (y < y0) y0 = y;
                            if (y > y1) y1 = y;
                        }
                var size = new Vector2(w, h);
                // GetPixels는 아래가 y=0이라 좌상단 기준으로 바꾼다
                var body = x1 < 0 ? new Rect(Vector2.zero, size) : Rect.MinMaxRect(x0, h - 1 - y1, x1 + 1, h - y0);
                return BoundsCache[path] = (size, body);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        // maxSize는 같은 그림을 쓰는 다른 Fix 스크립트와 같게 둔다(다르면 실행할 때마다 다시 임포트된다)
        static Image SimpleImage(RectTransform rt, string path, bool preserve, int maxSize = 1024)
        {
            rt.gameObject.SetActive(true);
            var img = GetOrAdd<Image>(rt);
            img.sprite = LoadSprite(path, maxSize);
            img.type = Image.Type.Simple;
            img.preserveAspect = preserve;
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        static Image SlicedImage(RectTransform rt, string path, float borderPx)
        {
            var img = GetOrAdd<Image>(rt);
            img.sprite = LoadSlicedSprite(path, borderPx);
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = 2f;   // 9-slice 그림은 보드 px의 2배로 그렸다
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        static Sprite LoadSlicedSprite(string path, float border)
        {
            LoadSprite(path, 1024);
            var b = new Vector4(border, border, border, border);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.spriteBorder != b)
            {
                ti.spriteBorder = b;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static RectTransform NewText(string name, RectTransform parent, TMP_FontAsset font)
        {
            var rt = NewRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            return rt;
        }

        static Rect Expand(Rect r, float d) => Rect.MinMaxRect(r.xMin - d, r.yMin - d, r.xMax + d, r.yMax + d);
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

        static RectTransform Need(Transform root, string name)
        {
            var t = root.Find(name) as RectTransform;
            if (t == null) Debug.LogError($"[ARFieldOrderLayout] {root.name} 아래 {name} 없음");
            return t;
        }

        // 주문 화면은 실행 중에만 켜진다. 켜고 '따뜻한' / '아메리카노' / '크게'를 고른 모양으로 태블릿·폰·16:9로 찍는다. 씬은 저장하지 않는다.
        [MenuItem("Artti/Debug/Capture ARField Order - Tab + Phone + 16x9")]
        static void CaptureOrder()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = EditorSceneManager.GetActiveScene().path;
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            try
            {
                foreach (var (w, h, tag) in new[] { (2560, 1600, "tab"), (2400, 1080, "phone"), (1920, 1080, "169") })
                {
                    EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
                    var panel = FindPanel("OrderPanel");
                    if (panel == null) { Debug.LogError("[ARFieldOrderLayout] Canvas/OrderPanel 없음"); return; }
                    foreach (Transform sib in panel.parent)
                        if (sib is RectTransform) sib.gameObject.SetActive(sib == panel);
                    foreach (var view in panel.GetComponentsInChildren<OrderChoiceView>(true))
                        view.SetSelected(view.Value == "따뜻한" || view.Value == "아메리카노" || view.Value == "큰");
                    string file = Path.Combine(outDir, $"{tag}_ARFieldOrder.png");
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);   // 첫 번은 캔버스 크기 맞추기
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            Debug.Log($"[ARFieldOrderLayout] 주문 화면 캡처 -> {outDir}");
        }
    }
}
