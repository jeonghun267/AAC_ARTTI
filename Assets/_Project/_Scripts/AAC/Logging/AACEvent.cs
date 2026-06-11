using System;

namespace Artti.AAC.Logging
{
    // PLAN.MD v3 section 8.4 - the 14 event types.
    public enum AACEventType
    {
        ScenarioEntered,
        ObjectiveEntered,
        ObjectiveCompleted,
        CardSelected,
        CardRepeated,
        StepRetryAttempt,
        TopicPendingAdded,
        TopicPendingResolved,
        SubflowEntered,
        SubflowReturned,
        LlmCallFailed,
        ToolCallFailed,
        SessionAbandoned,
        TtsPlayed,
        // 레포트 화면용 추가 (PLAN 8.4의 14종 외): payloadJson에 completed 등 상태 기록
        SessionEnded
    }

    [Serializable]
    public class AACEvent
    {
        public string profileId;
        public string sessionId;
        public string eventType;
        public string scenarioId;
        public string objectiveId;
        public long timestampUnixMs;
        public string payloadJson;

        public static AACEvent Create(string profileId, string sessionId, AACEventType type,
                                      string scenarioId = null, string objectiveId = null, string payloadJson = null)
        {
            return new AACEvent
            {
                profileId = profileId,
                sessionId = sessionId,
                eventType = type.ToString(),
                scenarioId = scenarioId,
                objectiveId = objectiveId,
                timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payloadJson = payloadJson
            };
        }
    }
}
