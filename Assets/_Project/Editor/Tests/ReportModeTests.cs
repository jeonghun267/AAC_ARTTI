#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Artti.AAC.Logging;
using Artti.Common;
using Artti.Common.Speech;
using Artti.Report;
using Artti.Training;

namespace Artti.EditorTests.Report
{
    // 정확도 모드 / 대화 모드 기록이 레포트에서 섞이지 않는지 확인한다.
    //   연습량(도전·완료 수)은 합산, 정답률·완료율·발화 정확도는 모드별로 나눈다.
    public class ReportModeTests
    {
        private sealed class CaptureLogStore : ILogStore
        {
            public readonly List<AACEvent> Events = new List<AACEvent>();
            public void Log(AACEvent ev) => Events.Add(ev);
            public UniTask FlushAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask DisposeAsync(CancellationToken ct = default) => UniTask.CompletedTask;
        }

        private CaptureLogStore _store;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _store = new CaptureLogStore();
            _path = Path.Combine(Path.GetTempPath(), "artti_mode_" + Guid.NewGuid().ToString("N") + ".jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        private static SttResult Spoken(string text, int pct) =>
            new SttResult { text = text, hasClarity = true, clarityPercent = pct, clarityLight = "yellow", clarityTarget = "물 주세요" };

        // 세션 1개 기록. completed=false면 SessionEnded 없이 끝난다.
        private void LogSession(TrainingMode mode, bool completed, int retries, string heard, int clarityPct)
        {
            var logger = new EventLogger(_store, "test_profile", "convenience");
            logger.LogScenarioEntered(mode);
            logger.LogObjectiveEntered("greeting");
            for (int i = 0; i < retries; i++) logger.LogStepRetryAttempt("greeting");
            logger.LogCardSelected("guided_voice", "물 주세요", Spoken(heard, clarityPct));
            if (completed) logger.LogSessionEnded("completed");
        }

        private ReportDataService BuildService()
        {
            var lines = new List<string>();
            foreach (var ev in _store.Events) lines.Add(JsonUtility.ToJson(ev));
            File.WriteAllLines(_path, lines);
            return new ReportDataService(_path);
        }

        [Test]
        public void Sessions_CarryTheirMode_AndCanBeFiltered()
        {
            LogSession(TrainingMode.Accuracy, true, 0, "물 주세요", 90);
            LogSession(TrainingMode.Conversation, true, 0, "무 주세여", 50);

            var service = BuildService();

            Assert.That(service.GetSessions().Count, Is.EqualTo(2));
            Assert.That(service.GetSessions(TrainingMode.Accuracy).Count, Is.EqualTo(1));
            Assert.That(service.GetSessions(TrainingMode.Conversation).Count, Is.EqualTo(1));
            Assert.That(service.GetRecentRecords(10, TrainingMode.Conversation)[0].mode, Is.EqualTo(TrainingMode.Conversation));
        }

        [Test]
        public void ConversationSessions_AreExcludedFromAccuracyRate()
        {
            LogSession(TrainingMode.Accuracy, true, 2, "물 주세요", 90);        // 재시도 2회 -> 정답률 낮음
            LogSession(TrainingMode.Conversation, true, 0, "무 주세여", 50);    // 재시도 없이 진행

            var service = BuildService();
            var stats = service.GetSummaryStats();
            var accuracyRecord = service.GetRecentRecords(10, TrainingMode.Accuracy)[0];
            var conversationRecord = service.GetRecentRecords(10, TrainingMode.Conversation)[0];

            Assert.That(conversationRecord.accuracyPct, Is.EqualTo(-1));
            Assert.That(stats.hasAccuracy, Is.True);
            Assert.That(stats.avgAccuracyPct, Is.EqualTo(accuracyRecord.accuracyPct));
        }

        [Test]
        public void CompletionRate_UsesAccuracyModeOnly_WhileCountsAreCombined()
        {
            LogSession(TrainingMode.Accuracy, true, 0, "물 주세요", 90);
            LogSession(TrainingMode.Accuracy, false, 0, "물 주세요", 90);
            LogSession(TrainingMode.Conversation, true, 0, "무 주세여", 50);
            LogSession(TrainingMode.Conversation, true, 0, "무 주세여", 50);

            var overview = BuildService().GetOverview();

            Assert.That(overview.totalAttempts, Is.EqualTo(4));
            Assert.That(overview.completedCount, Is.EqualTo(3));
            Assert.That(overview.accuracyAttempts, Is.EqualTo(2));
            Assert.That(overview.accuracyCompleted, Is.EqualTo(1));
            Assert.That(overview.conversationAttempts, Is.EqualTo(2));
            Assert.That(overview.conversationCompleted, Is.EqualTo(2));
            Assert.That(overview.CompletionRate, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Clarity_IsAveragedPerMode()
        {
            LogSession(TrainingMode.Accuracy, true, 0, "물 주세요", 90);
            LogSession(TrainingMode.Accuracy, true, 0, "물 주세요", 80);
            LogSession(TrainingMode.Conversation, true, 0, "무 주세여", 50);

            var stats = BuildService().GetSummaryStats();

            Assert.That(stats.accuracyModeClarityPct, Is.EqualTo(85));
            Assert.That(stats.conversationModeClarityPct, Is.EqualTo(50));
        }

        [Test]
        public void ConversationStep_KeepsHeardTextNextToInterpretedPhrase()
        {
            LogSession(TrainingMode.Conversation, true, 0, "무 주세여", 50);

            var service = BuildService();
            var report = service.GetRecordReport(_store.Events[0].sessionId);

            Assert.That(report.mode, Is.EqualTo(TrainingMode.Conversation));
            Assert.That(report.steps[0].userText, Is.EqualTo("물 주세요"));
            Assert.That(report.steps[0].heardText, Is.EqualTo("무 주세여"));
        }

        [Test]
        public void SessionWithoutModeRecord_IsTreatedAsAccuracyMode()
        {
            var logger = new EventLogger(_store, "test_profile", "convenience");
            logger.LogObjectiveEntered("greeting"); // ScenarioEntered 없음 (예전 기록 형태)
            logger.LogSessionEnded("completed");

            var service = BuildService();

            Assert.That(service.GetSessions()[0].mode, Is.EqualTo(TrainingMode.Accuracy));
        }
    }
}
#endif
