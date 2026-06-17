using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Artti.Training
{
    // 편의점 AAC 카드 룰베이스 로더 + 매칭 엔진.
    // OCR 모드 KeywordDictionary(텍스트 → 키워드 부분일치 → 카테고리) 패턴을 참고해 작성.
    // 원본 OCR 클래스(KeywordDictionary/KeywordMatch)는 절대 수정하지 않는다.
    //
    // 데이터: Resources/AAC/convenience_card_rules.json (FallbackResponsePicker와 동일한 Resources 로드 규칙)
    // 사용:
    //   var book = new ConvenienceCardRuleBook(textAsset.text);
    //   var match = book.Match("찾으시는 물건 있으세요?");   // 점원 발화로 맥락 추론
    //   match ??= book.ResolveByObjective("select_items");   // 키워드 미스 시 objective 폴백
    //   // match.CardIds = 표시 우선순위 순서대로의 카드 id
    public class ConvenienceCardRuleBook
    {
        private class Rule
        {
            public string id;
            public string objective;
            public string branch;
            public string[] keywords;   // 정규화(공백 제거) 완료 상태로 보관
            public string[] cards;
        }

        private readonly List<Rule> _rules = new List<Rule>();
        private readonly Dictionary<string, Rule> _byObjective = new Dictionary<string, Rule>();
        private bool _loaded;

        public bool IsLoaded => _loaded;

        public ConvenienceCardRuleBook(string jsonContent)
        {
            try
            {
                var root = JObject.Parse(jsonContent);
                var rules = root["rules"] as JArray;
                if (rules == null) return;

                foreach (var r in rules)
                {
                    var rule = new Rule
                    {
                        id = (string)r["id"],
                        objective = (string)r["objective"],
                        branch = (string)r["branch"],
                        keywords = ToArray(r["keywords"], normalize: true),
                        cards = ToArray(r["cards"], normalize: false)
                    };
                    // 카드 없는 규칙은 풀을 못 채우므로 제외 (예: 카드 미정의 서브플로)
                    if (rule.cards.Length == 0) continue;

                    _rules.Add(rule);
                    if (!string.IsNullOrEmpty(rule.objective) && !_byObjective.ContainsKey(rule.objective))
                        _byObjective[rule.objective] = rule;
                }

                _loaded = _rules.Count > 0;
                Debug.Log($"[ConvenienceCardRuleBook] 로드 완료. 규칙 수: {_rules.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ConvenienceCardRuleBook] JSON 파싱 실패: {e.Message}");
            }
        }

        // 발화 텍스트 키워드 매칭. KeywordDictionary.Match와 동일하게 정규화 후 부분일치(Contains).
        // 규칙은 JSON 정의 순서대로 검사 — 서브플로 규칙을 먼저 둬서 분기 상황을 우선 포착.
        public CardRuleMatch Match(string contextText)
        {
            if (string.IsNullOrEmpty(contextText) || !_loaded) return null;

            string normalized = Normalize(contextText);

            foreach (var rule in _rules)
            {
                foreach (var kw in rule.keywords)
                {
                    if (string.IsNullOrEmpty(kw)) continue;
                    if (normalized.Contains(kw))
                        return new CardRuleMatch(rule.id, rule.objective, rule.branch, rule.cards, kw);
                }
            }
            return null;
        }

        // objective 직접 조회 (키워드 매칭 실패 시 폴백). 빈 풀/오출력 방지용.
        public CardRuleMatch ResolveByObjective(string objective)
        {
            if (!_loaded || string.IsNullOrEmpty(objective)) return null;
            return _byObjective.TryGetValue(objective, out var rule)
                ? new CardRuleMatch(rule.id, rule.objective, rule.branch, rule.cards, null)
                : null;
        }

        private static string[] ToArray(JToken token, bool normalize)
        {
            if (!(token is JArray arr)) return System.Array.Empty<string>();
            var list = new List<string>(arr.Count);
            foreach (var t in arr)
            {
                var s = (string)t;
                if (string.IsNullOrEmpty(s)) continue;
                list.Add(normalize ? Normalize(s) : s);
            }
            return list.ToArray();
        }

        // KeywordDictionary와 동일한 정규화: 대문자화 + 공백/개행 제거 (한글은 대문자화 무영향, 공백만 제거)
        private static string Normalize(string s) =>
            s.ToUpperInvariant().Replace(" ", "").Replace("\n", "");
    }
}
