#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Artti.Common;

namespace Artti.EditorTests.Training
{
    // 정확도 모드 관문(다시 말하기) 판정
    public class TrainingModeTests
    {
        private const int Pass = TrainingModeContext.AccuracyPassPercent;

        [Test]
        public void AccuracyMode_BelowPass_Retries()
        {
            Assert.That(TrainingModeContext.ShouldRetry(TrainingMode.Accuracy, true, Pass - 1, 0), Is.True);
        }

        [Test]
        public void AccuracyMode_AtOrAbovePass_Passes()
        {
            Assert.That(TrainingModeContext.ShouldRetry(TrainingMode.Accuracy, true, Pass, 0), Is.False);
            Assert.That(TrainingModeContext.ShouldRetry(TrainingMode.Accuracy, true, 100, 0), Is.False);
        }

        [Test]
        public void AccuracyMode_WithoutClarity_Passes()
        {
            // 기기 STT 폴백 등 정확도를 잴 수 없으면 막지 않는다
            Assert.That(TrainingModeContext.ShouldRetry(TrainingMode.Accuracy, false, 0, 0), Is.False);
        }

        [Test]
        public void AccuracyMode_AfterMaxRetries_Passes()
        {
            Assert.That(TrainingModeContext.ShouldRetry(TrainingMode.Accuracy, true, 0, TrainingModeContext.AccuracyMaxRetries - 1), Is.True);
            Assert.That(TrainingModeContext.ShouldRetry(TrainingMode.Accuracy, true, 0, TrainingModeContext.AccuracyMaxRetries), Is.False);
        }

        [Test]
        public void ConversationMode_NeverRetries()
        {
            Assert.That(TrainingModeContext.ShouldRetry(TrainingMode.Conversation, true, 0, 0), Is.False);
        }

        [Test]
        public void MaxRetries_StaysBelowForcedCompletion()
        {
            // ScaffoldedRetryPolicy는 실패 4회에서 시나리오를 강제 마무리한다. 관문만으로 그 선에 닿으면 안 된다.
            Assert.That(new Artti.Training.ScaffoldedRetryPolicy().ShouldForceComplete(TrainingModeContext.AccuracyMaxRetries), Is.False);
        }
    }
}
#endif
