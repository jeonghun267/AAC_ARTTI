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

        private static readonly string[] Objectives = { "greeting", "select_items", "extras", "checkout", "farewell" };

        // 인사(음성, greetingTries 순서의 명료도 — 첫 시도 뒤마다 재시도 기록) -> 나머지 네 단계(버튼) -> 완료
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
            for (int i = 1; i < Objectives.Length; i++)
            {
                logger.LogObjectiveEntered(Objectives[i]);
                logger.LogCardSelected("quick_phrase", "감사합니다");
            }
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

        // 인사만 음성이고 네 단계는 버튼. 점원 말이 없어 속도 가산점은 없고, 인사 점수 91 / 5단계.
        [Test]
        public void FirstSession_ScoresFromProgressSpeechAndClarity_AndIsFirstRecord()
        {
            var id = LogSession(TrainingMode.Accuracy, 90);

            var r = Result(id);

            Assert.That(r.hasRecord, Is.True);
            Assert.That(r.score, Is.EqualTo(18));
            Assert.That(r.stars, Is.EqualTo(1));
            Assert.That(r.accuracyPct, Is.EqualTo(90));
            Assert.That(r.goodExpressions, Is.EqualTo(5), "잘한 표현 수는 음성 90%와 버튼 네 번을 모두 포함");
            Assert.That(r.record, Is.EqualTo(RecordState.First));
            Assert.That(r.previousBest, Is.EqualTo(-1));
        }

        [Test]
        public void RetriedStep_LowersScore_AndLowClarityIsNotCounted()
        {
            LogSession(TrainingMode.Accuracy, 90);
            var id = LogSession(TrainingMode.Accuracy, 40, 80);

            var r = Result(id);

            Assert.That(r.score, Is.EqualTo(14), "인사: 거절된 40% 시도 56점과 재시도 80% 응답 79.5점의 평균 / 5단계");
            Assert.That(r.stars, Is.EqualTo(1));
            Assert.That(r.accuracyPct, Is.EqualTo(60), "재시도 포함 음성 발화 평균 (40+80)/2");
            Assert.That(r.goodExpressions, Is.EqualTo(5), "기준(60) 미달 40%는 빼고 80%와 버튼 네 번");
            Assert.That(r.record, Is.EqualTo(RecordState.Lower));
            Assert.That(r.previousBest, Is.EqualTo(18));
            Assert.That(TrainingResultLabels.IsBest(r), Is.False);
        }

        [Test]
        public void HigherAndTie_ComparedWithPreviousBest()
        {
            LogSession(TrainingMode.Accuracy, 40, 80);            // 14점
            var higher = LogSession(TrainingMode.Accuracy, 90);   // 18점
            Assert.That(Result(higher).record, Is.EqualTo(RecordState.Higher));
            Assert.That(TrainingResultLabels.RecordNote(Result(higher)), Does.Contain("4점"));

            var tie = LogSession(TrainingMode.Accuracy, 90);      // 18점 = 지난 최고
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
            Assert.That(TrainingResultLabels.Summary(r), Is.EqualTo("편의점 · 구음진단모드 · 1분 안에 마쳤어요"));
        }

        // 변경점(사용자 요청 2026-10-03): 버튼만 눌러 끝낸 훈련은 진행·응답 속도와 관계없이 0점.
        [Test]
        public void TapOnlySession_IsZero()
        {
            var r = Result(LogTapOnlySession());

            Assert.That(r.score, Is.Zero);
            Assert.That(r.stars, Is.EqualTo(1));
            Assert.That(TrainingResultLabels.AccuracyValue(r), Is.EqualTo("-"));
        }

        // 실제 기록 경로: 점원 질문 -> 음성 또는 버튼 응답을 다섯 단계 모두 저장하고 완료 화면 결과를 계산한다.
        [TestCase(TrainingMode.Accuracy, 0, 0)]
        [TestCase(TrainingMode.Accuracy, 1, 20)]
        [TestCase(TrainingMode.Accuracy, 2, 40)]
        [TestCase(TrainingMode.Accuracy, 3, 60)]
        [TestCase(TrainingMode.Accuracy, 4, 80)]
        [TestCase(TrainingMode.Accuracy, 5, 100)]
        [TestCase(TrainingMode.Conversation, 0, 0)]
        [TestCase(TrainingMode.Conversation, 3, 60)]
        [TestCase(TrainingMode.Conversation, 5, 100)]
        public void CompletedSession_DistributesPointsAcrossVoiceAndButtons(TrainingMode mode, int voiceSteps, int expected)
        {
            var logger = new EventLogger(_store, "test_profile", "convenience");
            logger.LogScenarioEntered(mode);
            for (int i = 0; i < Objectives.Length; i++)
            {
                logger.LogObjectiveEntered(Objectives[i]);
                logger.LogNpcTurn("말씀해 주세요", Artti.AAC.DialogueTool.PresentCards);
                if (i < voiceSteps) logger.LogCardSelected("guided_voice", "안녕하세요", Spoken(100));
                else logger.LogCardSelected("quick_phrase", "안녕하세요");
            }
            logger.LogSessionEnded("completed");

            var result = Result(logger.SessionId, mode);

            Assert.That(result.score, Is.EqualTo(expected));
            Assert.That(result.stars, Is.EqualTo(TrainingResultCalculator.StarsFor(expected)));
            Assert.That(result.accuracyPct, Is.EqualTo(voiceSteps > 0 ? 100 : -1));
        }

        private static ReportTurn Reply(bool spoken, int clarity = 100, long at = 5000, bool rejected = false, bool free = false) =>
            new ReportTurn
            {
                isUser = true, spoken = spoken, text = "안녕하세요", heardText = spoken ? "안녕하세요" : null,
                clarityPct = clarity, timestampMs = at, rejected = rejected, freeSpeech = free,
            };

        private static ReportStep Step(string objective, params ReportTurn[] replies)
        {
            var step = new ReportStep { objectiveId = objective };
            foreach (var reply in replies)
            {
                step.turns.Add(new ReportTurn { text = "말씀해 주세요", timestampMs = 1000 });
                step.turns.Add(reply);
            }
            return step;
        }

        private static int Score(params ReportStep[] steps) => TrainingResultCalculator.ScoreOf(
            new ReportRecordDetail { scenarioId = "convenience" },
            new ReportSessionDetail { steps = new List<ReportStep>(steps) });

        private static ReportStep[] FiveSteps(bool spoken = true, int clarity = 100, long at = 5000)
        {
            var steps = new ReportStep[Objectives.Length];
            for (int i = 0; i < steps.Length; i++) steps[i] = Step(Objectives[i], Reply(spoken, clarity, at));
            return steps;
        }

        [TestCase(100, 100)]
        [TestCase(80, 92)]
        [TestCase(50, 80)]
        [TestCase(0, 60)]
        [TestCase(-1, 60)]
        public void VoiceQuality_ChangesScore_AndMissingClarityGetsNoBonus(int clarity, int expected)
        {
            Assert.That(Score(FiveSteps(clarity: clarity)), Is.EqualTo(expected));
        }

        [Test]
        public void VoiceAndButtonInEveryStep_EarnHalfTheVoiceScore()
        {
            var steps = new ReportStep[Objectives.Length];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = Step(Objectives[i], Reply(true, 80), Reply(false));

            Assert.That(Score(steps), Is.EqualTo(46), "음성 92점과 버튼 0점의 평균");
        }

        [Test]
        public void ManyPerfectUtterancesInOneStep_CannotReplaceOtherSteps()
        {
            var single = Step("greeting", Reply(true));
            var many = Step("greeting", Reply(true), Reply(true), Reply(true), Reply(true), Reply(true));
            Assert.That(Score(single), Is.EqualTo(20));
            Assert.That(Score(many), Is.EqualTo(20));
        }

        [Test]
        public void ReenteredObjective_SharesItsOriginalPoints()
        {
            Assert.That(Score(Step("greeting", Reply(true)), Step("greeting", Reply(true))), Is.EqualTo(20));
            Assert.That(Score(Step("greeting", Reply(true)), Step("greeting", Reply(false))), Is.EqualTo(10));
        }

        [Test]
        public void RetryThenVoice_UsesBothAttempts_InsteadOfAddingPoints()
        {
            var steps = FiveSteps();
            steps[0] = Step("greeting", Reply(true, 40, rejected: true), Reply(true, 80));
            steps[0].retryCount = 1;

            Assert.That(Score(steps), Is.EqualTo(94), "인사 (56 + 84.5)/2, 나머지 네 단계 100");
        }

        [Test]
        public void FailedVoiceThenButton_KeepsOnlySpeakingAttemptAndMeasuredClarity()
        {
            var steps = FiveSteps(spoken: false);
            steps[0] = Step("greeting", Reply(true, 40, rejected: true), Reply(false));
            steps[0].retryCount = 1;

            Assert.That(Score(steps), Is.EqualTo(6), "실패한 음성 56점과 버튼 0점 평균 / 5단계");
        }

        [Test]
        public void RepeatedFailures_CannotRaisePerfectMixedSessionScore()
        {
            var steps = FiveSteps();
            steps[0] = Step("greeting", Reply(true), Reply(false));
            int baseline = Score(steps);
            steps[0] = Step("greeting", Reply(true, 0, rejected: true), Reply(true, 0, rejected: true), Reply(true), Reply(false));
            steps[0].retryCount = 2;

            Assert.That(baseline, Is.EqualTo(90));
            Assert.That(Score(steps), Is.LessThan(baseline));
        }

        [TestCase(5000, 100)]
        [TestCase(25000, 97)]
        [TestCase(0, 95)]
        public void Speed_HasOnlyFivePoints_AndInvalidTimeGetsNoBonus(long at, int expected)
        {
            Assert.That(Score(FiveSteps(at: at)), Is.EqualTo(expected));
            Assert.That(Score(FiveSteps(spoken: false, at: at)), Is.Zero, "빠른 버튼 응답도 0점");
        }

        [Test]
        public void NpcPromptBeforeObjectiveChange_IsUsedForNextVoiceReply()
        {
            var previous = new ReportStep { objectiveId = "greeting" };
            previous.turns.Add(new ReportTurn { text = "무엇을 드릴까요?", timestampMs = 1000 });
            var next = new ReportStep { objectiveId = "select_items" };
            next.turns.Add(Reply(true));

            Assert.That(Score(previous, next), Is.EqualTo(20));
        }

        [Test]
        public void ButtonConsumesPrompt_AndCannotGiveLaterVoiceASpeedBonus()
        {
            var step = Step("greeting", Reply(false));
            step.turns.Add(Reply(true));

            Assert.That(Score(step), Is.EqualTo(10), "음성 95점과 버튼 0점 평균 / 5단계");
        }

        [Test]
        public void SmallTalkAndEmptyRecognition_CannotRaiseScenarioScore()
        {
            var steps = FiveSteps(spoken: false);
            steps[0].turns.Add(Reply(true, free: true));
            var silent = Reply(true);
            silent.heardText = "";
            steps[1].turns.Add(silent);
            var empty = Reply(true);
            empty.text = "";
            steps[2].turns.Add(empty);

            Assert.That(Score(steps), Is.Zero);
        }

        [Test]
        public void MissingDetails_DoNotAwardPointsFromProgressAlone()
        {
            Assert.That(TrainingResultCalculator.ScoreOf(new ReportRecordDetail { successRate = 1f }, null), Is.Zero);
            Assert.That(TrainingResultCalculator.ScoreOf(null, new ReportSessionDetail()), Is.Zero);
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
