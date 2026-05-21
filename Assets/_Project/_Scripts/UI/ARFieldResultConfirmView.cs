using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    public class ARFieldResultConfirmView : MonoBehaviour
    {
        [SerializeField] private RawImage capturedImage;
        [SerializeField] private Image aacIcon;
        [SerializeField] private TMP_Text categoryLabel;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button retryButton;

        public event Action OnRetryRequested;

        private void Awake()
        {
            if (retryButton != null)
                retryButton.onClick.AddListener(() => OnRetryRequested?.Invoke());
        }

        public void Bind(Texture capture, Sprite icon, string category, string description)
        {
            if (capturedImage != null) capturedImage.texture = capture;
            if (aacIcon != null) aacIcon.sprite = icon;
            if (categoryLabel != null) categoryLabel.text = category;
            if (descriptionText != null) descriptionText.text = description;
        }
    }
}
