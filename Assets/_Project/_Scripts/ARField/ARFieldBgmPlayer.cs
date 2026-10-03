using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.ARField
{
    // AR 현장모드 배경음. BgmZone이 붙은 패널(사진 추가하기 HomePanel, 결과 ResultPanel)이 하나라도 켜져 있으면 들리고,
    // 모두 꺼지면(도움/인사/주문 화면 등) 소리를 줄여 멈춘다. 다시 켜지면 멈췄던 곳부터 이어서 들린다(반복 재생).
    // 변경점: PauseForSpeech(결과 화면 '설명 듣기' 버튼이 부름)는 음성 읽기가 끝날 때까지 잠깐 멈췄다가 다시 들리게 한다.
    //   볼륨/페이드 시간만 여기서 정한다. 클립과 연결은 에디터 스크립트(ARFieldBgmSetup)가 채운다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class ARFieldBgmPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [Tooltip("원래 소리의 2/3")]
        [SerializeField, Range(0f, 1f)] private float volume = 2f / 3f;
        [SerializeField] private float fadeSeconds = 0.6f;

        [Header("설명 듣기 동안 멈춤")]
        [SerializeField] private TTSManager tts;
        [Tooltip("멈출 때 소리를 줄이는 시간(초)")]
        [SerializeField] private float speechFadeSeconds = 0.25f;
        [Tooltip("누른 뒤 읽기가 시작되기를 기다리는 최대 시간(초). 이 안에 시작하지 않으면(에디터 등) 다시 들린다")]
        [SerializeField] private float speechStartWait = 1.5f;
        [Tooltip("아무리 길어도 이 시간(초)이 지나면 다시 들린다")]
        [SerializeField] private float speechMaxHold = 60f;

        private const int SpeechPollMs = 200;

        private AudioSource _source;
        private int _zones;
        private bool _speaking;
        private bool _audible;
        private bool _paused;   // 우리가 Pause로 멈춘 상태(이어서 들려야 함)
        private CancellationTokenSource _fadeCts;
        private CancellationTokenSource _speechCts;

        private AudioSource Source
        {
            get
            {
                if (_source == null)
                {
                    _source = GetComponent<AudioSource>();
                    _source.playOnAwake = false;
                    _source.loop = true;
                    _source.clip = clip;
                    _source.volume = 0f;
                }
                return _source;
            }
        }

        public void EnterZone()
        {
            _zones++;
            Apply(fadeSeconds);
        }

        public void ExitZone()
        {
            if (_zones == 0) return;
            _zones--;
            Apply(fadeSeconds);
        }

        // 변경점: '설명 듣기'를 누르면 부른다. 읽기가 끝나면(또는 시작하지 않으면) 다시 들린다. 다시 누르면 처음부터 기다린다.
        public void PauseForSpeech()
        {
            _speechCts?.Cancel();
            _speechCts?.Dispose();
            _speechCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            HoldForSpeechAsync(_speechCts.Token).Forget();
        }

        private async UniTaskVoid HoldForSpeechAsync(CancellationToken ct)
        {
            _speaking = true;
            Apply(speechFadeSeconds);

            float waited = 0f;
            while (waited < speechStartWait && !IsSpeaking())
            {
                if (await UniTask.Delay(SpeechPollMs, true, PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
                waited += SpeechPollMs / 1000f;
            }
            float held = 0f;
            while (held < speechMaxHold && IsSpeaking())
            {
                if (await UniTask.Delay(SpeechPollMs, true, PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
                held += SpeechPollMs / 1000f;
            }

            _speaking = false;
            Apply(fadeSeconds);
        }

        private bool IsSpeaking() => tts != null && tts.IsSpeaking;

        // 변경점: 씬을 불러올 때 패널(BgmZone)이 이 오브젝트보다 먼저 켜지면, 아직 켜지지 않은 AudioSource의 Play가 무시돼
        //   소리가 끝까지 안 났다. 켜지기 전에는 기다렸다가 켜질 때 다시 맞춘다.
        private void OnEnable() => Apply(fadeSeconds);

        // 들려야 하는지(켜진 패널이 있고 읽는 중이 아님)가 바뀌었을 때만 소리를 키우거나 줄인다
        private void Apply(float seconds)
        {
            if (!isActiveAndEnabled) return;   // 켜지기 전(씬 불러오는 중)이면 OnEnable에서 다시 맞춘다
            bool want = _zones > 0 && !_speaking;
            if (want == _audible) return;
            _audible = want;
            StartFade(want, seconds);
        }

        private void StartFade(bool play, float seconds)
        {
            if (clip == null) return;
            _fadeCts?.Cancel();
            _fadeCts?.Dispose();
            _fadeCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            if (play)
            {
                // 우리가 멈춘 것은 이어서, 아니면(처음이거나 재생이 안 되고 있으면) 처음부터
                if (_paused)
                {
                    Source.UnPause();
                    _paused = false;
                }
                if (!Source.isPlaying)
                {
                    Source.Play();
                    Debug.Log($"[ARFieldBgm] 재생 시작: {clip.name} ({clip.length:F0}초), 볼륨 {volume:F2}");
                }
            }
            FadeAsync(play ? volume : 0f, !play, seconds, _fadeCts.Token).Forget();
        }

        private async UniTaskVoid FadeAsync(float target, bool pauseAtEnd, float seconds, CancellationToken ct)
        {
            var source = Source;
            float start = source.volume;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / seconds));
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
            }
            source.volume = target;
            if (pauseAtEnd)
            {
                source.Pause();
                _paused = true;
            }
        }

        private void OnDestroy()
        {
            _fadeCts?.Cancel();
            _fadeCts?.Dispose();
            _fadeCts = null;
            _speechCts?.Cancel();
            _speechCts?.Dispose();
            _speechCts = null;
        }
    }
}
