using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Artti.Common;
using UnityEngine;

namespace Artti.Report
{
    // 어려웠던 발음 한 개(단어 기준). 목표 문장과 서버가 알아들은 문장을 음절로 맞춰 처음 어긋난 음절을 풀어 쓴다.
    public class DifficultSound
    {
        public string word;          // 사과
        public string syllable;      // 사
        public string romanization;  // sa-gwa
        public string tag;           // ㅅ 발음 약함
        public string description;   // '사'의 초성 ㅅ이 약하게 발음되었어요.
        public int count;
        public long lastMs;
        // 변경점: '이렇게 들렸어요 → 바르게는' 비교 듣기(사용자 결정, 2026-10-02). 가장 최근에 틀린 때의 문장
        public string target;        // 사과 주세요 (바른 문장)
        public string heard;         // 아과 주세요 (서버가 알아들은 문장)
        public int wordIndex = -1;   // word 안에서 틀린 글자 자리 (사과 -> 0)
        public string heardWord;     // 들린 문장에서 word에 해당하는 단어 (아과). 맞출 수 없으면 null
        public int heardWordIndex = -1;   // heardWord 안에서 다르게 들린 글자 자리. 소리가 빠졌으면 -1
    }

    // 정확도 모드 지표(0~100, 기록이 없으면 -1). 서버가 주는 값은 인식 문장·명료도·신호등뿐이라
    // 발화 속도·억양 안정성·띄어읽기는 가까운 값으로 대신한다(사용자 결정, 2026-10-02).
    public class AccuracyMetrics
    {
        public int sessions;
        public int completed;
        public int practiceSeconds;
        public int repeats;               // 반복 연습 = 다시 말하기 횟수
        public int pronunciation = -1;    // 발음 정확도 = 음성 발화 명료도 평균
        public int speed = -1;            // 발화 속도(대신) = 점원 말 뒤 대답까지 걸린 시간 점수
        public int stability = -1;        // 억양 안정성(대신) = 명료도가 들쭉날쭉하지 않은 정도(100 - 표준편차 x 2)
        public int clearness = -1;        // 또렷함 = 신호등 평균(초록 100, 노랑 65, 빨강 30)
        public int phrasing = -1;         // 띄어읽기(대신) = 알아들은 문장의 띄어쓰기가 목표 문장과 맞는 정도
        public List<DifficultSound> difficult = new List<DifficultSound>();   // 많이·최근 순

        // 레이더 순서(위에서 시계 방향): 발음 정확도, 발화 속도, 억양 안정성, 또렷함, 띄어읽기
        public int[] Radar => new[] { pronunciation, speed, stability, clearness, phrasing };
        public int Overall => ModeReportMath.Average(Radar);
    }

    public class AccuracyReportData
    {
        public ReportPeriod period;
        public DateTime today;
        public AccuracyMetrics current = new AccuracyMetrics();
        public AccuracyMetrics previous;                       // 어제 / 지난주. 전체 탭은 null
        // 모범 발음 듣기: 그 기간에 내가 말한 문장 전부(오래된 것부터, 같은 문장은 한 번). 정확도 모드 점원 목소리로 읽는다(사용자 결정, 2026-10-02)
        public List<string> modelSentences = new List<string>();
    }

    public static class AccuracyReportBuilder
    {
        public static AccuracyReportData Build(ReportDataService service, ReportPeriod period, DateTime now)
        {
            var today = now.Date;
            var data = new AccuracyReportData { period = period, today = today };
            if (service == null) return data;

            var all = service.GetSessions(TrainingMode.Accuracy);   // 최신순
            ModeReportMath.Range(period, today, out var from, out var to, out var prevFrom, out var prevTo, out bool hasPrev);
            var inPeriod = all.Where(s => ModeReportMath.InRange(s, from, to)).ToList();
            data.current = Measure(service, inPeriod);
            if (hasPrev)
                data.previous = Measure(service, all.Where(s => ModeReportMath.InRange(s, prevFrom, prevTo)).ToList());
            data.modelSentences = SpokenSentences(service, inPeriod);
            return data;
        }

