using Artti.AAC.Logging;
using Artti.Common;
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

        // 변경점: 완료 화면이 레포트 기록에서 이번 세션 결과를 찾을 때 쓴다
        public string SessionId => _sessionId;

        // payload: "mode:accuracy" / "mode:conversation" — 레포트에서 두 모드 기록을 구분하기 위함
        public void LogScenarioEntered(TrainingMode mode = TrainingMode.Accuracy)
        {
            Log(AACEventType.ScenarioEntered,
                payload: mode == TrainingMode.Conversation ? "mode:conversation" : "mode:accuracy");
        }

        // status: "completed" — 시나리오 마지막 objective 도달 시
        public void LogSessionEnded(string status)
        {
            Log(AACEventType.SessionEnded, payload: status);
        }

        // 변경점: 대화 모드 AI 평가(응답 적절성·대화 자연스러움·잘한 점·다음에 해 볼 점)
        public void LogSessionEvaluated(Artti.Report.ConversationEvaluation evaluation)
        {
            if (evaluation == null) return;
            Log(AACEventType.SessionEvaluated, payload: JsonUtility.ToJson(evaluation));
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

        // 변경점: STT 결과를 그대로 받아 서버 발화 정확도(명료도)가 있으면 같은 턴에 함께 기록한다.
        public void LogCardSelected(string cardId, string text, SttResult stt)
        {
            LogCardSelected(cardId, text, stt.text, stt.hasClarity ? stt.clarityPercent : -1, stt.clarityLight, stt.clarityTarget,
                            stt.hasClarity ? stt.clarityText : null);
        }

        // payload: "{cardId}:{text}" [+ "|stt:{sttText}"] [+ "|clarityheard:{명료도 모델이 들은 문장}"] [+ "|clarity:{0..100}:{light}:{목표 문장}"]
        //   clarity는 항상 맨 끝에 붙여 기존 파서(|stt: 이후 전부를 STT 문장으로 읽음)와 구분한다.
        //   목표 문장은 마지막 필드라 ':'가 들어가도 된다. '|'는 구분자라 공백으로 바꾼다.
        //   변경점: clarityheard는 현장지원모드(대화 모드) 두 모델 서버에서 명료도를 계산한 기존 모델의 인식 문장
        //   (stt는 NPC 대화에 쓴 새 모델 문장). 한 모델로 인식·채점해 두 문장이 같으면 붙이지 않는다(상세기록은 stt 문장을 쓴다).
        public void LogCardSelected(string cardId, string text, string sttText, int clarityPercent, string clarityLight,
                                    string clarityTarget = null, string clarityHeard = null)
        {
            var payload = string.IsNullOrEmpty(sttText)
                ? $"{cardId}:{text}"
                : $"{cardId}:{text}|stt:{sttText}";
            if (clarityPercent >= 0)
            {
                if (clarityHeard != null && clarityHeard != sttText)
                    payload += "|clarityheard:" + clarityHeard.Replace("|", " ");
                var light = (clarityLight ?? "").Replace(":", "").Replace("|", "");
                var target = (clarityTarget ?? "").Replace("|", " ");
                payload += $"|clarity:{Mathf.Clamp(clarityPercent, 0, 100)}:{light}:{target}";
            }
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
