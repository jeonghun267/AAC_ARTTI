using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.Common.Speech
{
    public enum RecordStatus
    {
        Speech,          // 발화를 녹음함
        NoSpeech,        // 마이크는 동작했지만 말이 없었음
        MicUnavailable   // 마이크 디바이스가 없거나 입력이 들어오지 않음
    }

    // 마이크에서 "한 번의 발화"를 녹음해 PCM float 샘플로 돌려준다. 오디오를 직접 올려야 하는 서버 STT 경로용.
    // 발화 판정은 UtteranceDetector가 하고, 여기서는 Microphone 입출력과 20ms 단위 분할만 한다.
    // 프레임 길이와 무관하게 같은 판정이 나오도록 들어온 샘플을 고정 길이(20ms) 조각으로 나눠 넘긴다.
    // Microphone API는 메인 스레드 전용이므로 PlayerLoop(Update) 타이밍으로 폴링한다.
    public sealed class MicrophoneUtteranceRecorder
    {
        public const int DefaultSampleRate = 16000;

        private const float LeadInSeconds = 0.3f;
        private const float TailSeconds = 0.4f;

        // 마이크 입력이 처음부터 안 들어오거나 도중에 멈췄을 때 기다리는 한도(실시간 초)
        private const float NoDataTimeoutSeconds = 2f;

        private readonly int _requestedRate;
        private float[] _hopBuffer = Array.Empty<float>();
        private int _session; // 겹친 호출에서 이전 녹음의 정리가 새 녹음의 마이크를 끄지 않게 구분한다

        // 마지막 녹음의 실제 샘플레이트. 플랫폼이 요청값과 다른 레이트를 줄 수 있어 WAV 헤더에는 이 값을 쓴다.
        public int SampleRate { get; private set; }

        public MicrophoneUtteranceRecorder(int sampleRate = DefaultSampleRate)
        {
            _requestedRate = sampleRate;
            SampleRate = sampleRate;
        }

        public static bool HasDevice => Microphone.devices.Length > 0;

        // 취소되면 OperationCanceledException.
        public async UniTask<(RecordStatus status, float[] samples)> RecordAsync(CancellationToken ct)
        {
            string device = await WaitForDeviceAsync(ct);
            if (device == null)
            {
                Debug.LogWarning("[MicRecorder] 마이크 디바이스 없음");
                return (RecordStatus.MicUnavailable, null);
            }

            int session = ++_session;
            AudioClip clip = Microphone.Start(device, false, UtteranceDetector.MaxSeconds, _requestedRate);
            if (clip == null)
            {
                Debug.LogWarning("[MicRecorder] Microphone.Start 실패");
                return (RecordStatus.MicUnavailable, null);
            }

            try
            {
                SampleRate = clip.frequency;
                return await CaptureUtteranceAsync(device, clip, ct);
            }
            finally
            {
                if (session == _session && Microphone.IsRecording(device)) Microphone.End(device);
                UnityEngine.Object.Destroy(clip);
            }
        }

        // 권한 허용 직후 OS가 디바이스를 늦게 등록할 수 있어 잠깐 재시도한다.
        private static async UniTask<string> WaitForDeviceAsync(CancellationToken ct)
        {
            for (int attempt = 0; attempt < 5 && Microphone.devices.Length == 0; attempt++)
                await UniTask.Delay(200, cancellationToken: ct);
            return Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        }

        private async UniTask<(RecordStatus, float[])> CaptureUtteranceAsync(string device, AudioClip clip, CancellationToken ct)
        {
            int rate = clip.frequency;
            int total = clip.samples;
            int hop = UtteranceDetector.HopSamples(rate);
            if (_hopBuffer.Length != hop) _hopBuffer = new float[hop];

            var detector = new UtteranceDetector(rate);
            int processed = 0; // 판정에 넘긴 샘플 끝 (hop 경계)
            int seenPos = 0;   // 마이크가 써 넣은 샘플 끝
            float startTime = Time.realtimeSinceStartup;
            float lastAdvance = startTime;
            bool ended = false;

            while (!ended)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                float now = Time.realtimeSinceStartup;

                // loop:false 녹음은 끝에 닿으면 멈추고, 멈춘 뒤 GetPosition은 0을 돌려준다.
                bool recording = Microphone.IsRecording(device);
                int pos = recording ? Microphone.GetPosition(device) : total;

                if (pos <= seenPos)
                {
                    if (!recording) break;
                    if (now - lastAdvance > NoDataTimeoutSeconds ||
                        now - startTime > UtteranceDetector.MaxSeconds + NoDataTimeoutSeconds)
                    {
                        // 처음부터 입력이 없으면 마이크 문제, 도중에 멈췄으면(헤드셋 분리 등) 받은 데까지 쓴다
                        if (seenPos == 0)
                        {
                            Debug.LogWarning("[MicRecorder] 마이크 입력이 들어오지 않음");
                            return (RecordStatus.MicUnavailable, null);
                        }
                        Debug.LogWarning("[MicRecorder] 녹음 도중 마이크 입력이 멈춤 — 받은 데까지 사용");
                        break;
                    }
                    continue;
                }

                seenPos = pos;
                lastAdvance = now;

                while (processed + hop <= pos)
                {
                    var step = detector.Step(Rms(clip, processed, hop, total), processed, processed + hop);
                    processed += hop;
                    if (step == UtteranceDetector.StepResult.NoSpeech) return (RecordStatus.NoSpeech, null);
                    if (step == UtteranceDetector.StepResult.Ended) { ended = true; break; }
                }

                if (!ended && (!recording || pos >= total))
                {
                    // 녹음 끝 — 남은 조각까지 판정
                    if (pos > processed) detector.Step(Rms(clip, processed, pos - processed, total), processed, pos);
                    break;
                }
            }

            if (!detector.HasUtterance) return (RecordStatus.NoSpeech, null);

            int start = Math.Max(0, detector.FirstVoice - (int)(LeadInSeconds * rate));
            int end = Math.Min(Math.Min(seenPos, total), detector.LastVoice + (int)(TailSeconds * rate));
            if (end <= start) return (RecordStatus.NoSpeech, null);

            var samples = new float[end - start];
            clip.GetData(samples, start);
            return (RecordStatus.Speech, samples);
        }

        // [start, start+count) RMS. GetData는 배열 길이만큼 읽으므로 클립 끝을 넘지 않게 읽기 시작점을 당긴다.
        private float Rms(AudioClip clip, int start, int count, int total)
        {
            int readFrom = Math.Max(0, Math.Min(start, total - _hopBuffer.Length));
            clip.GetData(_hopBuffer, readFrom);

            int offset = start - readFrom;
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                float s = _hopBuffer[offset + i];
                sum += s * s;
            }
            return count > 0 ? Mathf.Sqrt(sum / count) : 0f;
        }
    }

    // 20ms 조각 RMS만 보고 한 번의 발화가 어디서 시작해 끝나는지 판정한다 (UnityEngine 비의존, 에디터 없이 시뮬레이션 가능).
    // 파라미터는 조용한 방·백색 소음·녹음 중 시작되는 소음·여러 사람 말소리 배경·기침·팝·첫 단어를 크게 말하기·
    // 한 음절 대답·말하는 도중 마이크 열림을 30/60/120fps와 프레임 끊김까지 넣어 시뮬레이션해 정했다.
    //   - 배경 소음 레벨: 조용한 조각이면 적당히 빨리 내려가고, 소음이 이어지면 천천히 올라간다.
    //     최근 SlidingWindowSeconds 동안의 최저 레벨 아래로는 내려가지 않게 끌어올린다 (말에는 음절 사이 틈이 있어
    //     이 최저값은 배경 소음 쪽이다). 마이크를 연 뒤 에어컨·음악이 시작돼도 소음을 말로 착각하지 않는다.
    //   - 말로 센 조각이 전부 지금 임계값 아래가 되면 그건 소음이었으므로 처음부터 다시 기다린다.
    //   - 발화가 자리 잡은 뒤에는 최대 레벨(0.1초로 매끈하게, 서서히 감쇠, 0.2초에 두 배 이상은 못 오름)의
    //     EndHysteresis배보다 작으면 조용한 것으로 본다. 사용자는 마이크 가까이서 말하므로 뒤쪽 말소리 배경보다 크다.
    //     최대 레벨이 천천히만 오르게 한 것은 말하는 도중 기침 한 번에 나머지 문장이 잘리지 않게 하기 위해서다.
    //   - 말이 시작되지 않은 채 NoSpeechTimeoutSeconds가 지나면 NoSpeech. 무음을 서버에 보내면 Whisper 계열이
    //     "시청해주셔서 감사합니다" 같은 문장을 지어낸다.
    //   - 말이 시작된 뒤 SilenceEndSeconds 동안 조용하면 끝. 끊어 말하는 사용자를 위해 짧게 잡지 않는다.
    //     지금까지 들은 소리가 한 음절·기침 정도로 짧으면(ShortBurstSeconds 미만) ShortBurstSilenceSeconds까지 더 기다린다.
    //     기침 뒤 잠시 쉬었다가 대답하는 경우 대답을 잃지 않기 위해서다 (짧은 대답은 그만큼 늦게 끝난다).
    //   - 탭 소리처럼 짧은 소리(MinVoiceSeconds 미만)는 발화로 치지 않고 계속 기다린다.
    public sealed class UtteranceDetector
    {
        public enum StepResult { Continue, Ended, NoSpeech }

        public const int MaxSeconds = 10;
        private const float HopSeconds = 0.02f;
        private const float NoSpeechTimeoutSeconds = 8f;
        private const float SilenceEndSeconds = 2f;
        private const float ShortBurstSeconds = 0.35f;
        private const float ShortBurstSilenceSeconds = 3.5f;
        private const float MinVoiceSeconds = 0.2f;
        private const float OnsetSeconds = 0.05f;          // 이만큼 연속으로 커야 발화 시작으로 본다

        // 음성 판정 임계값 = 배경 소음 레벨 × NoiseMultiplier (MinThreshold~MaxThreshold)
        private const float MinThreshold = 0.005f;
        private const float MaxThreshold = 0.03f;
        private const float NoiseMultiplier = 3f;

        // 배경 소음 추적 속도 (60fps 한 프레임 기준 비율). 조용해지면 빨리, 시끄러워지면 천천히 따라간다.
        private const float FloorFallPerFrame = 0.2f;
        private const float FloorRisePerFrame = 0.02f;

        private const float EndHysteresis = 0.4f;
        private const float PeakHalfLifeSeconds = 2f;
        private const float PeakDoublingSeconds = 0.2f;     // 최대 레벨이 이보다 빨리 두 배가 되지 못하게 한다
        private const float EnvelopeSeconds = 0.1f;         // 순간 잡음(팝·부딪힘) 하나로 최대값이 튀지 않게 하는 평활 시간

        // 최근 구간 최저 레벨로 배경 소음 추정을 끌어올린다 (마이크를 연 직후 WarmUpSeconds는 제외)
        private const float SlidingWindowSeconds = 1.5f;
        private const float WarmUpSeconds = 0.1f;
        private const float HeadSeconds = 0.1f;             // "열릴 때 이미 말하는 중"인지 볼 앞부분 (첫 조각이 음절 틈일 수 있어 여러 조각)

        public static int HopSamples(int rate) => Math.Max(1, (int)(rate * HopSeconds));

        private readonly int _rate;
        private readonly int _noSpeechLimit;
        private readonly int _silenceEnd;
        private readonly int _shortBurst;
        private readonly int _shortBurstSilence;
        private readonly int _minVoice;
        private readonly int _onset;
        private readonly int _warmUp;
        private readonly int _head;
        private readonly int _slidingWindow;
        private readonly float[] _history;
        private readonly int[] _historyPos;

        private int _historyStart;
        private int _historyCount;
        private float _noiseFloor = -1f;
        private float _firstLevel;
        private bool _headChecked;
        private float _peak;
        private float _envelope;
        private float _maxVoiced;
        private int _loudRunSamples;
        private int _loudRunStart = -1;

        public int FirstVoice { get; private set; } = -1;
        public int LastVoice { get; private set; } = -1;
        public int VoicedSamples { get; private set; }
        public bool HasUtterance => FirstVoice >= 0 && VoicedSamples >= _minVoice;

        public UtteranceDetector(int rate)
        {
            _rate = rate;
            _noSpeechLimit = (int)(NoSpeechTimeoutSeconds * rate);
            _silenceEnd = (int)(SilenceEndSeconds * rate);
            _shortBurst = (int)(ShortBurstSeconds * rate);
            _shortBurstSilence = (int)(ShortBurstSilenceSeconds * rate);
            _minVoice = (int)(MinVoiceSeconds * rate);
            _onset = (int)(OnsetSeconds * rate);
            _warmUp = (int)(WarmUpSeconds * rate);
            _head = (int)(HeadSeconds * rate);
            _slidingWindow = (int)(SlidingWindowSeconds * rate);
            int capacity = _slidingWindow / HopSamples(rate) + 2;
            _history = new float[capacity];
            _historyPos = new int[capacity];
        }

        // [start, end) 구간의 RMS를 넘긴다. 보통 end - start는 HopSamples, 녹음 끝에서만 더 짧다.
        public StepResult Step(float rms, int start, int end)
        {
            int n = end - start;
            if (n <= 0) return StepResult.Continue;

            if (_noiseFloor < 0f) _noiseFloor = rms;
            if (end <= _head && rms > _firstLevel) _firstLevel = rms;

            if (end > _warmUp) PushHistory(rms, end);

            float threshold = Clamp(_noiseFloor * NoiseMultiplier, MinThreshold, MaxThreshold);

            _envelope += (rms - _envelope) * (1f - (float)Math.Exp(-n / (EnvelopeSeconds * _rate)));
            _peak *= (float)Math.Pow(0.5, n / (PeakHalfLifeSeconds * _rate));
            float gate = VoicedSamples < _minVoice ? threshold : Math.Max(threshold, EndHysteresis * _peak);
            bool loud = rms > gate;

            float frames = n / (_rate / 60f);
            if (rms < _noiseFloor) _noiseFloor += (rms - _noiseFloor) * PerFrames(FloorFallPerFrame, frames);
            else if (rms <= threshold) _noiseFloor += (rms - _noiseFloor) * PerFrames(FloorRisePerFrame, frames);

            if (loud)
            {
                if (_loudRunSamples == 0)
                {
                    _loudRunStart = start;
                    // 처음부터로 잡았던 짧은 소리(열릴 때의 점원 음성 끝자락 등)는 한참 뒤 새 소리와 잇지 않는다
                    if (FirstVoice == 0 && VoicedSamples < _minVoice && start >= _slidingWindow) ResetUtterance();
                }
                _loudRunSamples += n;
            }
            else
            {
                _loudRunSamples = 0;
            }

            if (loud && (FirstVoice >= 0 || _loudRunSamples >= _onset))
            {
                if (FirstVoice < 0)
                {
                    // 마이크가 열릴 때 이미 말하고 있었다면(첫 조각이 지금 기준으로 큰 소리이고, 이 발화가 열린 직후
                    // SlidingWindowSeconds 안에 시작됐다면) 처음부터 발화로 본다. 한참 뒤의 발화에는 적용하지 않는다
                    // (열릴 때의 딸깍 소리 하나로 몇 초짜리 앞부분 무음까지 올라가지 않게).
                    FirstVoice = !_headChecked && _firstLevel > threshold && _loudRunStart < _slidingWindow ? 0 : _loudRunStart;
                    _headChecked = true;
                    VoicedSamples += _loudRunSamples - n; // 시작 판정에 쓰인 앞 조각들도 발화 길이에 넣는다
                }
                LastVoice = end;
                VoicedSamples += n;
                if (_envelope > _peak)
                {
                    float limit = Math.Max(_peak, threshold) * (float)Math.Pow(2.0, n / (PeakDoublingSeconds * _rate));
                    _peak = Math.Min(_envelope, limit);
                }
                if (rms > _maxVoiced) _maxVoiced = rms;
            }

            // 말로 센 구간이 전부 지금 임계값 아래 — 소음이었다
            bool wasNoise = FirstVoice >= 0 && _maxVoiced <= threshold;

            // 짧은 소리 연장 대기는 처음부터로 잡은 소리(열릴 때의 점원 음성 끝자락일 수 있음)에는 쓰지 않는다
            bool shortBurst = VoicedSamples >= _minVoice && VoicedSamples < _shortBurst && FirstVoice != 0;
            int silenceLimit = shortBurst ? _shortBurstSilence : _silenceEnd;

            // 연장 대기 중(기본 대기는 이미 지남)에 배경 소음이 올라 소음으로 보이게 된 것은 끝난 발화로 친다
            if (wasNoise && LastVoice >= 0 && end - LastVoice > _silenceEnd && VoicedSamples >= _minVoice)
                return StepResult.Ended;

            // 짧은 잡음이었거나 소음이었으면 발화로 치지 않고 처음부터 다시 기다린다
            if (wasNoise || (LastVoice >= 0 && end - LastVoice > silenceLimit))
            {
                if (!wasNoise && VoicedSamples >= _minVoice) return StepResult.Ended;
                ResetUtterance();
            }

            if (FirstVoice < 0 && end > _noSpeechLimit) return StepResult.NoSpeech;
            return StepResult.Continue;
        }

        private void ResetUtterance()
        {
            // 열린 직후에 시작됐던 발화가 무효가 되면, 이어지는 진짜 발화에 다시 처음부터 규칙을 적용할 수 있게 한다
            if (FirstVoice >= 0 && FirstVoice < _slidingWindow) _headChecked = false;
            FirstVoice = -1;
            LastVoice = -1;
            VoicedSamples = 0;
            _peak = 0f;
            _maxVoiced = 0f;
        }

        private void PushHistory(float rms, int end)
        {
            if (_historyCount == _history.Length)
            {
                _historyStart = (_historyStart + 1) % _history.Length;
                _historyCount--;
            }
            int slot = (_historyStart + _historyCount) % _history.Length;
            _history[slot] = rms;
            _historyPos[slot] = end;
            _historyCount++;

            while (_historyCount > 0 && _historyPos[_historyStart] < end - _slidingWindow)
            {
                _historyStart = (_historyStart + 1) % _history.Length;
                _historyCount--;
            }

            if (_historyCount > 0 && end - _historyPos[_historyStart] >= _slidingWindow / 2)
            {
                float min = float.MaxValue;
                for (int i = 0; i < _historyCount; i++)
                {
                    float v = _history[(_historyStart + i) % _history.Length];
                    if (v < min) min = v;
                }
                if (min > _noiseFloor) _noiseFloor = min;
            }
        }

        // 프레임 기준 추적 비율을 실제 경과 샘플 수에 맞춘다 (조각 길이가 달라도 같은 속도)
        private static float PerFrames(float perFrame, float frames) => 1f - (float)Math.Pow(1f - perFrame, frames);

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
