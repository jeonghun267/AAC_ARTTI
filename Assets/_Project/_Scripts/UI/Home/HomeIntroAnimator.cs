using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Artti.UI
{
    // 씬 진입 연출. 각 아이템을 시작 오프셋/스케일에서 제자리로 ease-out 이동.
    // 예) AAC 왼쪽 슬라이드, 카드 위에서 등장, 캐릭터 살짝 확대.
    // 모든 아이템이 끝나면 enableAfter(캐릭터 Idle 등)를 켠다.
    public class HomeIntroAnimator : MonoBehaviour
    {
        [System.Serializable]
        public struct Item
        {
            public RectTransform target;
            public Vector2 fromOffset; // 시작 위치 오프셋(px)
            public float fromScale;    // 시작 스케일(<=0이면 스케일 애니 없음)
            public float delay;
            public float duration;
            public bool fade;          // CanvasGroup 페이드
        }

        [SerializeField] private List<Item> items = new List<Item>();
        [SerializeField] private List<Behaviour> enableAfter = new List<Behaviour>();

        // 빌더에서 호출
        public void AddItem(RectTransform target, Vector2 fromOffset, float fromScale, float delay, float duration, bool fade)
        {
            items.Add(new Item
            {
                target = target,
                fromOffset = fromOffset,
                fromScale = fromScale,
                delay = delay,
                duration = duration,
                fade = fade
            });
        }

        public void AddEnableAfter(Behaviour b) { if (b != null) enableAfter.Add(b); }

        private void Start() => StartCoroutine(Play());

        private IEnumerator Play()
        {
            int n = items.Count;
            var rest = new Vector2[n];
            var restScale = new Vector3[n];
            var cg = new CanvasGroup[n];

            for (int i = 0; i < n; i++)
            {
                var it = items[i];
                if (it.target == null) continue;
                rest[i] = it.target.anchoredPosition;
                restScale[i] = it.target.localScale;
                it.target.anchoredPosition = rest[i] + it.fromOffset;
                if (it.fromScale > 0f) it.target.localScale = restScale[i] * it.fromScale;
                if (it.fade)
                {
                    cg[i] = it.target.GetComponent<CanvasGroup>();
                    if (cg[i] == null) cg[i] = it.target.gameObject.AddComponent<CanvasGroup>();
                    cg[i].alpha = 0f;
                }
            }

            var done = new bool[n];
            int remaining = n;
            float t = 0f;

            while (remaining > 0)
            {
                t += Time.deltaTime;
                for (int i = 0; i < n; i++)
                {
                    if (done[i]) continue;
                    var it = items[i];
                    if (it.target == null) { done[i] = true; remaining--; continue; }

                    float local = t - it.delay;
                    if (local < 0f) continue;

                    float k = it.duration <= 0f ? 1f : Mathf.Clamp01(local / it.duration);
                    float e = EaseOutCubic(k);
                    it.target.anchoredPosition = Vector2.LerpUnclamped(rest[i] + it.fromOffset, rest[i], e);
                    if (it.fromScale > 0f)
                        it.target.localScale = Vector3.LerpUnclamped(restScale[i] * it.fromScale, restScale[i], e);
                    if (cg[i] != null) cg[i].alpha = k;

                    if (k >= 1f) { done[i] = true; remaining--; }
                }
                yield return null;
            }

            foreach (var b in enableAfter)
                if (b != null) b.enabled = true;
        }

        private static float EaseOutCubic(float x)
        {
            float f = 1f - x;
            return 1f - f * f * f;
        }
    }
}
