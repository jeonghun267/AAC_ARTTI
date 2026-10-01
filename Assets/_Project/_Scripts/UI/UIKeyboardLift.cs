using UnityEngine;

namespace Artti.UI
{
    // 화면 자판(소프트 키보드)이 올라오면 판을 화면 맨 위로 올려 입력 칸과 버튼이 자판에 가리지 않게 하고,
    // 자판이 내려가면 제자리로 돌아온다. 뷰가 런타임에 붙인다(씬 수정 없음).
    // 위치(anchoredPosition)만 바꾸므로 UIPopupMotion의 크기 연출(가운데 피벗 판)과 겹치지 않는다.
    // 주의: MonoBehaviour는 클래스명과 같은 파일이어야 안드로이드 빌드에서 정상 직렬화된다.
    [DisallowMultipleComponent]
    public sealed class UIKeyboardLift : MonoBehaviour
    {
        const float TopMargin = 24f;   // 캔버스 단위: 화면 위 가장자리와 판 사이
        const float Seconds = 0.2f;

        private RectTransform _panel;
        private RectTransform _parent;
        private Vector2 _home;
        private float _k;

        public static void Attach(RectTransform panel)
        {
            if (panel == null || !(panel.parent is RectTransform parent)) return;
            if (!panel.TryGetComponent<UIKeyboardLift>(out var lift)) lift = panel.gameObject.AddComponent<UIKeyboardLift>();
            lift._panel = panel;
            lift._parent = parent;
            lift._home = panel.anchoredPosition;
        }

        private void OnDisable()
        {
            _k = 0f;
            if (_panel != null) _panel.anchoredPosition = _home;
        }

        private void LateUpdate()
        {
            if (_panel == null) return;
            float target = TouchScreenKeyboard.visible ? 1f : 0f;
            if (_k == target) return;
            _k = Mathf.MoveTowards(_k, target, Time.unscaledDeltaTime / Seconds);

            // 판 위 가장자리가 부모(화면 전체 막) 위 가장자리에서 TopMargin 아래에 오는 y
            float h = _parent.rect.height;
            float a = _panel.anchorMin.y;
            float lifted = (1f - a) * h - TopMargin - _panel.rect.height * (1f - _panel.pivot.y);
            float y = Mathf.Lerp(_home.y, Mathf.Max(_home.y, lifted), UIFx.EaseOutCubic(_k));
            _panel.anchoredPosition = new Vector2(_home.x, y);
        }
    }
}
