using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Artti.UI
{
    // 카드/버튼 Hover Glow. 평상시 은은한 후광(baseAlpha)이 깔려 URP Bloom과 어울려
    // "떠 있는" 입체감을 주고, 마우스 오버/터치 시 후광이 강해지고 살짝 커진다.
    // glow는 카드 뒤에 깔린 CardGlow 이미지를 가리킨다.
    [DisallowMultipleComponent]
    public class HomeButtonGlow : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Graphic glow;
        [SerializeField] private RectTransform scaleTarget;
        [SerializeField] private float baseAlpha = 0.16f;
        [SerializeField] private float hoverAlpha = 0.6f;
        [SerializeField] private float hoverScale = 1.03f;
        [SerializeField] private float pressScale = 0.985f;
        [SerializeField] private float speed = 10f;

        private float _targetAlpha;
        private float _targetScale = 1f;
        private Vector3 _baseScale = Vector3.one;
        private bool _hover, _press;

        public void Setup(Graphic glowGraphic, RectTransform target)
        {
            glow = glowGraphic;
            scaleTarget = target;
        }

        private void OnEnable()
        {
            if (scaleTarget != null) _baseScale = scaleTarget.localScale;
            _targetAlpha = baseAlpha;
            if (glow != null) { var c = glow.color; c.a = baseAlpha; glow.color = c; }
        }

        private void Update()
        {
            if (glow != null)
            {
                var c = glow.color;
                c.a = Mathf.MoveTowards(c.a, _targetAlpha, speed * Time.deltaTime);
                glow.color = c;
            }
            if (scaleTarget != null)
            {
                float cur = scaleTarget.localScale.x / Mathf.Max(_baseScale.x, 0.0001f);
                float k = Mathf.MoveTowards(cur, _targetScale, speed * Time.deltaTime);
                scaleTarget.localScale = _baseScale * k;
            }
        }

        private void Refresh()
        {
            _targetAlpha = _hover ? hoverAlpha : baseAlpha;
            _targetScale = _press ? pressScale : (_hover ? hoverScale : 1f);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; _press = false; Refresh(); }
        public void OnPointerDown(PointerEventData e) { _press = true; Refresh(); }
        public void OnPointerUp(PointerEventData e) { _press = false; Refresh(); }
    }
}
