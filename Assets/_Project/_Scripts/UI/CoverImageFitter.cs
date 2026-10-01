using UnityEngine;

namespace Artti.UI
{
    // 배경 이미지를 부모(캔버스)에 빈틈 없이 꽉 채운다(Envelope와 같은 cover).
    // 글자가 그려진 배경은 화면비가 다르면 가장자리 글자가 잘리기 쉬워서, 가로/세로 비율을 최대 maxStretch만큼
    // 바꾸는 것을 허용해 잘리는 양을 줄인다. 그래도 잘리는 쪽은 focus(이미지 비율 좌표, 아래가 0)를 기준으로 나눈다.
    //   16:10 태블릿(1920x1200): 좌우 106씩 자르던 것을 세로 6% 늘림으로 46씩만 자른다.
    //   20:9 폰(2400x1080): 위아래를 자르는데 focus.y가 위쪽이면 위(로고)는 적게, 아래는 많이 자른다.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class CoverImageFitter : MonoBehaviour
    {
        [SerializeField] private float aspectRatio = 16f / 9f;              // 이미지 가로 / 세로
        [SerializeField, Range(1f, 1.2f)] private float maxStretch = 1.06f;
        [SerializeField] private Vector2 focus = new Vector2(0.5f, 0.5f);

        [Header("이미지를 따라가는 UI (선택)")]
        // 이 이미지의 자식(가운데 기준, 크기 followerSize). 이미지가 늘어난 만큼 같이 늘어나, 그림 위 UI 위치가 어느 화면비에서도 맞는다.
        [SerializeField] private RectTransform follower;
        [SerializeField] private Vector2 followerSize = new Vector2(1920f, 1080f);   // follower를 배치한 기준 크기(= 이미지 전체)

        // 변경점: follower 안에서 화면에 꼭 보여야 하는 영역(follower 좌상단 기준, y 아래가 +). 폭이 0이면 쓰지 않는다.
        //   배경이 화면을 채우려고 위아래를 많이 자르면(4:3 배경을 20:9 폰에) 이 영역이 화면 밖으로 나가므로,
        //   그때만 follower를 같은 비율로 줄이고 옮겨 영역 전체가 화면 안(여백 safeMargin)에 들어오게 한다.
        [SerializeField] private Rect safeRect;
        [SerializeField] private float safeMargin = 16f;

        private RectTransform _rt;
        private DrivenRectTransformTracker _tracker;

        public void Setup(float aspect, float stretch, Vector2 focusPoint)
        {
            aspectRatio = aspect;
            maxStretch = stretch;
            focus = focusPoint;
            UpdateRect();
        }

        public void SetFollower(RectTransform target, Vector2 designSize)
        {
            follower = target;
            followerSize = designSize;
            UpdateRect();
        }

        // 변경점: follower에서 화면에 꼭 보여야 하는 영역 (follower 좌상단 기준 px, y 아래가 +)
        public void SetSafeRect(Rect rect, float margin)
        {
            safeRect = rect;
            safeMargin = margin;
            UpdateRect();
        }

        private void OnEnable() => UpdateRect();

        // 씬을 막 불러온 OnEnable 시점에는 캔버스 크기가 아직 0일 수 있다(그러면 follower 스케일이 0으로 남음). 한 번 더 맞춘다.
        private void Start() => UpdateRect();

        private void OnDisable() => _tracker.Clear();

        // 앵커를 부모 전체로 늘려 두므로 부모(캔버스) 크기가 바뀌면 여기로 들어온다.
        private void OnRectTransformDimensionsChange() => UpdateRect();

#if UNITY_EDITOR
        // OnValidate 안에서 RectTransform을 바꾸면 경고가 나므로 다음 에디터 틱으로 미룬다.
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) UpdateRect(); };
#endif

        private void UpdateRect()
        {
            if (!isActiveAndEnabled || aspectRatio <= 0f) return;
            if (_rt == null) _rt = (RectTransform)transform;
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;

            _tracker.Clear();
            _tracker.Add(this, _rt, DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot |
                                    DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.pivot = focus;                 // 부모의 focus 지점에 이미지의 focus 지점이 온다
            _rt.anchoredPosition = Vector2.zero;

            Vector2 p = parent.rect.size;
            if (p.x <= 0f || p.y <= 0f) return;
            float w, h;
            if (p.x / p.y < aspectRatio)
            {
                // 화면이 이미지보다 세로로 길다: 높이를 맞추고 좌우를 자른다(가로를 줄여 덜 자른다)
                h = p.y;
                w = Mathf.Max(p.x, h * aspectRatio / maxStretch);
            }
            else
            {
                // 화면이 더 넓다: 폭을 맞추고 위아래를 자른다(세로를 줄여 덜 자른다)
                w = p.x;
                h = Mathf.Max(p.y, w / aspectRatio / maxStretch);
            }
            var size = new Vector2(w, h) - p;
            if ((_rt.sizeDelta - size).sqrMagnitude > 0.0001f) _rt.sizeDelta = size;

            // 16:9에서는 1배. 태블릿/폰에서는 이미지와 같은 비율(가로/세로 차이 최대 maxStretch)로 늘린다.
            if (follower != null && followerSize.x > 0f && followerSize.y > 0f)
            {
                _tracker.Add(this, follower, DrivenTransformProperties.Scale);
                var scale = new Vector2(w / followerSize.x, h / followerSize.y);
                if (safeRect.width > 0f && safeRect.height > 0f)
                    FitSafeRect(p, new Vector2(w, h), ref scale);
                follower.localScale = new Vector3(scale.x, scale.y, 1f);
            }
        }

        // 변경점: safeRect가 화면(부모) 밖으로 나가면 follower를 같은 비율로 줄이고, 원래 자리에 가장 가깝게 화면 안으로 옮긴다.
        //   follower는 이미지 가운데 기준(anchor/pivot 0.5)이라 anchoredPosition이 곧 이미지 가운데에서의 이동량이다.
        private void FitSafeRect(Vector2 p, Vector2 imageSize, ref Vector2 scale)
        {
            _tracker.Add(this, follower, DrivenTransformProperties.AnchoredPosition);
            // 부모 좌하단 기준 이미지 가운데 (이미지 focus 지점이 부모 focus 지점에 온다)
            Vector2 imageCenter = Vector2.Scale(focus, p) + Vector2.Scale(new Vector2(0.5f, 0.5f) - focus, imageSize);
            // safeRect 가운데를 follower 가운데 기준(y 위가 +)으로
            var local = new Vector2(safeRect.center.x - followerSize.x * 0.5f, followerSize.y * 0.5f - safeRect.center.y);
            var room = p - 2f * new Vector2(safeMargin, safeMargin);
            float k = Mathf.Min(1f, room.x / (safeRect.width * scale.x), room.y / (safeRect.height * scale.y));

            Vector2 wanted = imageCenter + Vector2.Scale(local, scale);          // 줄이기 전 자리
            Vector2 half = Vector2.Scale(safeRect.size, scale) * (0.5f * k);
            var center = new Vector2(
                Mathf.Clamp(wanted.x, safeMargin + half.x, p.x - safeMargin - half.x),
                Mathf.Clamp(wanted.y, safeMargin + half.y, p.y - safeMargin - half.y));
            scale *= k;
            follower.anchoredPosition = center - (imageCenter + Vector2.Scale(local, scale));
        }
    }
}
