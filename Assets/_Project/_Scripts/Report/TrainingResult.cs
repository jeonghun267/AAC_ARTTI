using System.Collections.Generic;
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
        public int score;              // 0..100 = 단계별 말하기 점수의 평균. 버튼으로 답한 턴은 0점
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
            result.score = ScoreOf(report, detail);
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
                result.previousBest = Mathf.Max(result.previousBest, ScoreOf(pastReport, service.GetDetail(past.sessionId)));
            }
            result.record = Compare(result.score, result.previousBest);
            return result;
        }

        // 변경점(사용자 요청 2026-10-03): 버튼만으로 끝낸 훈련은 0점. 음성·버튼을 섞으면 각 단계 안에서 턴 점수를 평균한다.
        //   음성 턴: 말하기 시도 40 + 발음 최대 40 + 진행 최대 15 + 응답 속도 최대 5.
        //   진행은 재시도 0/1/2회 이상에 따라 15/7.5/0. 거절된 턴은 진행·속도 가산점이 없다.
        //   발음·시간 측정이 없으면 해당 가산점은 0(측정값을 지어내거나 다른 항목에 재배분하지 않는다).
        //   버튼·상품·키보드 입력은 0점이지만 평균의 분모에 들어간다. 일상 대화는 시나리오 채점에서 제외한다.
        //   단계마다 같은 배점이라 한 단계의 반복 발화로 다른 단계 몫을 채울 수 없다. 편의점의 미진입 단계도 0점.
        private const float SpeakingPoints = 40f;
        private const float ClarityPoints = 40f;
        private const float ProgressPoints = 15f;
        private const float SpeedPoints = 5f;

        private sealed class StepScore
        {
            public int retries;
            public int answers;
            public float points;
        }

        public static int ScoreOf(ReportRecordDetail report, ReportSessionDetail detail)
        {
            if (report == null || detail == null || detail.steps.Count == 0) return 0;

            // 같은 단계에 다시 진입해도 배점은 늘지 않는다. 재시도 수는 모든 기록에서 먼저 모은다.
            var scores = new Dictionary<string, StepScore>();
            foreach (var step in detail.steps)
            {
                if (!scores.TryGetValue(step.objectiveId ?? "", out var score))
                    scores.Add(step.objectiveId ?? "", score = new StepScore());
                score.retries += Mathf.Max(0, step.retryCount);
            }

            long lastNpcMs = 0;
            // 기록 순서대로 읽어 단계가 바뀌기 직전에 나온 점원 질문도 다음 응답에 연결한다.
            foreach (var step in detail.steps)
            {
                var score = scores[step.objectiveId ?? ""];
                float progress = score.retries == 0 ? 1f : score.retries == 1 ? 0.5f : 0f;
                foreach (var turn in step.turns)
                {
                    if (!turn.isUser) { lastNpcMs = turn.timestampMs; continue; }
                    long promptMs = lastNpcMs;
                    lastNpcMs = 0;   // 버튼·거절·일상 대화도 점원 질문에 대한 첫 응답을 소비한다
                    if (turn.freeSpeech || string.IsNullOrWhiteSpace(turn.text)) continue;
                    score.answers++;
                    if (!turn.spoken || string.IsNullOrWhiteSpace(turn.heardText)) continue;

                    float points = SpeakingPoints;
                    if (turn.clarityPct >= 0)
                        points += ClarityPoints * Mathf.Clamp(turn.clarityPct, 0, 100) / 100f;
                    if (!turn.rejected)
                    {
                        points += ProgressPoints * progress;
                        if (promptMs > 0 && turn.timestampMs >= promptMs)
                            points += SpeedPoints * ModeReportMath.SpeedScore((turn.timestampMs - promptMs) / 1000.0) / 100f;
                    }
                    score.points += points;
                }
            }

            float total = 0f;
            foreach (var score in scores.Values)
                if (score.answers > 0) total += score.points / score.answers;
            int steps = scores.Count;
            if (report.scenarioId == Artti.AAC.ScenarioIds.Convenience)
                steps = Mathf.Max(steps, ConversationReportBuilder.ScenarioSteps);
            return Mathf.Clamp(Mathf.RoundToInt(total / steps), 0, 100);
        }

        // 변경점(사용자 요청 2026-10-03): 별 = 점수 구간. 90 이상 5개, 70 이상 4개, 50 이상 3개, 30 이상 2개, 그 아래 1개.
        //   (예전 올림 방식은 81점도 별 5개라 점수와 안 맞아 보였다.) 끝까지 해냈으면 별은 적어도 1개
        public static int StarsFor(int score) => score >= 90 ? 5 : score >= 70 ? 4 : score >= 50 ? 3 : score >= 30 ? 2 : 1;

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
        // 변경점: 화면 모드 이름은 TrainingModeContext 한 곳에서 정한다(구음진단모드 / 소통 연습모드)
        public static string ModeName(TrainingMode mode) => TrainingModeContext.DisplayName(mode);

        // 점수 판 아래 띠: "편의점 · 구음진단모드 · 3분 동안 연습했어요"
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
