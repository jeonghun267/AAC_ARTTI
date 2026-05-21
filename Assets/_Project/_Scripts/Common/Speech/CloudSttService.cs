using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Artti.Common.Speech
{
    public class CloudSttService : ISttService
    {
        public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

        private readonly string _apiKey;
        private string _device;
        private AudioClip _clip;
        private CancellationTokenSource _activeCts;

        private const int SampleRate = 16000;
        private const int MaxSeconds = 10;
        private const float SilenceRms = 0.005f;
        private const float SilenceWindowSeconds = 1.2f;
        private const float MinSpeakSeconds = 0.4f;

        public CloudSttService(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async UniTask<SttResult> ListenOnceAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning("[CloudSTT] API key 미설정");
                return new SttResult { text = "", confidence = 0f, isEmpty = true };
            }

            // 외부 ct + 내부 Cancel() 둘 다 처리하기 위해 linked CTS
            _activeCts?.Dispose();
            _activeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var linkedCt = _activeCts.Token;

            try
            {
                Debug.Log($"[CloudSTT] start. devices={Microphone.devices.Length}");
                if (!await EnsureMicPermissionAsync(linkedCt))
                {
                    Debug.LogWarning("[CloudSTT] 마이크 권한 거부");
                    return new SttResult { text = "", isEmpty = true };
                }

                var pcm = await RecordPcmAsync(linkedCt);
                if (pcm == null || pcm.Length == 0)
                {
                    Debug.LogWarning($"[CloudSTT] PCM empty (pcm={(pcm==null?"null":pcm.Length.ToString())})");
                    return new SttResult { text = "", isEmpty = true };
                }

                Debug.Log($"[CloudSTT] recognize. pcmBytes={pcm.Length}");
                return await RecognizeAsync(pcm, linkedCt);
            }
            catch (OperationCanceledException)
            {
                return new SttResult { text = "", isEmpty = true };
            }
            finally
            {
                StopMic();
                _activeCts?.Dispose();
                _activeCts = null;
            }
        }

        public void Cancel()
        {
            try { _activeCts?.Cancel(); } catch (ObjectDisposedException) { }
        }

        // 외부에서 미리 권한 받아둘 수 있게 (씬 시작 시점에 호출 권장)
        public static async UniTask<bool> RequestMicPermissionAsync(CancellationToken ct = default)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Debug.Log("[CloudSTT] 권한 이미 허용됨");
                return true;
            }
            Debug.Log("[CloudSTT] 권한 요청 다이얼로그 표시");
            var tcs = new UniTaskCompletionSource<bool>();
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => { Debug.Log("[CloudSTT] 권한 허용"); tcs.TrySetResult(true); };
            callbacks.PermissionDenied += _ => { Debug.LogWarning("[CloudSTT] 권한 거부"); tcs.TrySetResult(false); };
            callbacks.PermissionDeniedAndDontAskAgain += _ => { Debug.LogWarning("[CloudSTT] 권한 영구 거부"); tcs.TrySetResult(false); };
            Permission.RequestUserPermission(Permission.Microphone, callbacks);
            // 다이얼로그 outside-tap 등으로 콜백 안 오는 경우 대비 30초 타임아웃
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeoutCts.CancelAfter(System.TimeSpan.FromSeconds(30));
                try { return await tcs.Task.AttachExternalCancellation(timeoutCts.Token); }
                catch (OperationCanceledException) { return Permission.HasUserAuthorizedPermission(Permission.Microphone); }
            }
#else
            await UniTask.CompletedTask;
            return true;
