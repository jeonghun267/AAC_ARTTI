using System.Threading;
using Artti.Common;
using Artti.Common.Speech;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TMPro;

// 현장모드(AR) 문장 읽기.
// 변경점(2026-10-06 사용자 요청): 활성 프로필의 읽어주는 목소리(여성/남성)와 말 빠르기로 읽는다(Google Cloud TTS, 훈련과 같은 목소리).
//   키가 없거나 음성 받기에 실패하면 예전처럼 기기 TTS(안드로이드 TextToSpeech)로 읽는다.
//   새 문장을 읽거나 Stop하면, 받는 중이던 이전 문장은 늦게 도착해도 재생하지 않는다(_generation).
//   호출하는 쪽(CardGenerator, OrderManager, HelpChatDrawerView, HelpTextInputController, LatestMenuController,
//   ARFieldBgmPlayer)의 Speak / SpeakText / IsSpeaking은 그대로 쓴다.
public class TTSManager : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject ttsContext;
    private AndroidJavaObject ttsObject;
    private bool isInitialized = false;
#endif

    private CloudTtsService _cloud;
    private AudioSource _source;
    private CancellationTokenSource _cts;
    private int _generation;
    private int _inFlight;   // 음성을 받는 중인 요청 수(받는 동안도 '읽는 중'으로 본다 - 배경음 멈춤용)
    private string _voiceName;

    void Start()
    {
        InitializeTTS();

        string key = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleTtsApi, ApiKeyLoader.GeminiApi);
        if (!string.IsNullOrEmpty(key))
        {
            _source = gameObject.AddComponent<AudioSource>();   // 이 오브젝트의 다른 소리와 섞이지 않게 읽기 전용
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _cloud = new CloudTtsService(key, _source);
        }
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
#endif
    }

    // 화면에 있는 텍스트 카드를 통째로 넘겨받아 읽는 함수 (기존)
    public void SpeakText(TMP_Text targetTextCard)
    {
        if (targetTextCard == null)
        {
            Debug.LogWarning("읽을 텍스트 카드가 지정되지 않았습니다.");
            return;
        }
        Speak(targetTextCard.text);
    }

    // 주문 관리자 등에서 글자(string)를 직접 넘겨받아 읽는 함수
    public void Speak(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        Stop();   // 읽던 문장·받는 중인 문장은 버리고 새 문장
        if (_cloud != null && _cloud.IsAvailable)
        {
            ApplyProfile();
            _cts = new CancellationTokenSource();
            SpeakCloudAsync(message, _generation, _cts.Token).Forget();
        }
        else SpeakNative(message);
    }

    // 변경점: 지금 읽는 것과 받는 중인 것을 모두 멈춘다
    public void Stop()
    {
        _generation++;
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        if (_source != null) _source.Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (isInitialized && ttsObject != null) ttsObject.Call<int>("stop");
#endif
    }

    // 지금 읽는 중인지(받는 중 포함). 배경음(ARFieldBgmPlayer)이 '설명 듣기' 동안 멈췄다가 다시 나올 때 쓴다.
    public bool IsSpeaking
    {
        get
        {
            if (_inFlight > 0 || (_source != null && _source.isPlaying)) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!isInitialized || ttsObject == null) return false;
            try { return ttsObject.Call<bool>("isSpeaking"); }
            catch (System.Exception) { return false; }
#else
            return false;
#endif
        }
    }

    // 활성 프로필의 목소리·빠르기(ProfileVoice). 읽을 때마다 넣어 프로필 설정을 바꾸면 바로 반영된다
    private void ApplyProfile()
    {
        _voiceName = ProfileVoice.Apply(_cloud);
    }

    private async UniTaskVoid SpeakCloudAsync(string message, int generation, CancellationToken ct)
    {
        _inFlight++;
        AudioClip clip;
        try
        {
            clip = await _cloud.SynthesizeAsync(message, ct);
        }
        finally
        {
            _inFlight--;
        }
        if (this == null) return;
        if (ct.IsCancellationRequested || generation != _generation)
        {
            Debug.Log($"[TTSManager] 늦게 도착한 이전 문장 버림: \"{message}\"");
            return;
        }
        if (clip == null)
        {
            Debug.LogWarning("[TTSManager] Cloud TTS 실패 - 기기 TTS로 읽음");
            SpeakNative(message);
            return;
        }
        _source.clip = clip;
        _source.Play();
        Debug.Log($"[TTSManager] Google 음성 재생 ({_voiceName}, {clip.length:F1}초): \"{message}\"");
    }

    private void SpeakNative(string message)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (isInitialized && ttsObject != null)
        {
            AndroidJavaObject emptyParams = new AndroidJavaObject("java.util.HashMap");
            ttsObject.Call<int>("speak", message, 0, emptyParams);
        }
#else
        Debug.Log($"[TTS 시뮬레이션] 다음 문장을 읽습니다: \"{message}\"");
#endif
    }

    void OnDestroy()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (ttsObject != null)
        {
            ttsObject.Call("stop");
            ttsObject.Call("shutdown");
        }
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
#endif
}
