using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.AAC;

namespace Artti.UI
{
    public class AACCardSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Button button;

        private AACCard currentCard;
        private Action<AACCard> currentCallback;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        public void Setup(AACCard card, Action<AACCard> onSelected)
        {
            currentCard = card;
            currentCallback = onSelected;

            Debug.Log($"[Slot:{name}] Setup card={card?.id ?? "<null>"}, symbol={card?.symbol?.name ?? "<null>"}, sprite={card?.symbol?.sprite?.name ?? "<null>"}, iconImage={(iconImage != null)}, labelText={(labelText != null)}");

            if (iconImage != null && card != null && card.symbol != null && card.symbol.sprite != null)
                iconImage.sprite = card.symbol.sprite;
            else
                Debug.LogWarning($"[Slot:{name}] sprite 할당 실패");

            if (labelText != null && card != null && card.phrase != null)
                labelText.text = card.phrase.text;
        }

        private void HandleClick()
        {
            if (currentCard != null)
                currentCallback?.Invoke(currentCard);
        }
    }
}