        // 세션들(최신순)에서 내가 한 말을 오래된 것부터, 같은 문장은 한 번만(목표 문장이 기록돼 있으면 그 문장)
        public static List<string> SpokenSentences(ReportDataService service, IList<ReportSessionSummary> sessions)
        {
            var list = new List<string>();
            var seen = new HashSet<string>();
            foreach (var s in sessions.Reverse())
            {
                var detail = service.GetDetail(s.sessionId);
                if (detail == null) continue;
                foreach (var t in detail.steps.SelectMany(st => st.turns))
                {
                    if (!t.isUser || string.IsNullOrWhiteSpace(t.text)) continue;
                    // 변경점: 음성 발화의 text는 인식기가 알아들은 원문이라 중간에 끊기면("주세요"만) 모범 발음도 짧게 읽었다.
                    //   목표 문장(명료도 기준)이 있으면 그 문장을 바른 발음으로 읽는다
                    var text = (!string.IsNullOrWhiteSpace(t.clarityTarget) ? t.clarityTarget : t.text).Trim();
                    if (seen.Add(text)) list.Add(text);
                }
            }
            return list;
        }

        public static AccuracyMetrics Measure(ReportDataService service, IList<ReportSessionSummary> sessions)
        {
            var m = new AccuracyMetrics();
            var clarities = new List<int>();
            int lightSum = 0, lightN = 0, speedSum = 0, speedN = 0, phraseSum = 0, phraseN = 0;
            var byWord = new Dictionary<string, DifficultSound>();
            foreach (var s in sessions)
            {
                var detail = service.GetDetail(s.sessionId);
                if (detail == null) continue;
                m.sessions++;
                if (s.completed) m.completed++;
                m.practiceSeconds += ModeReportMath.SessionSeconds(s);
                m.repeats += detail.steps.Sum(st => st.retryCount);

                long lastNpcMs = 0;
                foreach (var t in detail.steps.SelectMany(st => st.turns))
                {
                    if (!t.isUser) { lastNpcMs = t.timestampMs; continue; }
                    if (lastNpcMs > 0 && t.timestampMs >= lastNpcMs)
                    {
                        speedSum += ModeReportMath.SpeedScore((t.timestampMs - lastNpcMs) / 1000.0);
                        speedN++;
                    }
                    lastNpcMs = 0;
                    if (t.clarityPct < 0) continue;

                    clarities.Add(t.clarityPct);
                    lightSum += LightScore(t.clarityLight, t.clarityPct);
                    lightN++;
                    int phrase = PhrasingScore(t.clarityTarget, t.clarityHeard);
                    if (phrase >= 0) { phraseSum += phrase; phraseN++; }

                    var sound = Analyze(t.clarityTarget, t.clarityHeard);
                    if (sound == null) continue;
                    // 같은 단어는 한 줄로 모으고(횟수), 보여 줄 내용은 가장 최근에 틀린 때의 것으로
                    byWord.TryGetValue(sound.word, out var known);
                    sound.count = (known?.count ?? 0) + 1;
                    sound.lastMs = t.timestampMs;
                    if (known == null || t.timestampMs >= known.lastMs) byWord[sound.word] = sound;
                    else known.count = sound.count;
                }
            }
            if (clarities.Count > 0) m.pronunciation = Mathf.RoundToInt((float)clarities.Average());
            if (clarities.Count >= 2)
            {
                double mean = clarities.Average();
                double sd = Math.Sqrt(clarities.Sum(c => (c - mean) * (c - mean)) / clarities.Count);
                m.stability = Mathf.Clamp((int)Math.Round(100 - 2 * sd), 0, 100);
            }
            if (lightN > 0) m.clearness = Mathf.RoundToInt(lightSum / (float)lightN);
            if (speedN > 0) m.speed = Mathf.RoundToInt(speedSum / (float)speedN);
            if (phraseN > 0) m.phrasing = Mathf.RoundToInt(phraseSum / (float)phraseN);
            m.difficult = byWord.Values.OrderByDescending(d => d.count).ThenByDescending(d => d.lastMs).ToList();
            return m;
        }

        public static int LightScore(string light, int pct)
        {
            switch (light)
            {
                case "green": return 100;
                case "yellow": return 65;
                case "red": return 30;
                default: return pct;
            }
        }

