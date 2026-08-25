using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Artti.Common.Speech
{
    // Android 시스템 SpeechRecognizer 래퍼. Google 음성 서비스가 단말에 설치돼 있어야 동작.
    // 인터넷 의존: 단말/언어팩에 따라 일부 오프라인 가능. API 키 불필요.
    public class AndroidNativeSttService : ISttService
    {
        public bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        // 침묵 타임아웃(ms). 발달장애인 사용자의 느린/끊긴 발화를 끝까지 듣기 위해 기본값보다 길게.
        private const int CompleteSilenceMs = 3000;          // 발화 후 종료 판정까지의 침묵
        private const int PossiblyCompleteSilenceMs = 3000;  // 끝났을 수 있다고 보는 침묵
        private const int MinimumInputMs = 5000;             // 최소 인식 유지 시간(말 시작 지연 허용)

        private AndroidJavaObject _activity;
        private AndroidJavaObject _recognizer;
        private UniTaskCompletionSource<SttResult> _tcs;
        private readonly object _tcsLock = new object();

        public AndroidNativeSttService()
        {
            if (!IsAvailable) return;
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                _activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        public async UniTask<SttResult> ListenOnceAsync(CancellationToken ct = default)
        {
            if (!IsAvailable)
            {
                Debug.LogWarning("[NativeSTT] Android 빌드에서만 동작 — 빈 결과 반환");
                return new SttResult { text = "", isEmpty = true };
            }

            if (!await RequestMicPermissionAsync(ct))
            {
                Debug.LogWarning("[NativeSTT] 마이크 권한 거부");
                return new SttResult { text = "", isEmpty = true };
            }

            UniTaskCompletionSource<SttResult> tcs;
            lock (_tcsLock)
            {
                _tcs?.TrySetResult(new SttResult { text = "", isEmpty = true });
                _tcs = new UniTaskCompletionSource<SttResult>();
                tcs = _tcs;
            }

            using (ct.Register(Cancel))
            {
                RunOnUi(StartListeningOnUi);
                try
                {
                    return await tcs.Task;
                }
                finally
                {
                    RunOnUi(DestroyRecognizerOnUi);
                }
            }
        }

        public void Cancel()
        {
            RunOnUi(() =>
            {
                try { _recognizer?.Call("cancel"); } catch { }
            });
            lock (_tcsLock)
            {
                _tcs?.TrySetResult(new SttResult { text = "", isEmpty = true });
            }
        }

        private void StartListeningOnUi()
        {
            try
            {
                using (var srClass = new AndroidJavaClass("android.speech.SpeechRecognizer"))
                {
                    bool ok = srClass.CallStatic<bool>("isRecognitionAvailable", _activity);
                    if (!ok)
                    {
                        Debug.LogWarning("[NativeSTT] 단말에 SpeechRecognizer 미지원 (Google 앱 또는 음성 서비스 비활성)");
                        Resolve(new SttResult { text = "", isEmpty = true });
                        return;
                    }
                    _recognizer = srClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", _activity);
                }
                _recognizer.Call("setRecognitionListener", new RecogListener(this));

                using (var intent = new AndroidJavaObject("android.content.Intent", "android.speech.action.RECOGNIZE_SPEECH"))
                {
                    intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE", "ko-KR");
                    intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE_MODEL", "free_form");
                    intent.Call<AndroidJavaObject>("putExtra", "calling_package", _activity.Call<string>("getPackageName"));
                    intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.MAX_RESULTS", 1);

                    // 변경점: 침묵 타임아웃 extra 추가. 기본값이 너무 짧아 천천히/끊어 말하는
                    //         발달장애인 사용자의 발화가 중간에 잘려 ERROR_NO_MATCH/SPEECH_TIMEOUT으로
                    //         빠지던 문제 완화. (OEM recognizer는 무시할 수 있는 hint값)
                    // 말이 끝났다고 보기까지 기다리는 침묵 길이
                    intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.SPEECH_INPUT_COMPLETE_SILENCE_LENGTH_MILLIS", CompleteSilenceMs);
                    // 끝났을 "수도" 있다고 보는 침묵 길이
                    intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.SPEECH_INPUT_POSSIBLY_COMPLETE_SILENCE_LENGTH_MILLIS", PossiblyCompleteSilenceMs);
                    // 이 시간 전에는 침묵만으로 종료하지 않음(말 시작이 늦어도 기다림)
                    intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.SPEECH_INPUT_MINIMUM_LENGTH_MILLIS", MinimumInputMs);

                    _recognizer.Call("startListening", intent);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NativeSTT] start 실패: {e.Message}");
                Resolve(new SttResult { text = "", isEmpty = true });
            }
        }

        private void DestroyRecognizerOnUi()
        {
            try { _recognizer?.Call("destroy"); } catch { }
            _recognizer = null;
        }

        private void Resolve(SttResult result)
        {
            lock (_tcsLock)
            {
                _tcs?.TrySetResult(result);
            }
        }

        private void RunOnUi(Action action)
        {
            if (_activity == null) { action?.Invoke(); return; }
            _activity.Call("runOnUiThread", new AndroidJavaRunnable(action));
        }

        public static async UniTask<bool> RequestMicPermissionAsync(CancellationToken ct = default)
            => await CloudSttService.RequestMicPermissionAsync(ct);

        // android.speech.SpeechRecognizer 에러 코드 → 사람 친화 텍스트
        private static string ErrorMessage(int code)
        {
            switch (code)
            {
                case 1: return "ERROR_NETWORK_TIMEOUT";
                case 2: return "ERROR_NETWORK";
                case 3: return "ERROR_AUDIO";
                case 4: return "ERROR_SERVER";
                case 5: return "ERROR_CLIENT";
                case 6: return "ERROR_SPEECH_TIMEOUT";
                case 7: return "ERROR_NO_MATCH";
                case 8: return "ERROR_RECOGNIZER_BUSY";
                case 9: return "ERROR_INSUFFICIENT_PERMISSIONS";
                case 10: return "ERROR_TOO_MANY_REQUESTS";
                case 11: return "ERROR_SERVER_DISCONNECTED";
                case 12: return "ERROR_LANGUAGE_NOT_SUPPORTED";
                case 13: return "ERROR_LANGUAGE_UNAVAILABLE";
                default: return $"UNKNOWN({code})";
            }
        }

        private class RecogListener : AndroidJavaProxy
        {
            private readonly AndroidNativeSttService _owner;
            public RecogListener(AndroidNativeSttService owner) : base("android.speech.RecognitionListener") { _owner = owner; }

            public void onReadyForSpeech(AndroidJavaObject p) { }
            public void onBeginningOfSpeech() { }
            public void onRmsChanged(float rms) { }
            public void onBufferReceived(byte[] buf) { }
            public void onEndOfSpeech() { }
            public void onPartialResults(AndroidJavaObject p) { }
            public void onEvent(int eventType, AndroidJavaObject p) { }

            public void onError(int error)
            {
                Debug.LogWarning($"[NativeSTT] {ErrorMessage(error)}");
                _owner.Resolve(new SttResult { text = "", isEmpty = true });
            }

            public void onResults(AndroidJavaObject bundle)
            {
                string top = "";
                float conf = 0f;
                try
                {
                    var list = bundle.Call<AndroidJavaObject>("getStringArrayList", "results_recognition");
                    if (list != null)
                    {
                        int n = list.Call<int>("size");
                        if (n > 0) top = list.Call<string>("get", 0);
                    }
                    try
                    {
                        var confArr = bundle.Call<float[]>("getFloatArray", "confidence_scores");
                        if (confArr != null && confArr.Length > 0) conf = confArr[0];
                    }
                    catch { /* 일부 단말은 confidence_scores 없음 */ }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NativeSTT] onResults 파싱 실패: {e.Message}");
                }

                Debug.Log($"[NativeSTT] result='{top}' conf={conf:F2}");
                _owner.Resolve(new SttResult { text = top ?? "", confidence = conf, isEmpty = string.IsNullOrEmpty(top) });
            }
        }
    }
}
