using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 이미지 버튼 2개 토글: 선택은 불투명/비선택 반투명(alpha) + 누름 시 은은한 스케일 펀치(글 안 덮음).
    // 주의: MonoBehaviour는 클래스명과 동일한 파일이어야 안드로이드 빌드에서 MonoScript가
    // 외부 에셋으로 정상 직렬화된다.
    public class ImageToggle2 : MonoBehaviour
    {
        public Button btnA, btnB;
        public Image imgA, imgB;
        public float onAlpha = 1f;
        public float offAlpha = 0.5f;
        public int defaultIndex = 0;

        private void Start()
        {
            if (btnA != null) btnA.onClick.AddListener(() => Select(0));
            if (btnB != null) btnB.onClick.AddListener(() => Select(1));
            Select(defaultIndex);
        }

        private void Select(int i)
        {
            SetAlpha(imgA, i == 0);
            SetAlpha(imgB, i == 1);
            var rt = (i == 0 ? btnA : btnB)?.transform as RectTransform;
            if (rt != null) UIFx.PunchScale(rt, 0.045f, 0.18f, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void SetAlpha(Image img, bool sel)
        {
            if (img == null) return;
            var c = img.color; c.a = sel ? onAlpha : offAlpha; img.color = c;
        }
    }
}
