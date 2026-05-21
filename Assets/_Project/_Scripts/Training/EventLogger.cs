using Artti.AAC.Logging;
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

        public void LogObjectiveEntered(string objectiveId)
        {
            Log(AACEventType.ObjectiveEntered, objectiveId);
        }

        public void LogCardSelected(string cardId, string text, string sttText = null)
        {
            var payload = string.IsNullOrEmpty(sttText)
                ? $"{cardId}:{text}"
                : $"{cardId}:{text}|stt:{sttText}";
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
