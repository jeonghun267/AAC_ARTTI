using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.UI;

namespace Artti.Training
{
    // 편의점 훈련 씬 HUD (시안 1~10.png). 표시와 입력만 담당, 흐름 제어는 TrainingSceneRoot.
    public class ConvenienceHudView : MonoBehaviour
    {
        [Header("진행 표시 (objective 기준 5단계)")]
        [SerializeField] private Image[] stepDots;
        [SerializeField] private TMP_Text stepLabel;
        [SerializeField] private TMP_Text stepCounter;
        [SerializeField] private RectTransform stepFillMask;
        [SerializeField] private float stepFillWidth;
        [SerializeField] private Sprite stepNodeOnSprite;
        [SerializeField] private Sprite stepNodeOffSprite;
        // 변경점(TrainingCon 시안): 미션 링(Filled Radial360)과 "2/5" 글자 형식. 링이 없으면 무동작.
        [SerializeField] private Image stepRing;
        [SerializeField] private string stepCounterFormat = "{0} / {1} 단계";

        [Header("일시정지")]
        [SerializeField] private Button pauseBtn;
        [SerializeField] private GameObject pauseModal;
        [SerializeField] private Button pauseConfirmBtn; // 종료
        [SerializeField] private Button pauseCancelBtn;  // 취소

        // 변경점(TrainingCon 시안): 나가기 / 현장모드 — 같은 확인 창을 문구만 바꿔 쓴다.
        [Header("나가기 확인 (옵션)")]
        [SerializeField] private Button exitBtn;
        [SerializeField] private Button fieldModeBtn;
        [SerializeField] private TMP_Text pauseTitleText;
        [SerializeField] private TMP_Text pauseMessageText;
        [SerializeField] private TMP_Text pauseConfirmLabel;

        [Header("NPC 스피커 (재청취)")]
        [SerializeField] private Button speakerBtn;
        [SerializeField] private Graphic speakerIcon;
        [SerializeField] private AudioSource ttsSource;

        [Header("사용자 발화 버블")]
        [SerializeField] private GameObject userBubble;
        [SerializeField] private TMP_Text userBubbleText;

        [Header("생각하는 중 / 칭찬")]
        [SerializeField] private GameObject thinkingRoot;
        [SerializeField] private TMP_Text thinkingText;
        [SerializeField] private TMP_Text thinkingDots;

        [Header("완료 화면")]
        [SerializeField] private GameObject completionRoot;
        [SerializeField] private TMP_Text completionScenarioText;
        [SerializeField] private TMP_Text completionDurationText;
        [SerializeField] private TMP_Text completionStepsText;
        [SerializeField] private TMP_Text completionProfileNameText;
        [SerializeField] private Button retryBtn;
        [SerializeField] private Button hubBtn;
        [SerializeField] private Button homeBtn;
        [SerializeField] private Button completionHistoryBtn;
        [SerializeField] private Button completionTopHomeBtn;
        [SerializeField] private Button completionHelpBtn;
        [SerializeField] private GameObject completionHelpPanel;
        [SerializeField] private Button completionHelpCloseBtn;
        [SerializeField] private RectTransform confettiRoot;

        public event Action OnExitRequested;
        public event Action OnReplayRequested;
        public event Action OnRetrySession;
        public event Action OnGoHub;
        public event Action OnGoHome;
        public event Action OnGoReport;
        public event Action OnGoFieldMode;

        private enum ExitTarget { Hub, FieldMode }
        private ExitTarget _exitTarget = ExitTarget.Hub;

        private static readonly Color DotDone      = new Color32(26, 86, 219, 255);
        private static readonly Color DotCurrent   = new Color32(255, 138, 61, 255); // accent 주황
        private static readonly Color DotPending   = new Color32(210, 216, 226, 255);
        private static readonly Color SpeakerIdle  = new Color32(110, 118, 135, 255);
        private static readonly Color SpeakerActive = new Color32(26, 86, 219, 255);

        private static readonly string[] PraisePool = { "완벽해요!", "잘했어요!", "최고예요!", "멋져요!" };

        private Coroutine _praiseRoutine;
        private float _dotsTime;
        private bool _thinkingAnim;

