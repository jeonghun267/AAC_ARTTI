using Artti.Common;
using UnityEngine;

namespace Artti.Report
{
    // 완료 화면 '새 기록' 칸: 같은 모드·시나리오의 지난 완료 기록 중 최고 점수와 이번 점수를 비교한 결과
    public enum RecordState
    {
        First,   // 지난 완료 기록이 없다(첫 기록)
        Higher,  // 지난 최고보다 높다
        Tie,     // 지난 최고와 같다
        Lower    // 지난 최고보다 낮다
    }

    // 훈련 한 번의 결과(완료 화면 표시용). 숫자는 레포트와 같은 기록(JSONL)에서 계산해 두 화면이 어긋나지 않는다.
    public class TrainingResult
    {
        public TrainingMode mode;
        public string scenarioName;
        public int durationSeconds;

        public bool hasRecord;         // false = 기록이 없다(프로필 없이 바로 실행). 점수 칸들은 빈 값으로 보인다
        public int score;              // 0..100 = 레포트 단계 평가(바로 성공 1, 한 번 다시 0.5, 두 번 이상 0)의 평균
        public int stars;              // 1..5
        public int accuracyPct = -1;   // 음성 발화의 서버 명료도 평균(재시도 포함). 음성으로 말하지 않았으면 -1
        public int goodExpressions;    // 사용자가 전한 표현 중 명료도 기준을 넘었거나(측정 안 됨 포함) 카드·상품으로 전한 수
        public RecordState record;
        public int previousBest = -1;  // 지난 최고 점수. 없으면 -1
    }

    public static class TrainingResultCalculator
    {
        public static TrainingResult Build(ReportDataService service, string sessionId, int passClarityPct,
                                           TrainingMode mode, string scenarioName, int durationSeconds)
        {
            var result = new TrainingResult
            {
                mode = mode,
                scenarioName = scenarioName,
                durationSeconds = durationSeconds,
            };
            var report = service != null && !string.IsNullOrEmpty(sessionId) ? service.GetRecordReport(sessionId) : null;
            var detail = report != null ? service.GetDetail(sessionId) : null;
            if (report == null || detail == null || report.steps.Count == 0) return result;

            result.hasRecord = true;
            result.score = ScoreOf(report);
            result.stars = StarsFor(result.score);
            result.accuracyPct = report.avgClarityPct;
            foreach (var step in detail.steps)
                foreach (var turn in step.turns)
                    if (turn.isUser && (turn.clarityPct < 0 || turn.clarityPct >= passClarityPct))
                        result.goodExpressions++;

            foreach (var past in service.GetSessions(report.mode))
            {
                if (!past.completed || past.sessionId == sessionId || past.scenarioId != report.scenarioId) continue;
                var pastReport = service.GetRecordReport(past.sessionId);
                if (pastReport == null || pastReport.steps.Count == 0) continue;
                result.previousBest = Mathf.Max(result.previousBest, ScoreOf(pastReport));
            }
            result.record = Compare(result.score, result.previousBest);
            return result;
        }

        public static int ScoreOf(ReportRecordDetail report) => Mathf.RoundToInt(report.successRate * 100f);

        // 100~81 = 5개, 80~61 = 4개 ... 20 이하 = 1개. 끝까지 해냈으면 별은 적어도 1개
        public static int StarsFor(int score) => Mathf.Clamp(Mathf.CeilToInt(score / 20f), 1, 5);

        public static RecordState Compare(int score, int previousBest)
        {
            if (previousBest < 0) return RecordState.First;
            if (score > previousBest) return RecordState.Higher;
            return score == previousBest ? RecordState.Tie : RecordState.Lower;
        }
    }

    // 완료 화면 문구. 짧고 긍정적으로(발달장애 사용자: 점수가 낮아도 다음 목표로 안내한다)
    public static class TrainingResultLabels
    {
        public static string ModeName(TrainingMode mode) =>
            mode == TrainingMode.Conversation ? "대화 모드" : "정확도 모드";

        // 점수 판 아래 띠: "편의점 · 정확도 모드 · 3분 동안 연습했어요"
        public static string Summary(TrainingResult r)
        {
            int minutes = Mathf.RoundToInt(r.durationSeconds / 60f);
            string time = r.durationSeconds < 60 ? "1분 안에 마쳤어요" : $"{minutes}분 동안 연습했어요";
            return $"{r.scenarioName} · {ModeName(r.mode)} · {time}";
        }

        public static string AccuracyValue(TrainingResult r) =>
            r.hasRecord && r.accuracyPct >= 0 ? $"{r.accuracyPct}%" : "-";

        public static string AccuracyNote(TrainingResult r)
        {
            if (!r.hasRecord) return "기록을 남기면\n알려 드려요";
            if (r.accuracyPct < 0) return "목소리로 말하면\n알려 드려요";
            if (r.accuracyPct >= 90) return "정확한 표현이\n인상적이에요!";
            if (r.accuracyPct >= 70) return "또박또박\n잘 말했어요!";
            return "천천히 말하면\n더 잘 들려요!";
        }

        public static string ExpressionValue(TrainingResult r) =>
            r.hasRecord ? $"{r.goodExpressions}개" : "-";

        public static string ExpressionNote(TrainingResult r)
        {
            if (!r.hasRecord) return "기록을 남기면\n알려 드려요";
            return r.goodExpressions > 0 ? "자연스러운 표현을\n잘 사용했어요!" : "다음에는 한마디\n더 해 봐요!";
        }

        // '새 기록' 칸 아래 띠
        public static string RecordNote(TrainingResult r)
        {
            if (!r.hasRecord) return "기록을 남기면\n알려 드려요";
            switch (r.record)
            {
                case RecordState.First: return "첫 기록이에요!\n축하해요!";
                case RecordState.Higher: return $"이전 기록보다\n{r.score - r.previousBest}점 올랐어요!";
                case RecordState.Tie: return "최고 점수를\n또 받았어요!";
                default: return $"최고 기록까지\n{r.previousBest - r.score}점 남았어요!";
            }
        }

        // 최고 기록이 아닐 때(그림 속 '새 기록' '최고 점수!' 대신) 쓰는 제목과 큰 글자
        public const string LowerTitle = "내 기록";
        public static string LowerHeadline(TrainingResult r) => $"최고 {r.previousBest}점";

        public static bool IsBest(TrainingResult r) => r.hasRecord && r.record != RecordState.Lower;
    }
}
