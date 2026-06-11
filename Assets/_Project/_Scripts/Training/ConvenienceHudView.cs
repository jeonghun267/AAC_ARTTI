using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.Training
{
    // 편의점 훈련 씬 HUD (시안 1~10.png). 표시와 입력만 담당, 흐름 제어는 TrainingSceneRoot.
    public class ConvenienceHudView : MonoBehaviour
    {
        [Header("진행 표시 (objective 기준 5단계)")]
        [SerializeField] private Image[] stepDots;
        [SerializeField] private TMP_Text stepLabel;

        [Header("일시정지")]
        [SerializeField] private Button pauseBtn;
        [SerializeField] private GameObject pauseModal;
        [SerializeField] private Button pauseConfirmBtn; // 종료
        [SerializeField] private Button pauseCancelBtn;  // 취소

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
        [SerializeField] private Button retryBtn;
        [SerializeField] private Button hubBtn;
        [SerializeField] private Button homeBtn;
        [SerializeField] private RectTransform confettiRoot;

        public event Action OnExitRequested;
        public event Action OnReplayRequested;
        public event Action OnRetrySession;
        public event Action OnGoHub;
        public event Action OnGoHome;

        private static readonly Color DotDone      = new Color32(26, 86, 219, 255);
        private static readonly Color DotCurrent   = new Color32(255, 138, 61, 255); // accent 주황
        private static readonly Color DotPending   = new Color32(210, 216, 226, 255);
        private static readonly Color SpeakerIdle  = new Color32(110, 118, 135, 255);
        private static readonly Color SpeakerActive = new Color32(26, 86, 219, 255);

        private static readonly string[] PraisePool = { "완벽해요!", "잘했어요!", "최고예요!", "멋져요!" };

        private Coroutine _praiseRoutine;
        private float _dotsTime;
        private bool _thinkingAnim;

        private void Awake()
        {
            if (pauseBtn != null) pauseBtn.onClick.AddListener(() => SetActiveSafe(pauseModal, true));
            if (pauseCancelBtn != null) pauseCancelBtn.onClick.AddListener(() => SetActiveSafe(pauseModal, false));
            if (pauseConfirmBtn != null) pauseConfirmBtn.onClick.AddListener(() => OnExitRequested?.Invoke());
            if (speakerBtn != null) speakerBtn.onClick.AddListener(() => OnReplayRequested?.Invoke());
            if (retryBtn != null) retryBtn.onClick.AddListener(() => OnRetrySession?.Invoke());
            if (hubBtn != null) hubBtn.onClick.AddListener(() => OnGoHub?.Invoke());
            if (homeBtn != null) homeBtn.onClick.AddListener(() => OnGoHome?.Invoke());

            SetActiveSafe(pauseModal, false);
            SetActiveSafe(completionRoot, false);
            SetActiveSafe(thinkingRoot, false);
            SetActiveSafe(userBubble, false);
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
            if (stepDots != null)
            {
                for (int i = 0; i < stepDots.Length; i++)
                {
                    if (stepDots[i] == null) continue;
                    stepDots[i].color = i < index ? DotDone : (i == index ? DotCurrent : DotPending);
                }
            }
            if (stepLabel != null) stepLabel.text = label;
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
            SetActiveSafe(thinkingRoot, false);
            SetActiveSafe(completionRoot, true);
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
