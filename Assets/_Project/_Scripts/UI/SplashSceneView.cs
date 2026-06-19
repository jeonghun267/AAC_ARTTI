using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Artti.UI
{
    // 부팅 로딩 스플래시(100.png). 진행바(둥근 사각)의 폭을 0→최대로 늘리고 퍼센트를 갱신한 뒤 다음 씬으로 이동.
    // 폭만 늘리므로 양끝이 항상 둥글다(Filled 방식의 직각 잘림 없음).
    public class SplashSceneView : MonoBehaviour
    {
        [Header("Loading Bar")]
        [SerializeField] private RectTransform progressFill; // 진행바 채움(둥근 사각). 폭을 애니메이션.
        [SerializeField] private float fillMaxWidth = 940f;  // 100%일 때 폭 = 트랙 폭
        [SerializeField] private TMP_Text percentText;       // "68%"
        [SerializeField] private TMP_Text loadingText;       // "로딩 중입니다..."

        [Header("Behaviour")]
        [SerializeField] private string nextScene = "ProfileSelectScene";
        [SerializeField] private float loadSeconds = 2.4f;   // 0 → 100% 시간
        [SerializeField] private float holdSeconds = 0.35f;  // 100% 후 잠깐 멈춤

        private void Start()
        {
            SetFill(0f);
            if (percentText != null) percentText.text = "0%";
            Run(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid Run(CancellationToken ct)
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, loadSeconds);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float p = Mathf.Clamp01(Smooth(Mathf.Clamp01(t)));
                SetFill(p);
                if (percentText != null) percentText.text = Mathf.RoundToInt(p * 100f) + "%";
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            SetFill(1f);
            if (percentText != null) percentText.text = "100%";

            if (holdSeconds > 0f)
                await UniTask.Delay((int)(holdSeconds * 1000f), cancellationToken: ct);

            if (!string.IsNullOrEmpty(nextScene))
                SceneManager.LoadScene(nextScene);
        }

        private void SetFill(float p)
        {
            if (progressFill == null) return;
            var s = progressFill.sizeDelta;
            progressFill.sizeDelta = new Vector2(fillMaxWidth * Mathf.Clamp01(p), s.y);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
