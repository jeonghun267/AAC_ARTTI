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
    }
}
#endif
