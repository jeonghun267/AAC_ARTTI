namespace Artti.AAC
{
    // PLAN.MD 5.2.3 — LLM intent classifications
    public enum DialogueIntent
    {
        StepMatch,        // 발화가 현재 목표의 기대와 일치
        StepPartial,      // 의미는 비슷하나 표현이 부족
        OffTopicButValid, // 시나리오에서 벗어났지만 의미 있는 발화
        OffTopicInvalid,  // 시나리오·맥락 모두 무관
        UserDistressed,   // 정서적 어려움 신호 감지
        Unintelligible    // STT 결과 의미 불명
    }

    // PLAN.MD 5.2.4 / 5.5.3 — Scaffold levels for help intensity
    public enum ScaffoldLevel
    {
        None = 0,        // 1차 시도 — 일반 응답
        MildHint = 1,    // 2차 시도 — 부드러운 힌트
        StrongHint = 2   // 3차 시도 — 정답 카드 명시
        // 4차 시도는 force_complete_scenario 또는 transition_to_objective 도구 호출
    }

    // PLAN.MD 5.5.2 — LLM tool catalog
    public enum DialogueTool
    {
        PresentCards,         // 카드 표시 + NPC 발화 (가장 빈번)
        MarkObjectiveComplete,// 현재 목표 완료 처리
        TransitionToObjective,// 다음 목표로 명시적 전환
        EnterSubflow,         // 분기 서브플로 진입
        ReturnFromSubflow,    // 분기 서브플로 종료
        RequestClarification, // unintelligible 또는 confidence 낮을 때
        ExpressUnderstanding, // user_distressed 시 위로 응답
        ForceCompleteScenario // turn 상한 또는 반복 실패 시 강제 마무리
    }

    // Common scenario IDs (mirror scenarios.json scenarioId field)
    public static class ScenarioIds
    {
        public const string Pharmacy    = "pharmacy";
        public const string Convenience = "convenience";
        public const string Restaurant  = "restaurant";
    }
}
