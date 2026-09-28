#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Artti.AAC.Logging;
using Artti.Common.Speech;
using Artti.Report;
using Artti.Training;

namespace Artti.EditorTests.Report
{
    // EventLogger가 남긴 발음 명료도가 상세기록(ReportRecordDetail)까지 전달되는지 확인한다.
    public class ReportClarityTests
    {
        private sealed class CaptureLogStore : ILogStore
        {
            public readonly List<AACEvent> Events = new List<AACEvent>();
            public void Log(AACEvent ev) => Events.Add(ev);
            public UniTask FlushAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask DisposeAsync(CancellationToken ct = default) => UniTask.CompletedTask;
        }

        private CaptureLogStore _store;
        private EventLogger _logger;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _store = new CaptureLogStore();
            _logger = new EventLogger(_store, "test_profile", "convenience");
            _path = Path.Combine(Path.GetTempPath(), "artti_clarity_" + Guid.NewGuid().ToString("N") + ".jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        private static SttResult Spoken(string text, int pct, string light) =>
            new SttResult { text = text, hasClarity = true, clarityPercent = pct, clarityLight = light };

        private ReportRecordDetail BuildReport()
        {
            var lines = new List<string>();
            foreach (var ev in _store.Events) lines.Add(JsonUtility.ToJson(ev));
            File.WriteAllLines(_path, lines);
            var service = new ReportDataService(_path);
            return service.GetRecordReport(_store.Events[0].sessionId);
        }

        [Test]
        public void SpokenTurns_ShowClarityPerStepAndAverage()
        {
            _logger.LogScenarioEntered();
            _logger.LogObjectiveEntered("greeting");
            _logger.LogCardSelected("card_cvs_greeting", "안녕하세요", Spoken("안녕하세요", 80, "green"));
            _logger.LogObjectiveEntered("select_items");
            _logger.LogCardSelected("guided_voice", "물 주세요", Spoken("물 주세요", 62, "yellow"));
            _logger.LogSessionEnded("completed");

            var report = BuildReport();

            Assert.That(report.steps.Count, Is.EqualTo(2));
            Assert.That(report.steps[0].userText, Is.EqualTo("안녕하세요"));
            Assert.That(report.steps[0].clarityPct, Is.EqualTo(80));
            Assert.That(report.steps[0].clarityLight, Is.EqualTo("green"));
            Assert.That(report.steps[1].userText, Is.EqualTo("물 주세요"));
            Assert.That(report.steps[1].clarityPct, Is.EqualTo(62));
            Assert.That(report.steps[1].clarityLight, Is.EqualTo("yellow"));
            Assert.That(report.avgClarityPct, Is.EqualTo(71));   // (80 + 62) / 2
        }

        [Test]
        public void LegacyPayloadWithoutClarity_ReportsNoClarity()
        {
            _logger.LogScenarioEntered();
            _logger.LogObjectiveEntered("greeting");
            _logger.LogCardSelected("card_cvs_greeting", "안녕하세요", "안녕하세요");

            var report = BuildReport();

            Assert.That(report.steps[0].userText, Is.EqualTo("안녕하세요"));
            Assert.That(report.steps[0].clarityPct, Is.EqualTo(-1));
            Assert.That(report.steps[0].clarityLight, Is.Null);
            Assert.That(report.avgClarityPct, Is.EqualTo(-1));
        }

        [Test]
        public void TapAfterSpokenTurn_StepShowsTapWithoutClarity_AverageKeepsSpoken()
        {
            _logger.LogScenarioEntered();
            _logger.LogObjectiveEntered("select_items");
            _logger.LogCardSelected("guided_voice", "물 주세요", Spoken("물 주세요", 90, "green"));
            _logger.LogCardSelected("quick_phrase", "계산할게요", "계산할게요");

            var report = BuildReport();

            Assert.That(report.steps[0].userText, Is.EqualTo("계산할게요"));
            Assert.That(report.steps[0].clarityPct, Is.EqualTo(-1));
            Assert.That(report.avgClarityPct, Is.EqualTo(90));
        }

        [Test]
        public void EmptyCardText_FallsBackToSttText_WithoutClaritySuffix()
        {
            _logger.LogScenarioEntered();
            _logger.LogObjectiveEntered("greeting");
            _logger.LogCardSelected("card_x", "", Spoken("안녕하세요", 75, "green"));

            var report = BuildReport();

            Assert.That(report.steps[0].userText, Is.EqualTo("안녕하세요"));
            Assert.That(report.steps[0].clarityPct, Is.EqualTo(75));
        }

        [Test]
        public void MalformedClaritySuffix_IsIgnored()
        {
            _logger.LogScenarioEntered();
            _logger.LogObjectiveEntered("greeting");
            _store.Log(AACEvent.Create("test_profile", _store.Events[0].sessionId, AACEventType.CardSelected,
                                       "convenience", null, "card_cvs_greeting:안녕하세요|stt:안녕하세요|clarity:abc:green"));

            var report = BuildReport();

            Assert.That(report.steps[0].userText, Is.EqualTo("안녕하세요"));
            Assert.That(report.steps[0].clarityPct, Is.EqualTo(-1));
            Assert.That(report.avgClarityPct, Is.EqualTo(-1));
        }

        [Test]
        public void EmptySttResult_LogsNoClarity()
        {
            _logger.LogScenarioEntered();
            _logger.LogObjectiveEntered("greeting");
            _logger.LogCardSelected("card_cvs_greeting", "안녕하세요", new SttResult { text = "", isEmpty = true });

            Assert.That(_store.Events[_store.Events.Count - 1].payloadJson, Is.EqualTo("card_cvs_greeting:안녕하세요"));
        }
    }
}
#endif
