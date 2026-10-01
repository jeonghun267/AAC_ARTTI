using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Artti.UI
{
    // 카드를 원통 둘레에 놓고 밖에서 보는 3D 회전 카드 줄(회전목마). 사용자 선택 화면에서 사용자가 많을 때(카드 4장 이상) 쓴다.
    //   - 앞(가운데) 카드가 가장 크고, 옆으로 갈수록 원근으로 작아지며 가운데 쪽 변이 긴 사다리꼴이 된다(UITiltEffect).
    //   - 더 돌면 뒤로 넘어가 흐려지고, 반대편에서 다시 나온다(끝없이 돈다). 끝에서 잘려 숨지 않는다.
    //   - 끌면 원통이 손가락을 따라 돌고, 놓으면 가장 가까운 카드가 앞에 와서 멈춘다. 앞에 멈춘 카드를 알린다(onFrontChanged).
    //   - 카드를 탭하면 그 카드가 앞으로 돌아온다(RotateTo).
    // Overlay 캔버스는 3D 회전에 원근이 생기지 않아, 위치/배율/사다리꼴을 직접 계산한다.
    // 카드마다 Slot(위치·배율·알파·앞뒤 순서)을 만들어 카드를 그 안에 넣는다. 카드 자체의 배율(선택 시 떠오름, 등장 연출)은 그대로 쓴다.
    // 주의: MonoBehaviour는 클래스명과 같은 파일이어야 안드로이드 빌드에서 정상 직렬화된다.
    public class CylinderCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float CardGap = 60f;                         // 앞 카드와 옆 카드 사이(원통 둘레 기준)
        const float WidthSoften = 0.3f;                    // 옆 카드가 납작해지는 정도를 이만큼 덜어 이름이 읽히게(0 = 실제 원근)
        const float MinStep = 36f, MaxStep = 50f;          // 카드 사이 각도(도). 카드가 적으면 넓게
        const float CameraDistance = 900f;                 // 원통 앞면에서 눈까지 거리(작을수록 원근이 셈)
        const float FadeFrom = 55f, FadeTo = 95f;          // 이 각도(도)부터 흐려져 뒤로 넘어가면 사라짐
        const float RaycastMinAlpha = 0.3f;
        const float SnapSharpness = 12f;                   // 놓은 뒤 앞 카드로 맞춰 들어가는 빠르기
        const float FlickSeconds = 0.25f;                  // 놓을 때 속도로 이만큼 더 돈 자리에서 멈춘다
        static readonly string[] TiltTargets = { "SelectedBorder", "Frame", "AvatarMask", "CheckMark" };

        private class Item
        {
            public RectTransform card;
            public RectTransform slot;
            public CanvasGroup group;
            public UITiltEffect[] tilts;
            public float depth;   // 앞뒤 정렬용 (클수록 앞)
        }

        private RectTransform _viewport;
        private RectTransform _content;
        private readonly List<Item> _items = new List<Item>();
        private float[] _sortKeys = new float[0];
        private int[] _order = new int[0];
        private int[] _lastOrder = new int[0];

        private float _step;        // 카드 사이 각도(도)
        private float _span;        // 한 바퀴로 쓰는 각도 = 카드 수 x step
        private float _radius;
        private float _cardW = 180f;
        private float _angle;       // 지금 앞에 있는 각도(도). 카드 i의 자리 = i * step
        private float _target;
        private bool _snapping;
        private bool _dragging;
        private float _velocity;    // 도/초
        private bool _dirty = true;
        private int _front = -1;
        private Action<int> _onFrontChanged;

        public int Count => _items.Count;

        // 뷰포트에 붙이고, content 안의 카드들을 원통에 놓는다. 가로 레이아웃/스크롤은 끈다.
        public static CylinderCarousel Attach(RectTransform viewport, RectTransform content, IList<RectTransform> cards,
            int frontIndex, Action<int> onFrontChanged)
        {
            if (viewport == null || content == null || cards == null || cards.Count == 0) return null;
            if (!viewport.TryGetComponent<CylinderCarousel>(out var c)) c = viewport.gameObject.AddComponent<CylinderCarousel>();
            c.Setup(viewport, content, cards, frontIndex, onFrontChanged);
            return c;
        }

        private void Setup(RectTransform viewport, RectTransform content, IList<RectTransform> cards, int frontIndex, Action<int> onFrontChanged)
        {
            _viewport = viewport;
            _content = content;
            _onFrontChanged = onFrontChanged;

            if (viewport.TryGetComponent<ScrollRect>(out var scroll)) scroll.enabled = false;
            if (content.TryGetComponent<HorizontalLayoutGroup>(out var hlg)) hlg.enabled = false;
            if (content.TryGetComponent<ContentSizeFitter>(out var fitter)) fitter.enabled = false;
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            _items.Clear();
            var tilts = new List<UITiltEffect>();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                _cardW = card.rect.width > 1f ? card.rect.width : _cardW;

                var slot = new GameObject("Slot_" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                slot.SetParent(content, false);
                slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = card.rect.size;
                card.SetParent(slot, false);
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
                // 피벗이 가운데가 아닌 카드(선택 시 떠오르는 카드)도 카드 가운데가 Slot 가운데에 오게
                card.anchoredPosition = -Vector2.Scale(new Vector2(0.5f, 0.5f) - card.pivot, card.rect.size);

                tilts.Clear();
                foreach (var name in TiltTargets)
                {
                    var t = card.Find(name);
                    if (t == null || !t.TryGetComponent<Graphic>(out _)) continue;
                    if (!t.TryGetComponent<UITiltEffect>(out var fx)) fx = t.gameObject.AddComponent<UITiltEffect>();
                    fx.Setup(card);
                    tilts.Add(fx);
                }
                _items.Add(new Item { card = card, slot = slot, group = slot.gameObject.AddComponent<CanvasGroup>(), tilts = tilts.ToArray() });
            }

            int n = _items.Count;
            _step = Mathf.Clamp(360f / n, MinStep, MaxStep);
            _span = _step * n;
            _radius = (_cardW + CardGap) / (2f * Mathf.Sin(_step * 0.5f * Mathf.Deg2Rad));
            _sortKeys = new float[n];
            _order = new int[n];
            _lastOrder = new int[n];
            for (int i = 0; i < n; i++) _lastOrder[i] = -1;

            _angle = _target = Mathf.Clamp(frontIndex, 0, n - 1) * _step;
            _snapping = false;
            _front = -1;
            _dirty = true;
            ApplyNow();
            NotifyFront();
        }

        // 카드 i를 앞으로 돌린다(가까운 쪽으로)
        public void RotateTo(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            float baseAngle = index * _step;
            float k = Mathf.Round((_angle - baseAngle) / _span);
            _target = baseAngle + k * _span;
            _snapping = true;
            _velocity = 0f;
        }

        // 에디터 캡처용: 앞 각도를 바로 바꿔 그린다(실행 중이 아니면 LateUpdate가 돌지 않음)
        public void PreviewAngle(float extraDegrees)
        {
            _angle = _target + extraDegrees;
            _dirty = true;
            ApplyNow();
        }

        private void LateUpdate()
        {
            if (_items.Count == 0) return;
            if (!_dragging && _snapping)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                _angle = Mathf.Lerp(_angle, _target, 1f - Mathf.Exp(-SnapSharpness * dt));
                if (Mathf.Abs(_angle - _target) < 0.05f)
                {
                    _angle = _target;
                    _snapping = false;
                    NotifyFront();
                }
                _dirty = true;
            }
            if (_dirty) ApplyNow();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = true;
            _snapping = false;
            _velocity = 0f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!TryLocalX(e.position, e.pressEventCamera, out float x) ||
                !TryLocalX(e.position - e.delta, e.pressEventCamera, out float prevX)) return;
            // 앞 카드가 손가락을 따라가게: 앞면 둘레 길이 = 반지름 x 라디안
            float deg = -(x - prevX) / _radius * Mathf.Rad2Deg;
            _angle += deg;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-3f);
            _velocity = Mathf.Lerp(_velocity, deg / dt, 0.5f);
            _dirty = true;
        }

        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;
            _target = Mathf.Round((_angle + _velocity * FlickSeconds) / _step) * _step;
            _snapping = true;
        }

        private bool TryLocalX(Vector2 screen, Camera cam, out float x)
        {
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screen, cam, out var local);
            x = local.x;
            return ok;
        }

        private void NotifyFront()
        {
            int n = _items.Count;
            if (n == 0) return;
            int front = Mathf.RoundToInt(Mathf.Repeat(_angle, _span) / _step) % n;
            if (front == _front) return;
            _front = front;
            _onFrontChanged?.Invoke(front);
        }

        // 원근 배율: 앞면(각도 0) = 1, 옆/뒤로 갈수록 작아짐
        private float Persp(float rad) => CameraDistance / (CameraDistance + _radius * (1f - Mathf.Cos(rad)));
        private float ScreenX(float rad) => _radius * Mathf.Sin(rad) * Persp(rad);

        public void ApplyNow()
        {
            _dirty = false;
            int n = _items.Count;
            float halfCard = (_cardW * 0.5f) / _radius;   // 카드 반폭이 원통에서 차지하는 각도(라디안)
            for (int i = 0; i < n; i++)
            {
                var it = _items[i];
                if (it.slot == null) continue;
                float deg = Mathf.Repeat(i * _step - _angle + _span * 0.5f, _span) - _span * 0.5f;
                float rad = deg * Mathf.Deg2Rad;
                float lRad = rad - halfCard, rRad = rad + halfCard;

                float xl = ScreenX(lRad), xr = ScreenX(rRad);
                float sl = Persp(lRad), sr = Persp(rRad);
                float sy = Mathf.Min(sl, sr);
                float sx = Mathf.Max(0.02f, Mathf.Lerp((xr - xl) / _cardW, sy, WidthSoften));
                it.slot.anchoredPosition = new Vector2((xl + xr) * 0.5f, 0f);
                it.slot.localScale = new Vector3(sx, sy, 1f);

                // 앞쪽(긴) 변: 왼쪽이 앞이면 +, 오른쪽이 앞이면 -
                float tilt = sl >= sr ? sl / sr - 1f : -(sr / sl - 1f);
                tilt = Mathf.Clamp(tilt, -0.3f, 0.3f);
                for (int t = 0; t < it.tilts.Length; t++) it.tilts[t].SetAmount(tilt);

                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FadeFrom, FadeTo, Mathf.Abs(deg)));
                it.group.alpha = a;
                it.group.blocksRaycasts = a >= RaycastMinAlpha;

                it.depth = Mathf.Cos(rad);
                _sortKeys[i] = it.depth;
                _order[i] = i;
            }

            // 뒤 카드부터 그리게(앞 카드가 맨 위). 순서가 바뀔 때만 형제 순서를 바꾼다.
            Array.Sort(_sortKeys, _order);
            for (int k = 0; k < n; k++)
            {
                if (_order[k] == _lastOrder[k]) continue;
                for (int j = 0; j < n; j++)
                {
                    _items[_order[j]].slot.SetSiblingIndex(j);
                    _lastOrder[j] = _order[j];
                }
                break;
            }
        }
    }
}
