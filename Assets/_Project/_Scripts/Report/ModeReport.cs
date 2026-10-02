using System;
using Artti.Common;
using UnityEngine;

namespace Artti.Report
{
    // 모드별 리포트(정확도 / 대화) 기간 탭
    public enum ReportPeriod { Today, Week, All }

    // 두 리포트가 같이 쓰는 기간·점수 계산
    public static class ModeReportMath
    {
        // 기간과 비교 기간(오늘 -> 어제, 이번 주 -> 지난주). 전체 탭은 비교 기간이 없다(hasPrevious = false)
        public static void Range(ReportPeriod period, DateTime today, out DateTime from, out DateTime to,
                                 out DateTime prevFrom, out DateTime prevTo, out bool hasPrevious)
        {
            var weekStart = WeekStart(today);
            switch (period)
            {
                case ReportPeriod.Today:
                    from = today; to = today.AddDays(1); prevFrom = today.AddDays(-1); prevTo = today; hasPrevious = true;
                    break;
                case ReportPeriod.Week:
                    from = weekStart; to = weekStart.AddDays(7); prevFrom = weekStart.AddDays(-7); prevTo = weekStart; hasPrevious = true;
                    break;
                default:
                    from = DateTime.MinValue; to = DateTime.MaxValue; prevFrom = prevTo = DateTime.MinValue; hasPrevious = false;
                    break;
            }
        }

        public static bool InRange(ReportSessionSummary s, DateTime from, DateTime to)
        {
            var d = DateTimeOffset.FromUnixTimeMilliseconds(s.dateMs).ToLocalTime().DateTime;
            return d >= from && d < to;
        }

        // 월요일 시작
        public static DateTime WeekStart(DateTime day)
        {
            int sinceMonday = ((int)day.DayOfWeek + 6) % 7;
            return day.Date.AddDays(-sinceMonday);
        }

        // 점원 말이 시작된 뒤 대답까지(점원 음성 재생 시간 포함) 6초 이하면 100, 24초 이상이면 40, 사이는 직선
        public static int SpeedScore(double seconds)
        {
            const double fast = 6, slow = 24;
            double t = Math.Max(0, Math.Min(1, (seconds - fast) / (slow - fast)));
            return (int)Math.Round(100 - 60 * t);
        }

        // 있는 값(0 이상)의 평균, 없으면 -1
        public static int Average(params int[] values)
        {
            int sum = 0, n = 0;
            foreach (var v in values)
                if (v >= 0) { sum += v; n++; }
            return n > 0 ? Mathf.RoundToInt(sum / (float)n) : -1;
        }

        public static int SessionSeconds(ReportSessionSummary s) => (int)Math.Max(0, (s.dateMs - s.startMs) / 1000);
    }

    // 두 리포트가 같이 쓰는 표시 문구
    public static class ModeReportText
    {
        static readonly string[] Weekdays = { "일", "월", "화", "수", "목", "금", "토" };

        public static string Date(DateTime d) => $"{d.Year}년 {d.Month}월 {d.Day}일 ({Weekdays[(int)d.DayOfWeek]})";

        public static string Time(long ms)
        {
            if (ms <= 0) return "";
            var t = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().DateTime;
            int h = t.Hour % 12 == 0 ? 12 : t.Hour % 12;
            return $"{(t.Hour < 12 ? "오전" : "오후")} {h}:{t.Minute:00}";
        }

        public static int Minutes(int seconds) => seconds <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(seconds / 60f));

        // 큰 숫자 + 작은 단위 (TMP 리치 텍스트)
        public static string Value(int value, string unit) => value < 0 ? "-" : $"{value}<size=62%> {unit}</size>";

        public static string Grade(int overall)
        {
            if (overall < 0) return "-";
            if (overall >= 80) return "우수";
            return overall >= 60 ? "좋아요" : "성장 중";
        }

        // 시간 변화(%): 비교할 수 없거나 0이면 null(칩을 숨긴다)
        public static int? PercentDelta(int current, int previous)
        {
            if (previous <= 0 || current <= 0) return null;
            int pct = Mathf.RoundToInt(100f * (current - previous) / previous);
            return pct == 0 ? (int?)null : pct;
        }

        // 점수·개수 변화: 둘 중 하나가 없거나 같으면 null
        public static int? Delta(int current, int previous)
        {
            if (current < 0 || previous < 0 || current == previous) return null;
            return current - previous;
        }

        public static string PeriodWord(ReportPeriod period) => period == ReportPeriod.Today ? "어제" : "지난주";

        public static string ModeName(TrainingMode mode) => TrainingModeContext.DisplayName(mode);
    }
}
