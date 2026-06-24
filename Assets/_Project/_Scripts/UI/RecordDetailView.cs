using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.AAC.Logging;
using Artti.Common;
using Artti.Common.Speech;
using Artti.Report;

namespace Artti.UI
{
    // 세션 1개의 상세 리포트 화면(hh.png). 진입 시 RecordDetailContext.SessionId 세션을 읽어 표시.
    // 데이터 가공은 ReportDataService, 이 클래스는 표시/입력/네비게이션만 담당.
    public class RecordDetailView : MonoBehaviour
    {
        [Header("헤더")]
        [SerializeField] private Button backBtn;
        [SerializeField] private Button shareBtn;

        [Header("전체 성공률")]
        [SerializeField] private Image donutFill;
        [SerializeField] private TMP_Text donutPercentText;
        [SerializeField] private TMP_Text fullSuccessText;
        [SerializeField] private TMP_Text partialSuccessText;
        [SerializeField] private TMP_Text needHelpText;

        [Header("스탯 카드")]
        [SerializeField] private TMP_Text scenarioNameText;
        [SerializeField] private Button scenarioDetailBtn; // 상세 보기 (stub)
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text durationText;

        [Header("AI 리드백")]
        [SerializeField] private TMP_Text feedbackMainText;
        [SerializeField] private TMP_Text bubbleText;
        [SerializeField] private TMP_Text[] goodPointTexts;
        [SerializeField] private TMP_Text[] improvePointTexts;
        [SerializeField] private TMP_Text[] nextGoalTexts;

        [Header("연습 상세 기록")]
        [SerializeField] private RecordStepCard[] stepCards;

        [Header("하단")]
        [SerializeField] private Button prevBtn;
        [SerializeField] private Button retryBtn;
        [SerializeField] private Button nextBtn;

        [Header("TTS")]
        [SerializeField] private AudioSource audioSource;

        private static readonly string[] ScenarioCycle = { "pharmacy", "convenience", "restaurant" };

        private ReportDataService _service;
        private ReportRecordDetail _report;
        private ITtsService _tts;
        private CancellationToken _ct;

        private void Start()
        {
            _ct = this.GetCancellationTokenOnDestroy();

            if (backBtn != null) backBtn.onClick.AddListener(GoReport);
            if (prevBtn != null) prevBtn.onClick.AddListener(GoReport);
            if (shareBtn != null) shareBtn.onClick.AddListener(HandleShare);
            if (scenarioDetailBtn != null) scenarioDetailBtn.gameObject.SetActive(false); // 상세보기 제거 - 시나리오명만 표시
            if (retryBtn != null) retryBtn.onClick.AddListener(HandleRetry);
            if (nextBtn != null) nextBtn.onClick.AddListener(HandleNext);

            RenameHeader();        // 제목 "레포트" -> "상세 기록"
            SetupDecor();          // 배경 + 메달 반짝임 + 카드 탭 펀치
            PlayIntro().Forget();  // 섹션 순차 진입 애니
            LoadAsync().Forget();
        }

        private void RenameHeader()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var t = FindDeep(canvas.transform, "Title")?.GetComponent<TMP_Text>();
            if (t != null) t.text = "상세 기록";
        }

        // ===== 연출 셋업 (런타임, 씬/빌더 수정 없이) =====

        private void SetupDecor()
        {
            // 1) 연한 파스텔 그라데이션 + 미세 입자 배경 (캔버스 맨 뒤)
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var bg = new GameObject("Backdrop", typeof(RectTransform));
                bg.transform.SetParent(canvas.transform, false);
                bg.transform.SetSiblingIndex(0); // 맨 뒤
                bg.AddComponent<UIBackdrop>();
            }

            // 2) 타임라인 트랙(연결선) 데이터 파티클
            if (canvas != null)
            {
                var connector = FindDeep(canvas.transform, "Connector");
                if (connector != null && connector.GetComponent<UITrackParticles>() == null)
                    connector.gameObject.AddComponent<UITrackParticles>();
            }

