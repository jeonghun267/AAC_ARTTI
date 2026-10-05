using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.ARField
{
    // 패널을 서서히 사라지게 한 뒤 끈다(페이드아웃). 다음 화면은 호출하는 쪽이 먼저 켜 두고,
    //   이 패널을 잠깐 맨 앞으로 올려 그 위에서 사라지게 하므로 다음 화면이 아래에서 드러난다.
    //   사라지는 동안에는 이 패널을 누를 수 없고, 끝나면 순서·투명도·누르기를 원래대로 돌린다.
    //   쓰는 곳: 현장모드 홈 -> 결과(OcrTestController, 사용자 결정 2026-10-06)
    [DisallowMultipleComponent]
    public sealed class ARPanelFadeOut : MonoBehaviour
    {
        private CanvasGroup _group;
        private CancellationTokenSource _cts;
        private int _siblingIndex = -1;

        public void FadeOutAndHide(float seconds)
        {
            Cancel();
            if (!gameObject.activeInHierarchy || seconds <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _siblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
            _group.blocksRaycasts = false;
            _cts = new CancellationTokenSource();
            Debug.Log($"[ARPanelFx] {name} 페이드아웃 {seconds:F2}초");
            RunAsync(seconds, _cts.Token).Forget();
        }

        // 사라지는 중에 다시 이 패널로 돌아오면(홈으로) 멈추고 원래 모습으로
        public void Cancel()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            Restore();
        }

        private async UniTaskVoid RunAsync(float seconds, CancellationToken ct)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
                t += Mathf.Min(Time.unscaledDeltaTime, ARPanelEnterFx.MaxFrameStep);   // 늦은 프레임에도 끝까지 보이게
                float k = Mathf.Clamp01(t / seconds);
                _group.alpha = 1f - k * k * (3f - 2f * k);   // smoothstep
            }
            _cts?.Dispose();
            _cts = null;
            gameObject.SetActive(false);
            Restore();
        }

        private void Restore()
        {
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
            }
            if (_siblingIndex >= 0)
            {
                transform.SetSiblingIndex(_siblingIndex);
                _siblingIndex = -1;
            }
        }

        private void OnDisable()
        {
            // 씬을 떠나는 등 밖에서 꺼지면 사라지기를 멈춘다
            if (_cts != null) Cancel();
        }
    }
}
