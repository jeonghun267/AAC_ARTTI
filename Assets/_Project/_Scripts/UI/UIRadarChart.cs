using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 레이더(오각형 등) 값 다각형. 격자·축 이름은 그림(대화 모드 리포트 21.png)이고, 이 그래픽은 값 모양만 그린다.
    // 값 0~1, 첫 값이 위쪽이고 시계 방향으로 돈다. 반지름 = rect 짧은 변의 절반(= 격자 바깥 꼭짓점).
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIRadarChart : MaskableGraphic
    {
        [SerializeField] private float[] values = { 0.86f, 0.88f, 0.82f, 0.78f, 0.80f };
        [SerializeField] private Color centerColor = new Color(0.16f, 0.48f, 1f, 0.30f);
        [SerializeField] private Color edgeColor = new Color(0.20f, 0.80f, 1f, 0.62f);
        [SerializeField] private Color lineColor = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField] private float lineWidth = 2.5f;
        [SerializeField] private Color dotColor = Color.white;
        [SerializeField] private float dotRadius = 3.5f;
        [SerializeField, Range(0f, 1f)] private float grow = 1f;   // 등장 연출(0 = 가운데 점, 1 = 값 그대로)

        const int CircleSegments = 12;

        public void SetValues(float[] v)
        {
            values = v;
            SetVerticesDirty();
        }

        public float Grow
        {
            get => grow;
            set { grow = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = values != null ? values.Length : 0;
            if (n < 3) return;
            var r = rectTransform.rect;
            var c = r.center;
            float radius = Mathf.Min(r.width, r.height) / 2f;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float ang = (90f - 360f * i / n) * Mathf.Deg2Rad;
                pts[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius * Mathf.Clamp01(values[i]) * grow;
            }

            // 채움: 가운데(옅게) -> 꼭짓점(진하게)
            vh.AddVert(c, centerColor * color, Vector2.zero);
            for (int i = 0; i < n; i++) vh.AddVert(pts[i], edgeColor * color, Vector2.zero);
            for (int i = 0; i < n; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % n);

            for (int i = 0; i < n; i++) AddLine(vh, pts[i], pts[(i + 1) % n], lineWidth, lineColor * color);
            for (int i = 0; i < n; i++) AddDisc(vh, pts[i], dotRadius, dotColor * color);
        }

        internal static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
        {
            var dir = b - a;
            if (dir.sqrMagnitude < 0.0001f) return;
            var n = new Vector2(-dir.y, dir.x).normalized * (width / 2f);
            // 꺾이는 곳이 비지 않게 양 끝을 선 두께 절반만큼 늘린다
            var ext = dir.normalized * (width / 2f);
            int i0 = vh.currentVertCount;
            vh.AddVert(a - ext - n, col, Vector2.zero);
            vh.AddVert(a - ext + n, col, Vector2.zero);
            vh.AddVert(b + ext + n, col, Vector2.zero);
            vh.AddVert(b + ext - n, col, Vector2.zero);
            vh.AddTriangle(i0, i0 + 1, i0 + 2);
            vh.AddTriangle(i0, i0 + 2, i0 + 3);
        }

        internal static void AddDisc(VertexHelper vh, Vector2 c, float radius, Color col)
        {
            if (radius <= 0f) return;
            int i0 = vh.currentVertCount;
            vh.AddVert(c, col, Vector2.zero);
            for (int k = 0; k <= CircleSegments; k++)
            {
                float ang = 2f * Mathf.PI * k / CircleSegments;
                vh.AddVert(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius, col, Vector2.zero);
            }
            for (int k = 0; k < CircleSegments; k++) vh.AddTriangle(i0, i0 + 1 + k, i0 + 2 + k);
        }
    }
}
