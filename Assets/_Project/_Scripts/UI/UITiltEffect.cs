using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // Image 메시를 사다리꼴로 민다: 기준 사각형(reference, 보통 카드 루트)의 한쪽 변이 위아래로 |amount|만큼 길어지고 반대쪽 변은 그대로다.
    //   amount > 0 이면 왼쪽 변, < 0 이면 오른쪽 변이 길어진다(그쪽이 앞으로 꺾여 나온 모양).
    // 카드가 살짝 돌아 화면 앞으로 튀어나온 것처럼 보이게 한다(Overlay 캔버스는 3D 회전에 원근이 생기지 않아 메시로 만든다).
    // TMP 글자는 BaseMeshEffect를 거치지 않아 그대로 남는다. 글자는 카드 가운데 쪽이라 몇 px 차이라 어색하지 않다.
    // 주의: MonoBehaviour는 클래스명과 같은 파일이어야 안드로이드 빌드에서 정상 직렬화된다.
    [RequireComponent(typeof(Graphic))]
    public class UITiltEffect : BaseMeshEffect
    {
        [SerializeField] private RectTransform reference;
        [SerializeField, Range(-0.3f, 0.3f)] private float amount;

        public void Setup(RectTransform referenceRect) => reference = referenceRect;

        public void SetAmount(float a)
        {
            if (Mathf.Approximately(a, amount)) return;
            amount = a;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Mathf.Approximately(amount, 0f) || vh.currentVertCount == 0) return;
            var self = graphic.rectTransform;
            var refRt = reference != null ? reference : self;

            // 기준 사각형의 왼쪽/오른쪽 끝과 세로 가운데를 이 그래픽의 로컬 좌표로
            var r = refRt.rect;
            Vector3 bl = self.InverseTransformPoint(refRt.TransformPoint(new Vector3(r.xMin, r.yMin, 0f)));
            Vector3 tr = self.InverseTransformPoint(refRt.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)));
            float x0 = bl.x, w = tr.x - bl.x, cy = (bl.y + tr.y) * 0.5f;
            if (Mathf.Abs(w) < 0.001f) return;

            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = Mathf.Clamp01((v.position.x - x0) / w);   // 0 왼쪽 ~ 1 오른쪽
                float k = 1f + (amount > 0f ? amount * (1f - t) : -amount * t);
                v.position.y = cy + (v.position.y - cy) * k;
                vh.SetUIVertex(v, i);
            }
        }
    }
}
