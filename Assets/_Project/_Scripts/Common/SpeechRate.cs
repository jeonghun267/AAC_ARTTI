namespace Artti.Common
{
    // 프로필별 말 빠르기. JsonUtility는 enum을 정수로 저장하므로 보통을 0에 둔다
    // (이 값이 없던 예전 profiles.json을 읽으면 0 = 보통이 된다).
    public enum SpeechRate
    {
        Normal = 0,
        Slow = 1,
        Fast = 2,
    }

    public static class SpeechRateExtensions
    {
        // Cloud TTS speakingRate, Android TextToSpeech.setSpeechRate 둘 다 1.0이 기본 속도.
        public static float ToMultiplier(this SpeechRate rate)
        {
            switch (rate)
            {
                case SpeechRate.Slow: return 0.8f;
                case SpeechRate.Fast: return 1.2f;
                default: return 1f;
            }
        }
    }
}
