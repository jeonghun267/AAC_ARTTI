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
    // AR 현장모드 도움 화면(ARFieldScene / Canvas/HelpDetailPanel, 카테고리 고르기 + 최신 메뉴 + 문장 입력)을
    // 어느 화면비에서도 배경 그림과 딱 맞게 고친다.
    //   예전: 16:9 배경을 화면 전체로 늘리고(태블릿 16:10/4:3에서 찌그러짐) UI는 화면 비율 앵커 + 고정 px 글자라
    //         그림 속 최신 메뉴 틀과 어긋나고, 2560x1600 같은 고해상도에서는 글자가 작아졌다.
    //   지금: HelpBoard(보드 그림 크기 그대로) 안에 배경과 UI를 모두 그림 px 좌표로 놓고, BoardFitter가 보드를
    //         가로세로 같은 배율로 화면에 맞춘다.
    //   보드 그림은 16:9 원본(ui_helpdetail_background_neon, 1672x941)의 위아래를 52px씩 늘린 16:10(1672x1045)이라
    //   Tab S7 FE(2560x1600)에 잘리는 곳 없이 딱 맞는다. (16:9 그대로 덮으면 좌우가 잘려 바깥 틀 양옆/위 모서리가 화면 밖으로 나갔다)
    //   16:9는 늘린 위아래만 잘리고, 4:3·20:9처럼 꼭 보여야 할 영역이 잘리면 보드를 줄여 남는 자리는
    //   HelpBackdrop(보드를 흐리고 어둡게 한 그림)이 채운다.
    // ARFieldScene은 손배치 씬이라 다시 만들지 않고, 있는 오브젝트를 옮기고 자리/글자 크기만 바꾼다.
    //   LatestMenuController / HelpTextInputController / SequentialCardController / 버튼 연결은 그대로 둔다.
    //   보드/뒤 배경 그림은 Art_Source/ui_arfield/make_helpdetail_board.py가 만든다.
    //   카테고리 카드 아이콘/화살표는 Art_Source/ui_arfield/make_helpdetail_icons.py가 만든다.
    // 여러 번 실행해도 같은 결과가 되게(이미 옮긴 오브젝트는 이름으로 다시 찾는다) 만들었다.
    public static class ARFieldHelpDetailLayout
    {
        const string Generated = "Assets/_Project/Art/UI/Generated/";
        const string Derived = Generated + "derived/";
        const string BoardPath = Derived + "ui_helpdetail_board.png";
        const string BackdropPath = Derived + "ui_helpdetail_backdrop.png";
        const string FramePath = Derived + "ui_helpdetail_panel_frame.png";   // 9-slice, 보드 px x2
        const string ChatInputCleanPath = Derived + "ui_helpdetail_chat_input_clean.png";   // 입력창에서 그려진 아이콘을 지운 것
        const string CoffeePath = Derived + "ui_helpdetail_icon_coffee.png";
        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";   // 9-slice, 모서리 반지름 44
        // 변경점: 받은 네온 아이콘(1254x1254, 본체 = 알파 200 이상 px). 본체 가운데/폭으로 놓는다(가로세로 같은 배율).
        static readonly Vector2 GivenIconSize = new Vector2(1254f, 1254f);
        static readonly (string path, Rect body) NextIcon = (Generated + "ui_helpdetail_icon_next.png", Rect.MinMaxRect(369f, 371f, 882f, 877f));   // 파란 원 + 오른쪽 화살표
        static readonly (string path, Rect body) ChatIcon = (Generated + "ui_helpdetail_icon_chat.png", Rect.MinMaxRect(337f, 401f, 916f, 884f));
        static readonly (string path, Rect body) ClearIcon = (Generated + "ui_helpdetail_icon_clear.png", Rect.MinMaxRect(418f, 423f, 835f, 845f));
        // 변경점: 카드 순서(1~4)대로 아이콘 그림과 안내 글
        static readonly string[] CatIconPaths =
        {
            Derived + "ui_helpdetail_icon_greeting.png", Derived + "ui_helpdetail_icon_order.png",
            Derived + "ui_helpdetail_icon_payment.png", Derived + "ui_helpdetail_icon_convenience.png",
        };
        static readonly string[] CatSubs =
        {
            "매장 이용 시 인사 방법을 안내해요.", "메뉴 주문과 변경 방법을 안내해요.",
            "다양한 결제 방법을 안내해요.", "그 외 편의 시설 및 이용 정보를 안내해요.",
        };

        // 변경점: 보드 = 원본 위아래에 Ext씩 늘린 16:10 그림. 아래 자리는 원본 그림 px에 y만 Ext를 더해 적는다.
        const float Ext = 52f;
        static readonly Vector2 BoardSize = new Vector2(1672f, 941f + 2f * Ext);
        static readonly Rect BoardRect = new Rect(Vector2.zero, BoardSize);
        // 변경점: 화면에 꽉 차게 맞출 영역 = 그림 속 가장 큰 바깥 틀(x 63~1608, 위 빛 선) ~ 입력 줄 아래.
        //   BoardFitter(fillSafeRect)가 이 영역을 여백 SafeMargin만 남기고 화면에 맞춘다. 16:10에서는 틀 양옆이 화면 끝 SafeMargin까지
        //   오고(보드 배경 가장자리는 화면 밖), 위아래 남는 자리는 같게 나눈다. 폰/4:3에서 보드가 화면보다 작으면 HelpBackdrop이 채운다.
        static readonly Rect BoardSafe = Rect.MinMaxRect(63f, 60f + Ext, 1608f, 883f + Ext);
        const float SafeMargin = 32f;

        // ===== 배경 그림 속 자리 (px, 좌상단 기준, y 아래가 +) =====
        static readonly Rect Frame = Rect.MinMaxRect(214f, 219f + Ext, 922f, 755f + Ext);      // 최신 메뉴 틀(바깥 빛 선)
        static readonly Rect BackBox = Rect.MinMaxRect(97f, 90f + Ext, 185f, 178f + Ext);      // 뒤로 가기 원(원본 가운데 141,134) 누름 칸
        // 변경점: 뒤로 가기 = 받은 화살표(NextIcon)를 좌우로 뒤집어 그림 속 원(지름 약 71)을 덮는다
        static readonly Vector2 BackCenter = new Vector2(141f, 134f + Ext);
        const float BackRing = 76f;
        static readonly Rect HeadBox = Rect.MinMaxRect(430f, 95f + Ext, 1240f, 205f + Ext);    // 제목/부제(그림에 들어 있다)

        // 최신 메뉴: 제목은 안쪽 틀(원본 y 229) ~ 머리 가로 빛줄(원본 y 265) 사이, 카드 4장은 그 아래 안쪽 틀 안
        // 변경점: 제목 왼쪽에 커피 컵 네온 아이콘
        static readonly Rect MenuTitleBox = Rect.MinMaxRect(302f, 229f + Ext, 640f, 265f + Ext);
        static readonly Vector2 CoffeeCenter = new Vector2(276f, 247f + Ext);
        const float CoffeeSize = 46f;
        static readonly Rect MenuListBox = Rect.MinMaxRect(256f, 280f + Ext, 886f, 734f + Ext);
        const float MenuCardH = 98f, MenuCardGap = 16f;
        // 변경점: 최신 메뉴는 카드 MenuCardCount장을 세로 스크롤 목록에 둔다(데이터가 있는 카드만 보임).
        //   MenuList = 보이는 칸(뷰포트, 카드 칸보다 MenuScrollPad만큼 넓어 카드 빛이 조금 보인다), MenuContent = 카드들(세로 정렬).
        const int MenuCardCount = 20;
        const float MenuScrollPad = 12f;
        static readonly Vector2Int MenuScrollSoftness = new Vector2Int(8, 20);   // 위아래 끝을 부드럽게 흐리게(스크롤되는 느낌)
        // 메뉴 카드 안(카드 좌상단 기준)
        static readonly Rect SlotBody = Rect.MinMaxRect(10f, 9f, 96f, 89f);
        const float MenuImageInset = 4f, PhotoRadius = 12f;   // 사진(정사각) = 칸 안쪽 정사각형, 둥근 모서리
        static readonly Rect NameBox = Rect.MinMaxRect(110f, 10f, 478f, 46f);
        static readonly Rect DescBox = Rect.MinMaxRect(110f, 47f, 478f, 92f);     // 15px 두 줄
        static readonly Rect PillBody = Rect.MinMaxRect(486f, 31f, 620f, 67f);
        const float PricePad = 8f;

        // 변경점: 카테고리 카드를 최신 메뉴처럼 틀(CategoryFrame)로 묶는다. 틀은 최신 메뉴 틀과 위아래를 맞추고,
        //   오른쪽 끝은 왼쪽 여백(214)과 대칭. 틀 그림은 바깥 선 밖으로 FrameGlow만큼 빛 자리가 있고 가장자리 FrameBorder가 9-slice.
        static readonly Rect CategoryFrameBox = Rect.MinMaxRect(946f, 219f + Ext, 1458f, 755f + Ext);   // 바깥 빛 선
        const float FrameGlow = 12f, FrameBorderPx = 112f, FramePixelsPerUnit = 2f;
        // 카드 4장은 틀 안쪽 선 안에
        const float CatX0 = 974f, CatX1 = 1430f, CatY0 = 245f + Ext, CatH = 112f, CatGap = 12f;
        // 카드 안(본체 기준). 그림 속 왼쪽 원 가운데 = 폭의 0.156, 오른쪽 작은 원 가운데 = 0.893(지름 0.106)
        const float CatTextX0 = 0.30f, CatTextX1 = 0.82f;   // 왼쪽 원 오른쪽 ~ 오른쪽 작은 원 앞
        const float CatTitleY0 = 12f, CatTitleY1 = 60f, CatSubY0 = 62f, CatSubY1 = 90f;
        const float CatIconX = 0.156f, CatIconSize = 54f;
        const float CatArrowX = 0.893f, CatArrowRing = 0.112f;   // 받은 화살표 원이 그림 속 작은 원(0.106)을 덮게 조금 크게

        // 입력 줄: 왼쪽은 최신 메뉴 틀, 오른쪽은 카테고리 카드 끝에 맞춘다
        // 변경점: 전송 버튼을 없애고 입력창이 줄 전체를 쓴다(말하기는 키보드의 완료/엔터 = 입력창 제출)
        static readonly Rect InputRow = Rect.MinMaxRect(214f, 783f + Ext, 1458f, 875f + Ext);
        const float RefInputHeight = 92f;   // 아래 px 값의 기준 입력창 높이(다른 화면은 높이에 비례해 줄인다)
        // 변경점: 입력창 안 자리는 양끝에서의 px(RefInputHeight 기준). 입력창이 가로로 늘어나도 왼쪽 말풍선/세로줄, 오른쪽 X는 끝에 붙어 있다.
        //   입력창 그림은 9-slice(왼쪽 ChatCapLeftPx: 둥근 끝 + 말풍선 자리 + 세로줄, 오른쪽 ChatCapRightPx: 둥근 끝 + X 자리, 그림 px).
        const float InputTextLeft = 123f, InputTextRight = 100f, InputTextBottom = 0.15f, InputTextTop = 0.85f;
        const float ChatIconOffset = 61f, ChatIconW = 40f;       // 왼쪽 끝 -> 말풍선 가운데
        const float ClearOffset = 53f, ClearD = 40f, ClearHit = 60f;   // 오른쪽 끝 -> X 가운데
        const float ChatCapLeftPx = 150f, ChatCapRightPx = 120f;

        // ===== 글자 크기(보드 px, 16:10 태블릿에서 약 1.53배로 보인다) =====
        const float MenuTitleSize = 28f, NameMax = 22f, NameMin = 16f, DescSize = 15f, PriceMax = 15f, PriceMin = 11f;
        const float CatTextSize = 30f, CatSubMax = 14f, CatSubMin = 11f, InputTextSize = 20f;

        // ===== 그림 크기와 본체(알파 200 이상, px 좌상단 기준). 본체를 자리에 맞추고 빛 번짐은 밖으로 넘친다 =====
        static readonly Dictionary<string, (Vector2 size, Rect body)> Art = new Dictionary<string, (Vector2, Rect)>
        {
            { "ui_helpdetail_menu_item_bg", (new Vector2(2172f, 724f), Rect.MinMaxRect(109f, 164f, 2064f, 546f)) },
            { "ui_helpdetail_menu_image_slot", (new Vector2(1254f, 1254f), Rect.MinMaxRect(164f, 194f, 1090f, 1035f)) },
            { "ui_helpdetail_price_pill", (new Vector2(1774f, 887f), Rect.MinMaxRect(194f, 276f, 1579f, 599f)) },
            { "ui_helpdetail_card_greeting", (new Vector2(737f, 199f), Rect.MinMaxRect(18f, 20f, 724f, 193f)) },
            { "ui_helpdetail_card_order", (new Vector2(739f, 186f), Rect.MinMaxRect(20f, 7f, 727f, 177f)) },
            { "ui_helpdetail_card_payment", (new Vector2(739f, 191f), Rect.MinMaxRect(20f, 9f, 726f, 178f)) },
            { "ui_helpdetail_card_convenience", (new Vector2(737f, 184f), Rect.MinMaxRect(18f, 10f, 725f, 169f)) },
            { "ui_helpdetail_chat_input", (new Vector2(1280f, 147f), Rect.MinMaxRect(16f, 12f, 1271f, 129f)) },
            { "ui_helpdetail_chat_input_clean", (new Vector2(1280f, 147f), Rect.MinMaxRect(16f, 12f, 1271f, 129f)) },
            { "ui_helpdetail_send_button", (new Vector2(304f, 162f), Rect.MinMaxRect(5f, 27f, 301f, 139f)) },
        };

        static readonly Color TextWhite = new Color(0.96f, 0.98f, 1f, 1f);
        static readonly Color TextSub = new Color(0.78f, 0.86f, 1f, 0.95f);

        [MenuItem("Artti/Fix/ARField - Help Detail Panel Board Layout (keep hand placement)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ARFieldHelpDetailLayout] Play 모드에서는 실행하지 않는다."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ARField)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
            }

            RectTransform panel = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.transform.Find("HelpDetailPanel");
                if (t != null) { panel = (RectTransform)t; break; }
            }
            if (panel == null) { Debug.LogError("[ARFieldHelpDetailLayout] Canvas/HelpDetailPanel 없음"); return; }

            // ===== 뒤 배경(화면 cover) + 보드 =====
            var backdrop = (RectTransform)panel.Find("HelpBackdrop") ?? NewRect("HelpBackdrop", panel);
            backdrop.SetSiblingIndex(0);
            var backdropImg = GetOrAdd<Image>(backdrop);
            backdropImg.sprite = LoadSprite(BackdropPath, 2048);
            backdropImg.type = Image.Type.Simple;
            backdropImg.preserveAspect = false;
            backdropImg.color = Color.white;
            backdropImg.raycastTarget = true;   // 뒤(AR 화면)로 눌림이 지나가지 않게
            GetOrAdd<CoverImageFitter>(backdrop).Setup(BoardSize.x / BoardSize.y, 1f, new Vector2(0.5f, 0.5f));

            var board = (RectTransform)panel.Find("HelpBoard") ?? NewRect("HelpBoard", panel);
            board.SetSiblingIndex(1);
            GetOrAdd<BoardFitter>(board).Setup(BoardSize, BoardSafe, SafeMargin, true);

            var bg = Child(board, panel, "Background");
            if (bg == null) return;
            bg.SetParent(board, false);
            bg.SetAsFirstSibling();
            Stretch(bg);
            var bgImg = GetOrAdd<Image>(bg);
            bgImg.sprite = LoadSprite(BoardPath, 2048);
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
            bgImg.color = Color.white;
            bgImg.raycastTarget = true;

            // 원래 순서(최신 메뉴 -> 카테고리 -> 입력 -> 머리 -> 뒤로 가기)대로 보드로 옮긴다.
            // SequentialCardController가 자식 CanvasGroup을 이 순서로 찾아 차례로 켠다.
            var names = new[] { "LatestMenuArea", "RightCategoryArea", "TextInputArea", "HeadCard", "HelpBackButton" };
            var parts = new Dictionary<string, RectTransform>();
            foreach (var name in names)
            {
                var t = Child(board, panel, name);
                if (t == null) return;
                t.SetParent(board, false);
                t.SetAsLastSibling();
                parts[name] = t;
            }

            if (!LatestMenu(parts["LatestMenuArea"])) return;
            if (!Categories(parts["RightCategoryArea"])) return;
            if (!InputRowLayout(parts["TextInputArea"])) return;
            if (!ChatDrawer(panel, board, parts["TextInputArea"])) return;
            Place(parts["HeadCard"], board, HeadBox, BoardRect);
            Place(parts["HelpBackButton"], board, BackBox, BoardRect);
            BackButton(parts["HelpBackButton"]);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ARFieldHelpDetailLayout] 완료 - 도움 화면을 배경 보드(1672x941) 기준으로 맞춤, 연결은 그대로");
        }

        static bool LatestMenu(RectTransform area)
        {
            var board = (RectTransform)area.parent;
            Place(area, board, Frame, BoardRect);

            var header = Need(area, "Header");
            var title = header != null ? Need(header, "Title") : null;
            var list = Need(area, "MenuList");
            if (title == null || list == null) return false;
            Place(header, area, Frame, Frame);
            Place(title, header, MenuTitleBox, Frame);
            Style(title, MenuTitleSize, true, TextWhite, TextAlignmentOptions.MidlineLeft);
            var cup = (RectTransform)header.Find("TitleIcon") ?? NewRect("TitleIcon", header);
            Place(cup, header, new Rect(CoffeeCenter.x - CoffeeSize * 0.5f, CoffeeCenter.y - CoffeeSize * 0.5f, CoffeeSize, CoffeeSize), Frame);
            Icon(cup, CoffeePath);

            // 변경점: MenuList = 스크롤 뷰포트, MenuContent = 카드 세로 정렬. 카드는 MenuCardCount장(모자라면 1번 카드를 복제).
            var viewRect = Rect.MinMaxRect(MenuListBox.xMin - MenuScrollPad, MenuListBox.yMin - MenuScrollPad,
                MenuListBox.xMax + MenuScrollPad, MenuListBox.yMax + MenuScrollPad);
            Place(list, area, viewRect, Frame);
            var content = MenuScroll(list);
            var first = (content.Find("MenuItemCard_01") ?? list.Find("MenuItemCard_01")) as RectTransform;
            if (first == null) { Debug.LogError("[ARFieldHelpDetailLayout] MenuList 아래 MenuItemCard_01 없음"); return false; }
            var cards = new RectTransform[MenuCardCount];
            for (int i = 0; i < MenuCardCount; i++)
            {
                string cardName = $"MenuItemCard_{i + 1:00}";
                var found = (content.Find(cardName) ?? list.Find(cardName)) as RectTransform;
                if (found == null)
                {
                    found = (RectTransform)Object.Instantiate(first.gameObject, content).transform;
                    found.name = cardName;
                }
                found.SetParent(content, false);
                found.SetSiblingIndex(i);
                found.anchorMin = found.anchorMax = found.pivot = new Vector2(0f, 1f);
                found.localScale = Vector3.one;
                found.localRotation = Quaternion.identity;
                found.sizeDelta = new Vector2(MenuListBox.width, MenuCardH);
                if (found.TryGetComponent<LayoutElement>(out var le)) le.ignoreLayout = false;
                cards[i] = found;
            }

            // 카드 안 자리는 카드 좌상단 기준이라 모든 카드가 같은 cardRect로 계산한다(카드 위치는 MenuContent가 정한다)
            var cardRect = new Rect(MenuListBox.x, MenuListBox.y, MenuListBox.width, MenuCardH);
            var views = new (RectTransform card, Image image, TMP_Text name, TMP_Text desc, TMP_Text price, Button button)[MenuCardCount];
            for (int i = 0; i < MenuCardCount; i++)
            {
                var card = cards[i];
                var cardBg = Need(card, "CardBackground");
                var slot = Need(card, "MenuImageSlot");
                // 한 번 실행한 뒤에는 사진이 PhotoMask 안에 있다
                var image = slot != null ? (slot.Find("MenuImage") ?? slot.Find("PhotoMask/MenuImage")) as RectTransform : null;
                if (slot != null && image == null) Debug.LogError("[ARFieldHelpDetailLayout] MenuImageSlot 아래 MenuImage 없음");
                var textArea = Need(card, "TextArea");
                var name = textArea != null ? Need(textArea, "MenuName") : null;
                var desc = textArea != null ? Need(textArea, "MenuDescription") : null;
                var pill = Need(card, "PricePill");
                var price = pill != null ? Need(pill, "PriceText") : null;
                if (cardBg == null || image == null || name == null || desc == null || price == null) return false;

                // 카드 그림: 본체가 카드 칸에 맞고 빛 번짐은 밖으로
                Place(cardBg, card, ArtRect(cardBg, cardRect), cardRect);

                var slotBody = Offset(SlotBody, cardRect);
                var slotRect = ArtRect(slot, slotBody);
                Place(slot, card, slotRect, cardRect);
                // 변경점: 사진은 둥근 모서리 마스크(PhotoMask) 안에 꽉 채운다. 사진이 정사각형이라 마스크도 칸 안쪽 정사각형.
                float side = Mathf.Min(slotBody.width, slotBody.height) - 2f * MenuImageInset;
                var maskRect = new Rect(slotBody.center.x - side * 0.5f, slotBody.center.y - side * 0.5f, side, side);
                var photoMask = (RectTransform)slot.Find("PhotoMask") ?? NewRect("PhotoMask", slot);
                Place(photoMask, slot, maskRect, slotRect);
                RoundedMask(photoMask, PhotoRadius);
                image.SetParent(photoMask, false);
                Stretch(image);

                Place(textArea, card, cardRect, cardRect);
                Place(name, textArea, Offset(NameBox, cardRect), cardRect);
                var nameText = Style(name, NameMax, true, TextWhite, TextAlignmentOptions.MidlineLeft);
                nameText.enableAutoSizing = true;   // 긴 이름(밤고구마단호박팥크림치즈빵)은 줄여서 한 줄
                nameText.fontSizeMin = NameMin;
                nameText.fontSizeMax = NameMax;
                Place(desc, textArea, Offset(DescBox, cardRect), cardRect);
                var descText = Style(desc, DescSize, false, TextSub, TextAlignmentOptions.TopLeft);
                descText.textWrappingMode = TextWrappingModes.Normal;
                descText.overflowMode = TextOverflowModes.Ellipsis;   // 칸이 두 줄보다 높아 두 줄까지 보이고 넘치면 ...

                var pillBody = Offset(PillBody, cardRect);
                var pillRect = ArtRect(pill, pillBody);
                Place(pill, card, pillRect, cardRect);
                Place(price, pill, Rect.MinMaxRect(pillBody.xMin + PricePad, pillBody.yMin, pillBody.xMax - PricePad, pillBody.yMax), pillRect);
                var priceText = Style(price, PriceMax, true, TextWhite, TextAlignmentOptions.Center);
                priceText.enableAutoSizing = true;   // "매장별 가격 상이"도 알약 안에
                priceText.fontSizeMin = PriceMin;
                priceText.fontSizeMax = PriceMax;
                views[i] = (card, image.GetComponent<Image>(), nameText, descText, priceText, CardButton(card, cardBg));
            }
            return WireLatestMenu(area, views, list.GetComponent<ScrollRect>());
        }

        // 변경점: 스크롤 뷰포트(MenuList) + 내용(MenuContent). 카드 사이/빈 곳을 끌어도 스크롤되게 뷰포트에 투명 Image를 둔다.
        static RectTransform MenuScroll(RectTransform list)
        {
            var hit = GetOrAdd<Image>(list);
            hit.sprite = null;
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            var mask = GetOrAdd<RectMask2D>(list);
            mask.softness = MenuScrollSoftness;

            var content = (RectTransform)list.Find("MenuContent") ?? NewRect("MenuContent", list);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
            content.localScale = Vector3.one;
            var layout = GetOrAdd<VerticalLayoutGroup>(content);
            int pad = Mathf.RoundToInt(MenuScrollPad);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = MenuCardGap;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = GetOrAdd<ContentSizeFitter>(content);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // 보이는 카드 수만큼만 길어진다

            var scroll = GetOrAdd<ScrollRect>(list);
            scroll.viewport = list;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;
            scroll.horizontalScrollbar = null;
            scroll.verticalScrollbar = null;
            EditorUtility.SetDirty(scroll);
            return content;
        }

        // 변경점: LatestMenuController에 카드 MenuCardCount장과 스크롤을 연결하고, 메뉴 목록(Inspector)이 비어 있으면 지금 메뉴로 채운다
        // 변경점: 메뉴 카드 전체를 누름 칸으로(누르면 LatestMenuController가 "메뉴 이름 주세요"를 읽는다).
        //   카드 그림은 빛 번짐까지 커서 이웃 카드와 겹치므로, 누름 칸은 카드 루트(카드 크기)의 투명 Image 하나만 쓴다.
        //   카드를 끌면 그대로 목록이 스크롤된다(Button은 끌기를 받지 않아 ScrollRect로 넘어간다).
        static Button CardButton(RectTransform card, RectTransform cardBg)
        {
            foreach (var g in card.GetComponentsInChildren<Graphic>(true))
                if (g.transform != card) g.raycastTarget = false;
            var hit = GetOrAdd<Image>(card);
            hit.sprite = null;
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            var button = GetOrAdd<Button>(card);
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = cardBg.GetComponent<Image>();   // 누르는 동안 카드 그림이 살짝 어두워진다
            EditorUtility.SetDirty(button);
            return button;
        }

        static bool WireLatestMenu(RectTransform area, (RectTransform card, Image image, TMP_Text name, TMP_Text desc, TMP_Text price, Button button)[] views, ScrollRect scroll)
        {
            var controller = area.GetComponent<LatestMenuController>();
            if (controller == null) { Debug.LogError("[ARFieldHelpDetailLayout] LatestMenuArea에 LatestMenuController 없음"); return false; }
            controller.FillMenuListsIfEmpty();
            var so = new SerializedObject(controller);
            var cards = so.FindProperty("cards");
            cards.arraySize = views.Length;
            for (int i = 0; i < views.Length; i++)
            {
                var e = cards.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("cardRoot").objectReferenceValue = views[i].card.gameObject;
                e.FindPropertyRelative("menuImage").objectReferenceValue = views[i].image;
                e.FindPropertyRelative("menuName").objectReferenceValue = views[i].name;
                e.FindPropertyRelative("menuDescription").objectReferenceValue = views[i].desc;
                e.FindPropertyRelative("priceText").objectReferenceValue = views[i].price;
                e.FindPropertyRelative("button").objectReferenceValue = views[i].button;
            }
            so.FindProperty("menuScroll").objectReferenceValue = scroll;
            var tts = Object.FindFirstObjectByType<TTSManager>(FindObjectsInactive.Include);
            if (tts == null) Debug.LogWarning("[ARFieldHelpDetailLayout] TTSManager 없음 - 메뉴 카드를 눌러도 읽지 않음");
            so.FindProperty("tts").objectReferenceValue = tts;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            return true;
        }

        static bool Categories(RectTransform area)
        {
            var board = (RectTransform)area.parent;
            var areaRect = CategoryFrameBox;
            Place(area, board, areaRect, BoardRect);

            // 변경점: 카드 뒤 틀(최신 메뉴 틀과 같은 모양의 9-slice 그림)
            var frame = (RectTransform)area.Find("CategoryFrame") ?? NewRect("CategoryFrame", area);
            frame.SetAsFirstSibling();
            var frameRect = Rect.MinMaxRect(areaRect.xMin - FrameGlow, areaRect.yMin - FrameGlow, areaRect.xMax + FrameGlow, areaRect.yMax + FrameGlow);
            Place(frame, area, frameRect, areaRect);
            var frameImg = GetOrAdd<Image>(frame);
            frameImg.sprite = LoadSlicedSprite(FramePath, FrameBorderPx);
            frameImg.type = Image.Type.Sliced;
            frameImg.fillCenter = true;
            frameImg.pixelsPerUnitMultiplier = FramePixelsPerUnit;
            frameImg.color = Color.white;
            frameImg.raycastTarget = false;
            for (int i = 1; i <= 4; i++)
            {
                var card = Need(area, $"CardBackground{i}");
                var text = card != null ? Need(card, $"CardText{i}") : null;
                if (text == null) return false;
                var body = new Rect(CatX0, CatY0 + (i - 1) * (CatH + CatGap), CatX1 - CatX0, CatH);
                var cardRect = ArtRect(card, body);
                Place(card, area, cardRect, areaRect);

                // 변경점: 제목(위) + 안내 글(아래). 안내 글은 제목을 복제해 글꼴/재질을 그대로 쓴다.
                float tx0 = body.x + body.width * CatTextX0, tx1 = body.x + body.width * CatTextX1;
                Place(text, card, Rect.MinMaxRect(tx0, body.y + CatTitleY0, tx1, body.y + CatTitleY1), cardRect);
                Style(text, CatTextSize, true, TextWhite, TextAlignmentOptions.BottomLeft);
                var sub = (RectTransform)card.Find($"CardSub{i}");
                if (sub == null)
                {
                    sub = (RectTransform)Object.Instantiate(text.gameObject, card).transform;
                    sub.name = $"CardSub{i}";
                }
                Place(sub, card, Rect.MinMaxRect(tx0, body.y + CatSubY0, tx1, body.y + CatSubY1), cardRect);
                var subText = Style(sub, CatSubMax, false, TextSub, TextAlignmentOptions.TopLeft);
                subText.enableAutoSizing = true;   // 가장 긴 "그 외 편의 시설 및 이용 정보를 안내해요."도 한 줄
                subText.fontSizeMin = CatSubMin;
                subText.fontSizeMax = CatSubMax;
                subText.text = CatSubs[i - 1];

                // 변경점: 왼쪽 원 안 아이콘(꺼 두었던 IconImage를 새 그림으로 켠다)
                var icon = card.Find($"IconImage{i}") as RectTransform;
                if (icon != null)
                {
                    var c = new Vector2(body.x + body.width * CatIconX, body.center.y);
                    Place(icon, card, new Rect(c.x - CatIconSize * 0.5f, c.y - CatIconSize * 0.5f, CatIconSize, CatIconSize), cardRect);
                    Icon(icon, CatIconPaths[i - 1]);
                }

                // 변경점: 오른쪽 작은 원 위 다음 화살표(받은 아이콘, 원이 그림 속 원을 덮게)
                var arrow = (RectTransform)card.Find("Arrow") ?? NewRect("Arrow", card);
                PlaceGiven(arrow, card, NextIcon, new Vector2(body.x + body.width * CatArrowX, body.center.y), body.width * CatArrowRing, cardRect);
                arrow.SetAsLastSibling();
            }
            return true;
        }

        // 변경점: 뒤로 가기 = 다음 화살표를 좌우로 뒤집어 그림 속 원 위에 놓는다. 누름 칸(BackBox)과 연결은 그대로.
        static void BackButton(RectTransform back)
        {
            var arrow = (RectTransform)back.Find("ArrowImage") ?? NewRect("ArrowImage", back);
            PlaceGiven(arrow, back, NextIcon, BackCenter, BackRing, BackBox);
            // 가운데를 축으로 뒤집도록 pivot을 가운데로 옮긴다(Place는 좌상단 pivot)
            arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.anchoredPosition += new Vector2(arrow.sizeDelta.x * 0.5f, -arrow.sizeDelta.y * 0.5f);
            arrow.localScale = new Vector3(-1f, 1f, 1f);
            if (back.TryGetComponent<Button>(out var button))
            {
                button.targetGraphic = arrow.GetComponent<Image>();   // 누를 때 화살표 색이 바뀌게
                EditorUtility.SetDirty(button);
            }
        }

        // 받은 아이콘을 본체 가운데 center, 본체 폭 bodyWidth(보드 px)로 놓는다. 그림 전체는 본체보다 크다(빛 + 여백).
        static void PlaceGiven(RectTransform rt, RectTransform parent, (string path, Rect body) icon, Vector2 center, float bodyWidth, Rect parentRect)
        {
            float k = bodyWidth / icon.body.width;
            var rect = new Rect(center.x - icon.body.center.x * k, center.y - icon.body.center.y * k, GivenIconSize.x * k, GivenIconSize.y * k);
            Place(rt, parent, rect, parentRect);
            Icon(rt, icon.path, 512);
        }

        // 가장자리 border px을 9-slice로 들인다
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

        static void Icon(RectTransform rt, string path, int maxSize = 256)
        {
            rt.gameObject.SetActive(true);
            var img = GetOrAdd<Image>(rt);
            img.sprite = LoadSprite(path, maxSize);
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
            img.raycastTarget = false;   // 카드 버튼이 눌리게
        }

        // 둥근 모서리로 자식을 자르는 칸(모양만, 그림은 안 보임). RoundedRect 모서리 44px을 radius로 맞춘다.
        internal static void RoundedMask(RectTransform rt, float radius)
        {
            var img = GetOrAdd<Image>(rt);
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 44f / radius;
            img.color = Color.white;
            img.raycastTarget = false;
            var mask = GetOrAdd<Mask>(rt);
            mask.enabled = true;
            mask.showMaskGraphic = false;
        }

        static bool InputRowLayout(RectTransform area)
            => LayoutInputRow(area, (RectTransform)area.parent, BoardRect, InputRow);

        // 변경점: 입력 줄 = 입력창 하나(말풍선 아이콘 + 글 + 지우기 X)가 row 전체를 쓴다. 전송 버튼은 끈다(지우지 않음, 연결은 그대로).
        //   말하기는 키보드의 완료/엔터: 입력창 제출(onSubmit) -> HelpTextInputController가 TTS로 읽는다.
        //   row는 board 좌표(px). 인사/결제/편의/주문 화면(ARFieldGreetingLayout, ARFieldOrderLayout)도 같이 쓴다.
        internal static bool LayoutInputRow(RectTransform area, RectTransform board, Rect boardRect, Rect row)
        {
            float k = row.height / RefInputHeight;
            Place(area, board, row, boardRect);

            var inputBg = Need(area, "InputBackground");
            var field = inputBg != null ? Need(inputBg, "MessageInputField") : null;
            var viewport = field != null ? Need(field, "InputTextViewport") : null;
            if (viewport == null) return false;
            var send = area.Find("SendButton");
            if (send != null) send.gameObject.SetActive(false);

            // 입력창 그림(그려진 아이콘을 지운 것)을 가로 9-slice로: 본체가 row에 맞고 빛 번짐은 밖으로, 양끝은 찌그러지지 않는다
            var (artSize, artBody) = Art["ui_helpdetail_chat_input_clean"];
            float ka = row.height / artBody.height;
            var inputRect = new Rect(row.x - artBody.x * ka, row.y - artBody.y * ka,
                row.width + (artSize.x - artBody.width) * ka, artSize.y * ka);
            Place(inputBg, area, inputRect, row);
            var inputImg = GetOrAdd<Image>(inputBg);
            inputImg.sprite = LoadSpriteWithBorder(ChatInputCleanPath, new Vector4(ChatCapLeftPx, 0f, ChatCapRightPx, 0f));
            inputImg.type = Image.Type.Sliced;
            inputImg.fillCenter = true;
            inputImg.pixelsPerUnitMultiplier = 1f / ka;
            inputImg.preserveAspect = false;
            Place(field, inputBg, row, inputRect);   // 누르는 칸 = 입력창 본체

            var chat = (RectTransform)inputBg.Find("ChatIcon") ?? NewRect("ChatIcon", inputBg);
            PlaceGiven(chat, inputBg, ChatIcon, new Vector2(row.xMin + ChatIconOffset * k, row.center.y), ChatIconW * k, inputRect);

            // 지우기(X): 누름 칸(투명) + Visual(아이콘). 입력 칸보다 뒤 순서여야 눌린다.
            var clear = (RectTransform)inputBg.Find("ClearButton") ?? NewRect("ClearButton", inputBg);
            var clearCenter = new Vector2(row.xMax - ClearOffset * k, row.center.y);
            float clearHit = ClearHit * k;
            var clearRect = new Rect(clearCenter.x - clearHit * 0.5f, clearCenter.y - clearHit * 0.5f, clearHit, clearHit);
            Place(clear, inputBg, clearRect, inputRect);
            clear.SetAsLastSibling();
            var hit = GetOrAdd<Image>(clear);
            hit.sprite = null;
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            var clearVisual = (RectTransform)clear.Find("Visual") ?? NewRect("Visual", clear);
            PlaceGiven(clearVisual, clear, ClearIcon, clearCenter, ClearD * k, clearRect);
            var clearButton = GetOrAdd<Button>(clear);
            clearButton.targetGraphic = clearVisual.GetComponent<Image>();
            EditorUtility.SetDirty(clearButton);
            var inputController = area.GetComponent<HelpTextInputController>();
            if (inputController != null)
            {
                var so = new SerializedObject(inputController);
                so.FindProperty("clearButton").objectReferenceValue = clearButton;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("[ARFieldHelpDetailLayout] TextInputArea에 HelpTextInputController 없음 - 지우기 버튼 연결 안 함");

            // 글 칸: 말풍선/세로줄 오른쪽 ~ X 앞(양끝에서 px)
            viewport.anchorMin = new Vector2(0f, InputTextBottom);
            viewport.anchorMax = new Vector2(1f, InputTextTop);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = new Vector2(InputTextLeft * k, 0f);
            viewport.offsetMax = new Vector2(-InputTextRight * k, 0f);
            viewport.localScale = Vector3.one;
            // 안내 글(기울임)과 입력 글은 모양은 두고 크기만 보드 기준으로
            foreach (var t in viewport.GetComponentsInChildren<TMP_Text>(true))
            {
                t.enableAutoSizing = false;
                t.fontSize = InputTextSize * k;
                EditorUtility.SetDirty(t);
            }
            return true;
        }

        // 가장자리(왼쪽, 아래, 오른쪽, 위, 그림 px)를 9-slice로 들인다. 2048까지 줄이지 않고 들인다(입력창 그림 1280 폭).
        static Sprite LoadSpriteWithBorder(string path, Vector4 border)
        {
            LoadSprite(path, 2048);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.spriteBorder != border)
            {
                ti.spriteBorder = border;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ===== 변경점: 채팅창(아래 입력 줄을 누르면 오른쪽에서 나오는 창, 채팅이 있는 모든 화면) =====
        //   ChatDrawer(패널 맨 위) = Overlay(뒤 어두운 칸, 누르면 닫힘) + Sheet(오른쪽 창) > Content(보드 배율로 그림, 폭 ChatW 보드 px)
        //   Content = 어두운 바탕 + 틀 + 제목/닫기 + 문장 입력(아래 입력 줄 복제) + 보낸 문장 목록(스크롤, 누르면 다시 말하기)
        //   아래 입력 줄(TextInputArea)은 누르면 창을 여는 칸이 된다(위에 투명 OpenChatHit, 지우기 X는 끈다).
        const float ChatW = 760f;
        static readonly Rect ChatTitleBox = Rect.MinMaxRect(48f, 36f, 560f, 96f);
        static readonly Rect ChatSubBox = Rect.MinMaxRect(48f, 96f, 600f, 126f);
        static readonly Vector2 ChatCloseCenter = new Vector2(ChatW - 72f, 70f);
        const float ChatCloseRing = 52f, ChatCloseHit = 84f;
        static readonly Rect ChatInputRow = Rect.MinMaxRect(40f, 148f, ChatW - 40f, 228f);
        static readonly Rect ChatSectionBox = Rect.MinMaxRect(48f, 248f, ChatW - 48f, 282f);
        const float ChatListTop = 296f, ChatListSide = 40f, ChatListBottom = 40f;
        const float ChatItemH = 84f, ChatItemGap = 12f, ChatItemPad = 28f;
        const float ChatBackInset = 16f, ChatBackRadius = 28f;
        const string ChatGlassPath = Derived + "ui_order_glass_blue.png";   // 9-slice 88(그림 px), 보드 px의 2배로 그린 카드 유리
        static readonly Color ChatBackColor = new Color(0.02f, 0.05f, 0.17f, 0.95f);

        // 변경점: 인사/결제/편의(ARFieldGreetingLayout), 주문(ARFieldOrderLayout) 화면도 같이 쓴다.
        //   panel = 화면 패널, board = 그 화면 보드(BoardFitter, 창 안 배율), bottomInput = 그 화면 아래 입력 줄(TextInputArea)
        internal static bool ChatDrawer(RectTransform panel, RectTransform board, RectTransform bottomInput)
        {
            var font = panel.GetComponentInChildren<TMP_Text>(true)?.font;
            var root = (RectTransform)panel.Find("ChatDrawer") ?? NewRect("ChatDrawer", panel);
            Stretch(root);
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);

            // 뒤 어두운 칸
            var overlay = (RectTransform)root.Find("Overlay") ?? NewRect("Overlay", root);
            Stretch(overlay);
            var overlayImg = GetOrAdd<Image>(overlay);
            overlayImg.sprite = null;
            overlayImg.color = new Color(0f, 0f, 0f, 0f);
            overlayImg.raycastTarget = true;
            GetOrAdd<Button>(overlay).transition = Selectable.Transition.None;
            overlay.SetAsFirstSibling();

            // 오른쪽 창 + 창 안
            var sheet = (RectTransform)root.Find("Sheet") ?? NewRect("Sheet", root);
            sheet.anchorMin = new Vector2(1f, 0f);
            sheet.anchorMax = new Vector2(1f, 1f);
            sheet.pivot = new Vector2(1f, 0.5f);
            sheet.sizeDelta = new Vector2(ChatW, 0f);
            sheet.anchoredPosition = new Vector2(ChatW, 0f);
            sheet.localScale = Vector3.one;
            sheet.SetAsLastSibling();
            var content = (RectTransform)sheet.Find("Content") ?? NewRect("Content", sheet);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(ChatW, 1000f);
            content.localScale = Vector3.one;
            var contentRect = new Rect(0f, 0f, ChatW, 1000f);

            var back = (RectTransform)content.Find("Backing") ?? NewRect("Backing", content);
            StretchInset(back, ChatBackInset, ChatBackInset, ChatBackInset, ChatBackInset);
            var backImg = GetOrAdd<Image>(back);
            backImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            backImg.type = Image.Type.Sliced;
            backImg.pixelsPerUnitMultiplier = 44f / ChatBackRadius;
            backImg.color = ChatBackColor;
            backImg.raycastTarget = true;   // 창 안을 눌러도 뒤(어두운 칸)가 닫히지 않게
            back.SetSiblingIndex(0);
            var frame = (RectTransform)content.Find("Frame") ?? NewRect("Frame", content);
            float glowInset = ChatBackInset - FrameGlow;
            StretchInset(frame, glowInset, glowInset, glowInset, glowInset);
            var frameImg = GetOrAdd<Image>(frame);
            frameImg.sprite = LoadSlicedSprite(FramePath, FrameBorderPx);
            frameImg.type = Image.Type.Sliced;
            frameImg.fillCenter = true;
            frameImg.pixelsPerUnitMultiplier = FramePixelsPerUnit;
            frameImg.color = Color.white;
            frameImg.raycastTarget = false;
            frame.SetSiblingIndex(1);

            var title = (RectTransform)content.Find("Title") ?? NewText("Title", content, font);
            Place(title, content, ChatTitleBox, contentRect);
            Style(title, 32f, true, TextWhite, TextAlignmentOptions.MidlineLeft).text = "문장 입력";
            var sub = (RectTransform)content.Find("Subtitle") ?? NewText("Subtitle", content, font);
            Place(sub, content, ChatSubBox, contentRect);
            Style(sub, 18f, false, TextSub, TextAlignmentOptions.MidlineLeft).text = "입력하고 키보드의 완료를 누르면 말해요.";

            var close = (RectTransform)content.Find("CloseButton") ?? NewRect("CloseButton", content);
            var closeRect = new Rect(ChatCloseCenter.x - ChatCloseHit * 0.5f, ChatCloseCenter.y - ChatCloseHit * 0.5f, ChatCloseHit, ChatCloseHit);
            Place(close, content, closeRect, contentRect);
            var closeHit = GetOrAdd<Image>(close);
            closeHit.sprite = null;
            closeHit.color = new Color(1f, 1f, 1f, 0f);
            closeHit.raycastTarget = true;
            var closeVisual = (RectTransform)close.Find("Visual") ?? NewRect("Visual", close);
            PlaceGiven(closeVisual, close, ClearIcon, ChatCloseCenter, ChatCloseRing, closeRect);
            var closeButton = GetOrAdd<Button>(close);
            closeButton.targetGraphic = closeVisual.GetComponent<Image>();

            // 문장 입력: 아래 입력 줄을 복제(입력/지우기/말하기 연결은 복제본끼리, TTSManager는 그대로)
            var chatInput = content.Find("ChatInputArea") as RectTransform;
            if (chatInput == null)
            {
                chatInput = (RectTransform)Object.Instantiate(bottomInput.gameObject, content).transform;
                chatInput.name = "ChatInputArea";
                var copiedHit = chatInput.Find("OpenChatHit");
                if (copiedHit != null) Object.DestroyImmediate(copiedHit.gameObject);
            }
            if (!LayoutInputRow(chatInput, content, contentRect, ChatInputRow)) return false;
            var chatClear = chatInput.Find("InputBackground/ClearButton");
            if (chatClear != null) chatClear.gameObject.SetActive(true);
            var chatField = chatInput.GetComponentInChildren<TMP_InputField>(true);
            if (chatField != null) chatField.text = string.Empty;

            var section = (RectTransform)content.Find("SectionLabel") ?? NewText("SectionLabel", content, font);
            Place(section, content, ChatSectionBox, contentRect);
            Style(section, 20f, false, TextSub, TextAlignmentOptions.MidlineLeft).text = "보낸 문장 · 누르면 다시 말해요";

            // 보낸 문장 목록(스크롤)
            var view = (RectTransform)content.Find("History") ?? NewRect("History", content);
            StretchInset(view, ChatListSide, ChatListBottom, ChatListSide, ChatListTop);
            var viewHit = GetOrAdd<Image>(view);
            viewHit.sprite = null;
            viewHit.color = new Color(1f, 1f, 1f, 0f);
            viewHit.raycastTarget = true;
            GetOrAdd<RectMask2D>(view).softness = new Vector2Int(0, 16);
            var items = (RectTransform)view.Find("Items") ?? NewRect("Items", view);
            items.anchorMin = new Vector2(0f, 1f);
            items.anchorMax = new Vector2(1f, 1f);
            items.pivot = new Vector2(0.5f, 1f);
            items.anchoredPosition = Vector2.zero;
            items.sizeDelta = new Vector2(0f, items.sizeDelta.y);
            items.localScale = Vector3.one;
            var layout = GetOrAdd<VerticalLayoutGroup>(items);
            layout.padding = new RectOffset(0, 0, 6, 6);
            layout.spacing = ChatItemGap;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            var fitter = GetOrAdd<ContentSizeFitter>(items);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = GetOrAdd<ScrollRect>(view);
            scroll.viewport = view;
            scroll.content = items;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            var template = (RectTransform)items.Find("ItemTemplate") ?? NewRect("ItemTemplate", items);
            template.anchorMin = template.anchorMax = template.pivot = new Vector2(0.5f, 1f);
            template.sizeDelta = new Vector2(0f, ChatItemH);
            var itemImg = GetOrAdd<Image>(template);
            itemImg.sprite = LoadSlicedSprite(ChatGlassPath, 88f);
            itemImg.type = Image.Type.Sliced;
            itemImg.fillCenter = true;
            itemImg.pixelsPerUnitMultiplier = 2f;
            itemImg.color = Color.white;
            itemImg.raycastTarget = true;
            var itemLayout = GetOrAdd<LayoutElement>(template);
            itemLayout.preferredHeight = ChatItemH;
            itemLayout.minHeight = ChatItemH;
            var itemButton = GetOrAdd<Button>(template);
            itemButton.targetGraphic = itemImg;
            var itemLabel = (RectTransform)template.Find("Label") ?? NewText("Label", template, font);
            StretchInset(itemLabel, ChatItemPad, 0f, ChatItemPad, 0f);
            var itemText = Style(itemLabel, 26f, false, TextWhite, TextAlignmentOptions.MidlineLeft);
            itemText.text = "보낸 문장";
            itemText.textWrappingMode = TextWrappingModes.Normal;
            itemText.overflowMode = TextOverflowModes.Ellipsis;
            itemText.enableAutoSizing = true;   // 긴 문장은 줄이고 두 줄까지
            itemText.fontSizeMin = 18f;
            itemText.fontSizeMax = 26f;
            template.gameObject.SetActive(false);

            var empty = (RectTransform)view.Find("EmptyLabel") ?? NewText("EmptyLabel", view, font);
            Stretch(empty);
            Style(empty, 22f, false, TextSub, TextAlignmentOptions.Center).text = "아직 보낸 문장이 없어요.";
            empty.SetAsLastSibling();

            // 아래 입력 줄 = 창 여는 칸
            var bottomClear = bottomInput.Find("InputBackground/ClearButton");
            if (bottomClear != null) bottomClear.gameObject.SetActive(false);
            var openHit = (RectTransform)bottomInput.Find("OpenChatHit") ?? NewRect("OpenChatHit", bottomInput);
            Stretch(openHit);
            openHit.SetAsLastSibling();
            var openImg = GetOrAdd<Image>(openHit);
            openImg.sprite = null;
            openImg.color = new Color(1f, 1f, 1f, 0f);
            openImg.raycastTarget = true;
            var openButton = GetOrAdd<Button>(openHit);
            openButton.transition = Selectable.Transition.None;

            // 연결
            var drawer = GetOrAdd<HelpChatDrawerView>(root);
            var so = new SerializedObject(drawer);
            so.FindProperty("openButton").objectReferenceValue = openButton;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("overlay").objectReferenceValue = overlayImg;
            so.FindProperty("sheet").objectReferenceValue = sheet;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("board").objectReferenceValue = board;
            so.FindProperty("designWidth").floatValue = ChatW;
            so.FindProperty("input").objectReferenceValue = chatInput.GetComponent<HelpTextInputController>();
            so.FindProperty("listScroll").objectReferenceValue = scroll;
            so.FindProperty("listContent").objectReferenceValue = items;
            so.FindProperty("itemTemplate").objectReferenceValue = itemButton;
            so.FindProperty("emptyLabel").objectReferenceValue = empty.gameObject;
            so.FindProperty("tts").objectReferenceValue = Object.FindFirstObjectByType<TTSManager>(FindObjectsInactive.Include);
            so.ApplyModifiedPropertiesWithoutUndo();

            overlay.gameObject.SetActive(false);   // 닫힌 채로 둔다(HelpChatDrawerView가 연다)
            sheet.gameObject.SetActive(false);
            return true;
        }

        // 부모를 꽉 채우되 왼쪽/아래/오른쪽/위를 띄운다
        static void StretchInset(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static RectTransform NewText(string name, RectTransform parent, TMP_FontAsset font)
        {
            var rt = NewRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            return rt;
        }

        // 그림(Image.sprite)의 본체가 body에 오도록 빛 번짐까지 든 그림 전체 자리를 돌려준다(가로세로 따로 늘림).
        // 모르는 그림이면 그림 전체를 body에 맞춘다.
        static Rect ArtRect(Component c, Rect body)
        {
            var img = c.GetComponent<Image>();
            if (img == null || img.sprite == null || !Art.TryGetValue(img.sprite.name, out var art))
            {
                Debug.LogWarning($"[ARFieldHelpDetailLayout] {c.name}: 본체 크기를 모르는 그림이라 그림 전체를 칸에 맞춤");
                return body;
            }
            float kx = body.width / art.body.width, ky = body.height / art.body.height;
            return new Rect(body.x - art.body.x * kx, body.y - art.body.y * ky, art.size.x * kx, art.size.y * ky);
        }

        static Rect Offset(Rect r, Rect parent) => new Rect(parent.x + r.x, parent.y + r.y, r.width, r.height);

        // 이미 옮겼으면 보드에서, 아니면 패널 바로 아래에서 찾는다(최신 메뉴 안에도 Background가 있어 깊이 찾지 않는다)
        static RectTransform Child(RectTransform board, RectTransform panel, string name)
        {
            var t = (board.Find(name) ?? panel.Find(name)) as RectTransform;
            if (t == null) Debug.LogError($"[ARFieldHelpDetailLayout] HelpDetailPanel 아래 {name} 없음");
            return t;
        }

        static RectTransform Need(Transform root, string name)
        {
            var t = root.Find(name) as RectTransform;
            if (t == null) Debug.LogError($"[ARFieldHelpDetailLayout] {root.name} 아래 {name} 없음");
            return t;
        }

        // 도움 화면은 실행 중에만 켜지므로 켜고 메가커피 예시 메뉴를 넣어 태블릿(16:10, 4:3)·폰·16:9로 찍는다. 씬은 저장하지 않는다.
        // 변경점: 태블릿은 메뉴 MenuCardCount개(예시 이름)를 넣은 스크롤 모양도 찍는다(tab_ARFieldHelpDetail_scroll.png, 맨 위 상태).
        //   채팅창을 연 모양(예시 보낸 문장)도 태블릿·폰으로 찍는다(<tag>_ARFieldHelpDetail_chat.png).
        [MenuItem("Artti/Debug/Capture ARField Help Detail - Tab + 4x3 + Phone + 16x9")]
        static void CaptureHelpDetail()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = EditorSceneManager.GetActiveScene().path;
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            var many = new List<LatestMenuItem>();
            for (int i = 1; i <= MenuCardCount; i++) many.Add(new LatestMenuItem($"예시 메뉴 {i}", "나중에 넣을 메뉴 설명", "0,000원"));
            try
            {
                var chatSample = new[] { "따뜻한 아메리카노 주세요", "포장해 주세요", "화장실이 어디예요?", "카드로 결제할게요",
                    "얼음은 조금만 넣어 주세요", "감사합니다" };
                foreach (var (w, h, tag, mode) in new[] { (2560, 1600, "tab", 0), (2048, 1536, "tab43", 0), (2400, 1080, "phone", 0),
                             (1920, 1080, "169", 0), (2560, 1600, "tab", 1), (2560, 1600, "tab", 2), (2400, 1080, "phone", 2) })
                {
                    EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
                    foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        var panel = root.transform.Find("HelpDetailPanel");
                        if (panel == null) continue;
                        foreach (Transform sib in root.transform)
                            if (sib is RectTransform) sib.gameObject.SetActive(sib == panel);
                        var menu = panel.GetComponentInChildren<LatestMenuController>(true);
                        if (menu == null) continue;
                        if (mode == 1) menu.SetMenus(many);
                        else menu.ShowMegaMenu();
                    }
                    string file = Path.Combine(outDir, mode == 1 ? $"{tag}_ARFieldHelpDetail_scroll.png"
                        : mode == 2 ? $"{tag}_ARFieldHelpDetail_chat.png" : $"{tag}_ARFieldHelpDetail.png");
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);   // 첫 번은 캔버스 크기 맞추기(보드 배율이 정해진다)
                    if (mode == 2)
                    {
                        var drawer = Object.FindFirstObjectByType<HelpChatDrawerView>(FindObjectsInactive.Include);
                        if (drawer != null) drawer.PreviewOpen(chatSample);
                    }
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            Debug.Log($"[ARFieldHelpDetailLayout] 도움 화면 캡처 -> {outDir}");
        }
    }
}
