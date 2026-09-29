namespace Artti.Common
{
    public enum TrainingMode
    {
        Accuracy,      // 정확도 모드: 발화 정확도가 기준 이상일 때만 다음 단계로
        Conversation   // 대화 모드: 정확도와 관계없이 대화 진행, 정확도는 레포트에만 기록
    }

    // 훈련 허브 -> 편의점 씬 전환 시 선택한 모드 전달용 (씬 간 정적 홀더).
    // 허브를 거치지 않고 편의점 씬을 바로 실행하면 정확도 모드로 동작한다.
    public static class TrainingModeContext
    {
        public static TrainingMode Mode = TrainingMode.Accuracy;

        // 정확도 모드 통과 기준 (서버 STT 발화 정확도 %, 0..100). 이 값 미만이면 Gemini 호출 없이 다시 말하기.
        //   서버 STT가 정확도를 주지 못한 발화(기기 STT 폴백 등)는 잴 수 없으므로 통과시킨다.
        //   씬 빌더가 Inspector 값을 덮어쓰므로 기준은 여기 한 곳에서만 바꾼다.
        public const int AccuracyPassPercent = 60;

        // 정확도 모드에서 한 단계에 이 횟수만큼 기준 미달이면 다음 시도는 통과시킨다 (좌절 방지).
        //   재시도는 레포트에 그대로 남아 그 단계는 "연습해봐요"로 표시된다.
        //   ScaffoldedRetryPolicy의 강제 마무리(실패 4회)보다 작아야 관문 때문에 시나리오가 끝나지 않는다.
        public const int AccuracyMaxRetries = 3;

        // 모드별 점원 목소리 (Google Cloud TTS 한국어 표준 음성. A·B 여성, C·D 남성)
        public const string AccuracyVoice = "ko-KR-Standard-A";
        public const string ConversationVoice = "ko-KR-Standard-B";

        public static string DisplayName(TrainingMode mode) =>
            mode == TrainingMode.Accuracy ? "정확도 모드" : "대화 모드";

        // 정확도 모드 관문: 다시 말하게 할지 판정.
        //   대화 모드, 정확도를 잴 수 없는 발화, 기준 이상, 이미 AccuracyMaxRetries번 미달한 단계는 통과.
        public static bool ShouldRetry(TrainingMode mode, bool hasClarity, int clarityPercent, int failedAttempts) =>
            mode == TrainingMode.Accuracy
            && hasClarity
            && clarityPercent < AccuracyPassPercent
            && failedAttempts < AccuracyMaxRetries;

        public static string VoiceFor(TrainingMode mode) =>
            mode == TrainingMode.Accuracy ? AccuracyVoice : ConversationVoice;
    }
}
