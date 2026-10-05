#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Artti.AAC.Logging;
using Artti.Common;
using Artti.Report;

namespace Artti.EditorTests.Report
{
    // 리포트의 대화·연습 시간은 실제 연습한 시간만 센다: 기록 사이가 2분보다 길면(앱을 켜 둔 채 쉼, 백그라운드) 2분까지만.
    //   예전에는 끝 - 시작 경과 시간이라 앱을 켜 둔 채 다음 날 이어서 끝내면 총 대화 시간이 하루(1526분)로 찍혔다.
    public class ReportActiveTimeTests
    {
        private const long Minute = 60 * 1000;
        private string _path;
        private readonly List<AACEvent> _events = new List<AACEvent>();

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            _path = Path.Combine(Path.GetTempPath(), "artti_active_" + Guid.NewGuid().ToString("N") + ".jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        private static long Ms(DateTime local) => new DateTimeOffset(local).ToUnixTimeMilliseconds();

        private void Ev(string session, AACEventType type, long ms, string payload = null)
        {
            _events.Add(new AACEvent
            {
                profileId = "p", sessionId = session, eventType = type.ToString(), scenarioId = "convenience",
                timestampUnixMs = ms, payloadJson = payload,
            });
        }

        private ReportDataService Service()
        {
            var lines = new List<string>();
            foreach (var ev in _events) lines.Add(JsonUtility.ToJson(ev));
            File.WriteAllLines(_path, lines);
            return new ReportDataService(_path);
        }

        [Test]
        public void ActiveMs_ShortGaps_CountFully()
        {
            Assert.That(ReportDataService.ActiveMs(new long[] { 1000, 5000, 30000, 60000 }, 0, 60000), Is.EqualTo(60000));
        }

        [Test]
        public void ActiveMs_LongGap_CountsOnlyCap()
        {
            long dayLater = 25 * 60 * Minute;
            long total = ReportDataService.ActiveMs(new long[] { 5000, dayLater, dayLater + 10000 }, 0, dayLater + 10000);
            Assert.That(total, Is.EqualTo(5000 + ReportDataService.IdleCapMs + 10000));
        }

        [Test]
        public void ActiveMs_EventsAfterEnd_AreIgnored()
        {
            Assert.That(ReportDataService.ActiveMs(new long[] { 10000, 20000, 90000 }, 0, 20000), Is.EqualTo(20000));
        }

        [Test]
        public void ActiveMs_EndBeforeStart_IsZero()
        {
            Assert.That(ReportDataService.ActiveMs(new long[] { 5 }, 100, 50), Is.EqualTo(0));
        }

        // 어제 오후에 시작해 앱을 켜 둔 채 두었다가 오늘 이어서 끝낸 대화: 하루가 아니라 실제 연습한 시간만
        [Test]
        public void ConversationLeftOpenOvernight_TalkTimeIsActiveTimeOnly()
        {
            var now = DateTime.Now;
            long t0 = Ms(now.Date.AddDays(-1).AddHours(15));
            long resumed = Ms(now.Date.AddHours(16));
            Ev("s1", AACEventType.ScenarioEntered, t0, "mode:conversation");
            Ev("s1", AACEventType.TtsPlayed, t0 + 1000, "Speak:어서 오세요");
            Ev("s1", AACEventType.CardSelected, t0 + 5000, "guided_voice:안녕하세요|stt:안녕하세요");
            Ev("s1", AACEventType.TtsPlayed, resumed, "Speak:찾으시는 물건 있으세요?");
            Ev("s1", AACEventType.CardSelected, resumed + 8000, "guided_voice:물 주세요|stt:물 주세요");
            Ev("s1", AACEventType.SessionEnded, resumed + 10000, "completed");

            var service = Service();
            var summary = service.GetSessions(TrainingMode.Conversation)[0];
            int expected = (int)((5000 + ReportDataService.IdleCapMs + 10000) / 1000);   // 5초 + 쉰 시간 2분 + 10초

            Assert.That(ModeReportMath.SessionSeconds(summary), Is.EqualTo(expected));
            Assert.That(summary.durationMin, Is.EqualTo(2));
            var d = ConversationReportBuilder.Build(service, ReportPeriod.All, now);
            Assert.That(d.current.talkSeconds, Is.EqualTo(expected));
        }
    }
}
#endif
