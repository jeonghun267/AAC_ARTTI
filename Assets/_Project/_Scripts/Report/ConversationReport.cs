using System;
using System.Collections.Generic;
using System.Linq;
using Artti.Common;
using UnityEngine;

namespace Artti.Report
{
    // 대화 모드 지표(0~100, 기록이 없으면 -1). 레포트와 같은 기록(JSONL)에서 계산한다.
    public class ConversationMetrics
    {
        public int sessions;
        public int completed;
        public int talkSeconds;
        // 변경점(사용자 결정, 2026-10-02): 자연스러움·응답 적절성은 대화마다 Gemini 평가(ConversationEvaluation)가 있으면 그 값,
        //   없으면(평가 전·오프라인·실패) 아래 기존 계산 값을 쓰고, 대화들의 평균을 낸다
        public int naturalness = -1;     // 자연스러움: AI 평가, 없으면 음성 발화의 서버 명료도 평균
        public int appropriateness = -1; // 응답 적절성: AI 평가, 없으면 다시 말해 달라는 요청 없이 받아들여진 발화 비율
        public int aiSessions;           // AI 평가가 있는 대화 수
        public int understanding = -1;   // 질문 이해 = 단계 평가(바로 성공 1, 한 번 다시 0.5, 두 번 이상 0) 평균
        public int speed = -1;           // 응답 속도 = 점원 말 뒤 대답까지 걸린 시간 점수(ModeReportMath.SpeedScore)
        public int persistence = -1;     // 대화 지속력 = 대화를 끝까지 이어간 정도(완료 1, 아니면 지난 단계 수 / 5)
        public int expressiveness = -1;  // 표현력 = 카드·상품 대신 직접 말로 한 발화 비율

        // 레이더 순서(위에서 시계 방향): 이해, 자연스러움, 속도, 표현력, 지속력
        public int[] Radar => new[] { understanding, naturalness, speed, expressiveness, persistence };

        // 종합 = 레이더 다섯 값 중 있는 값의 평균
        public int Overall => ModeReportMath.Average(Radar);
    }

    public class ConversationLine
    {
        public bool isUser;
        public string text;
        public long timeMs;
        public bool spoken;     // 목소리로 한 말(아니면 카드·상품을 눌러서 한 말)
        public string heard;    // 목소리로 한 말의 음성 인식 원문(AI 평가에 넘긴다)
    }

    // 대화 한 번의 모든 말(오래된 것부터). '전체 보기' 창과 대화 기록 판이 쓴다
    public class ConversationTranscript
    {
        public long startMs;
        public List<ConversationLine> lines = new List<ConversationLine>();
    }

    public class ConversationReportData
    {
        public ReportPeriod period;
        public DateTime today;
        public ConversationMetrics current = new ConversationMetrics();
        public ConversationMetrics previous;                     // 비교 기간(어제 / 지난주). 전체 탭은 null
        public string latestSessionId;                           // 기간 안 가장 최근 대화 세션 (대화 기록, 전체 보기)
        public ConversationEvaluation latestEvaluation;          // 그 대화의 AI 평가(없으면 null). AI 코멘트에 쓴다
        public List<ConversationLine> lines = new List<ConversationLine>();   // 그 세션의 마지막 턴들(오래된 것부터)
        public int[] week = new int[7];                          // 이번 주 월~일 종합 점수. 기록 없는 날 -1
    }

    public static class ConversationReportBuilder
    {
        public const int ScenarioSteps = 5;    // 편의점 대화 단계 수 (인사 ~ 작별)
        public const int TimelineTurns = 10;   // 대화 기록 줄에 보이는 턴 수

        public static ConversationReportData Build(ReportDataService service, ReportPeriod period, DateTime now)
        {
            var today = now.Date;
            var data = new ConversationReportData { period = period, today = today };
            for (int i = 0; i < data.week.Length; i++) data.week[i] = -1;
            if (service == null) return data;

            var all = service.GetSessions(TrainingMode.Conversation);   // 최신순
            ModeReportMath.Range(period, today, out var from, out var to, out var prevFrom, out var prevTo, out bool hasPrev);
            var inPeriod = all.Where(s => ModeReportMath.InRange(s, from, to)).ToList();
            data.current = Measure(service, inPeriod);
            if (hasPrev)
                data.previous = Measure(service, all.Where(s => ModeReportMath.InRange(s, prevFrom, prevTo)).ToList());

            var weekStart = ModeReportMath.WeekStart(today);
            for (int d = 0; d < 7; d++)
            {
                var day = weekStart.AddDays(d);
                var daySessions = all.Where(s => ModeReportMath.InRange(s, day, day.AddDays(1))).ToList();
                data.week[d] = daySessions.Count > 0 ? Measure(service, daySessions).Overall : -1;
            }

            var latest = inPeriod.FirstOrDefault();
            if (latest != null)
            {
                data.latestSessionId = latest.sessionId;
                data.latestEvaluation = service.GetDetail(latest.sessionId)?.evaluation;
                var turns = Transcript(service, latest.sessionId).lines;
                data.lines.AddRange(turns.Skip(Math.Max(0, turns.Count - TimelineTurns)));
            }
            return data;
        }

