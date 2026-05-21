using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Artti.AAC
{
    public class AACCardButton : MonoBehaviour
    {
        [SerializeField] private AACCard card;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text phraseText;
        [SerializeField] private Button button;

        public event Action<AACCard> OnCardSelected;

        public AACCard Card => card;

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
            RefreshUI();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        public void SetCard(AACCard newCard)
        {
            card = newCard;
            RefreshUI();
        }

        public void RefreshUI()
        {
            if (card == null) return;

            if (iconImage != null && card.symbol != null)
            {
                iconImage.sprite = card.symbol.sprite;
            }

            if (phraseText != null && card.phrase != null)
            {
                phraseText.text = card.phrase.text;
            }
        }

        private void HandleClick()
        {
            if (card != null)
            {
                OnCardSelected?.Invoke(card);
            }
        }
    }
}
