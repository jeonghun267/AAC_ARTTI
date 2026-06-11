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
