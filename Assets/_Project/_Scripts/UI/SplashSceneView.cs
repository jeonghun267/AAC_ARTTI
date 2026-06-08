using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // SplashScene 인트로: 51.png 상태 → 52.png 상태로 전환.
    // 씬 정지(에디터) 배치값이 52(최종) 상태. 런타임에 51 상태로 되돌렸다가 스르르 이동.
    public class SplashSceneView : MonoBehaviour
    {
        [Header("Animated Targets (정지 배치 = 52 상태)")]
        [SerializeField] private RectTransform titleGroup;
        [SerializeField] private RectTransform teamLabel;
        [SerializeField] private CanvasGroup nameChipGroup;
        [SerializeField] private CanvasGroup startButtonGroup;

        [Header("Bindings")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button nameChipButton;
        [SerializeField] private Button startButton;
        [SerializeField] private string defaultName = "김연영";
        // 전환 대상은 다음 단계에서 와이어링 (이름칩 → 프로필 생성/선택, 시작하기 → 선택 씬)
        [SerializeField] private string nameChipTargetScene = "";
        [SerializeField] private string startTargetScene = "";

        [Header("Intro Animation")]
        [SerializeField] private float titleStartY = 55f;   // 51 상태 타이틀 그룹 Y
        [SerializeField] private float teamStartY = -260f;  // 51 상태 team ARTTI Y
        [SerializeField] private float chipSlideUp = 70f;   // 칩/버튼이 아래에서 올라오는 거리
        [SerializeField] private float holdSeconds = 0.7f;  // 51 상태 유지 시간
        [SerializeField] private float moveSeconds = 0.8f;  // 전환 길이 (hold+move ≈ 1.5초)

        private RectTransform _chipRect;
        private RectTransform _btnRect;

        private void Start()
        {
            _chipRect = nameChipGroup != null ? nameChipGroup.transform as RectTransform : null;
            _btnRect = startButtonGroup != null ? startButtonGroup.transform as RectTransform : null;

            BindName();
            if (nameChipButton != null) nameChipButton.onClick.AddListener(OnNameChip);
            if (startButton != null) startButton.onClick.AddListener(OnStart);

            PlayIntro(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void BindName()
        {
            var nick = AppBootstrap.Instance?.ProfileManager?.ActiveProfile?.nickname;
            if (string.IsNullOrEmpty(nick)) nick = defaultName;
            if (nameText != null) nameText.text = nick;
        }

        private async UniTaskVoid PlayIntro(CancellationToken ct)
        {
            // 52(종료) 상태 = 현재 배치값
            Vector2 titleEnd = titleGroup != null ? titleGroup.anchoredPosition : Vector2.zero;
            Vector2 teamEnd = teamLabel != null ? teamLabel.anchoredPosition : Vector2.zero;
            Vector2 chipEnd = _chipRect != null ? _chipRect.anchoredPosition : Vector2.zero;
            Vector2 btnEnd = _btnRect != null ? _btnRect.anchoredPosition : Vector2.zero;

            // 51(시작) 상태
            Vector2 titleStart = new Vector2(titleEnd.x, titleStartY);
            Vector2 teamStart = new Vector2(teamEnd.x, teamStartY);
            Vector2 chipStart = chipEnd + new Vector2(0f, -chipSlideUp);
            Vector2 btnStart = btnEnd + new Vector2(0f, -chipSlideUp);

            Apply(0f, titleStart, titleEnd, teamStart, teamEnd, chipStart, chipEnd, btnStart, btnEnd);

            if (holdSeconds > 0f)
                await UniTask.Delay((int)(holdSeconds * 1000f), cancellationToken: ct);

            float t = 0f;
            float dur = Mathf.Max(0.0001f, moveSeconds);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                Apply(Smooth(Mathf.Clamp01(t)), titleStart, titleEnd, teamStart, teamEnd, chipStart, chipEnd, btnStart, btnEnd);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            Apply(1f, titleStart, titleEnd, teamStart, teamEnd, chipStart, chipEnd, btnStart, btnEnd);
        }

        private void Apply(float e, Vector2 ts, Vector2 te, Vector2 mas, Vector2 mae, Vector2 cs, Vector2 ce, Vector2 bs, Vector2 be)
        {
            if (titleGroup != null) titleGroup.anchoredPosition = Vector2.LerpUnclamped(ts, te, e);
            if (teamLabel != null) teamLabel.anchoredPosition = Vector2.LerpUnclamped(mas, mae, e);
            if (_chipRect != null)
            {
                _chipRect.anchoredPosition = Vector2.LerpUnclamped(cs, ce, e);
                if (nameChipGroup != null) nameChipGroup.alpha = e;
            }
            if (_btnRect != null)
            {
                _btnRect.anchoredPosition = Vector2.LerpUnclamped(bs, be, e);
                if (startButtonGroup != null) startButtonGroup.alpha = e;
            }
        }

        // smoothstep 이징
        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private void OnNameChip()
        {
            if (!string.IsNullOrEmpty(nameChipTargetScene)) SceneManager.LoadScene(nameChipTargetScene);
            else Debug.Log("[SplashSceneView] 이름칩 클릭 — 전환 대상 미설정 (프로필 생성/선택 씬 연결 예정)");
        }

        private void OnStart()
        {
            if (!string.IsNullOrEmpty(startTargetScene)) SceneManager.LoadScene(startTargetScene);
            else Debug.Log("[SplashSceneView] 시작하기 클릭 — 전환 대상 미설정 (선택 씬 연결 예정)");
        }
    }
}
