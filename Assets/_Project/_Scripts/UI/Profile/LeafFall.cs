using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 프로필 만들기 배경: 나뭇잎이 화면 위에서 아래로 떨어진다.
    // 떨어지는 동안 좌우로 U자 호를 그리며 흔들리고 살짝 회전한 뒤,
    // 바닥을 지나면 다시 위로 올라가 무한 반복한다. (Random 미사용 -> 결정적)
    // 빌더가 leaves 배열에 나뭇잎 Image들을 연결한다. 이 컴포넌트는 부모(컨테이너)
    // RectTransform의 높이를 기준으로 낙하 구간을 잡는다.
    [DisallowMultipleComponent]
    public class LeafFall : MonoBehaviour
    {
        [SerializeField] private Image[] leaves;

        [Header("낙하")]
        [SerializeField] private float fallPeriod = 7f;     // 위->아래 한 바퀴 초
        [SerializeField] private float margin = 80f;        // 화면 위/아래로 더 벗어나는 여유 px

        [Header("U자 흔들림")]
        [SerializeField] private float swayAmplitude = 90f; // 좌우 흔들림 폭 px
        [SerializeField] private float swaySpan = 1.5f;     // 낙하 1회당 흔들림 반사이클 수
        [SerializeField] private float spin = 22f;          // 좌우로 기우는 각도

        [Header("페이드")]
        [SerializeField] private float fadeEdge = 0.12f;    // 위/아래 끝 구간에서 서서히 나타나고 사라짐

        private float[] _baseX;
        private float[] _phase;
        private float _topY;
        private float _bottomY;

        private void Awake()
        {
            var rt = (RectTransform)transform;
            float half = rt.rect.height * 0.5f;
            _topY = half + margin;
            _bottomY = -half - margin;

            int n = leaves != null ? leaves.Length : 0;
            _baseX = new float[n];
            _phase = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (leaves[i] != null)
                    _baseX[i] = leaves[i].rectTransform.anchoredPosition.x;
                // 잎마다 시작 위치/위상을 고르게 흩뿌려 동시에 떨어지지 않게
                _phase[i] = n > 0 ? i / (float)n : 0f;
            }
        }

        private void Update()
        {
            if (leaves == null) return;
            for (int i = 0; i < leaves.Length; i++)
            {
                var leaf = leaves[i];
                if (leaf == null) continue;

                float t = Mathf.Repeat(Time.time / fallPeriod + _phase[i], 1f);

                float y = Mathf.Lerp(_topY, _bottomY, t);
                float arc = Mathf.Sin((t * swaySpan + _phase[i]) * Mathf.PI * 2f);
                float x = _baseX[i] + arc * swayAmplitude;

                var crt = leaf.rectTransform;
                crt.anchoredPosition = new Vector2(x, y);
                crt.localRotation = Quaternion.Euler(0f, 0f, arc * spin);

                var c = leaf.color;
                c.a = EdgeFade(t);
                leaf.color = c;
            }
        }

        // 끝(0,1) 근처에서 0, 가운데 구간에서 1
        private float EdgeFade(float t)
        {
            if (fadeEdge <= 0f) return 1f;
            float a = Mathf.Clamp01(t / fadeEdge);
            float b = Mathf.Clamp01((1f - t) / fadeEdge);
            return Mathf.Min(a, b);
        }
    }
}
