#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
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
        public void TryMatchCandidate_FarFromEverything_IsLeftForAiJudge()
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryMatchCandidate("생수 하나요", "물 주세요", ItemCandidates, out var match), Is.False);
            Assert.That(match, Is.Null);
            Assert.That(ConvenienceQuickPhrasePolicy.TryMatchCandidate("무 주세여", "물 주세요", ItemCandidates, out match), Is.True);
            Assert.That(match, Is.EqualTo("물 주세요"));
        }

        // 변경점: 상품 이름만 말해도(어눌해도) 그 상품 요청 문장으로. 추천 문장("물 주세요")으로 넘어가지 않는다
        private static readonly string[] ProductCandidates =
            { "물 주세요", "어디에 있어요?", "얼마예요?", "계산할게요", "감사합니다", "과자 주세요", "우유 주세요", "삼각김밥 주세요" };

        [TestCase("과자", ExpectedResult = "과자 주세요")]
        [TestCase("가자", ExpectedResult = "과자 주세요")]        // 어눌하게 한 글자 틀림
        [TestCase("과자 주세여", ExpectedResult = "과자 주세요")]
        [TestCase("우유", ExpectedResult = "우유 주세요")]
        [TestCase("삼각김바", ExpectedResult = "삼각김밥 주세요")]
        [TestCase("물", ExpectedResult = "물 주세요")]
        public string TryMatchCandidate_ProductNameAlone(string recognized)
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryMatchCandidate(recognized, "물 주세요", ProductCandidates, out var match), Is.True);
            return match;
        }

        [TestCase("네")]      // 한 글자 이름("물")은 똑같을 때만
        [TestCase("뭘")]
        public void TryMatchCandidate_OneLetterName_NeedsExactMatch(string recognized)
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryMatchCandidate(recognized, "물 주세요", ProductCandidates, out _), Is.False);
        }

        // 변경점: 어눌한 발음(자모 비교 + 헷갈리는 소리 반만 틀림). 단계별 화면 후보로 확인한다
        private static readonly string[] GreetingCandidates =
            { "물 주세요", "어디에 있어요?", "얼마예요?", "계산할게요", "감사합니다",
              "생수 주세요", "삼각김밥 주세요", "컵라면 주세요", "과자 주세요", "초콜릿 주세요", "캔커피 주세요", "우유 주세요", "샌드위치 주세요" };
        private static readonly string[] PayCandidates =
            { "카드로 할게요", "현금으로 할게요", "얼마예요?", "계산할게요", "감사합니다" };

        [TestCase("가자", ExpectedResult = "과자 주세요")]             // ㅘ -> ㅏ
        [TestCase("꽈자 주세요", ExpectedResult = "과자 주세요")]       // ㄱ -> ㄲ
        [TestCase("음 과자요", ExpectedResult = "과자 주세요")]         // 이름이 말 속에 들어 있음
        [TestCase("초코릿 주떼요", ExpectedResult = "초콜릿 주세요")]   // 받침 빠짐, ㅅ -> ㄸ
        [TestCase("컴나면 주세여", ExpectedResult = "컵라면 주세요")]   // 받침·초성 바뀜, ㅛ -> ㅕ
        [TestCase("캥커피", ExpectedResult = "캔커피 주세요")]
        [TestCase("새드위치", ExpectedResult = "샌드위치 주세요")]
        [TestCase("얼마에요", ExpectedResult = "얼마예요?")]            // ㅖ -> ㅔ
        [TestCase("게산 하께요", ExpectedResult = "계산할게요")]        // ㅖ -> ㅔ, 받침 빠짐
        [TestCase("감사함니다", ExpectedResult = "감사합니다")]
        [TestCase("어디 이써요", ExpectedResult = "어디에 있어요?")]
        public string Slurred_GreetingStep(string recognized)
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryMatchCandidate(recognized, "물 주세요", GreetingCandidates, out var match), Is.True,
                $"'{recognized}'를 못 알아들음");
            return match;
        }

        [TestCase("가드로 할께요", ExpectedResult = "카드로 할게요")]    // ㅋ -> ㄱ, ㄱ -> ㄲ
        [TestCase("카드요", ExpectedResult = "카드로 할게요")]
        [TestCase("형그므로 하께요", ExpectedResult = "현금으로 할게요")]
        public string Slurred_PayStep(string recognized)
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryMatchCandidate(recognized, "카드로 할게요", PayCandidates, out var match), Is.True,
                $"'{recognized}'를 못 알아들음");
            return match;
        }

        [TestCase("봉두 주떼요", ExpectedResult = "봉투 주세요")]
        [TestCase("봉투 필요 업써요", ExpectedResult = "봉투 필요 없어요")]
        public string Slurred_BagStep(string recognized)
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryMatchCandidate(recognized, "봉투 주세요", BagCandidates, out var match), Is.True);
            return match;
        }

        [Test]
        public void Slurred_SecondGuessOfRecognizer_IsUsed()
        {
            var options = new List<string> { "가다 주세요", "과자 주세요" };   // 첫 추측은 틀리고 두 번째가 맞음
            Assert.That(ConvenienceQuickPhrasePolicy.TryMatchCandidate(options, "물 주세요", GreetingCandidates, out var match), Is.True);
            Assert.That(match, Is.EqualTo("과자 주세요"));
        }

        [TestCase("오늘 날씨 좋네요")]   // 관계없는 말은 AI 판단·다시 묻기로 넘긴다
        [TestCase("아아아아아아")]
        [TestCase("네")]
        public void Unrelated_IsLeftForAiOrRetry(string recognized)
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryMatchCandidate(recognized, "물 주세요", GreetingCandidates, out _), Is.False);
        }

        // 변경점: AI 판단 답 읽기 - 번호 / 0 = 알아들었지만 해당 없음(일상 대화) / X·이상한 답 = 모름
        [TestCase("3", 5, ExpectedResult = 2)]
        [TestCase(" 1\n", 5, ExpectedResult = 0)]
        [TestCase("0", 5, ExpectedResult = GeminiDialogueService.PhraseNone)]
        [TestCase("X", 5, ExpectedResult = GeminiDialogueService.PhraseUnclear)]
        [TestCase("9", 5, ExpectedResult = GeminiDialogueService.PhraseUnclear)]
        [TestCase(null, 5, ExpectedResult = GeminiDialogueService.PhraseUnclear)]
        public int ParsePhraseMatch(string reply, int count) => GeminiDialogueService.ParsePhraseMatch(reply, count);

        [Test]
        public void ResolveConversationUtterance_NoTargetAndNoMatch_KeepsRecognized()
        {
            Assert.That(ConvenienceQuickPhrasePolicy.ResolveConversationUtterance("아아아아아아", null, ItemCandidates),
                Is.EqualTo("아아아아아아"));
        }

        // 변경점: 구음진단모드는 인식 원문이 그대로 오므로 띄어쓰기·문장부호·같은 뜻의 말도 고정 문장 규칙으로 처리해야 한다
        [TestCase("계산 할게요.")]
        [TestCase("결제할게요")]
        [TestCase("결제 해 주세요")]
        [TestCase("계산할께요!")]
        public void CheckoutVariants_UseTheCheckoutRule(string heard)
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryResolve(heard, "select_items", "물", true, out var decision), Is.True);
            Assert.That(decision.Tool, Is.EqualTo(DialogueTool.MarkObjectiveComplete));
            Assert.That(decision.ObjectiveId, Is.EqualTo("select_items"));
            Assert.That(decision.NpcSpeech, Does.Contain("봉투"));
        }

        [TestCase("카드로 결제할게요", "카드")]
        [TestCase("현금으로 계산 할게요", "현금")]
        public void PaymentVariants_CompleteCheckout(string heard, string method)
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryResolve(heard, "checkout", "물", true, out var decision), Is.True);
            Assert.That(decision.Tool, Is.EqualTo(DialogueTool.MarkObjectiveComplete));
            Assert.That(decision.NpcSpeech, Does.Contain(method));
        }

        [Test]
        public void UnknownSentence_IsNotAFixedPhrase()
        {
            Assert.That(ConvenienceQuickPhrasePolicy.TryResolve("오늘 날씨 좋네요", "select_items", "물", true, out _), Is.False);
        }

        // 변경점: 단계가 맞지 않는 답에 "결제 단계에서 …", "계산 단계에서 …" 같은 안내문을 말하지 않고 지금 단계 질문으로 돌아온다
        [TestCase("카드로 할게요", "select_items")]
        [TestCase("현금으로 할게요", "extras")]
        [TestCase("봉투 주세요", "select_items")]
        [TestCase("봉투 필요 없어요", "checkout")]
        [TestCase("계산할게요", "extras")]
        public void OutOfStepAnswer_StaysWithNaturalClerkLine(string phrase, string objectiveId)
        {
            ConvenienceQuickPhrasePolicy.TryResolve(phrase, objectiveId, "물", true, out var decision);
            Assert.That(decision.Tool, Is.EqualTo(DialogueTool.PresentCards));
            Assert.That(decision.NpcSpeech, Does.Not.Contain("단계"));
            Assert.That(decision.NpcSpeech, Does.EndWith("?"));
        }

        // 변경점: 점원 대사가 한국어가 아니면(영어 문장, 한자·가나 섞임) 읽지 않는다
        [TestCase("네, 계산 도와드릴게요. 봉투 필요하세요?", ExpectedResult = true)]
        [TestCase("네, GS25 카드로 결제 도와드릴게요.", ExpectedResult = true)]
        [TestCase("", ExpectedResult = true)]
        [TestCase("Sure, I can help you with the payment.", ExpectedResult = false)]
        [TestCase("네, 结账 도와드릴게요.", ExpectedResult = false)]
        [TestCase("はい、봉투 필요하세요?", ExpectedResult = false)]
        public bool IsKoreanSpeech(string line) => GeminiDialogueService.IsKoreanSpeech(line);
    }
}
#endif
