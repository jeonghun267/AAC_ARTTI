using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 연습 리포트 대시보드. 레퍼런스 1920x1080 + Expand. (시안: Desktop/report 완성본)
    // 좌표는 4:3 Game 뷰(캔버스 1920x1440, 세로 ±720)에서 손배치한 값을 이식한 것.
    // 카드/버튼은 베이크된 PNG(Art/UI/Report/New)이고, 값 슬롯(pill/박스)은 이미지 안 정규화 좌표(0..1)로 텍스트를 얹는다.
    // 구성: 캐릭터 카드 | 스탯 3장 + 최근 연습 기록 | AI 피드백 + 이번 주 목표 + 추천 다음 연습 + CTA.
    // '전체 보기'는 같은 씬의 오버레이(AllSessionsPanel), 기록 상세는 RecordDetailScene.
    public static class ReportSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color32 Ink        = new Color32(46, 42, 110, 255);    // 진한 남보라 (제목/값)
        static readonly Color32 SubInk     = new Color32(110, 105, 150, 255);  // 보조 텍스트
        static readonly Color32 Accent     = new Color32(91, 63, 214, 255);    // 타이틀 보라
        static readonly Color32 LinkBlue   = new Color32(70, 90, 200, 255);    // 전체 보기
        static readonly Color32 Green      = new Color32(40, 150, 90, 255);    // 완료/증가 칩
        static readonly Color32 XpTrack    = new Color32(218, 217, 252, 255);  // 캐릭터 카드 XP 트랙 (이미지 색 샘플)
        static readonly Color32 XpFill     = new Color32(168, 118, 246, 255);
        static readonly Color32 GoalTrack  = new Color32(209, 208, 252, 255);  // 주간 목표 트랙 (이미지 색 샘플)
        static readonly Color32 GoalFill   = new Color32(170, 125, 251, 255);
        static readonly Color32 XpPatch    = new Color32(217, 216, 252, 255);  // "/ XP" 베이크 텍스트 덮개
        static readonly Color32 CreamTile  = new Color32(255, 246, 228, 255);  // 추천 카드 아이콘 타일
        static readonly Color32 TitleColor = new Color32(33, 41, 60, 255);
        static readonly Color32 SubColor   = new Color32(110, 118, 135, 255);
        static readonly Color   White      = Color.white;

        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png"; // 96px, border 44 -> ppuMult = 88/h 면 완전 pill
        const string NewDir      = "Assets/_Project/Art/UI/Report/New/";
        const string OODir       = "Assets/_Project/Art/UI/Report/OO/";

        const string PrefabDir             = "Assets/_Project/Prefabs/Report";
        const string SessionCardPrefabPath = PrefabDir + "/SessionCard.prefab";

        [MenuItem("Artti/Build ReportScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            EnsureSceneAsset(ScenePaths.Report);
            SceneBuilderUtils.OpenScene(ScenePaths.Report);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            // Expand: 절대 좌표 손배치라 20:9 기기에서 세로가 잘리지 않게 (RecordDetailScene과 동일)
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution, CanvasScaler.ScreenMatchMode.Expand);
            var font = SceneBuilderUtils.GetKoreanFont();
            var sessionCardPrefab = EnsureSessionCardPrefab(font);

            // ===== 배경 (사진, 비율 유지 cover) =====
            var bgPanel = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            var bgImg = bgPanel.AddComponent<Image>();
            bgImg.sprite = LoadPhotoSprite(NewDir + "bg_report.png");
            bgImg.color = White; bgImg.raycastTarget = false;
            var bgFit = bgPanel.AddComponent<AspectRatioFitter>();
            bgFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            bgFit.aspectRatio = bgImg.sprite != null ? bgImg.sprite.rect.width / bgImg.sprite.rect.height : 16f / 9f;

            // ================= 대시보드 (ListPanel) =================
            var listPanel = SceneBuilderUtils.CreatePanel("ListPanel", canvasGo.transform);
            var root = listPanel.transform;

            // ----- 상단 바: 뒤로 / 공유하기 / 이미지 저장 (글래스 pill) -----
            var backBtn  = MakeGlassPill("BackButton", root, "←  뒤로", new Vector2(-805, 640), new Vector2(165, 64), 26, font);
            var shareBtn = MakeGlassPill("ShareButton", root, "공유하기", new Vector2(622, 649), new Vector2(176, 64), 24, font);
            var saveBtn  = MakeGlassPill("SaveImageButton", root, "이미지 저장", new Vector2(837, 649), new Vector2(209, 64), 24, font);

            // ----- 타이틀 -----
            var title = MakeText("Title", root, "연습 리포트", 92, Accent, font, bold: true);
            PlaceCenter(title.rectTransform, new Vector2(-208, 540), new Vector2(700, 120));
            title.rectTransform.localScale = Vector3.one * 1.1f;
            var subtitle = MakeText("Subtitle", root, "오늘도 수고했어요!", 33, new Color32(75, 63, 143, 255), font, bold: true);
            PlaceCenter(subtitle.rectTransform, new Vector2(-208, 467), new Vector2(700, 48));
            subtitle.rectTransform.localScale = Vector3.one * 1.1f;
            var subtitle2 = MakeText("Subtitle2", root, "꾸준히 연습하면 더 멋진 내가 될 거예요.", 21, SubColor, font, bold: false);
            PlaceCenter(subtitle2.rectTransform, new Vector2(-208, 430), new Vector2(700, 30));
            subtitle2.rectTransform.localScale = Vector3.one * 1.1f;

            // ----- 좌열: 캐릭터 카드 -----
            var ch = PlaceImage(root, "CharacterCard", NewDir + "card_character.png", new Vector2(-720, -80), 430f);
            var charName = MakeText("CharName", ch, "이름", 32, Ink, font, bold: true);
            AutoSize(charName, 16, 32);
            Slot(charName.rectTransform, ch, 0.52f, 0.50f, 0.50f, 0.06f);
            var levelNum = MakeText("LevelNum", ch, "1", 26, Ink, font, bold: true);
            Slot(levelNum.rectTransform, ch, 0.51f, 0.70f, 0.36f, 0.04f);
            var xpFill = AddProgressBar(ch, "XpBar", 0.51f, 0.75f, 0.78f, 0.04f, XpTrack, XpFill);
            // 베이크된 "/ XP" 텍스트를 카드 색 패치로 덮고 "n / m XP"를 얹는다
            var xpPatch = ChildRect("XpPatch", ch);
            Slot(xpPatch, ch, 0.54f, 0.80f, 0.30f, 0.05f);
            var xpPatchImg = xpPatch.gameObject.AddComponent<Image>();
            xpPatchImg.sprite = Rounded(); xpPatchImg.type = Image.Type.Sliced; xpPatchImg.pixelsPerUnitMultiplier = 6f;
            xpPatchImg.color = XpPatch; xpPatchImg.raycastTarget = false;
            var xpText = MakeText("XpText", ch, "0 / 300 XP", 22, SubInk, font, bold: false);
            AutoSize(xpText, 12, 22);
            Slot(xpText.rectTransform, ch, 0.54f, 0.80f, 0.36f, 0.05f);
            var quote = MakeText("Quote", ch, "꾸준한 연습이\n멋진 변화를 만들어요!", 22, SubInk, font, bold: false);
            quote.textWrappingMode = TextWrappingModes.Normal;
            AutoSize(quote, 12, 22);
            Slot(quote.rectTransform, ch, 0.52f, 0.905f, 0.56f, 0.08f);

            // ----- 중열 상단: 스탯 3장 (라벨 / 값 / 변화 칩) -----
            var st1 = PlaceImage(root, "StatScenario", NewDir + "stat_scenario.png", new Vector2(-347, 247), 252f, 0.93f);
            AddStatTexts(st1, "연습한 시나리오", "0회", "오늘 0회", font, out var statCompleted, out var statCompletedDelta);
            var st2 = PlaceImage(root, "StatTime", NewDir + "stat_time.png", new Vector2(-91, 247), 252f, 0.93f);
            AddStatTexts(st2, "총 연습 시간", "0분", "오늘 0분", font, out var statStudy, out var statStudyDelta);
            var st3 = PlaceImage(root, "StatAccuracy", NewDir + "stat_accuracy.png", new Vector2(165, 247), 252f, 0.93f);
            AddStatTexts(st3, "평균 정답률", "--", "기록 없음", font, out var statAccuracy, out var statAccuracyDelta);

            // ----- 중열 하단: 최근 연습 기록 -----
            var rc = PlaceImage(root, "RecordsCard", NewDir + "card_records.png", new Vector2(-88, -182), 620f, 1.23f);
            var moreLabel = MakeText("MoreLabel", rc, "전체 보기", 18, LinkBlue, font, bold: true);
            Slot(moreLabel.rectTransform, rc, 0.825f, 0.135f, 0.15f, 0.07f);
            var moreBtn = MakeHitButton("MoreHit", rc, Vector2.zero, Vector2.zero);
            Slot(moreBtn.GetComponent<RectTransform>(), rc, 0.86f, 0.135f, 0.24f, 0.12f); // pill + chevron
            var recordRows = new[]
            {
                AddRecordRow(rc, 0.33f, font),
                AddRecordRow(rc, 0.575f, font),
                AddRecordRow(rc, 0.815f, font),
            };

            // ----- 우열: 고양이 응원 / AI 피드백 / 이번 주 목표 / 추천 다음 연습 -----
            var ai = PlaceImage(root, "AiFeedbackCard", NewDir + "card_ai_feedback.png", new Vector2(607, 248), 470f, 1.14f);
            var aiText = MakeText("FeedbackText", ai, "대화가 점점 자연스러워지고 있어요!\n끝까지 도전한 점이 아주 좋았어요.\n조금 더 다양한 표현을 사용하면\n더 멋진 대화를 할 수 있어요.", 20, Ink, font, bold: false);
            aiText.alignment = TextAlignmentOptions.Left;
            aiText.textWrappingMode = TextWrappingModes.Normal;
            AutoSize(aiText, 11, 20);
            Slot(aiText.rectTransform, ai, 0.52f, 0.64f, 0.80f, 0.42f);

            var wg = PlaceImage(root, "WeeklyGoalCard", NewDir + "card_weekly_goal.png", new Vector2(610, -8), 470f, 1.12f);
            var wgTitle = MakeText("GoalTitle", wg, "이번 주 목표", 22, Ink, font, bold: true);
            wgTitle.alignment = TextAlignmentOptions.Left;
            Slot(wgTitle.rectTransform, wg, 0.44f, 0.20f, 0.40f, 0.15f);
            var goalFill = AddProgressBar(wg, "GoalBar", 0.50f, 0.56f, 0.52f, 0.11f, GoalTrack, GoalFill);
            var goalText = MakeText("GoalValue", wg, "0 / 5", 22, Ink, font, bold: true);
            Slot(goalText.rectTransform, wg, 0.845f, 0.545f, 0.13f, 0.26f);
            var goalHint = MakeText("GoalHint", wg, "이번 주 세 번 연습하기", 16, new Color32(90, 80, 160, 255), font, bold: false);
            AutoSize(goalHint, 10, 16);
            Slot(goalHint.rectTransform, wg, 0.575f, 0.765f, 0.66f, 0.12f);

            var rm = PlaceImage(root, "RecommendCard", NewDir + "card_recommend.png", new Vector2(575, -250), 470f);
            var rmTitle = MakeText("RecommendTitle", rm, "추천 다음 연습", 22, Ink, font, bold: true);
            rmTitle.alignment = TextAlignmentOptions.Left;
            Slot(rmTitle.rectTransform, rm, 0.455f, 0.215f, 0.47f, 0.16f);
            var rmName = MakeText("RecommendName", rm, "음식점 주문하기", 22, Ink, font, bold: true);
            rmName.alignment = TextAlignmentOptions.Left;
            AutoSize(rmName, 12, 22);
            Slot(rmName.rectTransform, rm, 0.595f, 0.52f, 0.51f, 0.19f);
            var rmDesc = MakeText("RecommendDesc", rm, "실생활에 바로 쓰는 표현을 연습해보세요!", 15, SubColor, font, bold: false);
            rmDesc.alignment = TextAlignmentOptions.Left;
            AutoSize(rmDesc, 9, 15);
            Slot(rmDesc.rectTransform, rm, 0.595f, 0.74f, 0.51f, 0.18f);
            // 음식점 외 추천 시 베이크된 음식 이미지를 덮는 타일 + 시나리오 아이콘 (ReportView가 토글)
            var rmTile = ChildRect("RecommendIconTile", rm);
            Slot(rmTile, rm, 0.185f, 0.615f, 0.21f, 0.43f);
            var rmTileImg = rmTile.gameObject.AddComponent<Image>();
            rmTileImg.sprite = Rounded(); rmTileImg.type = Image.Type.Sliced; rmTileImg.pixelsPerUnitMultiplier = 2f;
            rmTileImg.color = CreamTile; rmTileImg.raycastTarget = false;
            var rmIconRect = ChildRect("Icon", rmTile);
            StretchFull(rmIconRect, 14);
            var rmIcon = rmIconRect.gameObject.AddComponent<Image>();
            rmIcon.preserveAspect = true; rmIcon.raycastTarget = false;
            rmTile.gameObject.SetActive(false);
            var recommendBtn = MakeHitButton("RecommendHit", rm, Vector2.zero, rm.sizeDelta);

            // 고양이 (AI 카드 위에 살짝 걸침 -> 카드보다 뒤에 만들어 앞에 그려지게 순서 조정)
            var cat = PlaceImage(root, "CatCheer", NewDir + "cat_cheer.png", new Vector2(526, 443), 560f);
            cat.SetAsLastSibling();

            // ----- CTA: 다른 시나리오 연습하기 -----
            var cta = PlaceImage(root, "NextScenarioButton", NewDir + "btn_next_scenario.png", new Vector2(549, -615), 570f);
            var ctaImg = cta.GetComponent<Image>(); ctaImg.raycastTarget = true;
            var nextBtn = cta.gameObject.AddComponent<Button>(); nextBtn.targetGraphic = ctaImg;

            // ================= 전체 학습 기록 (전체 보기 오버레이) =================
            var allSessionsPanel = SceneBuilderUtils.CreatePanel("AllSessionsPanel", canvasGo.transform);
            var allDim = allSessionsPanel.AddComponent<Image>();
            allDim.color = new Color(0.96f, 0.96f, 1f, 0.92f); // 사진 배경 위 가독성

            var allTitle = MakeText("Title", allSessionsPanel.transform, "전체 학습 기록", 56, TitleColor, font, bold: true);
            PlaceTop(allTitle.rectTransform, new Vector2(0, -48), new Vector2(900, 80));
            var allSubtitle = MakeText("Subtitle", allSessionsPanel.transform, "지금까지의 학습 여정을 한눈에 확인해 보세요.", 28, TitleColor, font, bold: true);
            PlaceTop(allSubtitle.rectTransform, new Vector2(0, -124), new Vector2(1000, 44));
            var allSessionsEmpty = MakeText("EmptyState", allSessionsPanel.transform, "아직 학습 기록이 없어요.", 36, SubColor, font, bold: false);
            PlaceCenter(allSessionsEmpty.rectTransform, Vector2.zero, new Vector2(900, 60));

            // 오버레이용 뒤로가기 (대시보드 BackButton은 ListPanel 안이라 가려짐)
            var allBack = MakeGlassPill("BackButton", allSessionsPanel.transform, "←  뒤로", new Vector2(-805, 640), new Vector2(165, 64), 26, font);

            var scrollRect = ChildRect("ScrollView", allSessionsPanel.transform);
            PlaceCenter(scrollRect, new Vector2(0, -64), new Vector2(1040, 760));
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var viewport = ChildRect("Viewport", scrollRect);
            StretchFull(viewport, 0);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var allSessionsContent = ChildRect("Content", viewport);
            allSessionsContent.anchorMin = new Vector2(0f, 1f);
            allSessionsContent.anchorMax = new Vector2(1f, 1f);
            allSessionsContent.pivot = new Vector2(0.5f, 1f);
            allSessionsContent.anchoredPosition = Vector2.zero;
            allSessionsContent.sizeDelta = Vector2.zero;
            SceneBuilderUtils.AddVerticalLayout(allSessionsContent.gameObject, spacing: 20,
                padding: new RectOffset(20, 20, 20, 20), alignment: TextAnchor.UpperCenter,
                expandWidth: true, expandHeight: false);
            var fitter = allSessionsContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = allSessionsContent;
            allSessionsPanel.SetActive(false);

            // ===== View 와이어링 =====
            var view = canvasGo.AddComponent<ReportView>();
            var so = new SerializedObject(view);
            so.FindProperty("backBtn").objectReferenceValue = backBtn;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("shareBtn").objectReferenceValue = shareBtn;
            so.FindProperty("saveImageBtn").objectReferenceValue = saveBtn;
            so.FindProperty("listPanel").objectReferenceValue = listPanel;
            so.FindProperty("nextScenarioBtn").objectReferenceValue = nextBtn;

            so.FindProperty("charNameText").objectReferenceValue = charName;
            so.FindProperty("levelNumText").objectReferenceValue = levelNum;
            so.FindProperty("xpFill").objectReferenceValue = xpFill;
            so.FindProperty("xpText").objectReferenceValue = xpText;
            so.FindProperty("quoteText").objectReferenceValue = quote;

            so.FindProperty("statCompletedText").objectReferenceValue = statCompleted;
            so.FindProperty("statCompletedDelta").objectReferenceValue = statCompletedDelta;
            so.FindProperty("statStudyTimeText").objectReferenceValue = statStudy;
            so.FindProperty("statStudyTimeDelta").objectReferenceValue = statStudyDelta;
            so.FindProperty("statAccuracyText").objectReferenceValue = statAccuracy;
            so.FindProperty("statAccuracyDelta").objectReferenceValue = statAccuracyDelta;

            so.FindProperty("aiFeedbackText").objectReferenceValue = aiText;

            WireArray(so, "recordRows", recordRows);
            so.FindProperty("moreBtn").objectReferenceValue = moreBtn;
            so.FindProperty("convenienceIcon").objectReferenceValue = LoadPhotoSprite(OODir + "ic_convenience.png");
            so.FindProperty("pharmacyIcon").objectReferenceValue = LoadPhotoSprite(OODir + "ic_pharmacy.png");
            so.FindProperty("restaurantIcon").objectReferenceValue = LoadPhotoSprite(OODir + "ic_restaurant.png");

            so.FindProperty("allSessionsPanel").objectReferenceValue = allSessionsPanel;
            so.FindProperty("allSessionsContainer").objectReferenceValue = allSessionsContent;
            so.FindProperty("allSessionsEmpty").objectReferenceValue = allSessionsEmpty.gameObject;
            so.FindProperty("sessionCardPrefab").objectReferenceValue = sessionCardPrefab;
            so.FindProperty("allSessionsBackBtn").objectReferenceValue = allBack;

            so.FindProperty("weeklyGoalFill").objectReferenceValue = goalFill;
            so.FindProperty("weeklyGoalText").objectReferenceValue = goalText;
            so.FindProperty("weeklyMissionText").objectReferenceValue = goalHint;

            so.FindProperty("recommendTitleText").objectReferenceValue = rmName;
            so.FindProperty("recommendDescText").objectReferenceValue = rmDesc;
            so.FindProperty("recommendBtn").objectReferenceValue = recommendBtn;
            so.FindProperty("recommendIconTile").objectReferenceValue = rmTile.gameObject;
            so.FindProperty("recommendIcon").objectReferenceValue = rmIcon;
            so.ApplyModifiedProperties();

            EnsureSceneInBuildSettings(ScenePaths.Report);
            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ReportSceneBuilder] 완료");
        }

        // ===== 대시보드 헬퍼 =====

        // 베이크 PNG 카드를 폭 기준(비율 유지)으로 중앙 앵커 배치. 자식 슬롯은 Slot()으로 얹는다.
        // scale: 손배치에서 localScale로 키운 값 그대로 (슬롯 폰트까지 같이 커지도록 size 대신 scale 유지).
        static RectTransform PlaceImage(Transform parent, string name, string path, Vector2 pos, float width, float scale = 1f)
        {
            var sprite = LoadPhotoSprite(path);
            float h = sprite != null ? width * sprite.rect.height / sprite.rect.width : width;
            var r = ChildRect(name, parent);
            PlaceCenter(r, pos, new Vector2(width, h));
            if (!Mathf.Approximately(scale, 1f)) r.localScale = Vector3.one * scale;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = White; img.raycastTarget = false;
            return r;
        }

        // 카드 이미지 안 정규화 좌표(fx,fy: 0..1, 좌상단 원점)로 슬롯 배치. fw/fh는 카드 폭/높이 대비 비율.
        static void Slot(RectTransform rect, RectTransform card, float fx, float fy, float fw, float fh)
        {
            var size = card.sizeDelta;
            PlaceCenter(rect, new Vector2((fx - 0.5f) * size.x, (0.5f - fy) * size.y), new Vector2(fw * size.x, fh * size.y));
        }

        // 스탯 카드: 아이콘 아래 라벨 / 큰 값 / 하단 흰 pill 안 변화 칩
        static void AddStatTexts(RectTransform card, string label, string value, string delta, TMP_FontAsset font,
            out TMP_Text valueText, out TMP_Text deltaText)
        {
            var lb = MakeText("Label", card, label, 22, SubInk, font, bold: false);
            Slot(lb.rectTransform, card, 0.50f, 0.48f, 0.84f, 0.10f);
            valueText = MakeText("Value", card, value, 44, Ink, font, bold: true);
            Slot(valueText.rectTransform, card, 0.50f, 0.615f, 0.84f, 0.14f);
            deltaText = MakeText("Delta", card, delta, 20, Green, font, bold: true);
            AutoSize(deltaText, 11, 20);
            Slot(deltaText.rectTransform, card, 0.46f, 0.79f, 0.54f, 0.14f);
        }

        // 트랙(둥근 pill) + Filled 가로 fill. 베이크된 바 위를 정확히 덮는다.
        static Image AddProgressBar(RectTransform card, string name, float fx, float fy, float fw, float fh, Color track, Color fill)
        {
            var bar = ChildRect(name, card);
            Slot(bar, card, fx, fy, fw, fh);
            var trackImg = bar.gameObject.AddComponent<Image>();
            trackImg.sprite = Rounded(); trackImg.type = Image.Type.Sliced; trackImg.pixelsPerUnitMultiplier = PillMult(bar.sizeDelta.y);
            trackImg.color = track; trackImg.raycastTarget = false;
            var f = ChildRect("Fill", bar);
            StretchFull(f, 0);
            var fillImg = f.gameObject.AddComponent<Image>();
            fillImg.sprite = Rounded(); fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal; fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0f; fillImg.color = fill; fillImg.raycastTarget = false;
            return fillImg;
        }

        // 최근 연습 기록 한 행: 아이콘 박스 / 이름 + 코멘트 / 상태 칩 / 정답률 / 날짜. 행 전체가 버튼.
        static ReportRecordRow AddRecordRow(RectTransform card, float fy, TMP_FontAsset font)
        {
            var row = ChildRect("RecordRow", card);
            Slot(row, card, 0.50f, fy, 0.90f, 0.22f);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f); // 투명, 레이캐스트만
            var btn = row.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;

            // 행 내부는 카드 기준 fx를 행 기준으로 환산: 행은 카드 x 0.05..0.95, y fy±0.11
            float RX(float cx) => (cx - 0.05f) / 0.90f;
            float RY(float cy) => (cy - (fy - 0.11f)) / 0.22f;

            var iconRect = ChildRect("Icon", row);
            Slot(iconRect, row, RX(0.14f), 0.5f, 0.09f / 0.90f, 0.12f / 0.22f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false;

            var nm = MakeText("RecName", row, "", 22, Ink, font, bold: true);
            nm.alignment = TextAlignmentOptions.Left;
            AutoSize(nm, 12, 22);
            Slot(nm.rectTransform, row, RX(0.37f), RY(fy - 0.035f), 0.30f / 0.90f, 0.06f / 0.22f);
            var sub = MakeText("RecSub", row, "", 16, SubColor, font, bold: false);
            sub.alignment = TextAlignmentOptions.Left;
            AutoSize(sub, 9, 16);
            Slot(sub.rectTransform, row, RX(0.37f), RY(fy + 0.035f), 0.30f / 0.90f, 0.06f / 0.22f);
            var status = MakeText("RecStatus", row, "완료", 18, Green, font, bold: true);
            Slot(status.rectTransform, row, RX(0.63f), 0.5f, 0.12f / 0.90f, 0.075f / 0.22f);
            var pct = MakeText("RecPct", row, "", 22, Ink, font, bold: true);
            Slot(pct.rectTransform, row, RX(0.745f), 0.5f, 0.08f / 0.90f, 0.06f / 0.22f);
            var dt = MakeText("RecDate", row, "", 16, SubColor, font, bold: false);
            Slot(dt.rectTransform, row, RX(0.85f), 0.5f, 0.10f / 0.90f, 0.06f / 0.22f);

            var view = row.gameObject.AddComponent<ReportRecordRow>();
            view.iconImage = icon; view.nameText = nm; view.subText = sub; view.statusText = status;
            view.accuracyText = pct; view.dateText = dt; view.pointsText = null; view.button = btn;
            return view;
        }

        // 반투명 흰 글래스 pill 버튼 (상단 바)
        static Button MakeGlassPill(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize, TMP_FontAsset font)
        {
            var shadow = ChildRect(name + "Shadow", parent);
            PlaceCenter(shadow, pos + new Vector2(0f, -6f), size + new Vector2(24f, 24f));
            var sImg = shadow.gameObject.AddComponent<Image>();
            sImg.sprite = SceneBuilderUtils.EnsureGlowSprite();
            sImg.color = new Color(0.25f, 0.2f, 0.5f, 0.16f); sImg.raycastTarget = false;

            var rect = ChildRect(name, parent);
            PlaceCenter(rect, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = PillMult(size.y);
            img.color = new Color(1f, 1f, 1f, 0.82f);
            var btn = rect.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var t = MakeText("Text", rect, label, fontSize, Ink, font, bold: true);
            StretchFull(t.rectTransform, 4);
            return btn;
        }

        // RoundedRect(96px, border 44)를 높이 h인 완전 pill로 만드는 ppu 배수
        static float PillMult(float h) => h > 1f ? 88f / h : 1f;

        // ===== 세션 카드 프리팹 (전체 보기 오버레이) =====

        static GameObject EnsureSessionCardPrefab(TMP_FontAsset font)
        {
            DeleteIfExists(SessionCardPrefabPath);
            EnsureFolder(PrefabDir);

            var root = new GameObject("SessionCard");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(880, 150);
            var bodyImg = root.AddComponent<Image>();
            bodyImg.sprite = Rounded(); bodyImg.type = Image.Type.Sliced; bodyImg.pixelsPerUnitMultiplier = 1f;
            bodyImg.color = White;
            var btn = root.AddComponent<Button>();
            btn.targetGraphic = bodyImg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.97f, 1f, 1f);
            colors.pressedColor = new Color(0.90f, 0.94f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.1f;
            btn.colors = colors;
            SceneBuilderUtils.AddLayoutElement(root, preferredHeight: 150);

            var badge = ChildRect("StatusBadge", root.transform);
            AnchorLeft(badge, new Vector2(32, 0), new Vector2(108, 46));
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = Rounded(); badgeImg.type = Image.Type.Sliced; badgeImg.pixelsPerUnitMultiplier = 2f;
            badgeImg.raycastTarget = false;
            var badgeText = MakeText("Text", badge, "완료", 24, TitleColor, font, bold: true);
            StretchFull(badgeText.rectTransform, 2);

            var iconBox = ChildRect("IconBox", root.transform);
            AnchorLeft(iconBox, new Vector2(156, 0), new Vector2(84, 84));
            var iconBoxImg = iconBox.gameObject.AddComponent<Image>();
            iconBoxImg.sprite = Rounded(); iconBoxImg.type = Image.Type.Sliced; iconBoxImg.pixelsPerUnitMultiplier = 2f;
            iconBoxImg.color = new Color32(232, 238, 250, 255); iconBoxImg.raycastTarget = false;
            var icon = ChildRect("Icon", iconBox);
            StretchFull(icon, 14);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true; iconImg.raycastTarget = false;

            var nameText = MakeText("Name", root.transform, "약국", 40, TitleColor, font, bold: true);
            nameText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(nameText.rectTransform, new Vector2(268, 22), new Vector2(440, 54));
            var dateText = MakeText("Date", root.transform, "", 26, SubColor, font, bold: false);
            dateText.alignment = TextAlignmentOptions.Left;
            AnchorLeft(dateText.rectTransform, new Vector2(270, -30), new Vector2(500, 40));

            var chevron = MakeText("Chevron", root.transform, ">", 44, SubColor, font, bold: true);
            chevron.rectTransform.anchorMin = chevron.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            chevron.rectTransform.pivot = new Vector2(1f, 0.5f);
            chevron.rectTransform.anchoredPosition = new Vector2(-36, 0);
            chevron.rectTransform.sizeDelta = new Vector2(50, 60);

            var view = root.AddComponent<SessionCardView>();
            view.statusBg = badgeImg;
            view.statusText = badgeText;
            view.nameText = nameText;
            view.dateText = dateText;
            view.iconImage = iconImg;
            view.selectButton = btn;

            return SaveAsPrefab(root, SessionCardPrefabPath);
        }

        // ===== 공통 헬퍼 =====

        static void EnsureSceneAsset(string path)
        {
            if (File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }

        static void EnsureSceneInBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == path)) return;
            EditorBuildSettings.scenes = scenes
                .Concat(new[] { new EditorBuildSettingsScene(path, true) })
                .ToArray();
            Debug.Log($"[ReportSceneBuilder] Build Settings에 추가: {path}");
        }

        // 투명 클릭 영역 버튼 (표시 그래픽 없음, 레이캐스트만)
        static Button MakeHitButton(string name, RectTransform parent, Vector2 pos, Vector2 size)
        {
            var rect = ChildRect(name, parent);
            PlaceCenter(rect, pos, size);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
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

        // 가변 길이 텍스트(LLM/닉네임)가 슬롯을 넘지 않게 자동 축소
        static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        static GameObject SaveAsPrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void WireArray(SerializedObject so, string prop, UnityEngine.Object[] items)
        {
            var p = so.FindProperty(prop);
            p.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void PlaceCenter(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void PlaceTop(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void AnchorLeft(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void StretchFull(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : Builtin("UI/Skin/UISprite.psd");
        }

        // PNG를 Sprite로 로드 (미임포트/타입 불일치면 Sprite Single로 보정). 카드 이미지는 압축 없이 원본 해상도 유지.
        static Sprite LoadPhotoSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool changed = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; changed = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (path.StartsWith(NewDir))
                {
                    if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; changed = true; }
                    if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
                    if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                }
                if (changed) ti.SaveAndReimport();
            }
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogWarning($"[ReportSceneBuilder] 스프라이트 없음: {path}");
            return s;
        }
    }
}
