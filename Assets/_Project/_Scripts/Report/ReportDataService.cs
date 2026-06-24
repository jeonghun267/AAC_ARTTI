using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Artti.AAC.Logging;
using UnityEngine;

namespace Artti.Report
{
    public class ReportSessionSummary
    {
        public string sessionId;
        public string scenarioId;
        public bool completed;
        public long dateMs;       // SessionEnded ts, 없으면 마지막 이벤트 ts
        public long startMs;      // ScenarioEntered ts, 없으면 첫 이벤트 ts
        public int durationMin;   // startMs ~ dateMs
    }

    public class ReportOverview
    {
        public int totalAttempts;       // ScenarioEntered 수 (시나리오 구분 없이 합산)
        public int completedCount;      // SessionEnded(completed) 세션 수
        public Dictionary<string, int> completedByScenario = new Dictionary<string, int>();

        public float CompletionRate =>
            totalAttempts > 0 ? Mathf.Clamp01((float)completedCount / totalAttempts) : 0f;
    }

    // 요약 패널 4스탯
    public class ReportSummaryStats
    {
        public int completedCount;     // 완료 시나리오 수
        public int totalStudyMinutes;  // 총 학습 시간(분)
        public int streakDays;         // 연속 학습일
        public int level;              // 레벨
        public string levelTitle;      // 레벨 칭호 (예: AAC Explorer)
    }

    // 최근 학습 기록 한 줄
    public class ReportRecord
    {
        public string sessionId;
        public string scenarioId;
        public long dateMs;
        public int points;
    }

    public enum StepRating { Excellent, Good, Practice, NeedHelp }

    // 상세 리포트(hh.png)의 한 단계
    public class ReportStepReport
    {
        public int index;          // 1-based
        public string objectiveName;
        public string userText;    // 마지막 사용자 발화
        public string npcText;     // 마지막 NPC 발화 (TTS 재생용)
        public string time;        // HH:mm
        public int retryCount;
        public StepRating rating;
        public string ratingLabel; // "잘했어요😊" 등
    }

    // 세션 1개의 상세 리포트 (전체 학습기록 씬)
    public class ReportRecordDetail
    {
        public string sessionId;
        public string scenarioId;
        public string scenarioName;
        public bool completed;
        public long dateMs;
        public int durationMin;

        public int fullSuccess;    // 완전 성공
        public int partialSuccess; // 부분 성공
        public int needHelp;       // 도움 필요
        public float successRate;  // 0..1

        public string feedbackMain;
        public string feedbackBubble;
        public List<string> goodPoints = new List<string>();
        public List<string> improvePoints = new List<string>();
        public List<string> nextGoals = new List<string>();

        public List<ReportStepReport> steps = new List<ReportStepReport>();
    }

    public class ReportTurn
    {
        public bool isUser;
        public string text;
    }

    public class ReportStep
    {
        public string objectiveId;
        public int retryCount;
        public List<ReportTurn> turns = new List<ReportTurn>();
    }

    public class ReportSessionDetail
    {
        public ReportSessionSummary summary;
        public List<ReportStep> steps = new List<ReportStep>();
        public List<string> needsPractice = new List<string>(); // objectiveId
    }

    // JSONL 로그(JsonAppendLogStore 산출물)를 레포트 화면 데이터로 가공.
    // MonoBehaviour 아님 — View(ReportView)가 결과만 받아 표시.
    public class ReportDataService
    {
        readonly string _filePath;
        readonly List<AACEvent> _events = new List<AACEvent>();

        static readonly string EvScenarioEntered  = AACEventType.ScenarioEntered.ToString();
        static readonly string EvObjectiveEntered = AACEventType.ObjectiveEntered.ToString();
        static readonly string EvCardSelected     = AACEventType.CardSelected.ToString();
        static readonly string EvStepRetry        = AACEventType.StepRetryAttempt.ToString();
        static readonly string EvSessionAbandoned = AACEventType.SessionAbandoned.ToString();
        static readonly string EvTtsPlayed        = AACEventType.TtsPlayed.ToString();
        static readonly string EvSessionEnded     = AACEventType.SessionEnded.ToString();