        // 변경점: 진행 표시 연출 — 단계가 오르면 목표 글자 자리에 칭찬이 톡 뜨고, 채움 막대가 다음 점까지 차오르며
        //   지나가는 점이 켜지고 톡 커진 뒤, 잠시 후 새 목표로 바뀐다. 처음 표시·같은 단계·뒤로 가기는 바로 바꾼다.
        const float FillDelaySeconds = 0.12f;
        const float FillSeconds = 0.45f;
        const float PraiseHoldSeconds = 1.1f;
        static readonly Vector2 LabelAnchorLeft = new Vector2(0f, 0.5f);   // 왼쪽 정렬 글자는 왼쪽 가운데 기준으로 커진다
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly string[] ScoreStarNames = { "ScoreStar_1", "ScoreStar_2", "ScoreStar_3", "ScoreStar_4", "ScoreStar_5" };

        private int _objectiveIndex = -1;
        private string _objectiveLabel;
        private CancellationTokenSource _objectiveCts;
        private Vector2 _labelHome;
        private CanvasGroup _labelGroup;
        private bool _completionShown;

        private void Awake()
        {
            // 변경점: 팝업은 SetActive 대신 UIPopupMotion으로 열고 닫는다(막 페이드 + 판 톡)
            if (pauseBtn != null) pauseBtn.onClick.AddListener(() => OpenExitConfirm(ExitTarget.Hub));
            if (exitBtn != null) exitBtn.onClick.AddListener(() => OpenExitConfirm(ExitTarget.Hub));
            if (fieldModeBtn != null) fieldModeBtn.onClick.AddListener(() => OpenExitConfirm(ExitTarget.FieldMode));
            if (pauseCancelBtn != null) pauseCancelBtn.onClick.AddListener(() => UIPopupMotion.Close(pauseModal));
            if (pauseConfirmBtn != null) pauseConfirmBtn.onClick.AddListener(ConfirmExit);
            if (speakerBtn != null) speakerBtn.onClick.AddListener(() => OnReplayRequested?.Invoke());
            if (retryBtn != null) retryBtn.onClick.AddListener(() => OnRetrySession?.Invoke());
            if (hubBtn != null) hubBtn.onClick.AddListener(() => OnGoHub?.Invoke());
            if (homeBtn != null) homeBtn.onClick.AddListener(() => OnGoHome?.Invoke());
            if (completionHistoryBtn != null) completionHistoryBtn.onClick.AddListener(() => OnGoReport?.Invoke());
            if (completionTopHomeBtn != null) completionTopHomeBtn.onClick.AddListener(() => OnGoHome?.Invoke());
            if (completionHelpBtn != null) completionHelpBtn.onClick.AddListener(() => UIPopupMotion.Open(completionHelpPanel));
            if (completionHelpCloseBtn != null) completionHelpCloseBtn.onClick.AddListener(() => UIPopupMotion.Close(completionHelpPanel));

            // 변경점: 누르면 살짝 작아졌다 돌아오는 탭 피드백 (점원 말풍선 = 다시 듣기 버튼은 말풍선 연출과 겹쳐 제외)
            AttachPress(pauseBtn, pauseConfirmBtn, pauseCancelBtn, retryBtn, hubBtn, homeBtn,
                completionHistoryBtn, completionTopHomeBtn, completionHelpBtn, completionHelpCloseBtn,
                exitBtn, fieldModeBtn);
            if (stepLabel != null)
            {
                _labelHome = stepLabel.rectTransform.anchoredPosition;
                _labelGroup = UIFx.Group(stepLabel);
            }

            SetActiveSafe(pauseModal, false);
            SetActiveSafe(completionRoot, false);
            SetActiveSafe(completionHelpPanel, false);
            SetActiveSafe(thinkingRoot, false);
            SetActiveSafe(userBubble, false);
        }

        // 변경점: 모드별 점원 배역에 따라 스피커 표시가 볼 NPC 음성 채널을 바꾼다.
        public void SetSpeakerSource(AudioSource source)
        {
            if (source != null) ttsSource = source;
        }

        private void Update()
        {
            // 스피커 아이콘: TTS 재생 중 색상 변경 (재청취는 여러 번 가능)
            if (speakerIcon != null && ttsSource != null)
                speakerIcon.color = ttsSource.isPlaying ? SpeakerActive : SpeakerIdle;

            // 생각하는 중: 점 3개 루프
            if (_thinkingAnim && thinkingDots != null)
            {
                _dotsTime += Time.deltaTime;
                int n = 1 + Mathf.FloorToInt(_dotsTime * 2.5f) % 3;
                thinkingDots.text = new string('.', n);
            }

            if (completionRoot != null && completionRoot.activeSelf && confettiRoot != null)
                AnimateConfetti();
        }

