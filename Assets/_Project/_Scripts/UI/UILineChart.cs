using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 꺾은선 그래프(대화 모드 리포트 '이번 주 변화'). 점은 rect 가로에 고르게(첫 점 = 왼쪽 끝, 끝 점 = 오른쪽 끝),
    // 값 0~1이 rect 아래~위. 0보다 작은 값은 기록 없음(점·선을 건너뛴다). 점마다 세로 점선 안내선을 그린다.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UILineChart : MaskableGraphic
    {
        [SerializeField] private float[] values = { 0.23f, 0.38f, 0.5f, 0.56f, 0.65f, 0.75f, 0.92f };
        [SerializeField] private Color lineColor = new Color(0.25f, 0.85f, 1f, 1f);
        [SerializeField] private float lineWidth = 3f;
        [SerializeField] private Color dotColor = new Color(0.30f, 0.90f, 1f, 1f);
        [SerializeField] private float dotRadius = 5f;
        [SerializeField] private Color lastDotColor = Color.white;
        [SerializeField] private float lastDotRadius = 6.5f;
        [SerializeField] private Color guideColor = new Color(0.70f, 0.82f, 1f, 0.35f);
        [SerializeField] private float guideWidth = 1.2f;
        [SerializeField] private float dash = 4f;
        [SerializeField, Range(0f, 1f)] private float reveal = 1f;   // 등장 연출(왼쪽부터 그려진다)

        public void SetValues(float[] v)
        {
            values = v;
            SetVerticesDirty();
        }

        public float Reveal
        {
            get => reveal;
            set { reveal = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = values != null ? values.Length : 0;
            if (n < 2) return;
            var r = rectTransform.rect;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
                pts[i] = new Vector2(r.xMin + r.width * i / (n - 1), r.yMin + r.height * Mathf.Clamp01(values[i]));

            for (int i = 0; i < n; i++)
                for (float y = r.yMin; y < r.yMax; y += dash * 2f)
                    UIRadarChart.AddLine(vh, new Vector2(pts[i].x, y), new Vector2(pts[i].x, Mathf.Min(y + dash, r.yMax)),
                        guideWidth, guideColor * color);

            float limitX = r.xMin + r.width * reveal;
            int prev = -1, last = -1;
            for (int i = 0; i < n; i++)
            {
                if (values[i] < 0f) continue;
                if (prev >= 0)
                {
                    var a = pts[prev];
                    var b = pts[i];
                    if (a.x < limitX)
                    {
                        if (b.x > limitX) b = Vector2.Lerp(a, b, (limitX - a.x) / (b.x - a.x));
                        UIRadarChart.AddLine(vh, a, b, lineWidth, lineColor * color);
                    }
                }
                prev = i;
                last = i;
            }
            for (int i = 0; i < n; i++)
            {
                if (values[i] < 0f || pts[i].x > limitX + 0.5f) continue;
                bool isLast = i == last;
                UIRadarChart.AddDisc(vh, pts[i], isLast ? lastDotRadius : dotRadius, (isLast ? lastDotColor : dotColor) * color);
            }
        }
    }
}
