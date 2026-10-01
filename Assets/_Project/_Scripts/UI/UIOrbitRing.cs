using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 바닥에 놓인 원판 위를 도는 빛 고리. 사각형에 꼭 맞는 타원을 따라 무늬 띠(texture)를 감고,
    //   띠를 둘레 방향으로 밀어(초당 degreesPerSecond) 무늬가 타원 위를 빙글빙글 도는 것처럼 보이게 한다.
    //   납작한 타원을 그림 회전으로 흉내 내면 앞뒤 선이 1~2px로 가늘어져 깜빡이므로, 굵기(band)는 화면 단위로 어디서나 같게 그린다.
    //   위쪽(뒤) 반원은 backAlpha만큼 어둡게 해 앞이 가깝게 보이게 한다.
    // 띠 그림: 가로(u) = 한 바퀴, 세로(v) = 굵기 방향(0 안쪽, 1 바깥쪽). 가로 Wrap Mode는 Repeat이어야 한다.
    // 실행 중에만 돌고, 매 프레임 메시를 다시 만든다(꼭짓점 2 x (segments + 1)개).
    // 주의: MonoBehaviour는 클래스명과 같은 파일이어야 안드로이드 빌드에서 정상 직렬화된다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIOrbitRing : MaskableGraphic
    {
        [SerializeField] private Texture texture;
        [SerializeField] private float band = 12f;                    // 고리 굵기(이 Graphic 단위)
        [SerializeField, Range(16, 256)] private int segments = 120;
        [SerializeField] private float degreesPerSecond = 20f;        // 음수면 반대로 돈다
        [SerializeField, Range(0f, 1f)] private float backAlpha = 0.5f;

        private float _phase;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        public void Setup(Texture strip, float bandWidth, float speed, float back)
        {
            texture = strip;
            band = bandWidth;
            degreesPerSecond = speed;
            backAlpha = back;
            raycastTarget = false;
            SetAllDirty();
        }

        private void Update()
        {
            if (!Application.isPlaying || degreesPerSecond == 0f) return;
            _phase = Mathf.Repeat(_phase + degreesPerSecond / 360f * Time.unscaledDeltaTime, 1f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            Vector2 c = r.center;
            float a = r.width * 0.5f, b = r.height * 0.5f, h = band * 0.5f;
            Color32 baseColor = color;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * 2f * Mathf.PI;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                var vc = baseColor;
                vc.a = (byte)(baseColor.a * Mathf.Lerp(backAlpha, 1f, 0.5f - 0.5f * sin));   // 아래(앞) 1, 위(뒤) backAlpha
                float u = t + _phase;
                vh.AddVert(new Vector3(c.x + (a + h) * cos, c.y + (b + h) * sin), vc, new Vector2(u, 1f));
                vh.AddVert(new Vector3(c.x + (a - h) * cos, c.y + (b - h) * sin), vc, new Vector2(u, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int k = i * 2;
                vh.AddTriangle(k, k + 2, k + 1);
                vh.AddTriangle(k + 1, k + 2, k + 3);
            }
        }
    }
}
