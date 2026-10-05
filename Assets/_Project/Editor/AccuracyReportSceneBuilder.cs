using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.Report;
using Artti.UI;
using static Artti.Editor.ModeReportSceneKit;
using static Artti.Editor.ProfileAvatarStepBuilder;

namespace Artti.Editor
{
    // 정확도 모드 리포트(정확도 시안 1672x941, 조각은 대화 모드 리포트의 REPORT를 함께 쓴다). 새 씬이고 손배치가 없어 이 빌더가 원본이다.
    // 공통 틀은 ModeReportSceneKit, 이 빌더는 어려웠던 발음 목록 판과 모범 발음 듣기 판을 더한다.
    // - 값은 실행하면 AccuracyReportView가 기록(JSONL)으로 채운다. 씬에는 시안과 같은 예시 값이 들어 있다.
    public static class AccuracyReportSceneBuilder
    {
        static readonly float[] RowTops = { 376f, 469f, 562f };
        const float RowH = 84f, RowX0 = 1114f, RowX1 = 1598f;   // 안쪽 유리판(약 1109~1603) 안
        static readonly (string word, string roman, string tag, string desc)[] DifficultSample =
        {
            ("사과", "/sa-gwa/", "ㅅ 발음 약함", "'사'의 초성 ㅅ이 약하게 발음되었어요."),
            ("초콜릿", "/cho-kol-lit/", "ㅊ 발음 불명확", "'초'의 ㅊ 발음이 부정확했어요."),
            ("계산할게요", "/gye-san-hal-ge-yo/", "받침 처리 불안정", "'할'의 받침 ㄹ이 약하게 들렸어요."),   // 긴 단어(칸 맞춤 확인용)
        };

        // 모범 발음 듣기: 파형 자리(시안 px), 음절 칸 수, 단어 구분선 수
        // 모범 발음 듣기: 파형 자리(시안 px), 파형 아래 안내 글자 줄, 문장 경계 점선 수(문장이 더 많으면 점선은 숨긴다)
        const float WaveX0 = 480f, WaveX1 = 1510f, WaveY = 748f, CaptionY = 794f;
        const int SeparatorSlots = 6;
        static readonly string[] ModelSample = { "사과 주세요", "초콜릿 주세요", "학교 가요" };

        static FrameSpec Spec() => new FrameSpec
        {
            mode = TrainingMode.Accuracy,
            headerIcon = "icon_target",
            title = TrainingModeContext.AccuracyName + " 리포트",   // 변경점: "구음진단모드 리포트"(새 시안 이름)
            subtitle = "오늘의 발음 연습 결과를 확인해보세요.",
            cards = new[]
            {
                new CardSpec("icon_clock", "총 연습 시간", "24<size=62%> 분</size>", "33%", "어제보다 6분 더 연습했어요!"),
                new CardSpec("icon_aim", "발음 정확도", "92<size=62%> %</size>", "8%", "매우 좋은 결과예요!"),
                new CardSpec("icon_repeat", "반복 연습", "12<size=62%> 회</size>", "4회", "꾸준한 반복이 실력을 만듭니다."),
                new CardSpec("icon_warning", "어려운 발음", "3<size=62%> 개</size>", "2개", "어제보다 2개 줄었어요!", true),
            },
            analysisTitle = "영역별 평가",
            rows = new[]
            {
                new RowSpec(ArtDer + "icon_aim.png", Icon256, 160f, "발음 정확도", "fill_blue", 0.92f),
                new RowSpec(ArtDer + "icon_gauge.png", Icon256, 150f, "발화 속도", "fill_blue", 0.80f),
                new RowSpec(ArtDer + "icon_wave.png", Icon256, 175f, "억양 안정성", "fill_violet", 0.78f),
                new RowSpec(ArtDer + "icon_diamond.png", Icon256, 135f, "또렷함", "fill_blue", 0.88f),
                new RowSpec(ArtDer + "icon_link.png", Icon256, 160f, "띄어읽기", "fill_cyan", 0.85f),
            },
            rowY = new[] { 387f, 435f, 484f, 534f, 582f },
            radarNames = new[] { "발음 정확도", "발화 속도", "억양 안정성", "또렷함", "띄어읽기" },
            radarSamples = new[] { "92%", "80%", "78%", "88%", "85%" },
            retryLabel = "다시 연습하기",
        };

