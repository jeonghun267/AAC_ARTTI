using UnityEngine;

namespace Artti.AAC
{
    [CreateAssetMenu(fileName = "sym_", menuName = "AAC/Symbol", order = 100)]
    public class AACSymbol : ScriptableObject
    {
        public string id;
        public Sprite sprite;
        public string[] semanticTags;
        [TextArea] public string description;
        public string licenseInfo = "Pictograms author: Sergio Palao. Origin: ARASAAC. License: CC BY-NC-SA";
    }
}