        public ReportDataService(string filePath)
        {
            _filePath = filePath;
            Reload();
        }

        public void Reload()
        {
            _events.Clear();
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath)) return;
            try
            {
                foreach (var line in File.ReadAllLines(_filePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var ev = JsonUtility.FromJson<AACEvent>(line);
                        if (ev != null) _events.Add(ev);
                    }
                    catch { /* malformed line 무시 */ }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReportDataService] 로그 읽기 실패: {e.Message}");
            }
        }

        public ReportOverview GetOverview()
        {
            var overview = new ReportOverview();
            overview.totalAttempts = _events.Count(e => e.eventType == EvScenarioEntered);

            foreach (var group in EventsBySession())
            {
                var ended = group.Value.LastOrDefault(e => e.eventType == EvSessionEnded);
                if (ended == null || ended.payloadJson != "completed") continue;
                overview.completedCount++;
                var sid = group.Value[0].scenarioId ?? "";
                overview.completedByScenario.TryGetValue(sid, out var n);
                overview.completedByScenario[sid] = n + 1;
            }
            return overview;
        }

        // ===== 요약 스탯 / 추세 / 최근 기록 (요약 패널용) =====

        public ReportSummaryStats GetSummaryStats()
        {
            var sessions = GetSessions(); // 최신순
            var stats = new ReportSummaryStats
            {
                completedCount = sessions.Count(s => s.completed),
                totalStudyMinutes = sessions.Sum(s => s.durationMin),
                streakDays = ComputeStreak(sessions),
            };
            // 레벨 규칙(임시): 완료 3회마다 +1. 칭호는 구간별. (제품 규칙 확정 시 조정)
            stats.level = 1 + stats.completedCount / 3;
            stats.levelTitle = LevelTitle(stats.level);
            return stats;
        }

        static string LevelTitle(int level)
        {
            if (level >= 5) return "AAC Master";
            if (level >= 3) return "AAC Explorer";
            return "AAC Beginner";
        }

        // 가장 최근 학습일부터 거꾸로 연속된 날 수
        static int ComputeStreak(List<ReportSessionSummary> sessions)
        {
            if (sessions == null || sessions.Count == 0) return 0;
            var days = sessions
                .Select(s => DateTimeOffset.FromUnixTimeMilliseconds(s.dateMs).ToLocalTime().Date)
                .Distinct().OrderByDescending(d => d).ToList();
            int streak = 1;
            for (int i = 1; i < days.Count; i++)
            {
                if ((days[i - 1] - days[i]).Days == 1) streak++;
                else break;
            }
            return streak;
        }

        // 최근 days일을 일 단위로 버킷팅한 정규화 시리즈(0..1).
        float[] GetDailyTrend(int days, string eventType)
        {
            if (days < 2) days = 2;
            var counts = new int[days];
            var today = DateTimeOffset.Now.ToLocalTime().Date;
            foreach (var ev in _events)
            {
                if (ev.eventType != eventType) continue;
                var d = DateTimeOffset.FromUnixTimeMilliseconds(ev.timestampUnixMs).ToLocalTime().Date;
                int idx = days - 1 - (today - d).Days;
                if (idx >= 0 && idx < days) counts[idx]++;
            }
            return Normalize(counts);
        }

        public float[] GetSessionTrend(int days)    => GetDailyTrend(days, EvScenarioEntered);
        public float[] GetCompletedTrend(int days)  => GetDailyTrend(days, EvSessionEnded);
        public float[] GetAppearanceTrend(int days) => GetDailyTrend(days, EvCardSelected);

        static float[] Normalize(int[] counts)
        {
            var outv = new float[counts.Length];
            int max = 0;
            foreach (var c in counts) if (c > max) max = c;
            if (max <= 0) return outv; // 전부 0 -> 바닥선
            for (int i = 0; i < counts.Length; i++) outv[i] = (float)counts[i] / max;
            return outv;
        }

        public List<ReportRecord> GetRecentRecords(int n)
        {
            var list = new List<ReportRecord>();
            foreach (var group in EventsBySession())
            {
                var summary = BuildSummary(group.Key, group.Value);
                int objectives = group.Value.Count(e => e.eventType == EvObjectiveEntered);
                int retries = group.Value.Count(e => e.eventType == EvStepRetry);
                // 점수 규칙(임시): 완료 +30, 목표당 +10, 재시도당 -5, 0 이상. (제품 규칙 확정 시 조정)
                int points = Mathf.Max(0, (summary.completed ? 30 : 0) + objectives * 10 - retries * 5);
                list.Add(new ReportRecord
                {
                    sessionId = group.Key,
                    scenarioId = summary.scenarioId,
                    dateMs = summary.dateMs,
                    points = points
                });
            }
            return list.OrderByDescending(r => r.dateMs).Take(n).ToList();
        }

        // 세션 1개의 상세 리포트(hh.png). 피드백은 규칙 기반(추후 LLM 대체 가능).
        public ReportRecordDetail GetRecordReport(string sessionId)
        {
            var detail = GetDetail(sessionId);
            if (detail == null) return null;

            var rep = new ReportRecordDetail
            {
                sessionId = sessionId,
                scenarioId = detail.summary.scenarioId,
                scenarioName = ReportLabels.ScenarioName(detail.summary.scenarioId),
                completed = detail.summary.completed,
                dateMs = detail.summary.dateMs,
                durationMin = detail.summary.durationMin,
            };

            var events = SessionEvents(sessionId);
            var enteredTimes = events.Where(e => e.eventType == EvObjectiveEntered)
                                     .Select(e => e.timestampUnixMs).ToList();

            int idx = 1;
            foreach (var step in detail.steps)
            {
                var sr = new ReportStepReport
                {
                    index = idx,
                    objectiveName = ReportLabels.ObjectiveName(step.objectiveId),
                    retryCount = step.retryCount,
                };
                for (int t = step.turns.Count - 1; t >= 0; t--)
                {
                    if (sr.userText == null && step.turns[t].isUser) sr.userText = step.turns[t].text;
                    if (sr.npcText == null && !step.turns[t].isUser) sr.npcText = step.turns[t].text;
                }
                sr.time = (idx - 1 < enteredTimes.Count) ? FmtTime(enteredTimes[idx - 1]) : "";
                Classify(sr);
                rep.steps.Add(sr);
                idx++;
            }

            // 완료 세션의 마지막 단계는 0 재시도면 최고 등급
            if (rep.steps.Count > 0 && detail.summary.completed)
            {
                var last = rep.steps[rep.steps.Count - 1];
                if (last.retryCount == 0) { last.rating = StepRating.Excellent; last.ratingLabel = "아주 잘함🎉"; }
            }

            foreach (var s in rep.steps)
            {
                if (s.rating == StepRating.Excellent || s.rating == StepRating.Good) rep.fullSuccess++;
                else if (s.rating == StepRating.Practice) rep.partialSuccess++;
                else rep.needHelp++;
            }
            int totalSteps = Mathf.Max(1, rep.steps.Count);
            rep.successRate = Mathf.Clamp01((rep.fullSuccess + 0.5f * rep.partialSuccess) / totalSteps);

            // 피드백 (규칙 기반)
            rep.feedbackMain = detail.summary.completed
                ? "잘했어요! 꾸준히 연습하면 더 자연스러운 표현을 할 수 있어요."
                : "조금 더 연습이 필요해요. 다시 도전해볼까요?";
            rep.feedbackBubble = "다음 연습도 화이팅이에요! 💙";
            foreach (var s in rep.steps)
            {
                bool ok = s.rating == StepRating.Excellent || s.rating == StepRating.Good;
                if (ok && rep.goodPoints.Count < 3) rep.goodPoints.Add(s.objectiveName);
                else if (!ok && rep.improvePoints.Count < 3) rep.improvePoints.Add(s.objectiveName);
            }
            if (rep.goodPoints.Count == 0) rep.goodPoints.Add("끝까지 도전한 점");
            if (rep.improvePoints.Count == 0) rep.improvePoints.Add("지금처럼만 하면 충분해요");
            rep.nextGoals.Add("더 정확하고 자연스러운 표현으로 대화를 이어가요!");
            return rep;
        }

        static void Classify(ReportStepReport sr)
        {
            if (sr.retryCount == 0) { sr.rating = StepRating.Good; sr.ratingLabel = "잘했어요😊"; }
            else if (sr.retryCount == 1) { sr.rating = StepRating.Practice; sr.ratingLabel = "좋았어요👍"; }
            else { sr.rating = StepRating.NeedHelp; sr.ratingLabel = "연습해봐요"; }
        }

        static string FmtTime(long ms) =>
            DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("HH:mm");

        // 최신 세션이 앞에 오도록 정렬
        public List<ReportSessionSummary> GetSessions()
        {
            var list = new List<ReportSessionSummary>();
            foreach (var group in EventsBySession())
                list.Add(BuildSummary(group.Key, group.Value));
            return list.OrderByDescending(s => s.dateMs).ToList();
        }

        public ReportSessionDetail GetDetail(string sessionId)
        {
            var events = SessionEvents(sessionId);
            if (events.Count == 0) return null;

            var detail = new ReportSessionDetail { summary = BuildSummary(sessionId, events) };
            ReportStep current = null;
            string lastObjective = null;

            foreach (var ev in events)
            {
                if (ev.eventType == EvObjectiveEntered)
                {
                    current = new ReportStep { objectiveId = ev.objectiveId ?? "" };
                    detail.steps.Add(current);
                    lastObjective = ev.objectiveId;
                }
                else if (ev.eventType == EvTtsPlayed)
                {
                    AddTurn(detail, ref current, isUser: false, ParseNpcText(ev.payloadJson));
                }
                else if (ev.eventType == EvCardSelected)
                {
                    AddTurn(detail, ref current, isUser: true, ParseUserText(ev.payloadJson));
                }
                else if (ev.eventType == EvStepRetry)
                {
                    var step = detail.steps.LastOrDefault(s => s.objectiveId == (ev.objectiveId ?? "")) ?? current;
                    if (step != null) step.retryCount++;
                }
            }

            // 연습이 필요해요: 재시도 발생 목표 + (미완료 세션이면) 마지막 진입 목표
            foreach (var step in detail.steps)
                if (step.retryCount > 0 && !detail.needsPractice.Contains(step.objectiveId))
                    detail.needsPractice.Add(step.objectiveId);
            if (!detail.summary.completed && !string.IsNullOrEmpty(lastObjective)
                && !detail.needsPractice.Contains(lastObjective))
                detail.needsPractice.Add(lastObjective);

            return detail;
        }

        // 해당 세션의 라인을 제거하고 파일을 다시 씀. 성공 시 메모리도 갱신
        public bool DeleteSession(string sessionId)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath) || string.IsNullOrEmpty(sessionId))
                return false;
            try
            {
                var kept = new List<string>();
                foreach (var line in File.ReadAllLines(_filePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    AACEvent ev = null;
                    try { ev = JsonUtility.FromJson<AACEvent>(line); } catch { }
                    if (ev != null && ev.sessionId == sessionId) continue;
                    kept.Add(line);
                }
                File.WriteAllLines(_filePath, kept);
                Reload();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReportDataService] 세션 삭제 실패: {e.Message}");
                return false;
            }
        }

        // ===== 내부 =====

        IEnumerable<KeyValuePair<string, List<AACEvent>>> EventsBySession()
        {
            var map = new Dictionary<string, List<AACEvent>>();
            var order = new List<string>();
            foreach (var ev in _events)
            {
                if (string.IsNullOrEmpty(ev.sessionId)) continue;
                if (!map.TryGetValue(ev.sessionId, out var list))
                {
                    list = new List<AACEvent>();
                    map[ev.sessionId] = list;
                    order.Add(ev.sessionId);
                }
                list.Add(ev);
            }
            foreach (var id in order)
                yield return new KeyValuePair<string, List<AACEvent>>(id, map[id]);
        }

        List<AACEvent> SessionEvents(string sessionId)
        {
            return _events.Where(e => e.sessionId == sessionId).ToList();
        }

        ReportSessionSummary BuildSummary(string sessionId, List<AACEvent> events)
        {
            var entered = events.FirstOrDefault(e => e.eventType == EvScenarioEntered);
            var ended = events.LastOrDefault(e => e.eventType == EvSessionEnded);
            long startMs = entered?.timestampUnixMs ?? events[0].timestampUnixMs;
            long endMs = ended?.timestampUnixMs ?? events[events.Count - 1].timestampUnixMs;

            return new ReportSessionSummary
            {
                sessionId = sessionId,
                scenarioId = events[0].scenarioId ?? "",
                completed = ended != null && ended.payloadJson == "completed",
                startMs = startMs,
                dateMs = endMs,
                durationMin = Mathf.Max(0, Mathf.RoundToInt((endMs - startMs) / 60000f))
            };
        }

        static void AddTurn(ReportSessionDetail detail, ref ReportStep current, bool isUser, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (current == null)
            {
                current = new ReportStep { objectiveId = "" };
                detail.steps.Add(current);
            }
            current.turns.Add(new ReportTurn { isUser = isUser, text = text });
        }

        // TtsPlayed payload: "{tool}:{npcText}"
        static string ParseNpcText(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;
            int idx = payload.IndexOf(':');
            return idx >= 0 ? payload.Substring(idx + 1) : payload;
        }

        // CardSelected payload: "{cardId}:{text}" 또는 "{cardId}:{text}|stt:{sttText}"
        static string ParseUserText(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;
            int sttIdx = payload.IndexOf("|stt:", StringComparison.Ordinal);
            var head = sttIdx >= 0 ? payload.Substring(0, sttIdx) : payload;
            int idx = head.IndexOf(':');
            var cardText = idx >= 0 ? head.Substring(idx + 1) : head;
            if (!string.IsNullOrWhiteSpace(cardText)) return cardText;
            // 카드 문구가 비어있으면 STT 결과로 대체
            return sttIdx >= 0 ? payload.Substring(sttIdx + 5) : null;
        }
    }

    // scenarioId / objectiveId → 화면 표시용 한국어 라벨
    public static class ReportLabels
    {
        static readonly Dictionary<string, string> Scenario = new Dictionary<string, string>
        {
            { "pharmacy", "약국" }, { "convenience", "편의점" }, { "restaurant", "음식점" }
        };

        static readonly Dictionary<string, string> Objective = new Dictionary<string, string>
        {
            { "greeting",            "인사하기" },
            { "farewell",            "시나리오 완료" },
            { "payment",             "물품 계산하기" },
            // pharmacy
            { "identify_needs",      "증상 말하기" },
            { "serve_meds",          "필요한 물품 요구하기" },
            // convenience
            { "select_items",        "물건 고르기" },
            { "checkout",            "계산하기" },
            { "extras",              "추가 요청하기" },
            // restaurant
            { "menu_browse",         "메뉴 보기" },
            { "order",               "주문하기" },
            { "order_modifications", "추가 주문하기" }
        };

        public static string ScenarioName(string scenarioId) =>
            !string.IsNullOrEmpty(scenarioId) && Scenario.TryGetValue(scenarioId, out var n) ? n : (scenarioId ?? "");

        public static string ObjectiveName(string objectiveId) =>
            !string.IsNullOrEmpty(objectiveId) && Objective.TryGetValue(objectiveId, out var n) ? n : (objectiveId ?? "기타");
    }
}
