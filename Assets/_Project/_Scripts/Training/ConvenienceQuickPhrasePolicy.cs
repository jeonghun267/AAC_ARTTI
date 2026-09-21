using System;
using System.Collections.Generic;
using Artti.AAC;

namespace Artti.Training
{
    // 대시보드의 5개 고정 문장을 현재 objective와 장바구니 맥락에 맞게 판정한다.
    // 고정 버튼은 뜻이 분명하므로 LLM의 매 턴 해석 편차보다 이 정책을 우선한다.
    public static class ConvenienceQuickPhrasePolicy
    {
        public sealed class Decision
        {
            public DialogueTool Tool;
            public string ObjectiveId;
            public string NpcSpeech;
            public string[] CardIds;
            public string ItemToAppend;
        }

        private static readonly string[] ItemCards =
            { "card_cvs_water", "card_cvs_snack", "card_cvs_buy_this" };
        private static readonly string[] YesNoCards =
            { "card_cvs_yes", "card_cvs_no" };
        private static readonly string[] PaymentCards =
            { "card_cvs_pay_card", "card_cvs_pay_cash" };
        private static readonly string[] FarewellCards =
            { "card_cvs_thanks", "card_cvs_farewell" };

        private static readonly Dictionary<string, string> Prices = new Dictionary<string, string>
        {
            { "물", "1,000원" }, { "생수", "1,000원" }, { "캔커피", "1,500원" },
            { "우유", "1,500원" }, { "초콜릿", "1,200원" }, { "껌", "1,000원" },
            { "삼각김밥", "1,500원" }, { "샌드위치", "2,800원" }, { "컵라면", "1,800원" },
            { "충전기", "12,000원" }, { "마스크", "1,000원" }, { "우산", "8,000원" },
            { "휴지", "2,000원" }
        };

        public static bool TryResolve(
            string phrase, string objectiveId, string lastItem, bool hasItems, out Decision decision)
        {
            decision = null;
            var normalized = phrase?.Trim().TrimEnd('?', '!', '.', '…');
            if (string.IsNullOrEmpty(normalized) || string.IsNullOrEmpty(objectiveId)) return false;

            switch (normalized)
            {
                case "물 주세요":
                    decision = ResolveWater(objectiveId);
                    return true;
                case "어디에 있어요":
                    decision = ResolveLocation(objectiveId, lastItem);
                    return true;
                case "얼마예요":
                    decision = ResolvePrice(objectiveId, lastItem);
                    return true;
                case "계산할게요":
                    decision = ResolveCheckout(objectiveId, hasItems);
                    return true;
                case "감사합니다":
                    decision = ResolveThanks(objectiveId);
                    return true;
                case "봉투 주세요":
                    decision = ResolveBag(objectiveId, true);
                    return true;
                case "봉투 필요 없어요":
                    decision = ResolveBag(objectiveId, false);
                    return true;
                case "카드로 할게요":
                    decision = ResolvePayment(objectiveId, "카드");
                    return true;
                case "현금으로 할게요":
                    decision = ResolvePayment(objectiveId, "현금");
                    return true;
                default:
                    return false;
            }
        }

        private static Decision ResolveBag(string objectiveId, bool needed)
        {
            if (objectiveId != "extras")
                return Stay("계산 단계에서 봉투가 필요한지 다시 알려주세요.", YesNoCards);

            var bagLine = needed ? "네, 봉투에 담아드릴게요." : "네, 봉투 없이 준비할게요.";
            return CompleteCurrent("extras",
                $"{bagLine}\n카드와 현금 중 골라주세요.", PaymentCards);
        }

        private static Decision ResolvePayment(string objectiveId, string method)
        {
            if (objectiveId != "checkout")
                return Stay("결제 단계에서 결제 방법을 선택해주세요.", PaymentCards);

            return CompleteCurrent("checkout",
                $"네, {method} 결제가 완료됐습니다. 이용해 주셔서 감사합니다.", FarewellCards);
        }

        private static Decision ResolveWater(string objectiveId)
        {
            switch (objectiveId)
            {
                case "greeting":
                    return CompleteCurrent("greeting",
                        "네, 물 준비해 드릴게요.\n‘계산할게요’라고 직접 말해볼까요?\n어려우면 대화 힌트를 눌러도 돼요.", YesNoCards, "물");
                case "select_items":
                    return Stay("네, 물도 담아드릴게요.\n‘계산할게요’라고 직접 말해볼까요?\n어려우면 대화 힌트를 눌러도 돼요.", YesNoCards, "물");
                case "extras":
                    return Stay("네, 물도 함께 준비할게요. 봉투 필요하세요?", YesNoCards, "물");
                case "checkout":
                    return Stay("네, 물도 함께 계산할게요. 결제는 어떻게 하시겠어요?", PaymentCards, "물");
                default:
                    return Stay("계산이 끝났어요. 추가 구매는 새로 시작해서 도와드릴게요.", FarewellCards);
            }
        }

