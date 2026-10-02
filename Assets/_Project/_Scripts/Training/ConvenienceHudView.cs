using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.Report;
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
        // 변경점(TrainingCon 시안): 진행 배. 단계 점(stepDots) 자리를 따라 물 위를 스르륵 움직이고 평소에는 살짝 흔들린다.
        //   점과 같은 부모 아래에 두고 피벗은 배 바닥에 둔다. 비우면 무동작.
        [SerializeField] private RectTransform stepBoat;
        [SerializeField] private Vector2 stepBoatOffset;
        [SerializeField] private float stepMoveSeconds = 0.45f;   // 단계가 오를 때 채움(배)이 다음 점까지 가는 시간
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

        // 변경점(ending 시안): 완료 화면 = 모드별 배경(정확도 1.png / 대화 11.png) + 점수 판 + 기록 판 3개 + 버튼 5개.
        //   숫자와 문구는 TrainingResult(레포트와 같은 기록에서 계산)로 채운다.
        [Header("완료 화면")]
        [SerializeField] private GameObject completionRoot;
        [SerializeField] private Image completionBackground;
        [SerializeField] private Sprite accuracyBackgroundSprite;
        [SerializeField] private Sprite conversationBackgroundSprite;
        [SerializeField] private TMP_Text completionScoreText;
        [SerializeField] private Image[] completionStars;
        [SerializeField] private Sprite starOnSprite;
        [SerializeField] private Sprite starOffSprite;
        [SerializeField] private TMP_Text completionSummaryText;
        [SerializeField] private TMP_Text completionAccuracyText;
        [SerializeField] private TMP_Text completionAccuracyNote;
        [SerializeField] private TMP_Text completionExpressionText;
        [SerializeField] private TMP_Text completionExpressionNote;
        // 새 기록 판: 최고 기록이면 '새 기록' '최고 점수!'가 그려진 그림, 아니면 글자를 지운 그림 + 제목·큰 글자(TMP)
        [SerializeField] private Image completionRecordCard;
        [SerializeField] private Sprite recordBestSprite;
        [SerializeField] private Sprite recordPlainSprite;
        [SerializeField] private TMP_Text completionRecordTitle;
        [SerializeField] private TMP_Text completionRecordHeadline;
        [SerializeField] private TMP_Text completionRecordNote;
        [SerializeField] private Button retryBtn;
        [SerializeField] private Button hubBtn;
        [SerializeField] private Button homeBtn;
        [SerializeField] private Button completionReportBtn;
        [SerializeField] private Button completionQuitBtn;

        public event Action OnExitRequested;
        public event Action OnReplayRequested;
        public event Action OnRetrySession;
        public event Action OnGoHub;
        public event Action OnGoHome;
        public event Action OnGoReport;
        public event Action OnGoFieldMode;
        public event Action OnQuitApp;

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
        // 배 흔들림: 천천히 위아래 2.5, 기울기 3도(번쩍임 없음)
        const float BoatBobUnits = 2.5f;
        const float BoatBobHz = 0.45f;
        const float BoatTiltDegrees = 3f;
        const float PraiseHoldSeconds = 1.1f;
        static readonly Vector2 LabelAnchorLeft = new Vector2(0f, 0.5f);   // 왼쪽 정렬 글자는 왼쪽 가운데 기준으로 커진다
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        // 완료 화면 요소 경로(completionRoot 기준). 배경을 따라 크기가 바뀌는 무대(Stage) 아래에 있다
        const string EndingStage = "Background/Stage";
        static readonly string[] EndingStatCards = { "AccuracyCard", "ExpressionCard", "RecordCard" };
        static readonly string[] EndingButtons = { "RetryButton", "NextScenarioButton", "HomeButton" };

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
            if (completionReportBtn != null) completionReportBtn.onClick.AddListener(() => OnGoReport?.Invoke());
            if (completionQuitBtn != null) completionQuitBtn.onClick.AddListener(() => OnQuitApp?.Invoke());

            // 변경점: 누르면 살짝 작아졌다 돌아오는 탭 피드백 (점원 말풍선 = 다시 듣기 버튼은 말풍선 연출과 겹쳐 제외)
            AttachPress(pauseBtn, pauseConfirmBtn, pauseCancelBtn, retryBtn, hubBtn, homeBtn,
                completionReportBtn, completionQuitBtn, exitBtn, fieldModeBtn);
            if (stepLabel != null)
            {
                _labelHome = stepLabel.rectTransform.anchoredPosition;
                _labelGroup = UIFx.Group(stepLabel);
            }

            SetActiveSafe(pauseModal, false);
            SetActiveSafe(completionRoot, false);
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

            // 진행 배: 물 위에서 살짝 흔들린다(자리는 SetBoat가 정한 _boatBase)
            if (_boatPlaced)
            {
                _boatTime += Time.deltaTime;
                float phase = _boatTime * BoatBobHz * 2f * Mathf.PI;
                stepBoat.anchoredPosition = _boatBase + new Vector2(0f, Mathf.Sin(phase) * BoatBobUnits);
                stepBoat.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase + 1.2f) * BoatTiltDegrees);
            }
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
                SetBoat(current);
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
            bool canceled = await UIFx.Tween(this, FillDelaySeconds, stepMoveSeconds, k =>
            {
                float e = 0.5f - 0.5f * Mathf.Cos(k * Mathf.PI);   // 스르륵: 천천히 떠나 천천히 닿는다
                SetFill(Mathf.Lerp(w0, w1, e));
                SetBoat(Mathf.Lerp(prev, next, e));
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

        // 배 자리 = 단계 점 사이를 잇는 위치(progress = 0 ~ 점 개수-1, 소수면 두 점 사이). 점 간격이 고르지 않아도 별 위에 선다.
        private Vector2 _boatBase;
        private bool _boatPlaced;
        private float _boatTime;

        private void SetBoat(float progress)
        {
            if (stepBoat == null || stepDots == null || stepDots.Length == 0) return;
            int last = stepDots.Length - 1;
            progress = Mathf.Clamp(progress, 0f, last);
            int i0 = Mathf.FloorToInt(progress);
            int i1 = Mathf.Min(i0 + 1, last);
            _boatBase = Vector2.Lerp(DotPosition(i0), DotPosition(i1), progress - i0) + stepBoatOffset;
            _boatPlaced = true;
            stepBoat.anchoredPosition = _boatBase;
        }

        private Vector2 DotPosition(int i) =>
            stepDots[i] != null ? stepDots[i].rectTransform.anchoredPosition : Vector2.zero;

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

        public void ShowCompletion(TrainingResult result)
        {
            if (result == null) return;
            ApplyCompletion(result);
            SetActiveSafe(thinkingRoot, false);
            SetActiveSafe(completionRoot, true);
            PlayCompletionEntrance();
        }

        // 연출 없이 값만 채운다(에디터 캡처 메뉴도 이것을 부른다)
        private void ApplyCompletion(TrainingResult result)
        {
            if (completionBackground != null)
            {
                var bg = result.mode == TrainingMode.Conversation ? conversationBackgroundSprite : accuracyBackgroundSprite;
                if (bg != null) completionBackground.sprite = bg;
            }

            // 점수 판: "100점"(점은 작게) + 점수만큼 켜진 별. 기록이 없으면 "완료!"와 별 5개
            SetText(completionScoreText, result.hasRecord ? $"{result.score}<size=60%>점</size>" : "완료!");
            if (completionStars != null)
            {
                int lit = result.hasRecord ? result.stars : completionStars.Length;
                for (int i = 0; i < completionStars.Length; i++)
                {
                    var sprite = i < lit ? starOnSprite : starOffSprite;
                    if (completionStars[i] != null && sprite != null) completionStars[i].sprite = sprite;
                }
            }
            SetText(completionSummaryText, TrainingResultLabels.Summary(result));

            SetText(completionAccuracyText, TrainingResultLabels.AccuracyValue(result));
            SetText(completionAccuracyNote, TrainingResultLabels.AccuracyNote(result));
            SetText(completionExpressionText, TrainingResultLabels.ExpressionValue(result));
            SetText(completionExpressionNote, TrainingResultLabels.ExpressionNote(result));

            bool best = TrainingResultLabels.IsBest(result);
            var recordSprite = best ? recordBestSprite : recordPlainSprite;
            if (completionRecordCard != null && recordSprite != null) completionRecordCard.sprite = recordSprite;
            if (completionRecordTitle != null)
            {
                completionRecordTitle.text = TrainingResultLabels.LowerTitle;
                SetActiveSafe(completionRecordTitle.gameObject, !best);
            }
            if (completionRecordHeadline != null)
            {
                completionRecordHeadline.text = result.hasRecord ? TrainingResultLabels.LowerHeadline(result) : "-";
                SetActiveSafe(completionRecordHeadline.gameObject, !best);
            }
            SetText(completionRecordNote, TrainingResultLabels.RecordNote(result));
        }

        // 변경점: 완료 화면 등장 — 한 번만, 1초 남짓 안에 차례로: 전체 페이드 -> 제목 내려옴 -> 점수 판 톡 -> 별 하나씩
        //   -> 기록 판 올라옴 -> 색종이 톡톡 -> 아래 버튼 올라옴. 끝난 뒤 반복되는 움직임은 없다.
        private void PlayCompletionEntrance()
        {
            if (completionRoot == null || _completionShown) return;
            _completionShown = true;
            var root = completionRoot.transform;
            var stage = root.Find(EndingStage);
            var ct = this.GetCancellationTokenOnDestroy();

            UIFx.FadeGroup(UIFx.Group(root), 0f, 1f, 0f, 0.35f, ct).Forget();
            if (stage == null) return;
            MoveIn(stage.Find("Title"), new Vector2(0f, 24f), 0.1f, 0.4f, ct);
            MoveIn(stage.Find("Subtitle"), new Vector2(0f, 16f), 0.2f, 0.35f, ct);
            PopIn(stage.Find("ScoreCard"), 0.2f, 0.4f, ct);
            if (completionStars != null)
                for (int i = 0; i < completionStars.Length; i++)
                    if (completionStars[i] != null) PopIn(completionStars[i].transform, 0.5f + i * 0.08f, 0.3f, ct);
            for (int i = 0; i < EndingStatCards.Length; i++)
                MoveIn(stage.Find(EndingStatCards[i]), new Vector2(0f, -30f), 0.45f + i * 0.08f, 0.4f, ct);

            int confetti = 0;
            for (int i = 0; i < stage.childCount; i++)
            {
                var child = stage.GetChild(i);
                if (!child.name.StartsWith("Confetti", StringComparison.Ordinal)) continue;
                PopIn(child, 0.25f + confetti * 0.06f, 0.35f, ct);
                confetti++;
            }

            for (int i = 0; i < EndingButtons.Length; i++)
                MoveIn(stage.Find(EndingButtons[i]), new Vector2(0f, -36f), 0.75f + i * 0.08f, 0.4f, ct);
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

        private static void SetActiveSafe(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
