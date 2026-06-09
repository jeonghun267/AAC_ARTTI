using UnityEngine;
using TMPro;

public class TTSManager : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject ttsContext;
    private AndroidJavaObject ttsObject;
    private bool isInitialized = false;
#endif

    void Start()
    {
        InitializeTTS();
    }

    private void InitializeTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            ttsContext = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            ttsObject = new AndroidJavaObject("android.speech.tts.TextToSpeech", ttsContext, new TTSOnInitListener(this));
        }
        catch (System.Exception e)
        {
            Debug.LogError("TTS 초기화 실패: " + e.Message);
        }
#else
        Debug.Log("현재 환경(Editor)에서는 TTS 기능 연결 확인용 로그만 출력됩니다.");
#endif
    }

    // ★ 수정된 부분: 클릭한 텍스트 카드를 매개변수로 직접 받습니다.
    public void SpeakText(TMP_Text targetTextCard)
    {
        if (targetTextCard == null)
        {
            Debug.LogWarning("읽을 텍스트 카드가 지정되지 않았습니다.");
            return;
        }

        string message = targetTextCard.text;
        if (string.IsNullOrEmpty(message)) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (isInitialized && ttsObject != null)
        {
            ttsObject.Call<int>("speak", message, 0, null, "UtteranceID");
        }
#else
        Debug.Log($"[TTS 시뮬레이션] 다음 문장을 읽습니다: \"{message}\"");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private class TTSOnInitListener : AndroidJavaProxy
    {
        private TTSManager manager;

        public TTSOnInitListener(TTSManager manager) : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            this.manager = manager;
        }

        public void onInit(int status)
        {
            if (status == 0)
            {
                manager.isInitialized = true;
                AndroidJavaClass localeClass = new AndroidJavaClass("java.util.Locale");
                AndroidJavaObject koreanLocale = localeClass.GetStatic<AndroidJavaObject>("KOREAN");
                manager.ttsObject.Call<int>("setLanguage", koreanLocale);
            }
            else
            {
                Debug.LogError("TTS 시스템 초기화 실패 (Status: " + status + ")");
            }
        }
    }

    void OnDestroy()
    {
        if (ttsObject != null)
        {
            ttsObject.Call("stop");
            ttsObject.Call("shutdown");
        }
    }
#endif
}