using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 부팅 스플래시 연출. 음악을 들려 주는 동안 totalSeconds(기본 6초) 머문 뒤 다음 씬으로 이동.
    //   0.0s~       검은 화면에서 페이드 인, 음악 재생
    //   0.2s~1.2s   로고가 살짝 커지며 나타남
    //   0.9s~1.7s   문구 나타남
    //   1.4s~2.0s   로딩 점 + "불러오는 중..." 나타남, 점은 dotStepSeconds마다 하나씩 켜짐
    //   끝 fadeOutSeconds 동안 화면 암전 + 음악 줄이기
    public class SplashSceneView : MonoBehaviour
    {
        [Header("연출 대상")]
        [SerializeField] private CanvasGroup logoGroup;
        [SerializeField] private RectTransform logoRect;
        [SerializeField] private CanvasGroup taglineGroup;
        [SerializeField] private CanvasGroup loadingGroup;
        [SerializeField] private GameObject[] dotOn;        // 켜진 점(46). 꺼진 점(47)은 아래에 항상 깔려 있다.
        [SerializeField] private Image fadeOverlay;         // 화면 전체 검은 막

        [Header("음악 (clip이 없으면 무음)")]
        [SerializeField] private AudioSource bgm;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.8f;

        [Header("시간")]
        [SerializeField] private float totalSeconds = 6f;   // 스플래시 전체 길이
        [SerializeField] private float fadeInSeconds = 0.6f;
        [SerializeField] private float fadeOutSeconds = 0.8f;
        [SerializeField] private float dotStepSeconds = 0.4f;
        [SerializeField] private string nextScene = "MainScene";

        const float LogoStart = 0.2f, LogoDur = 1.0f, LogoFromScale = 0.92f;
        const float TaglineStart = 0.9f, TaglineDur = 0.8f;
        const float LoadingStart = 1.4f, LoadingDur = 0.6f;

        private int _lastDot = -1;

        private void Start()
        {
            Apply(0f);
            if (bgm != null && bgm.clip != null)
            {
                bgm.volume = bgmVolume;
                bgm.Play();
            }
            Run(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid Run(CancellationToken ct)
        {
            float t = 0f;
            while (t < totalSeconds)
            {
                t += Time.deltaTime;
                Apply(t);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            Apply(totalSeconds);

            // 변경점: 스플래시는 이미 검은 화면으로 끝나므로 덮는 단계 없이 넘어가고, 다음 씬만 서서히 밝힌다.
            if (!string.IsNullOrEmpty(nextScene))
                SceneFader.LoadFromBlack(nextScene);
        }

        private void Apply(float t)
        {
            float fadeIn = fadeInSeconds > 0f ? 1f - Mathf.Clamp01(t / fadeInSeconds) : 0f;
            float fadeOut = fadeOutSeconds > 0f ? Mathf.Clamp01((t - (totalSeconds - fadeOutSeconds)) / fadeOutSeconds) : 0f;
            if (fadeOverlay != null)
            {
                var c = fadeOverlay.color;
                c.a = Mathf.Max(fadeIn, fadeOut);
                fadeOverlay.color = c;
            }
            if (bgm != null && bgm.clip != null) bgm.volume = bgmVolume * (1f - fadeOut);

            float logo = Ease((t - LogoStart) / LogoDur);
            if (logoGroup != null) logoGroup.alpha = logo;
            if (logoRect != null) logoRect.localScale = Vector3.one * Mathf.Lerp(LogoFromScale, 1f, logo);
            if (taglineGroup != null) taglineGroup.alpha = Ease((t - TaglineStart) / TaglineDur);
            if (loadingGroup != null) loadingGroup.alpha = Ease((t - LoadingStart) / LoadingDur);

            if (dotOn == null || dotOn.Length == 0) return;
            int dot = Mathf.FloorToInt(t / Mathf.Max(0.05f, dotStepSeconds)) % dotOn.Length;
            if (dot == _lastDot) return;
            _lastDot = dot;
            for (int i = 0; i < dotOn.Length; i++)
                if (dotOn[i] != null) dotOn[i].SetActive(i == dot);
        }

        // 0~1로 자른 뒤 부드럽게(smoothstep)
        private static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
