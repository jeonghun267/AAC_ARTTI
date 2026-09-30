using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        // 변경점: 화면 효과 (씬 수정 없이 기존 연결만 사용)
        // - 상품/분류 칸: 목록이 바뀔 때 차례로 톡 나타나고, 누르면 살짝 작아졌다 돌아온다
        // - 대화 힌트: 누른 문장이 톡 커지고, 단계가 바뀌어 문장이 바뀌면 차례로 스며 나오며, 새 추천 문장이 한 번 톡 커진다
        // - 안내 문구: 바뀔 때 스며 나온다
        static readonly Vector2 LabelAnchorLeft = new Vector2(0f, 0.5f);
        const float TileStep = 0.05f;
        const float TileSeconds = 0.28f;
        const float TileFromScale = 0.85f;
        const float FirstRevealDelay = 0.3f;   // 씬 진입 페이드가 걷히는 동안 나타나게
        const float HintStep = 0.05f;

        private Vector2[] _labelHomes;
        private CanvasGroup[] _labelGroups;
        private CanvasGroup _guideGroup;
        private CanvasGroup _practiceGroup;
        private CancellationTokenSource _tilesCts;
        private CancellationTokenSource _hintsCts;
        private bool _tilesRevealed;
        private int _recommendedIndex = -1;

        private static readonly string[] BagPhrases =
            { "봉투 주세요", "봉투 필요 없어요", "얼마예요?", "계산할게요", "감사합니다" };
        private static readonly string[] PaymentPhrases =
            { "카드로 할게요", "현금으로 할게요", "얼마예요?", "계산할게요", "감사합니다" };

        public event Action<string, string, string> OnProductSelected;
        public event Action<string> OnQuickPhraseSelected;

        public bool HasProducts => productButtons != null && productButtons.Length > 0;

        // 변경점: 음성 버튼 명료도의 기준 문장 — 현재 단계에서 강조(추천)된 대화 힌트.
        public string RecommendedPhrase { get; private set; } = string.Empty;

        // 변경점: 음성 버튼에서 사용자가 말할 수 있는 후보 문장(대화 힌트 + 상품 요청 문장).
        //   인식 문장이 기준 문장이 아닌 다른 후보에 더 가까우면 명료도를 기록하지 않는 데 쓴다.
        public IEnumerable<string> GetSpeechCandidates()
        {
            var phrases = _activeQuickPhrases ?? quickPhrases;
            for (int i = 0; phrases != null && i < phrases.Length; i++)
                if (!string.IsNullOrWhiteSpace(phrases[i])) yield return phrases[i];
            for (int i = 0; productNames != null && i < productNames.Length; i++)
                if (!string.IsNullOrWhiteSpace(productNames[i]))
                    yield return ValueAt(productUtterances, i, productNames[i] + " 주세요");
        }

        private void Awake()
        {
            if (previousButton != null) previousButton.onClick.AddListener(ShowPrevious);
            if (nextButton != null) nextButton.onClick.AddListener(ShowNext);
            if (helpButton != null) helpButton.onClick.AddListener(ShowHelp);
            if (helpCloseButton != null) helpCloseButton.onClick.AddListener(HideHelp);

            WireCategories();
            WireProducts();
            WireQuickPhrases();
            AttachPress(categoryButtons);
            AttachPress(productButtons);
            if (helpButton != null) UIPressScale.Attach(helpButton);
            CacheTextEffects();
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

            RecommendedPhrase = ValueAt(_activeQuickPhrases, recommendedIndex, ValueAt(quickPhrases, recommendedIndex, string.Empty));

            bool phrasesChanged = false;
            for (int i = 0; quickPhraseLabels != null && i < quickPhraseLabels.Length; i++)
            {
                var label = quickPhraseLabels[i];
                if (label == null) continue;
                string phrase = ValueAt(_activeQuickPhrases, i, ValueAt(quickPhrases, i, string.Empty));
                if (label.text != phrase)
                {
                    label.text = phrase;
                    phrasesChanged = true;
                }
                bool recommended = i == recommendedIndex;
                label.color = recommended ? new Color32(31, 94, 220, 255) : new Color32(26, 35, 58, 255);
                label.fontStyle = recommended ? FontStyles.Bold : FontStyles.Normal;
            }
            PlayHintEffects(phrasesChanged, recommendedIndex);
        }

        // 변경점: 문장이 바뀌면 차례로 스며 나오고, 추천 문장이 바뀌면 그 문장이 한 번 톡 커져 "다음에 할 말"을 알려 준다.
        //   씬 첫 표시(시작 단계)는 그대로 둔다.
        private void PlayHintEffects(bool phrasesChanged, int recommendedIndex)
        {
            bool first = _recommendedIndex < 0;
            bool recommendedChanged = recommendedIndex != _recommendedIndex;
            _recommendedIndex = recommendedIndex;
            if (first || _labelGroups == null || (!phrasesChanged && !recommendedChanged)) return;

            var ct = UIFx.Restart(ref _hintsCts, this);
            ResetHints();   // 앞 연출이 중간에 끊겨 흐리거나 커진 채 남지 않게
            float punchDelay = 0.15f;
            if (phrasesChanged)
            {
                for (int i = 0; i < _labelGroups.Length; i++)
                    UIFx.FadeGroup(_labelGroups[i], 0f, 1f, i * HintStep, 0.25f, ct).Forget();
                punchDelay += _labelGroups.Length * HintStep + 0.1f;
            }
            PunchLabel(recommendedIndex, punchDelay, ct);
        }

        private void ResetHints()
        {
            for (int i = 0; i < _labelGroups.Length; i++)
            {
                if (_labelGroups[i] != null) _labelGroups[i].alpha = 1f;
                if (quickPhraseLabels[i] != null)
                    UIFx.SetScaleAround(quickPhraseLabels[i].rectTransform, _labelHomes[i], LabelAnchorLeft, 1f);
            }
        }

        private void PunchLabel(int index, float delay, CancellationToken ct)
        {
            if (quickPhraseLabels == null || _labelHomes == null || index < 0 || index >= quickPhraseLabels.Length
                || quickPhraseLabels[index] == null) return;
            UIFx.PunchAround(quickPhraseLabels[index].rectTransform, _labelHomes[index], LabelAnchorLeft, 0.1f, delay, 0.26f, ct).Forget();
        }

        private void SetGuide(string text)
        {
            if (quickPhraseGuide == null || quickPhraseGuide.text == text) return;
            quickPhraseGuide.text = text;
            FadeInText(_guideGroup);
        }

        private void SetSpeechPractice(string text)
        {
            if (speechPracticeText == null || speechPracticeText.text == text) return;
            speechPracticeText.text = text;
            FadeInText(_practiceGroup);
        }

        private void FadeInText(CanvasGroup group)
        {
            if (_recommendedIndex < 0) return;   // 씬 첫 표시는 그대로
            UIFx.FadeGroup(group, 0f, 1f, 0f, 0.3f, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void CacheTextEffects()
        {
            int n = quickPhraseLabels != null ? quickPhraseLabels.Length : 0;
            _labelHomes = new Vector2[n];
            _labelGroups = new CanvasGroup[n];
            for (int i = 0; i < n; i++)
            {
                if (quickPhraseLabels[i] == null) continue;
                _labelHomes[i] = quickPhraseLabels[i].rectTransform.anchoredPosition;
                _labelGroups[i] = UIFx.Group(quickPhraseLabels[i]);
            }
            if (quickPhraseGuide != null) _guideGroup = UIFx.Group(quickPhraseGuide);
            if (speechPracticeText != null) _practiceGroup = UIFx.Group(speechPracticeText);
        }

        private static void AttachPress(Button[] buttons)
        {
            for (int i = 0; buttons != null && i < buttons.Length; i++) UIPressScale.Attach(buttons[i]);
        }

        // 목록이 바뀔 때 보이는 칸이 차례로 톡 나타난다(칸은 가로 레이아웃 자식이라 크기·알파만 바꾼다)
        private void RevealTiles(Button[] tiles)
        {
            if (tiles == null) return;
            float delay = _tilesRevealed ? 0f : FirstRevealDelay;
            _tilesRevealed = true;
            var ct = UIFx.Restart(ref _tilesCts, this);
            int n = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null || !tiles[i].gameObject.activeSelf) continue;
                UIFx.PopScaleIn((RectTransform)tiles[i].transform, delay + Mathf.Min(n, 6) * TileStep,
                    TileSeconds, TileFromScale, ct).Forget();
                n++;
            }
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
            RevealTiles(productButtons);
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
            RevealTiles(categoryButtons);
        }

        private void SelectQuickPhrase(int index)
        {
            string phrase = ValueAt(_activeQuickPhrases, index, ValueAt(quickPhrases, index, null));
            if (string.IsNullOrWhiteSpace(phrase)) return;
            PunchLabel(index, 0f, this.GetCancellationTokenOnDestroy());   // 변경점: 누른 문장이 톡 커진다
            OnQuickPhraseSelected?.Invoke(phrase);
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

        // 변경점: 팝업 열기/닫기 연출
        private void ShowHelp() => UIPopupMotion.Open(helpPanel);

        private void HideHelp() => UIPopupMotion.Close(helpPanel);
    }
}
