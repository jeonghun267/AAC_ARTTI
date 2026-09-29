#if UNITY_INCLUDE_TESTS
using Artti.AAC;
using Artti.Training;
using NUnit.Framework;

namespace Artti.EditorTests.Training
{
    public class ConvenienceQuickPhrasePolicyTests
    {
        [TestCase("물 주세요")]
        [TestCase("어디에 있어요?")]
        [TestCase("얼마예요?")]
        [TestCase("계산할게요")]
        [TestCase("감사합니다")]
        public void FiveDashboardPhrases_AreAllHandled(string phrase)
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryResolve(
                phrase, "select_items", "물", true, out var decision), Is.True);
            Assert.That(decision, Is.Not.Null);
            Assert.That(decision.NpcSpeech, Is.Not.Empty);
        }

        [Test]
        public void WaterAtGreeting_EntersItemSelectionButDoesNotFinishIt()
        {
            ConvenienceQuickPhrasePolicy.TryResolve(
                "물 주세요", "greeting", null, false, out var decision);

            Assert.That(decision.Tool, Is.EqualTo(DialogueTool.MarkObjectiveComplete));
            Assert.That(decision.ObjectiveId, Is.EqualTo("greeting"));
            Assert.That(decision.ItemToAppend, Is.EqualTo("물"));
            Assert.That(decision.NpcSpeech, Does.Contain("계산할게요"));
        }

        [Test]
        public void PriceWithLastItem_AnswersThatItemPriceAndStays()
        {
            ConvenienceQuickPhrasePolicy.TryResolve(
                "얼마예요?", "select_items", "물", true, out var decision);

            Assert.That(decision.Tool, Is.EqualTo(DialogueTool.PresentCards));
            Assert.That(decision.NpcSpeech, Does.Contain("1,000원"));
        }

        [Test]
        public void CheckoutWithItems_CompletesSelectionAndAsksForBag()
        {
            ConvenienceQuickPhrasePolicy.TryResolve(
                "계산할게요", "select_items", "물", true, out var decision);

            Assert.That(decision.Tool, Is.EqualTo(DialogueTool.MarkObjectiveComplete));
            Assert.That(decision.ObjectiveId, Is.EqualTo("select_items"));
            Assert.That(decision.NpcSpeech, Does.Contain("봉투"));
        }

        [Test]
        public void ThanksBeforeFarewell_DoesNotEndScenario()
        {
            ConvenienceQuickPhrasePolicy.TryResolve(
                "감사합니다", "checkout", "물", true, out var decision);

            Assert.That(decision.Tool, Is.EqualTo(DialogueTool.PresentCards));
            Assert.That(decision.NpcSpeech, Does.Contain("결제 방법"));
        }

        [TestCase("봉투 주세요")]
        [TestCase("봉투 필요 없어요")]
        public void BagAnswer_CompletesExtrasAndAsksPayment(string phrase)
        {
            ConvenienceQuickPhrasePolicy.TryResolve(
                phrase, "extras", "물", true, out var decision);

            Assert.That(decision.Tool, Is.EqualTo(DialogueTool.MarkObjectiveComplete));
            Assert.That(decision.ObjectiveId, Is.EqualTo("extras"));
            Assert.That(decision.NpcSpeech, Does.Contain("카드와 현금"));
        }

        [TestCase("카드로 할게요", "카드")]
        [TestCase("현금으로 할게요", "현금")]
        public void PaymentAnswer_CompletesCheckout(string phrase, string method)
        {
            ConvenienceQuickPhrasePolicy.TryResolve(
                phrase, "checkout", "물", true, out var decision);

            Assert.That(decision.Tool, Is.EqualTo(DialogueTool.MarkObjectiveComplete));
            Assert.That(decision.ObjectiveId, Is.EqualTo("checkout"));
            Assert.That(decision.NpcSpeech, Does.Contain(method));
        }

        private static readonly string[] BagCandidates =
            { "봉투 주세요", "봉투 필요 없어요", "얼마예요?", "계산할게요", "감사합니다" };

        [TestCase("봉투 주세요", ExpectedResult = true)]      // 목표 그대로
        [TestCase("봉두 주세요.", ExpectedResult = true)]     // 발음이 흐려도 목표에 가장 가까움
        [TestCase("봉투 필요 없어요", ExpectedResult = false)] // 다른 정답을 고름
        [TestCase("감사합니다", ExpectedResult = false)]
        [TestCase("", ExpectedResult = false)]
        public bool IsAttemptAtTarget_BagStep(string recognized)
        {
            return ConvenienceQuickPhrasePolicy.IsAttemptAtTarget(recognized, "봉투 주세요", BagCandidates);
        }

        [Test]
        public void IsAttemptAtTarget_OtherProductRequest_IsNotScoredAgainstWater()
        {
            var candidates = new[] { "물 주세요", "어디에 있어요?", "얼마예요?", "계산할게요", "감사합니다", "우유 주세요", "껌 주세요" };

            Assert.That(ConvenienceQuickPhrasePolicy.IsAttemptAtTarget("우유 주세요", "물 주세요", candidates), Is.False);
            Assert.That(ConvenienceQuickPhrasePolicy.IsAttemptAtTarget("물 주세요", "물 주세요", candidates), Is.True);
        }

        [Test]
        public void IsAttemptAtTarget_NoTarget_IsFalse()
        {
            Assert.That(ConvenienceQuickPhrasePolicy.IsAttemptAtTarget("물 주세요", null, BagCandidates), Is.False);
        }

        // 대화 모드: 어눌한 발화를 후보 문장으로 해석
        private static readonly string[] ItemCandidates =
            { "물 주세요", "어디에 있어요?", "얼마예요?", "계산할게요", "감사합니다", "우유 주세요" };

        [TestCase("물 주세요", ExpectedResult = "물 주세요")]       // 정확히 말함
        [TestCase("무 주세여", ExpectedResult = "물 주세요")]       // 어눌하지만 가까움
        [TestCase("계산할께요", ExpectedResult = "계산할게요")]     // 추천 문장이 아닌 다른 후보
        [TestCase("우유 주세여", ExpectedResult = "우유 주세요")]   // 상품 요청 문장
        [TestCase("아아아아아아", ExpectedResult = "물 주세요")]    // 어느 후보와도 멀면 추천 문장
        public string ResolveConversationUtterance_ItemStep(string recognized)
        {
            return ConvenienceQuickPhrasePolicy.ResolveConversationUtterance(recognized, "물 주세요", ItemCandidates);
        }

        [Test]
        public void ResolveConversationUtterance_NoTargetAndNoMatch_KeepsRecognized()
        {
            Assert.That(ConvenienceQuickPhrasePolicy.ResolveConversationUtterance("아아아아아아", null, ItemCandidates),
                Is.EqualTo("아아아아아아"));
        }
    }
}
#endif
