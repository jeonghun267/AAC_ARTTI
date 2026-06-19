using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 정규화된 값(0~1) 배열을 꺾은선으로 그리는 단순 UI 차트.
    // 색은 Graphic.color, 굵기는 thickness. 한 컴포넌트 = 한 선(시리즈).
    // 그래프 패널 안에 stretch로 깔고 빌더가 values를 채운다.
    [DisallowMultipleComponent]
    public class LineChart : Graphic
    {
        [SerializeField] private float[] values;     // 0..1 정규화 높이
        [SerializeField] private float thickness = 5f;
        [SerializeField] private float padding = 12f; // 패널 안쪽 여백
        [SerializeField] private bool fillUnder = false; // 선 아래 면 채우기(영역 그래프)
        [SerializeField] private float fillAlpha = 0.18f;

        public void SetValues(float[] v) { values = v; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (values == null || values.Length < 2) return;

            var r = rectTransform.rect;
            float x0 = r.xMin + padding, y0 = r.yMin + padding;
            float w = r.width - padding * 2f, h = r.height - padding * 2f;
            int n = values.Length;

            // 영역 채우기 (옵션)
            if (fillUnder)
            {
                var fc = color; fc.a *= fillAlpha;
                for (int i = 0; i < n - 1; i++)
                {
                    Vector2 a = Pt(i, x0, y0, w, h, n), b = Pt(i + 1, x0, y0, w, h, n);
                    int idx = vh.currentVertCount;
                    vh.AddVert(new Vector3(a.x, y0), fc, Vector2.zero);
                    vh.AddVert(new Vector3(a.x, a.y), fc, Vector2.zero);
                    vh.AddVert(new Vector3(b.x, b.y), fc, Vector2.zero);
                    vh.AddVert(new Vector3(b.x, y0), fc, Vector2.zero);
                    vh.AddTriangle(idx, idx + 1, idx + 2);
                    vh.AddTriangle(idx, idx + 2, idx + 3);
                }
            }

            // 선
            for (int i = 0; i < n - 1; i++)
                AddSeg(vh, Pt(i, x0, y0, w, h, n), Pt(i + 1, x0, y0, w, h, n));
        }

        private Vector2 Pt(int i, float x0, float y0, float w, float h, int n)
        {
            return new Vector2(x0 + w * i / (n - 1), y0 + h * Mathf.Clamp01(values[i]));
        }

        private void AddSeg(VertexHelper vh, Vector2 a, Vector2 b)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 nrm = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
            int idx = vh.currentVertCount;
            vh.AddVert(new Vector3(a.x - nrm.x, a.y - nrm.y), color, Vector2.zero);
            vh.AddVert(new Vector3(a.x + nrm.x, a.y + nrm.y), color, Vector2.zero);
            vh.AddVert(new Vector3(b.x + nrm.x, b.y + nrm.y), color, Vector2.zero);
            vh.AddVert(new Vector3(b.x - nrm.x, b.y - nrm.y), color, Vector2.zero);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx, idx + 2, idx + 3);
        }
    }
}
