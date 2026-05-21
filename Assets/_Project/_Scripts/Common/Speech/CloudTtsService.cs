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
                voice = new TtsVoice { languageCode = "ko-KR", name = "ko-KR-Standard-A" },
                audioConfig = new TtsAudioConfig { audioEncoding = "LINEAR16", sampleRateHertz = SampleRate }
            };
            var jsonBody = JsonUtility.ToJson(req);

            var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={_apiKey}";
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

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

        public void StopAll() => _audioSource?.Stop();
        public void SetVoice(string voiceName) { }

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
        [Serializable] class TtsAudioConfig{ public string audioEncoding; public int sampleRateHertz; }
        [Serializable] class TtsResponse   { public string audioContent; }
    }
}
