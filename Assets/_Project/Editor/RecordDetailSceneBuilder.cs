using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 세션 1개의 상세 리포트 씬 (시안: hh.png).
    // 헤더 / 전체 성공률 도넛 / 4 스탯카드 / AI 리드백(캐릭터+3컬럼) / 연습 상세 타임라인 / 하단 3버튼.
    // 캐릭터는 레포트 씬 여성 상반신(ReportCH.png)을 임시 사용.
    public static class RecordDetailSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color32 Primary    = new Color32(26, 86, 219, 255);
        static readonly Color32 Bg         = new Color32(233, 238, 247, 255); // 페이지 바탕(카드가 떠보이게)
        static readonly Color32 Card       = new Color32(255, 255, 255, 255);
        static readonly Color32 Glass      = new Color32(244, 248, 254, 255); // 글래스 패널(연한 블루)
        static readonly Color32 Divider    = new Color32(226, 231, 240, 255);
        static readonly Color32 Title      = new Color32(33, 41, 60, 255);
        static readonly Color32 Sub        = new Color32(110, 118, 135, 255);
        static readonly Color32 RingGray   = new Color32(229, 233, 240, 255);
        static readonly Color32 Green      = new Color32(30, 158, 85, 255);
        static readonly Color32 Orange     = new Color32(235, 140, 52, 255);
        static readonly Color32 Red        = new Color32(224, 64, 64, 255);
        static readonly Color   White      = Color.white;

        const string RoundedPath   = "Assets/_Project/Art/UI/RoundedRect.png";
        const string RingPath       = "Assets/_Project/Art/UI/DonutRing.png";
        const string CharacterPath  = "Assets/_Project/Art/UI/ReportCH.png";
        const string IconDir        = "Assets/_Project/Art/UI/Report/Icons";

        [MenuItem("Artti/Build RecordDetailScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            EnsureSceneAsset(ScenePaths.RecordDetail);
            SceneBuilderUtils.OpenScene(ScenePaths.RecordDetail);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);
            var font = SceneBuilderUtils.GetKoreanFont();

            // 배경
            var bg = canvasGo.AddComponent<Image>();
            bg.color = Bg;

            var root = canvasGo.transform;

            // ===== 헤더 =====
            var backBtn = MakeCircleButton("BackButton", root, "←", font);
            PlaceTL(backBtn.GetComponent<RectTransform>(), 48, -44, 88, 88);

            var title = MakeText("Title", root, "레포트", 56, Title, font, bold: true, align: TextAlignmentOptions.Left);
            PlaceTL(title.rectTransform, 160, -40, 400, 72);
            var subtitle = MakeText("Subtitle", root, "이번 연습이 어떻게 진행되었는지 확인할 수 있어요.", 26, Sub, font, bold: false, align: TextAlignmentOptions.Left);
            PlaceTL(subtitle.rectTransform, 162, -108, 800, 40);

            var shareBtn = MakePill("ShareBtn", root, "리포트 공유", 28, Card, Title, font);
            PlaceTR(shareBtn.GetComponent<RectTransform>(), -48, -48, 220, 76);

            // ===== 전체 성공률 도넛 카드 =====
            var donutCard = MakeCard("DonutCard", root);
            PlaceTL(donutCard, 60, -150, 520, 270);
            var donutTitle = MakeText("Header", donutCard, "전체 성공률", 28, Title, font, bold: true, align: TextAlignmentOptions.Left);
            ChildTL(donutTitle.rectTransform, 36, -24, 300, 40);

            // 도넛(배경 링 + 채움 링) + 퍼센트
            var ringBg = MakeImage("RingBg", donutCard, RingPath);
            ChildTL(ringBg.rectTransform, 40, -78, 180, 180);
            ringBg.color = RingGray; ringBg.raycastTarget = false;
            var donutFill = MakeImage("RingFill", donutCard, RingPath);
            ChildTL(donutFill.rectTransform, 40, -78, 180, 180);
            donutFill.color = Primary; donutFill.raycastTarget = false;
            donutFill.type = Image.Type.Filled;
            donutFill.fillMethod = Image.FillMethod.Radial360;
            donutFill.fillOrigin = (int)Image.Origin360.Top;
            donutFill.fillClockwise = true;
            donutFill.fillAmount = 0f;
            var donutPct = MakeText("Percent", donutCard, "0<size=50%>%</size>", 44, Title, font, bold: true);
            ChildTL(donutPct.rectTransform, 40, -78, 180, 180);

            // 범례 3행 (완전/부분/도움)
            var fullVal    = MakeLegendRow(donutCard, "완전 성공", Green,  280, -86, font);
            var partialVal = MakeLegendRow(donutCard, "부분 성공", Orange, 280, -146, font);
            var helpVal    = MakeLegendRow(donutCard, "도움 필요", Red,    280, -206, font);

            // ===== 4 스탯 카드 =====
            // 시나리오(상세보기 버튼 포함) / 상태 / 연습 날짜 / 걸린 시간
            var scenarioCard = MakeCard("ScenarioCard", root);
            PlaceTL(scenarioCard, 620, -150, 288, 270);
            MakeIconIn(scenarioCard, IconDir + "/ic_store.png", 0, -24, 88);
            MakeText("Label", scenarioCard, "시나리오", 24, Sub, font, bold: false).Pipe(t => CenterIn(t.rectTransform, 0, -120, 240, 36));
            var scenarioName = MakeText("Value", scenarioCard, "-", 38, Title, font, bold: true);
            CenterIn(scenarioName.rectTransform, 0, -164, 240, 46);
            var detailBtn = MakePill("DetailBtn", scenarioCard.transform, "상세 보기", 22, new Color32(235, 240, 250, 255), Primary, font);
            CenterIn(detailBtn.GetComponent<RectTransform>(), 0, -220, 168, 44);

            var statusCard = MakeCard("StatusCard", root);
            PlaceTL(statusCard, 928, -150, 288, 270);
            MakeIconIn(statusCard, IconDir + "/ic_status.png", 0, -24, 88);
            MakeText("Label", statusCard, "상태", 24, Sub, font, bold: false).Pipe(t => CenterIn(t.rectTransform, 0, -120, 240, 36));
            var statusVal = MakeText("Value", statusCard, "-", 36, Green, font, bold: true);
            CenterIn(statusVal.rectTransform, 0, -164, 240, 52);

            var dateCard = MakeCard("DateCard", root);
            PlaceTL(dateCard, 1236, -150, 288, 270);
            MakeIconIn(dateCard, IconDir + "/ic_calendar.png", 0, -24, 88);
            MakeText("Label", dateCard, "연습 날짜", 24, Sub, font, bold: false).Pipe(t => CenterIn(t.rectTransform, 0, -120, 240, 36));
            var dateVal = MakeText("Value", dateCard, "-", 30, Title, font, bold: true);
            CenterIn(dateVal.rectTransform, 0, -164, 270, 52);

            var timeCard = MakeCard("TimeCard", root);
            PlaceTL(timeCard, 1544, -150, 288, 270);
            MakeIconIn(timeCard, IconDir + "/ic_clock.png", 0, -24, 88);
            MakeText("Label", timeCard, "걸린 시간", 24, Sub, font, bold: false).Pipe(t => CenterIn(t.rectTransform, 0, -120, 240, 36));
            var durationVal = MakeText("Value", timeCard, "-", 36, Title, font, bold: true);
            CenterIn(durationVal.rectTransform, 0, -164, 240, 52);

            // ===== AI 리드백 요약 =====
            var aiSecTitle = MakeText("AiTitle", root, "AI 리드백 요약", 28, Title, font, bold: true, align: TextAlignmentOptions.Left);
            PlaceTL(aiSecTitle.rectTransform, 64, -428, 400, 34);

            var aiPanel = MakeCard("AiPanel", root, Glass);
            PlaceTL(aiPanel, 60, -466, 1800, 188);

            // 좌: 따봉 + 메인 피드백
            var thumb = MakeImage("Thumb", aiPanel, IconDir + "/ic_thumb.png");
            thumb.raycastTarget = false;
            ChildTL(thumb.rectTransform, 28, -26, 56, 56);
            var feedbackMain = MakeText("FeedbackMain", aiPanel, "잘했어요! 꾸준히 연습하면 더 자연스러운 표현을 할 수 있어요.", 23, Title, font, bold: true, align: TextAlignmentOptions.TopLeft);
            feedbackMain.textWrappingMode = TextWrappingModes.Normal;
            ChildTL(feedbackMain.rectTransform, 98, -24, 432, 156);

            MakeDivider(aiPanel, 562, -22, 152);

            // 중: 말풍선 + 캐릭터
            var bubble = MakeCard("Bubble", aiPanel, White);
            ChildTL(bubble, 584, -30, 196, 66);
            var bubbleText = MakeText("Text", bubble, "다음 연습도\n화이팅이에요! 💙", 19, Title, font, bold: false);
            bubbleText.textWrappingMode = TextWrappingModes.Normal;
            StretchFull(bubbleText.rectTransform, 10);
            var character = MakeImage("Character", aiPanel, CharacterPath);
            character.preserveAspect = true; character.raycastTarget = false;
            ChildTL(character.rectTransform, 800, -6, 192, 186);

            MakeDivider(aiPanel, 1024, -22, 152);

            // 우: 3 컬럼
            var goodPoints   = MakeFeedbackColumn(aiPanel, "잘한 점",       Green,   IconDir + "/ic_thumb.png",  1058, font);
            var improvePoints= MakeFeedbackColumn(aiPanel, "연습하면 좋아요", Orange,  IconDir + "/ic_status.png", 1306, font);
            var nextGoals    = MakeFeedbackColumn(aiPanel, "다음 목표",      Primary, IconDir + "/ic_flag.png",   1556, font);

            // ===== 연습 상세 기록 (타임라인) =====
            var tlTitle = MakeText("TimelineTitle", root, "연습 상세 기록", 28, Title, font, bold: true, align: TextAlignmentOptions.Left);
            PlaceTL(tlTitle.rectTransform, 64, -672, 400, 32);

            // 글래스 패널로 전체 감싸기. 연결선/색점/카드/깃발 모두 패널 자식. 하단 3버튼보다 위로.
            var tlPanel = MakeCard("TimelinePanel", root, Glass);
            PlaceTL(tlPanel, 60, -710, 1800, 236);

            const float cardW = 280f, gap = 24f, leftPad = 0f;
            float firstCenter = leftPad + cardW / 2f;
            float lastCenter  = leftPad + 5 * (cardW + gap) + cardW / 2f;

            var connector = ChildRect("Connector", tlPanel);
            ChildTL(connector, firstCenter, -27, (lastCenter - firstCenter) + 110, 6);
            var connImg = connector.gameObject.AddComponent<Image>();
            connImg.sprite = Rounded(); connImg.type = Image.Type.Sliced; connImg.pixelsPerUnitMultiplier = 1f;
            connImg.color = RingGray; connImg.raycastTarget = false;

            Color32[] dotColors = { Primary, Green, Orange, new Color32(150, 90, 220, 255), Primary, Green };
            string[] spk = { "spk_blue", "spk_green", "spk_orange", "spk_purple", "spk_blue", "spk_green" };
            var stepCards = new RecordStepCard[6];
            for (int i = 0; i < 6; i++)
            {
                float x = leftPad + i * (cardW + gap);
                var dot = ChildRect("Dot" + i, tlPanel);
                ChildTL(dot, x + cardW / 2f - 12, -18, 24, 24);
                var dotImg = dot.gameObject.AddComponent<Image>();
                dotImg.sprite = Rounded(); dotImg.color = dotColors[i]; dotImg.raycastTarget = false;

                stepCards[i] = MakeStepCard("Step" + i, tlPanel, x, -44, i + 1, dotColors[i], IconDir + "/" + spk[i] + ".png", font);
            }
            var flag = MakeImage("Flag", tlPanel, IconDir + "/ic_flag.png");
            flag.raycastTarget = false;
            ChildTL(flag.GetComponent<RectTransform>(), lastCenter + 64, -10, 52, 58);

            // ===== 하단 3버튼 =====
            var prevBtn = MakePill("PrevBtn", root, "←  이전으로", 30, Card, Title, font);
            PlaceBL(prevBtn.GetComponent<RectTransform>(), 60, 20, 540, 96);
            var retryBtn = MakePill("RetryBtn", root, "↻  다시 연습하기", 30, Card, Title, font);
            PlaceBL(retryBtn.GetComponent<RectTransform>(), 690, 20, 540, 96);
            var nextBtn = MakePill("NextBtn", root, "다음 시나리오  →", 30, Primary, White, font);
            PlaceBL(nextBtn.GetComponent<RectTransform>(), 1320, 20, 540, 96);

            // ===== View 와이어링 =====
            var view = canvasGo.AddComponent<RecordDetailView>();
            var so = new SerializedObject(view);
            so.FindProperty("backBtn").objectReferenceValue = backBtn.GetComponent<Button>();
            so.FindProperty("shareBtn").objectReferenceValue = shareBtn;
            so.FindProperty("donutFill").objectReferenceValue = donutFill;
            so.FindProperty("donutPercentText").objectReferenceValue = donutPct;
            so.FindProperty("fullSuccessText").objectReferenceValue = fullVal;
            so.FindProperty("partialSuccessText").objectReferenceValue = partialVal;
            so.FindProperty("needHelpText").objectReferenceValue = helpVal;
            so.FindProperty("scenarioNameText").objectReferenceValue = scenarioName;
            so.FindProperty("scenarioDetailBtn").objectReferenceValue = detailBtn;
            so.FindProperty("statusText").objectReferenceValue = statusVal;
            so.FindProperty("dateText").objectReferenceValue = dateVal;
            so.FindProperty("durationText").objectReferenceValue = durationVal;
            so.FindProperty("feedbackMainText").objectReferenceValue = feedbackMain;
            so.FindProperty("bubbleText").objectReferenceValue = bubbleText;
            WireArray(so, "goodPointTexts", goodPoints);
            WireArray(so, "improvePointTexts", improvePoints);
            WireArray(so, "nextGoalTexts", nextGoals);
            WireArray(so, "stepCards", stepCards);
            so.FindProperty("prevBtn").objectReferenceValue = prevBtn;
            so.FindProperty("retryBtn").objectReferenceValue = retryBtn;
            so.FindProperty("nextBtn").objectReferenceValue = nextBtn;
            so.ApplyModifiedProperties();

            EnsureSceneInBuildSettings(ScenePaths.RecordDetail);
            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[RecordDetailSceneBuilder] 완료");
        }

        // ===== 복합 헬퍼 =====

        static TMP_Text MakeLegendRow(RectTransform card, string label, Color dot, float x, float y, TMP_FontAsset font)
        {
            var d = MakeImage("Dot", card, RoundedPath);
            d.color = dot; d.raycastTarget = false;
            ChildTL(d.rectTransform, x, y, 22, 22);
            var l = MakeText("Label", card, label, 24, Title, font, bold: false, align: TextAlignmentOptions.Left);
            ChildTL(l.rectTransform, x + 34, y + 4, 150, 36);
            var v = MakeText("Value", card, "0회", 26, Title, font, bold: true, align: TextAlignmentOptions.Right);
            ChildTL(v.rectTransform, x + 110, y + 4, 90, 36);
            return v;
        }

        // 3컬럼 피드백. 헤더(아이콘+제목) + 텍스트 슬롯 2개. 슬롯 배열 반환.
        static TMP_Text[] MakeFeedbackColumn(RectTransform panel, string header, Color headerColor, string iconPath, float x, TMP_FontAsset font)
        {
            var icon = MakeImage("Icon", panel, iconPath);
            icon.raycastTarget = false;
            ChildTL(icon.rectTransform, x, -22, 30, 30);
            var h = MakeText("Header", panel, header, 22, headerColor, font, bold: true, align: TextAlignmentOptions.Left);
            ChildTL(h.rectTransform, x + 40, -22, 190, 34);

            var slots = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                slots[i] = MakeText("Item" + i, panel, "", 21, Title, font, bold: false, align: TextAlignmentOptions.TopLeft);
                slots[i].textWrappingMode = TextWrappingModes.Normal;
                ChildTL(slots[i].rectTransform, x, -74 - i * 54, 235, 50);
            }
            return slots;
        }

        static RecordStepCard MakeStepCard(string name, Transform parent, float x, float y, int number, Color accent, string speakerPath, TMP_FontAsset font)
        {
            var card = MakeCard(name, parent);
            PlaceTL(card, x, y, 280, 174); // 씬 손배치 기준 (Bg 304x174)

            // 윗줄: 번호 배지 + 스피커 + 목표
            var badge = ChildRect("NumBadge", card);
            ChildTL(badge, 16, -16, 52, 32);
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = Rounded(); badgeImg.type = Image.Type.Sliced; badgeImg.pixelsPerUnitMultiplier = 2f;
            badgeImg.color = Tint(accent, 0.82f); badgeImg.raycastTarget = false;
            var num = MakeText("Number", badge, number.ToString("D2"), 20, accent, font, bold: true);
            StretchFull(num.rectTransform, 2);

            var spk = MakeImage("Speaker", card, speakerPath);
            ChildTL(spk.rectTransform, 74, -15, 36, 36);
            var spkBtn = spk.gameObject.AddComponent<Button>();
            spkBtn.targetGraphic = spk;

            var obj = MakeText("Objective", card, "", 20, Title, font, bold: true, align: TextAlignmentOptions.Left);
            ChildTL(obj.rectTransform, 116, -16, 152, 32);

            // 회색 발화 박스 (씬: 252x52, 카드 가로 정중앙)
            var box = ChildRect("UserBox", card);
            ChildTL(box, 14, -64, 252, 52);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.sprite = Rounded(); boxImg.type = Image.Type.Sliced; boxImg.pixelsPerUnitMultiplier = 1.5f;
            boxImg.color = new Color32(241, 243, 247, 255); boxImg.raycastTarget = false;
            var user = MakeText("UserText", box, "", 18, Title, font, bold: false, align: TextAlignmentOptions.Left);
            user.textWrappingMode = TextWrappingModes.Normal;
            StretchFull(user.rectTransform, 12);

            // 아랫줄: 시간(좌) + 평가(중) + 메달(우)
            var time = MakeText("Time", card, "", 17, Sub, font, bold: false, align: TextAlignmentOptions.Left);
            ChildTL(time.rectTransform, 16, -132, 66, 24);
            var rating = MakeText("Rating", card, "", 18, Green, font, bold: true, align: TextAlignmentOptions.Left);
            ChildTL(rating.rectTransform, 86, -132, 120, 24);
            var medal = MakeImage("Medal", card, IconDir + "/ic_medal.png");
            medal.raycastTarget = false;
            ChildTL(medal.rectTransform, 226, -128, 40, 44);

            var view = card.gameObject.AddComponent<RecordStepCard>();
            view.numberText = num; view.objectiveText = obj; view.userText = user;
            view.timeText = time; view.ratingText = rating; view.speakerButton = spkBtn;
            return view;
        }

        static Color Tint(Color c, float t) => Color.Lerp(c, White, t);

        // ===== 기본 헬퍼 =====

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static RectTransform MakeCard(string name, Transform parent) => MakeCard(name, parent, Card);

        // 카드 = [그림자(글래스 부유감)] + [흰(또는 지정색) 둥근 배경]. 콘텐츠는 container에 추가하면 배경 위에 렌더.
        static RectTransform MakeCard(string name, Transform parent, Color bgColor)
        {
            var container = ChildRect(name, parent);

            var shadow = ChildRect("Shadow", container);
            shadow.anchorMin = Vector2.zero; shadow.anchorMax = Vector2.one;
            shadow.offsetMin = new Vector2(-12, -20); shadow.offsetMax = new Vector2(12, 4);
            var sImg = shadow.gameObject.AddComponent<Image>();
            sImg.sprite = SceneBuilderUtils.EnsureGlowSprite();
            sImg.color = new Color(0.10f, 0.16f, 0.30f, 0.13f);
            sImg.raycastTarget = false;

            var bg = ChildRect("Bg", container);
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
            var img = bg.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = bgColor;
            img.raycastTarget = false;
            return container;
        }

        // 얇은 세로 구분선
        static void MakeDivider(RectTransform parent, float x, float y, float h)
        {
            var d = ChildRect("Divider", parent);
            ChildTL(d, x, y, 2, h);
            var img = d.gameObject.AddComponent<Image>();
            img.color = Divider; img.raycastTarget = false;
        }

        static Image MakeImage(string name, Transform parent, string spritePath)
        {
            var rect = ChildRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            var s = LoadSprite(spritePath);
            if (s != null) { img.sprite = s; img.preserveAspect = true; }
            else { img.sprite = Rounded(); img.type = Image.Type.Sliced; }
            return img;
        }

        static void MakeIconIn(RectTransform card, string spritePath, float xCenterOffset, float y, float size)
        {
            var img = MakeImage("Icon", card, spritePath);
            img.raycastTarget = false;
            // 카드 가로 중앙 기준 배치
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            img.rectTransform.pivot = new Vector2(0.5f, 1f);
            img.rectTransform.anchoredPosition = new Vector2(xCenterOffset, y);
            img.rectTransform.sizeDelta = new Vector2(size, size);
        }

        static TMP_Text MakeText(string name, Transform parent, string text, int size, Color color, TMP_FontAsset font, bool bold, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var tmp = SceneBuilderUtils.CreateTMPText(name, parent, text, size);
            tmp.color = color;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        static GameObject MakeCircleButton(string name, Transform parent, string glyph, TMP_FontAsset font)
        {
            var rect = ChildRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = Card;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, glyph, 44, Title, font, bold: true);
            StretchFull(t.rectTransform, 4);
            return rect.gameObject;
        }

        static Button MakePill(string name, Transform parent, string label, int size, Color bg, Color textColor, TMP_FontAsset font)
        {
            var rect = ChildRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = bg;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, label, size, textColor, font, bold: true);
            StretchFull(t.rectTransform, 6);
            return btn;
        }

        // 부모 좌상단(0,1) 기준 배치
        static void PlaceTL(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }
        static void ChildTL(RectTransform r, float x, float y, float w, float h) => PlaceTL(r, x, y, w, h);

        static void PlaceTR(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        static void PlaceBL(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
            r.pivot = new Vector2(0f, 0f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        // 부모 중앙 가로 + 상단 기준(세로) 배치 (스탯 카드 내부용)
        static void CenterIn(RectTransform r, float xOffset, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(xOffset, y);
            r.sizeDelta = new Vector2(w, h);
        }

        static void StretchFull(RectTransform r, float pad)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(pad, pad); r.offsetMax = new Vector2(-pad, -pad);
        }

        static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        static Sprite LoadSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void WireArray(SerializedObject so, string prop, UnityEngine.Object[] items)
        {
            var p = so.FindProperty(prop);
            p.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static void EnsureSceneAsset(string path)
        {
            if (File.Exists(path)) return;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path);
        }

        static void EnsureSceneInBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == path)) return;
            EditorBuildSettings.scenes = scenes
                .Concat(new[] { new EditorBuildSettingsScene(path, true) })
                .ToArray();
            Debug.Log($"[RecordDetailSceneBuilder] Build Settings에 추가: {path}");
        }
    }

    // MakeText(...).Pipe(t => ...) 패턴용 확장 (빌더 내부 가독성)
    static class RecordBuilderExt
    {
        public static void Pipe(this TMP_Text t, System.Action<TMP_Text> act) => act(t);
    }
}