        // index = 현재 objective 순번 (0부터). 이전 단계 파랑, 현재 주황, 이후 회색
        public void SetObjective(int index, string label)
        {
            int count = stepDots != null ? stepDots.Length : 0;
            int clamped = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
            int prev = _objectiveIndex;
            string prevLabel = _objectiveLabel;
            _objectiveIndex = clamped;
            _objectiveLabel = label;

            var ct = UIFx.Restart(ref _objectiveCts, this);
            if (prev < 0 || clamped <= prev || count == 0)
            {
                ApplyObjective(clamped, label);
                return;
            }
            ApplyObjective(prev, prevLabel);   // 앞 연출이 중간에 끊겼어도 이전 단계 모습에서 시작
            AnimateObjectiveAsync(prev, clamped, label, ct).Forget();
        }

        // 연출 없이 해당 단계 모습으로 바로 맞춘다
        private void ApplyObjective(int current, string label)
        {
            if (stepDots != null)
            {
                for (int i = 0; i < stepDots.Length; i++) SetDot(i, current);
                if (stepCounter != null) stepCounter.text = FormatCounter(current);
                SetFill(FillWidth(current));
                SetRing(RingFill(current));
            }
            if (stepLabel != null)
            {
                stepLabel.text = label;
                UIFx.SetScaleAround(stepLabel.rectTransform, _labelHome, LabelAnchorLeft, 1f);
                if (_labelGroup != null) _labelGroup.alpha = 1f;
            }
        }

