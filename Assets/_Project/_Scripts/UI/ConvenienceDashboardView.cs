using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Artti.UI
{
    /// <summary>
    /// 편의점 대시보드의 화면 입력을 TrainingSceneRoot에 전달한다.
    /// 대화 상태와 Gemini/TTS 처리는 이 View가 아닌 TrainingSceneRoot가 소유한다.
    /// </summary>
    public sealed class ConvenienceDashboardView : MonoBehaviour
    {
        [Header("Recommended products")]
        [SerializeField] private ScrollRect productScroll;
        [SerializeField] private Button[] productButtons;
        [SerializeField] private string[] productIds;
        [SerializeField] private string[] productNames;
        [SerializeField] private string[] productUtterances;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("Dialogue hints")]
        [SerializeField] private Button[] quickPhraseButtons;
        [SerializeField] private string[] quickPhrases;

        [Header("Help")]
        [SerializeField] private Button helpButton;
        [SerializeField] private Button helpCloseButton;
        [SerializeField] private GameObject helpPanel;

        private UnityAction[] _productCallbacks;
        private UnityAction[] _quickPhraseCallbacks;

        public event Action<string, string, string> OnProductSelected;
        public event Action<string> OnQuickPhraseSelected;

        public bool HasProducts => productButtons != null && productButtons.Length > 0;

        private void Awake()
        {
            if (previousButton != null) previousButton.onClick.AddListener(ShowPrevious);
            if (nextButton != null) nextButton.onClick.AddListener(ShowNext);
            if (helpButton != null) helpButton.onClick.AddListener(ShowHelp);
            if (helpCloseButton != null) helpCloseButton.onClick.AddListener(HideHelp);

            WireProducts();
            WireQuickPhrases();
            if (helpPanel != null) helpPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(ShowPrevious);
            if (nextButton != null) nextButton.onClick.RemoveListener(ShowNext);
            if (helpButton != null) helpButton.onClick.RemoveListener(ShowHelp);
            if (helpCloseButton != null) helpCloseButton.onClick.RemoveListener(HideHelp);

            for (int i = 0; productButtons != null && _productCallbacks != null && i < productButtons.Length; i++)
                if (productButtons[i] != null && _productCallbacks[i] != null)
                    productButtons[i].onClick.RemoveListener(_productCallbacks[i]);

            for (int i = 0; quickPhraseButtons != null && _quickPhraseCallbacks != null && i < quickPhraseButtons.Length; i++)
                if (quickPhraseButtons[i] != null && _quickPhraseCallbacks[i] != null)
                    quickPhraseButtons[i].onClick.RemoveListener(_quickPhraseCallbacks[i]);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            for (int i = 0; productButtons != null && i < productButtons.Length; i++)
                if (productButtons[i] != null) productButtons[i].interactable = enabled;
            for (int i = 0; quickPhraseButtons != null && i < quickPhraseButtons.Length; i++)
                if (quickPhraseButtons[i] != null) quickPhraseButtons[i].interactable = enabled;
            if (previousButton != null) previousButton.interactable = enabled;
            if (nextButton != null) nextButton.interactable = enabled;
        }

        private void WireProducts()
        {
            int count = productButtons != null ? productButtons.Length : 0;
            _productCallbacks = new UnityAction[count];
            for (int i = 0; i < count; i++)
            {
                int captured = i;
                _productCallbacks[i] = () => SelectProduct(captured);
                if (productButtons[i] != null) productButtons[i].onClick.AddListener(_productCallbacks[i]);
            }
        }

        private void WireQuickPhrases()
        {
            int count = quickPhraseButtons != null ? quickPhraseButtons.Length : 0;
            _quickPhraseCallbacks = new UnityAction[count];
            for (int i = 0; i < count; i++)
            {
                int captured = i;
                _quickPhraseCallbacks[i] = () => SelectQuickPhrase(captured);
                if (quickPhraseButtons[i] != null) quickPhraseButtons[i].onClick.AddListener(_quickPhraseCallbacks[i]);
            }
        }

        private void SelectProduct(int index)
        {
            string id = ValueAt(productIds, index, $"dashboard_product_{index + 1}");
            string name = ValueAt(productNames, index, "상품");
            string utterance = ValueAt(productUtterances, index, $"{name} 주세요");
            OnProductSelected?.Invoke(id, name, utterance);
        }

        private void SelectQuickPhrase(int index)
        {
            string phrase = ValueAt(quickPhrases, index, null);
            if (!string.IsNullOrWhiteSpace(phrase)) OnQuickPhraseSelected?.Invoke(phrase);
        }

        private static string ValueAt(string[] values, int index, string fallback) =>
            values != null && index >= 0 && index < values.Length && !string.IsNullOrWhiteSpace(values[index])
                ? values[index]
                : fallback;

        private void ShowPrevious() => ScrollBy(-0.34f);

        private void ShowNext() => ScrollBy(0.34f);

        private void ScrollBy(float amount)
        {
            if (productScroll == null) return;
            productScroll.StopMovement();
            productScroll.horizontalNormalizedPosition = Mathf.Clamp01(
                productScroll.horizontalNormalizedPosition + amount);
        }

        private void ShowHelp()
        {
            if (helpPanel != null) helpPanel.SetActive(true);
        }

        private void HideHelp()
        {
            if (helpPanel != null) helpPanel.SetActive(false);
        }
    }
}
