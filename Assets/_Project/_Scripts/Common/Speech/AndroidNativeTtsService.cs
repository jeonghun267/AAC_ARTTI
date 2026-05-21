using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.Common.Speech
{
    public class AndroidNativeTtsService : ITtsService
    {
        public bool IsAvailable => Application.platform == RuntimePlatform.Android;

        private AndroidJavaObject _tts;

        public AndroidNativeTtsService()
        {
            if (IsAvailable)
            {
                InitializeAndroidTts();
            }
        }

        private void InitializeAndroidTts()
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (var context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", context, new TtsInitListener());
                }
            }
        }

        public async UniTask SpeakAsync(string text, CancellationToken ct = default)
        {
            if (!IsAvailable || _tts == null) return;

            _tts.Call<int>("speak", text, 0, null, "aac_tts_id");
            
            // Native TTS typically doesn't provide a direct UniTask, 
            // but we can poll isSpeaking or just return if it's fire-and-forget.
            // PLAN.MD 11.3 suggests awaiting completion if possible.
            await UniTask.Yield(); 
        }

        public void StopAll()
        {
            _tts?.Call<int>("stop");
        }

        public void SetVoice(string voiceName) { }

        private class TtsInitListener : AndroidJavaProxy
        {
            public TtsInitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") { }
            public void onInit(int status) { }
        }
    }
}
