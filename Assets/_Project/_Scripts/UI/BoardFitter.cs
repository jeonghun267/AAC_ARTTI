using UnityEngine;

namespace Artti.UI
{
    // 그림이 그려진 보드(디자인 크기 designSize)를 부모(화면)에 맞춘다. 가로세로 같은 배율이라 그림이 찌그러지지 않는다.
    //   기본은 화면을 빈틈없이 덮고(cover), 그러면 safeRect(꼭 보여야 하는 영역)가 화면 밖으로 잘릴 때만 그만큼 줄인다.
    //   줄어서 남는 화면 가장자리는 부모 뒤의 다른 배경이 채운다(보드 그림 가장자리를 투명하게 녹여 이음매를 숨긴다).
    //   예) AR 결과 화면: 3:2 보드가 16:10 태블릿은 덮고, 20:9 폰에서는 위아래 제목/버튼이 안 잘리게 줄어 좌우에 배경이 보인다.
    // 앵커를 부모 전체로 늘려 두어(CoverImageFitter와 같은 방식) 부모 크기가 바뀌면 OnRectTransformDimensionsChange로 다시 맞춘다.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class BoardFitter : MonoBehaviour
    {
        [SerializeField] private Vector2 designSize = new Vector2(1536f, 1024f);
        // 보드 좌상단 기준(px, y 아래가 +)
        [SerializeField] private Rect safeRect;
        [SerializeField] private float safeMargin = 4f;
        // 변경점: 켜면 덮기(cover)와 상관없이 safeRect가 화면(여백 safeMargin)에 꽉 차게 맞춘다.
        //   보드 그림 가장자리는 화면 밖으로 잘려도 되고, 그림 속 틀을 화면에 크게 보이고 싶을 때 쓴다(AR 도움 화면).
        [SerializeField] private bool fillSafeRect;

        private RectTransform _rt;
        private DrivenRectTransformTracker _tracker;

        public void Setup(Vector2 size, Rect safe, float margin, bool fillSafe = false)
        {
            designSize = size;
            safeRect = safe;
            safeMargin = margin;
            fillSafeRect = fillSafe;
            UpdateRect();
        }

        private void OnEnable() => UpdateRect();
        private void Start() => UpdateRect();
        private void OnDisable() => _tracker.Clear();
        private void OnRectTransformDimensionsChange() => UpdateRect();

#if UNITY_EDITOR
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) UpdateRect(); };
#endif

        private void UpdateRect()
        {
            if (!isActiveAndEnabled || designSize.x <= 0f || designSize.y <= 0f) return;
            if (_rt == null) _rt = (RectTransform)transform;
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            Vector2 p = parent.rect.size;
            if (p.x <= 0f || p.y <= 0f) return;

            _tracker.Clear();
            _tracker.Add(this, _rt, DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot |
                                    DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta |
                                    DrivenTransformProperties.Scale);
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.pivot = new Vector2(0.5f, 0.5f);

            float s = Mathf.Max(p.x / designSize.x, p.y / designSize.y);
            Vector2 pos = Vector2.zero;
            if (safeRect.width > 0f && safeRect.height > 0f)
            {
                float fit = Mathf.Min((p.x - 2f * safeMargin) / safeRect.width, (p.y - 2f * safeMargin) / safeRect.height);
                s = fillSafeRect ? fit : Mathf.Min(s, fit);
                // safeRect 가운데를 화면 가운데로, 단 보드가 화면을 덮는 방향으로는 보드 끝이 화면 안으로 들어오지 않게
                var local = new Vector2(safeRect.center.x - designSize.x * 0.5f, designSize.y * 0.5f - safeRect.center.y);
                pos = -local * s;
                float mx = Mathf.Max(0f, (designSize.x * s - p.x) * 0.5f);
                float my = Mathf.Max(0f, (designSize.y * s - p.y) * 0.5f);
                pos = new Vector2(Mathf.Clamp(pos.x, -mx, mx), Mathf.Clamp(pos.y, -my, my));
            }

            // 크기는 designSize 그대로(늘린 앵커라 sizeDelta = designSize - 부모 크기), 화면에 맞추는 건 scale로
            var delta = designSize - p;
            if ((_rt.sizeDelta - delta).sqrMagnitude > 0.0001f) _rt.sizeDelta = delta;
            if ((_rt.anchoredPosition - pos).sqrMagnitude > 0.0001f) _rt.anchoredPosition = pos;
            var scale = new Vector3(s, s, 1f);
            if ((_rt.localScale - scale).sqrMagnitude > 0.0000001f) _rt.localScale = scale;
        }
    }
}
