using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Artti.Training;

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
        // 변경점(TrainingCon 시안): 분류 없이 상품을 바로 보여 주고, 화살표는 한 쪽씩 넘긴다.
        [SerializeField] private float productTilePitch = 134f;

        [Header("Dialogue hints")]
        [SerializeField] private Button[] quickPhraseButtons;
        [SerializeField] private string[] quickPhrases;
        [SerializeField] private TMP_Text[] quickPhraseLabels;
        [SerializeField] private TMP_Text quickPhraseGuide;

        // 변경점(TrainingCon 시안): 말풍선 문장. 추천 문장은 빛나는 말풍선, 문장 앞에는 분류 아이콘.
        //   비워 두면 옛 배치처럼 글자 색·굵기로만 추천 문장을 표시한다.
        [Header("Dialogue hint bubbles (옵션)")]
        [SerializeField] private GameObject[] quickPhraseActiveBubbles;
        [SerializeField] private Image[] quickPhraseIcons;
        [SerializeField] private string[] iconKeys;
        [SerializeField] private Sprite[] iconSprites;

        // 변경점(TrainingCon 시안): 오른쪽 '대화 힌트' 분류 버튼 5개 (ConvenienceHintCatalog 순서).
        //   현재 단계의 분류가 주황으로 켜지고, 누르면 그 분류 문장이 왼쪽 말풍선 맨 앞에 온다.
        [Header("Hint categories (옵션)")]
        [SerializeField] private Button[] hintCategoryButtons;
        [SerializeField] private Sprite[] hintCategoryNormalSprites;
        [SerializeField] private Sprite[] hintCategoryActiveSprites;

        [Header("Speech practice")]
        [SerializeField] private TMP_Text speechPracticeText;

        [Header("Help")]
        [SerializeField] private Button helpButton;
        [SerializeField] private Button helpCloseButton;
        [SerializeField] private GameObject helpPanel;

        // 변경점(TrainingCon 시안): 키보드로 문장을 써서 말하기 대신 보낸다.
        [Header("Keyboard input (옵션)")]
        [SerializeField] private Button keyboardButton;
        [SerializeField] private GameObject keyboardPanel;
        [SerializeField] private TMP_InputField keyboardField;
        [SerializeField] private Button keyboardSendButton;
        [SerializeField] private Button keyboardCloseButton;

        private UnityAction[] _categoryCallbacks;
        private UnityAction[] _productCallbacks;
        private UnityAction[] _quickPhraseCallbacks;
        private UnityAction[] _hintCategoryCallbacks;
        private bool _showingProducts;
        private string[] _activeQuickPhrases;

        // 변경점: 화면 효과 (씬 수정 없이 기존 연결만 사용)
        // - 상품/분류 칸: 목록이 바뀔 때 차례로 톡 나타나고, 누르면 살짝 작아졌다 돌아온다
        // - 대화 힌트: 누른 문장이 톡 커지고, 단계가 바뀌어 문장이 바뀌면 차례로 스며 나오며, 새 추천 문장이 한 번 톡 커진다
        // - 안내 문구: 바뀔 때 스며 나온다
        static readonly Vector2 LabelAnchorLeft = new Vector2(0f, 0.5f);
        static readonly Color InkColor = new Color32(26, 35, 58, 255);
        static readonly Color RecommendedColor = new Color32(31, 94, 220, 255);
        const float TileStep = 0.05f;
        const float TileSeconds = 0.28f;
        const float TileFromScale = 0.85f;
        const float FirstRevealDelay = 0.3f;   // 씬 진입 페이드가 걷히는 동안 나타나게
        const float HintStep = 0.05f;

        private RectTransform[] _hintTargets;   // 톡 커지는 대상: 말풍선이 있으면 말풍선 칸, 없으면 글자
        private Vector2[] _hintHomes;
        private CanvasGroup[] _hintGroups;
        private CanvasGroup _guideGroup;
        private CanvasGroup _practiceGroup;
        private CancellationTokenSource _tilesCts;
        private CancellationTokenSource _hintsCts;
        private bool _tilesRevealed;
        private int _recommendedIndex = -1;
        private Dictionary<string, Sprite> _icons;

        // 현재 단계의 기본 문장 묶음과 추천 문장. 분류 버튼으로 바꿔 본 뒤에도 여기로 돌아온다.
        private string[] _stepPhrases;
        private string _stepRecommended = string.Empty;
        private int _browsedCategory = -1;
        private int _highlightedCategory = -1;

        private static readonly string[] BagPhrases =
            { "봉투 주세요", "봉투 필요 없어요", "얼마예요?", "계산할게요", "감사합니다" };
        private static readonly string[] PaymentPhrases =
            { "카드로 할게요", "현금으로 할게요", "얼마예요?", "계산할게요", "감사합니다" };

        public event Action<string, string, string> OnProductSelected;
        public event Action<string> OnQuickPhraseSelected;
        public event Action<string> OnTypedUtterance;

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

        private bool HasBubbles => quickPhraseActiveBubbles != null && quickPhraseActiveBubbles.Length > 0;
        private bool ShowsCategoryTiles => categoryContent != null && categoryButtons != null && categoryButtons.Length > 0;

        private void Awake()
        {
            if (previousButton != null) previousButton.onClick.AddListener(ShowPrevious);
            if (nextButton != null) nextButton.onClick.AddListener(ShowNext);
            if (helpButton != null) helpButton.onClick.AddListener(ShowHelp);
            if (helpCloseButton != null) helpCloseButton.onClick.AddListener(HideHelp);
            if (keyboardButton != null) keyboardButton.onClick.AddListener(ShowKeyboard);
            if (keyboardSendButton != null) keyboardSendButton.onClick.AddListener(SendKeyboardText);
            if (keyboardCloseButton != null) keyboardCloseButton.onClick.AddListener(HideKeyboard);
            if (keyboardField != null) keyboardField.onSubmit.AddListener(OnKeyboardSubmit);

            WireCategories();
            WireProducts();
            WireQuickPhrases();
            WireHintCategories();
            AttachPress(categoryButtons);
            AttachPress(productButtons);
            AttachPress(hintCategoryButtons);
            AttachPress(previousButton, nextButton, helpButton, keyboardButton, keyboardSendButton, keyboardCloseButton);
            CacheIcons();
            CacheTextEffects();
            _activeQuickPhrases = quickPhrases;
            if (ShowsCategoryTiles) ShowCategories();
            else ShowAllProducts();
            if (helpPanel != null) helpPanel.SetActive(false);
            if (keyboardPanel != null)
            {
                // 변경점: 화면 자판이 올라오면 키보드 창을 화면 위로 올린다(가운데에 두면 입력 칸·버튼이 자판에 가린다)
                UIKeyboardLift.Attach(keyboardPanel.transform.Find("Card") as RectTransform);
                keyboardPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(ShowPrevious);
            if (nextButton != null) nextButton.onClick.RemoveListener(ShowNext);
            if (helpButton != null) helpButton.onClick.RemoveListener(ShowHelp);
            if (helpCloseButton != null) helpCloseButton.onClick.RemoveListener(HideHelp);
            if (keyboardButton != null) keyboardButton.onClick.RemoveListener(ShowKeyboard);
            if (keyboardSendButton != null) keyboardSendButton.onClick.RemoveListener(SendKeyboardText);
            if (keyboardCloseButton != null) keyboardCloseButton.onClick.RemoveListener(HideKeyboard);
            if (keyboardField != null) keyboardField.onSubmit.RemoveListener(OnKeyboardSubmit);

            Unwire(categoryButtons, _categoryCallbacks);
            Unwire(productButtons, _productCallbacks);
            Unwire(quickPhraseButtons, _quickPhraseCallbacks);
            Unwire(hintCategoryButtons, _hintCategoryCallbacks);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            SetInteractable(categoryButtons, enabled);
            SetInteractable(productButtons, enabled);
            SetInteractable(quickPhraseButtons, enabled);
            if (previousButton != null) previousButton.interactable = enabled;
            if (nextButton != null) nextButton.interactable = enabled;
            if (keyboardButton != null) keyboardButton.interactable = enabled;
            if (keyboardSendButton != null) keyboardSendButton.interactable = enabled;
        }

        /// <summary>
        /// 같은 5개 자리를 현재 단계에서 실제로 답할 수 있는 문장으로 바꾼다.
        /// 추천 상품/힌트만으로도 편의점 흐름을 끝까지 진행할 수 있어야 한다.
        /// </summary>
        public void SetDialogueContext(string objectiveId, bool hasItems)
        {
            string[] phrases;
            int recommendedIndex;
            switch (objectiveId)
            {
                case "extras":
                    phrases = BagPhrases;
                    recommendedIndex = 1;
                    SetGuide("다음: 봉투가 필요한지 선택하세요");
                    SetSpeechPractice("‘봉투 주세요’ 또는\n‘봉투 필요 없어요’라고 말해보세요.");
                    break;
                case "checkout":
                    phrases = PaymentPhrases;
                    recommendedIndex = 0;
                    SetGuide("다음: 결제 방법을 선택하세요");
                    SetSpeechPractice("‘카드로 할게요’ 또는\n‘현금으로 할게요’라고 말해보세요.");
                    break;
                case "farewell":
                    phrases = quickPhrases;
                    recommendedIndex = 4;
                    SetGuide("다음: ‘감사합니다’를 눌러 마무리하세요");
                    SetSpeechPractice("마지막으로\n‘감사합니다’라고 말해보세요.");
                    break;
                case "select_items":
                    phrases = quickPhrases;
                    recommendedIndex = hasItems ? 3 : 0;
                    SetGuide(hasItems
                        ? "상품을 더 고르거나 ‘계산할게요’를 누르세요"
                        : "원하는 상품을 먼저 선택하세요");
                    SetSpeechPractice(hasItems
                        ? "더 필요한 상품이 없다면\n‘계산할게요’라고 말해보세요."
                        : "원하는 상품명을 말해보세요.\n예: ‘물 주세요’");
                    break;
                default:
                    phrases = quickPhrases;
                    recommendedIndex = 0;
                    SetGuide("상품을 선택하거나 대화 문장을 눌러보세요");
                    SetSpeechPractice("원하는 상품명을 말해보세요.\n예: ‘물 주세요’");
                    break;
            }

            _stepPhrases = phrases;
            _stepRecommended = ValueAt(phrases, recommendedIndex, ValueAt(quickPhrases, recommendedIndex, string.Empty));
            _browsedCategory = -1;
            ApplyPhrases(phrases, recommendedIndex, ConvenienceHintCatalog.CategoryOf(_stepRecommended));
        }

        // 변경점: 오른쪽 분류 버튼 — 그 분류 문장을 왼쪽 말풍선 맨 앞에 두고 나머지는 현재 단계 문장으로 채운다.
        //   같은 분류를 다시 누르면 단계 기본 문장으로 돌아간다. 추천(강조) 문장 = 그 분류 안에서 단계 추천 문장, 없으면 첫 문장.
        private void SelectHintCategory(int category)
        {
            if (_stepPhrases == null) return;
            if (category == _browsedCategory)
            {
                _browsedCategory = -1;
                ApplyPhrases(_stepPhrases, IndexOf(_stepPhrases, _stepRecommended),
                    ConvenienceHintCatalog.CategoryOf(_stepRecommended));
                return;
            }

            _browsedCategory = category;
            int slots = SlotCount();
            var list = new List<string>(slots);
            foreach (var phrase in ConvenienceHintCatalog.PhrasesOf(category))
                if (list.Count < slots && !Contains(list, phrase)) list.Add(phrase);
            for (int i = 0; i < _stepPhrases.Length && list.Count < slots; i++)
                if (!Contains(list, _stepPhrases[i])) list.Add(_stepPhrases[i]);

            int recommended = 0;
            for (int i = 0; i < list.Count; i++)
                if (ConvenienceHintCatalog.CategoryOf(list[i]) == category && SamePhrase(list[i], _stepRecommended))
                    recommended = i;
            ApplyPhrases(list.ToArray(), recommended, category);
        }

        private void ApplyPhrases(string[] phrases, int recommendedIndex, int highlightedCategory)
        {
            _activeQuickPhrases = phrases;
            RecommendedPhrase = ValueAt(phrases, recommendedIndex, string.Empty);
            SetHighlightedCategory(highlightedCategory);

            bool phrasesChanged = false;
            int slots = SlotCount();
            for (int i = 0; i < slots; i++)
            {
                string phrase = ValueAt(phrases, i, string.Empty);
                bool has = !string.IsNullOrEmpty(phrase);
                if (quickPhraseButtons != null && i < quickPhraseButtons.Length && quickPhraseButtons[i] != null
                    && HasBubbles && quickPhraseButtons[i].gameObject.activeSelf != has)
                    quickPhraseButtons[i].gameObject.SetActive(has);

                var label = quickPhraseLabels != null && i < quickPhraseLabels.Length ? quickPhraseLabels[i] : null;
                if (label != null)
                {
                    if (label.text != phrase)
                    {
                        label.text = phrase;
                        phrasesChanged = true;
                    }
                    if (!HasBubbles)
                    {
                        bool recommended = i == recommendedIndex;
                        label.color = recommended ? RecommendedColor : InkColor;
                        label.fontStyle = recommended ? FontStyles.Bold : FontStyles.Normal;
                    }
                }

                if (HasBubbles && i < quickPhraseActiveBubbles.Length && quickPhraseActiveBubbles[i] != null)
                    quickPhraseActiveBubbles[i].SetActive(i == recommendedIndex);
                SetIcon(i, phrase);
            }
            PlayHintEffects(phrasesChanged, recommendedIndex);
        }

        private void SetIcon(int index, string phrase)
        {
            if (quickPhraseIcons == null || index >= quickPhraseIcons.Length || quickPhraseIcons[index] == null) return;
            string key = ConvenienceHintCatalog.IconOf(phrase)
                         ?? ConvenienceHintCatalog.CategoryIcon(ConvenienceHintCatalog.CategoryOf(phrase));
            var icon = quickPhraseIcons[index];
            if (key != null && _icons != null && _icons.TryGetValue(key, out var sprite) && sprite != null)
            {
                icon.sprite = sprite;
                icon.enabled = true;
            }
            else icon.enabled = false;
        }

        private void SetHighlightedCategory(int category)
        {
            _highlightedCategory = category;
            for (int i = 0; hintCategoryButtons != null && i < hintCategoryButtons.Length; i++)
            {
                var button = hintCategoryButtons[i];
                if (button == null || !(button.targetGraphic is Image image)) continue;
                var sprite = i == category
                    ? (hintCategoryActiveSprites != null && i < hintCategoryActiveSprites.Length ? hintCategoryActiveSprites[i] : null)
                    : (hintCategoryNormalSprites != null && i < hintCategoryNormalSprites.Length ? hintCategoryNormalSprites[i] : null);
                if (sprite != null) image.sprite = sprite;
            }
        }

        // 변경점: 문장이 바뀌면 차례로 스며 나오고, 추천 문장이 바뀌면 그 문장이 한 번 톡 커져 "다음에 할 말"을 알려 준다.
        //   씬 첫 표시(시작 단계)는 그대로 둔다.
        private void PlayHintEffects(bool phrasesChanged, int recommendedIndex)
        {
            bool first = _recommendedIndex < 0;
            bool recommendedChanged = recommendedIndex != _recommendedIndex;
            _recommendedIndex = recommendedIndex;
            if (first || _hintGroups == null || (!phrasesChanged && !recommendedChanged)) return;

            var ct = UIFx.Restart(ref _hintsCts, this);
            ResetHints();   // 앞 연출이 중간에 끊겨 흐리거나 커진 채 남지 않게
            float punchDelay = 0.15f;
            if (phrasesChanged)
            {
                for (int i = 0; i < _hintGroups.Length; i++)
                    UIFx.FadeGroup(_hintGroups[i], 0f, 1f, i * HintStep, 0.25f, ct).Forget();
                punchDelay += _hintGroups.Length * HintStep + 0.1f;
            }
            PunchHint(recommendedIndex, punchDelay, ct);
        }

        private void ResetHints()
        {
            for (int i = 0; i < _hintGroups.Length; i++)
            {
                if (_hintGroups[i] != null) _hintGroups[i].alpha = 1f;
                if (_hintTargets[i] != null)
                    UIFx.SetScaleAround(_hintTargets[i], _hintHomes[i], LabelAnchorLeft, 1f);
            }
        }

        private void PunchHint(int index, float delay, CancellationToken ct)
        {
            if (_hintTargets == null || index < 0 || index >= _hintTargets.Length || _hintTargets[index] == null) return;
            float amount = HasBubbles ? 0.05f : 0.1f;   // 말풍선은 글자보다 커서 조금만 키운다
            UIFx.PunchAround(_hintTargets[index], _hintHomes[index], LabelAnchorLeft, amount, delay, 0.26f, ct).Forget();
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
            int n = SlotCount();
            _hintTargets = new RectTransform[n];
            _hintHomes = new Vector2[n];
            _hintGroups = new CanvasGroup[n];
            for (int i = 0; i < n; i++)
            {
                Component target = HasBubbles && quickPhraseButtons != null && i < quickPhraseButtons.Length
                    ? (Component)quickPhraseButtons[i]
                    : quickPhraseLabels != null && i < quickPhraseLabels.Length ? quickPhraseLabels[i] : null;
                if (target == null) continue;
                _hintTargets[i] = (RectTransform)target.transform;
                _hintHomes[i] = _hintTargets[i].anchoredPosition;
                _hintGroups[i] = UIFx.Group(target);
            }
            if (quickPhraseGuide != null) _guideGroup = UIFx.Group(quickPhraseGuide);
            if (speechPracticeText != null) _practiceGroup = UIFx.Group(speechPracticeText);
        }

        private void CacheIcons()
        {
            _icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            for (int i = 0; iconKeys != null && i < iconKeys.Length; i++)
                if (!string.IsNullOrEmpty(iconKeys[i]) && iconSprites != null && i < iconSprites.Length)
                    _icons[iconKeys[i]] = iconSprites[i];
        }

        private int SlotCount()
        {
            int n = quickPhraseLabels != null ? quickPhraseLabels.Length : 0;
            if (HasBubbles && quickPhraseButtons != null) n = Mathf.Max(n, quickPhraseButtons.Length);
            return n;
        }

        private static void AttachPress(params Button[] buttons)
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

        private void WireCategories() => _categoryCallbacks = Wire(categoryButtons, SelectCategory);
        private void WireProducts() => _productCallbacks = Wire(productButtons, SelectProduct);
        private void WireQuickPhrases() => _quickPhraseCallbacks = Wire(quickPhraseButtons, SelectQuickPhrase);
        private void WireHintCategories() => _hintCategoryCallbacks = Wire(hintCategoryButtons, SelectHintCategory);

        private static UnityAction[] Wire(Button[] buttons, Action<int> handler)
        {
            int count = buttons != null ? buttons.Length : 0;
            var callbacks = new UnityAction[count];
            for (int i = 0; i < count; i++)
            {
                int captured = i;
                callbacks[i] = () => handler(captured);
                if (buttons[i] != null) buttons[i].onClick.AddListener(callbacks[i]);
            }
            return callbacks;
        }

        private static void Unwire(Button[] buttons, UnityAction[] callbacks)
        {
            for (int i = 0; buttons != null && callbacks != null && i < buttons.Length && i < callbacks.Length; i++)
                if (buttons[i] != null && callbacks[i] != null)
                    buttons[i].onClick.RemoveListener(callbacks[i]);
        }

        private static void SetInteractable(Button[] buttons, bool enabled)
        {
            for (int i = 0; buttons != null && i < buttons.Length; i++)
                if (buttons[i] != null) buttons[i].interactable = enabled;
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
            ShowProductContent(visibleCount);
        }

        // 변경점(TrainingCon 시안): 분류 칸 없이 모든 상품을 한 줄로 보여 준다.
        private void ShowAllProducts()
        {
            if (productContent == null) return;
            int count = 0;
            for (int i = 0; productButtons != null && i < productButtons.Length; i++)
            {
                if (productButtons[i] == null) continue;
                productButtons[i].gameObject.SetActive(true);
                count++;
            }
            ShowProductContent(count);
        }

        private void ShowProductContent(int visibleCount)
        {
            if (categoryContent != null) categoryContent.gameObject.SetActive(false);
            productContent.gameObject.SetActive(true);
            productContent.sizeDelta = new Vector2(Mathf.Max(1, visibleCount) * productTilePitch, productContent.sizeDelta.y);
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
            PunchHint(index, 0f, this.GetCancellationTokenOnDestroy());   // 변경점: 누른 문장이 톡 커진다
            OnQuickPhraseSelected?.Invoke(phrase);
        }

        private static string ValueAt(string[] values, int index, string fallback) =>
            values != null && index >= 0 && index < values.Length && !string.IsNullOrWhiteSpace(values[index])
                ? values[index]
                : fallback;

        private static int IndexOf(string[] values, string phrase)
        {
            for (int i = 0; values != null && i < values.Length; i++)
                if (SamePhrase(values[i], phrase)) return i;
            return 0;
        }

        private static bool Contains(List<string> list, string phrase)
        {
            foreach (var item in list)
                if (SamePhrase(item, phrase)) return true;
            return false;
        }

        private static bool SamePhrase(string a, string b) =>
            string.Equals(a?.Trim().TrimEnd('?', '!', '.', '…'), b?.Trim().TrimEnd('?', '!', '.', '…'), StringComparison.Ordinal);

        private void ShowPrevious()
        {
            if (_showingProducts && ShowsCategoryTiles)
            {
                ShowCategories();
                return;
            }
            ScrollBy(-PageStep());
        }

        private void ShowNext() => ScrollBy(PageStep());

        // 화면에 보이는 칸 수만큼 넘긴다. 옛 배치(분류 칸)는 예전 값 그대로.
        private float PageStep()
        {
            if (ShowsCategoryTiles || productScroll == null || productScroll.content == null || productScroll.viewport == null)
                return 0.34f;
            float view = productScroll.viewport.rect.width;
            float hidden = productScroll.content.rect.width - view;
            return hidden <= 1f ? 1f : Mathf.Clamp01(view / hidden);
        }

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

        // 변경점(TrainingCon 시안): 키보드 입력 창. 보낸 문장은 대화 힌트를 누른 것과 같은 한 턴이 된다.
        private void ShowKeyboard()
        {
            if (keyboardPanel == null) return;
            if (keyboardField != null) keyboardField.text = string.Empty;
            UIPopupMotion.Open(keyboardPanel);
            if (keyboardField != null) keyboardField.ActivateInputField();
        }

        private void HideKeyboard()
        {
            if (keyboardField != null) keyboardField.DeactivateInputField();
            UIPopupMotion.Close(keyboardPanel);
        }

        private void OnKeyboardSubmit(string _) => SendKeyboardText();

        private void SendKeyboardText()
        {
            if (keyboardField == null) return;
            if (keyboardSendButton != null && !keyboardSendButton.interactable) return;   // 점원이 대답하는 중
            string text = keyboardField.text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            HideKeyboard();
            OnTypedUtterance?.Invoke(text);
        }
    }
}