            // 3) 메달 반짝임 + 카드 탭 펀치
            if (stepCards != null)
            {
                foreach (var card in stepCards)
                {
                    if (card == null) continue;
                    var medal = card.transform.Find("Medal")?.GetComponent<Graphic>();
                    if (medal != null && medal.GetComponent<UISparkle>() == null)
                        medal.gameObject.AddComponent<UISparkle>();

                    // 카드 전체 탭 -> 펀치 + 라이트블루 플래시
                    if (card.GetComponent<UICardTap>() == null)
                    {
                        var tap = card.gameObject.AddComponent<UICardTap>();
                        tap.flashTarget = card.transform.Find("Bg")?.GetComponent<Graphic>();
                    }
                }
            }
        }

        // 제목 -> 상단 요약/스탯 -> AI -> 타임라인 순서로 살짝 떠오르며 페이드 인
        private async UniTaskVoid PlayIntro()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = canvas.transform;

            string[][] groups =
            {
                new[] { "Title", "Subtitle" },
                new[] { "DonutCard", "ScenarioCard", "StatusCard", "DateCard", "TimeCard" },
                new[] { "AiTitle", "AiPanel" },
                new[] { "TimelineTitle", "TimelinePanel" },
                new[] { "PrevBtn", "RetryBtn", "NextBtn" },
            };

            float delay = 0f;
            foreach (var group in groups)
            {
                foreach (var n in group)
                {
                    var rt = FindDeep(root, n);
                    if (rt != null) UIFx.SlideFadeIn(rt, delay, 0.45f, 44f, _ct).Forget();
                }
                delay += 0.12f;
            }
            await UniTask.CompletedTask;
        }

        private static RectTransform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root as RectTransform;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private async UniTaskVoid LoadAsync()
        {
            var store = AppBootstrap.Instance?.LogStore;
            string path = null;
            if (store != null)
            {
                await store.FlushAsync(_ct);
                path = (store as JsonAppendLogStore)?.FilePath;
            }
            _service = new ReportDataService(path);

            string sessionId = RecordDetailContext.SessionId;
            if (string.IsNullOrEmpty(sessionId))
            {
                var sessions = _service.GetSessions();
                if (sessions.Count > 0) sessionId = sessions[0].sessionId;
            }
            if (!string.IsNullOrEmpty(sessionId)) _report = _service.GetRecordReport(sessionId);

            // 데이터 없으면(프로필 미선택/로그 없음) 빈 상태로 — 가짜 값 표시 안 함
            if (_report == null)
            {
                ShowEmpty();
                return;
            }

            Bind(_report);                       // 규칙 기반 즉시 표시
            GenerateFeedbackAsync(_report).Forget(); // Gemini 응답 오면 AI 피드백 교체
        }

        // 학습 기록이 없을 때: 모든 값 비우고 카드/항목 숨김
        private void ShowEmpty()
        {
            if (donutFill != null) donutFill.fillAmount = 0f;
            if (donutPercentText != null) donutPercentText.text = "0<size=50%>%</size>";
            if (fullSuccessText != null) fullSuccessText.text = "0개";
            if (partialSuccessText != null) partialSuccessText.text = "0개";
            if (needHelpText != null) needHelpText.text = "0개";
            if (scenarioNameText != null) scenarioNameText.text = "-";
            if (statusText != null) statusText.text = "-";
            if (dateText != null) dateText.text = "-";
            if (durationText != null) durationText.text = "-";
            if (feedbackMainText != null) feedbackMainText.text = "아직 학습 기록이 없어요.";
            if (bubbleText != null) bubbleText.text = "";
            HideAll(goodPointTexts);
            HideAll(improvePointTexts);
            HideAll(nextGoalTexts);
            if (stepCards != null)
                foreach (var c in stepCards) if (c != null) c.gameObject.SetActive(false);
        }

        private static void HideAll(TMP_Text[] arr)
        {
            if (arr == null) return;
            foreach (var t in arr) if (t != null) t.gameObject.SetActive(false);
        }

        // AI 리드백 요약을 실제 Gemini 판단으로 생성해 교체 (실패 시 규칙 기반 유지).
        private async UniTaskVoid GenerateFeedbackAsync(ReportRecordDetail r)
        {
            var key = ApiKeyLoader.Get(ApiKeyLoader.GeminiApi);
            if (string.IsNullOrEmpty(key)) return;
            string nick = AppBootstrap.Instance?.ProfileManager?.ActiveProfile?.nickname;
            try
            {
                var fb = await new ReportFeedbackService(key).GenerateAsync(r, nick, _ct);
                if (fb == null) return;
                if (feedbackMainText != null && !string.IsNullOrWhiteSpace(fb.main)) UIFx.Typewriter(feedbackMainText, fb.main, 38f, _ct).Forget();
                if (bubbleText != null && !string.IsNullOrWhiteSpace(fb.bubble)) bubbleText.text = fb.bubble;
                if (fb.good != null && fb.good.Count > 0) FillList(goodPointTexts, fb.good);
                if (fb.improve != null && fb.improve.Count > 0) FillList(improvePointTexts, fb.improve);
                if (fb.next != null && fb.next.Count > 0) FillList(nextGoalTexts, fb.next);
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e) { Debug.LogWarning($"[RecordDetail] AI 피드백 실패: {e.Message}"); }
        }

        private void Bind(ReportRecordDetail r)
        {
            // 전체 성공률 (0% -> 실제% 카운트업 + 도넛 채움 동시)
            float rate = r.successRate;
            UIFx.CountUp(0f, rate, 1.2f, v =>
            {
                if (donutFill != null) donutFill.fillAmount = v;
                if (donutPercentText != null) donutPercentText.text = $"{Mathf.RoundToInt(v * 100f)}<size=50%>%</size>";
            }, _ct).Forget();
            if (fullSuccessText != null) fullSuccessText.text = $"{r.fullSuccess}개";
            if (partialSuccessText != null) partialSuccessText.text = $"{r.partialSuccess}개";
            if (needHelpText != null) needHelpText.text = $"{r.needHelp}개";

            // 스탯 카드
            if (scenarioNameText != null) scenarioNameText.text = r.scenarioName;
            if (statusText != null) statusText.text = r.completed ? "완료" : "미완료";
            if (dateText != null)
            {
                var dt = System.DateTimeOffset.FromUnixTimeMilliseconds(r.dateMs).ToLocalTime();
                dateText.text = $"{dt:yyyy.MM.dd} ({Weekday(dt.DayOfWeek)})";
            }
            if (durationText != null) durationText.text = $"{r.durationMin}분";

            // AI 리드백 (메인 문구 타자기 효과)
            if (feedbackMainText != null) UIFx.Typewriter(feedbackMainText, r.feedbackMain, 38f, _ct).Forget();
            if (bubbleText != null) bubbleText.text = r.feedbackBubble;
            FillList(goodPointTexts, r.goodPoints);
            FillList(improvePointTexts, r.improvePoints);
            FillList(nextGoalTexts, r.nextGoals);

            // 타임라인
            if (stepCards != null)
            {
                for (int i = 0; i < stepCards.Length; i++)
                {
                    var card = stepCards[i];
                    if (card == null) continue;
                    if (i >= r.steps.Count) { card.gameObject.SetActive(false); continue; }

                    var s = r.steps[i];
                    card.gameObject.SetActive(true);
                    if (card.numberText != null) card.numberText.text = $"{s.index:D2}";
                    if (card.objectiveText != null) card.objectiveText.text = s.objectiveName;
                    if (card.userText != null) card.userText.text = s.userText ?? "";
                    if (card.timeText != null) card.timeText.text = s.time;
                    if (card.ratingText != null) card.ratingText.text = s.ratingLabel;
                    if (card.speakerButton != null)
                    {
                        var line = !string.IsNullOrEmpty(s.npcText) ? s.npcText : s.userText;
                        card.speakerButton.onClick.RemoveAllListeners();
                        card.speakerButton.onClick.AddListener(() => Speak(line));
                    }
                }
            }
        }

        private static void FillList(TMP_Text[] slots, System.Collections.Generic.List<string> values)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                if (values != null && i < values.Count)
                {
                    slots[i].gameObject.SetActive(true);
                    slots[i].text = $"· {values[i]}";
                }
                else slots[i].gameObject.SetActive(false);
            }
        }

        private static string Weekday(System.DayOfWeek d)
        {
            switch (d)
            {
                case System.DayOfWeek.Monday: return "월";
                case System.DayOfWeek.Tuesday: return "화";
                case System.DayOfWeek.Wednesday: return "수";
                case System.DayOfWeek.Thursday: return "목";
                case System.DayOfWeek.Friday: return "금";
                case System.DayOfWeek.Saturday: return "토";
                default: return "일";
            }
        }

        // ===== 네비게이션 =====

        private void GoReport() => SceneManager.LoadScene("ReportScene");

        private void HandleRetry()
        {
            if (_report == null) { GoReport(); return; }
            SceneManager.LoadScene(TrainingScene(_report.scenarioId));
        }

        private void HandleNext()
        {
            string next = NextScenario(_report != null ? _report.scenarioId : null);
            SceneManager.LoadScene(TrainingScene(next));
        }

        private void HandleShare()
        {
            // TODO: 리포트 공유 (이미지/링크). 현재는 미구현.
            Debug.Log("[RecordDetail] 공유 기능 미구현");
        }

        private static string TrainingScene(string scenarioId)
        {
            switch (scenarioId)
            {
                case "convenience": return "TrainingConvenienceScene";
                case "restaurant":  return "TrainingRestaurantScene";
                default:            return "TrainingPharmacyScene";
            }
        }

        private static string NextScenario(string scenarioId)
        {
            int i = System.Array.IndexOf(ScenarioCycle, scenarioId);
            if (i < 0) return ScenarioCycle[0];
            return ScenarioCycle[(i + 1) % ScenarioCycle.Length];
        }

        // ===== TTS =====

        private void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            SpeakAsync(text).Forget();
        }

        private async UniTaskVoid SpeakAsync(string text)
        {
            try
            {
                _tts ??= BuildTts();
                if (_tts != null) await _tts.SpeakAsync(text, _ct);
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e) { Debug.LogWarning($"[RecordDetail] TTS 실패: {e.Message}"); }
        }

        private ITtsService BuildTts()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            var key = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleTtsApi, ApiKeyLoader.GeminiApi);
            if (string.IsNullOrEmpty(key)) return new NoopTtsService();
            return new CloudTtsService(key, audioSource);
        }
    }
}
