using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 프로필 만들기 배경: 파스텔 색 빛덩이들이 은은하게 호흡(밝기/크기)하며
    // 아주 살짝 떠다녀 따뜻하고 몽환적인 분위기를 만든다.
    // 빌더가 orbs 배열에 부드러운 글로우 Image들을 연결한다. (Random 미사용 -> 결정적)
    [DisallowMultipleComponent]
    public class PastelGlow : MonoBehaviour
    {
        [SerializeField] private Graphic[] orbs;

        [Header("호흡 (밝기)")]
        [SerializeField] private float minAlpha = 0.10f;
        [SerializeField] private float maxAlpha = 0.28f;
        [SerializeField] private float period = 5.5f;     // 한 호흡 초 (느리게)

        [Header("크기/표류")]
        [SerializeField] private float scaleAmount = 0.10f; // 호흡 시 커지는 비율
        [SerializeField] private float drift = 16f;         // 살짝 떠다니는 px

        private float[] _phase;
        private Vector2[] _basePos;
        private Vector3[] _baseScale;

        private void Awake()
        {
            int n = orbs != null ? orbs.Length : 0;
            _phase = new float[n];
            _basePos = new Vector2[n];
            _baseScale = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                // 황금각으로 위상을 흩뿌려 동시에 깜빡이지 않게
                _phase[i] = i * 2.39996f;
                if (orbs[i] == null) continue;
                var rt = orbs[i].rectTransform;
                _basePos[i] = rt.anchoredPosition;
                _baseScale[i] = rt.localScale;
            }
        }

        private void Update()
        {
            if (orbs == null) return;
            float w = period > 0.01f ? (2f * Mathf.PI / period) : 0f;
            for (int i = 0; i < orbs.Length; i++)
            {
                var g = orbs[i];
                if (g == null) continue;
                float t = Time.time * w + _phase[i];
                float s = (Mathf.Sin(t) + 1f) * 0.5f; // 0..1

                var c = g.color;
                c.a = Mathf.Lerp(minAlpha, maxAlpha, s);
                g.color = c;

                var rt = g.rectTransform;
                rt.localScale = _baseScale[i] * (1f + scaleAmount * (s - 0.5f) * 2f);
                rt.anchoredPosition = _basePos[i] + new Vector2(
                    Mathf.Sin(t * 0.5f) * drift,
                    Mathf.Cos(t * 0.4f) * drift);
            }
        }
    }
}
