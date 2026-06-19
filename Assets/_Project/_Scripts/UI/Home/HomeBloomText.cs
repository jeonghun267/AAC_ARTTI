using UnityEngine;
using TMPro;

namespace Artti.UI
{
    // TMP 글자의 FaceColor를 HDR(>1)로 올려 URP Bloom이 "이 글자만" 잡도록 한다.
    // 카드/배경(휘도 ~1.0)은 Bloom 임계(threshold) 아래라 번지지 않고, AAC 글자만 빛난다.
    // 캔버스가 Screen Space - Camera이고 카메라 PostProcessing/HDR이 켜져 있어야 동작.
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class HomeBloomText : MonoBehaviour
    {
        [SerializeField] private float intensity = 3.2f; // 클수록 강하게 번짐
        [SerializeField] private bool useCurrentColor = true;
        [SerializeField] private Color tint = Color.white;

        public void SetIntensity(float value) => intensity = value;

        private void Start()
        {
            var tmp = GetComponent<TMP_Text>();
            Color baseColor = useCurrentColor ? tmp.color : tint;
            Color hdr = new Color(baseColor.r * intensity, baseColor.g * intensity, baseColor.b * intensity, baseColor.a);
            // fontMaterial 접근 시 인스턴스 머티리얼이 생성됨(이 오브젝트만 영향)
            tmp.fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, hdr);
        }
    }
}
