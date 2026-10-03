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
    // 완료 화면 결과(TrainingResultCalculator): 점수·별·정확도·잘한 표현 수·최고 기록 비교가 레포트 기록과 맞는지 확인한다.
    public class TrainingResultTests
    {
        private sealed class CaptureLogStore : ILogStore
        {
            public readonly List<AACEvent> Events = new List<AACEvent>();
            public void Log(AACEvent ev) => Events.Add(ev);
            public UniTask FlushAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask DisposeAsync(CancellationToken ct = default) => UniTask.CompletedTask;
        }

        private const int Pass = 60;
        private CaptureLogStore _store;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _store = new CaptureLogStore();
            _path = Path.Combine(Path.GetTempPath(), "artti_result_" + Guid.NewGuid().ToString("N") + ".jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        private static SttResult Spoken(int pct) =>
            new SttResult { text = "안녕하세요", hasClarity = true, clarityPercent = pct, clarityLight = "green", clarityTarget = "안녕하세요" };

        // 인사(음성, greetingTries 순서의 명료도 — 첫 시도 뒤마다 재시도 기록) -> 작별(버튼 탭) -> 완료
        private string LogSession(TrainingMode mode, params int[] greetingTries)
        {
            var logger = new EventLogger(_store, "test_profile", "convenience");
            logger.LogScenarioEntered(mode);
            logger.LogObjectiveEntered("greeting");
            for (int i = 0; i < greetingTries.Length; i++)
            {
                logger.LogCardSelected("guided_voice", "안녕하세요", Spoken(greetingTries[i]));
                if (i < greetingTries.Length - 1) logger.LogStepRetryAttempt("greeting");
            }
            logger.LogObjectiveEntered("farewell");
            logger.LogCardSelected("bye", "감사합니다");
            logger.LogSessionEnded("completed");
            return logger.SessionId;
        }

        private ReportDataService BuildService()
        {
            var lines = new List<string>();
            foreach (var ev in _store.Events) lines.Add(JsonUtility.ToJson(ev));
            File.WriteAllLines(_path, lines);
            return new ReportDataService(_path);
        }

        // 버튼만 눌러 끝낸 훈련(인사·작별 둘 다 버튼)
        private string LogTapOnlySession()
        {
            var logger = new EventLogger(_store, "test_profile", "convenience");
            logger.LogScenarioEntered(TrainingMode.Accuracy);
            logger.LogObjectiveEntered("greeting");
            logger.LogCardSelected("quick_phrase", "안녕하세요");
            logger.LogObjectiveEntered("farewell");
            logger.LogCardSelected("quick_phrase", "감사합니다");
            logger.LogSessionEnded("completed");
            return logger.SessionId;
        }

        private TrainingResult Result(string sessionId, TrainingMode mode = TrainingMode.Accuracy) =>
            TrainingResultCalculator.Build(BuildService(), sessionId, Pass, mode, "편의점", 90);

        // 점수 = 진행·말하기·발음·대답 속도 중 있는 값의 평균 (이 테스트 기록에는 점원 말이 없어 대답 속도는 빠진다)
        [Test]
        public void FirstSession_ScoresFromProgressSpeechAndClarity_AndIsFirstRecord()
        {
            var id = LogSession(TrainingMode.Accuracy, 90);

            var r = Result(id);

            Assert.That(r.hasRecord, Is.True);
            Assert.That(r.score, Is.EqualTo(80), "진행 100, 말하기 50(두 말 중 하나만 목소리), 발음 90");
            Assert.That(r.stars, Is.EqualTo(4));
            Assert.That(r.accuracyPct, Is.EqualTo(90));
            Assert.That(r.goodExpressions, Is.EqualTo(2), "음성 90%와 버튼 탭 둘 다 잘한 표현");
            Assert.That(r.record, Is.EqualTo(RecordState.First));
            Assert.That(r.previousBest, Is.EqualTo(-1));
        }

        [Test]
        public void RetriedStep_LowersScore_AndLowClarityIsNotCounted()
        {
            LogSession(TrainingMode.Accuracy, 90);
            var id = LogSession(TrainingMode.Accuracy, 40, 80);   // 인사 단계 한 번 다시 -> 0.5, 작별 1 -> 진행 75

            var r = Result(id);

            Assert.That(r.score, Is.EqualTo(67), "진행 75, 말하기 67(세 말 중 둘), 발음 60");
            Assert.That(r.stars, Is.EqualTo(3));
            Assert.That(r.accuracyPct, Is.EqualTo(60), "재시도 포함 음성 발화 평균 (40+80)/2");
            Assert.That(r.goodExpressions, Is.EqualTo(2), "기준(60) 미달 40%는 빼고 80%와 버튼 탭");
            Assert.That(r.record, Is.EqualTo(RecordState.Lower));
            Assert.That(r.previousBest, Is.EqualTo(80));
            Assert.That(TrainingResultLabels.IsBest(r), Is.False);
        }

        [Test]
        public void HigherAndTie_ComparedWithPreviousBest()
        {
            LogSession(TrainingMode.Accuracy, 40, 80);            // 67점
            var higher = LogSession(TrainingMode.Accuracy, 90);   // 80점
            Assert.That(Result(higher).record, Is.EqualTo(RecordState.Higher));
            Assert.That(TrainingResultLabels.RecordNote(Result(higher)), Does.Contain("13점"));

            var tie = LogSession(TrainingMode.Accuracy, 90);      // 80점 = 지난 최고
            Assert.That(Result(tie).record, Is.EqualTo(RecordState.Tie));
        }

        [Test]
        public void OtherModeRecords_DoNotCountAsPreviousBest()
        {
            LogSession(TrainingMode.Conversation, 90);
            var id = LogSession(TrainingMode.Accuracy, 40, 80);

            var r = Result(id);

            Assert.That(r.record, Is.EqualTo(RecordState.First));
        }

        [Test]
        public void NoRecord_ShowsBlankValues()
        {
            var r = TrainingResultCalculator.Build(null, null, Pass, TrainingMode.Accuracy, "편의점", 30);

            Assert.That(r.hasRecord, Is.False);
            Assert.That(TrainingResultLabels.AccuracyValue(r), Is.EqualTo("-"));
            Assert.That(TrainingResultLabels.ExpressionValue(r), Is.EqualTo("-"));
            Assert.That(TrainingResultLabels.IsBest(r), Is.False);
            Assert.That(TrainingResultLabels.Summary(r), Is.EqualTo("편의점 · 정확도 모드 · 1분 안에 마쳤어요"));
        }

        // 변경점(사용자 요청 2026-10-03): 버튼만 눌러 끝내도 늘 100점이던 문제 — 목소리로 한 말이 없으면 말하기 0이 들어간다
        [Test]
        public void TapOnlySession_IsNotFullScore()
        {
            var r = Result(LogTapOnlySession());

            Assert.That(r.score, Is.EqualTo(50), "진행 100, 말하기 0, 발음은 잴 말이 없어 뺀다");
            Assert.That(r.stars, Is.EqualTo(3));
            Assert.That(TrainingResultLabels.AccuracyValue(r), Is.EqualTo("-"));
        }

        [TestCase(100, 5)]
        [TestCase(90, 5)]
        [TestCase(89, 4)]
        [TestCase(70, 4)]
        [TestCase(69, 3)]
        [TestCase(50, 3)]
        [TestCase(49, 2)]
        [TestCase(30, 2)]
        [TestCase(29, 1)]
        [TestCase(0, 1)]
        public void Stars_FollowScoreBands(int score, int stars)
        {
            Assert.That(TrainingResultCalculator.StarsFor(score), Is.EqualTo(stars));
        }
    }
}
#endif