        // 띄어읽기(대신): 목표 문장이 두 단어 이상일 때만 잰다. 음절이 같으면 띄어 쓴 자리가 얼마나 같은지(F1),
        //   다르면 단어 수 차이로 잰다. 잴 수 없으면 -1
        public static int PhrasingScore(string target, string heard)
        {
            var tw = Words(target);
            var hw = Words(heard);
            if (tw.Count < 2 || hw.Count == 0) return -1;
            if (string.Concat(tw) == string.Concat(hw))
            {
                var tb = Boundaries(tw);
                var hb = Boundaries(hw);
                int both = tb.Intersect(hb).Count();
                if (tb.Count + hb.Count == 0) return 100;
                return Mathf.RoundToInt(100f * 2 * both / (tb.Count + hb.Count));
            }
            return Mathf.Clamp(Mathf.RoundToInt(100f * (1f - Mathf.Abs(tw.Count - hw.Count) / (float)tw.Count)), 0, 100);
        }

        static HashSet<int> Boundaries(List<string> words)
        {
            var set = new HashSet<int>();
            int pos = 0;
            for (int i = 0; i < words.Count - 1; i++)
            {
                pos += words[i].Length;
                set.Add(pos);
            }
            return set;
        }

        // 공백으로 나눈 단어(문장 부호 제거, 빈 단어 제외)
        public static List<string> Words(string s)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (var raw in s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                var sb = new StringBuilder();
                foreach (var ch in raw)
                    if (!char.IsPunctuation(ch) && !char.IsSymbol(ch)) sb.Append(ch);
                if (sb.Length > 0) list.Add(sb.ToString());
            }
            return list;
        }

