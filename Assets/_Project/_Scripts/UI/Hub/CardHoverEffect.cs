using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Artti.UI
{
    // 시나리오 카드에 마우스를 올리면 살짝 커지고 위로 떠오르며 그림자가 강해진다.
    // 빌더가 카드 root에 부착하고, root에 투명 raycast 카처 Image와 shadow 참조를 함께 세팅한다.
    // (참조는 OnEnable에서만 캐싱, Update에서는 보간만 수행)
    [DisallowMultipleComponent]
    public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("떠오름")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float hoverLift = 14f;     // 위로 떠오르는 px
        [SerializeField] private float lerpSpeed = 12f;     // 클수록 빠르게 따라붙음

        [Header("그림자 강화")]
        [SerializeField] private Image shadow;              // DressGlass가 만든 Shadow Image
        [SerializeField] private float shadowRestAlpha = 0.16f;
        [SerializeField] private float shadowHoverAlpha = 0.34f;

        [Header("환경 효과 (hover 중에만 표시)")]
        [SerializeField] private CanvasGroup ambience;      // 업종별 효과 컨테이너. 평소 alpha 0
        [SerializeField] private float ambienceFadeSpeed = 9f;

        private RectTransform _rt;
        private Vector2 _restPos;
        private bool _hovered;

        private void OnEnable()
        {
            _rt = (RectTransform)transform;
            _restPos = _rt.anchoredPosition;
            _hovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        private void Update()
        {
            // 프레임레이트 독립적인 지수 보간
            float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);

            float targetScale = _hovered ? hoverScale : 1f;
            Vector2 targetPos = _hovered ? _restPos + new Vector2(0f, hoverLift) : _restPos;

            _rt.localScale = Vector3.Lerp(_rt.localScale, Vector3.one * targetScale, t);
            _rt.anchoredPosition = Vector2.Lerp(_rt.anchoredPosition, targetPos, t);

            if (shadow != null)
            {
                var c = shadow.color;
                c.a = Mathf.Lerp(c.a, _hovered ? shadowHoverAlpha : shadowRestAlpha, t);
                shadow.color = c;
            }

            if (ambience != null)
            {
                float at = 1f - Mathf.Exp(-ambienceFadeSpeed * Time.deltaTime);
                ambience.alpha = Mathf.Lerp(ambience.alpha, _hovered ? 1f : 0f, at);
            }
        }
    }
}
