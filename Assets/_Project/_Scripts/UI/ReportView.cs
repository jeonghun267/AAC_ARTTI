using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.AAC.Logging;
using Artti.Common;
using Artti.Report;

namespace Artti.UI
{
    // 연습 리포트 대시보드. 데이터 가공은 ReportDataService, 이 클래스는 표시와 입력만 담당.
    // 구성: 캐릭터 카드(이름/레벨/XP/응원) + 스탯 3장 + AI 피드백 + 최근 연습 기록 + 이번 주 목표 + 추천 다음 연습.
    // '전체 보기'는 같은 씬의 오버레이(AllSessionsPanel), 기록 상세는 RecordDetailScene으로 이동.
    public class ReportView : MonoBehaviour
    {
        [Header("공통")]
        [SerializeField] private Button backBtn;
        [SerializeField] private TMP_Text subtitleText;          // "{닉네임}님, 오늘도 수고했어요!"
        [SerializeField] private Button shareBtn;
        [SerializeField] private Button saveImageBtn;
        [SerializeField] private GameObject listPanel;           // 대시보드 루트
        [SerializeField] private Button nextScenarioBtn;         // 다른 시나리오 연습하기 -> TrainingHubScene

        [Header("캐릭터 카드")]
        [SerializeField] private TMP_Text charNameText;
        [SerializeField] private TMP_Text levelNumText;          // "Level" 아래 pill 안 숫자
        [SerializeField] private Image xpFill;
        [SerializeField] private TMP_Text xpText;                // "120 / 300 XP"
        [SerializeField] private TMP_Text quoteText;             // LLM 응원 문구 (따옴표 박스)

        [Header("스탯 카드")]
        [SerializeField] private TMP_Text statCompletedText;     // 연습한 시나리오
        [SerializeField] private TMP_Text statCompletedDelta;    // "오늘 +1회"
        [SerializeField] private TMP_Text statStudyTimeText;     // 총 연습 시간
        [SerializeField] private TMP_Text statStudyTimeDelta;    // "오늘 +12분"
        [SerializeField] private TMP_Text statAccuracyText;      // 평균 정답률
        [SerializeField] private TMP_Text statAccuracyDelta;     // "+10%"

        [Header("AI 피드백")]
        [SerializeField] private TMP_Text aiFeedbackText;

        [Header("최근 연습 기록")]
        [SerializeField] private ReportRecordRow[] recordRows;   // 클릭 -> 해당 세션 상세
        [SerializeField] private Button moreBtn;                 // "전체 보기"
        [SerializeField] private Sprite convenienceIcon;
        [SerializeField] private Sprite pharmacyIcon;
        [SerializeField] private Sprite restaurantIcon;

        [Header("전체 학습 기록 (전체 보기)")]
        [SerializeField] private GameObject allSessionsPanel;
        [SerializeField] private RectTransform allSessionsContainer;
        [SerializeField] private GameObject allSessionsEmpty;
        [SerializeField] private GameObject sessionCardPrefab;
        [SerializeField] private Button allSessionsBackBtn;      // 오버레이 뒤로 -> 대시보드

        [Header("이번 주 목표")]
        [SerializeField] private Image weeklyGoalFill;
        [SerializeField] private TMP_Text weeklyGoalText;        // "2 / 5"
        [SerializeField] private TMP_Text weeklyMissionText;     // Gemini 주간 미션 (힌트 pill)

        [Header("추천 다음 연습")]
        [SerializeField] private TMP_Text recommendTitleText;
        [SerializeField] private TMP_Text recommendDescText;
        [SerializeField] private Button recommendBtn;            // 카드 전체 -> 해당 훈련 씬
        [SerializeField] private GameObject recommendIconTile;   // 음식점 외 추천 시 baked 음식 이미지를 덮는 타일
        [SerializeField] private Image recommendIcon;

        private static readonly Color CompletedBg   = new Color32(220, 243, 229, 255);
        private static readonly Color CompletedText = new Color32(30, 158, 85, 255);
        private static readonly Color PendingBg     = new Color32(238, 240, 244, 255);
        private static readonly Color PendingText   = new Color32(110, 118, 135, 255);
        private static readonly Color DeltaUp       = new Color32(40, 150, 90, 255);
        private static readonly Color DeltaDown     = new Color32(224, 96, 96, 255);
        private static readonly Color DeltaFlat     = new Color32(110, 105, 150, 255);

        const int WeeklyTarget = 5;

        private ReportDataService _service;
        private string _recommendedScenario;

