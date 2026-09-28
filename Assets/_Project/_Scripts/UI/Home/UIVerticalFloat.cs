using UnityEngine;

namespace Artti.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIVerticalFloat : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float amplitude = 10f;
        [SerializeField, Min(0.01f)] private float duration = 4f;
        [SerializeField, Range(0f, 1f)] private float phase;

        private RectTransform _rectTransform;
        private Vector2 _basePosition;
        private bool _hasBasePosition;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            _basePosition = _rectTransform.anchoredPosition;
            _hasBasePosition = true;
        }

        private void Update()
        {
            float cycle = (Time.unscaledTime / Mathf.Max(0.01f, duration) + phase) * Mathf.PI * 2f;
            _rectTransform.anchoredPosition = _basePosition + Vector2.up * (Mathf.Sin(cycle) * amplitude);
        }

        private void OnDisable()
        {
            if (_hasBasePosition && _rectTransform != null)
                _rectTransform.anchoredPosition = _basePosition;
        }

        private void OnValidate()
        {
            amplitude = Mathf.Max(0f, amplitude);
            duration = Mathf.Max(0.01f, duration);
            phase = Mathf.Repeat(phase, 1f);
        }
    }
}
