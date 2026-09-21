using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

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
        [SerializeField] private Button[] categoryButtons;
        [SerializeField] private string[] categoryIds;
        [SerializeField] private RectTransform categoryContent;
        [SerializeField] private Button[] productButtons;
        [SerializeField] private string[] productIds;
        [SerializeField] private string[] productNames;
        [SerializeField] private string[] productUtterances;
        [SerializeField] private string[] productCategoryIds;
        [SerializeField] private RectTransform productContent;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("Dialogue hints")]
        [SerializeField] private Button[] quickPhraseButtons;
        [SerializeField] private string[] quickPhrases;
        [SerializeField] private TMP_Text[] quickPhraseLabels;
        [SerializeField] private TMP_Text quickPhraseGuide;

        [Header("Speech practice")]
        [SerializeField] private TMP_Text speechPracticeText;

        [Header("Help")]
        [SerializeField] private Button helpButton;
        [SerializeField] private Button helpCloseButton;
        [SerializeField] private GameObject helpPanel;

        private UnityAction[] _categoryCallbacks;
        private UnityAction[] _productCallbacks;
        private UnityAction[] _quickPhraseCallbacks;
        private bool _showingProducts;
        private string[] _activeQuickPhrases;

        private static readonly string[] BagPhrases =
            { "봉투 주세요", "봉투 필요 없어요", "얼마예요?", "계산할게요", "감사합니다" };
        private static readonly string[] PaymentPhrases =
            { "카드로 할게요", "현금으로 할게요", "얼마예요?", "계산할게요", "감사합니다" };

        public event Action<string, string, string> OnProductSelected;
        public event Action<string> OnQuickPhraseSelected;

        public bool HasProducts => productButtons != null && productButtons.Length > 0;

        private void Awake()
        {
            if (previousButton != null) previousButton.onClick.AddListener(ShowPrevious);
            if (nextButton != null) nextButton.onClick.AddListener(ShowNext);
            if (helpButton != null) helpButton.onClick.AddListener(ShowHelp);
            if (helpCloseButton != null) helpCloseButton.onClick.AddListener(HideHelp);

            WireCategories();
            WireProducts();
            WireQuickPhrases();
            _activeQuickPhrases = quickPhrases;
            ShowCategories();
            if (helpPanel != null) helpPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(ShowPrevious);
            if (nextButton != null) nextButton.onClick.RemoveListener(ShowNext);
            if (helpButton != null) helpButton.onClick.RemoveListener(ShowHelp);
            if (helpCloseButton != null) helpCloseButton.onClick.RemoveListener(HideHelp);

            for (int i = 0; categoryButtons != null && _categoryCallbacks != null && i < categoryButtons.Length; i++)
                if (categoryButtons[i] != null && _categoryCallbacks[i] != null)
                    categoryButtons[i].onClick.RemoveListener(_categoryCallbacks[i]);

            for (int i = 0; productButtons != null && _productCallbacks != null && i < productButtons.Length; i++)
                if (productButtons[i] != null && _productCallbacks[i] != null)
                    productButtons[i].onClick.RemoveListener(_productCallbacks[i]);

            for (int i = 0; quickPhraseButtons != null && _quickPhraseCallbacks != null && i < quickPhraseButtons.Length; i++)
                if (quickPhraseButtons[i] != null && _quickPhraseCallbacks[i] != null)
                    quickPhraseButtons[i].onClick.RemoveListener(_quickPhraseCallbacks[i]);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            for (int i = 0; categoryButtons != null && i < categoryButtons.Length; i++)
                if (categoryButtons[i] != null) categoryButtons[i].interactable = enabled;
            for (int i = 0; productButtons != null && i < productButtons.Length; i++)
                if (productButtons[i] != null) productButtons[i].interactable = enabled;
            for (int i = 0; quickPhraseButtons != null && i < quickPhraseButtons.Length; i++)
                if (quickPhraseButtons[i] != null) quickPhraseButtons[i].interactable = enabled;
            if (previousButton != null) previousButton.interactable = enabled;
            if (nextButton != null) nextButton.interactable = enabled;
        }

        /// <summary>
        /// 같은 5개 자리를 현재 단계에서 실제로 답할 수 있는 문장으로 바꾼다.
        /// 추천 상품/힌트만으로도 편의점 흐름을 끝까지 진행할 수 있어야 한다.
        /// </summary>
        public void SetDialogueContext(string objectiveId, bool hasItems)
        {
            int recommendedIndex;
            switch (objectiveId)
            {
                case "extras":
                    _activeQuickPhrases = BagPhrases;
                    recommendedIndex = 1;
                    SetGuide("다음: 봉투가 필요한지 선택하세요");
                    SetSpeechPractice("‘봉투 주세요’ 또는\n‘봉투 필요 없어요’라고 말해보세요.");
                    break;
                case "checkout":
                    _activeQuickPhrases = PaymentPhrases;
                    recommendedIndex = 0;
                    SetGuide("다음: 결제 방법을 선택하세요");
                    SetSpeechPractice("‘카드로 할게요’ 또는\n‘현금으로 할게요’라고 말해보세요.");
                    break;
                case "farewell":
                    _activeQuickPhrases = quickPhrases;
                    recommendedIndex = 4;
                    SetGuide("다음: ‘감사합니다’를 눌러 마무리하세요");
                    SetSpeechPractice("마지막으로\n‘감사합니다’라고 말해보세요.");
                    break;
                case "select_items":
                    _activeQuickPhrases = quickPhrases;
                    recommendedIndex = hasItems ? 3 : 0;
                    SetGuide(hasItems
                        ? "상품을 더 고르거나 ‘계산할게요’를 누르세요"
                        : "원하는 상품을 먼저 선택하세요");
                    SetSpeechPractice(hasItems
                        ? "더 필요한 상품이 없다면\n‘계산할게요’라고 말해보세요."
                        : "원하는 상품명을 말해보세요.\n예: ‘물 주세요’");
                    break;
                default:
                    _activeQuickPhrases = quickPhrases;
                    recommendedIndex = 0;
                    SetGuide("상품을 선택하거나 대화 문장을 눌러보세요");
                    SetSpeechPractice("원하는 상품명을 말해보세요.\n예: ‘물 주세요’");
                    break;
            }

            for (int i = 0; quickPhraseLabels != null && i < quickPhraseLabels.Length; i++)
            {
                var label = quickPhraseLabels[i];
                if (label == null) continue;
                label.text = ValueAt(_activeQuickPhrases, i, ValueAt(quickPhrases, i, string.Empty));
                bool recommended = i == recommendedIndex;
                label.color = recommended ? new Color32(31, 94, 220, 255) : new Color32(26, 35, 58, 255);
                label.fontStyle = recommended ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private void SetGuide(string text)
        {
            if (quickPhraseGuide != null) quickPhraseGuide.text = text;
        }

        private void SetSpeechPractice(string text)
        {
            if (speechPracticeText != null) speechPracticeText.text = text;
        }

        private void WireCategories()
        {
            int count = categoryButtons != null ? categoryButtons.Length : 0;
            _categoryCallbacks = new UnityAction[count];
            for (int i = 0; i < count; i++)
            {
                int captured = i;
                _categoryCallbacks[i] = () => SelectCategory(captured);
                if (categoryButtons[i] != null) categoryButtons[i].onClick.AddListener(_categoryCallbacks[i]);
            }
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

        private void SelectCategory(int index)
        {
            string categoryId = ValueAt(categoryIds, index, null);
            if (string.IsNullOrWhiteSpace(categoryId) || productContent == null) return;

            int visibleCount = 0;
            for (int i = 0; productButtons != null && i < productButtons.Length; i++)
            {
                bool visible = string.Equals(
                    ValueAt(productCategoryIds, i, null), categoryId, StringComparison.Ordinal);
                if (productButtons[i] != null) productButtons[i].gameObject.SetActive(visible);
                if (visible) visibleCount++;
            }

            if (categoryContent != null) categoryContent.gameObject.SetActive(false);
            productContent.gameObject.SetActive(true);
            productContent.sizeDelta = new Vector2(Mathf.Max(1, visibleCount) * 134f, 150f);
            if (productScroll != null)
            {
                productScroll.StopMovement();
                productScroll.content = productContent;
                productScroll.horizontalNormalizedPosition = 0f;
            }
            _showingProducts = true;
        }

        private void ShowCategories()
        {
            if (productContent != null) productContent.gameObject.SetActive(false);
            if (categoryContent != null) categoryContent.gameObject.SetActive(true);
            if (productScroll != null && categoryContent != null)
            {
                productScroll.StopMovement();
                productScroll.content = categoryContent;
                productScroll.horizontalNormalizedPosition = 0f;
            }
            _showingProducts = false;
        }

        private void SelectQuickPhrase(int index)
        {
            string phrase = ValueAt(_activeQuickPhrases, index, ValueAt(quickPhrases, index, null));
            if (!string.IsNullOrWhiteSpace(phrase)) OnQuickPhraseSelected?.Invoke(phrase);
        }

        private static string ValueAt(string[] values, int index, string fallback) =>
            values != null && index >= 0 && index < values.Length && !string.IsNullOrWhiteSpace(values[index])
                ? values[index]
                : fallback;

        private void ShowPrevious()
        {
            if (_showingProducts)
            {
                ShowCategories();
                return;
            }
            ScrollBy(-0.34f);
        }

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
