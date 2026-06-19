using System;
using System.Collections.Generic;
using Artti.AAC;

namespace Artti.Training
{
    public class DialogueManager
    {
        public string CurrentObjectiveId { get; private set; }
        public int AttemptCount { get; private set; }
        public ScaffoldLevel CurrentScaffoldLevel { get; private set; }
        
        private Dictionary<string, string> _slots = new Dictionary<string, string>();
        private List<string> _pendingQueue = new List<string>();
        private ScaffoldedRetryPolicy _retryPolicy = new ScaffoldedRetryPolicy();

        // Unit 4: 다중 턴 맥락용 대화 이력 (Gemini에 최근 N턴 전달). "User:"/"Clerk:" 줄로 누적.
        private readonly List<string> _history = new List<string>();
        private const int HistoryLinesKept = 6; // 약 3턴(사용자+점원)

        public event Action<string> OnObjectiveChanged;
        public event Action<DialogueTool, string, string[]> OnToolCallApplied;

        public void Initialize(string initialObjectiveId)
        {
            CurrentObjectiveId = initialObjectiveId;
            AttemptCount = 0;
            CurrentScaffoldLevel = ScaffoldLevel.None;
            _slots.Clear();
            _pendingQueue.Clear();
            _history.Clear();
        }

        public void HandleUserTurn(AACCard selectedCard, string sttText)
        {
            AttemptCount++;
            CurrentScaffoldLevel = _retryPolicy.GetScaffoldLevel(AttemptCount);
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

        public void ApplyToolCall(DialogueTool tool, string npcText, string[] cardIds)
        {
            switch (tool)
            {
                case DialogueTool.MarkObjectiveComplete:
                    // In a real implementation, we'd lookup the next objective from ScenarioCatalog
                    break;
                case DialogueTool.TransitionToObjective:
                    if (cardIds != null && cardIds.Length > 0)
                    {
                        SetObjective(cardIds[0]); // Using cardIds[0] as objectiveId for transition tool
                    }
                    break;
                case DialogueTool.PresentCards:
                    // Keep current objective, just show cards
                    break;
            }

            OnToolCallApplied?.Invoke(tool, npcText, cardIds);
        }
        
        public void SetObjective(string objectiveId)
        {
            if (CurrentObjectiveId != objectiveId)
            {
                CurrentObjectiveId = objectiveId;
                AttemptCount = 0;
                CurrentScaffoldLevel = ScaffoldLevel.None;
                OnObjectiveChanged?.Invoke(objectiveId);
            }
        }

        public bool ShouldForceComplete() => _retryPolicy.ShouldForceComplete(AttemptCount);
    }
}
