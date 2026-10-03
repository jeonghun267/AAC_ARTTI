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
            { "휴지", "2,000원" }, { "과자", "1,700원" }   // 변경점: 추천 상품에 과자 추가(TrainingCon 시안)
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
                        "네, 물 준비해 드릴게요.\n‘계산할게요’라고 직접 말해볼까요?\n어려우면 왼쪽 버튼을 눌러도 돼요.", YesNoCards, "물");
                case "select_items":
                    return Stay("네, 물도 담아드릴게요.\n‘계산할게요’라고 직접 말해볼까요?\n어려우면 왼쪽 버튼을 눌러도 돼요.", YesNoCards, "물");
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
                case "초콜릿": case "껌": case "과자":
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

        // 변경점: 음성 버튼 발화의 명료도(서버 STT, 목표 문장 대비)를 기록해도 되는지 판정.
        //   인식 문장이 목표보다 다른 후보(다른 대화 힌트, 다른 상품 요청)에 더 가까우면 사용자가 다른 답을 고른 것이다.
        //   그때 목표 대비 명료도는 발음이 아니라 문장 차이를 재므로 상세기록에 남기지 않는다.
        public static bool IsAttemptAtTarget(string recognized, string target, IEnumerable<string> candidates)
        {
            var r = NormalizeSpeech(recognized);
            var t = NormalizeSpeech(target);
            if (r.Length == 0 || t.Length == 0) return false;
            if (candidates == null) return true;

            int toTarget = EditDistance(r, t);
            foreach (var candidate in candidates)
            {
                var c = NormalizeSpeech(candidate);
                if (c.Length == 0 || c == t) continue;
                if (EditDistance(r, c) < toTarget) return false;
            }
            return true;
        }

        // 대화 모드: 어눌한 인식 문장을 화면 후보 문장 중 가장 가까운 것으로 해석한다.
        //   어느 후보와도 충분히 가깝지 않으면(편집 거리 > 후보 글자 수의 절반) 추천 문장(target)을 말한 것으로 본다.
        //   동점이면 추천 문장을 우선한다. 인식 원문과 명료도 기록은 호출 측 책임.
        public static string ResolveConversationUtterance(string recognized, string target, IEnumerable<string> candidates)
        {
            if (TryMatchCandidate(recognized, target, candidates, out var match)) return match;
            return ConversationFallback(recognized, target);
        }

        private const string RequestEnding = "주세요";
        // 변경점(2026-10-02): 발음 거리(KoreanSpeechSimilarity, 0~1) 기준. 자모로 나눠 비교하고 헷갈리는 소리는 반만 틀린 것으로 센다
        public const float SentenceThreshold = 0.35f;   // 문장 전체와 비교할 때
        public const float NameThreshold = 0.3f;        // 상품 이름만 말했을 때("과자", "가자")
        private const float NameContainedScore = 0.1f;  // 상품 이름이 말 속에 그대로 들어 있을 때("음 과자요")
        private const float AlternativePenalty = 0.05f; // 인식기의 두 번째·세 번째 추측은 조금 덜 믿는다

        // 규칙 판정만: 기준 이내로 가까운 후보가 있으면 true. 없으면(애매함) 호출 측이 AI 판단 또는 다시 묻기를 쓴다.
        public static bool TryMatchCandidate(string recognized, string target, IEnumerable<string> candidates, out string match) =>
            TryMatchCandidate(new[] { recognized }, target, candidates, out match);

        // 변경점: 어눌한 발음도 알아듣게(사용자가 가장 중요하다고 한 기능).
        //   heardOptions = 인식기의 추측들(첫 번째가 가장 그럴듯함). 휴대폰 인식은 어눌한 말일수록 두세 번째 추측이 맞을 때가 많다.
        //   후보마다 발음 거리를 재서 가장 가까운 것, 동점이면 추천 문장. 예전에는 글자 단위 편집 거리라 "가자"와 "과자"를 완전히 다른 글자로 봤다.
        //   상품 요청("과자 주세요")은 이름만 말해도 된다. 한 글자 이름("물")은 똑같을 때만("네"가 "물"로 잡히지 않게).
        public static bool TryMatchCandidate(IList<string> heardOptions, string target, IEnumerable<string> candidates, out string match)
        {
            match = null;
            if (heardOptions == null || candidates == null) return false;

            string best = null;
            float bestScore = float.MaxValue;
            void Consider(string candidate, float score, float allowed)
            {
                if (score > allowed) return;
                if (score < bestScore || (System.Math.Abs(score - bestScore) < 0.0001f && candidate == target))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            var list = new List<string>();
            foreach (var candidate in candidates)
                if (!string.IsNullOrWhiteSpace(candidate)) list.Add(candidate);
            var keywords = UniqueKeywords(list);

            for (int rank = 0; rank < heardOptions.Count; rank++)
            {
                var heard = KoreanSpeechSimilarity.Compact(heardOptions[rank]);
                if (heard.Length == 0) continue;
                var shortAnswer = StripPoliteEnding(heard);   // "카드요" -> "카드", "현금이요" -> "현금"
                float penalty = rank * AlternativePenalty;
                for (int i = 0; i < list.Count; i++)
                {
                    var candidate = list[i];
                    var c = KoreanSpeechSimilarity.Compact(candidate);
                    if (c.Length == 0) continue;
                    Consider(candidate, KoreanSpeechSimilarity.Distance(heard, c) + penalty, SentenceThreshold + penalty);

                    // 핵심 단어만 말한 짧은 대답: 상품 이름("과자 주세요" -> "과자"), 첫 단어에서 조사를 뗀 것("카드로 할게요" -> "카드")
                    var key = keywords[i];
                    if (key == null) continue;
                    if (key.Length == 1)
                    {
                        if (heard == key || shortAnswer == key) Consider(candidate, penalty, NameThreshold + penalty);
                        continue;
                    }
                    if (heard.Contains(key)) Consider(candidate, NameContainedScore + penalty, NameThreshold + penalty);
                    else
                    {
                        float d = System.Math.Min(KoreanSpeechSimilarity.Distance(heard, key), KoreanSpeechSimilarity.Distance(shortAnswer, key));
                        Consider(candidate, d + penalty, NameThreshold + penalty);
                    }
                }
            }

            if (best == null) return false;
            match = best;
            return true;
        }

        static readonly string[] Particles = { "으로", "로", "에서", "에", "를", "을", "이", "가", "은", "는", "도" };

        // 후보마다 핵심 단어(없거나 다른 후보와 겹치면 null). 겹치는 단어("봉투 주세요"/"봉투 필요 없어요"의 "봉투")로는 고르지 않는다
        static string[] UniqueKeywords(IList<string> candidates)
        {
            var keys = new string[candidates.Count];
            var count = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                keys[i] = KeywordOf(candidates[i]);
                if (keys[i] == null) continue;
                count.TryGetValue(keys[i], out int n);
                count[keys[i]] = n + 1;
            }
            for (int i = 0; i < keys.Length; i++)
                if (keys[i] != null && count[keys[i]] > 1) keys[i] = null;
            return keys;
        }

        static string KeywordOf(string candidate)
        {
            var c = KoreanSpeechSimilarity.Compact(candidate);
            if (c.Length > RequestEnding.Length && c.EndsWith(RequestEnding, StringComparison.Ordinal))
                return c.Substring(0, c.Length - RequestEnding.Length);   // 상품 이름(한 글자도 됨: "물")
            var words = candidate.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2) return null;
            var first = KoreanSpeechSimilarity.Compact(words[0]);
            foreach (var p in Particles)
            {
                if (first.Length - p.Length >= 2 && first.EndsWith(p, StringComparison.Ordinal))
                {
                    first = first.Substring(0, first.Length - p.Length);
                    break;
                }
            }
            return first.Length >= 2 ? first : null;
        }

        // 짧은 대답 끝의 "요"/"이요"를 뗀다(남는 게 없으면 그대로)
        static string StripPoliteEnding(string compact)
        {
            if (compact.Length > 2 && compact.EndsWith("이요", StringComparison.Ordinal)) return compact.Substring(0, compact.Length - 2);
            if (compact.Length > 1 && compact.EndsWith("요", StringComparison.Ordinal)) return compact.Substring(0, compact.Length - 1);
            return compact;
        }

        // 규칙도 AI도 고르지 못했을 때: 추천 문장, 추천 문장이 없으면 인식 원문 그대로
        public static string ConversationFallback(string recognized, string target) =>
            string.IsNullOrWhiteSpace(target) ? recognized : target;

        // 공백·문장부호·따옴표를 뺀 비교용 문자열
        private static string NormalizeSpeech(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s)
                if (!char.IsWhiteSpace(ch) && !char.IsPunctuation(ch) && !char.IsSymbol(ch)) sb.Append(ch);
            return sb.ToString();
        }

        // 글자(음절) 단위 편집 거리
        private static int EditDistance(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                var swap = prev; prev = cur; cur = swap;
            }
            return prev[b.Length];
        }
    }
}
