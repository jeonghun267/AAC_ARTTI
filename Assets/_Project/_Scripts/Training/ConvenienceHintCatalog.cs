using System;
using System.Collections.Generic;

namespace Artti.Training
{
    // 편의점 대화 힌트 문장의 분류(오른쪽 '대화 힌트' 버튼 5개)와 말풍선 앞 아이콘 이름.
    // 화면(ConvenienceDashboardView)은 이 표만 보고 강조할 분류와 아이콘을 고른다.
    // 문장 판정(다음 단계로 넘어가는지)은 ConvenienceQuickPhrasePolicy가 따로 맡는다.
    public static class ConvenienceHintCatalog
    {
        public const int RequestItem = 0; // 물건 요청하기
        public const int AskPrice = 1;    // 가격 묻기
        public const int Pay = 2;         // 결제하기
        public const int Extra = 3;       // 추가로 요청하기
        public const int Greeting = 4;    // 인사/마무리하기
        public const int CategoryCount = 5;

        // 분류 버튼을 눌렀을 때 왼쪽 말풍선 맨 앞에 올 문장들 (정책이 판정할 수 있는 문장 위주)
        private static readonly string[][] PhrasesByCategory =
        {
            new[] { "물 주세요", "어디에 있어요?" },
            new[] { "얼마예요?" },
            new[] { "계산할게요", "카드로 할게요", "현금으로 할게요" },
            new[] { "봉투 주세요", "봉투 필요 없어요" },
            new[] { "안녕하세요", "감사합니다" },
        };

        private static readonly Dictionary<string, (int category, string icon)> Entries =
            new Dictionary<string, (int, string)>(StringComparer.Ordinal)
            {
                { "물 주세요", (RequestItem, "basket") },
                { "이것을 주세요", (RequestItem, "basket") },
                { "어디에 있어요", (RequestItem, "pin") },
                { "얼마예요", (AskPrice, "question") },
                { "계산할게요", (Pay, "card") },
                { "카드로 할게요", (Pay, "card") },
                { "카드로 결제할게요", (Pay, "card") },
                { "현금으로 할게요", (Pay, "won") },
                { "봉투 주세요", (Extra, "bag") },
                { "봉투 필요 없어요", (Extra, "bag") },
                { "안녕하세요", (Greeting, "smile") },
                { "감사합니다", (Greeting, "smile") },
            };

        private static readonly string[] CategoryIcons = { "basket", "question", "card", "bag", "smile" };

        public static IReadOnlyList<string> PhrasesOf(int category) =>
            category >= 0 && category < PhrasesByCategory.Length ? PhrasesByCategory[category] : Array.Empty<string>();

        // 모르는 문장은 -1
        public static int CategoryOf(string phrase) =>
            Entries.TryGetValue(Normalize(phrase), out var e) ? e.category : -1;

        // 모르는 문장은 분류 아이콘, 분류도 모르면 null
        public static string IconOf(string phrase)
        {
            if (Entries.TryGetValue(Normalize(phrase), out var e)) return e.icon;
            return null;
        }

        public static string CategoryIcon(int category) =>
            category >= 0 && category < CategoryIcons.Length ? CategoryIcons[category] : null;

        private static string Normalize(string phrase) =>
            phrase == null ? string.Empty : phrase.Trim().TrimEnd('?', '!', '.', '…');
    }
}
