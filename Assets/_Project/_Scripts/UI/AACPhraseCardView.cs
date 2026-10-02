using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // AAC 문장 카드(CardGenerator가 복제하는 템플릿)의 모양을 문장에 맞춘다: 글, 아이콘, 파랑/보라 바탕, 아이콘 둘레 원 색.
    //   누르기(TTS)는 CardGenerator가 붙인다. 모양 고르기는 PhraseStylePicker.
    //   자리/그림 연결은 에디터 스크립트(ARFieldGreetingLayout)가 한다.
    [DisallowMultipleComponent]
    public sealed class AACPhraseCardView : MonoBehaviour
    {
        [Serializable]
        public struct Style
        {
            public Sprite icon;
            public bool purple;
            [Tooltip("문장에 이 낱말 중 하나가 있으면 이 모양")]
            public string[] keywords;
        }

        [SerializeField] private TMP_Text label;
        [SerializeField] private Image icon;
        [SerializeField] private Image ring;
        [SerializeField] private GameObject blueBackground;
        [SerializeField] private GameObject purpleBackground;
        [SerializeField] private Color blueRing = new Color(0.55f, 0.75f, 1f, 1f);
        [SerializeField] private Color purpleRing = new Color(0.82f, 0.66f, 1f, 1f);
        [SerializeField] private Style[] styles = Array.Empty<Style>();

        private string[][] _keywords;

        public void Show(string phrase, int index)
        {
            if (label != null) label.text = phrase;
            if (_keywords == null || _keywords.Length != styles.Length)
            {
                _keywords = new string[styles.Length][];
                for (int i = 0; i < styles.Length; i++) _keywords[i] = styles[i].keywords;
            }

            int pick = PhraseStylePicker.Pick(phrase, index, _keywords);
            if (pick < 0) return;
            var style = styles[pick];
            if (icon != null && style.icon != null) icon.sprite = style.icon;
            if (blueBackground != null) blueBackground.SetActive(!style.purple);
            if (purpleBackground != null) purpleBackground.SetActive(style.purple);
            if (ring != null) ring.color = style.purple ? purpleRing : blueRing;
        }
    }
}
