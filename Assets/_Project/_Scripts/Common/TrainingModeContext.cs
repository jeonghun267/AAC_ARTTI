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

        public static string DisplayName(TrainingMode mode) =>
            mode == TrainingMode.Accuracy ? "정확도 모드" : "대화 모드";
    }
}
