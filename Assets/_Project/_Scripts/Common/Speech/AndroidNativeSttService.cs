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
