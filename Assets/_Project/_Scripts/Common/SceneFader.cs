using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Artti.Common
{
    // 씬 전환 페이드: 화면을 검게 덮고(짧게) 씬을 바꾼 뒤, 새 씬에서 다시 걷어낸다(조금 길게).
    // 전환 중에는 터치를 막아 버튼을 두 번 눌러 씬이 두 번 바뀌는 일도 막는다.
    // 씬 파일을 건드리지 않도록 처음 쓸 때 스스로 만들어 DontDestroyOnLoad로 둔다(손배치 씬 보호).
    // 쓰는 법: SceneManager.LoadScene(name) 대신 SceneFader.Load(name).
    public sealed class SceneFader : MonoBehaviour
    {
        const float OutSeconds = 0.22f;
        const float InSeconds = 0.32f;
        const float MaxFrameStep = 1f / 30f;   // 씬 로드 직후 긴 프레임에 페이드가 통째로 건너뛰지 않게
        const int SortingOrder = 32000;         // 모든 UI 위

        // 현장모드(AR)는 아직 페이드를 붙이지 않는다. 이 씬으로 가거나 이 씬에서 나올 때는 기존처럼 바로 바꾼다.
        static readonly string[] HardCutScenes = { "ARFieldScene", "ARStoreScene" };

        static SceneFader _instance;

        private Canvas _canvas;
        private GraphicRaycaster _raycaster;
        private CanvasGroup _group;
        private bool _busy;
        private bool _dipMusic;   // 변경점: 음악 씬(스플래시·메인·프로필)끼리 오갈 때만 배경음악을 같이 줄였다 키운다

        public static bool IsBusy => _instance != null && _instance._busy;

        public static void Load(string sceneName) => Begin(sceneName, false);

        // 이미 화면이 검게 끝난 곳(스플래시 마지막)에서 부를 때: 덮는 단계 없이 바로 바꾸고 걷어내기만 한다.
        public static void LoadFromBlack(string sceneName) => Begin(sceneName, true);

        // Reload Domain을 끈 에디터에서는 정적 필드가 플레이 사이에 남으므로 매 플레이 시작 때 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private static void Begin(string sceneName, bool fromBlack)
        {
            if (string.IsNullOrEmpty(sceneName) || IsBusy) return;   // 전환 중 두 번째 요청은 무시
            if (IsHardCut(SceneManager.GetActiveScene().name) || IsHardCut(sceneName))
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
            Ensure().Run(sceneName, fromBlack).Forget();
        }

        private static bool IsHardCut(string sceneName) => Array.IndexOf(HardCutScenes, sceneName) >= 0;

        private static SceneFader Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[SceneFader]", typeof(RectTransform));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneFader>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            _raycaster = gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();

            var cover = new GameObject("Cover", typeof(RectTransform));
            var rt = (RectTransform)cover.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = cover.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;   // 전환 중 터치 막기

            _group.alpha = 0f;
            SetVisible(false);
        }

        // 쉬는 동안은 캔버스를 꺼 그리기·레이캐스트 비용을 없앤다.
        private void SetVisible(bool on)
        {
            _canvas.enabled = on;
            _raycaster.enabled = on;
            _group.blocksRaycasts = on;
        }

        private async UniTaskVoid Run(string sceneName, bool fromBlack)
        {
            var ct = this.GetCancellationTokenOnDestroy();
            _busy = true;
            _dipMusic = BgmPlayer.IsMusicScene(SceneManager.GetActiveScene().name) && BgmPlayer.IsMusicScene(sceneName);
            SetVisible(true);
            try
            {
                if (fromBlack) { _group.alpha = 1f; if (_dipMusic) BgmPlayer.SetDip(1f); }
                else await FadeTo(1f, OutSeconds, ct);

                SceneManager.LoadScene(sceneName);
                // 새 씬의 Awake/Start와 첫 프레임(무거운 초기화)을 검은 막 아래에서 넘긴다
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await FadeTo(0f, InSeconds, ct);
            }
            catch (OperationCanceledException) { return; }   // 앱 종료로 파괴됨
            _busy = false;
            SetVisible(false);
        }

        private async UniTask FadeTo(float target, float seconds, CancellationToken ct)
        {
            float from = _group.alpha;
            float t = 0f;
            while (t < seconds)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                t += Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
                float k = Mathf.Clamp01(t / seconds);
                _group.alpha = Mathf.Lerp(from, target, k * k * (3f - 2f * k));
                if (_dipMusic) BgmPlayer.SetDip(_group.alpha);   // 변경점: 배경음악도 화면이 어두워지는 만큼 줄었다가 밝아지면 다시 커진다
            }
        }
    }
}