        // 목표 문장과 알아들은 문장을 음절로 맞춰(편집 거리) 처음 어긋난 목표 음절을 풀어 쓴다. 같으면 null
        public static DifficultSound Analyze(string target, string heard)
        {
            var tw = Words(target);
            var hw = Words(heard);
            if (tw.Count == 0 || hw.Count == 0) return null;
            string t = string.Concat(tw), h = string.Concat(hw);
            if (t == h) return null;

            int n = t.Length, k = h.Length;
            var dp = new int[n + 1, k + 1];
            for (int i = 0; i <= n; i++) dp[i, 0] = i;
            for (int j = 0; j <= k; j++) dp[0, j] = j;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= k; j++)
                    dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + (t[i - 1] == h[j - 1] ? 0 : 1));

            // 뒤에서부터 따라가며 어긋난 목표 음절을 모으고, 가장 앞의 것을 쓴다(그때 들린 음절 자리도)
            int bi = n, bj = k, firstIdx = -1, heardIdx = -1;
            char heardChar = '\0';
            while (bi > 0 || bj > 0)
            {
                if (bi > 0 && bj > 0 && t[bi - 1] == h[bj - 1] && dp[bi, bj] == dp[bi - 1, bj - 1]) { bi--; bj--; continue; }
                if (bi > 0 && bj > 0 && dp[bi, bj] == dp[bi - 1, bj - 1] + 1)
                {
                    firstIdx = bi - 1; heardIdx = bj - 1; heardChar = h[bj - 1]; bi--; bj--; continue;
                }
                if (bi > 0 && dp[bi, bj] == dp[bi - 1, bj] + 1) { firstIdx = bi - 1; heardIdx = -1; heardChar = '\0'; bi--; continue; }
                bj--;   // 더 들린 소리는 목표 단어가 없어 건너뛴다
            }
            if (firstIdx < 0) return null;

            int wi = WordAt(tw, firstIdx, out int inWord);
            string word = tw[wi];
            string heardWord = null;
            int heardInWord = -1;
            if (heardIdx >= 0) heardWord = hw[WordAt(hw, heardIdx, out heardInWord)];
            else if (hw.Count == tw.Count) heardWord = hw[wi];   // 소리가 빠짐: 같은 자리 단어를 보여 준다
            char ts = t[firstIdx];
            Describe(ts, heardChar, out var tag, out var desc);
            return new DifficultSound
            {
                word = word, syllable = ts.ToString(), romanization = Hangul.Romanize(word), tag = tag, description = desc,
                target = target.Trim(), heard = heard.Trim(), wordIndex = inWord, heardWord = heardWord, heardWordIndex = heardInWord,
            };
        }

        // 공백 없이 이어 붙인 글자 자리 -> 몇 번째 단어, 그 단어 안 몇 번째 글자
        static int WordAt(List<string> words, int index, out int inWord)
        {
            int acc = 0;
            for (int i = 0; i < words.Count; i++)
            {
                if (index < acc + words[i].Length) { inWord = index - acc; return i; }
                acc += words[i].Length;
            }
            inWord = words[words.Count - 1].Length - 1;
            return words.Count - 1;
        }

        static void Describe(char target, char heard, out string tag, out string desc)
        {
            if (heard == '\0')
            {
                tag = "소리 빠짐";
                desc = $"'{target}' 소리가 들리지 않았어요.";
                return;
            }
            if (!Hangul.Decompose(target, out int tc, out int tv, out int tj) || !Hangul.Decompose(heard, out int hc, out int hv, out int hj))
            {
                tag = "발음 불명확";
                desc = $"'{target}' 소리가 다르게 들렸어요.";
                return;
            }
            if (tc != hc)
            {
                bool weak = Hangul.IsWeakened(tc, hc);
                string cho = Hangul.ChoJamo(tc);
                tag = $"{cho} 발음 {(weak ? "약함" : "불명확")}";
                desc = weak ? $"'{target}'의 초성 {cho}이 약하게 발음되었어요." : $"'{target}'의 {cho} 발음이 부정확했어요.";
            }
            else if (tv != hv)
            {
                string v = Hangul.JungJamo(tv);
                tag = $"{v} 발음 불명확";
                desc = $"'{target}'의 모음 {v} 발음이 부정확했어요.";
            }
            else
            {
                tag = "받침 처리 불안정";
                desc = tj != 0 ? $"'{target}'의 받침 {Hangul.JongJamo(tj)}이 약하게 들렸어요." : $"'{target}'에 받침 소리가 더 들렸어요.";
            }
        }
    }

    // 한글 음절 분해·자모·로마자(국어의 로마자 표기법 기본 대응 + 'ㄹㄹ' 하나만)
    public static class Hangul
    {
        const string Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        const string Jung = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
        const string Jong = " ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";
        static readonly string[] ChoRoman = { "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h" };
        static readonly string[] JungRoman =
            { "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i" };
        static readonly string[] JongRoman =
            { "", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m", "l", "l", "l", "p", "l", "m", "p", "p", "t", "t", "ng", "t", "t", "k", "t", "p", "t" };

        public static bool Decompose(char c, out int cho, out int jung, out int jong)
        {
            cho = jung = jong = 0;
            if (c < 0xAC00 || c > 0xD7A3) return false;
            int code = c - 0xAC00;
            cho = code / (21 * 28);
            jung = code % (21 * 28) / 28;
            jong = code % 28;
            return true;
        }

        public static string ChoJamo(int i) => Cho[i].ToString();
        public static string JungJamo(int i) => Jung[i].ToString();
        public static string JongJamo(int i) => Jong[i].ToString();

        // 거센소리·된소리가 예사소리로, 또는 첫소리가 빠지면(ㅇ) '약하게' 발음된 것으로 본다
        public static bool IsWeakened(int target, int heard)
        {
            if (heard == 11) return true;   // ㅇ
            switch (target)
            {
                case 14: return heard == 12;                // ㅊ -> ㅈ
                case 15: case 1: return heard == 0;         // ㅋ, ㄲ -> ㄱ
                case 16: case 4: return heard == 3;         // ㅌ, ㄸ -> ㄷ
                case 17: case 8: return heard == 7;         // ㅍ, ㅃ -> ㅂ
                case 10: return heard == 9;                 // ㅆ -> ㅅ
                case 13: return heard == 12;                // ㅉ -> ㅈ
                case 9: return heard == 3 || heard == 12;   // ㅅ -> ㄷ, ㅈ
                default: return false;
            }
        }

        public static string Romanize(string word)
        {
            var parts = new List<string>();
            int prevJong = 0;
            foreach (var ch in word)
            {
                if (!Decompose(ch, out int c, out int v, out int j)) { parts.Add(ch.ToString()); prevJong = 0; continue; }
                string initial = c == 5 && prevJong == 8 ? "l" : ChoRoman[c];   // ㄹ 받침 뒤 ㄹ은 l
                parts.Add(initial + JungRoman[v] + JongRoman[j]);
                prevJong = j;
            }
            return string.Join("-", parts);
        }
    }

    // 정확도 모드 리포트 문구
    public static class AccuracyReportLabels
    {
        public static string PracticeNote(AccuracyReportData d)
        {
            var c = d.current;
            if (c.sessions == 0) return "아직 연습 기록이 없어요";
            if (d.period == ReportPeriod.All) return $"지금까지 {c.sessions}번 연습했어요!";
            int diff = ModeReportText.Minutes(c.practiceSeconds) - (d.previous != null ? ModeReportText.Minutes(d.previous.practiceSeconds) : 0);
            if (diff > 0) return $"{ModeReportText.PeriodWord(d.period)}보다 {diff}분 더 연습했어요!";
            return d.period == ReportPeriod.Today ? "오늘도 발음을 연습했어요!" : "이번 주도 발음을 연습했어요!";
        }

        public static string PronunciationNote(AccuracyReportData d)
        {
            int v = d.current.pronunciation;
            if (v < 0) return d.current.sessions == 0 ? "연습을 하면 알려 드려요" : "목소리로 말하면 알려 드려요";
            if (v >= 90) return "매우 좋은 결과예요!";
            return v >= 75 ? "좋은 결과예요!" : "또박또박 말하면 더 좋아져요!";
        }

        public static string RepeatNote(AccuracyReportData d)
        {
            if (d.current.sessions == 0) return "연습을 하면 알려 드려요";
            return d.current.repeats > 0 ? "꾸준한 반복이 실력을 만듭니다." : "한 번에 잘 말했어요!";
        }

        public static string DifficultNote(AccuracyReportData d)
        {
            if (d.current.sessions == 0) return "연습을 하면 알려 드려요";
            int n = d.current.difficult.Count;
            var delta = d.previous != null ? ModeReportText.Delta(n, d.previous.difficult.Count) : null;
            if (delta < 0) return $"{ModeReportText.PeriodWord(d.period)}보다 {-delta}개 줄었어요!";
            return n == 0 ? "어려운 발음이 없어요!" : "아래에서 다시 들어 봐요!";
        }

        public static int? PracticeDelta(AccuracyReportData d) =>
            d.previous == null ? null : ModeReportText.PercentDelta(d.current.practiceSeconds, d.previous.practiceSeconds);

        public static int? CountDelta(int current, AccuracyMetrics previous, Func<AccuracyMetrics, int> pick) =>
            previous == null ? null : ModeReportText.Delta(current, pick(previous));

        // 모범 발음 듣기 길이 추정(음절 0.3초 + 문장 사이 0.6초) -> "0:08"
        public static string ModelLength(IList<string> sentences)
        {
            if (sentences == null || sentences.Count == 0) return "0:00";
            float sec = sentences.Sum(Syllables) * 0.3f + sentences.Count * 0.6f;
            int s = Mathf.Max(1, Mathf.RoundToInt(sec));
            return $"{s / 60}:{s % 60:00}";
        }

        // 공백·문장 부호를 뺀 글자 수(파형 위 문장 칸 폭과 진행에 쓴다)
        public static int Syllables(string s) =>
            string.IsNullOrEmpty(s) ? 0 : s.Count(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c));

        // 모범 발음 듣기 안내: 쉬는 중 / 읽는 중
        public static string ModelIdle(int count) =>
            count == 0 ? "연습한 문장이 생기면 모범 발음으로 들려 드려요" : $"내가 말한 문장 {count}개를 모범 발음으로 들어 봐요";

        public static string ModelPlaying(int index, int count, string sentence) => $"{index + 1} / {count}   {sentence}";

        // ===== 비교 듣기: '이렇게 들렸어요 → 바르게는' (줄 ▶) =====
        const string HeardColor = "FF7FA8", RightColor = "6FE8FF";

        // 단어의 index번째 글자만 색을 입힌다(TMP 리치 텍스트). index가 없으면 그대로
        public static string Mark(string word, int index, string hex)
        {
            if (string.IsNullOrEmpty(word) || index < 0 || index >= word.Length) return word ?? "";
            return $"{word.Substring(0, index)}<color=#{hex}>{word[index]}</color>{word.Substring(index + 1)}";
        }

        public static string HeardLine(DifficultSound d) =>
            d.heardWord == null ? "이렇게 들렸어요  →  (소리가 들리지 않았어요)" : $"이렇게 들렸어요  →  {Mark(d.heardWord, d.heardWordIndex, HeardColor)}";

        public static string RightLine(DifficultSound d) => $"바르게는  →  {Mark(d.word, d.wordIndex, RightColor)}";

        public static string HeardSpeech(DifficultSound d) => $"이렇게 들렸어요. {d.heard}";

        public static string RightSpeech(DifficultSound d) => $"바르게 말하면, {(string.IsNullOrWhiteSpace(d.target) ? d.word : d.target)}";
    }
}