        [MenuItem("Artti/Build AccuracyReportScene Hierarchy")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[AccuracyReportSceneBuilder] Play 모드에서는 실행하지 않는다."); return; }
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            AssetDatabase.Refresh();
            OpenOrCreate(ScenePaths.AccuracyReport);
            var r = BuildFrame(Spec());
            var stage = r.stage;
            var font = r.font;

            // ===== 어려웠던 발음 목록 =====
            var listBox = RightBox;
            var list = Box("DifficultPanel", stage, Vector2.zero, listBox);
            var lo = new Vector2(listBox.xMin, listBox.yMin);
            Sliced("Frame", list, lo, ArtDir + "7.png", new Vector2(1448f, 1086f), Rect.MinMaxRect(58, 196, 1390, 876),
                new Vector4(324, 346, 218, 366), listBox, 537f / 1332f, 2048);
            // 안쪽 유리판 왼쪽 위가 비스듬히 잘려 있어(시안 (1109,362)~(1150,326)) 제목 줄은 그 안쪽에, 오른쪽 칩도 안쪽 판(~1603) 안에
            Img("TitleIcon", list, lo, ArtDer + "icon_alert.png", Rect.MinMaxRect(1141, 327, 1183, 369), 256);
            Label("Title", list, lo, "어려웠던 발음 목록", font, 22f, Ink, true, 1190, 1470, 348, TextAlignmentOptions.MidlineLeft);
            var totalBox = Rect.MinMaxRect(1500, 333, 1595, 363);
            var total = Box("TotalChip", list, lo, totalBox);
            var tco = new Vector2(totalBox.xMin, totalBox.yMin);
            Sliced("Frame", total, tco, ArtDer + "chip.png", new Vector2(400f, 160f), Rect.MinMaxRect(20, 20, 380, 140),
                new Vector4(80, 0, 80, 0), totalBox, 30f / 120f, 512);
            var totalText = Label("Text", total, tco, "총 3개", font, 16f, Ink, false, totalBox.xMin, totalBox.xMax, 348,
                TextAlignmentOptions.Center);

            var rows = new (GameObject root, TMP_Text syl, TMP_Text word, TMP_Text roman, TMP_Text tag, TMP_Text desc, Button play, Image icon)[3];
            for (int i = 0; i < 3; i++)
            {
                float y0 = RowTops[i], yc = y0 + RowH / 2f;
                var sample = DifficultSample[i];
                var rowBox = Rect.MinMaxRect(RowX0, y0, RowX1, y0 + RowH);
                var row = Box("Row" + i, list, lo, rowBox);
                var ro = new Vector2(rowBox.xMin, rowBox.yMin);
                Sliced("Plate", row, ro, ArtDer + "row_plate.png", new Vector2(640f, 240f), Rect.MinMaxRect(20, 20, 620, 220),
                    new Vector4(70, 70, 70, 70), rowBox, RowH / 200f, 1024);
                Img("Ring", row, ro, ArtDer + "icon_ring.png", Rect.MinMaxRect(1124, yc - 31, 1186, yc + 31), 256);
                var syl = Label("Syllable", row, ro, sample.word.Substring(0, 1), font, 26f, Color.white, true, 1125, 1185, yc,
                    TextAlignmentOptions.Center);
                var word = Label("Word", row, ro, sample.word, font, 21f, Ink, true, 1197, 1295, y0 + 28, TextAlignmentOptions.MidlineLeft);
                var roman = Label("Roman", row, ro, sample.roman, font, 15f, Faint, false, 1197, 1295, y0 + 60, TextAlignmentOptions.MidlineLeft);
                var tagBox = Rect.MinMaxRect(1299, y0 + 14, 1423, y0 + 40);
                Sliced("TagFrame", row, ro, ArtDer + "tag_pink.png", new Vector2(400f, 160f), Rect.MinMaxRect(20, 20, 380, 140),
                    new Vector4(80, 0, 80, 0), tagBox, 26f / 120f, 512);
                var tag = Label("Tag", row, ro, sample.tag, font, 14f, new Color32(255, 205, 222, 255), true, tagBox.xMin + 4, tagBox.xMax - 4,
                    tagBox.center.y, TextAlignmentOptions.Center);
                var desc = Label("Description", row, ro, sample.desc, font, 14.5f, SubInk, false, 1299, 1534, y0 + 62,
                    TextAlignmentOptions.MidlineLeft);
                desc.overflowMode = TextOverflowModes.Ellipsis;
                // 변경점(사용자 요청 2026-10-03, '발음 불명확' 칩과 겹침): 단어 칸은 98px뿐이라 "계산할게요" 같은 5글자 단어와
                //   긴 로마자가 오른쪽 칩·설명 위로 넘쳤다. 칸에 맞게 글자를 줄이고, 그래도 길면 말줄임
                FitInBox(word, 14f);
                FitInBox(roman, 10f);
                FitInBox(tag, 10f);
                var play = HitBox("Play", row, ro, Rect.MinMaxRect(1540, yc - 23, 1586, yc + 23));
                var playIcon = Img("Icon", play, new Vector2(1540, yc - 23), ArtDer + "icon_play.png", Rect.MinMaxRect(1537, yc - 26, 1589, yc + 26), 256);
                rows[i] = (row.gameObject, syl, word, roman, tag, desc, play.GetComponent<Button>(), playIcon);
            }
            var empty = Label("Empty", list, lo, "어려운 발음이 없어요! 잘했어요", font, 18f, Faint, false, 1100, 1612, 500, TextAlignmentOptions.Center);
            empty.gameObject.SetActive(false);

            // ===== 모범 발음 듣기 =====
            var bottomBox = BottomBox;
            var bottom = Box("BottomPanel", stage, Vector2.zero, bottomBox);
            var bo = new Vector2(bottomBox.xMin, bottomBox.yMin);
            Sliced("Frame", bottom, bo, ArtDir + "8.png", S2172, Rect.MinMaxRect(85, 137, 2092, 570),
                new Vector4(446, 284, 281, 267), bottomBox, 174f / 433f, 4096);
            // 안쪽 유리판 왼쪽 위가 비스듬히 잘려 있어(시안 약 (295,716)~(346,682)) 제목 줄·재생 버튼은 그 안쪽에
            PanelTitle(bottom, bo, ArtDer + "icon_bars.png", new Vector2(344f, 301f), 241f, new Vector2(364f, 700f), "모범 발음 듣기", font);
            var playBig = HitBox("ModelPlay", bottom, bo, Rect.MinMaxRect(350, 718, 414, 782));
            var modelIcon = Img("Icon", playBig, new Vector2(350, 718), ArtDer + "icon_play.png", Rect.MinMaxRect(342, 710, 422, 790), 256);
            var time = Label("Time", bottom, bo, "0:08", font, 16f, Ink, false, 342, 422, 796, TextAlignmentOptions.Center);
            Line("Divider", bottom, bo, Rect.MinMaxRect(465.25f, 712, 466.75f, 800), LineColor);

            // 파형: 어두운 파형(25) 위에 밝은 파형(26)을 왼쪽부터 진행만큼 보여 준다(RectMask2D 폭 = 전체 x 진행)
            var waveBody = Rect.MinMaxRect(21, 20, 1050, 87);
            var wavePng = new Vector2(1068f, 103f);
            float waveW = WaveX1 - WaveX0, ws = waveW / waveBody.width;
            var waveRect = new Rect(WaveX0 - waveBody.xMin * ws, WaveY - wavePng.y * ws / 2f, wavePng.x * ws, wavePng.y * ws);
            var dimWave = Img("WaveDim", bottom, bo, ArtDir + "25.png", waveRect, 2048);
            dimWave.preserveAspect = false;
            var progress = ChildRect("WaveProgress", bottom);
            progress.anchorMin = progress.anchorMax = new Vector2(0f, 1f);
            progress.pivot = new Vector2(0f, 0.5f);
            progress.anchoredPosition = new Vector2(WaveX0 - bo.x, -(WaveY - bo.y));
            progress.sizeDelta = new Vector2(waveW, waveRect.height);
            progress.gameObject.AddComponent<RectMask2D>();
            var bright = ChildRect("WaveBright", progress);
            bright.anchorMin = bright.anchorMax = new Vector2(0f, 0.5f);
            bright.pivot = new Vector2(0f, 0.5f);
            bright.anchoredPosition = new Vector2(-waveBody.xMin * ws, 0f);
            bright.sizeDelta = waveRect.size;
            var brightImg = bright.gameObject.AddComponent<Image>();
            brightImg.sprite = LoadSprite(ArtDir + "26.png", 2048);
            brightImg.raycastTarget = false;

            var caption = Label("Caption", bottom, bo, $"내가 말한 문장 {ModelSample.Length}개를 모범 발음으로 들어 봐요", font, 16f, SubInk, false,
                WaveX0, WaveX1, CaptionY, TextAlignmentOptions.Center);
            caption.overflowMode = TextOverflowModes.Ellipsis;
            var separators = new RectTransform[SeparatorSlots];
            for (int i = 0; i < SeparatorSlots; i++)
                separators[i] = Line("Separator" + i, bottom, bo, Rect.MinMaxRect(WaveX0 - 0.75f, 712, WaveX0 + 0.75f, 782), LineColor).rectTransform;
            // 씬 예시: 문장 3개 경계(실행하면 View가 기록으로 다시 놓는다)
            SampleSeparators(separators, bo.x);

            // ===== View 연결 =====
            var view = r.canvasGo.AddComponent<AccuracyReportView>();
            var audio = r.canvasGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var so = new SerializedObject(view);
            WireCommon(so, r);
            so.FindProperty("difficultTotal").objectReferenceValue = totalText;
            var rowsProp = so.FindProperty("difficultRows");
            rowsProp.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                var e = rowsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                e.FindPropertyRelative("syllable").objectReferenceValue = rows[i].syl;
                e.FindPropertyRelative("word").objectReferenceValue = rows[i].word;
                e.FindPropertyRelative("romanization").objectReferenceValue = rows[i].roman;
                e.FindPropertyRelative("tag").objectReferenceValue = rows[i].tag;
                e.FindPropertyRelative("description").objectReferenceValue = rows[i].desc;
                e.FindPropertyRelative("play").objectReferenceValue = rows[i].play;
                e.FindPropertyRelative("playIcon").objectReferenceValue = rows[i].icon;
            }
            so.FindProperty("difficultEmpty").objectReferenceValue = empty;
            so.FindProperty("modelPlayBtn").objectReferenceValue = playBig.GetComponent<Button>();
            so.FindProperty("modelTime").objectReferenceValue = time;
            so.FindProperty("waveProgress").objectReferenceValue = progress;
            so.FindProperty("waveFullWidth").floatValue = waveW;
            so.FindProperty("modelCaption").objectReferenceValue = caption;
            SetArray(so, "sentenceSeparators", separators);
            so.FindProperty("waveLeft").floatValue = WaveX0 - bo.x;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("modelPlayIcon").objectReferenceValue = modelIcon;
            so.FindProperty("playSprite").objectReferenceValue = LoadSprite(ArtDer + "icon_play.png", 256);
            so.FindProperty("stopSprite").objectReferenceValue = LoadSprite(ArtDer + "icon_stop.png", 256);
            so.ApplyModifiedPropertiesWithoutUndo();

