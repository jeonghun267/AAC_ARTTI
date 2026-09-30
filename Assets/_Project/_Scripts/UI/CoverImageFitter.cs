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
                follower.localScale = new Vector3(w / followerSize.x, h / followerSize.y, 1f);
            }
        }
    }
}
