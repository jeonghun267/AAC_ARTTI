using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Artti.UI
{
    // 누르는 동안 살짝 작아졌다가 떼면 제자리로 톡 돌아오는 탭 피드백. 뷰가 버튼에 런타임으로 붙인다(씬 수정 없음).
    // 피벗이 모서리에 있는 버튼도 가운데를 기준으로 줄어든다. 레이아웃 그룹 자식은 위치를 건드리지 않고 크기만 바꾼다.
    // 연출 중일 때만 크기를 쓰고 평소에는 아무것도 하지 않는다(Update 없음).
    // 주의: MonoBehaviour는 클래스명과 같은 파일이어야 안드로이드 빌드에서 정상 직렬화된다.
    [DisallowMultipleComponent]
    public sealed class UIPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const float PressedScale = 0.94f;
        const float DownSeconds = 0.08f;
        const float UpSeconds = 0.22f;
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private RectTransform _target;
        private Selectable _selectable;
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _home;
        private bool _aroundCenter;
        private bool _pressed;
        private float _k = 1f;
        private CancellationTokenSource _cts;

        // target을 비우면 버튼 자신이 줄어든다(투명한 누름 영역 버튼이면 보이는 그림을 target으로 준다).
        public static void Attach(Selectable button, RectTransform target = null)
        {
            if (button == null) return;
            if (!button.TryGetComponent<UIPressScale>(out var p)) p = button.gameObject.AddComponent<UIPressScale>();
            p._selectable = button;
            p._target = target != null ? target : (RectTransform)button.transform;
            p._baseScale = p._target.localScale;
            p._home = p._target.anchoredPosition;
            p._aroundCenter = p._target.pivot != Center
                              && !(p._target.parent != null && p._target.parent.TryGetComponent<LayoutGroup>(out _));
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_target == null || (_selectable != null && !_selectable.IsInteractable())) return;
            _pressed = true;
            AnimateTo(PressedScale, DownSeconds, false);
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) => Release();

        private void Release()
        {
            if (!_pressed) return;
            _pressed = false;
            AnimateTo(1f, UpSeconds, true);
        }

        private void AnimateTo(float target, float seconds, bool back)
        {
            var ct = UIFx.Restart(ref _cts, this);
            float from = _k;
            UIFx.Tween(this, 0f, seconds, k =>
            {
                float e = back ? UIFx.EaseOutBack(k) : UIFx.EaseOutCubic(k);
                Apply(Mathf.LerpUnclamped(from, target, e));
            }, ct).Forget();
        }

        private void Apply(float k)
        {
            _k = k;
            if (_target == null) return;
            if (_aroundCenter) UIFx.SetScaleAround(_target, _home, Center, k);   // 모서리 피벗 버튼(크기 1)은 가운데 기준
            else _target.localScale = _baseScale * k;
        }

        // 누른 채로 버튼이 꺼지면 작아진 채 남지 않게 되돌린다.
        private void OnDisable()
        {
            UIFx.Cancel(ref _cts);
            _pressed = false;
            if (_k != 1f) Apply(1f);
        }
    }
}
