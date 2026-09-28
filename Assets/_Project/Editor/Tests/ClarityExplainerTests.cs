#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Artti.Report;

namespace Artti.EditorTests.Report
{
    public class ClarityExplainerTests
    {
        [Test]
        public void SameSentence_Green_SaysClear()
        {
            Assert.That(ClarityExplainer.Explain("물 주세요", "물 주세요.", 92, "green"),
                        Is.EqualTo("목표 문장 그대로 또렷하게 알아들었어요."));
        }

        [Test]
        public void SameSentence_Yellow_SaysPronunciationSlightlyUnclear()
        {
            Assert.That(ClarityExplainer.Explain("물 주세요", "물 주세요", 64, "yellow"), Does.Contain("조금 흐리게"));
        }

        [TestCase("물 주세요", "무 주세요", "‘물’ 부분이 ‘무’로 들렸어요.")]
        [TestCase("감사합니다", "감사합니당", "‘다’ 부분이 ‘당’으로 들렸어요.")]
        [TestCase("계산할게요", "계산할게", "‘요’ 부분이 들리지 않았어요.")]
        [TestCase("물 주세요", "물 좀 주세요", "‘좀’ 소리가 더 들렸어요.")]
        public void SmallDifferences_AreNamed(string target, string heard, string expected)
        {
            Assert.That(ClarityExplainer.Explain(target, heard, 70, "yellow"), Is.EqualTo(expected));
        }

        [Test]
        public void VeryDifferentSentence_SaysSoAndQuotesHeard()
        {
            var reason = ClarityExplainer.Explain("물 주세요", "감사합니다", 10, "red");

            Assert.That(reason, Does.StartWith("목표 문장과 많이 다르게 들렸어요."));
            Assert.That(reason, Does.Contain("감사합니다"));
        }

        [Test]
        public void NoTarget_FallsBackToServerLight()
        {
            Assert.That(ClarityExplainer.Explain(null, "물 주세요", 80, "green"), Is.EqualTo("서버 판정: 또렷함(초록)."));
        }

        [Test]
        public void NoClarity_ReturnsNull()
        {
            Assert.That(ClarityExplainer.Explain("물 주세요", "물 주세요", -1, null), Is.Null);
        }
    }
}
#endif
