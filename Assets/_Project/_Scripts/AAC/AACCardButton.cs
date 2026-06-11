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

        private CanvasGroup _group;

        // STT 수집 중 다른 카드 터치 비활성 + 은은한 흐림 (편의점 시안)
        public void SetLocked(bool locked, bool dim)
        {
            if (button != null) button.interactable = !locked;
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            }
            _group.alpha = dim ? 0.4f : 1f;
        }

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
