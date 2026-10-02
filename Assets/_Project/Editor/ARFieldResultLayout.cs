using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // AR 현장모드 결과 화면(ARFieldScene / Canvas/ResultPanel)을 HJ 시안(Art/UI/Home/HJ 92~96)으로 바꾼다.
    // ARFieldScene은 손배치 씬이라 다시 만들지 않고, 결과 화면에 이미 있는 오브젝트를 옮기고 모양만 바꾼다.
    //   OcrTestController / NaverStaticMapController / 버튼 클릭(설명 듣기 = TTS, 도움 카드 = 도움 패널)의 연결은 그대로 둔다.
    //
    // 구성
    //   ResultBackground = 92(패널 없는 배경, 화면 cover)
    //   Board = 93(세 패널이 그려진 배경, 1536x1024 기준). BoardFitter가 태블릿은 덮고, 폰은 위아래가 안 잘리게 줄인다(좌우는 92가 보임).
    //     모든 글자/사진/지도/버튼은 Board 안에 93 그림 px 좌표로 놓아 그려진 패널에 딱 맞는다.
    //   93의 가장자리를 녹인 그림과 뒤로 가기 링은 Art_Source/ui_arfield/make_derived.py가 만든다.
    // 여러 번 실행해도 같은 결과가 되게(이미 옮긴 오브젝트는 이름으로 다시 찾는다) 만들었다.
    public static class ARFieldResultLayout
    {
        const string Dir = "Assets/_Project/Art/UI/Home/HJ/";
        const string BgPath = Dir + "92.png";
        const string BoardPath = Dir + "derived/93_board.png";
        const string BackRingPath = Dir + "derived/back_ring.png";
        const string ListenPath = Dir + "94.png";
        const string HelpPath = Dir + "95.png";
        const string HintPath = Dir + "96.png";
        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";   // 모서리 반지름 44(1:1)

        static readonly Vector2 BoardSize = new Vector2(1536f, 1024f);
        // 폰에서도 꼭 보여야 하는 영역: 제목 위 ~ 안내 바 아래
        static readonly Rect BoardSafe = Rect.MinMaxRect(20f, 48f, 1516f, 982f);

        // ===== 93 그림 속 자리 (px, 좌상단 기준, y 아래가 +) =====
        static readonly Rect Card1 = Rect.MinMaxRect(25f, 220f, 510f, 815f);     // 촬영 이미지
        static readonly Rect Card2 = Rect.MinMaxRect(528f, 220f, 1041f, 815f);   // 위치 정보
        static readonly Rect Card3 = Rect.MinMaxRect(1061f, 220f, 1514f, 815f);  // 인식 결과
        const float HeadTitleY = 250f, HeadSubY = 290f, HeadTextX = 125f;      // 카드 헤더(아이콘 원 오른쪽), x는 카드 왼쪽에서
        static readonly Rect PhotoBox = Rect.MinMaxRect(52f, 346f, 486f, 793f);
        static readonly Rect MapBox = Rect.MinMaxRect(552f, 346f, 1020f, 691f);
        static readonly Rect CoordBar = Rect.MinMaxRect(548f, 710f, 1024f, 795f);
        static readonly Rect LatText = Rect.MinMaxRect(616f, 726f, 772f, 778f);   // 조준 아이콘 ~ 가운데 세로줄
        static readonly Rect LonText = Rect.MinMaxRect(850f, 726f, 1016f, 778f);  // 핀 아이콘 오른쪽
        // 분류 칩 = 97 그림(네온 테두리 유리 막대, 본체 1962x300 약 6.5:1). 아이콘 칸 오른쪽 ~ 안쪽 칸 오른쪽 끝, 아이콘과 같은 가운데 높이(394)
        // 변경점: 아이콘 칸과 큰 이름을 없애고 칩(지점 이름)을 그 자리 왼쪽 끝에 크게 둔다. 칩 폭은 실행 중 이름 길이에 맞춰 바뀐다(오른쪽 끝 ChipBox.xMax까지).
        static readonly Rect ChipBox = Rect.MinMaxRect(1100f, 374f, 1478f, 430f);   // 오른쪽 = 최대 폭(안쪽 테두리에서 10px 띄움)
        const float ChipAlpha = 1f;      // 그림 그대로(필요하면 낮춰 뒤가 비치게)
        // 변경점: 분류 칩 그림 = 97(네온 유리 막대)을 1024 폭으로 줄인 9-slice 그림. 칩 폭이 장소 이름 길이에 따라 바뀐다(Art_Source/ui_arfield/make_result_chip.py)
        const string ChipSlicedPath = Dir + "derived/97_chip.png";
        static readonly Vector2 ChipSlicedSize = new Vector2(1024f, 373f);
        static readonly Rect ChipSlicedBody = Rect.MinMaxRect(28.6f, 109.4f, 995.4f, 257.2f);
        // 9-slice 가장자리(그림 px): 양끝 = 둥근 부분 + 빛, 위/아래 = 둥근 모서리(반지름 약 50) + 빛. 두 줄로 높아져도 모서리가 그대로다
        const float ChipCapPx = 128f, ChipCapTopPx = 165f, ChipCapBottomPx = 168f;
        const float ChipPadding = 26f, ChipMinWidth = 120f, ChipFontMax = 30f, ChipFontMin = 18f;
        static readonly Rect DescBox = Rect.MinMaxRect(1100f, 452f, 1476f, 600f);   // 칩 바로 아래
        const float RowX0 = 1086f, RowX1 = 1488f, RowTop = 632f, RowH = 51f, RowTextX = 1160f, RowTextW = 282f;

        // 위쪽 머리
        static readonly Rect BackBox = Rect.MinMaxRect(50f, 54f, 134f, 138f);
        static readonly Rect BrandBox = Rect.MinMaxRect(156f, 66f, 420f, 126f);
        static readonly Rect TitleBox = Rect.MinMaxRect(518f, 36f, 1018f, 148f);   // 가로 빛줄(y 140) 위
        static readonly Rect SubBox = Rect.MinMaxRect(418f, 148f, 1118f, 196f);

        // 아래 버튼 / 안내 바 (본체 높이 기준, 그림은 빛 번짐만큼 바깥으로 넘친다)
        const float ButtonY = 833f, ButtonH = 78f, ButtonGap = 30f;
        const float HintY = 925f, HintH = 50f;
        static readonly Vector2 PillSize = new Vector2(2172f, 724f);
        static readonly Rect ListenBody = Rect.MinMaxRect(119f, 120f, 2056f, 599f);   // 94 알파>200
        static readonly Rect HelpBody = Rect.MinMaxRect(81f, 100f, 2090f, 599f);      // 95
        static readonly Rect HintBody = Rect.MinMaxRect(87f, 229f, 2086f, 489f);      // 96

        static readonly Color TextWhite = new Color(0.96f, 0.97f, 1f, 1f);
        static readonly Color TextSub = new Color(0.78f, 0.85f, 1f, 0.95f);
        static readonly Color TextBody = new Color(0.86f, 0.9f, 1f, 1f);

        [MenuItem("Artti/Fix/ARField - Result Panel HJ Design (keep hand placement)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ARFieldResultLayout] Play 모드에서는 실행하지 않는다."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ARField)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
            }

            RectTransform panel = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.transform.Find("ResultPanel");
                if (t != null) { panel = (RectTransform)t; break; }
            }
            if (panel == null) { Debug.LogError("[ARFieldResultLayout] Canvas/ResultPanel 없음"); return; }

            var controller = Object.FindFirstObjectByType<OcrTestController>(FindObjectsInactive.Include);
            if (controller == null) { Debug.LogError("[ARFieldResultLayout] OcrTestController 없음"); return; }
            var cso = new SerializedObject(controller);
            var nameText = cso.FindProperty("categoryText").objectReferenceValue as TMP_Text;
            var descText = cso.FindProperty("descriptionText").objectReferenceValue as TMP_Text;
            var aacImage = cso.FindProperty("aacImageComponent").objectReferenceValue as Image;
            var badge = cso.FindProperty("categoryBadgeImage").objectReferenceValue as Image;
            var photo = cso.FindProperty("capturedImage").objectReferenceValue as RawImage;
            if (nameText == null || descText == null || badge == null || photo == null)
            { Debug.LogError("[ARFieldResultLayout] OcrTestController 결과 칸 연결이 비어 있음"); return; }

            // ===== 배경 =====
            var bg = Need(panel, "ResultBackground");
            if (bg == null) return;
            bg.SetAsFirstSibling();
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = LoadSprite(BgPath, 2048);
            bgImg.color = Color.white;
            bgImg.type = Image.Type.Simple;
            var fitter = bg.GetComponent<CoverImageFitter>() ?? bg.gameObject.AddComponent<CoverImageFitter>();
            fitter.Setup(1448f / 1086f, 1f, new Vector2(0.5f, 0.5f));

            var board = FindDeep(panel, "Board") ?? NewRect("Board", panel);
            board.SetSiblingIndex(1);
            var bf = board.GetComponent<BoardFitter>() ?? board.gameObject.AddComponent<BoardFitter>();
            bf.Setup(BoardSize, BoardSafe, 4f);
            var boardImgRt = FindDeep(board, "BoardImage") ?? NewRect("BoardImage", board);
            boardImgRt.SetAsFirstSibling();
            Stretch(boardImgRt);
            var boardImg = GetOrAdd<Image>(boardImgRt);
            boardImg.sprite = LoadSprite(BoardPath, 2048);
            boardImg.raycastTarget = false;
            var boardRect = new Rect(Vector2.zero, BoardSize);

            // ===== 위쪽 머리 =====
            var back = Need(panel, "BackButton");
            var brand = Need(panel, "ArttiBrand");
            var title = Need(panel, "HeaderTitle");
            var subtitle = Need(panel, "HeaderSubtitle");
            if (back == null || brand == null || title == null || subtitle == null) return;
            Place(back, board, BackBox, boardRect);
            var backImg = back.GetComponent<Image>();
            backImg.sprite = LoadSprite(BackRingPath, 512);
            backImg.color = Color.white;
            backImg.type = Image.Type.Simple;
            backImg.preserveAspect = true;
            DisableOutline(back);
            var arrow = back.Find("ArrowImage");
            if (arrow != null) arrow.gameObject.SetActive(false);   // 화살표는 링 그림에 들어 있다

            Place(brand, board, BrandBox, boardRect);
            Style(brand, 36f, false, TextWhite, TextAlignmentOptions.MidlineLeft).characterSpacing = 22f;
            Place(title, board, TitleBox, boardRect);
            Style(title, 70f, true, TextWhite, TextAlignmentOptions.Center);
            Place(subtitle, board, SubBox, boardRect);
            Style(subtitle, 28f, false, TextSub, TextAlignmentOptions.Center);

            // ===== 카드 1: 촬영 이미지 =====
            var c1 = Need(panel, "LeftPhotoCard");
            var c2 = Need(panel, "CenterMapCard");
            var c3 = Need(panel, "RightResultCard");
            if (c1 == null || c2 == null || c3 == null) return;
            foreach (var c in new[] { c1, c2, c3 }) HideCardSurface(c);
            Place(c1, board, Card1, boardRect);
            CardHeader(c1, Card1, "촬영 이미지", "촬영한 사진입니다.");
            var photoMask = FindDeep(c1, "PhotoMask") ?? NewRect("PhotoMask", c1);
            Place(photoMask, c1, PhotoBox, Card1);
            RoundedMask(photoMask, 18f);
            var photoRt = (RectTransform)photo.transform;
            photoRt.SetParent(photoMask, false);
            Stretch(photoRt);
            GetOrAdd<RawImageCover>(photoRt);

            // ===== 카드 2: 위치 정보 =====
            Place(c2, board, Card2, boardRect);
            CardHeader(c2, Card2, "위치 정보", "인식된 위치를 지도에서 확인하세요.");
            var mapFrame = Need(c2, "MapFrame");
            var coord = Need(c2, "CoordinatePanel");
            if (mapFrame == null || coord == null) return;
            Place(mapFrame, c2, MapBox, Card2);
            DisableOutline(mapFrame);
            RoundedMask(mapFrame, 18f);
            foreach (Transform child in mapFrame)
            {
                var rt = (RectTransform)child;
                if (child.name == "MyLocationMarker")
                {
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(40f, 40f);
                }
                else Stretch(rt);
                if (child.name == "MapPlaceholder" && child.TryGetComponent<Image>(out var ph))
                    ph.color = new Color(0.02f, 0.07f, 0.2f, 0.35f);   // 지도 오기 전에는 그려진 유리가 비치게
            }
            Place(coord, c2, CoordBar, Card2);
            if (coord.TryGetComponent<Image>(out var coordImg)) coordImg.enabled = false;   // 칸은 93에 그려져 있다
            var lat = Need(coord, "LatitudeText");
            var lon = Need(coord, "LongitudeText");
            if (lat == null || lon == null) return;
            Place(lat, coord, LatText, CoordBar);
            Place(lon, coord, LonText, CoordBar);
            foreach (var t in new[] { lat, lon })
            {
                var tmp = Style(t, 22f, false, TextWhite, TextAlignmentOptions.MidlineLeft);
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 13f;
                tmp.fontSizeMax = 22f;
            }

            // ===== 카드 3: 인식 결과 =====
            Place(c3, board, Card3, boardRect);
            CardHeader(c3, Card3, "인식 결과", "사진을 분석한 결과입니다.");
            var chip = (RectTransform)badge.transform;
            DisableOutline(chip);
            // 변경점: 칩 그림 = 97. OcrTestController가 이 Image에 분류별 색을 칠하는데, 97에 색이 곱해지면 탁해지므로
            //   이 Image는 끄고(연결은 그대로) 자식 Visual에 97을 그대로 그린다.
            badge.enabled = false;
            var chipTextRt = FindDeep(chip, "ChipText") ?? NewRect("ChipText", chip);
            var chipText = GetOrAdd<TextMeshProUGUI>(chipTextRt);
            chipText.font = nameText.font;
            PlaceNameCard(c3, chip, chipText, aacImage, nameText, descText);

            // 아래 세 줄(아이콘과 > 는 93에 그려져 있다). 줄 전체가 누르는 칸.
            var rows = FindDeep(c3, "InfoRows") ?? NewRect("InfoRows", c3);
            Place(rows, c3, Rect.MinMaxRect(RowX0, RowTop, RowX1, RowTop + RowH * 3f), Card3);
            var rowTexts = new TMP_Text[3];
            var rowButtons = new Button[3];
            var rowsRect = Rect.MinMaxRect(RowX0, RowTop, RowX1, RowTop + RowH * 3f);
            string[] sample = { "장소 이름", "장소 관련 의사소통 문구", "도움 카드 보기" };   // 자리 표시(실행하면 ARResultView가 채움)
            for (int i = 0; i < 3; i++)
            {
                var row = FindDeep(rows, "Row" + i) ?? NewRect("Row" + i, rows);
                var rowRect = new Rect(RowX0, RowTop + RowH * i, RowX1 - RowX0, RowH);
                Place(row, rows, rowRect, rowsRect);
                var hit = GetOrAdd<Image>(row);
                hit.color = new Color(1f, 1f, 1f, 0f);
                rowButtons[i] = GetOrAdd<Button>(row);
                rowButtons[i].targetGraphic = hit;
                rowButtons[i].transition = Selectable.Transition.None;
                var label = FindDeep(row, "Label") ?? NewRect("Label", row);
                Place(label, row, new Rect(RowTextX, rowRect.y, RowTextW, RowH), rowRect);
                var tmp = GetOrAdd<TextMeshProUGUI>(label);
                tmp.font = nameText.font;
                Style(label, 20f, false, TextWhite, TextAlignmentOptions.MidlineLeft).text = sample[i];
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 13f;
                tmp.fontSizeMax = 20f;
                tmp.overflowMode = TextOverflowModes.Ellipsis;   // 아주 긴 지점명은 줄임표(칸 높이 51은 한 줄보다 넉넉)
                rowTexts[i] = tmp;
            }

            // ===== 아래 버튼 / 안내 바 =====
            var listen = Need(panel, "Listencard");
            var help = Need(panel, "HelpCard");
            if (listen == null || help == null) return;
            float listenW = ButtonH * ListenBody.width / ListenBody.height;
            float helpW = ButtonH * HelpBody.width / HelpBody.height;
            float x0 = BoardSize.x * 0.5f - (listenW + ButtonGap + helpW) * 0.5f;
            ImageButton(listen, board, ListenPath, ListenBody, new Rect(x0, ButtonY, listenW, ButtonH), boardRect);
            ImageButton(help, board, HelpPath, HelpBody, new Rect(x0 + listenW + ButtonGap, ButtonY, helpW, ButtonH), boardRect);

            float hintW = HintH * HintBody.width / HintBody.height;
            var hint = FindDeep(board, "HintBar") ?? NewRect("HintBar", board);
            Place(hint, board, new Rect(BoardSize.x * 0.5f - hintW * 0.5f, HintY, hintW, HintH), boardRect);
            Visual(hint, HintPath, HintBody, PillSize);

            // ===== 남은 예전 틀 정리 =====
            var oldSurface = panel.Find("MainSurface");
            if (oldSurface != null) Object.DestroyImmediate(oldSurface.gameObject);   // 헤더 장식/카드 틀(옮길 것은 위에서 다 옮김)
            var setter = panel.GetComponent<ResultPanelLayoutSetter>();
            if (setter != null) Object.DestroyImmediate(setter);   // 예전 배치 도구(실행하면 새 배치를 덮어씀)

            // 칸 채우기(분류 칩, 큰 이름, 세 줄)
            var view = GetOrAdd<ARResultView>(panel);
            view.Setup(chip.gameObject, chipText, nameText, aacImage, rowTexts, rowButtons, help.GetComponent<Button>(),
                Object.FindFirstObjectByType<TTSManager>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<KakaoLocalSearcher>(FindObjectsInactive.Include));
            EditorUtility.SetDirty(view);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ARFieldResultLayout] 결과 화면 HJ 시안 적용 완료 - 다른 패널/연결은 그대로");
        }

        // 변경점: 인식 결과 카드 위쪽만 다시 맞춘다(결과 화면 전체 Fix를 다시 돌리지 않아도 되게).
        //   칩/큰 이름/아이콘은 ARResultView 연결, 설명 글은 OcrTestController 연결에서 찾는다.
        [MenuItem("Artti/Fix/ARField - Result Place Name Chip (result card only)")]
        public static void ApplyChipOnly()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ARFieldResultLayout] Play 모드에서는 실행하지 않는다."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ARField)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
            }
            var view = Object.FindFirstObjectByType<ARResultView>(FindObjectsInactive.Include);
            var controller = Object.FindFirstObjectByType<OcrTestController>(FindObjectsInactive.Include);
            if (view == null || controller == null) { Debug.LogError("[ARFieldResultLayout] ARResultView/OcrTestController 없음 - 결과 화면 Fix(HJ Design)를 먼저 실행"); return; }
            var so = new SerializedObject(view);
            var chip = so.FindProperty("chip").objectReferenceValue as GameObject;
            var chipText = so.FindProperty("chipText").objectReferenceValue as TMP_Text;
            var nameText = so.FindProperty("nameText").objectReferenceValue as TMP_Text;
            var icon = so.FindProperty("iconImage").objectReferenceValue as Image;
            var descText = new SerializedObject(controller).FindProperty("descriptionText").objectReferenceValue as TMP_Text;
            if (chip == null || chipText == null || descText == null) { Debug.LogError("[ARFieldResultLayout] 칩/설명 글 연결이 비어 있음"); return; }
            var c3 = (RectTransform)chip.transform.parent;
            PlaceNameCard(c3, (RectTransform)chip.transform, chipText, icon, nameText, descText);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ARFieldResultLayout] 칩 완료 - 아이콘/큰 이름 숨김, 칩에 지점 이름(길이에 맞춰 폭이 바뀜)");
        }

        // 변경점: 인식 결과 카드 위쪽 = 지점 이름 칩 + 설명. 흰 아이콘 칸, 분류 아이콘, 큰 이름은 끈다(지우지 않음, 다른 코드 연결은 그대로).
        static void PlaceNameCard(RectTransform c3, RectTransform chip, TMP_Text chipText, Image aacImage, TMP_Text nameText, TMP_Text descText)
        {
            var iconSurface = FindDeep(c3, "CategoryIconSurface");
            if (iconSurface != null) iconSurface.gameObject.SetActive(false);
            if (aacImage != null) aacImage.gameObject.SetActive(false);
            if (nameText != null) nameText.gameObject.SetActive(false);

            var descRt = (RectTransform)descText.transform;
            Place(descRt, c3, DescBox, Card3);
            var descTmp = Style(descRt, 19f, false, TextBody, TextAlignmentOptions.TopLeft);
            descTmp.textWrappingMode = TextWrappingModes.Normal;
            descTmp.enableAutoSizing = true;
            descTmp.fontSizeMin = 13f;
            descTmp.fontSizeMax = 19f;
            descTmp.lineSpacing = 6f;

            Place(chip, c3, ChipBox, Card3);
            SetupChip(chip, chipText, descRt);   // 칩이 두 줄로 높아지면 설명 칸 위쪽도 같이 내려간다
        }

        // 변경점: 분류 칩 = 97 그림(9-slice: 양끝 둥근 부분은 그대로, 가운데만 늘어남) + 장소 이름.
        //   칩 크기는 실행 중 TextChipAutoWidth가 이름 길이에 맞춰 바꾼다(왼쪽 위 고정, 오른쪽으로 ChipBox 끝까지,
        //   넘치면 띄어쓰기에서 두 줄로 바꾸고 아래로 높아진다. below = 같이 내릴 칸).
        internal static void SetupChip(RectTransform chip, TMP_Text chipText, RectTransform below = null)
        {
            float k = ChipBox.height / ChipSlicedBody.height;   // 그림 px -> 보드 px(높이 기준이라 둥근 끝이 찌그러지지 않는다)
            var visual = FindDeep(chip, "Visual") ?? NewRect("Visual", chip);
            visual.gameObject.SetActive(true);
            visual.SetAsFirstSibling();
            // 칩(막대 본체) 크기를 따라 늘어나고, 빛 번짐만큼 사방으로 넘친다
            visual.anchorMin = Vector2.zero;
            visual.anchorMax = Vector2.one;
            visual.pivot = new Vector2(0.5f, 0.5f);
            visual.localScale = Vector3.one;
            visual.offsetMin = new Vector2(-ChipSlicedBody.xMin * k, -(ChipSlicedSize.y - ChipSlicedBody.yMax) * k);
            visual.offsetMax = new Vector2((ChipSlicedSize.x - ChipSlicedBody.xMax) * k, ChipSlicedBody.yMin * k);
            var img = GetOrAdd<Image>(visual);
            img.sprite = LoadChipSprite();
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = 1f / k;   // 9-slice 가장자리 그림 px -> 보드 px(x k)
            img.preserveAspect = false;
            img.color = new Color(1f, 1f, 1f, ChipAlpha);
            img.raycastTarget = false;

            var textRt = (RectTransform)chipText.transform;
            Stretch(textRt);
            textRt.offsetMin = new Vector2(ChipPadding, 0f);
            textRt.offsetMax = new Vector2(-ChipPadding, 0f);
            Style(textRt, ChipFontMax, true, TextWhite, TextAlignmentOptions.Center).text = "장소";   // 자리 표시(실행하면 장소 이름으로)
            textRt.SetAsLastSibling();

            var fit = GetOrAdd<TextChipAutoWidth>(chip);
            var so = new SerializedObject(fit);
            so.FindProperty("label").objectReferenceValue = chipText;
            so.FindProperty("padding").floatValue = ChipPadding;
            so.FindProperty("minWidth").floatValue = ChipMinWidth;
            so.FindProperty("maxWidth").floatValue = ChipBox.width;
            so.FindProperty("fontMax").floatValue = ChipFontMax;
            so.FindProperty("fontMin").floatValue = ChipFontMin;
            so.FindProperty("baseHeight").floatValue = ChipBox.height;
            so.FindProperty("below").objectReferenceValue = below;
            if (below != null)
            {
                so.FindProperty("belowBaseY").floatValue = below.anchoredPosition.y;
                so.FindProperty("belowBaseHeight").floatValue = below.sizeDelta.y;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Sprite LoadChipSprite()
        {
            LoadSprite(ChipSlicedPath, 1024);
            var border = new Vector4(ChipCapPx, ChipCapBottomPx, ChipCapPx, ChipCapTopPx);   // 왼쪽, 아래, 오른쪽, 위
            if (AssetImporter.GetAtPath(ChipSlicedPath) is TextureImporter ti && ti.spriteBorder != border)
            {
                ti.spriteBorder = border;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(ChipSlicedPath);
        }

        // ===== 도우미 =====

        static RectTransform Need(Transform root, string name)
        {
            var t = FindDeep(root, name);
            if (t == null) Debug.LogError($"[ARFieldResultLayout] {root.name} 아래 {name} 없음");
            return t;
        }

        internal static RectTransform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
                if (t != root && t.name == name) return t;
            return null;
        }

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static T GetOrAdd<T>(Component c) where T : Component
            => c.TryGetComponent<T>(out var x) ? x : c.gameObject.AddComponent<T>();

        // rect(그림 px)를 parent(그림 속 parentRect 자리) 안에 좌상단 기준으로 놓는다
        internal static void Place(RectTransform rt, RectTransform parent, Rect rect, Rect parentRect)
        {
            if (rt.parent != parent) rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.anchoredPosition = new Vector2(rect.x - parentRect.x, -(rect.y - parentRect.y));
            rt.sizeDelta = rect.size;
            if (rt.TryGetComponent<LayoutElement>(out var le)) le.ignoreLayout = true;
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        internal static TMP_Text Style(RectTransform rt, float size, bool bold, Color color, TextAlignmentOptions align)
        {
            var t = rt.GetComponent<TMP_Text>();
            t.fontSize = size;
            t.enableAutoSizing = false;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            // Noto Sans KR은 줄 높이가 글자 크기의 약 1.45배라, 칸이 한 줄보다 낮으면 Ellipsis/Truncate가 줄을 통째로 숨긴다. 기본은 넘침 허용.
            t.overflowMode = TextOverflowModes.Overflow;
            t.characterSpacing = 0f;
            t.margin = Vector4.zero;
            t.raycastTarget = false;
            return t;
        }

        // 카드 틀 그림/테두리는 93에 그려져 있으니 끈다(오브젝트와 붙어 있는 기능은 그대로)
        static void HideCardSurface(RectTransform card)
        {
            if (card.TryGetComponent<Image>(out var img)) img.enabled = false;
            if (card.TryGetComponent<Mask>(out var mask)) mask.enabled = false;
            DisableOutline(card);
        }

        internal static void DisableOutline(Component c)
        {
            foreach (var o in c.GetComponents<Shadow>()) o.enabled = false;   // Outline은 Shadow를 상속
        }

        static void CardHeader(RectTransform card, Rect cardRect, string title, string sub)
        {
            var t = Need(card, "TitleText");
            var s = Need(card, "SubtitleText");
            if (t == null || s == null) return;
            float x = cardRect.x + HeadTextX, w = cardRect.width - HeadTextX - 16f;
            Place(t, card, new Rect(x, HeadTitleY - 4f, w, 48f), cardRect);
            Style(t, 30f, true, TextWhite, TextAlignmentOptions.MidlineLeft).text = title;
            Place(s, card, new Rect(x, HeadSubY, w, 32f), cardRect);
            Style(s, 20f, false, TextSub, TextAlignmentOptions.MidlineLeft).text = sub;
        }

        // 둥근 모서리로 자식을 자르는 칸(그림은 안 보이고 모양만)
        static void RoundedMask(RectTransform rt, float radius)
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

        // 버튼 루트 = 본체 크기 클릭 칸(투명), 자식 Visual = 빛 번짐까지 든 그림. 버튼 연결(onClick)은 그대로.
        static void ImageButton(RectTransform root, RectTransform board, string path, Rect body, Rect rect, Rect boardRect)
        {
            Place(root, board, rect, boardRect);
            DisableOutline(root);
            if (root.TryGetComponent<Image>(out var hit)) { hit.sprite = null; hit.color = new Color(1f, 1f, 1f, 0f); }
            foreach (Transform child in root)
                if (child.name != "Visual") child.gameObject.SetActive(false);   // 예전 글자/아이콘(그림에 들어 있다)
            Visual(root, path, body, PillSize);
        }

        // 본체(body) 크기로 놓인 root 안에, 빛 번짐까지 든 그림 전체(imageSize px)를 본체가 root와 겹치게 놓는다
        internal static void Visual(RectTransform root, string path, Rect body, Vector2 imageSize, int maxSize = 1024)
        {
            var v = FindDeep(root, "Visual") ?? NewRect("Visual", root);
            v.gameObject.SetActive(true);
            v.SetAsFirstSibling();
            float k = root.sizeDelta.x / body.width;
            v.anchorMin = v.anchorMax = v.pivot = new Vector2(0.5f, 0.5f);
            v.sizeDelta = imageSize * k;
            v.anchoredPosition = new Vector2((imageSize.x * 0.5f - body.center.x) * k, (body.center.y - imageSize.y * 0.5f) * k);
            v.localScale = Vector3.one;
            var img = GetOrAdd<Image>(v);
            img.sprite = LoadSprite(path, maxSize);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Color.white;
        }

        internal static Sprite LoadSprite(string path, int maxSize)
        {
            if (AssetImporter.GetAtPath(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (ti.maxTextureSize != maxSize) { ti.maxTextureSize = maxSize; dirty = true; }
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
                if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[ARFieldResultLayout] 이미지 없음: {path}");
            return sprite;
        }
    }
}
