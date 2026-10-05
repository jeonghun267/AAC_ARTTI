using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Artti.Common
{
    // 배경음악: 스플래시·메인·프로필(고르기·만들기)·설정 씬에서 같은 곡을 끊기지 않게 이어서 틀고,
    // 다른 씬(훈련·리포트·현장모드)으로 가면 서서히 줄여 멈췄다가 돌아오면 멈춘 곳부터 다시 튼다.
    // 씬 파일을 건드리지 않도록 SceneFader처럼 처음 필요할 때 스스로 만들어 DontDestroyOnLoad로 둔다.
    // 곡: Resources/Audio/BGM/Unity_BGM (가져오기 설정은 Editor/BgmImportSettings)
    public sealed class BgmPlayer : MonoBehaviour
    {
        const string ClipPath = "Audio/BGM/Unity_BGM";
        // 변경점(2026-10-06 사용자 요청): 전체 배경음악을 예전의 1/3로(0.45 / 0.12 / 0.15 -> 0.15 / 0.04 / 0.05)
        const float Volume = 0.15f;
        const float DuckedVolume = 0.04f;      // 목소리 미리듣기처럼 말소리가 나는 동안
        const float DipVolume = 0.05f;         // 화면이 어두워지는 전환(SceneFader, 스플래시 끝) 동안 잠깐
        const float FadeInSeconds = 1.2f;      // 다시 커질 때(전환 뒤 '진입' 느낌도 이 속도)
        const float FadeOutSeconds = 0.6f;
        const float DipDownSeconds = 0.2f;     // 전환 암전(0.22초)이 짧아 줄일 때는 빠르게
        const float MaxFrameStep = 1f / 30f;   // 씬 로드 직후 긴 프레임에 페이드가 통째로 건너뛰지 않게

        static readonly string[] MusicScenes =
        {
            "SplashScene", "MainScene", "ProfileSelectScene", "ProfileCreateScene", "ProfileSettingsScene",
        };

        static BgmPlayer _instance;
        static bool _ducked;
        static float _dip;   // 0~1. 전환 화면이 어두운 정도. 새 씬이 밝아지면 0으로 돌아가 음악이 다시 커진다(진입 느낌)

        private AudioSource _source;
        private bool _wanted;
        private bool _started;

        // Reload Domain을 끈 에디터에서는 정적 필드·이벤트 구독이 플레이 사이에 남으므로 매 플레이 시작 때 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _ducked = false;
            _dip = 0f;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Apply(SceneManager.GetActiveScene().name);   // 첫 씬은 activeSceneChanged가 오지 않는다
        }

        // 배경음악이 나오는 씬인지(SceneFader가 음악 씬끼리 오갈 때만 줄였다 키우려고 묻는다)
        public static bool IsMusicScene(string sceneName) => Array.IndexOf(MusicScenes, sceneName) >= 0;

        // 말소리가 나는 동안 음악을 작게(켜기) / 원래대로(끄기)
        public static void SetDucked(bool ducked) => _ducked = ducked;

        // 변경점: 화면이 어두워지는 정도(0~1)만큼 음악을 잠깐 줄인다. SceneFader 전환과 스플래시 끝 암전에서 부른다.
        public static void SetDip(float amount) => _dip = Mathf.Clamp01(amount);

        private static void OnActiveSceneChanged(Scene from, Scene to) => Apply(to.name);

        private static void Apply(string sceneName)
        {
            bool wanted = IsMusicScene(sceneName);
            if (!wanted && _instance == null) return;   // 음악 씬에 간 적이 없으면 만들지 않는다
            var player = Ensure();
            if (player == null) return;
            player._wanted = wanted;
            if (!SceneFader.IsBusy) _dip = 0f;   // 페이드 전환 중이면 SceneFader가 밝아지는 만큼 풀어 준다(AR 바로 전환 등은 여기서)
            if (!wanted) _ducked = false;   // 미리듣기 중에 씬을 떠나도 줄인 상태가 남지 않게
            if (wanted && !player._source.isPlaying)
            {
                if (player._started) player._source.UnPause();   // 다른 씬에 갔다 오면 멈춘 곳부터
                else player._source.Play();
                player._started = true;
            }
        }

        private static BgmPlayer Ensure()
        {
            if (_instance != null) return _instance;
            var clip = Resources.Load<AudioClip>(ClipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[BgmPlayer] Resources/{ClipPath} 없음 - 배경음악 없이 진행");
                return null;
            }
            var go = new GameObject("[BgmPlayer]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BgmPlayer>();
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;   // Update에서 서서히 키운다
            _instance._source = source;
            return _instance;
        }

        private void Update()
        {
            float level = _ducked ? DuckedVolume : Volume;
            float target = !_wanted ? 0f : Mathf.Lerp(level, Mathf.Min(level, DipVolume), _dip);
            float current = _source.volume;
            if (Mathf.Approximately(current, target))
            {
                if (!_wanted && _source.isPlaying && current <= 0f) _source.Pause();
                return;
            }
            float seconds = target > current ? FadeInSeconds : _dip > 0f ? DipDownSeconds : FadeOutSeconds;
            float step = Volume / seconds * Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
            _source.volume = Mathf.MoveTowards(current, target, step);
        }
    }
}