        // 세션 하나의 모든 턴. 기록이 없으면 빈 대화
        public static ConversationTranscript Transcript(ReportDataService service, string sessionId)
        {
            var result = new ConversationTranscript();
            var detail = service != null && !string.IsNullOrEmpty(sessionId) ? service.GetDetail(sessionId) : null;
            if (detail == null) return result;
            result.startMs = detail.summary.startMs;
            foreach (var t in detail.steps.SelectMany(s => s.turns))
                result.lines.Add(new ConversationLine
                {
                    isUser = t.isUser, text = t.text, timeMs = t.timestampMs, spoken = t.spoken, heard = t.heardText,
                });
            return result;
        }

        public static ConversationMetrics Measure(ReportDataService service, IList<ReportSessionSummary> sessions)
        {
            var m = new ConversationMetrics();
            int userTurns = 0, spoken = 0, speedSum = 0, speedN = 0;
            float stepScore = 0f, persistSum = 0f, natSum = 0f, apprSum = 0f;
            int steps = 0, natN = 0, apprN = 0;
            foreach (var s in sessions)
            {
                var detail = service.GetDetail(s.sessionId);
                var report = service.GetRecordReport(s.sessionId);
                if (detail == null || report == null) continue;
                m.sessions++;
                if (s.completed) m.completed++;
                m.talkSeconds += ModeReportMath.SessionSeconds(s);

                stepScore += report.fullSuccess + 0.5f * report.partialSuccess;
                steps += report.steps.Count;
                int reached = detail.steps.Select(st => st.objectiveId).Where(id => !string.IsNullOrEmpty(id)).Distinct().Count();
                persistSum += s.completed ? 1f : Mathf.Clamp01(reached / (float)ScenarioSteps);

                long lastNpcMs = 0;
                int sClaritySum = 0, sClarityN = 0, sUser = 0, sAccepted = 0;
                foreach (var t in detail.steps.SelectMany(st => st.turns))
                {
                    if (!t.isUser) { lastNpcMs = t.timestampMs; continue; }
                    userTurns++;
                    sUser++;
                    if (!t.rejected) sAccepted++;
                    if (t.spoken) spoken++;
                    if (t.clarityPct >= 0) { sClaritySum += t.clarityPct; sClarityN++; }
                    if (lastNpcMs > 0 && t.timestampMs >= lastNpcMs)
                    {
                        speedSum += ModeReportMath.SpeedScore((t.timestampMs - lastNpcMs) / 1000.0);
                        speedN++;
                    }
                    lastNpcMs = 0;   // 대답 하나만 잰다(점원이 다시 말해야 다음을 잰다)
                }

                float nat = sClarityN > 0 ? sClaritySum / (float)sClarityN : -1f;
                float appr = sUser > 0 ? 100f * sAccepted / sUser : -1f;
                if (detail.evaluation != null && detail.evaluation.IsValid)
                {
                    nat = detail.evaluation.naturalness;
                    appr = detail.evaluation.appropriateness;
                    m.aiSessions++;
                }
                if (nat >= 0f) { natSum += nat; natN++; }
                if (appr >= 0f) { apprSum += appr; apprN++; }
            }
            if (m.sessions == 0) return m;
            if (natN > 0) m.naturalness = Mathf.RoundToInt(natSum / natN);
            if (apprN > 0) m.appropriateness = Mathf.RoundToInt(apprSum / apprN);
            if (userTurns > 0) m.expressiveness = Mathf.RoundToInt(100f * spoken / userTurns);
            if (steps > 0) m.understanding = Mathf.RoundToInt(100f * stepScore / steps);
            if (speedN > 0) m.speed = Mathf.RoundToInt(speedSum / (float)speedN);
            m.persistence = Mathf.RoundToInt(100f * persistSum / m.sessions);
            return m;
        }
    }

    // 대화 모드 리포트 문구. 짧고 긍정적으로(낮은 값은 다음에 해 볼 일로 안내한다)
    public static class ConversationReportLabels
    {
        public static int? TalkDelta(ConversationReportData d) =>
            d.previous == null ? null : ModeReportText.PercentDelta(d.current.talkSeconds, d.previous.talkSeconds);

        public static int? CompletedDelta(ConversationReportData d) =>
            d.previous == null || d.current.completed == d.previous.completed ? (int?)null : d.current.completed - d.previous.completed;

