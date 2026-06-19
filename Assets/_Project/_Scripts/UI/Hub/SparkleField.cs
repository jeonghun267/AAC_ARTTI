using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 편의점 카드: 진열된 상품이 반짝이는 효과.
    // 각 반짝임 점이 서로 다른 위상으로 잠깐 밝아졌다 사라지며 살짝 커진다.
    // 빌더가 sparkles 배열에 데코 Graphic들을 연결한다.
    [DisallowMultipleComponent]
    public class SparkleField : MonoBehaviour
    {
        [SerializeField] private Graphic[] sparkles;
        [SerializeField] private float period = 1.6f;
        [SerializeField] private float maxAlpha = 0.95f;
        [SerializeField] private float popScale = 0.5f;    // 반짝일 때 커지는 비율

        private float[] _phases;
        private Vector3[] _baseScale;

        private void Awake()
        {
            int n = sparkles != null ? sparkles.Length : 0;
            _phases = new float[n];
            _baseScale = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                // 황금각으로 위상을 흩뿌려 동시에 깜빡이지 않게 (Random 미사용 → 결정적)
                _phases[i] = i * 2.39996f;
                if (sparkles[i] != null) _baseScale[i] = sparkles[i].rectTransform.localScale;
            }
        }

        private void Update()
        {
            if (sparkles == null) return;
            float w = period > 0.01f ? (2f * Mathf.PI / period) : 0f;
            for (int i = 0; i < sparkles.Length; i++)
            {
                var g = sparkles[i];
                if (g == null) continue;
                float s = (Mathf.Sin(Time.time * w + _phases[i]) + 1f) * 0.5f;
                float tw = s * s * s; // 뾰족하게: 짧은 순간만 반짝
                var c = g.color; c.a = maxAlpha * tw; g.color = c;
                g.rectTransform.localScale = _baseScale[i] * (1f + popScale * tw);
            }
        }
    }
}
