using UnityEngine;

namespace Artti.AAC
{
    [CreateAssetMenu(fileName = "card_", menuName = "AAC/Card", order = 102)]
    public class AACCard : ScriptableObject
    {
        public string id;
        public AACSymbol symbol;
        public AACPhrase phrase;
        public string[] applicableScenarios;
        public string[] applicableObjectives;
        public string[] tags;
        public string branchId;
        public int priorityHint;
    }
}
