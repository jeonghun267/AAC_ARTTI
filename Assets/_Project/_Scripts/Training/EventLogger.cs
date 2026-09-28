using Artti.AAC.Logging;
using Artti.Common.Speech;
using UnityEngine;
using System;

namespace Artti.Training
{
    public class EventLogger
    {
        private readonly ILogStore _logStore;
        private readonly string _profileId;
        private readonly string _scenarioId;
        private readonly string _sessionId;

        public EventLogger(ILogStore logStore, string profileId, string scenarioId)
        {
            _logStore = logStore;
            _profileId = profileId;
            _scenarioId = scenarioId;
            _sessionId = Guid.NewGuid().ToString();
        }

        public void LogScenarioEntered()
        {
            Log(AACEventType.ScenarioEntered);
        }

        // status: "completed" — 시나리오 마지막 objective 도달 시
        public void LogSessionEnded(string status)
        {
            Log(AACEventType.SessionEnded, payload: status);
        }

        public void LogSessionAbandoned(string objectiveId)
        {
            Log(AACEventType.SessionAbandoned, objectiveId);
        }

        public void LogStepRetryAttempt(string objectiveId)
        {
            Log(AACEventType.StepRetryAttempt, objectiveId);
        }

        public void LogObjectiveEntered(string objectiveId)
        {
            Log(AACEventType.ObjectiveEntered, objectiveId);
        }

        public void LogCardSelected(string cardId, string text, string sttText = null)
        {
            LogCardSelected(cardId, text, sttText, -1, null);
        }

        // 변경점: STT 결과를 그대로 받아 발음 명료도가 있으면 같은 턴에 함께 기록한다.
        public void LogCardSelected(string cardId, string text, SttResult stt)
        {
            LogCardSelected(cardId, text, stt.text, stt.hasClarity ? stt.clarityPercent : -1, stt.clarityLight);
        }

        // payload: "{cardId}:{text}" [+ "|stt:{sttText}"] [+ "|clarity:{0..100}:{light}"]
        //   clarity는 항상 맨 끝에 붙여 기존 파서(|stt: 이후 전부를 STT 문장으로 읽음)와 구분한다.
        public void LogCardSelected(string cardId, string text, string sttText, int clarityPercent, string clarityLight)
        {
            var payload = string.IsNullOrEmpty(sttText)
                ? $"{cardId}:{text}"
                : $"{cardId}:{text}|stt:{sttText}";
            if (clarityPercent >= 0)
                payload += $"|clarity:{Mathf.Clamp(clarityPercent, 0, 100)}:{clarityLight}";
            Log(AACEventType.CardSelected, payload: payload);
        }

        public void LogNpcTurn(string npcText, Artti.AAC.DialogueTool tool)
        {
            Log(AACEventType.TtsPlayed, payload: $"{tool}:{npcText}");
        }

        private void Log(AACEventType type, string objectiveId = null, string payload = null)
        {
            var ev = AACEvent.Create(_profileId, _sessionId, type, _scenarioId, objectiveId, payload);
            _logStore.Log(ev);
        }
    }
}
