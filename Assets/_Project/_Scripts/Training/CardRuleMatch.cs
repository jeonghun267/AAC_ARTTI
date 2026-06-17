namespace Artti.Training
{
    // 편의점 AAC 카드 룰베이스 매칭 결과.
    // OCR 모드의 KeywordMatch 패턴을 참고해 작성(원본 OCR 클래스는 건드리지 않음).
    // KeywordMatch가 "텍스트 → 카테고리"였다면, 이쪽은 "발화 → 맥락에 맞는 카드 목록"이다.
    public class CardRuleMatch
    {
        public string RuleId { get; }          // "select_items", "location_subflow" 등 규칙 식별자
        public string Objective { get; }       // 연결된 objective (서브플로 전용 규칙이면 null)
        public string Branch { get; }          // 서브플로 branchId (없으면 null)
        public string[] CardIds { get; }       // 표시 우선순위대로 정렬된 카드 id (cards 배열 순서)
        public string MatchedKeyword { get; }  // 어떤 키워드로 매칭됐는지 (objective 폴백이면 null)

        public CardRuleMatch(string ruleId, string objective, string branch, string[] cardIds, string matchedKeyword)
        {
            RuleId = ruleId;
            Objective = objective;
            Branch = branch;
            CardIds = cardIds ?? System.Array.Empty<string>();
            MatchedKeyword = matchedKeyword;
        }
    }
}
