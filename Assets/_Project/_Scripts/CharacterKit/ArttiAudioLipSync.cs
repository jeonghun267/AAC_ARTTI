using UnityEngine;

namespace Artti.CharacterKit
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArttiFaceDriver))]
    public sealed class ArttiAudioLipSync : MonoBehaviour
    {
        public AudioSource speechSource;
        public uLipSync.Profile voiceProfile;
        public AudioClip demoSpeech;
        [Tooltip("RMS noise floor, below which the mouth returns to neutral.")]
        public float silenceThreshold = .003f;
        public float fullVolume = .045f;
        public string CurrentPhoneme { get; private set; } = "sil";
        public float CurrentVolume { get; private set; }
        public int AnalysisCallbacks { get; private set; }
        private uLipSync.uLipSync analyzer;
        private ArttiFaceDriver face;
        private float lastReceived;

        private void Awake() => Connect();
        private void OnEnable() { if (Application.isPlaying && face) Connect(); }
        public void Connect()
        {
            face=GetComponent<ArttiFaceDriver>();
            if(!speechSource) speechSource=GetComponent<AudioSource>()??gameObject.AddComponent<AudioSource>();
            if(analyzer) analyzer.onLipSyncUpdate.RemoveListener(OnAnalysis);
            analyzer=speechSource.GetComponent<uLipSync.uLipSync>()??speechSource.gameObject.AddComponent<uLipSync.uLipSync>();
            analyzer.enabled=false;analyzer.profile=voiceProfile;analyzer.enabled=true;
            analyzer.onLipSyncUpdate.AddListener(OnAnalysis);
            face.SetExternalLipSync(false);
            if(!voiceProfile) Debug.LogError("ARTTI: assign the included Korean voice profile to enable lip sync.",this);
        }
        public void SetSource(AudioSource source) { speechSource=source;Connect(); }
        public void Play(AudioClip clip)
        {
            if(!speechSource||!analyzer)Connect();
            ResetMouth();speechSource.Stop();speechSource.clip=clip;
            if(clip)speechSource.Play();
        }
        public void PlayDemo() => Play(demoSpeech);
        public void Stop() { if(speechSource)speechSource.Stop();ResetMouth(); }
        private void Update()
        {
            if(!speechSource||!speechSource.isPlaying||Time.unscaledTime-lastReceived>.2f)ResetMouth();
        }
        private void OnAnalysis(uLipSync.LipSyncInfo info)
        {
            if(!isActiveAndEnabled||!face)return;
            lastReceived=Time.unscaledTime;AnalysisCallbacks++;CurrentVolume=info.rawVolume;
            if(!speechSource||!speechSource.isPlaying||info.rawVolume<silenceThreshold) { ResetMouth();return; }
            CurrentPhoneme=info.phoneme;
            float amplitude=Mathf.Clamp01((info.rawVolume-silenceThreshold)/(fullVolume-silenceThreshold));
            amplitude=Mathf.Sqrt(amplitude)*100f;
            float Ratio(string key) => info.phonemeRatios!=null&&info.phonemeRatios.TryGetValue(key,out float v)?v:0;
            face.SetVisemes(Ratio("A")*amplitude,Ratio("E")*amplitude,Ratio("I")*amplitude,Ratio("O")*amplitude,Ratio("U")*amplitude);
            face.SetJawOpen(0);face.SetMouthClosure(Ratio("M")*amplitude);
        }
        private void ResetMouth()
        {
            CurrentPhoneme="sil";CurrentVolume=0;
            if(face){face.SetVisemes(0,0,0,0,0);face.SetJawOpen(0);face.SetMouthClosure(0);}
        }
        private void OnDisable()
        {
            if(analyzer)analyzer.onLipSyncUpdate.RemoveListener(OnAnalysis);
            ResetMouth();
        }
    }
}
