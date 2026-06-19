using UnityEngine;

namespace Artti.UI
{
    // CanvasGroup의 알파(와 선택적 스케일)를 사인파로 은은하게 펄스시킨다.
    // 약국 십자가 펄스, 음식점 따뜻한 조명에 공용으로 사용.
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public class GlowPulse : MonoBehaviour
    {
        [SerializeField] private float minAlpha = 0.35f;
        [SerializeField] private float maxAlpha = 0.9f;
        [SerializeField] private float period = 2.4f;      // 한 사이클 초
        [SerializeField] private float scaleAmount = 0f;   // 0이면 스케일 변화 없음
        [SerializeField] private float phase = 0f;

        private CanvasGroup _cg;
        private RectTransform _rt;
        private Vector3 _baseScale;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _rt = (RectTransform)transform;
            _baseScale = _rt.localScale;
        }

        private void Update()
        {
            float w = period > 0.01f ? (2f * Mathf.PI / period) : 0f;
            float s = (Mathf.Sin(Time.time * w + phase) + 1f) * 0.5f; // 0..1
            _cg.alpha = Mathf.Lerp(minAlpha, maxAlpha, s);
            if (scaleAmount > 0f)
                _rt.localScale = _baseScale * (1f + scaleAmount * (s - 0.5f) * 2f);
        }
    }
}