        private void Start()
        {
            if (backBtn != null) backBtn.onClick.AddListener(HandleBack);
            if (moreBtn != null) moreBtn.onClick.AddListener(ShowAllSessions);
            if (allSessionsBackBtn != null) allSessionsBackBtn.onClick.AddListener(ShowList);
            if (nextScenarioBtn != null) nextScenarioBtn.onClick.AddListener(() => SceneManager.LoadScene("TrainingHubScene"));
            if (recommendBtn != null) recommendBtn.onClick.AddListener(HandleRecommend);
            if (shareBtn != null) shareBtn.onClick.AddListener(HandleShare);
            if (saveImageBtn != null) saveImageBtn.onClick.AddListener(HandleSaveImage);

            var profile = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;
            string nick = profile != null && !string.IsNullOrEmpty(profile.nickname) ? profile.nickname : null;
            if (subtitleText != null)
                subtitleText.text = nick != null ? $"{nick}님, 오늘도 수고했어요!" : "오늘도 수고했어요!";
            if (charNameText != null) charNameText.text = nick ?? "이름";

            ShowList();
            InitAsync().Forget();
        }

        private async UniTaskVoid InitAsync()
        {
            var store = AppBootstrap.Instance?.LogStore;
            string path = null;
            if (store != null)
            {
                await store.FlushAsync(this.GetCancellationTokenOnDestroy());
                path = (store as JsonAppendLogStore)?.FilePath;
            }
            _service = new ReportDataService(path);
            RefreshListPanel();
            LoadLlmTextsAsync().Forget(); // 응원 문구 / AI 피드백 / 주간 미션 (Gemini, 실패 시 폴백)
        }

        // 응원(캐릭터 카드) + AI 피드백 + 주간 미션을 병렬로 생성해 채운다.
        private async UniTaskVoid LoadLlmTextsAsync()
        {
            if (_service == null) return;
            var overview = _service.GetOverview();
            var profile = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;
            string nick = profile != null ? profile.nickname : null;
            var svc = new ReportEncouragementService(ApiKeyLoader.Get(ApiKeyLoader.GeminiApi));
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                var (encourage, feedback, missions) = await UniTask.WhenAll(
                    svc.GenerateAsync(overview, nick, ct),
                    svc.GenerateFeedbackAsync(overview, nick, ct),
                    svc.GenerateMissionsAsync(overview, nick, ct));
                if (quoteText != null && !string.IsNullOrEmpty(encourage.message)) quoteText.text = encourage.message;
                if (aiFeedbackText != null && !string.IsNullOrEmpty(feedback)) aiFeedbackText.text = feedback;
                if (weeklyMissionText != null && !string.IsNullOrEmpty(missions.weekly)) weeklyMissionText.text = missions.weekly;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogWarning($"[ReportView] LLM 문구 생성 실패(기본 문구 유지): {e.Message}"); }
        }

        // ===== 패널 전환 =====

        private void HandleBack()
        {
            if (allSessionsPanel != null && allSessionsPanel.activeSelf) ShowList();
            else SceneManager.LoadScene("MainScene");
        }

        private void ShowList()
        {
            if (listPanel != null) listPanel.SetActive(true);
            if (allSessionsPanel != null) allSessionsPanel.SetActive(false);
        }

