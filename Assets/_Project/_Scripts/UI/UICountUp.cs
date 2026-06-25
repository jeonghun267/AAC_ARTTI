using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Artti.UI
{
    // TMP 텍스트를 0에서 target까지 카운트업 (delay 후). 시나리오 숙련도 % 등.
    // 주의: MonoBehaviour는 클래스명과 동일한 파일이어야 안드로이드 빌드에서 MonoScript가
    // 외부 에셋으로 정상 직렬화된다.
    public class UICountUp : MonoBehaviour
    {
        public float target = 100f;
        public float duration = 0.9f;
        public float delay = 0.2f;
        public string suffix = "%";

        private async UniTaskVoid Start()
        {
            var t = GetComponent<TMP_Text>();
            if (t == null) return;
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                t.text = "0" + suffix;
                if (delay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
                await UIFx.CountUp(0f, target, duration, v => t.text = Mathf.RoundToInt(v) + suffix, ct);
            }
            catch (OperationCanceledException) { }
        }
    }
}
