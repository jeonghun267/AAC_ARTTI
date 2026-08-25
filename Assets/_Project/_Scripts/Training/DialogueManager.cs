using System;
using System.Collections.Generic;
using System.Linq;
using Artti.AAC;
using UnityEngine;

namespace Artti.Training
{
    // 훈련 세션의 대화 상태 머신. objective 순서·슬롯·서브플로·카운터의 단일 진실 원본.
    //
    // 하이브리드 진행 제어: Gemini가 objective_id를 "제안"하면 여기서 검증한 뒤에만 반영한다.
    //   - 존재하지 않는 단계(환각) → 거부하고 순서상 다음 단계로 폴백
    //   - 이미 지나온 단계(역행)   → 거부하고 현재 유지
    // 이렇게 해야 LLM이 헛짚어도 시나리오가 무한 루프에 빠지거나 단계가 튀지 않는다.
    public class DialogueManager
    {
        public string CurrentObjectiveId { get; private set; }

        // 변경점: 카운터를 둘로 분리.
        //   TurnCount    — 사용자 발화마다 +1, 리셋 없음. turnLimit(강제 종료) 판정용.
        //   AttemptCount — "실패한" 턴만 +1, objective 변경 시 0. scaffold(힌트 강도) 판정용.
        // 이전에는 카드 탭마다 무조건 AttemptCount++라, 페르소나가 권장하는 자연스러운 여러 턴 대화를
        // 하면 3턴 만에 scaffold_level=2(정답 카드 지목)가 되어 잘 하고 있는 사용자에게 힌트가 쏟아졌다.
        public int TurnCount { get; private set; }
        public int AttemptCount { get; private set; }
        public ScaffoldLevel CurrentScaffoldLevel { get; private set; }

        public string ActiveSubflowId { get; private set; }
        public string PendingTopic { get; private set; }

        private readonly Dictionary<string, string> _slots = new Dictionary<string, string>();
        // 서브플로 진입 시 복귀할 objective를 쌓아둔다 (return_from_subflow에서 pop).
        private readonly List<string> _pendingQueue = new List<string>();
        private readonly ScaffoldedRetryPolicy _retryPolicy = new ScaffoldedRetryPolicy();

        // 시나리오 objective 순서와 "그 단계에 카드가 있는가" 판정자. Initialize에서 주입.
        private string[] _objectiveOrder = Array.Empty<string>();
        private Func<string, bool> _hasCards;
        private int _turnLimit = 12;

        // Unit 4: 다중 턴 맥락용 대화 이력 (Gemini에 최근 N턴 전달). "User:"/"Clerk:" 줄로 누적.
        private readonly List<string> _history = new List<string>();
        private const int HistoryLinesKept = 6; // 약 3턴(사용자+점원)

        public event Action<string> OnObjectiveChanged;
        public event Action<DialogueTool, string, string[]> OnToolCallApplied;

        // objectiveOrder / hasCards는 scenarios.json 순서와 AACDatabase 조회를 호출부가 주입한다.
        // (CLAUDE.md: MonoBehaviour는 view·입력만 — 진행 판정 로직을 여기로 옮긴 이유)
        public void Initialize(string initialObjectiveId, string[] objectiveOrder = null,
                               Func<string, bool> hasCards = null, int turnLimit = 12)
        {
            CurrentObjectiveId = initialObjectiveId;
            _objectiveOrder = objectiveOrder ?? Array.Empty<string>();
            _hasCards = hasCards;
            _turnLimit = turnLimit > 0 ? turnLimit : 12;

            TurnCount = 0;
            AttemptCount = 0;
            CurrentScaffoldLevel = ScaffoldLevel.None;
            ActiveSubflowId = null;
            PendingTopic = null;
            _slots.Clear();
            _pendingQueue.Clear();
            _history.Clear();
        }

        // 사용자 발화 1회. 성공/실패 판정은 LLM 응답을 본 뒤 RegisterSuccess/RegisterFailure로 따로 한다.
        public void HandleUserTurn(AACCard selectedCard, string sttText)
        {
            TurnCount++;
        }

        // 이번 턴이 잘 풀렸다 — 힌트 강도를 초기화한다.
        public void RegisterSuccess()
        {
            AttemptCount = 0;
            CurrentScaffoldLevel = ScaffoldLevel.None;
        }

        // 이번 턴이 막혔다(STT 빈 결과 / LLM 호출 실패 / request_clarification) — 힌트 강도를 올린다.
        public void RegisterFailure()
        {
            AttemptCount++;
            CurrentScaffoldLevel = _retryPolicy.GetScaffoldLevel(AttemptCount);
        }

        // LLM이 스스로 판단한 scaffold_level을 존중하되, 앱 정책값을 하한으로 보장한다.
        public void ApplyScaffoldHint(int? llmScaffoldLevel)
        {
            if (!llmScaffoldLevel.HasValue) return;
            var suggested = (ScaffoldLevel)Mathf.Clamp(llmScaffoldLevel.Value, 0, (int)ScaffoldLevel.StrongHint);
            if (suggested > CurrentScaffoldLevel) CurrentScaffoldLevel = suggested;
        }