        // "전체 보기": 전체 세션 카드를 채우고 오버레이로 전환
        private void ShowAllSessions()
        {
            if (_service == null || !HasActiveProfile()) return; // 프로필 미선택 시 진입 불가
            var sessions = _service.GetSessions();
            var ct = this.GetCancellationTokenOnDestroy();

            if (allSessionsPanel != null)
            {
                var pT = allSessionsPanel.transform;
                if (pT.Find("Title") is RectTransform tr) UIFx.SlideFadeIn(tr, 0f, 0.3f, 24f, ct).Forget();
                if (pT.Find("Subtitle") is RectTransform sr) UIFx.SlideFadeIn(sr, 0.06f, 0.3f, 24f, ct).Forget();
            }

            if (allSessionsContainer != null) ClearChildren(allSessionsContainer);
            if (allSessionsEmpty != null) allSessionsEmpty.SetActive(sessions.Count == 0);
            if (sessionCardPrefab != null && allSessionsContainer != null)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    var go = Instantiate(sessionCardPrefab, allSessionsContainer);
                    var card = go.GetComponent<SessionCardView>();
                    if (card == null) continue;
                    ApplyStatusBadge(card.statusBg, card.statusText, session.completed);
                    if (card.nameText != null) card.nameText.text = ReportLabels.ScenarioName(session.scenarioId);
                    if (card.iconImage != null)
                    {
                        var icon = IconFor(session.scenarioId);
                        card.iconImage.sprite = icon;
                        card.iconImage.enabled = icon != null;
                    }
                    if (card.dateText != null)
                    {
                        var dt = DateTimeOffset.FromUnixTimeMilliseconds(session.dateMs).ToLocalTime();
                        card.dateText.text = $"{dt:yyyy-MM-dd}  ·  {session.durationMin}분";
                    }
                    var id = session.sessionId;
                    if (card.selectButton != null) card.selectButton.onClick.AddListener(() => ShowDetail(id));

                    if (go.GetComponent<UICard3D>() == null) go.AddComponent<UICard3D>();
                    if (go.GetComponent<Shadow>() == null)
                    {
                        var sh = go.AddComponent<Shadow>();
                        sh.effectColor = new Color(0.10f, 0.16f, 0.30f, 0.20f);
                        sh.effectDistance = new Vector2(0f, -6f);
                        sh.useGraphicAlpha = false;
                    }
                    if (session.completed && card.statusBg != null && card.statusBg.GetComponent<UISparkle>() == null)
                        card.statusBg.gameObject.AddComponent<UISparkle>();
                    var chevron = go.transform.Find("Chevron");
                    if (chevron != null && chevron.GetComponent<UISparkle>() == null)
                    {
                        var sp = chevron.gameObject.AddComponent<UISparkle>();
                        sp.scaleAmp = 0.16f;
                        sp.alphaAmp = 0.30f;
                    }
                    UIFx.PopIn((RectTransform)go.transform, 0.15f + i * 0.12f, 0.42f, ct).Forget();
                }
            }

            if (listPanel != null) listPanel.SetActive(false);
            if (allSessionsPanel != null) allSessionsPanel.SetActive(true);
        }

        private Sprite IconFor(string scenarioId)
        {
            switch (scenarioId)
            {
                case "pharmacy":   return pharmacyIcon;
                case "restaurant": return restaurantIcon;
                default:           return convenienceIcon;
            }
        }

        // ===== 대시보드 =====

        private void RefreshListPanel()
        {
            if (_service == null) return;
            var stats = _service.GetSummaryStats();

            // 캐릭터 카드: 레벨 / XP
            if (levelNumText != null) levelNumText.text = stats.level.ToString();
            if (xpFill != null) xpFill.fillAmount = stats.xpPerLevel > 0 ? Mathf.Clamp01(stats.xpInLevel / (float)stats.xpPerLevel) : 0f;
            if (xpText != null) xpText.text = $"{stats.xpInLevel} / {stats.xpPerLevel} XP";

            // 스탯 3장 + 변화 칩
            if (statCompletedText != null) statCompletedText.text = $"{stats.completedCount}회";
            SetDelta(statCompletedDelta, stats.todayCompleted, $"오늘 +{stats.todayCompleted}회", "오늘 0회");
            if (statStudyTimeText != null) statStudyTimeText.text = FormatStudyTime(stats.totalStudyMinutes);
            SetDelta(statStudyTimeDelta, stats.todayMinutes, $"오늘 +{stats.todayMinutes}분", "오늘 0분");
            if (statAccuracyText != null) statAccuracyText.text = stats.hasAccuracy ? $"{stats.avgAccuracyPct}%" : "--";
            if (statAccuracyDelta != null)
            {
                if (!stats.hasAccuracy) { statAccuracyDelta.text = "기록 없음"; statAccuracyDelta.color = DeltaFlat; }
                else SetDelta(statAccuracyDelta, stats.accuracyDeltaPct,
                    $"{(stats.accuracyDeltaPct > 0 ? "+" : "")}{stats.accuracyDeltaPct}%", "변화 없음");
            }

            // 이번 주 목표 (완료 세션 기준)
            int doneWeek = 0;
            var today = DateTimeOffset.Now.ToLocalTime().Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            foreach (var s in _service.GetSessions())
            {
                if (!s.completed) continue;
                var d = DateTimeOffset.FromUnixTimeMilliseconds(s.dateMs).ToLocalTime().Date;
                if (d >= weekStart) doneWeek++;
            }
            if (weeklyGoalFill != null) weeklyGoalFill.fillAmount = Mathf.Clamp01(doneWeek / (float)WeeklyTarget);
            if (weeklyGoalText != null) weeklyGoalText.text = $"{doneWeek} / {WeeklyTarget}";

            // 추천 다음 연습
            _recommendedScenario = _service.GetRecommendedScenario();
            if (recommendTitleText != null) recommendTitleText.text = ReportLabels.ScenarioTaskTitle(_recommendedScenario);
            if (recommendDescText != null) recommendDescText.text = ReportLabels.ScenarioTaskDesc(_recommendedScenario);
            bool coverFood = _recommendedScenario != "restaurant"; // 카드 이미지에 음식이 박혀 있어 다른 시나리오면 타일로 덮음
            if (recommendIconTile != null) recommendIconTile.SetActive(coverFood);
            if (recommendIcon != null) { recommendIcon.sprite = IconFor(_recommendedScenario); recommendIcon.enabled = coverFood && recommendIcon.sprite != null; }

            RefreshRecords();
            if (moreBtn != null) moreBtn.interactable = HasActiveProfile();
        }

        // 변화 칩: 양수 초록, 음수 빨강, 0은 회색 플랫 문구
        private static void SetDelta(TMP_Text t, int value, string text, string zeroText)
        {
            if (t == null) return;
            if (value == 0) { t.text = zeroText; t.color = DeltaFlat; return; }
            t.text = text;
            t.color = value > 0 ? DeltaUp : DeltaDown;
        }

        private static bool HasActiveProfile() =>
            AppBootstrap.Instance?.ProfileManager?.ActiveProfile != null;

        private void RefreshRecords()
        {
            if (recordRows == null || recordRows.Length == 0) return;
            var records = HasActiveProfile() ? _service.GetRecentRecords(recordRows.Length)
                                             : new List<ReportRecord>();
            for (int i = 0; i < recordRows.Length; i++)
            {
                var row = recordRows[i];
                if (row == null) continue;
                if (i >= records.Count) { row.gameObject.SetActive(false); continue; }

                var rec = records[i];
                row.gameObject.SetActive(true);
                if (row.iconImage != null)
                {
                    var icon = IconFor(rec.scenarioId);
                    row.iconImage.sprite = icon;
                    row.iconImage.enabled = icon != null;
                }
                if (row.nameText != null) row.nameText.text = $"{ReportLabels.ScenarioName(rec.scenarioId)} 상황 대화";
                if (row.subText != null) row.subText.text = ReportLabels.RecordComment(rec.completed, rec.accuracyPct);
                if (row.statusText != null)
                {
                    row.statusText.text = rec.completed ? "완료" : "학습중";
                    row.statusText.color = rec.completed ? CompletedText : PendingText;
                }
                if (row.accuracyText != null) row.accuracyText.text = rec.accuracyPct >= 0 ? $"{rec.accuracyPct}%" : "--";
                if (row.dateText != null)
                {
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(rec.dateMs).ToLocalTime();
                    row.dateText.text = $"{dt:MM.dd}";
                }
                if (row.pointsText != null) row.pointsText.text = $"+{rec.points}";
                if (row.button != null)
                {
                    var id = rec.sessionId;
                    row.button.onClick.RemoveAllListeners();
                    row.button.onClick.AddListener(() => ShowDetail(id));
                }
            }
        }

        private static string FormatStudyTime(int minutes)
        {
            if (minutes < 60) return $"{minutes}분";
            int h = minutes / 60, m = minutes % 60;
            return m > 0 ? $"{h}시간 {m}분" : $"{h}시간";
        }

        // ===== 네비게이션 / 액션 =====

        private void ShowDetail(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            RecordDetailContext.SessionId = sessionId;
            SceneManager.LoadScene("RecordDetailScene");
        }

        private void HandleRecommend()
        {
            SceneManager.LoadScene(TrainingScene(_recommendedScenario));
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

        private void HandleShare()
        {
            // TODO: 리포트 공유 (이미지/링크). 현재는 미구현.
            Debug.Log("[ReportView] 공유 기능 미구현");
        }

        // 화면 캡처 -> NativeGallery로 갤러리(앨범 Artti)에 저장. 캡처는 프레임 끝에서만 가능해 Coroutine 사용.
        private void HandleSaveImage()
        {
            if (_saving) return;
            StartCoroutine(SaveImageRoutine());
        }

        private bool _saving;

        private System.Collections.IEnumerator SaveImageRoutine()
        {
            _saving = true;
            if (saveImageBtn != null) saveImageBtn.interactable = false;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            string file = $"report_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            NativeGallery.SaveImageToGallery(tex, "Artti", file, (ok, path) =>
                Debug.Log(ok ? $"[ReportView] 리포트 이미지 저장: {path}" : "[ReportView] 리포트 이미지 저장 실패"));
            Destroy(tex);
            if (saveImageBtn != null) saveImageBtn.interactable = true;
            _saving = false;
        }

        // ===== 헬퍼 =====

        private static void ApplyStatusBadge(Image bg, TMP_Text text, bool completed)
        {
            if (bg != null) bg.color = completed ? CompletedBg : PendingBg;
            if (text != null)
            {
                text.text = completed ? "완료" : "미완료";
                text.color = completed ? CompletedText : PendingText;
            }
        }

        private static void ClearChildren(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
        }
    }
}
