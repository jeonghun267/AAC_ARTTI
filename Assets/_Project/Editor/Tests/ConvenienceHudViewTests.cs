#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using Artti.Training;

namespace Artti.EditorTests.Training
{
    public class ConvenienceHudViewTests
    {
        [TestCase(0, 5, 0f)]
        [TestCase(1, 5, 0.25f)]
        [TestCase(2, 5, 0.5f)]
        [TestCase(3, 5, 0.75f)]
        [TestCase(4, 5, 1f)]
        public void StepFillRatio_AlignsWithNodePositions(int index, int count, float expected)
        {
            var method = typeof(ConvenienceHudView).GetMethod(
                "CalculateStepFillRatio", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            var actual = (float)method.Invoke(null, new object[] { index, count });
            Assert.That(actual, Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
#endif
