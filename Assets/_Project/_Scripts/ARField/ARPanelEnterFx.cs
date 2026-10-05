using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Artti.ARField
{
    // 현장모드(ARFieldScene) 패널 전환 효과: 홈(HomePanel)을 뺀 패널이 켜질 때 짧게 페이드 + 살짝 확대(0.98 -> 1, 0.22초).
    //   패널을 켜는 곳이 여러 군데(버튼 OnClick, OcrTestController, OrderManager 등)라 패널의 OnEnable에서 재생한다.
    //   씬 파일을 건드리지 않도록 씬이 열릴 때 Canvas 바로 아래 패널에 스스로 붙는다(BgmPlayer·SceneFader와 같은 방식).
    //   홈으로 돌아갈 때는 효과가 없다(사용자 결정 2026-10-06).
    [DisallowMultipleComponent]
    public sealed class ARPanelEnterFx : MonoBehaviour
    {
        const string SceneName = "ARFieldScene";
        const string CanvasName = "Canvas";
        const string HomePanelName = "HomePanel";
        const float Duration = 0.22f;
        const float StartScale = 0.98f;

        private CanvasGroup _group;
        private CancellationTokenSource _cts;

        // Reload Domain을 끈 에디터에서는 이벤트 구독이 플레이 사이에 남으므로 매 플레이 시작 때 다시 건다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != CanvasName) continue;
                foreach (Transform panel in root.transform)
                {
                    if (panel.name == HomePanelName || !(panel is RectTransform)) continue;
                    if (panel.GetComponent<ARPanelEnterFx>() == null) panel.gameObject.AddComponent<ARPanelEnterFx>();
                }
            }
        }

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            Stop();
            _cts = new CancellationTokenSource();
            PlayAsync(_cts.Token).Forget();
        }

        private void OnDisable()
        {
            Stop();
            Apply(1f);   // 꺼지는 도중이면 다음에 켜질 때를 위해 원래 모습으로
        }

        private void Stop()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        private async UniTaskVoid PlayAsync(CancellationToken ct)
        {
            Apply(0f);
            float t = 0f;
            while (t < Duration)
            {
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
                t += Time.unscaledDeltaTime;
                Apply(Mathf.Clamp01(t / Duration));
            }
            Apply(1f);
        }

        // k: 0 = 시작(투명, 0.98배), 1 = 끝. easeOutQuad
        private void Apply(float k)
        {
            float e = 1f - (1f - k) * (1f - k);
            if (_group != null) _group.alpha = e;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(StartScale, 1f, e);
        }
    }
}
