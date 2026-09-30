#if UNITY_INCLUDE_TESTS
using Artti.Training;
using NUnit.Framework;

namespace Artti.EditorTests.Training
{
    public class ClarityGatePolicyTests
    {
        [Test]
        public void AtOrAboveThreshold_Passes()
        {
            var gate = new ClarityGatePolicy(60, 2);
            Assert.That(gate.Evaluate("greeting", true, 60), Is.EqualTo(ClarityVerdict.Pass));
            Assert.That(gate.Evaluate("greeting", true, 95), Is.EqualTo(ClarityVerdict.Pass));
        }

        [Test]
        public void WithoutClarity_IsNotBlocked()
        {
            var gate = new ClarityGatePolicy(60, 2);
            Assert.That(gate.Evaluate("greeting", false, 0), Is.EqualTo(ClarityVerdict.NotMeasured));
            Assert.That(gate.Failures, Is.EqualTo(0));
        }

        [Test]
        public void BelowThreshold_RetriesThenLetsThrough()
        {
            var gate = new ClarityGatePolicy(60, 2);
            Assert.That(gate.Evaluate("checkout", true, 40), Is.EqualTo(ClarityVerdict.Retry));
            Assert.That(gate.Evaluate("checkout", true, 45), Is.EqualTo(ClarityVerdict.Retry));
            Assert.That(gate.Evaluate("checkout", true, 50), Is.EqualTo(ClarityVerdict.PassAfterRetries));
            Assert.That(gate.Failures, Is.EqualTo(2));
        }

        [Test]
        public void NewObjective_ResetsFailures()
        {
            var gate = new ClarityGatePolicy(60, 1);
            Assert.That(gate.Evaluate("select_items", true, 30), Is.EqualTo(ClarityVerdict.Retry));
            Assert.That(gate.Evaluate("checkout", true, 30), Is.EqualTo(ClarityVerdict.Retry));
            Assert.That(gate.Failures, Is.EqualTo(1));
        }

        [Test]
        public void RetryLine_GetsMoreSpecificOnSecondFailure()
        {
            var gate = new ClarityGatePolicy(60, 3);
            gate.Evaluate("greeting", true, 10);
            var first = gate.RetryLine();
            gate.Evaluate("greeting", true, 10);
            Assert.That(gate.RetryLine(), Is.Not.EqualTo(first));
        }
    }
}
#endif
