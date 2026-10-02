#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Artti.AAC.Logging;
using Artti.Report;

namespace Artti.EditorTests.Report
{
    // 대화 모드 리포트 지표(ConversationReportBuilder): 시각을 직접 넣은 기록으로 응답 속도·적절성·자연스러움 등을 확인한다.
    public class ConversationReportTests
    {
        private readonly List<AACEvent> _events = new List<AACEvent>();
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            _path = Path.Combine(Path.GetTempPath(), "artti_conv_" + Guid.NewGuid().ToString("N") + ".jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        private static long Ms(DateTime local) => new DateTimeOffset(local).ToUnixTimeMilliseconds();

        private void Ev(string session, AACEventType type, long ms, string objective = null, string payload = null)
        {
            _events.Add(new AACEvent
            {
                profileId = "p", sessionId = session, eventType = type.ToString(), scenarioId = "convenience",
                objectiveId = objective, timestampUnixMs = ms, payloadJson = payload,
            });
        }

        // 인사(4초 만에 음성 90%) -> 물건(12초 만에 음성 50%, 다시 말해 달라고 함) -> 10초 만에 상품 탭 -> 완료(60초)
        private void LogTypicalSession(string session, DateTime start, string mode = "mode:conversation")
        {
            long t0 = Ms(start);
            Ev(session, AACEventType.ScenarioEntered, t0, payload: mode);
            Ev(session, AACEventType.ObjectiveEntered, t0, "greeting");
            Ev(session, AACEventType.TtsPlayed, t0 + 1000, payload: "Speak:어서 오세요");
            Ev(session, AACEventType.CardSelected, t0 + 5000, payload: "guided_voice:안녕하세요|stt:안녕하세요|clarity:90:green:안녕하세요");
            Ev(session, AACEventType.ObjectiveEntered, t0 + 6000, "select_items");
            Ev(session, AACEventType.TtsPlayed, t0 + 7000, payload: "Speak:찾으시는 물건 있으세요?");
            Ev(session, AACEventType.CardSelected, t0 + 19000, payload: "guided_voice:물 주세요|stt:무 주세요|clarity:50:red:물 주세요");
            Ev(session, AACEventType.StepRetryAttempt, t0 + 19500, "select_items");
            Ev(session, AACEventType.TtsPlayed, t0 + 20000, payload: "Speak:다시 말씀해 주세요");
            Ev(session, AACEventType.CardSelected, t0 + 30000, payload: "product_water:물 주세요");
            Ev(session, AACEventType.SessionEnded, t0 + 60000, payload: "completed");
        }

        private ReportDataService Service()
        {
            var lines = new List<string>();
            foreach (var ev in _events) lines.Add(JsonUtility.ToJson(ev));
            File.WriteAllLines(_path, lines);
            return new ReportDataService(_path);
        }

        [Test]
        public void TypicalSession_ProducesExpectedMetrics()
        {
            var now = DateTime.Now;
            LogTypicalSession("s1", now.Date.AddHours(10).AddMinutes(14));

            var d = ConversationReportBuilder.Build(Service(), ReportPeriod.Today, now);
            var m = d.current;

            Assert.That(m.sessions, Is.EqualTo(1));
            Assert.That(m.completed, Is.EqualTo(1));
            Assert.That(m.talkSeconds, Is.EqualTo(60));
            Assert.That(m.naturalness, Is.EqualTo(70), "음성 명료도 (90+50)/2");
            Assert.That(m.appropriateness, Is.EqualTo(67), "발화 3개 중 다시 말해 달라고 한 1개를 뺀 2개");
            Assert.That(m.expressiveness, Is.EqualTo(67), "3개 중 음성 2개");
            Assert.That(m.speed, Is.EqualTo(89), "4초 100, 12초 80, 10초 87의 평균");
            Assert.That(m.understanding, Is.EqualTo(75), "인사 바로 성공 1 + 물건 한 번 다시 0.5");
            Assert.That(m.persistence, Is.EqualTo(100), "완료한 세션");
            Assert.That(m.Overall, Is.EqualTo(80));
            Assert.That(ModeReportText.Grade(m.Overall), Is.EqualTo("우수"));

            Assert.That(d.latestSessionId, Is.EqualTo("s1"));
            Assert.That(d.lines.Count, Is.EqualTo(6));
            Assert.That(d.lines[0].isUser, Is.False);
            Assert.That(d.lines[5].isUser, Is.True);
            Assert.That(d.lines[5].text, Is.EqualTo("물 주세요"));
            var transcript = ConversationReportBuilder.Transcript(Service(), "s1");
            Assert.That(transcript.lines.Count, Is.EqualTo(6), "전체 보기 = 그 대화의 모든 말");
            Assert.That(transcript.lines[2].text, Is.EqualTo("찾으시는 물건 있으세요?"));
            Assert.That(ConversationReportLabels.TranscriptSubtitle(transcript), Does.EndWith("· 말 6개"));
            Assert.That(ConversationReportBuilder.Transcript(Service(), null).lines, Is.Empty);
            int todayIndex = ((int)now.DayOfWeek + 6) % 7;
            Assert.That(d.week[todayIndex], Is.EqualTo(80));
        }

        [Test]
        public void Today_ComparesWithYesterday()
        {
            var now = DateTime.Now;
            LogTypicalSession("today", now.Date.AddHours(10));
            // 어제: 30초짜리 짧은 대화(음성 60%)
            long y = Ms(now.Date.AddDays(-1).AddHours(9));
            Ev("yesterday", AACEventType.ScenarioEntered, y, payload: "mode:conversation");
            Ev("yesterday", AACEventType.ObjectiveEntered, y, "greeting");
            Ev("yesterday", AACEventType.TtsPlayed, y + 1000, payload: "Speak:어서 오세요");
            Ev("yesterday", AACEventType.CardSelected, y + 3000, payload: "guided_voice:안녕하세요|stt:안녕|clarity:60:yellow:안녕하세요");
            Ev("yesterday", AACEventType.SessionAbandoned, y + 30000, "greeting");

            var d = ConversationReportBuilder.Build(Service(), ReportPeriod.Today, now);

            Assert.That(d.previous.talkSeconds, Is.EqualTo(30));
            Assert.That(ConversationReportLabels.TalkDelta(d), Is.EqualTo(100));
            Assert.That(ModeReportText.Delta(d.current.naturalness, d.previous.naturalness), Is.EqualTo(10));
            Assert.That(ConversationReportLabels.CompletedDelta(d), Is.EqualTo(1));
            Assert.That(d.previous.persistence, Is.EqualTo(20), "미완료, 5단계 중 1단계");
        }

        [Test]
        public void AccuracyModeSessions_AreIgnored()
        {
            var now = DateTime.Now;
            LogTypicalSession("acc", now.Date.AddHours(10), "mode:accuracy");

            var d = ConversationReportBuilder.Build(Service(), ReportPeriod.All, now);

            Assert.That(d.current.sessions, Is.EqualTo(0));
            Assert.That(d.previous, Is.Null, "전체 탭은 비교하지 않는다");
            Assert.That(d.lines, Is.Empty);
            Assert.That(ConversationReportLabels.Comments(d.current)[0], Is.EqualTo("아직 대화 기록이 없어요."));
        }

        [Test]
        public void NoService_GivesEmptyReport()
        {
            var d = ConversationReportBuilder.Build(null, ReportPeriod.Week, DateTime.Now);

            Assert.That(d.current.Overall, Is.EqualTo(-1));
            Assert.That(Array.TrueForAll(d.week, v => v == -1), Is.True);
            Assert.That(ModeReportText.Value(d.current.naturalness, "%"), Is.EqualTo("-"));
        }

        // AI 평가가 기록에 있으면 자연스러움·응답 적절성은 그 값, AI 코멘트 첫 줄·끝 줄은 AI의 잘한 점·다음에 해 볼 점
        [Test]
        public void AiEvaluation_ReplacesRuleScores_AndFeedsComments()
        {
            var now = DateTime.Now;
            var start = now.Date.AddHours(10);
            LogTypicalSession("s1", start);
            var eval = new ConversationEvaluation { appropriateness = 92, naturalness = 85, praise = "생수를 정확히 찾았어요.", tip = "봉투도 직접 말해 봐요." };
            Ev("s1", AACEventType.SessionEvaluated, Ms(start) + 65000, payload: JsonUtility.ToJson(eval));

            var d = ConversationReportBuilder.Build(Service(), ReportPeriod.Today, now);

            Assert.That(d.current.naturalness, Is.EqualTo(85));
            Assert.That(d.current.appropriateness, Is.EqualTo(92));
            Assert.That(d.current.aiSessions, Is.EqualTo(1));
            Assert.That(d.current.talkSeconds, Is.EqualTo(60), "평가 이벤트가 대화 시간을 늘리지 않는다");
            var lines = ConversationReportLabels.Comments(d.current, d.latestEvaluation);
            Assert.That(lines[0], Is.EqualTo("생수를 정확히 찾았어요."));
            Assert.That(lines[2], Is.EqualTo("봉투도 직접 말해 봐요."));
        }

        [Test]
        public void MixedSessions_AverageAiAndRuleScores()
        {
            var now = DateTime.Now;
            LogTypicalSession("rule", now.Date.AddHours(9));            // 자연스러움 70, 적절성 67 (기존 계산)
            var start = now.Date.AddHours(11);
            LogTypicalSession("ai", start);
            Ev("ai", AACEventType.SessionEvaluated, Ms(start) + 65000,
                payload: JsonUtility.ToJson(new ConversationEvaluation { appropriateness = 93, naturalness = 90 }));

            var m = ConversationReportBuilder.Build(Service(), ReportPeriod.Today, now).current;

            Assert.That(m.naturalness, Is.EqualTo(80), "(70 + 90) / 2");
            Assert.That(m.appropriateness, Is.EqualTo(80), "(66.7 + 93) / 2");
            Assert.That(m.aiSessions, Is.EqualTo(1));
        }

        [Test]
        public void EvaluationParsing_ClampsAndRejectsBadReplies()
        {
            var e = ConversationEvaluationService.ParseResult("{\"appropriateness\":120,\"naturalness\":-5,\"praise\":\" 잘했어요 \",\"tip\":\"\"}");
            Assert.That(e.appropriateness, Is.EqualTo(100));
            Assert.That(e.naturalness, Is.EqualTo(0));
            Assert.That(e.praise, Is.EqualTo("잘했어요"));
            Assert.That(e.tip, Is.Null);
            Assert.That(ConversationEvaluationService.ParseResult("{\"praise\":\"점수 없음\"}"), Is.Null);
            Assert.That(ConversationEvaluationService.ParseResult("JSON 아님"), Is.Null);
            // 변경점: 배열이나 ```json 울타리로 와도 읽는다(Gemini가 [{...}]로 답한 적 있음)
            Assert.That(ConversationEvaluationService.ParseResult("[{\"appropriateness\":70,\"naturalness\":60}]").naturalness, Is.EqualTo(60));
            Assert.That(ConversationEvaluationService.ParseResult("```json\n{\"appropriateness\":70,\"naturalness\":65}\n```").naturalness, Is.EqualTo(65));
            Assert.That(ConversationEvaluationService.ParseResult("[]"), Is.Null);
            // 한 줄 칸을 넘는 문장은 중간에 자르지 않고 버린다(기존 문구로 대신)
            string longTip = new string('가', 41);
            Assert.That(ConversationEvaluationService.ParseResult("{\"appropriateness\":70,\"naturalness\":65,\"tip\":\"" + longTip + "\"}").tip, Is.Null);

            string envelope = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"appropriateness\\\":80,\\\"naturalness\\\":75}\"}]}}]}";
            var fromApi = ConversationEvaluationService.Parse(envelope);
            Assert.That(fromApi.appropriateness, Is.EqualTo(80));
            Assert.That(fromApi.naturalness, Is.EqualTo(75));
        }

        [Test]
        public void TranscriptForAi_MarksVoiceCardAndHeardText()
        {
            var now = DateTime.Now;
            LogTypicalSession("s1", now.Date.AddHours(10));

            var text = ConversationEvaluationService.BuildTranscript(ConversationReportBuilder.Transcript(Service(), "s1").lines);

            StringAssert.Contains("점원: 어서 오세요", text);
            StringAssert.Contains("나(목소리): 안녕하세요", text);
            StringAssert.Contains("나(목소리, 실제로 들린 말: 무 주세요): 물 주세요", text);
            StringAssert.Contains("나(카드로 고름): 물 주세요", text);
        }

        [TestCase(0.0, 100)]
        [TestCase(6.0, 100)]
        [TestCase(15.0, 70)]
        [TestCase(24.0, 40)]
        [TestCase(60.0, 40)]
        public void SpeedScore_Bands(double seconds, int expected)
        {
            Assert.That(ModeReportMath.SpeedScore(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void Labels_FormatKoreanTimeAndDate()
        {
            var t = new DateTime(2026, 10, 1, 10, 14, 0, DateTimeKind.Local);
            Assert.That(ModeReportText.Time(Ms(t)), Is.EqualTo("오전 10:14"));
            Assert.That(ModeReportText.Time(Ms(t.AddHours(5))), Is.EqualTo("오후 3:14"));
            Assert.That(ModeReportText.Date(t), Is.EqualTo("2026년 10월 1일 (목)"));
            Assert.That(ConversationReportLabels.WeekDelta(new[] { -1, 60, -1, 70, 74, -1, -1 }), Is.EqualTo(14));
            Assert.That(ConversationReportLabels.WeekDelta(new[] { -1, 60, -1, -1, -1, -1, -1 }), Is.Null);
        }
    }
}
#endif
