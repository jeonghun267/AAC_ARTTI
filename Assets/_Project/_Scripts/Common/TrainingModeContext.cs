namespace Artti.Common
{
    public enum TrainingMode
    {
        Accuracy,      // 정확도 모드: 기존 훈련 흐름 그대로 (말하기가 어려우면 대화 힌트 카드로 넘길 수 있음)
        Conversation   // 대화 모드: 정확도와 관계없이 대화 진행, 정확도는 레포트에만 기록
    }

    // 훈련 허브 -> 편의점 씬 전환 시 선택한 모드 전달용 (씬 간 정적 홀더).
    // 허브를 거치지 않고 편의점 씬을 바로 실행하면 정확도 모드로 동작한다.
    public static class TrainingModeContext
    {
        public static TrainingMode Mode = TrainingMode.Accuracy;

        // 모드별 점원 목소리 (Google Cloud TTS 한국어 표준 음성. A·B 여성, C·D 남성)
        // 대화 모드 점원은 남자 캐릭터(ARTTI_Male)라 남성 음성을 쓴다.
        public const string AccuracyVoice = "ko-KR-Standard-A";
        public const string ConversationVoice = "ko-KR-Standard-C";

        public static string DisplayName(TrainingMode mode) =>
            mode == TrainingMode.Accuracy ? "정확도 모드" : "대화 모드";

        public static string VoiceFor(TrainingMode mode) =>
            mode == TrainingMode.Accuracy ? AccuracyVoice : ConversationVoice;
    }
}
