using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.UI
{
    // 시작 시 아래에서 위로 떠오르며 페이드 인 (delay 후). 포디움 메달이 단상 위로 올라오는 연출 등.
    // 주의: MonoBehaviour는 클래스명과 동일한 파일이어야 안드로이드 빌드에서 MonoScript가
    // 외부 에셋으로 정상 직렬화된다.
    public class UIRiseIn : MonoBehaviour
    {
        public float delay = 0f;
        public float duration = 0.5f;
        public float rise = 90f;

        private void Start()
        {
            UIFx.SlideFadeIn((RectTransform)transform, delay, duration, rise,
                this.GetCancellationTokenOnDestroy()).Forget();
        }
    }
}
