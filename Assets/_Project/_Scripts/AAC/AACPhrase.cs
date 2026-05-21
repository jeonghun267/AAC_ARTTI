using UnityEngine;

namespace Artti.AAC
{
    public enum AACRegister { Polite, Casual }
    public enum AACLengthClass { Short, Medium, Long }

    [CreateAssetMenu(fileName = "phr_", menuName = "AAC/Phrase", order = 101)]
    public class AACPhrase : ScriptableObject
    {
        public string id;
        public string text;
        public string ttsText;
        public AACRegister register = AACRegister.Polite;
        public AACLengthClass lengthClass = AACLengthClass.Short;
    }
}
