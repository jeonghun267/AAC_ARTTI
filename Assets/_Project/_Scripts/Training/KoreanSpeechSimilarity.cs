using System.Collections.Generic;
using System.Text;

namespace Artti.Training
{
    // 대화 모드에서 어눌한 발음을 알아듣기 위한 한국어 발음 거리(0 = 같음 ~ 1 = 전혀 다름).
    //   글자(음절)를 초성·중성·종성으로 나눠 비교한다. 글자 단위로는 "가자"와 "과자"가 완전히 다른 글자지만,
    //   자모로 보면 모음 하나(ㅏ/ㅘ)만 다르다.
    //   발달장애인 발화에서 흔히 헷갈리는 소리는 반만 틀린 것으로 센다(2026-10-02, 사용자: "어눌하게 말해도 이해 가능"이 제일 중요).
    //     - 같은 자리 자음: ㄱ/ㄲ/ㅋ, ㄷ/ㄸ/ㅌ, ㅂ/ㅃ/ㅍ, ㅈ/ㅉ/ㅊ, ㅅ/ㅆ, ㄴ/ㄹ
    //     - 첫소리가 빠짐(ㅇ으로 들림): "사과" -> "아과"
    //     - 받침이 빠지거나 덧붙음, 받침 ㄴ/ㅇ/ㅁ
    //     - 비슷한 모음: ㅐ/ㅔ, ㅒ/ㅖ, ㅏ/ㅘ/ㅑ, ㅓ/ㅝ/ㅕ, ㅗ/ㅛ/ㅓ, ㅜ/ㅠ/ㅡ, ㅕ/ㅛ, ㅣ/ㅟ/ㅢ, ㅚ/ㅙ/ㅞ
    //   띄어쓰기·문장 부호는 보지 않는다. MonoBehaviour 아님 - 순수 계산.
    public static class KoreanSpeechSimilarity
    {
        const int Base = 0xAC00, Last = 0xD7A3;
        const char Initial = 'I', Vowel = 'V', Final = 'F', Other = 'O';
        const float Half = 0.5f;

        // 초성 번호: ㄱ0 ㄲ1 ㄴ2 ㄷ3 ㄸ4 ㄹ5 ㅁ6 ㅂ7 ㅃ8 ㅅ9 ㅆ10 ㅇ11 ㅈ12 ㅉ13 ㅊ14 ㅋ15 ㅌ16 ㅍ17 ㅎ18
        static readonly int[][] InitialGroups =
        {
            new[] { 0, 1, 15 }, new[] { 3, 4, 16 }, new[] { 7, 8, 17 }, new[] { 12, 13, 14 }, new[] { 9, 10 }, new[] { 2, 5 },
        };
        const int SilentInitial = 11;

        // 중성 번호: ㅏ0 ㅐ1 ㅑ2 ㅒ3 ㅓ4 ㅔ5 ㅕ6 ㅖ7 ㅗ8 ㅘ9 ㅙ10 ㅚ11 ㅛ12 ㅜ13 ㅝ14 ㅞ15 ㅟ16 ㅠ17 ㅡ18 ㅢ19 ㅣ20
        static readonly int[][] VowelGroups =
        {
            new[] { 1, 5 }, new[] { 3, 7 }, new[] { 0, 9, 2 }, new[] { 4, 14, 6 }, new[] { 8, 12, 4 },
            new[] { 13, 17, 18 }, new[] { 6, 12 }, new[] { 20, 16, 19 }, new[] { 11, 10, 15 },
        };

