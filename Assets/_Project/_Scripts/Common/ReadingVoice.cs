namespace Artti.Common
{
    // 프로필별 카드 읽어주는 목소리. JsonUtility는 enum을 정수로 저장하므로 여자를 0에 둔다
    // (이 값이 없던 예전 profiles.json을 읽으면 0 = 여자 = 지금까지 쓰던 카드 음성이 된다).
    public enum ReadingVoice
    {
        Female = 0,
        Male = 1,
    }

    public static class ReadingVoiceExtensions
    {
        // Google Cloud TTS 한국어 표준 음성 (A·B 여성, C·D 남성). 점원 목소리와 같은 음성을 쓴다.
        public static string ToVoiceName(this ReadingVoice voice) =>
            voice == ReadingVoice.Male ? TrainingModeContext.ConversationVoice : TrainingModeContext.AccuracyVoice;

        public static string DisplayName(this ReadingVoice voice) =>
            voice == ReadingVoice.Male ? "남자" : "여자";
    }
}