        private static Decision ResolveLocation(string objectiveId, string lastItem)
        {
            if (string.IsNullOrWhiteSpace(lastItem))
            {
                var ask = "어떤 물건의 위치가 궁금하세요?";
                return objectiveId == "greeting"
                    ? CompleteCurrent("greeting", ask, ItemCards)
                    : Stay(ask, ItemCards);
            }

            var answer = LocationFor(lastItem);
            return ResumeCurrentStep(objectiveId, answer);
        }

        private static Decision ResolvePrice(string objectiveId, string lastItem)
        {
            if (string.IsNullOrWhiteSpace(lastItem))
            {
                var ask = "어떤 상품의 가격이 궁금하세요?";
                return objectiveId == "greeting"
                    ? CompleteCurrent("greeting", ask, ItemCards)
                    : Stay(ask, ItemCards);
            }

            var price = Prices.TryGetValue(lastItem, out var known) ? known : "상품에 표시된 가격";
            return ResumeCurrentStep(objectiveId, $"{lastItem} 가격은 {price}이에요.");
        }

        private static Decision ResolveCheckout(string objectiveId, bool hasItems)
        {
            switch (objectiveId)
            {
                case "greeting":
                    return CompleteCurrent("greeting", "먼저 계산할 상품을 골라주세요.", ItemCards);
                case "select_items":
                    return hasItems
                        ? CompleteCurrent("select_items", "네, 계산 도와드릴게요. 봉투 필요하세요?", YesNoCards)
                        : Stay("먼저 계산할 상품을 골라주세요.", ItemCards);
                case "extras":
                    return Stay("계산 전에 봉투가 필요한지 알려주세요.", YesNoCards);
                case "checkout":
                    return Stay("네, 결제는 카드와 현금 중 무엇으로 하시겠어요?", PaymentCards);
                default:
                    return Stay("계산이 모두 끝났어요. 감사합니다.", FarewellCards);
            }
        }

        private static Decision ResolveThanks(string objectiveId)
        {
            switch (objectiveId)
            {
                case "greeting":
                    return CompleteCurrent("greeting", "천만에요. 찾으시는 물건 있으세요?", ItemCards);
                case "select_items":
                    return Stay("천만에요. 더 필요한 물건이 없으면 계산한다고 말씀해주세요.", ItemCards);
                case "extras":
                    return Stay("천만에요. 봉투 필요하세요?", YesNoCards);
                case "checkout":
                    return Stay("천만에요. 결제 방법을 알려주세요.", PaymentCards);
                default:
                    return CompleteCurrent("farewell", "감사합니다. 안녕히 가세요.", FarewellCards);
            }
        }

        private static Decision ResumeCurrentStep(string objectiveId, string prefix)
        {
            switch (objectiveId)
            {
                case "greeting":
                    return CompleteCurrent("greeting", $"{prefix} 더 필요한 물건 있으세요?", YesNoCards);
                case "select_items":
                    return Stay($"{prefix} 더 필요한 물건 있으세요?", YesNoCards);
                case "extras":
                    return Stay($"{prefix} 봉투 필요하세요?", YesNoCards);
                case "checkout":
                    return Stay($"{prefix} 결제는 어떻게 하시겠어요?", PaymentCards);
                default:
                    return Stay($"{prefix} 이용해 주셔서 감사합니다.", FarewellCards);
            }
        }

        private static string LocationFor(string item)
        {
            switch (item)
            {
                case "물": case "생수": case "캔커피": case "우유":
                    return $"찾으신 {item}은 음료 냉장고에 있어요.";
                case "초콜릿": case "껌":
                    return $"찾으신 {item}은 계산대 앞 간식 진열대에 있어요.";
                case "삼각김밥": case "샌드위치":
                    return $"찾으신 {item}은 냉장 식품 코너에 있어요.";
                case "컵라면":
                    return "컵라면은 라면 코너에 있어요.";
                case "충전기": case "마스크": case "우산": case "휴지":
                    return $"찾으신 {item}은 생활용품 코너에 있어요.";
                default:
                    return $"찾으신 {item}의 위치를 확인해 드릴게요.";
            }
        }

        private static Decision CompleteCurrent(
            string objectiveId, string speech, string[] cards, string item = null) =>
            new Decision
            {
                Tool = DialogueTool.MarkObjectiveComplete,
                ObjectiveId = objectiveId,
                NpcSpeech = speech,
                CardIds = cards,
                ItemToAppend = item
            };

        private static Decision Stay(string speech, string[] cards, string item = null) =>
            new Decision
            {
                Tool = DialogueTool.PresentCards,
                NpcSpeech = speech,
                CardIds = cards,
                ItemToAppend = item
            };
    }
}