        // 종성 번호(0 = 없음): ㄱ1 ㄲ2 ㄳ3 ㄴ4 ㄵ5 ㄶ6 ㄷ7 ㄹ8 ㄺ9 ㄻ10 ㄼ11 ㄽ12 ㄾ13 ㄿ14 ㅀ15 ㅁ16 ㅂ17 ㅄ18 ㅅ19 ㅆ20 ㅇ21 ㅈ22 ㅊ23 ㅋ24 ㅌ25 ㅍ26 ㅎ27
        static readonly int[][] FinalGroups =
        {
            new[] { 1, 2, 3, 24, 9 }, new[] { 7, 19, 20, 22, 23, 25, 27 }, new[] { 17, 18, 26, 11, 14 }, new[] { 4, 21, 16 }, new[] { 8, 10, 12, 13, 15 },
        };

        struct Token
        {
            public char role;
            public int code;
        }

        // 0~1. 두 문장의 자모 편집 거리(헷갈리는 소리는 0.5)를 긴 쪽 자모 수로 나눈 값. 한쪽이라도 비면 1
        public static float Distance(string a, string b)
        {
            var ta = Tokens(a);
            var tb = Tokens(b);
            if (ta.Count == 0 || tb.Count == 0) return 1f;
            return WeightedEditDistance(ta, tb) / System.Math.Max(ta.Count, tb.Count);
        }

        // 띄어쓰기·문장 부호를 뺀 비교용 문자열
        public static string Compact(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
                if (!char.IsWhiteSpace(ch) && !char.IsPunctuation(ch) && !char.IsSymbol(ch)) sb.Append(ch);
            return sb.ToString();
        }

        static List<Token> Tokens(string s)
        {
            var list = new List<Token>();
            foreach (var ch in Compact(s))
            {
                if (ch >= Base && ch <= Last)
                {
                    int idx = ch - Base;
                    list.Add(new Token { role = Initial, code = idx / 588 });
                    list.Add(new Token { role = Vowel, code = idx % 588 / 28 });
                    int final = idx % 28;
                    if (final > 0) list.Add(new Token { role = Final, code = final });
                }
                else list.Add(new Token { role = Other, code = char.ToLowerInvariant(ch) });
            }
            return list;
        }

        static float WeightedEditDistance(List<Token> a, List<Token> b)
        {
            var prev = new float[b.Count + 1];
            var cur = new float[b.Count + 1];
            for (int j = 1; j <= b.Count; j++) prev[j] = prev[j - 1] + GapCost(b[j - 1]);
            for (int i = 1; i <= a.Count; i++)
            {
                cur[0] = prev[0] + GapCost(a[i - 1]);
                for (int j = 1; j <= b.Count; j++)
                {
                    float sub = prev[j - 1] + SubstituteCost(a[i - 1], b[j - 1]);
                    float del = prev[j] + GapCost(a[i - 1]);
                    float ins = cur[j - 1] + GapCost(b[j - 1]);
                    cur[j] = System.Math.Min(sub, System.Math.Min(del, ins));
                }
                var swap = prev; prev = cur; cur = swap;
            }
            return prev[b.Count];
        }

        // 받침이 빠지거나 덧붙는 것은 반만 틀린 것
        static float GapCost(Token t) => t.role == Final ? Half : 1f;

        static float SubstituteCost(Token x, Token y)
        {
            if (x.role == y.role && x.code == y.code) return 0f;
            if (x.role != y.role) return 1f;
            switch (x.role)
            {
                case Initial:
                    if (x.code == SilentInitial || y.code == SilentInitial) return Half;   // 첫소리가 빠짐
                    return SameGroup(InitialGroups, x.code, y.code) ? Half : 1f;
                case Vowel: return SameGroup(VowelGroups, x.code, y.code) ? Half : 1f;
                case Final: return SameGroup(FinalGroups, x.code, y.code) ? Half : 1f;
                default: return 1f;
            }
        }

        static bool SameGroup(int[][] groups, int x, int y)
        {
            foreach (var g in groups)
            {
                bool hasX = false, hasY = false;
                foreach (var v in g)
                {
                    if (v == x) hasX = true;
                    if (v == y) hasY = true;
                }
                if (hasX && hasY) return true;
            }
            return false;
        }
    }
}
