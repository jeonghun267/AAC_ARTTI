using System.IO;
using System.Linq;
using Artti.Common;
using Artti.Report;
using Artti.Training;
using Artti.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Artti.Editor
{
    // 편의점 훈련 완료 화면 = ending 시안(1448x1086, Art/UI/Home/ending). 옛 CompletionRoot를 지우고 같은 자리에 새로 만든다.
    // - 배경(정확도 모드 1.png / 대화 모드 11.png, 런타임에 HUD가 바꾼다)이 화면을 꽉 채우고(CoverImageFitter),
    //   UI는 배경을 따라 커지는 무대(Stage, 좌표 = 시안 px) 위에 시안 자리 그대로 놓는다.
    //   화면이 시안보다 위아래로 많이 잘리면(태블릿 약간, 폰 많이) 무대만 같은 비율로 줄여 EndingSafe 영역이 다 보이게 한다.
    // - 왼쪽 위 손글씨(성장)는 배경 그림 속 자리에 붙어 있어야 해서 배경의 자식(배경 비율 앵커)이다.
    //   점원 앞치마 위 손글씨(응원)는 '다시 연습' 버튼 바로 위라 버튼과 같이 무대에 둔다(폰에서 무대가 줄어도 겹치지 않게).
    // - 시안에 없는 그림(제목, 손글씨, 별, 글자 지운 기록 판 등)은 Art_Source/ui_ending/make_derived.py가 만든다(ending/derived).
    // - 숫자·문구는 런타임에 ConvenienceHudView.ShowCompletion(TrainingResult)이 채운다. 씬에는 시안과 같은 예시 값이 들어 있다.
    public static partial class ConvenienceTrainingConLayout
    {
        const string EndDir = "Assets/_Project/Art/UI/Home/ending/";
        const string EndDer = EndDir + "derived/";
        const string ConfettiDir = "Assets/_Project/Art/UI/Training/Completion/";
        static readonly Vector2 EndingSize = new Vector2(1448f, 1086f);

        // 무대에서 꼭 보여야 하는 영역(시안 px): 위 버튼 ~ 아래 버튼, 응원 손글씨(시안의 다시 연습 왼쪽 끝) ~ 기록 판 오른쪽 끝
        static readonly Rect EndingSafe = Rect.MinMaxRect(252f, 14f, 1416f, 998f);
        static readonly Vector2 EndingFocus = new Vector2(0.5f, 0.55f);   // 폰에서 잘릴 때 위(점원 얼굴·간판)를 조금 더 남긴다

        // ===== 그림(원본 px 크기, 몸통 = 빛 번짐을 뺀 판·버튼 테두리)과 시안 자리(px) =====
        static readonly Vector2 ScoreCardPng = new Vector2(1536f, 1024f);
        static readonly Rect ScoreCardBody = Rect.MinMaxRect(65f, 175f, 1487f, 887f);
        static readonly Rect ScoreCardBox = Rect.MinMaxRect(595f, 275f, 1315f, 0f);          // 가로 기준(세로는 그림 비율)
        // 점수 판 안(그림 px): 점수 글자, 별(원래 별 자리에서 아래로 22px, 0.8배), 아래 띠. 판이 오른쪽으로 2도쯤 기울어 있다
        static readonly Rect ScoreValueBox = Rect.MinMaxRect(380f, 384f, 1160f, 572f);
        static readonly Vector2[] StarCenters =
        {
            new Vector2(503f, 635f), new Vector2(638f, 639.5f), new Vector2(771.5f, 644.5f),
            new Vector2(906f, 649.5f), new Vector2(1039f, 654f),
        };
        static readonly Vector2 StarPng = new Vector2(130f, 124f);   // star_on/off.png 한 칸(별은 칸 가운데)
        const float StarScale = 0.8f;
        static readonly Rect SummaryBox = Rect.MinMaxRect(205f, 728f, 1375f, 852f);
        const float ScoreCardTilt = -2f;

        // 기록 판 3개(몸통 가로/세로 1.25로 맞춘 파생 그림). 시안 자리: 한 줄, 간격 10
        static readonly Vector2 StatAccuracyPng = new Vector2(1394f, 1637f);
        static readonly Rect StatAccuracyBody = Rect.MinMaxRect(148f, 380f, 1247f, 1260f);
        static readonly Vector2 StatExpressionPng = new Vector2(1394f, 1563f);
        static readonly Rect StatExpressionBody = Rect.MinMaxRect(159f, 346f, 1238f, 1212f);
        static readonly Vector2 StatRecordPng = new Vector2(1394f, 1495f);
        static readonly Rect StatRecordBody = Rect.MinMaxRect(139f, 305f, 1265f, 1207f);
        const float StatY = 656f, StatW = 248f, StatGap = 10f, StatX0 = 570f;
        // 몸통 안 비율 자리(0~1, 위가 0): '최고 점수!' 줄과 아래 띠(새 기록 판 그림 기준). 세 판 모두 같은 높이에 맞춘다
        static readonly Rect StatValueRel = Rect.MinMaxRect(0.05f, 0.45f, 0.95f, 0.63f);
        static readonly Rect StatNoteRel = Rect.MinMaxRect(0.07f, 0.713f, 0.925f, 0.932f);
        static readonly Rect RecordTitleRel = Rect.MinMaxRect(0.40f, 0.18f, 0.95f, 0.30f);

        // 버튼(2172x724). 몸통 = 밝은 테두리 상자. 시안 자리는 높이 기준, 가로는 상자 가운데에
        // 시안의 다음 시나리오(7)·종료하기(9)는 쓰지 않는다(2026-10-03 사용자 요청으로 뺌).
        // 남은 다시 연습·메인으로는 크기 그대로, 간격 24로 기록 판 3개 줄 가운데(x 952) 밑에 둔다.
        static readonly Vector2 ButtonPng = new Vector2(2172f, 724f);
        static readonly Rect RetryBody = Rect.MinMaxRect(420f, 183f, 1753f, 534f);
        static readonly Rect HomeBody = Rect.MinMaxRect(417f, 162f, 1756f, 551f);
        static readonly Rect ReportBody = Rect.MinMaxRect(340f, 153f, 1832f, 576f);
        static readonly Rect RetryBox = Rect.MinMaxRect(674.5f, 915f, 950.5f, 988f);
        static readonly Rect HomeBox = Rect.MinMaxRect(974.5f, 915f, 1229.5f, 988f);
        const float TopButtonY0 = 22f, TopButtonY1 = 80f, TopButtonRight = 1407f;

        // 제목(title.png 1997x689, 글자 몸통 (90,270)-(1907,599)) -> 시안 글자 상자 가로 662~1140, 세로 가운데 187
        static readonly Vector2 TitlePng = new Vector2(1997f, 689f);
        static readonly Rect TitleBody = Rect.MinMaxRect(90f, 270f, 1907f, 599f);
        const float TitleX0 = 662f, TitleX1 = 1140f, TitleCenterY = 187f;
        static readonly Rect SubtitleBox = Rect.MinMaxRect(690f, 238f, 1080f, 284f);

        // 손글씨(3배로 그린 그림, 사방 여백 72px). 시안의 글씨 상자(잉크)에 가로를 맞춘다
        const float HandPad = 72f;
        static readonly Vector2 HandGrowthPng = new Vector2(646f, 710f);
        static readonly Rect HandGrowthInk = Rect.MinMaxRect(34f, 356f, 166f, 0f);
        static readonly Vector2 HandCheerPng = new Vector2(891f, 378f);
        static readonly Rect HandCheerInk = Rect.MinMaxRect(305f, 766f, 530f, 0f);

        // 색종이(옛 완료 화면 그림 재사용): 이름, 그림, 가운데(시안 px), 크기, 기울기
        static readonly (string name, string file, Vector2 center, float size, float angle)[] Confetti =
        {
            ("ConfettiGold1", "confetti_ribbon_yellow.png", new Vector2(632f, 93f), 40f, 18f),
            ("ConfettiViolet1", "confetti_ribbon_purple.png", new Vector2(1262f, 228f), 38f, -28f),
            ("ConfettiCyan1", "confetti_ribbon_cyan.png", new Vector2(1166f, 276f), 30f, 38f),
            ("ConfettiGold2", "confetti_ribbon_yellow.png", new Vector2(1334f, 588f), 46f, -16f),
            ("ConfettiSparkle1", "confetti_sparkle.png", new Vector2(742f, 66f), 34f, 0f),
            ("ConfettiSparkle2", "confetti_sparkle.png", new Vector2(1182f, 168f), 26f, 0f),
        };

        static readonly Color EndInk = new Color32(236, 243, 255, 255);
        static readonly Color EndNote = new Color32(222, 233, 255, 255);
        static readonly Color EndGold = new Color32(255, 216, 128, 255);

        [MenuItem("Artti/Fix/TrainingConvenience - Apply Ending Design")]
        static void ApplyEndingMenu()
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
            AssetDatabase.Refresh();
            _font = SceneBuilderUtils.GetKoreanFont();
            ApplyEnding(canvas);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SceneBuilderUtils.SaveActiveSceneAgainAfterEditorTicks();
            Debug.Log("[ConvenienceTrainingConLayout] 완료 — ending 시안 완료 화면 적용 후 씬 저장");
        }

        public static void ApplyEnding(GameObject canvasGo)
        {
            var canvas = canvasGo.transform;
            int index = canvas.childCount;
            var old = canvas.Find("CompletionRoot");
            if (old != null)
            {
                index = old.GetSiblingIndex();
                Object.DestroyImmediate(old.gameObject);
            }

            var root = NewRect("CompletionRoot", canvas);
            Stretch(root);
            root.SetSiblingIndex(Mathf.Min(index, canvas.childCount - 1));

            // 배경 + 무대
            var bg = NewRect("Background", root);
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.sprite = EndSprite(EndDir + "1.png", 2048);
            bgImage.raycastTarget = true;   // 뒤 훈련 화면이 눌리지 않게
            var fitter = bg.gameObject.AddComponent<CoverImageFitter>();
            BuildHand("HandGrowth", bg, EndDer + "hand_growth.png", HandGrowthPng, HandGrowthInk);
            var stage = NewRect("Stage", bg);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = EndingSize;
            fitter.Setup(EndingSize.x / EndingSize.y, 1.06f, EndingFocus);
            fitter.SetFollower(stage, EndingSize);
            fitter.SetSafeRect(EndingSafe, 16f);
            BuildHand("HandCheer", stage, EndDer + "hand_cheer.png", HandCheerPng, HandCheerInk);

            foreach (var c in Confetti)
            {
                var rt = StageBox(c.name, stage, new Rect(c.center.x - c.size / 2f, c.center.y - c.size / 2f, c.size, c.size));
                var image = rt.gameObject.AddComponent<Image>();
                image.sprite = EndSprite(ConfettiDir + c.file, 512);
                image.preserveAspect = true;
                image.raycastTarget = false;
                rt.localRotation = Quaternion.Euler(0f, 0f, c.angle);
            }

            // 제목 + 부제
            float ts = (TitleX1 - TitleX0) / TitleBody.width;
            var titleBodyTop = TitleCenterY - TitleBody.height * ts / 2f;
            EndImage("Title", stage, EndDer + "title.png", 1024,
                new Rect(TitleX0 - TitleBody.xMin * ts, titleBodyTop - TitleBody.yMin * ts, TitlePng.x * ts, TitlePng.y * ts));
            EndLabel("Subtitle", stage, SubtitleBox, "오늘의 훈련을 멋지게 마쳤어요", 26f, EndInk, false,
                TextAlignmentOptions.Center);

            // 점수 판
            var scoreOuter = FitWidth(ScoreCardPng, ScoreCardBody, ScoreCardBox, out float ss);
            var scoreCard = EndImage("ScoreCard", stage, EndDer + "score_card.png", 2048, scoreOuter);
            var scoreValue = EndLabel("ScoreValue", scoreCard, Scaled(ScoreValueBox, ss), "100<size=60%>점</size>", 100f,
                Color.white, true, TextAlignmentOptions.Center);
            scoreValue.enableVertexGradient = true;
            scoreValue.colorGradient = new VertexGradient(Color.white, Color.white,
                new Color32(132, 222, 255, 255), new Color32(132, 222, 255, 255));
            scoreValue.characterSpacing = -2f;
            scoreValue.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ScoreCardTilt);
            var starOn = EndSprite(EndDer + "star_on.png", 256);
            var starOff = EndSprite(EndDer + "star_off.png", 256);
            var stars = new Image[StarCenters.Length];
            for (int i = 0; i < StarCenters.Length; i++)
            {
                var size = StarPng * StarScale;
                var box = new Rect(StarCenters[i] - size / 2f, size);
                var rt = StageBox($"Star_{i + 1}", scoreCard, Scaled(box, ss));
                stars[i] = rt.gameObject.AddComponent<Image>();
                stars[i].sprite = starOn;
                stars[i].raycastTarget = false;
            }
            var summary = EndLabel("Summary", scoreCard, Scaled(SummaryBox, ss), "편의점 · 구음진단모드 · 1분 안에 마쳤어요",
                22f, EndInk, false, TextAlignmentOptions.Center);
            summary.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ScoreCardTilt);

            // 기록 판 3개
            BuildStatCard("AccuracyCard", stage, EndDer + "stat_accuracy.png", StatAccuracyPng, StatAccuracyBody, 0,
                "98%", "정확한 표현이\n인상적이에요!", out var accuracyValue, out var accuracyNote);
            BuildStatCard("ExpressionCard", stage, EndDer + "stat_expression.png", StatExpressionPng, StatExpressionBody, 1,
                "12개", "자연스러운 표현을\n잘 사용했어요!", out var expressionValue, out var expressionNote);
            var record = BuildStatCard("RecordCard", stage, EndDer + "stat_record.png", StatRecordPng, StatRecordBody, 2,
                null, "이전 기록보다\n20점 올랐어요!", out var recordHeadline, out var recordNote);
            recordHeadline.text = "최고 80점";
            recordHeadline.fontSize = 38f;
            recordHeadline.colorGradient = new VertexGradient(new Color32(255, 240, 190, 255), new Color32(255, 240, 190, 255), EndGold, EndGold);
            recordHeadline.gameObject.SetActive(false);   // 최고 기록이 아닐 때만(그림 속 '최고 점수!' 대신)
            var recordTitle = EndLabel("RecordTitle", record, RelBox(record, StatRecordBody, StatRecordPng, RecordTitleRel),
                TrainingResultLabels.LowerTitle, 20f, Color.white, true, TextAlignmentOptions.MidlineLeft);
            recordTitle.gameObject.SetActive(false);

            // 버튼
            var retry = EndButton("RetryButton", stage, EndDir + "6.png", RetryBody, RetryBox);
            var home = EndButton("HomeButton", stage, EndDir + "8.png", HomeBody, HomeBox);
            float reportW = ReportBody.width * (TopButtonY1 - TopButtonY0) / ReportBody.height;
            var reportBox = Rect.MinMaxRect(TopButtonRight - reportW, TopButtonY0, TopButtonRight, TopButtonY1);
            var report = EndButton("ReportButton", stage, EndDir + "10.png", ReportBody, reportBox);

            // HUD 연결
            var hud = canvasGo.GetComponent<ConvenienceHudView>();
            if (hud != null)
            {
                var so = new SerializedObject(hud);
                so.FindProperty("completionRoot").objectReferenceValue = root.gameObject;
                so.FindProperty("completionBackground").objectReferenceValue = bgImage;
                so.FindProperty("accuracyBackgroundSprite").objectReferenceValue = EndSprite(EndDir + "1.png", 2048);
                so.FindProperty("conversationBackgroundSprite").objectReferenceValue = EndSprite(EndDir + "11.png", 2048);
                so.FindProperty("completionScoreText").objectReferenceValue = scoreValue;
                SetObjects(so.FindProperty("completionStars"), stars);
                so.FindProperty("starOnSprite").objectReferenceValue = starOn;
                so.FindProperty("starOffSprite").objectReferenceValue = starOff;
                so.FindProperty("completionSummaryText").objectReferenceValue = summary;
                so.FindProperty("completionAccuracyText").objectReferenceValue = accuracyValue;
                so.FindProperty("completionAccuracyNote").objectReferenceValue = accuracyNote;
                so.FindProperty("completionExpressionText").objectReferenceValue = expressionValue;
                so.FindProperty("completionExpressionNote").objectReferenceValue = expressionNote;
                so.FindProperty("completionRecordCard").objectReferenceValue = record.GetComponent<Image>();
                so.FindProperty("recordBestSprite").objectReferenceValue = EndSprite(EndDer + "stat_record.png", 1024);
                so.FindProperty("recordPlainSprite").objectReferenceValue = EndSprite(EndDer + "stat_record_blank.png", 1024);
                so.FindProperty("completionRecordTitle").objectReferenceValue = recordTitle;
                so.FindProperty("completionRecordHeadline").objectReferenceValue = recordHeadline;
                so.FindProperty("completionRecordNote").objectReferenceValue = recordNote;
                so.FindProperty("retryBtn").objectReferenceValue = retry;
                so.FindProperty("hubBtn").objectReferenceValue = null;   // 다음 시나리오 버튼 없음
                so.FindProperty("homeBtn").objectReferenceValue = home;
                so.FindProperty("completionReportBtn").objectReferenceValue = report;
                so.FindProperty("completionQuitBtn").objectReferenceValue = null;   // 종료하기 버튼 없음
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("[ConvenienceTrainingConLayout] ConvenienceHudView 없음 — 완료 화면을 연결하지 못했다");

            root.gameObject.SetActive(false);
        }

        // 기록 판: 몸통 가로 StatW로 slot번째 자리에. value가 null이면 큰 글자는 비워 두고(새 기록 판) 돌려준다
        static RectTransform BuildStatCard(string name, RectTransform stage, string path, Vector2 png, Rect body, int slot,
                                           string value, string note, out TMP_Text valueText, out TMP_Text noteText)
        {
            float x0 = StatX0 + slot * (StatW + StatGap);
            var outer = FitWidth(png, body, Rect.MinMaxRect(x0, StatY, x0 + StatW, 0f), out _);
            var card = EndImage(name, stage, path, 1024, outer);
            valueText = EndLabel("Value", card, RelBox(card, body, png, StatValueRel), value ?? "", 46f, Color.white, true,
                TextAlignmentOptions.Center);
            valueText.enableVertexGradient = true;
            valueText.colorGradient = new VertexGradient(Color.white, Color.white,
                new Color32(150, 228, 255, 255), new Color32(150, 228, 255, 255));
            noteText = EndLabel("Note", card, RelBox(card, body, png, StatNoteRel), note, 15.5f, EndNote, false,
                TextAlignmentOptions.Center);
            noteText.lineSpacing = -6f;
            return card;
        }

        static void BuildHand(string name, RectTransform parent, string path, Vector2 png, Rect ink)
        {
            float s = ink.width / (png.x - 2f * HandPad);
            var outer = new Rect(ink.xMin - HandPad * s, ink.yMin - HandPad * s, png.x * s, png.y * s);
            var rt = NewRect(name, parent);
            // 부모(배경 또는 무대, 둘 다 시안 크기) 비율 앵커(시안 px -> 0~1, y는 아래가 0): 부모가 커지고 늘어나도 같은 자리
            rt.anchorMin = new Vector2(outer.xMin / EndingSize.x, 1f - outer.yMax / EndingSize.y);
            rt.anchorMax = new Vector2(outer.xMax / EndingSize.x, 1f - outer.yMin / EndingSize.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = EndSprite(path, 1024);
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        static Button EndButton(string name, RectTransform stage, string path, Rect body, Rect box)
        {
            float s = box.height / body.height;
            float x0 = box.center.x - body.width * s / 2f;
            var outer = new Rect(x0 - body.xMin * s, box.yMin - body.yMin * s, ButtonPng.x * s, ButtonPng.y * s);
            var rt = EndImage(name, stage, path, 1024, outer);
            var image = rt.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        static RectTransform EndImage(string name, Transform parent, string path, int maxSize, Rect box)
        {
            var rt = StageBox(name, parent, box);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = EndSprite(path, maxSize);
            image.raycastTarget = false;
            return rt;
        }

        // 크기 단위가 시안 px인 글자(무대가 화면에 맞춰 커지고 줄어든다)
        static TMP_Text EndLabel(string name, Transform parent, Rect box, string value, float size, Color color, bool bold,
                                 TextAlignmentOptions align)
        {
            var rt = StageBox(name, parent, box);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // 부모(시안 px 상자, 좌상단 기준) 안에 놓는다. 피벗은 가운데(등장 연출의 톡·올라옴 기준)
        static RectTransform StageBox(string name, Transform parent, Rect box)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(box.center.x, -box.center.y);
            rt.sizeDelta = box.size;
            return rt;
        }

        // 그림 몸통이 target 가로에 맞게(세로는 그림 비율) 놓일 바깥 상자. target.yMin = 몸통 위
        static Rect FitWidth(Vector2 png, Rect body, Rect target, out float s)
        {
            s = target.width / body.width;
            return new Rect(target.xMin - body.xMin * s, target.yMin - body.yMin * s, png.x * s, png.y * s);
        }

        static Rect Scaled(Rect r, float s) => new Rect(r.x * s, r.y * s, r.width * s, r.height * s);

        // 판 그림 몸통 안 비율 자리(0~1) -> 판(바깥 상자) 안 px 상자
        static Rect RelBox(RectTransform card, Rect body, Vector2 png, Rect rel)
        {
            float s = card.sizeDelta.x / png.x;
            return Rect.MinMaxRect((body.xMin + rel.xMin * body.width) * s, (body.yMin + rel.yMin * body.height) * s,
                (body.xMin + rel.xMax * body.width) * s, (body.yMin + rel.yMax * body.height) * s);
        }

        // 스프라이트 가져오기 설정(Sprite/Single, 밉맵 없음, 압축 없음). 화면에 작게 보이는 큰 그림은 maxSize로 줄여 메모리를 아낀다
        static Sprite EndSprite(string path, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer != null && (importer.textureType != TextureImporterType.Sprite
                                     || importer.spriteImportMode != SpriteImportMode.Single
                                     || importer.mipmapEnabled
                                     || importer.textureCompression != TextureImporterCompression.Uncompressed
                                     || importer.maxTextureSize != maxSize))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[ConvenienceTrainingConLayout] Sprite 없음: {path}");
            return sprite;
        }

        // ===== 확인용 캡처 (씬은 저장하지 않는다) =====
        //   정확도 모드 첫 기록(그림 속 '최고 점수!') / 대화 모드 최고 기록 아님(글자 지운 판 + TMP) / 기록 없음
        [MenuItem("Artti/Debug/Capture TrainingConvenience Ending - Tab + Phone + 4x3")]
        static void CaptureEndingMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("[ConvenienceTrainingConLayout] 저장 안 한 씬 변경이 있어 캡처하지 않는다.");
                return;
            }
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);

            var first = new TrainingResult
            {
                mode = TrainingMode.Accuracy, scenarioName = "편의점", durationSeconds = 150, hasRecord = true,
                score = 100, stars = 5, accuracyPct = 98, goodExpressions = 12, record = RecordState.First,
            };
            var lower = new TrainingResult
            {
                mode = TrainingMode.Conversation, scenarioName = "편의점", durationSeconds = 45, hasRecord = true,
                score = 70, stars = 4, accuracyPct = 74, goodExpressions = 6, record = RecordState.Lower, previousBest = 90,
            };
            var none = new TrainingResult { mode = TrainingMode.Accuracy, scenarioName = "편의점", durationSeconds = 200 };
            var shots = new (int w, int h, string tag, TrainingResult result)[]
            {
                (2560, 1600, "tab", first), (2560, 1600, "tab_conversation_lower", lower), (2560, 1600, "tab_norecord", none),
                (2400, 1080, "phone", first), (1920, 1440, "43", first),
            };
            var apply = typeof(ConvenienceHudView).GetMethod("ApplyCompletion",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            try
            {
                foreach (var shot in shots)
                {
                    var scene = EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
                    var canvas = scene.GetRootGameObjects().First(g => g.name == "[Canvas]");
                    var hud = canvas.GetComponent<ConvenienceHudView>();
                    var root = canvas.transform.Find("CompletionRoot");
                    if (hud == null || root == null) return;
                    root.gameObject.SetActive(true);
                    apply?.Invoke(hud, new object[] { shot.result });
                    string file = Path.Combine(outDir, $"{shot.tag}_TrainingEnding.png");
                    // 캔버스가 캡처 해상도로 바뀐 뒤 배경 맞춤(CoverImageFitter)이 다시 계산되도록 두 번 찍는다
                    DeviceLayoutCapture.CaptureActiveScene(shot.w, shot.h, file);
                    root.GetComponentInChildren<CoverImageFitter>()?.SendMessage("UpdateRect", SendMessageOptions.DontRequireReceiver);
                    DeviceLayoutCapture.CaptureActiveScene(shot.w, shot.h, file);
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(ScenePaths.TrainingConvenience, OpenSceneMode.Single);
            }
            Debug.Log($"[ConvenienceTrainingConLayout] 완료 화면 캡처 완료 -> {outDir}");
        }
    }
}
