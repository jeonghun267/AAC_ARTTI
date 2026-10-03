using UnityEngine;

namespace Artti.Common.Speech
{
    // 명료도(또렷함 %)는 STT 서버만 준다. 서버에 못 붙으면(같은 와이파이가 아님, 서버 PC 꺼짐) 기기 STT로 넘어가
    // 정확도 리포트의 발음 정확도·억양 안정성·또렷함·띄어읽기·어려웠던 발음·오각형이 모두 비었다(2026-10-02).
    // 서버 값이 없을 때 리포트용으로 목표 문장과 알아들은 문장의 음절 일치도(편집 거리)로 대신 잰다(사용자 결정 '가까운 값으로 대신').
    // 다시 말하기 관문에는 쓰지 않는다(기기 STT로는 막지 않던 기존 동작 유지). MonoBehaviour 아님 - 순수 계산.
    public static class ClarityEstimator
    {
        public const int GreenFrom = 85;
        public const int YellowFrom = 60;   // ClarityGatePolicy 기본 기준(60%)과 같게

        // 서버 명료도가 없고 잴 수 있으면 추정치(퍼센트·신호등·목표 문장)를 채운 복사본을, 아니면 그대로 돌려준다
        public static SttResult FillIfMissing(SttResult result, string target)
        {
            if (result.hasClarity || !TryEstimate(target, result.text, out int percent)) return result;
            result.hasClarity = true;
            result.clarityPercent = percent;
            result.clarityLight = percent >= GreenFrom ? "green" : percent >= YellowFrom ? "yellow" : "red";
            result.clarityTarget = target;
            return result;
        }

        // 띄어쓰기·문장 부호를 빼고 음절끼리 비교: 100 x (1 - 편집 거리 / 긴 쪽 길이). 한쪽이 비면 false
        public static bool TryEstimate(string target, string heard, out int percent)
        {
            percent = -1;
            var t = Letters(target);
            var h = Letters(heard);
            if (t.Length == 0 || h.Length == 0) return false;
            int distance = Distance(t, h);
            percent = Mathf.Clamp(Mathf.RoundToInt(100f * (1f - distance / (float)Mathf.Max(t.Length, h.Length))), 0, 100);
            return true;
        }

        static string Letters(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var chars = new char[s.Length];
            int n = 0;
            foreach (var c in s)
                if (char.IsLetterOrDigit(c)) chars[n++] = c;
            return new string(chars, 0, n);
        }

        static int Distance(string a, string b)
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
                    cur[j] = Mathf.Min(Mathf.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                }
                var swap = prev; prev = cur; cur = swap;
            }
            return prev[b.Length];
        }
    }
}
