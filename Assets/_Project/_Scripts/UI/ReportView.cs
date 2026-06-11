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
        [SerializeField] private Image donutFill;
        [SerializeField] private TMP_Text donutPercentText;
        [SerializeField] private TMP_Text totalCompletedText;
        [SerializeField] private TMP_Text convenienceCountText;
        [SerializeField] private TMP_Text pharmacyCountText;
        [SerializeField] private TMP_Text restaurantCountText;

        [Header("세션 목록")]
        [SerializeField] private RectTransform sessionListContainer;
        [SerializeField] private GameObject sessionCardPrefab;
        [SerializeField] private GameObject emptyState;

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

            var profile = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;
            if (titleText != null)
                titleText.text = profile != null && !string.IsNullOrEmpty(profile.nickname)
                    ? $"{profile.nickname} 님의 레포트"
                    : "레포트";
            if (profile != null)
                _avatarSprite = AvatarLibrary.Load()?.GetById(profile.avatarId);

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
        }

        // ===== 패널 전환 =====

        private void HandleBack()
        {
            if (detailPanel != null && detailPanel.activeSelf) ShowList();
            else SceneManager.LoadScene("MainScene");
        }

        private void ShowList()
        {
            if (listPanel != null) listPanel.SetActive(true);
            if (detailPanel != null) detailPanel.SetActive(false);
        }

        private void SetTab(bool speech)
        {
            if (speechTabBg != null) speechTabBg.color = speech ? Primary : Color.white;
            if (arTabBg != null) arTabBg.color = speech ? Color.white : Primary;
            if (speechTabText != null) speechTabText.color = speech ? Color.white : TabIdleText;
            if (arTabText != null) arTabText.color = speech ? TabIdleText : Color.white;
            if (speechRoot != null) speechRoot.SetActive(speech);
            if (arPlaceholder != null) arPlaceholder.SetActive(!speech);
        }

        // ===== 목록 패널 =====

        private void RefreshListPanel()
        {
            if (_service == null) return;

            var overview = _service.GetOverview();
            int pct = Mathf.RoundToInt(overview.CompletionRate * 100f);
            if (donutFill != null) donutFill.fillAmount = overview.CompletionRate;
            if (donutPercentText != null) donutPercentText.text = $"{pct}<size=60%>%</size>";
            if (totalCompletedText != null) totalCompletedText.text = $"{overview.completedCount} 회";
            SetCount(convenienceCountText, overview, "convenience");
            SetCount(pharmacyCountText, overview, "pharmacy");
            SetCount(restaurantCountText, overview, "restaurant");

            ClearChildren(sessionListContainer);
            var sessions = _service.GetSessions();
            if (emptyState != null) emptyState.SetActive(sessions.Count == 0);

            foreach (var session in sessions)
            {
                var go = Instantiate(sessionCardPrefab, sessionListContainer);
                var card = go.GetComponent<SessionCardView>();
                if (card == null) continue;
                ApplyStatusBadge(card.statusBg, card.statusText, session.completed);
                if (card.nameText != null) card.nameText.text = ReportLabels.ScenarioName(session.scenarioId);
                if (card.dateText != null)
                {
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(session.dateMs).ToLocalTime();
                    card.dateText.text = $"{dt:yyyy-MM-dd}  ·  {session.durationMin} 분";
                }
                var id = session.sessionId;
                if (card.selectButton != null) card.selectButton.onClick.AddListener(() => ShowDetail(id));
            }
        }

        private static void SetCount(TMP_Text label, ReportOverview overview, string scenarioId)
        {
            if (label == null) return;
            overview.completedByScenario.TryGetValue(scenarioId, out var n);
            label.text = $"{n} 회";
        }

        // ===== 상세 패널 =====

        private void ShowDetail(string sessionId)
        {
            if (_service == null) return;
            var detail = _service.GetDetail(sessionId);
            if (detail == null) return;
            _currentSessionId = sessionId;

            if (detailScenarioText != null) detailScenarioText.text = ReportLabels.ScenarioName(detail.summary.scenarioId);
            ApplyStatusBadge(detailStatusBg, detailStatusText, detail.summary.completed);
            if (detailDateText != null)
            {
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(detail.summary.dateMs).ToLocalTime();
                detailDateText.text = $"{dt:yyyy.MM.dd}";
            }
            if (detailDurationText != null) detailDurationText.text = $"{detail.summary.durationMin}분";

            // 연습이 필요해요
            ClearChildren(practiceChipContainer);
            if (practiceSection != null) practiceSection.SetActive(detail.needsPractice.Count > 0);
            foreach (var objectiveId in detail.needsPractice)
            {
                var chip = Instantiate(practiceChipPrefab, practiceChipContainer);
                var text = chip.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = ReportLabels.ObjectiveName(objectiveId);
            }

            // 진행 흐름
            ClearChildren(stepContainer);
            foreach (var step in detail.steps)
            {
                var go = Instantiate(stepPrefab, stepContainer);
                var view = go.GetComponent<ReportStepView>();
                if (view == null) continue;
                if (view.objectiveText != null) view.objectiveText.text = ReportLabels.ObjectiveName(step.objectiveId);
                if (view.retryBadge != null) view.retryBadge.SetActive(step.retryCount > 0);
                if (view.retryText != null) view.retryText.text = $"{step.retryCount}회 다시 시도했어요.";
                foreach (var turn in step.turns)
                {
                    var bubble = Instantiate(turn.isUser ? userBubblePrefab : npcBubblePrefab, view.bubbleContainer);
                    var text = bubble.GetComponentInChildren<TMP_Text>();
                    if (text != null) text.text = turn.text;
                    if (turn.isUser)
                    {
                        var avatar = bubble.transform.Find("Avatar")?.GetComponent<Image>();
                        if (avatar != null)
                        {
                            if (_avatarSprite != null) avatar.sprite = _avatarSprite;
                            avatar.gameObject.SetActive(_avatarSprite != null);
                        }
                    }
                }
            }

            if (listPanel != null) listPanel.SetActive(false);
            if (detailPanel != null) detailPanel.SetActive(true);
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