        // Unit 4: 한 턴(사용자 발화 + 점원 응답)을 이력에 기록. 최근 N줄만 유지.
        public void RecordTurn(string userText, string clerkText)
        {
            if (!string.IsNullOrWhiteSpace(userText)) _history.Add($"User: {userText.Trim()}");
            if (!string.IsNullOrWhiteSpace(clerkText)) _history.Add($"Clerk: {clerkText.Trim()}");
            while (_history.Count > HistoryLinesKept) _history.RemoveAt(0);
        }

        // 최근 대화 이력을 Gemini userPrompt에 넣을 텍스트로 반환.
        public string RecentHistory() => _history.Count == 0 ? "(none)" : string.Join("\n", _history);

        // ===== 슬롯 =====

        // mark_objective_complete / return_from_subflow의 slots_filled 반영.
        public void ApplySlots(Dictionary<string, string> slots)
        {
            if (slots == null) return;
            foreach (var kv in slots)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                _slots[kv.Key] = kv.Value;
            }
        }

        // 프롬프트에 실어 보낼 슬롯 상태. 시스템 프롬프트가 "슬롯은 별도로 전달되며 authoritative"라고
        // 선언해 놓고 정작 안 넘기던 것을 실제로 넘기기 위한 것.
        public string SlotsSnapshot() =>
            _slots.Count == 0 ? "(none)" : string.Join(", ", _slots.Select(kv => $"{kv.Key}={kv.Value}"));

        // ===== 서브플로 =====

        public void EnterSubflow(string subflowId, string pendingTopic)
        {
            if (string.IsNullOrEmpty(subflowId)) return;
            _pendingQueue.Add(CurrentObjectiveId);
            ActiveSubflowId = subflowId;
            PendingTopic = pendingTopic;
        }

        // 서브플로 종료 — 진입 당시 objective로 복귀. 스택이 비어 있으면 현재 유지.
        public void ReturnFromSubflow()
        {
            ActiveSubflowId = null;
            PendingTopic = null;
            if (_pendingQueue.Count == 0) return;

            var restored = _pendingQueue[_pendingQueue.Count - 1];
            _pendingQueue.RemoveAt(_pendingQueue.Count - 1);
            SetObjective(restored);
        }

        // ===== 진행 제어 (하이브리드) =====

        // LLM이 제안한 objective_id를 검증해 실제로 이동할 단계를 돌려준다.
        // 더 갈 곳이 없으면 null (호출부가 세션 완료 처리).
        public string ResolveNextObjective(string requested)
        {
            if (_objectiveOrder.Length == 0)
            {
                Debug.LogWarning("[DialogueManager] objective 순서 미주입 — 진행 불가");
                return null;
            }

            int currentIdx = Array.IndexOf(_objectiveOrder, CurrentObjectiveId);

            if (!string.IsNullOrEmpty(requested))
            {
                int requestedIdx = Array.IndexOf(_objectiveOrder, requested);

                if (requestedIdx < 0)
                {
                    // 환각 방어 — 시나리오에 없는 단계 이름
                    Debug.LogWarning($"[DialogueManager] LLM이 없는 objective 지정: '{requested}' — 순서상 다음으로 폴백");
                }
                else if (requestedIdx < currentIdx)
                {
                    // 역행 거부 — 이미 지나온 단계로 되돌아가면 무한 루프가 된다
                    Debug.LogWarning($"[DialogueManager] objective 역행 거부: {CurrentObjectiveId} → {requested}");
                    return null;
                }
                else if (requestedIdx == currentIdx)
                {
                    // 현재 단계를 "완료"로 지목한 정상 케이스 — 순서상 다음으로 진행
                }
                else if (HasCards(requested))
                {
                    return requested;
                }
                else
                {
                    Debug.Log($"[DialogueManager] '{requested}' 카드 없음 — 순서상 다음으로 폴백");
                }
            }

            // 다음으로: 카드가 있는 가장 가까운 후속 단계
            for (int i = Math.Max(currentIdx, -1) + 1; i < _objectiveOrder.Length; i++)
            {
                var next = _objectiveOrder[i];
                if (HasCards(next)) return next;
                Debug.Log($"[DialogueManager] '{next}' objective 카드 없음 — 건너뜀");
            }
            return null;
        }

        private bool HasCards(string objectiveId) => _hasCards == null || _hasCards(objectiveId);

        public void ApplyToolCall(DialogueTool tool, string npcText, string[] cardIds)
        {
            OnToolCallApplied?.Invoke(tool, npcText, cardIds);
        }

        public void SetObjective(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId) || CurrentObjectiveId == objectiveId) return;

            CurrentObjectiveId = objectiveId;
            AttemptCount = 0;
            CurrentScaffoldLevel = ScaffoldLevel.None;
            OnObjectiveChanged?.Invoke(objectiveId);
        }

        // 턴 상한 도달 또는 같은 단계에서 반복 실패 — 세션을 부드럽게 마무리해야 한다.
        // 이전에는 정의만 되어 있고 호출부가 없었다.
        // TurnCount는 이번 턴까지 포함해 증가한 뒤 검사되므로 ">" 를 쓴다.
        // ">=" 였다면 turnLimit=12일 때 12번째 발화가 응답 없이 잘린다.
        public bool ShouldForceComplete() =>
            TurnCount > _turnLimit || _retryPolicy.ShouldForceComplete(AttemptCount);
    }
}