        private async UniTaskVoid AnimateObjectiveAsync(int prev, int next, string label, CancellationToken ct)
        {
            // 1) 칭찬: 목표 글자 자리에 "잘했어요!" 등이 톡 뜬다
            if (stepLabel != null)
            {
                stepLabel.text = PraisePool[UnityEngine.Random.Range(0, PraisePool.Length)];
                UIFx.BounceAround(stepLabel.rectTransform, _labelHome, LabelAnchorLeft, 0f, 0.32f, 0.8f, ct).Forget();
            }
            for (int i = 0; i <= prev; i++) SetDot(i, next);   // 지난 점은 완료 모습으로

            // 2) 채움 막대가 다음 점까지 차오르고, 채움 끝이 지나간 점이 켜지며 톡 커진다
            float w0 = FillWidth(prev), w1 = FillWidth(next);
            int lit = prev;
            bool canceled = await UIFx.Tween(this, FillDelaySeconds, FillSeconds, k =>
            {
                float e = UIFx.EaseOutCubic(k);
                SetFill(Mathf.Lerp(w0, w1, e));
                SetRing(Mathf.Lerp(RingFill(prev), RingFill(next), e));
                int reached = prev + Mathf.FloorToInt((next - prev) * e + 0.001f);
                while (lit < reached) LightDot(++lit, next, ct);
            }, ct).SuppressCancellationThrow();
            if (canceled) return;
            while (lit < next) LightDot(++lit, next, ct);
            if (stepCounter != null) stepCounter.text = FormatCounter(next);

            // 3) 칭찬을 잠시 보여 준 뒤 새 목표로 바꾼다
            if (stepLabel == null) return;
            if (await UniTask.Delay(TimeSpan.FromSeconds(PraiseHoldSeconds), DelayType.UnscaledDeltaTime,
                    PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
            if (await UIFx.FadeGroup(_labelGroup, 1f, 0f, 0f, 0.12f, ct).SuppressCancellationThrow()) return;
            stepLabel.text = label;
            UIFx.FadeGroup(_labelGroup, 0f, 1f, 0f, 0.2f, ct).Forget();
        }

        private void LightDot(int i, int current, CancellationToken ct)
        {
            SetDot(i, current);
            var rt = stepDots[i] != null ? stepDots[i].rectTransform : null;
            if (rt != null) UIFx.BounceAround(rt, rt.anchoredPosition, rt.pivot, 0f, 0.35f, 0.55f, ct).Forget();
        }

        private void SetDot(int i, int current)
        {
            var dot = stepDots[i];
            if (dot == null) return;
            if (stepNodeOnSprite != null && stepNodeOffSprite != null)
            {
                dot.sprite = i <= current ? stepNodeOnSprite : stepNodeOffSprite;
                dot.color = Color.white;
            }
            else
            {
                dot.color = i < current ? DotDone : (i == current ? DotCurrent : DotPending);
            }
            dot.rectTransform.localScale = Vector3.one;
        }

        // 점들은 0부터 stepFillWidth까지 균등 배치된다. 현재 점의 좌표에 맞춰야
        // 채움 끝이 점 사이가 아니라 정확히 현재 단계 체크포인트에서 멈춘다.
        private float FillWidth(int index) =>
            stepFillWidth * CalculateStepFillRatio(index, stepDots != null ? stepDots.Length : 0);

        private void SetFill(float width)
        {
            if (stepFillMask == null || stepFillWidth <= 0f) return;
            var size = stepFillMask.sizeDelta;
            size.x = width;
            stepFillMask.sizeDelta = size;
        }

        // 변경점(TrainingCon 시안): 링은 "몇 번째 단계인가"를 채운다(2/5 = 40%).
        private float RingFill(int index)
        {
            int count = stepDots != null ? stepDots.Length : 0;
            return count > 0 ? Mathf.Clamp01((index + 1f) / count) : 0f;
        }

        private void SetRing(float fill)
        {
            if (stepRing != null) stepRing.fillAmount = fill;
        }

        private string FormatCounter(int index) =>
            string.Format(string.IsNullOrEmpty(stepCounterFormat) ? "{0} / {1} 단계" : stepCounterFormat,
                index + 1, stepDots != null ? stepDots.Length : 0);

        // ===== 나가기 확인 (TrainingCon 시안) =====

        private void OpenExitConfirm(ExitTarget target)
        {
            _exitTarget = target;
            switch (target)
            {
                case ExitTarget.FieldMode:
                    SetText(pauseTitleText, "현장모드");
                    SetText(pauseMessageText, "훈련을 그만하고 현장모드로 갈까요?\n카메라로 가게 간판을 비춰 볼 수 있어요.");
                    SetText(pauseConfirmLabel, "현장모드로");
                    break;
                default:
                    SetText(pauseTitleText, "나가기");
                    SetText(pauseMessageText, "훈련을 그만하고 시나리오 선택으로 돌아갈까요?");
                    SetText(pauseConfirmLabel, "훈련 종료");
                    break;
            }
            UIPopupMotion.Open(pauseModal);
        }

        private void ConfirmExit()
        {
            switch (_exitTarget)
            {
                case ExitTarget.FieldMode: OnGoFieldMode?.Invoke(); break;
                default: OnExitRequested?.Invoke(); break;
            }
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null) label.text = text;
        }

        private static float CalculateStepFillRatio(int index, int stepCount)
        {
            if (stepCount <= 0) return 0f;
            if (stepCount == 1) return 1f;
            return Mathf.Clamp01((float)index / (stepCount - 1));
        }

        public void SetUserUtterance(string text)
        {
            SetActiveSafe(userBubble, !string.IsNullOrEmpty(text));
            if (userBubbleText != null) userBubbleText.text = text;
        }

        public void ShowThinking(bool on)
        {
            StopPraise();
            _thinkingAnim = on;
            _dotsTime = 0f;
            if (thinkingText != null) thinkingText.text = "생각하는 중";
            if (thinkingDots != null) thinkingDots.text = on ? "." : "";
            SetActiveSafe(thinkingRoot, on);
        }

        // STT 성공 직후: "생각하는 중" 자리를 짧은 긍정 피드백으로 전환
        public void ShowPraise(float seconds = 1.6f)
        {
            StopPraise();
            _thinkingAnim = false;
            if (thinkingText != null) thinkingText.text = PraisePool[UnityEngine.Random.Range(0, PraisePool.Length)];
            if (thinkingDots != null) thinkingDots.text = "";
            SetActiveSafe(thinkingRoot, true);
            _praiseRoutine = StartCoroutine(HidePraiseAfter(seconds));
        }

        public void ShowCompletion(string scenarioName, string durationText, string stepsText)
        {
            if (completionScenarioText != null) completionScenarioText.text = scenarioName;
            if (completionDurationText != null) completionDurationText.text = durationText;
            if (completionStepsText != null) completionStepsText.text = stepsText;
            if (completionProfileNameText != null)
            {
                string nickname = AppBootstrap.Instance?.ProfileManager?.ActiveProfile?.nickname;
                completionProfileNameText.text = string.IsNullOrWhiteSpace(nickname) ? "김연영님" : $"{nickname}님";
            }
            SetActiveSafe(thinkingRoot, false);
            SetActiveSafe(completionRoot, true);
            PlayCompletionEntrance();
        }

        // 변경점: 완료 화면 등장 — 한 번만, 1초 안에 차례로: 전체 페이드 -> 배지 톡 -> 제목 내려옴 -> 점원 올라옴
        //   -> 별 하나씩 -> 색종이 톡톡 -> 아래 버튼 올라옴. 끝난 뒤 반복되는 움직임은 없다.
        //   완료 화면은 요소들이 한 부모 아래 나란히 있어, 스스로 완결된 요소(배지·별·버튼 등)만 움직이고 나머지는 페이드만 한다.
        private void PlayCompletionEntrance()
        {
            if (completionRoot == null || _completionShown) return;
            _completionShown = true;
            var root = completionRoot.transform;
            var ct = this.GetCancellationTokenOnDestroy();

            UIFx.FadeGroup(UIFx.Group(root), 0f, 1f, 0f, 0.35f, ct).Forget();
            PopIn(root.Find("CompletionBadge"), 0.15f, 0.45f, ct);
            MoveIn(root.Find("CompletionTitle"), new Vector2(0f, 24f), 0.25f, 0.4f, ct);
            MoveIn(root.Find("CompletionClerkClip/CompletionClerk"), new Vector2(0f, -70f), 0.1f, 0.5f, ct);
            MoveIn(root.Find("SpeechBubble"), new Vector2(0f, -10f), 0.45f, 0.35f, ct);
            MoveIn(root.Find("Speech"), new Vector2(0f, -10f), 0.45f, 0.35f, ct);   // 말풍선 그림과 같은 값으로 함께 움직인다
            for (int i = 0; i < ScoreStarNames.Length; i++)
                PopIn(root.Find(ScoreStarNames[i]), 0.5f + i * 0.08f, 0.3f, ct);

            int confetti = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (!child.name.StartsWith("Confetti", StringComparison.Ordinal)) continue;
                PopIn(child, 0.2f + confetti * 0.06f, 0.35f, ct);
                confetti++;
            }

            MoveIn(root.Find("RetryButton"), new Vector2(0f, -36f), 0.7f, 0.4f, ct);
            MoveIn(root.Find("NextScenarioButton"), new Vector2(0f, -36f), 0.78f, 0.4f, ct);
            MoveIn(root.Find("HomeButton"), new Vector2(0f, -36f), 0.86f, 0.4f, ct);
        }

