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
    // 레포트 화면(401/402 시안). 목록 패널과 상세 패널을 토글.
    // 데이터 가공은 ReportDataService, 이 클래스는 표시와 입력만 담당.
    public class ReportView : MonoBehaviour
    {
        [Header("공통")]
        [SerializeField] private Button backBtn;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private GameObject listPanel;
        [SerializeField] private GameObject detailPanel;

        [Header("탭")]
        [SerializeField] private Button speechTabBtn;
        [SerializeField] private Button arTabBtn;
        [SerializeField] private Image speechTabBg;
        [SerializeField] private Image arTabBg;
        [SerializeField] private TMP_Text speechTabText;
        [SerializeField] private TMP_Text arTabText;
        [SerializeField] private GameObject speechRoot;
        [SerializeField] private GameObject arPlaceholder;

        [Header("한눈에 요약")]
        [SerializeField] private TMP_Text statCompletedText;    // 완료 시나리오
        [SerializeField] private TMP_Text statStudyTimeText;    // 총 학습 시간
        [SerializeField] private TMP_Text statStreakText;       // 연속 학습
        [SerializeField] private TMP_Text statLevelText;        // 레벨 (Level N)
        [SerializeField] private TMP_Text statLevelTitleText;   // 레벨 칭호
        [SerializeField] private Button[] summaryButtons;       // 스탯 카드 + 전체 보기 (클릭 -> 최신 상세)

        [Header("하단 격려 바")]
        [SerializeField] private TMP_Text encourageText;        // LLM 응원 문구 (캐릭터 말풍선 대체)

        [Header("헤더 / 캐릭터")]
        [SerializeField] private Image profileAvatar;           // 좌상단 프로필 아바타
        [SerializeField] private TMP_Text charNameText;         // 캐릭터 카드 이름

        [Header("그래프 (최근 7일 추세)")]
        [SerializeField] private LineChart sessionTrendLine;    // 전체 학습 세션
        [SerializeField] private LineChart completedTrendLine;  // 전체 학습 세션 보조선(완료)
        [SerializeField] private LineChart appearanceTrendLine; // 전체 출현
        [SerializeField] private Button[] graphButtons;         // 그래프 패널 (클릭 -> 최신 상세)

        [Header("일일미션 & 목표")]
        [SerializeField] private Image dailyGoalFill;           // 일일 목표 진행 바
        [SerializeField] private TMP_Text dailyGoalText;        // "x/1"
        [SerializeField] private Image weeklyGoalFill;          // 주간 목표 진행 바
        [SerializeField] private TMP_Text weeklyGoalText;       // "x/5"
        [SerializeField] private TMP_Text dailyMissionText;     // Gemini 일일 미션
        [SerializeField] private TMP_Text weeklyMissionText;    // Gemini 주간 미션

        [Header("최근 학습 기록")]
        [SerializeField] private ReportRecordRow[] recordRows;  // 클릭 -> 해당 세션 상세

        [Header("전체 학습 기록 (전체 보기)")]
        [SerializeField] private Button moreBtn;                // "전체 보기" -> 전체 세션 목록
        [SerializeField] private GameObject allSessionsPanel;   // 전체 목록 오버레이
        [SerializeField] private RectTransform allSessionsContainer; // 세션 카드 컨테이너
        [SerializeField] private GameObject allSessionsEmpty;   // 빈 상태 안내
        [SerializeField] private GameObject sessionCardPrefab;  // 세션 카드 프리팹
        [SerializeField] private Sprite convenienceIcon;        // 편의점 카드 아이콘
        [SerializeField] private Sprite pharmacyIcon;           // 약국
        [SerializeField] private Sprite restaurantIcon;         // 음식점

        [Header("학습 상세")]
        [SerializeField] private TMP_Text detailScenarioText;
        [SerializeField] private Image detailStatusBg;
        [SerializeField] private TMP_Text detailStatusText;
        [SerializeField] private TMP_Text detailDateText;
        [SerializeField] private TMP_Text detailDurationText;
        [SerializeField] private RectTransform practiceChipContainer;
        [SerializeField] private GameObject practiceChipPrefab;
        [SerializeField] private GameObject practiceSection;
        [SerializeField] private RectTransform stepContainer;
        [SerializeField] private GameObject stepPrefab;
        [SerializeField] private GameObject npcBubblePrefab;
        [SerializeField] private GameObject userBubblePrefab;
        [SerializeField] private Button deleteBtn;

        [Header("삭제 확인")]
        [SerializeField] private GameObject deleteConfirmPopup;
        [SerializeField] private Button deleteConfirmBtn;
        [SerializeField] private Button deleteCancelBtn;

        [Header("캐릭터 연출")]
        [SerializeField] private ReportCharacterDirector characterDirector;
        [SerializeField] private GameObject characterSpeechBubble; // 말풍선 (전체보기에서 숨김용 직접 참조)

        private static readonly Color CompletedBg   = new Color32(220, 243, 229, 255);
        private static readonly Color CompletedText = new Color32(30, 158, 85, 255);
        private static readonly Color PendingBg     = new Color32(238, 240, 244, 255);
        private static readonly Color PendingText   = new Color32(110, 118, 135, 255);
        private static readonly Color Primary       = new Color32(26, 86, 219, 255);
        private static readonly Color TabIdleText   = new Color32(33, 41, 60, 255);

        private ReportDataService _service;
        private Sprite _avatarSprite;
        private string _currentSessionId;

        private void Start()
        {
            backBtn.onClick.AddListener(HandleBack);
            speechTabBtn.onClick.AddListener(() => SetTab(speech: true));
            arTabBtn.onClick.AddListener(() => SetTab(speech: false));
            if (deleteBtn != null) deleteBtn.onClick.AddListener(() => deleteConfirmPopup?.SetActive(true));
            if (deleteCancelBtn != null) deleteCancelBtn.onClick.AddListener(() => deleteConfirmPopup?.SetActive(false));
            if (deleteConfirmBtn != null) deleteConfirmBtn.onClick.AddListener(HandleDeleteConfirmed);
            if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);

            // 요약 스탯 카드 / 그래프 패널 -> 최신 학습 상세, 전체 보기 -> 전체 세션 목록
            if (summaryButtons != null)
                foreach (var b in summaryButtons) if (b != null) b.onClick.AddListener(ShowLatestDetail);
            if (graphButtons != null)
                foreach (var b in graphButtons) if (b != null) b.onClick.AddListener(ShowLatestDetail);
            if (moreBtn != null) moreBtn.onClick.AddListener(ShowAllSessions);

            var profile = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;
            string nick = profile != null && !string.IsNullOrEmpty(profile.nickname) ? profile.nickname : null;
            if (titleText != null)
                titleText.text = nick != null ? $"{nick}님의 종합 리포트" : "종합 리포트";
            if (charNameText != null) charNameText.text = nick ?? "이름";
            if (profile != null)
                _avatarSprite = AvatarLibrary.Load()?.GetById(profile.avatarId);
            if (profileAvatar != null && _avatarSprite != null) profileAvatar.sprite = _avatarSprite;

            SetTab(speech: true);
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
            LoadMissionsAsync().Forget(); // 일일/주간 미션 (Gemini)

            // 데이터 준비 완료 -> 캐릭터 입장 연출 시작. 말풍선 문구는 레포트 기록 기반으로 LLM 생성.
            PlayCharacterIntro();
        }

        // 캐릭터 walk 입장과 병렬로 레포트 기반 응원 문구(LLM)를 생성해 director에 넘긴다.
        private void PlayCharacterIntro()
        {
            if (_service == null) return;

            var overview = _service.GetOverview();
            var profile = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;
            string nick = profile != null ? profile.nickname : null;

            var geminiKey = ApiKeyLoader.Get(ApiKeyLoader.GeminiApi);
            var encourage = new ReportEncouragementService(geminiKey);
            var ct = this.GetCancellationTokenOnDestroy();

            var messageTask = GenerateMessageAsync(encourage, overview, nick, ct);
            // 캐릭터가 있으면 말풍선, 없으면(oo.png 대시보드) 하단 격려 바에 표시
            if (characterDirector != null) characterDirector.Play(messageTask);
            else ShowEncouragementAsync(messageTask).Forget();
        }

        // 하단 격려 바에 LLM 응원 문구 표시. 실패/취소 시 빌더 기본 문구 유지.
        private async UniTaskVoid ShowEncouragementAsync(UniTask<(string message, bool isHighScore)> task)
        {
            if (encourageText == null) return;
            try
            {
                var (message, _) = await task;
                if (!string.IsNullOrEmpty(message)) encourageText.text = message;
            }
            catch (OperationCanceledException) { }
            catch { /* LLM 실패 시 기본 문구 유지 */ }
        }

        // 일일/주간 미션을 Gemini로 생성해 목표 카드에 표시. 실패/오프라인 시 폴백 문구.
        private async UniTaskVoid LoadMissionsAsync()
        {
            if (_service == null || (dailyMissionText == null && weeklyMissionText == null)) return;
            var overview = _service.GetOverview();
            var profile = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;
            string nick = profile != null ? profile.nickname : null;
            var svc = new ReportEncouragementService(ApiKeyLoader.Get(ApiKeyLoader.GeminiApi));
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                var m = await svc.GenerateMissionsAsync(overview, nick, ct);
                if (dailyMissionText != null) dailyMissionText.text = m.daily;
                if (weeklyMissionText != null) weeklyMissionText.text = m.weekly;
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private static async UniTask<(string message, bool isHighScore)> GenerateMessageAsync(
            ReportEncouragementService svc, ReportOverview overview, string nick, System.Threading.CancellationToken ct)
        {
            var r = await svc.GenerateAsync(overview, nick, ct);
            return (r.message, r.isHighScore);
        }

        // ===== 패널 전환 =====

        private bool _detailFromAll; // 상세를 전체목록에서 열었는지 (뒤로가기 복귀용)

        private void HandleBack()
        {
            if (detailPanel != null && detailPanel.activeSelf)
            {
                if (_detailFromAll) ShowAllSessions();
                else ShowList();
            }
            else if (allSessionsPanel != null && allSessionsPanel.activeSelf) ShowList();
            else SceneManager.LoadScene("MainScene");
        }

        private void ShowList()
        {
            if (listPanel != null) listPanel.SetActive(true);
            if (detailPanel != null) detailPanel.SetActive(false);
            if (allSessionsPanel != null) allSessionsPanel.SetActive(false);
            SetCharacterShown(true);
        }

        // 캐릭터/말풍선은 캔버스 직속이라 패널 토글로 안 가려짐 -> 전체보기에선 직접 숨김
        private void SetCharacterShown(bool on)
        {
            if (characterDirector != null)
            {
                characterDirector.SetSuppressed(!on);   // talk 연출이 말풍선 다시 켜는 것 방지
                characterDirector.gameObject.SetActive(on);
            }
            // 말풍선은 숨길 때만 강제(보일 땐 director가 제어). 직접 참조 우선, 없으면 이름으로 탐색.
            if (!on)
            {
                if (characterSpeechBubble != null) characterSpeechBubble.SetActive(false);
                var speech = transform.Find("CharacterSpeech");
                if (speech != null) speech.gameObject.SetActive(false);
            }
        }

        // "전체 보기": 전체 세션 카드를 채우고 목록 패널로 전환
        private void ShowAllSessions()
        {
            if (_service == null || !HasActiveProfile()) return; // 프로필 미선택 시 진입 불가
            var sessions = _service.GetSessions();

            var ct = this.GetCancellationTokenOnDestroy();

            // 제목/부제 진입 애니 (패널 직속이라 위치 애니 OK)
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

                    // 🃏 3D 호버(틸트/떠오름)
                    if (go.GetComponent<UICard3D>() == null) go.AddComponent<UICard3D>();

                    // 🃏 떠있는 느낌의 부드러운 그림자 (레이아웃 안 깨지게 카드 본체에 Shadow 이펙트)
                    if (go.GetComponent<Shadow>() == null)
                    {
                        var sh = go.AddComponent<Shadow>();
                        sh.effectColor = new Color(0.10f, 0.16f, 0.30f, 0.20f);
                        sh.effectDistance = new Vector2(0f, -6f);
                        sh.useGraphicAlpha = false;
                    }

                    // 💡 완료 배지: 미세한 반복 펄스(스케일+밝기)
                    if (session.completed && card.statusBg != null &&
                        card.statusBg.GetComponent<UISparkle>() == null)
                        card.statusBg.gameObject.AddComponent<UISparkle>();

                    // 💡 화살표(>): 미세한 반복 펄스로 "탭 가능" 힌트
                    var chevron = go.transform.Find("Chevron");
                    if (chevron != null && chevron.GetComponent<UISparkle>() == null)
                    {
                        var sp = chevron.gameObject.AddComponent<UISparkle>();
                        sp.scaleAmp = 0.16f;   // 화살표는 살짝 더 또렷하게
                        sp.alphaAmp = 0.30f;
                    }

                    // 🌱 카드 순차 팝인 (레이아웃 그룹이라 위치 대신 알파+스케일) - 천천히 보이게
                    UIFx.PopIn((RectTransform)go.transform, 0.15f + i * 0.12f, 0.42f, ct).Forget();
                }
            }

            if (listPanel != null) listPanel.SetActive(false);
            if (detailPanel != null) detailPanel.SetActive(false);
            if (allSessionsPanel != null) allSessionsPanel.SetActive(true);
            SetCharacterShown(false); // 전체보기에선 캐릭터+말풍선 숨김
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

        private void SetTab(bool speech)
        {
            // 이미지 탭: 선택은 불투명(1), 비선택은 반투명(0.5). 베이크 텍스트라 색은 안 건드림.
            if (speechTabBg != null) { var c = speechTabBg.color; c.a = speech ? 1f : 0.5f; speechTabBg.color = c; }
            if (arTabBg != null) { var c = arTabBg.color; c.a = speech ? 0.5f : 1f; arTabBg.color = c; }
            if (speechRoot != null) speechRoot.SetActive(speech);
            if (arPlaceholder != null) arPlaceholder.SetActive(!speech);
        }

        // ===== 목록 패널 =====

        private void RefreshListPanel()
        {
            if (_service == null) return;

            // 한눈에 요약 4스탯
            var stats = _service.GetSummaryStats();
            if (statCompletedText != null) statCompletedText.text = $"{stats.completedCount}회";
            if (statStudyTimeText != null) statStudyTimeText.text = FormatStudyTime(stats.totalStudyMinutes);
            if (statStreakText != null) statStreakText.text = $"{stats.streakDays}일";
            if (statLevelText != null) statLevelText.text = $"Level {stats.level}";
            if (statLevelTitleText != null) statLevelTitleText.text = stats.levelTitle;

            // 그래프 (최근 7일 추세)
            const int days = 7;
            if (sessionTrendLine != null) sessionTrendLine.SetValues(_service.GetSessionTrend(days));
            if (completedTrendLine != null) completedTrendLine.SetValues(_service.GetCompletedTrend(days));
            if (appearanceTrendLine != null) appearanceTrendLine.SetValues(_service.GetAppearanceTrend(days));

            // 일일미션 & 목표 (완료 세션 기준: 오늘 / 이번 주)
            int doneToday = 0, doneWeek = 0;
            var today = DateTimeOffset.Now.ToLocalTime().Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            foreach (var s in _service.GetSessions())
            {
                if (!s.completed) continue;
                var d = DateTimeOffset.FromUnixTimeMilliseconds(s.dateMs).ToLocalTime().Date;
                if (d == today) doneToday++;
                if (d >= weekStart) doneWeek++;
            }
            const int dailyTarget = 2, weeklyTarget = 5;
            if (dailyGoalFill != null) dailyGoalFill.fillAmount = Mathf.Clamp01(doneToday / (float)dailyTarget);
            if (dailyGoalText != null) dailyGoalText.text = $"{doneToday}/{dailyTarget}";
            if (weeklyGoalFill != null) weeklyGoalFill.fillAmount = Mathf.Clamp01(doneWeek / (float)weeklyTarget);
            if (weeklyGoalText != null) weeklyGoalText.text = $"{doneWeek}/{weeklyTarget}";

            // 최근 학습 기록 (+ 프로필 미선택 시 전체보기 비활성)
            RefreshRecords();
            if (moreBtn != null) moreBtn.interactable = HasActiveProfile();
        }

        // 활성 프로필이 선택돼 있는지. 미선택 시 기록/전체보기 차단.
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
                if (row.nameText != null) row.nameText.text = ReportLabels.ScenarioName(rec.scenarioId);
                if (row.dateText != null)
                {
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(rec.dateMs).ToLocalTime();
                    row.dateText.text = $"{dt:yyyy.MM.dd}";
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

        private void ShowLatestDetail()
        {
            if (_service == null) return;
            var sessions = _service.GetSessions();
            if (sessions.Count == 0) return;
            ShowDetail(sessions[0].sessionId);
        }

        private static string FormatStudyTime(int minutes)
        {
            if (minutes < 60) return $"{minutes}분";
            int h = minutes / 60, m = minutes % 60;
            return m > 0 ? $"{h}시간 {m}분" : $"{h}시간";
        }

        // ===== 상세: 별도 씬(RecordDetailScene)으로 이동 =====

        private void ShowDetail(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            RecordDetailContext.SessionId = sessionId;   // 선택 세션 전달
            SceneManager.LoadScene("RecordDetailScene");
        }

        private void HandleDeleteConfirmed()
        {
            if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);
            if (_service == null || string.IsNullOrEmpty(_currentSessionId)) return;
            if (_service.DeleteSession(_currentSessionId))
            {
                _currentSessionId = null;
                RefreshListPanel();
                ShowList();
            }
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
