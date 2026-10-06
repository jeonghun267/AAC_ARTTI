namespace Artti.Common.Speech
{
    // 변경점(2026-10-06 사용자 결정): 앱이 사용자 대신 읽어 주는 소리는 모두 활성 프로필의 목소리(여성/남성)와 말 빠르기로.
    //   쓰는 곳: 현장모드 읽기(TTSManager), 훈련 카드 읽기, 리포트 모범 발음·문장 듣기, 리포트 캐릭터.
    //   예외: 훈련 점원(NPC)은 화면 속 캐릭터 목소리(TrainingModeContext.VoiceFor), 프로필 설정 미리듣기는 고르는 중인 목소리.
    //   프로필이 없으면(에디터에서 씬을 바로 실행) 여성 목소리, 보통 빠르기.
    public static class ProfileVoice
    {
        public static ReadingVoice Voice => Profile != null ? Profile.readingVoice : ReadingVoice.Female;
        public static float SpeakingRate => Profile != null ? Profile.speechRate.ToMultiplier() : 1f;

        // 목소리·빠르기를 넣고 넣은 목소리 이름을 돌려준다(로그용)
        public static string Apply(CloudTtsService tts)
        {
            string voice = Voice.ToVoiceName();
            if (tts == null) return voice;
            tts.SetVoice(voice);
            tts.SetSpeakingRate(SpeakingRate);
            return voice;
        }

        static ProfileData Profile =>
            AppBootstrap.Instance != null && AppBootstrap.Instance.ProfileManager != null
                ? AppBootstrap.Instance.ProfileManager.ActiveProfile
                : null;
    }
}
