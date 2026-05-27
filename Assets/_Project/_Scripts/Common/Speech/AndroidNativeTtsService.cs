using System;
using System.Collections.Concurrent;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.Common.Speech
{
    // Android 시스템 TextToSpeech 래퍼. 키/네트워크 불필요(엔진 따라 일부 오프라인).
    // SpeakAsync는 발화 완료(또는 취소)까지 대기 — UtteranceProgressListener로 콜백 수신.
    public class AndroidNativeTtsService : ITtsService
    {
        public bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _ttsReady;
#else
                return false;
#endif
            }
        }

        private AndroidJavaObject _tts;
        private bool _ttsReady;
        private int _utterCounter;
        private readonly ConcurrentDictionary<string, UniTaskCompletionSource> _pending
            = new ConcurrentDictionary<string, UniTaskCompletionSource>();

        public AndroidNativeTtsService()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidTts();
#endif
        }

        private void InitializeAndroidTts()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", context, new TtsInitListener(this));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NativeTTS] 초기화 실패: {e.Message}");
            }
        }

        private void OnInit(int status)
        {
            // TextToSpeech.SUCCESS=0, ERROR=-1
            if (status != 0)
            {
                Debug.LogWarning($"[NativeTTS] OnInit 실패 status={status} — 단말의 TTS 엔진 미설치/비활성. 설정→접근성→TTS 출력 확인 필요");
                return;
            }
            try
            {
                // 디폴트 엔진 이름 로그 (Google/Samsung/제조사별)
                try
                {
                    var engine = _tts.Call<string>("getDefaultEngine");
                    Debug.Log($"[NativeTTS] engine='{engine}'");
                }
                catch { }

                int langResult;
                using (var locale = new AndroidJavaObject("java.util.Locale", "ko", "KR"))
                {
                    langResult = _tts.Call<int>("setLanguage", locale);
                }
                // setLanguage 리턴값: AVAILABLE=0, COUNTRY_AVAILABLE=1, VAR_AVAILABLE=2, MISSING_DATA=-1, NOT_SUPPORTED=-2
                Debug.Log($"[NativeTTS] setLanguage(ko-KR) = {langResult} ({LangResultName(langResult)})");

                if (langResult == -1 || langResult == -2)
                {
                    Debug.LogWarning("[NativeTTS] 한국어 음성 데이터 없음 — 단말 설정→시스템→언어 및 입력→TTS→음성 데이터 설치 필요. 디폴트 언어로 폴백 시도");
                    try
                    {
                        using (var defLocale = new AndroidJavaObject("java.util.Locale", "en", "US"))
                        {
                            _tts.Call<int>("setLanguage", defLocale);
                        }
                    }
                    catch { }
                }

                _tts.Call<int>("setOnUtteranceProgressListener", new UtterListener(this));
                _ttsReady = true;
                Debug.Log("[NativeTTS] ready");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NativeTTS] OnInit 후속 설정 실패: {e.Message}");
            }
        }

        private static string LangResultName(int code)
        {
            switch (code)
            {
                case  0: return "LANG_AVAILABLE";
                case  1: return "LANG_COUNTRY_AVAILABLE";
                case  2: return "LANG_COUNTRY_VAR_AVAILABLE";
                case -1: return "LANG_MISSING_DATA";
                case -2: return "LANG_NOT_SUPPORTED";
                default: return $"UNKNOWN({code})";
            }
        }

        public async UniTask SpeakAsync(string text, CancellationToken ct = default)
        {
            if (!IsAvailable || _tts == null || string.IsNullOrEmpty(text)) return;

            var id = $"utt_{Interlocked.Increment(ref _utterCounter)}";
            var tcs = new UniTaskCompletionSource();
            _pending[id] = tcs;

            try
            {
                _tts.Call<int>("speak", text, /*QUEUE_FLUSH=*/0, null, id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NativeTTS] speak 실패: {e.Message}");
                _pending.TryRemove(id, out _);
                return;
            }

            using (ct.Register(() =>
            {
                try { _tts?.Call<int>("stop"); } catch { }
                tcs.TrySetCanceled();
            }))
            {
                try { await tcs.Task; }
                catch (OperationCanceledException) { }
                finally { _pending.TryRemove(id, out _); }
            }
        }

        public void StopAll()
        {
            try { _tts?.Call<int>("stop"); } catch { }
            foreach (var kv in _pending) kv.Value.TrySetCanceled();
            _pending.Clear();
        }

        public void SetVoice(string voiceName) { /* 단말 기본 음성 사용 — 향후 voice 선택 필요 시 구현 */ }

        private void Complete(string id)
        {
            if (_pending.TryRemove(id, out var tcs))
                tcs.TrySetResult();
        }

        private class TtsInitListener : AndroidJavaProxy
        {
            private readonly AndroidNativeTtsService _owner;
            public TtsInitListener(AndroidNativeTtsService o) : base("android.speech.tts.TextToSpeech$OnInitListener") { _owner = o; }
            public void onInit(int status) { _owner.OnInit(status); }
        }

        private class UtterListener : AndroidJavaProxy
        {
            private readonly AndroidNativeTtsService _owner;
            public UtterListener(AndroidNativeTtsService o) : base("android.speech.tts.UtteranceProgressListener") { _owner = o; }
            public void onStart(string id) { }
            public void onDone(string id) { _owner.Complete(id); }
            public void onError(string id) { _owner.Complete(id); }
            public void onError(string id, int errorCode) { _owner.Complete(id); }
            public void onStop(string id, bool interrupted) { _owner.Complete(id); }
        }
    }
}
