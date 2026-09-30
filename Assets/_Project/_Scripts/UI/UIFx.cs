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

        // ===== 변경점: 화면 효과 공통 (씬 전환·팝업·훈련 피드백·화면 등장) =====
        // 원칙: 짧게(0.15~0.5초), 번쩍임·흔들기 없이, 연출 중에도 터치는 막지 않는다.

        // 씬을 막 불러온 프레임처럼 한 프레임이 길어도 연출이 통째로 건너뛰지 않게 한 프레임 진행량을 제한한다.
        const float MaxFrameStep = 1f / 30f;

        public static float EaseOutCubic(float t)
        {
            float f = 1f - t;
            return 1f - f * f * f;
        }

        // 끝에서 살짝(약 7%) 넘쳤다 돌아오는 곡선. 튕김을 크게 하지 않는다.
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float f = t - 1f;
            return 1f + c3 * f * f * f + c1 * f * f;
        }

        // 공통 보간: 부르는 즉시 apply(0) -> delay 뒤 dur 동안 매 프레임 apply(0~1) -> 끝에 apply(1).
        // owner가 파괴되면 조용히 멈춘다(씬 전환·목록 재생성 중 MissingReference 방지).
        public static async UniTask Tween(UnityEngine.Object owner, float delay, float dur, Action<float> apply, CancellationToken ct)
        {
            if (owner == null) return;
            apply(0f);
            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);
                if (owner == null) return;
            }
            if (dur <= 0f) { apply(1f); return; }
            float t = 0f;
            while (t < dur)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (owner == null) return;
                t += Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
                apply(Mathf.Clamp01(t / dur));
            }
        }

        // 이전 연출을 멈추고 새 토큰을 만든다(같은 대상에 연출이 겹치지 않게). owner가 파괴되면 함께 취소된다.
        public static CancellationToken Restart(ref CancellationTokenSource cts, MonoBehaviour owner)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }
            cts = CancellationTokenSource.CreateLinkedTokenSource(owner.GetCancellationTokenOnDestroy());
            return cts.Token;
        }

        public static void Cancel(ref CancellationTokenSource cts)
        {
            if (cts == null) return;
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        public static CanvasGroup Group(Component c)
        {
            if (!c.TryGetComponent<CanvasGroup>(out var g)) g = c.gameObject.AddComponent<CanvasGroup>();
            return g;
        }

        // point(사각형 안 0~1 위치)를 고정한 채 배율 k를 적용한다. 피벗이 모서리에 있어도 가운데·말풍선 꼬리 등을 기준으로 커진다.
        // home = 배율 1일 때의 anchoredPosition. 위치를 바꾸므로 레이아웃 그룹 자식에는 쓰지 않는다.
        public static void SetScaleAround(RectTransform rect, Vector2 home, Vector2 point, float k)
        {
            rect.localScale = new Vector3(k, k, 1f);
            rect.anchoredPosition = home + Vector2.Scale(point - rect.pivot, rect.rect.size) * (1f - k);
        }

        public static UniTask FadeGroup(CanvasGroup g, float from, float to, float delay, float dur, CancellationToken ct)
        {
            if (g == null) return UniTask.CompletedTask;
            return Tween(g, delay, dur, k => g.alpha = Mathf.Lerp(from, to, k), ct);
        }

        // 알파 + 크기로 톡 나타나기. 위치는 그대로라 레이아웃 그룹 자식에도 쓴다. 크기는 1에서 끝난다.
        public static UniTask PopScaleIn(RectTransform rect, float delay, float dur, float fromScale, CancellationToken ct)
        {
            if (rect == null) return UniTask.CompletedTask;
            var g = Group(rect);
            return Tween(rect, delay, dur, k =>
            {
                g.alpha = Mathf.Clamp01(k * 2f);
                float s = Mathf.LerpUnclamped(fromScale, 1f, EaseOutBack(k));
                rect.localScale = new Vector3(s, s, 1f);
            }, ct);
        }

        // PopScaleIn과 같지만 point 기준으로 커진다(피벗이 모서리인 요소용).
        public static UniTask PopAroundIn(RectTransform rect, Vector2 home, Vector2 point, float delay, float dur, float fromScale, CancellationToken ct)
        {
            if (rect == null) return UniTask.CompletedTask;
            var g = Group(rect);
            return Tween(rect, delay, dur, k =>
            {
                g.alpha = Mathf.Clamp01(k * 2f);
                SetScaleAround(rect, home, point, Mathf.LerpUnclamped(fromScale, 1f, EaseOutBack(k)));
            }, ct);
        }

        // 이미 보이는 요소를 point 기준으로 fromScale -> 1 톡 키운다(알파는 그대로).
        public static UniTask BounceAround(RectTransform rect, Vector2 home, Vector2 point, float delay, float dur, float fromScale, CancellationToken ct)
        {
            if (rect == null) return UniTask.CompletedTask;
            return Tween(rect, delay, dur, k => SetScaleAround(rect, home, point, Mathf.LerpUnclamped(fromScale, 1f, EaseOutBack(k))), ct);
        }

        // point 기준 펀치(1 -> 1+amount -> 1). 누른 문장·새 추천 문장 등 "이것" 표시.
        public static UniTask PunchAround(RectTransform rect, Vector2 home, Vector2 point, float amount, float delay, float dur, CancellationToken ct)
        {
            if (rect == null) return UniTask.CompletedTask;
            return Tween(rect, delay, dur, k => SetScaleAround(rect, home, point, 1f + amount * Mathf.Sin(k * Mathf.PI)), ct);
        }

        // home + fromOffset에서 home으로 미끄러지며 나타난다(레이아웃 그룹 밖 요소용).
        public static UniTask MoveFadeIn(RectTransform rect, Vector2 home, Vector2 fromOffset, float delay, float dur, CancellationToken ct)
        {
            if (rect == null) return UniTask.CompletedTask;
            var g = Group(rect);
            return Tween(rect, delay, dur, k =>
            {
                g.alpha = Mathf.Clamp01(k * 1.6f);
                rect.anchoredPosition = Vector2.LerpUnclamped(home + fromOffset, home, EaseOutCubic(k));
            }, ct);
        }
    }
}
