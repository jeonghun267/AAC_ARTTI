using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Artti.ARField
{
    // 현장모드(ARFieldScene) 패널 전환 효과: 패널이 켜질 때 짧게 페이드인(+ 살짝 확대).
    //   패널을 켜는 곳이 여러 군데(버튼 OnClick, OcrTestController, OrderManager 등)라 패널의 OnEnable에서 재생한다.
    //   씬 파일을 건드리지 않도록 씬이 열릴 때 Canvas 바로 아래 패널에 스스로 붙는다(BgmPlayer·SceneFader와 같은 방식).
    //   변경점(사용자 결정 2026-10-06):
    //     홈(HomePanel) = 효과 없음. 결과(ResultPanel) = 효과 없음(홈이 위에서 페이드아웃하며 드러난다, ARPanelFadeOut).
    //     도움 상세(HelpDetailPanel) = 페이드인만 0.6초. 인사·주문·결제·편의(그 밖) = 페이드 + 0.96 -> 1 확대 0.6초.
    [DisallowMultipleComponent]
    public sealed class ARPanelEnterFx : MonoBehaviour
    {
        const string SceneName = "ARFieldScene";
        const string CanvasName = "Canvas";
        const string HomePanelName = "HomePanel";
        const string ResultPanelName = "ResultPanel";
        const string HelpDetailPanelName = "HelpDetailPanel";
        // 변경점(실기기 2026-10-06 "너무 빨라 안 보임"): 0.35초 / 0.98배 / 도움 0.3초 -> 0.6초 / 0.96배 / 도움 0.6초
        const float Duration = 0.6f;
        const float StartScale = 0.96f;
        const float HelpDetailDuration = 0.6f;
        // 변경점: 한 프레임에 진행하는 최대 시간. 패널이 처음 켜지는 프레임은 카드 만들기·사진·지도 때문에 수백 ms가 걸려
        //   효과가 한 프레임 만에 끝나 버렸다(실기기 2026-10-06). 늦은 프레임에도 페이드가 끝까지 보이게 한다
        internal const float MaxFrameStep = 1f / 30f;

        private CanvasGroup _group;
        private CancellationTokenSource _cts;
        private float _duration = Duration;
        private float _startScale = StartScale;

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
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != CanvasName) continue;
                foreach (Transform panel in root.transform)
                {
                    if (panel.name == HomePanelName || panel.name == ResultPanelName || !(panel is RectTransform)) continue;
                    var fx = panel.GetComponent<ARPanelEnterFx>();
                    if (fx == null) fx = panel.gameObject.AddComponent<ARPanelEnterFx>();
                    if (panel.name == HelpDetailPanelName) fx.Setup(HelpDetailDuration, 1f);   // 페이드인만
                    else fx.Setup(Duration, StartScale);
                    count++;
                }
            }
            Debug.Log($"[ARPanelFx] 패널 {count}개에 등장 효과를 붙임");
        }

        public void Setup(float duration, float startScale)
        {
            _duration = Mathf.Max(0.01f, duration);
            _startScale = startScale;
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
            while (t < _duration)
            {
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
                t += Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
                Apply(Mathf.Clamp01(t / _duration));
            }
            Apply(1f);
        }

        // k: 0 = 시작(투명, _startScale배), 1 = 끝. easeOutQuad
        private void Apply(float k)
        {
            float e = 1f - (1f - k) * (1f - k);
            if (_group != null) _group.alpha = e;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(_startScale, 1f, e);
        }
    }
}
