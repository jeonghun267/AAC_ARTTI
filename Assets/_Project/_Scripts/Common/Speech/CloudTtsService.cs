using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Artti.Common.Speech
{
    public class CloudTtsService : ITtsService
    {
        public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

        private readonly string _apiKey;
        private readonly AudioSource _audioSource;
        private const int SampleRate = 24000;
        // 변경점: 응답 없는 요청이 무한정 걸려 있지 않게 한다 (기본값 0 = 제한 없음).
        //   훈련 씬은 점원 음성이 끝나길 기다렸다가 마이크를 여므로 걸린 요청이 듣기를 막는다.
        private const int RequestTimeoutSeconds = 10;
        private string _voiceName = "ko-KR-Standard-A"; // SetVoice로 교체 (훈련 모드별 점원 목소리)
        private float _speakingRate = 1f;                // SetSpeakingRate로 교체 (프로필 말 빠르기)

        public CloudTtsService(string apiKey, AudioSource audioSource)
        {
            _apiKey = apiKey;
            _audioSource = audioSource;
            var tail = string.IsNullOrEmpty(apiKey) ? "(empty)" : apiKey.Substring(System.Math.Max(0, apiKey.Length - 4));
            Debug.Log($"[CloudTTS] init. keyTail=...{tail} len={(apiKey?.Length ?? 0)}");
        }

        public async UniTask SpeakAsync(string text, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_apiKey)) return;

            var req = new TtsRequest
            {
                input = new TtsInput { text = text },
                voice = new TtsVoice { languageCode = "ko-KR", name = _voiceName },
                audioConfig = new TtsAudioConfig { audioEncoding = "LINEAR16", sampleRateHertz = SampleRate, speakingRate = _speakingRate }
            };
            var jsonBody = JsonUtility.ToJson(req);

            var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={_apiKey}";
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = RequestTimeoutSeconds;

            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { return; }
            catch (UnityWebRequestException e)
            {
                Debug.LogWarning($"[CloudTTS] HTTP {(int)e.ResponseCode}: {e.Error}\nbody: {e.Text}");
                return;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CloudTTS] HTTP error: {request.error}\n{request.downloadHandler.text}");
                return;
            }

            var resp = JsonUtility.FromJson<TtsResponse>(request.downloadHandler.text);
            if (resp == null || string.IsNullOrEmpty(resp.audioContent))
            {
                Debug.LogWarning("[CloudTTS] empty audioContent");
                return;
            }

            byte[] pcm = Convert.FromBase64String(resp.audioContent);
            var clip = PcmToAudioClip(pcm, SampleRate);
            if (clip != null && _audioSource != null)
            {
                _audioSource.clip = clip;
                _audioSource.Play();
            }
        }

        // 변경점: ?. 는 파괴된 Unity 오브젝트를 걸러내지 못한다(씬 종료 시 OnDestroy 순서에 따라 AudioSource가 먼저 파괴됨).
        //   Unity의 == 연산자로 확인해야 MissingReferenceException이 나지 않는다.
        public void StopAll()
        {
            if (_audioSource != null) _audioSource.Stop();
        }
        public void SetVoice(string voiceName)
        {
            if (!string.IsNullOrWhiteSpace(voiceName)) _voiceName = voiceName;
        }
        // 변경점: Cloud TTS speakingRate 허용 범위는 0.25~4.0 (1.0 = 기본 속도). 범위 밖이면 요청이 400으로 실패한다.
        public void SetSpeakingRate(float rate)
        {
            _speakingRate = Mathf.Clamp(rate, 0.25f, 4f);
        }

        static AudioClip PcmToAudioClip(byte[] pcm16le, int sampleRate)
        {
            // LINEAR16은 raw일 수도 있고 WAV 헤더(44바이트) 포함일 수도. WAV 헤더 검출 후 스킵.
            int offset = 0;
            if (pcm16le.Length > 44 && pcm16le[0] == (byte)'R' && pcm16le[1] == (byte)'I' && pcm16le[2] == (byte)'F' && pcm16le[3] == (byte)'F')
                offset = 44;

            int sampleCount = (pcm16le.Length - offset) / 2;
            if (sampleCount <= 0) return null;
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short s = (short)(pcm16le[offset + i * 2] | (pcm16le[offset + i * 2 + 1] << 8));
                samples[i] = s / 32768f;
            }
            var clip = AudioClip.Create("tts", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        [Serializable] class TtsRequest    { public TtsInput input; public TtsVoice voice; public TtsAudioConfig audioConfig; }
        [Serializable] class TtsInput      { public string text; }
        [Serializable] class TtsVoice      { public string languageCode; public string name; }
        [Serializable] class TtsAudioConfig{ public string audioEncoding; public int sampleRateHertz; public float speakingRate; }
        [Serializable] class TtsResponse   { public string audioContent; }
    }
}
