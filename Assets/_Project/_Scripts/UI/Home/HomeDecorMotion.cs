using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 배경 스티커 모션.
    //  Float  : 두둥실 떠다님(느린 8자 드리프트 + 살짝 회전 흔들림)
    //  Twinkle: 제자리 고정 + 반짝임(스케일/투명도 펄스)
    // 스티커마다 위상이 달라 따로 노는 느낌을 준다.
    [DisallowMultipleComponent]
    public class HomeDecorMotion : MonoBehaviour
    {
        public enum Mode { Float, Twinkle }

        [SerializeField] private Mode mode = Mode.Float;

        [Header("Float")]
        [SerializeField] private Vector2 floatAmp = new Vector2(22f, 30f);
        [SerializeField] private float floatSpeed = 0.22f;
        [SerializeField] private float rotAmp = 6f;
        [SerializeField] private float rotSpeed = 0.18f;

        [Header("Twinkle")]
        [SerializeField] private float twinkleScaleAmp = 0.14f;
        [SerializeField] private float twinkleAlphaMin = 0.5f;
        [SerializeField] private float twinkleSpeed = 1.1f;

        private RectTransform _rt;
        private Graphic _gfx;
        private Vector2 _basePos;
        private Vector3 _baseScale;
        private float _phase;

        public void Configure(Mode m) => mode = m;

        // 별 후광처럼 "조금만 둥둥" 떠다니게: 진폭/속도/회전을 작게 직접 지정.
        public void ConfigureFloat(Vector2 amp, float speed, float rot = 0f)
        {
            mode = Mode.Float;
            floatAmp = amp;
            floatSpeed = speed;
            rotAmp = rot;
        }

        private void OnEnable()
        {
            _rt = (RectTransform)transform;
            _gfx = GetComponent<Graphic>();
            _basePos = _rt.anchoredPosition;
            _baseScale = _rt.localScale;
            _phase = (Mathf.Abs(GetInstanceID()) % 1000) * 0.0173f;
        }

        private void Update()
        {
            float t = Time.time + _phase;
            const float TAU = Mathf.PI * 2f;

            if (mode == Mode.Float)
            {
                float x = Mathf.Sin(t * floatSpeed * TAU) * floatAmp.x;
                float y = Mathf.Sin(t * floatSpeed * 1.3f * TAU + 1.3f) * floatAmp.y;
                _rt.anchoredPosition = _basePos + new Vector2(x, y);
                _rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * rotSpeed * TAU) * rotAmp);
            }
            else
            {
                float k = Mathf.Sin(t * twinkleSpeed * TAU);
                _rt.localScale = _baseScale * (1f + k * twinkleScaleAmp);
                if (_gfx != null)
                {
                    var c = _gfx.color;
                    c.a = Mathf.Lerp(twinkleAlphaMin, 1f, k * 0.5f + 0.5f);
                    _gfx.color = c;
                }
                _rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * rotSpeed * 0.5f * TAU) * rotAmp * 0.4f);
            }
        }
    }
}