            Finish(r.canvasGo, ScenePaths.AccuracyReport);
            Debug.Log("[AccuracyReportSceneBuilder] 완료 (공통 틀 + 어려웠던 발음 목록 + 모범 발음 듣기)");
        }

        // 씬 예시 문장 경계(View.SetSentences와 같은 규칙: 공백·부호를 뺀 글자 수 비율)
        static void SampleSeparators(RectTransform[] separators, float originX)
        {
            var lengths = ModelSample.Select(AccuracyReportLabels.Syllables).ToArray();
            float total = lengths.Sum(), acc = 0f;
            for (int i = 0; i < separators.Length; i++)
            {
                bool has = i < lengths.Length - 1;
                separators[i].gameObject.SetActive(has);
                if (!has) continue;
                acc += lengths[i];
                separators[i].anchoredPosition = new Vector2(WaveX0 + (WaveX1 - WaveX0) * acc / total - originX, separators[i].anchoredPosition.y);
            }
        }

        // 한 줄 글자가 칸 폭을 넘으면 minSize까지 줄이고, 그래도 넘치면 말줄임(…)
        static void FitInBox(TMP_Text text, float minSize)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = minSize;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        [MenuItem("Artti/Debug/Capture AccuracyReportScene - Tab + Phone + 4x3")]
        static void CaptureMenu() => Capture(ScenePaths.AccuracyReport, "AccuracyReport");
    }
}
