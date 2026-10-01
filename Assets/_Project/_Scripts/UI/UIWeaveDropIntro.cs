using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.UI
{
    // 화면이 켜질 때(OnEnable) 카드 두 장이 위에서 내려오며 서로 엮이듯 꼬였다가 제자리에 내려앉는다.
    //   두 카드는 가운데 세로축을 함께 도는 나선 위에서 늘 반대편에 있다(x = 가운데 + 반지름 * sin, 깊이 = cos).
    //   앞(cos > 0)을 지나는 카드는 조금 크고 위에 그려지고, 뒤를 지나는 카드는 작고 흐리다. 1.5바퀴라 처음엔 서로 자리가 바뀐 채
    //   나타나 두 번 엇갈리며(한 번씩 번갈아 앞으로) 끝에서 원래 자리·크기·그리기 순서로 딱 멈춘다.
    //   카드는 늘 앞을 보고(글이 읽힌다), 크기는 바닥 가운데를 기준으로 바꾼다(발판에 내려앉는 느낌).
    //   내려앉은 뒤에는 발판에서 hoverHeight만큼 천천히 떠올라 위아래로 두둥실 떠 있다(두 카드는 박자를 어긋나게).
    //   흔들림은 작고 느려서 누르기에 방해되지 않는다.
    // 중간에 화면이 꺼지면 바로 원래 모습으로 되돌린다.
    // 주의: MonoBehaviour는 클래스명과 같은 파일이어야 안드로이드 빌드에서 정상 직렬화된다.
    public sealed class UIWeaveDropIntro : MonoBehaviour
    {
        [SerializeField] private RectTransform first;            // 왼쪽 카드
        [SerializeField] private RectTransform second;           // 오른쪽 카드
        [SerializeField] private float delay = 0.1f;
        [SerializeField] private float duration = 1.2f;
        [SerializeField] private float drop = 650f;              // 시작 높이(부모 단위, 위로)
        [SerializeField] private float turns = 1.5f;             // 나선 바퀴 수
        [SerializeField] private float depthScale = 0.14f;       // 맨 앞 +14%, 맨 뒤 -14%
        [SerializeField, Range(0f, 1f)] private float backAlpha = 0.55f;   // 맨 뒤를 지날 때 밝기
        [SerializeField] private float fromScale = 0.85f;
        [SerializeField, Range(0f, 1f)] private float fadePortion = 0.25f; // 앞쪽 이 비율 동안 투명 -> 불투명

        [Header("내려앉은 뒤 두둥실")]
        [SerializeField] private float hoverHeight = 10f;          // 발판에서 떠오르는 높이(부모 단위)
        [SerializeField] private float hoverRiseSeconds = 0.9f;    // 떠오르는 데 걸리는 시간
        [SerializeField] private float bobAmplitude = 5f;          // 위아래 흔들림(부모 단위)
        [SerializeField] private float bobPeriod = 3.2f;           // 한 번 오르내리는 시간(초)
        [SerializeField, Range(0f, 1f)] private float bobPhaseOffset = 0.35f;   // 오른쪽 카드는 이만큼(주기 비율) 어긋나게

        private RectTransform[] _cards;
        private Vector2[] _home;          // 원래 anchoredPosition
        private Vector2[] _foot;          // 원래 바닥 가운데(부모 좌표)
        private float[] _finalAngle;      // 끝 각도(도): 왼쪽 -90, 오른쪽 +90
        private CanvasGroup[] _groups;
        private int[] _order;             // 원래 그리기 순서(형제 순번)
        private float _axisX, _radius;
        private bool _firstOnTop;
        private bool _floating;
        private float _floatTime;
        private CancellationTokenSource _cts;

        public void Setup(RectTransform left, RectTransform right)
        {
            first = left;
            second = right;
        }

        private void Awake()
        {
            if (first == null || second == null) return;
            _cards = new[] { first, second };
            _home = new Vector2[2];
            _foot = new Vector2[2];
            _finalAngle = new float[2];
            _groups = new CanvasGroup[2];
            _order = new[] { first.GetSiblingIndex(), second.GetSiblingIndex() };
            for (int i = 0; i < 2; i++)
            {
                var rt = _cards[i];
                _home[i] = rt.anchoredPosition;
                var size = rt.rect.size;
                _foot[i] = _home[i] + new Vector2((0.5f - rt.pivot.x) * size.x, -rt.pivot.y * size.y);
                _groups[i] = UIFx.Group(rt);
            }
            _axisX = (_foot[0].x + _foot[1].x) * 0.5f;
            _radius = Mathf.Abs(_foot[1].x - _foot[0].x) * 0.5f;
            _finalAngle[0] = _foot[0].x <= _axisX ? -90f : 90f;
            _finalAngle[1] = -_finalAngle[0];
            _firstOnTop = _order[0] > _order[1];
        }

        private void OnEnable()
        {
            if (_cards == null) return;
            var ct = UIFx.Restart(ref _cts, this);
            Play(ct).Forget();
        }

        private void OnDisable()
        {
            UIFx.Cancel(ref _cts);
            _floating = false;
            if (_cards != null) Apply(1f);
        }

        private async UniTaskVoid Play(CancellationToken ct)
        {
            _floating = false;
            bool canceled = await UIFx.Tween(this, delay, duration, Apply, ct).SuppressCancellationThrow();
            if (canceled) return;
            _floatTime = 0f;
            _floating = true;
        }

        // 내려앉은 뒤: 천천히 떠올라(SmoothStep) 위아래로 흔들린다. 떠오르는 동안 흔들림도 0에서 커지므로 끊김이 없다.
        private void Update()
        {
            if (!_floating) return;
            _floatTime += Time.unscaledDeltaTime;
            float rise = Mathf.SmoothStep(0f, 1f, _floatTime / hoverRiseSeconds);
            for (int i = 0; i < 2; i++)
            {
                if (_cards[i] == null) continue;
                float phase = (_floatTime / bobPeriod + bobPhaseOffset * i) * 2f * Mathf.PI;
                float y = rise * (hoverHeight + bobAmplitude * Mathf.Sin(phase));
                _cards[i].anchoredPosition = _home[i] + new Vector2(0f, y);
            }
        }

        // k = 0(시작) ~ 1(끝)
        private void Apply(float k)
        {
            float e = 1f - (1f - k) * (1f - k);   // 고르게 돌다 부드럽게 멈춘다(EaseOutQuad)
            float fade = fadePortion > 0f ? Mathf.Clamp01(k / fadePortion) : 1f;
            float baseScale = Mathf.Lerp(fromScale, 1f, e);
            float lift = (1f - e) * drop;
            float z0 = 0f, z1 = 0f;
            for (int i = 0; i < 2; i++)
            {
                var rt = _cards[i];
                if (rt == null) continue;
                float angle = (_finalAngle[i] + (1f - e) * turns * 360f) * Mathf.Deg2Rad;
                float z = k >= 1f ? 0f : Mathf.Cos(angle);
                float x = k >= 1f ? _foot[i].x : _axisX + _radius * Mathf.Sin(angle);
                float s = k >= 1f ? 1f : baseScale * (1f + depthScale * z);
                var size = rt.rect.size;
                rt.localScale = new Vector3(s, s, 1f);
                var foot = new Vector2(x, _foot[i].y + lift);
                rt.anchoredPosition = k >= 1f ? _home[i]
                    : foot - new Vector2((0.5f - rt.pivot.x) * size.x * s, -rt.pivot.y * size.y * s);
                if (_groups[i] != null) _groups[i].alpha = fade * (z < 0f ? Mathf.Lerp(1f, backAlpha, -z) : 1f);
                if (i == 0) z0 = z; else z1 = z;
            }

            // 앞을 지나는 카드를 위에 그린다(바뀔 때만). 끝나면 원래 순서로.
            bool firstOnTop = k >= 1f ? _order[0] > _order[1] : z0 > z1;
            if (firstOnTop != _firstOnTop)
            {
                _firstOnTop = firstOnTop;
                int lo = Mathf.Min(_order[0], _order[1]), hi = Mathf.Max(_order[0], _order[1]);
                _cards[0].SetSiblingIndex(firstOnTop ? hi : lo);
                _cards[1].SetSiblingIndex(firstOnTop ? lo : hi);
            }
        }
    }
}
