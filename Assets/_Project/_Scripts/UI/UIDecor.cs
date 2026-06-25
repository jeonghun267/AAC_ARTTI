using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Artti.UI
{
    // 주의: 이 파일에 든 MonoBehaviour들은 현재 어떤 씬에서도 사용하지 않는다.
    // 사용하려면 반드시 클래스명과 동일한 파일로 분리할 것. 한 파일에 여러 MonoBehaviour를 두면
    // 파일명과 일치하는 클래스가 없을 때 씬에 가짜 MonoScript가 임베드되어 안드로이드 빌드에서
    // "level corrupted / Position out of bounds" 크래시가 난다. (UIBackdrop/UIRiseIn/ImageToggle2/
    // UICountUp은 이 문제로 각자 파일로 분리됨.)

    // 카드 전체를 탭하면 살짝 확대 펀치 + 네온 글로우 플래시 (모바일 탭 피드백, 호버 대체).
    public class UICardTap : MonoBehaviour, IPointerClickHandler
    {
        public Graphic flashTarget;   // 카드 흰 배경 (잠깐 라이트블루)
        public float punch = 0.05f;
        private RectTransform _rt;
        private Color _base;
        private bool _busy;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            if (flashTarget != null) _base = flashTarget.color;

            // 클릭 수신용 투명 레이캐스트 (카드 컨테이너엔 그래픽이 없으므로)
            var hit = gameObject.GetComponent<Image>();
            if (hit == null) hit = gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true; // 루트 Image는 자식보다 먼저 렌더돼 콘텐츠를 가리지 않음
        }

        public void OnPointerClick(PointerEventData e) { Play().Forget(); }

        private async UniTaskVoid Play()
        {
            if (_busy) return;
            _busy = true;
            var ct = this.GetCancellationTokenOnDestroy();
            float dur = 0.22f, t = 0f;
            var flash = new Color(0.85f, 0.91f, 1f, _base.a);
            try
            {
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float bump = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI); // 0->1->0
                    _rt.localScale = Vector3.one * (1f + punch * bump);
                    if (flashTarget != null) flashTarget.color = Color.Lerp(_base, flash, bump);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException) { }
            _rt.localScale = Vector3.one;
            if (flashTarget != null) flashTarget.color = _base;
            _busy = false;
        }
    }

    // 대상 Graphic을 주기적으로 아주 은은하게 확대/밝기 펄스 (메달·별 반짝임). 위치로 위상 분산.
    public class UISparkle : MonoBehaviour
    {
        public float period = 2.2f;
        public float scaleAmp = 0.11f;
        public float alphaAmp = 0.22f;

        private RectTransform _rt;
        private Graphic _g;
        private float _baseAlpha = 1f;
        private float _phase;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _g = GetComponent<Graphic>();
            if (_g != null) _baseAlpha = _g.color.a;
            _phase = Mathf.Repeat(Mathf.Abs(transform.position.x) * 0.013f, 1f) * Mathf.PI * 2f;
        }

        private void Update()
        {
            float s = Mathf.Sin(Time.time * (2f * Mathf.PI / Mathf.Max(0.1f, period)) + _phase);
            _rt.localScale = Vector3.one * (1f + scaleAmp * 0.5f * (s + 1f));
            if (_g != null) { var c = _g.color; c.a = Mathf.Clamp01(_baseAlpha + alphaAmp * s); _g.color = c; }
        }
    }

    // 카드 글래스 + 3D 호버: 마우스를 올리면 커서 방향으로 살짝 기울고(틸트) 떠오르며(스케일) 글래스 시트가 밝아짐.
    // 데스크톱/에디터 전용 효과(터치엔 호버 없음). 진입 애니와 충돌 안 나게 호버 전엔 건드리지 않음.
    public class UICard3D : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public float hoverScale = 1.035f;
        public float tilt = 6f;
        public float speed = 12f;

        private RectTransform _rt;
        private Camera _cam;
        private bool _hover, _returning;
        private Vector2 _ptr;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            _cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; _returning = false; _ptr = e.position; }
        public void OnPointerExit(PointerEventData e) { _hover = false; _returning = true; }
        public void OnPointerMove(PointerEventData e) { _ptr = e.position; }

        private void Update()
        {
            if (!_hover && !_returning) return; // 평소엔 건드리지 않음(진입 애니 보존)
            float k = 1f - Mathf.Exp(-speed * Time.deltaTime);

            Vector3 ts = _hover ? Vector3.one * hoverScale : Vector3.one;
            _rt.localScale = Vector3.Lerp(_rt.localScale, ts, k);

            Quaternion tr = Quaternion.identity;
            if (_hover && RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, _ptr, _cam, out var local))
            {
                float nx = Mathf.Clamp(local.x / Mathf.Max(1f, _rt.rect.width * 0.5f), -1f, 1f);
                float ny = Mathf.Clamp(local.y / Mathf.Max(1f, _rt.rect.height * 0.5f), -1f, 1f);
                tr = Quaternion.Euler(ny * tilt, -nx * tilt, 0f);
            }
            _rt.localRotation = Quaternion.Slerp(_rt.localRotation, tr, k);

            if (!_hover &&
                _rt.localScale.x <= 1.002f &&
                Quaternion.Angle(_rt.localRotation, Quaternion.identity) < 0.2f)
            {
                _rt.localScale = Vector3.one;
                _rt.localRotation = Quaternion.identity;
                _returning = false; // 정착 -> 휴면
            }
        }
    }

    // 가로 트랙(연결선)을 따라 좌->우로 흐르는 데이터 파티클. 트랙 RectTransform에 런타임 부착.
    public class UITrackParticles : MonoBehaviour
    {
        public int count = 6;
        public float speed = 90f; // px/s
        public Color color = new Color(0.16f, 0.45f, 0.9f, 0.7f);

        private RectTransform[] _dots;
        private float _w = 1000f;

        private void Start()
        {
            var track = (RectTransform)transform;
            _w = track.rect.width > 1f ? track.rect.width : 1000f;
            var sprite = MakeDot();
            _dots = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Flow" + i, typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(track, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(12f, 12f);
                rt.anchoredPosition = new Vector2(_w * i / count, 0f);
                var img = go.AddComponent<Image>();
                img.sprite = sprite; img.color = color; img.raycastTarget = false;
                _dots[i] = rt;
            }
        }

        private void Update()
        {
            if (_dots == null) return;
            for (int i = 0; i < _dots.Length; i++)
            {
                var p = _dots[i].anchoredPosition;
                p.x += speed * Time.deltaTime;
                if (p.x > _w) p.x -= _w;
                _dots[i].anchoredPosition = p;
            }
        }

        private static Sprite MakeDot()
        {
            const int s = 24;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float r = s * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }
    }
}
