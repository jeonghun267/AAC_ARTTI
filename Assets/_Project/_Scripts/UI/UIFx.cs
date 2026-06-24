using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 가벼운 UniTask 기반 UI 연출 헬퍼 (모바일 친화: 프레임당 할당 없음, 단순 보간).
    public static class UIFx
    {
        static float Ease(float t) => 1f - (1f - t) * (1f - t); // easeOutQuad

        // 아래에서 살짝 떠오르며 페이드 인. delay 후 dur 동안.
        public static async UniTask SlideFadeIn(RectTransform rect, float delay, float dur, float rise, CancellationToken ct)
        {
            if (rect == null) return;
            var cg = rect.GetComponent<CanvasGroup>();
            if (cg == null) cg = rect.gameObject.AddComponent<CanvasGroup>();
            Vector2 home = rect.anchoredPosition;
            cg.alpha = 0f;
            rect.anchoredPosition = home + new Vector2(0f, -rise);

            if (delay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Ease(Mathf.Clamp01(t / dur));
                cg.alpha = k;
                rect.anchoredPosition = Vector2.LerpUnclamped(home + new Vector2(0f, -rise), home, k);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            cg.alpha = 1f;
            rect.anchoredPosition = home;
        }

        // 레이아웃 그룹 자식용 진입: 위치 대신 알파+스케일로 팝인 (위치는 레이아웃이 제어하므로).
        public static async UniTask PopIn(RectTransform rect, float delay, float dur, CancellationToken ct)
        {
            if (rect == null) return;
            var cg = rect.GetComponent<CanvasGroup>();
            if (cg == null) cg = rect.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            rect.localScale = Vector3.one * 0.92f;

            if (delay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Ease(Mathf.Clamp01(t / dur));
                cg.alpha = k;
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, k);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            cg.alpha = 1f;
            rect.localScale = Vector3.one;
        }

        // 숫자 카운트 업. setter로 표시(예: 퍼센트, 도넛 fill 동시 갱신).
        public static async UniTask CountUp(float from, float to, float dur, Action<float> apply, CancellationToken ct)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Ease(Mathf.Clamp01(t / dur));
                apply(Mathf.Lerp(from, to, k));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            apply(to);
        }

        // 타자기 효과. cps = 초당 글자수.
        public static async UniTask Typewriter(TMP_Text label, string full, float cps, CancellationToken ct)
        {
            if (label == null || string.IsNullOrEmpty(full)) { if (label != null) label.text = full; return; }
            label.text = full;
            label.ForceMeshUpdate();
            int total = label.textInfo.characterCount;
            label.maxVisibleCharacters = 0;
            float per = cps > 0f ? 1f / cps : 0f;
            for (int i = 1; i <= total; i++)
            {
                label.maxVisibleCharacters = i;
                if (per > 0f) await UniTask.Delay(TimeSpan.FromSeconds(per), cancellationToken: ct);
            }
            label.maxVisibleCharacters = total;
        }

        // 한 번 확대했다 복귀하는 펀치 (탭 피드백).
        public static async UniTask PunchScale(RectTransform rect, float amount, float dur, CancellationToken ct)
        {
            if (rect == null) return;
            Vector3 baseScale = Vector3.one;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = 1f + amount * Mathf.Sin(k * Mathf.PI); // 0->peak->0
                rect.localScale = baseScale * s;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            rect.localScale = baseScale;
        }
    }
}
