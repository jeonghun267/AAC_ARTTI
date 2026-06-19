using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 음식점 카드: 커피/음식 위로 김이 모락모락 올라가는 효과.
    // 각 wisp가 아래에서 위로 떠오르며 좌우로 살짝 흔들리고 점점 옅어진 뒤 반복된다.
    // 빌더가 wisps 배열에 데코 Image들을 연결한다.
    [DisallowMultipleComponent]
    public class SteamRise : MonoBehaviour
    {
        [SerializeField] private Image[] wisps;
        [SerializeField] private float riseHeight = 70f;
        [SerializeField] private float period = 2.8f;
        [SerializeField] private float peakAlpha = 0.5f;
        [SerializeField] private float drift = 10f;        // 좌우 흔들림 px

        private float[] _baseX;
        private float[] _baseY;

        private void Awake()
        {
            int n = wisps != null ? wisps.Length : 0;
            _baseX = new float[n];
            _baseY = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (wisps[i] == null) continue;
                var p = wisps[i].rectTransform.anchoredPosition;
                _baseX[i] = p.x; _baseY[i] = p.y;
            }
        }

        private void Update()
        {
            if (wisps == null) return;
            for (int i = 0; i < wisps.Length; i++)
            {
                var img = wisps[i];
                if (img == null) continue;
                // wisp마다 사이클을 어긋나게 시작
                float t = Mathf.Repeat(Time.time / period + i / (float)wisps.Length, 1f);
                float y = _baseY[i] + t * riseHeight;
                float x = _baseX[i] + Mathf.Sin(t * Mathf.PI * 2f) * drift;
                img.rectTransform.anchoredPosition = new Vector2(x, y);
                // 중간에 가장 진하고 양 끝에서 사라짐
                var c = img.color; c.a = Mathf.Sin(t * Mathf.PI) * peakAlpha; img.color = c;
                img.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.3f, t);
            }
        }
    }
}
