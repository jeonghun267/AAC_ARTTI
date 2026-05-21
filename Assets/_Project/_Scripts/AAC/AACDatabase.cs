using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Artti.AAC
{
    public class AACDatabase : ScriptableObject
    {
        public List<AACSymbol> symbols = new List<AACSymbol>();
        public List<AACPhrase> phrases = new List<AACPhrase>();
        public List<AACCard> cards = new List<AACCard>();

        Dictionary<string, AACSymbol> _symById;
        Dictionary<string, AACPhrase> _phrById;
        Dictionary<string, AACCard> _cardById;

        public AACSymbol GetSymbol(string id)
        {
            EnsureIndex();
            return _symById.TryGetValue(id, out var v) ? v : null;
        }
        public AACPhrase GetPhrase(string id)
        {
            EnsureIndex();
            return _phrById.TryGetValue(id, out var v) ? v : null;
        }
        public AACCard GetCard(string id)
        {
            EnsureIndex();
            return _cardById.TryGetValue(id, out var v) ? v : null;
        }

        public IEnumerable<AACCard> CardsForScenario(string scenarioId)
        {
            foreach (var c in cards)
                if (c.applicableScenarios != null && System.Array.IndexOf(c.applicableScenarios, scenarioId) >= 0)
                    yield return c;
        }

        public IEnumerable<AACCard> CardsForObjective(string scenarioId, string objectiveId)
        {
            foreach (var c in CardsForScenario(scenarioId))
                if (c.applicableObjectives != null && System.Array.IndexOf(c.applicableObjectives, objectiveId) >= 0)
                    yield return c;
        }

        public IEnumerable<AACCard> CardsInBranch(string branchId)
        {
            foreach (var c in cards)
                if (c.branchId == branchId) yield return c;
        }

        void EnsureIndex()
        {
            if (_symById != null) return;
            _symById = symbols.Where(s => s != null && !string.IsNullOrEmpty(s.id)).ToDictionary(s => s.id);
            _phrById = phrases.Where(p => p != null && !string.IsNullOrEmpty(p.id)).ToDictionary(p => p.id);
            _cardById = cards.Where(c => c != null && !string.IsNullOrEmpty(c.id)).ToDictionary(c => c.id);
        }

        public void InvalidateIndex() { _symById = null; _phrById = null; _cardById = null; }
    }
}