#endif
        }

        private async UniTask<bool> EnsureMicPermissionAsync(CancellationToken ct)
            => await RequestMicPermissionAsync(ct);

        // 마이크에서 PCM 16bit 추출. VAD는 단순 RMS 기반 — silence가 일정 시간 이어지면 종료.
        private async UniTask<byte[]> RecordPcmAsync(CancellationToken ct)
        {
            // 권한 허용 직후 OS가 디바이스를 비동기로 등록할 수 있으므로 짧게 재시도
            for (int attempt = 0; attempt < 5 && Microphone.devices.Length == 0; attempt++)
            {
                await UniTask.Delay(200, cancellationToken: ct);
            }
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[CloudSTT] 마이크 디바이스 없음 (재시도 5회 실패)");
                return null;
            }
            _device = Microphone.devices[0];
            Debug.Log($"[CloudSTT] mic device='{_device}'");
            _clip = Microphone.Start(_device, loop: false, lengthSec: MaxSeconds, frequency: SampleRate);
            if (_clip == null)
            {
                Debug.LogWarning("[CloudSTT] Microphone.Start 실패");
                return null;
            }
            Debug.Log($"[CloudSTT] mic started. clipSamples={_clip.samples} freq={_clip.frequency}");

            int totalSamples = MaxSeconds * SampleRate;
            int silenceWindowSamples = (int)(SilenceWindowSeconds * SampleRate);
            int minSpeakSamples = (int)(MinSpeakSeconds * SampleRate);
            int lastVoiceSample = -1;
            int prevPos = 0;
            var ring = new float[1024];

            while (!ct.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (!Microphone.IsRecording(_device)) break;

                int pos = Microphone.GetPosition(_device);
                if (pos == prevPos) continue;

                // 새로 들어온 구간 RMS
                int newSamples = pos > prevPos ? pos - prevPos : (totalSamples - prevPos + pos);
                int read = Mathf.Min(newSamples, ring.Length);
                _clip.GetData(ring, Mathf.Max(0, pos - read));
                float sum = 0f;
                for (int i = 0; i < read; i++) sum += ring[i] * ring[i];
                float rms = Mathf.Sqrt(sum / read);

                if (rms > SilenceRms) lastVoiceSample = pos;

                bool spokenLongEnough = lastVoiceSample >= 0 &&
                                        (pos >= minSpeakSamples || pos < prevPos);
                bool silenceLongEnough = lastVoiceSample >= 0 &&
                                         SamplesBetween(lastVoiceSample, pos, totalSamples) > silenceWindowSamples;

                if (spokenLongEnough && silenceLongEnough) break;
                if (pos >= totalSamples - 1) break;
                prevPos = pos;
            }

            int recordedLen = Microphone.GetPosition(_device);
            StopMic();
            if (recordedLen <= 0 || _clip == null) return null;

            var samples = new float[recordedLen];
            _clip.GetData(samples, 0);
            return FloatToLinear16(samples);
        }

        private static int SamplesBetween(int from, int to, int total)
        {
            return to >= from ? to - from : (total - from + to);
        }

        private void StopMic()
        {
            if (!string.IsNullOrEmpty(_device) && Microphone.IsRecording(_device))
                Microphone.End(_device);
            _device = null;
            if (_clip != null) { UnityEngine.Object.Destroy(_clip); _clip = null; }
        }

        private static byte[] FloatToLinear16(float[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                int s = (int)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                bytes[i * 2]     = (byte)(s & 0xFF);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            return bytes;
        }

        private async UniTask<SttResult> RecognizeAsync(byte[] pcm, CancellationToken ct)
        {
            var body = new SttRequest
            {
                config = new SttConfig
                {
                    encoding = "LINEAR16",
                    sampleRateHertz = SampleRate,
                    languageCode = "ko-KR",
                    enableAutomaticPunctuation = true,
                    model = "default"
                },
                audio = new SttAudio { content = Convert.ToBase64String(pcm) }
            };
            string jsonBody = JsonUtility.ToJson(body);

            var url = $"https://speech.googleapis.com/v1/speech:recognize?key={_apiKey}";
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await req.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException) { return new SttResult { text = "", isEmpty = true }; }
            catch (UnityWebRequestException e)
            {
                Debug.LogWarning($"[CloudSTT] HTTP {(int)e.ResponseCode}: {e.Error}\nbody: {e.Text}");
                return new SttResult { text = "", isEmpty = true };
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CloudSTT] {req.error}\n{req.downloadHandler.text}");
                return new SttResult { text = "", isEmpty = true };
            }

            var resp = JsonUtility.FromJson<SttResponse>(req.downloadHandler.text);
            if (resp?.results == null || resp.results.Length == 0 ||
                resp.results[0].alternatives == null || resp.results[0].alternatives.Length == 0)
            {
                return new SttResult { text = "", isEmpty = true };
            }
            var top = resp.results[0].alternatives[0];
            return new SttResult { text = top.transcript ?? "", confidence = top.confidence, isEmpty = string.IsNullOrEmpty(top.transcript) };
        }

        [Serializable] class SttRequest  { public SttConfig config; public SttAudio audio; }
        [Serializable] class SttConfig   { public string encoding; public int sampleRateHertz; public string languageCode; public bool enableAutomaticPunctuation; public string model; }
        [Serializable] class SttAudio    { public string content; }
        [Serializable] class SttResponse { public SttResultEntry[] results; }
        [Serializable] class SttResultEntry { public SttAlternative[] alternatives; }
        [Serializable] class SttAlternative { public string transcript; public float confidence; }
    }
}
