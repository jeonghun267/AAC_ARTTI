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

        public event Action<string> OnObjectiveChanged;
        public event Action<DialogueTool, string, string[]> OnToolCallApplied;

        public void Initialize(string initialObjectiveId)
        {
            CurrentObjectiveId = initialObjectiveId;
            AttemptCount = 0;
            CurrentScaffoldLevel = ScaffoldLevel.None;
            _slots.Clear();
            _pendingQueue.Clear();
        }

        public void HandleUserTurn(AACCard selectedCard, string sttText)
        {
            AttemptCount++;
            CurrentScaffoldLevel = _retryPolicy.GetScaffoldLevel(AttemptCount);
        }

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
