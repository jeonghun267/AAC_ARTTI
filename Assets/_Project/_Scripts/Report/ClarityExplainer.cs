using System.Collections.Generic;
using System.Text;

namespace Artti.Report
{
    // 서버 발화 정확도(명료도 %)가 왜 그렇게 나왔는지 상세기록에 적을 문장을 만든다.
    // 근거는 서버가 돌려준 값뿐이다: 목표 문장, 서버가 알아들은 문장, 명료도 %, 신호등.
    // 앱이 따로 점수를 계산하지 않는다 — 서버 결과를 사람이 읽을 수 있게 풀어 쓸 뿐이다.
    public static class ClarityExplainer
    {
        private const int MaxDiffsToList = 2;

        public static string Explain(string target, string heard, int pct, string light)
        {
            if (pct < 0) return null;

            var t = Normalize(target);
            var h = Normalize(heard);
            if (t.Length == 0 || h.Length == 0)
                return LightOnly(light) ?? "서버가 판정한 발화 정확도예요.";

            if (t == h)
            {
                switch (light)
                {
                    case "yellow": return "목표 문장 그대로 알아들었지만, 발음이 조금 흐리게 들렸어요.";
                    case "red":    return "목표 문장 그대로 알아들었지만, 발음이 많이 흐리게 들렸어요.";
                    default:       return "목표 문장 그대로 또렷하게 알아들었어요.";
                }
            }

            var diffs = Diff(t, h);
            if (diffs.Count > MaxDiffsToList || MismatchedChars(diffs) * 2 > t.Length)
                return $"목표 문장과 많이 다르게 들렸어요. (‘{heard.Trim()}’로 알아들었어요)";

            var sb = new StringBuilder();
            foreach (var d in diffs)
            {
                if (sb.Length > 0) sb.Append(' ');
                if (d.target.Length > 0 && d.heard.Length > 0)
                    sb.Append($"‘{d.target}’ 부분이 ‘{d.heard}’{Ro(d.heard)} 들렸어요.");
                else if (d.target.Length > 0)
                    sb.Append($"‘{d.target}’ 부분이 들리지 않았어요.");
                else
                    sb.Append($"‘{d.heard}’ 소리가 더 들렸어요.");
            }
            return sb.ToString();
        }

        private static string LightOnly(string light)
        {
            switch (light)
            {
                case "green":  return "서버 판정: 또렷함(초록).";
                case "yellow": return "서버 판정: 조금 흐림(노랑).";
                case "red":    return "서버 판정: 많이 흐림(빨강).";
                default:       return null;
            }
        }

        // 받침 유무로 조사 '로/으로' 선택 (ㄹ 받침은 '로')
        private static string Ro(string word)
        {
            char last = word[word.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return "로";
            int jong = (last - 0xAC00) % 28;
            return jong == 0 || jong == 8 ? "로" : "으로";
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
                if (!char.IsWhiteSpace(ch) && !char.IsPunctuation(ch) && !char.IsSymbol(ch)) sb.Append(ch);
            return sb.ToString();
        }

        private struct DiffChunk
        {
            public string target;   // 목표 문장 쪽 글자 (빠진/바뀐 부분)
            public string heard;    // 알아들은 문장 쪽 글자 (더 들린/바뀐 부분)
        }

        private static int MismatchedChars(List<DiffChunk> diffs)
        {
            int n = 0;
            foreach (var d in diffs) n += System.Math.Max(d.target.Length, d.heard.Length);
            return n;
        }

        // 글자(음절) 단위 정렬 후, 연속으로 어긋난 구간을 하나의 덩어리로 묶는다.
        private static List<DiffChunk> Diff(string t, string h)
        {
            int n = t.Length, m = h.Length;
            var dp = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) dp[i, 0] = i;
            for (int j = 0; j <= m; j++) dp[0, j] = j;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    int cost = t[i - 1] == h[j - 1] ? 0 : 1;
                    dp[i, j] = System.Math.Min(System.Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
                }

            // 뒤에서부터 되짚으며 어긋난 글자를 모은다
            var chunks = new List<DiffChunk>();
            var tBuf = new StringBuilder();
            var hBuf = new StringBuilder();
            int a = n, b = m;
            while (a > 0 || b > 0)
            {
                if (a > 0 && b > 0 && t[a - 1] == h[b - 1] && dp[a, b] == dp[a - 1, b - 1])
                {
                    Flush(chunks, tBuf, hBuf);
                    a--; b--;
                }
                else if (a > 0 && b > 0 && dp[a, b] == dp[a - 1, b - 1] + 1)
                {
                    tBuf.Insert(0, t[a - 1]); hBuf.Insert(0, h[b - 1]);
                    a--; b--;
                }
                else if (a > 0 && dp[a, b] == dp[a - 1, b] + 1)
                {
                    tBuf.Insert(0, t[a - 1]);
                    a--;
                }
                else
                {
                    hBuf.Insert(0, h[b - 1]);
                    b--;
                }
            }
            Flush(chunks, tBuf, hBuf);
            chunks.Reverse();
            return chunks;
        }

        private static void Flush(List<DiffChunk> chunks, StringBuilder tBuf, StringBuilder hBuf)
        {
            if (tBuf.Length == 0 && hBuf.Length == 0) return;
            chunks.Add(new DiffChunk { target = tBuf.ToString(), heard = hBuf.ToString() });
            tBuf.Clear();
            hBuf.Clear();
        }
    }
}