        public static string TalkNote(ConversationReportData d)
        {
            var c = d.current;
            if (c.sessions == 0) return "아직 대화 기록이 없어요";
            if (d.period == ReportPeriod.All) return $"지금까지 {c.sessions}번 대화했어요!";
            int diff = ModeReportText.Minutes(c.talkSeconds) - (d.previous != null ? ModeReportText.Minutes(d.previous.talkSeconds) : 0);
            if (diff > 0) return $"{ModeReportText.PeriodWord(d.period)}보다 {diff}분 더 대화했어요!";
            return d.period == ReportPeriod.Today ? "오늘도 대화를 연습했어요!" : "이번 주도 대화를 연습했어요!";
        }

        public static string NaturalnessNote(ConversationReportData d)
        {
            int v = d.current.naturalness;
            if (v < 0) return d.current.sessions == 0 ? "대화를 하면 알려 드려요" : "목소리로 말하면 알려 드려요";
            var delta = ModeReportText.Delta(v, d.previous?.naturalness ?? -1);
            if (delta > 0) return "더 자연스러운 대화를 보여줬어요!";
            return v >= 80 ? "자연스럽게 대화했어요!" : "또박또박 말하면 더 좋아져요!";
        }

        public static string AppropriatenessNote(ConversationReportData d)
        {
            int v = d.current.appropriateness;
            if (v < 0) return "대화를 하면 알려 드려요";
            return v >= 80 ? "상황에 맞는 답변을 잘했어요!" : "점원의 말을 듣고 답해 봐요!";
        }

        public static string CompletedNote(ConversationReportData d)
        {
            int n = d.current.completed;
            if (n >= 2) return "다양한 상황을 경험했어요!";
            return n == 1 ? "대화를 끝까지 마쳤어요!" : "대화를 끝까지 해 봐요!";
        }

        // AI 코멘트 3줄: 가장 높은 두 지표 칭찬 + 가장 낮은 지표 다음 목표
        static readonly string[] Praise =
        {
            "질문을 정확히 이해하고 적절하게 응답했어요.",
            "전반적으로 자연스러운 대화를 잘 이어갔어요.",
            "점원의 말에 빠르게 대답했어요.",
            "카드 대신 직접 말로 잘 표현했어요.",
            "대화를 끝까지 잘 이어갔어요.",
        };
        static readonly string[] NextGoal =
        {
            "점원의 질문을 끝까지 듣고 답해 보면 더 좋아요.",
            "천천히 또박또박 말하면 더 자연스러워져요.",
            "생각이 정리되면 조금 더 빨리 대답해 봐요.",
            "조금 더 다양한 표현을 사용하면 더 좋아질 수 있어요.",
            "대화를 끝까지 이어가 보면 더 좋아요.",
        };

        public static string[] Comments(ConversationMetrics m)
        {
            var radar = m.Radar;
            var have = Enumerable.Range(0, radar.Length).Where(i => radar[i] >= 0).ToList();
            if (have.Count == 0)
                return new[] { "아직 대화 기록이 없어요.", "대화 모드로 연습하면 여기에서 결과를 볼 수 있어요.", "오늘 첫 대화를 시작해 볼까요?" };
            var order = have.OrderByDescending(i => radar[i]).ToList();
            var lines = new List<string> { Praise[order[0]] };
            if (order.Count > 1) lines.Add(Praise[order[1]]);
            lines.Add(NextGoal[order[order.Count - 1]]);
            while (lines.Count < 3) lines.Add("오늘도 대화 연습을 해 줘서 고마워요.");
            return lines.ToArray();
        }

        // '전체 보기' 창 부제: "2026년 10월 2일 (금) 오후 3:12 · 말 12개"
        public static string TranscriptSubtitle(ConversationTranscript t)
        {
            if (t == null || t.lines.Count == 0) return "";
            var start = DateTimeOffset.FromUnixTimeMilliseconds(t.startMs).ToLocalTime().DateTime;
            return $"{ModeReportText.Date(start)} {ModeReportText.Time(t.startMs)} · 말 {t.lines.Count}개";
        }

        // AI 평가가 있으면: AI가 말한 잘한 점 -> 지표 칭찬 한 줄 -> AI가 말한 다음에 해 볼 점 (빈 칸은 규칙 문장으로 채운다)
        public static string[] Comments(ConversationMetrics m, ConversationEvaluation evaluation)
        {
            var rule = Comments(m);
            if (evaluation == null) return rule;
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(evaluation.praise)) lines.Add(evaluation.praise);
            lines.Add(rule[0]);
            if (lines.Count < 2) lines.Add(rule[1]);
            lines.Add(!string.IsNullOrWhiteSpace(evaluation.tip) ? evaluation.tip : rule[2]);
            return lines.ToArray();
        }

        // 이번 주 변화: 기록 있는 첫날과 마지막 날의 종합 점수 차이. 두 날 미만이면 null
        public static int? WeekDelta(int[] week)
        {
            var have = week.Where(v => v >= 0).ToList();
            if (have.Count < 2) return null;
            return have[have.Count - 1] - have[0];
        }
    }
}