        private static void PopIn(Transform t, float delay, float dur, CancellationToken ct)
        {
            if (t is RectTransform rt) UIFx.PopAroundIn(rt, rt.anchoredPosition, Center, delay, dur, 0.4f, ct).Forget();
        }

        private static void MoveIn(Transform t, Vector2 fromOffset, float delay, float dur, CancellationToken ct)
        {
            if (t is RectTransform rt) UIFx.MoveFadeIn(rt, rt.anchoredPosition, fromOffset, delay, dur, ct).Forget();
        }

        private static void AttachPress(params Button[] buttons)
        {
            if (buttons == null) return;
            foreach (var b in buttons) UIPressScale.Attach(b);
        }

        // ===== 내부 =====

        private void StopPraise()
        {
            if (_praiseRoutine != null) { StopCoroutine(_praiseRoutine); _praiseRoutine = null; }
        }

        private IEnumerator HidePraiseAfter(float sec)
        {
            yield return new WaitForSeconds(sec);
            SetActiveSafe(thinkingRoot, false);
            _praiseRoutine = null;
        }

        // 컨페티: confettiRoot 자식 사각형들을 인덱스 기반 의사난수로 낙하 + 좌우 흔들림 + 회전 (2~3초 주기 반복)
        private void AnimateConfetti()
        {
            float w = confettiRoot.rect.width;
            float h = confettiRoot.rect.height;
            int count = confettiRoot.childCount;
            for (int i = 0; i < count; i++)
            {
                var child = confettiRoot.GetChild(i) as RectTransform;
                if (child == null) continue;
                float speed = 0.35f + 0.13f * (i % 4);                 // 한 사이클 2~3초
                float phase = i * 0.617f;
                float t = (Time.time * speed + phase) % 1f;
                float x = (((i * 0.137f) + 0.07f) % 1f - 0.5f) * w + Mathf.Sin(Time.time * 2f + i) * 30f;
                float y = (0.5f - t) * (h + 60f);
                child.anchoredPosition = new Vector2(x, y);
                child.localRotation = Quaternion.Euler(0, 0, Time.time * (90f + i * 17f) % 360f);
            }
        }

        private static void SetActiveSafe(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
