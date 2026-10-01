using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using Artti.AAC;
using Artti.UI;
using System;

namespace Artti.Training
{
    public class TrainingUIView : MonoBehaviour
    {
        [SerializeField] private AACCardButton cardSlot1;
        [SerializeField] private AACCardButton cardSlot2;
        [SerializeField] private TMP_Text npcDialoguePanel;

        [Header("4-card pool (스크롤 리스트)")]
        [FormerlySerializedAs("pharmacyCardSlots")]
        [SerializeField] private AACCardButton[] cardPoolSlots;

        [Header("기타 모달")]
        [SerializeField] private Button extraButton;          // 하단 "기타" 버튼
        [SerializeField] private Button freeTalkButton;        // 하단 "대화하기" 버튼 (자유 대화 토글)
        [SerializeField] private TMP_Text freeTalkLabel;       // 토글 상태에 따라 문구 교체
        [SerializeField] private GameObject extraModal;       // 풀스크린 오버레이
        [SerializeField] private AACCardButton[] extraCardSlots;
        [SerializeField] private Button extraCloseButton;

        [Header("STT inline (NPC 패널 외곽 진행 테두리)")]
        [SerializeField] private GameObject npcBorderRoot;    // 4 edge 부모 — 듣는 동안만 SetActive
        [SerializeField] private Image npcBorderTop;          // Filled Horizontal, Origin Left
        [SerializeField] private Image npcBorderRight;        // Filled Vertical,   Origin Top
        [SerializeField] private Image npcBorderBottom;       // Filled Horizontal, Origin Right
        [SerializeField] private Image npcBorderLeft;         // Filled Vertical,   Origin Bottom
        [SerializeField] private float npcBorderCycleSeconds = 1.6f; // 한 바퀴 시간

        [Header("STT Overlay (legacy — 비워두면 자동 무시)")]
        [SerializeField] private GameObject sttOverlay;       // 풀스크린 반투명 + 가운데 카드
        [SerializeField] private TMP_Text sttStatusText;      // "듣고 있어요..." / "이렇게 들렸어요" / "잘 못 들었어요"
        [SerializeField] private TMP_Text sttResultText;      // 인식된 텍스트
        [SerializeField] private RectTransform sttPulseCircle;// 펄스 애니메이션 대상

        // 변경점: 점원 말풍선이 톡 커질 때 기준점(말풍선 사각형 안 0~1, 꼬리 끝). 말풍선 그림을 바꾸면 함께 바꾼다.
        [Header("점원 말풍선 꼬리 위치")]
        [SerializeField] private Vector2 bubbleTailPoint = new Vector2(0.11f, 0f);

        [Header("Legacy mic indicator (옵션)")]
        [SerializeField] private GameObject micIndicator;
        [SerializeField] private Image micFillImage;

        public event Action<AACCard> OnCardTapped;
        public event Action OnExtraRequested;
        public event Action OnFreeTalkToggled;

        private bool _isListening;
        private float _pulseTime;
        private float _borderTime;
        private Coroutine _hideRoutine;

        // 변경점: 화면 효과 (씬 수정 없이 기존 연결만 사용)
        // - 점원 말풍선: 새 대사마다 꼬리(왼쪽 아래) 쪽에서 톡 커지고 글자가 스며 나온다
        // - 대답을 기다리는 동안 말풍선에 "생각하는 중..." (점 1~3개 반복)
        // - 듣는 중 표시: 톡 나타나고, 빨간 점이 천천히 숨 쉬듯 깜빡인다(초당 1회 미만, 번쩍임 없음)
        static readonly string[] ThinkingTexts = { "생각하는 중.", "생각하는 중..", "생각하는 중..." };
        const float ThinkingStepsPerSecond = 2.5f;
        const float LiveDotHz = 0.8f;
        const float LiveDotMinAlpha = 0.35f;

        private RectTransform _bubble;
        private Vector2 _bubbleHome;
        private CanvasGroup _npcTextGroup;
        private CancellationTokenSource _bubbleCts;
        private string _npcLine;
        private bool _thinking;
        private float _thinkingTime;
        private int _thinkingStep = -1;
        private Graphic _liveDot;
        private float _liveDotTime;

        private void Awake()
        {
            if (npcDialoguePanel != null)
            {
                _npcTextGroup = UIFx.Group(npcDialoguePanel);
                // 말풍선 = 대사 글자의 부모. 글자가 캔버스 바로 아래에 있는 옛 배치면 말풍선 연출은 건너뛴다.
                var parent = npcDialoguePanel.rectTransform.parent as RectTransform;
                if (parent != null && !parent.TryGetComponent<Canvas>(out _))
                {
                    _bubble = parent;
                    _bubbleHome = parent.anchoredPosition;
                }
            }
            if (micIndicator != null)
            {
                var dot = micIndicator.transform.Find("LiveDot");
                if (dot != null) _liveDot = dot.GetComponent<Graphic>();
            }
            if (freeTalkButton != null) UIPressScale.Attach(freeTalkButton);
        }

        private void OnEnable()
        {
            if (cardSlot1 != null) cardSlot1.OnCardSelected += HandleCardSelected;
            if (cardSlot2 != null) cardSlot2.OnCardSelected += HandleCardSelected;
            if (cardPoolSlots != null)
                foreach (var s in cardPoolSlots)
                    if (s != null) s.OnCardSelected += HandleCardSelected;
            if (extraCardSlots != null)
                foreach (var s in extraCardSlots)
                    if (s != null) s.OnCardSelected += HandleCardSelected;
            if (extraButton != null) extraButton.onClick.AddListener(HandleExtraRequested);
            if (freeTalkButton != null) freeTalkButton.onClick.AddListener(HandleFreeTalkToggled);
            if (extraCloseButton != null) extraCloseButton.onClick.AddListener(HideExtraModal);
        }

        private void OnDisable()
        {
            if (cardSlot1 != null) cardSlot1.OnCardSelected -= HandleCardSelected;
            if (cardSlot2 != null) cardSlot2.OnCardSelected -= HandleCardSelected;
            if (cardPoolSlots != null)
                foreach (var s in cardPoolSlots)
                    if (s != null) s.OnCardSelected -= HandleCardSelected;
            if (extraCardSlots != null)
                foreach (var s in extraCardSlots)
                    if (s != null) s.OnCardSelected -= HandleCardSelected;
            if (extraButton != null) extraButton.onClick.RemoveListener(HandleExtraRequested);
            if (freeTalkButton != null) freeTalkButton.onClick.RemoveListener(HandleFreeTalkToggled);
            if (extraCloseButton != null) extraCloseButton.onClick.RemoveListener(HideExtraModal);
        }

        private void HandleExtraRequested() => OnExtraRequested?.Invoke();
        private void HandleFreeTalkToggled() => OnFreeTalkToggled?.Invoke();

        // 대화하기 모드 표시 — 버튼 문구를 바꾸고, 카드 풀은 잠가서 자유 발화에 집중하게 한다.
        // 잠금 연출은 기존 SetPoolLocked/UnlockPool을 그대로 재사용한다.
        public void SetFreeTalkActive(bool active)
        {
            if (freeTalkLabel != null) freeTalkLabel.text = active ? "대화 끝내기" : "대화하기";
            if (extraButton != null) extraButton.interactable = !active;
            if (active) SetPoolLocked(null);
            else UnlockPool();
        }

        public void ShowExtraModal(System.Collections.Generic.IList<AACCard> cards)
        {
            if (extraCardSlots != null)
            {
                for (int i = 0; i < extraCardSlots.Length; i++)
                {
                    var slot = extraCardSlots[i];
                    if (slot == null) continue;
                    if (cards != null && i < cards.Count)
                    {
                        slot.gameObject.SetActive(true);
                        slot.SetCard(cards[i]);
                    }
                    else
                    {
                        slot.gameObject.SetActive(false);
                    }
                }
            }
            UIPopupMotion.Open(extraModal);   // 변경점: 팝업 열기/닫기 연출
        }

        public void HideExtraModal()
        {
            UIPopupMotion.Close(extraModal);
        }

        public bool HasExtraPool => extraCardSlots != null && extraCardSlots.Length > 0;

        private void Update()
        {
            if (_thinking) AnimateThinking();
            if (!_isListening) return;

            // 변경점: 듣는 중 빨간 점 — 천천히 숨 쉬듯 밝아졌다 어두워진다
            if (_liveDot != null)
            {
                _liveDotTime += Time.deltaTime;
                float wave = 0.5f + 0.5f * Mathf.Cos(_liveDotTime * LiveDotHz * 2f * Mathf.PI);
                _liveDot.canvasRenderer.SetAlpha(Mathf.Lerp(LiveDotMinAlpha, 1f, wave));
            }

            // 신규: NPC 패널 외곽 테두리 진행 — 시계방향으로 top→right→bottom→left 순차 채움, 한 바퀴마다 리셋
            if (npcBorderRoot != null && npcBorderRoot.activeInHierarchy)
            {
                _borderTime += Time.deltaTime;
                float cycle = Mathf.Max(0.1f, npcBorderCycleSeconds);
                float t = (_borderTime % cycle) / cycle; // 0..1

                // 각 변은 0.25 구간씩 0→1로 채움. 이전 변은 1 유지. 한 바퀴 끝나면 다음 cycle에서 모두 0부터.
                float top, right, bottom, left;
                if (t < 0.25f) { top = t / 0.25f;          right = 0f; bottom = 0f; left = 0f; }
                else if (t < 0.5f) { top = 1f; right = (t - 0.25f) / 0.25f; bottom = 0f; left = 0f; }
                else if (t < 0.75f) { top = 1f; right = 1f; bottom = (t - 0.5f) / 0.25f; left = 0f; }
                else { top = 1f; right = 1f; bottom = 1f; left = (t - 0.75f) / 0.25f; }

                if (npcBorderTop != null)    npcBorderTop.fillAmount    = top;
                if (npcBorderRight != null)  npcBorderRight.fillAmount  = right;
                if (npcBorderBottom != null) npcBorderBottom.fillAmount = bottom;
                if (npcBorderLeft != null)   npcBorderLeft.fillAmount   = left;
            }

            // 레거시: 풀스크린 펄스 (와이어링돼 있을 때만)
            if (sttPulseCircle != null)
            {
                _pulseTime += Time.deltaTime;
                float s = 1f + 0.18f * Mathf.Sin(_pulseTime * 5.5f);
                sttPulseCircle.localScale = new Vector3(s, s, 1f);
            }
        }

        private void ResetBorderFill()
        {
            if (npcBorderTop != null)    npcBorderTop.fillAmount    = 0f;
            if (npcBorderRight != null)  npcBorderRight.fillAmount  = 0f;
            if (npcBorderBottom != null) npcBorderBottom.fillAmount = 0f;
            if (npcBorderLeft != null)   npcBorderLeft.fillAmount   = 0f;
        }

        private void HandleCardSelected(AACCard card) => OnCardTapped?.Invoke(card);

        public void SetNPCDialogue(string text)
        {
            _npcLine = text;
            _thinking = false;
            if (npcDialoguePanel == null) return;
            npcDialoguePanel.text = text;
            PlayBubblePop();
        }

        // 변경점: 점원이 대답을 준비하는 동안(LLM 대기) 말풍선에 "생각하는 중..."을 띄운다.
        //   끄면 마지막 대사로 되돌린다(보통 같은 프레임에 새 대사가 바로 덮는다).
        public void ShowNpcThinking(bool on)
        {
            if (npcDialoguePanel == null || _thinking == on) return;
            _thinking = on;
            _thinkingTime = 0f;
            _thinkingStep = -1;
            if (on)
            {
                AnimateThinking();
                FadeInNpcText();
            }
            else if (_npcLine != null) npcDialoguePanel.text = _npcLine;
        }

        private void AnimateThinking()
        {
            _thinkingTime += Time.deltaTime;
            int step = Mathf.FloorToInt(_thinkingTime * ThinkingStepsPerSecond) % ThinkingTexts.Length;
            if (step == _thinkingStep) return;   // 글자는 바뀔 때만 쓴다(매 프레임 메시 재생성 방지)
            _thinkingStep = step;
            npcDialoguePanel.text = ThinkingTexts[step];
        }

        // 새 대사: 말풍선이 꼬리 쪽에서 톡 커지고 글자가 스며 나온다
        private void PlayBubblePop()
        {
            var ct = UIFx.Restart(ref _bubbleCts, this);
            if (_bubble != null) UIFx.BounceAround(_bubble, _bubbleHome, bubbleTailPoint, 0f, 0.3f, 0.92f, ct).Forget();
            UIFx.FadeGroup(_npcTextGroup, 0f, 1f, 0.04f, 0.22f, ct).Forget();
        }

        private void FadeInNpcText()
        {
            var ct = UIFx.Restart(ref _bubbleCts, this);
            if (_bubble != null) UIFx.SetScaleAround(_bubble, _bubbleHome, bubbleTailPoint, 1f);
            UIFx.FadeGroup(_npcTextGroup, 0f, 1f, 0f, 0.2f, ct).Forget();
        }

        // 듣는 중 표시(작은 알림 배지): 톡 나타나고 사라질 때는 살짝 줄며 흐려진다
        private void SetMicIndicatorVisible(bool visible)
        {
            if (micIndicator == null) return;
            if (visible) UIPopupMotion.Open(micIndicator, (RectTransform)micIndicator.transform);
            else UIPopupMotion.Close(micIndicator);
            if (!visible && _liveDot != null)
            {
                _liveDotTime = 0f;
                _liveDot.canvasRenderer.SetAlpha(1f);
            }
        }

        public void SetCards(AACCard card1, AACCard card2)
        {
            if (cardSlot1 != null) cardSlot1.SetCard(card1);
            if (cardSlot2 != null) cardSlot2.SetCard(card2);
        }

        // 카드 풀 갱신 — cardPoolSlots 배열에 순서대로 N장 채우고 남는 슬롯은 비활성
        public void SetCardList(System.Collections.Generic.IList<AACCard> cards)
        {
            if (cardPoolSlots == null || cardPoolSlots.Length == 0) return;
            for (int i = 0; i < cardPoolSlots.Length; i++)
            {
                var slot = cardPoolSlots[i];
                if (slot == null) continue;
                if (cards != null && i < cards.Count)
                {
                    slot.gameObject.SetActive(true);
                    slot.SetCard(cards[i]);
                }
                else
                {
                    slot.gameObject.SetActive(false);
                }
            }
        }

        public bool HasCardPool => cardPoolSlots != null && cardPoolSlots.Length > 0;

        // STT 수집 중: 모든 카드 잠금, 선택 카드 외에는 은은히 흐림 (편의점 시안 3.png)
        public void SetPoolLocked(AACCard selected)
        {
            if (cardPoolSlots == null) return;
            foreach (var slot in cardPoolSlots)
                if (slot != null && slot.gameObject.activeSelf)
                    slot.SetLocked(true, dim: slot.Card != selected);
        }

        public void UnlockPool()
        {
            if (cardPoolSlots == null) return;
            foreach (var slot in cardPoolSlots)
                if (slot != null && slot.gameObject.activeSelf)
                    slot.SetLocked(false, dim: false);
        }

        // 마이크 켜진 동안 호출 — NPC 패널 외곽 진행 테두리 ON + 텍스트 갱신
        public void ShowMicIndicator(bool visible)
        {
            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }

            _isListening = visible;
            _pulseTime = 0f;
            _borderTime = 0f;

            // 신규: 외곽 테두리 진행
            if (npcBorderRoot != null) npcBorderRoot.SetActive(visible);
            if (!visible) ResetBorderFill();
            if (visible && npcDialoguePanel != null)
            {
                _thinking = false;   // 변경점: 생각 표시가 "듣고 있어요"를 덮어쓰지 않게
                npcDialoguePanel.text = "듣고 있어요...";
                FadeInNpcText();
            }

            // 레거시: 풀스크린 오버레이 (와이어링됐을 때만)
            if (sttOverlay != null) sttOverlay.SetActive(visible);
            if (sttStatusText != null) sttStatusText.text = "듣고 있어요...";
            if (sttResultText != null) sttResultText.text = "";
            if (sttPulseCircle != null) sttPulseCircle.localScale = Vector3.one;

            SetMicIndicatorVisible(visible);
        }

        // STT 결과 — 빈 결과면 NPC 텍스트로 "잘 못 들었어요" 잠깐 표시. 정상 결과면 다음 NPC 대사가 곧 덮음.
        public void ShowSttResult(string text, float displaySeconds = 2.0f)
        {
            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }

            _isListening = false;

            // 신규: 테두리 OFF + 빈 결과만 NPC 텍스트로 안내
            if (npcBorderRoot != null) npcBorderRoot.SetActive(false);
            ResetBorderFill();
            if (string.IsNullOrWhiteSpace(text) && npcBorderRoot != null && npcDialoguePanel != null)
            {
                _thinking = false;
                npcDialoguePanel.text = "잘 못 들었어요";
            }

            // 레거시: 풀스크린 오버레이 (sttOverlay 와이어링됐을 때만)
            if (sttPulseCircle != null) sttPulseCircle.localScale = Vector3.one;
            if (sttOverlay != null)
            {
                sttOverlay.SetActive(true);
                bool empty = string.IsNullOrWhiteSpace(text);
                if (sttStatusText != null) sttStatusText.text = empty ? "잘 못 들었어요" : "이렇게 들렸어요";
                if (sttResultText != null) sttResultText.text = empty ? "" : $"“{text}”";
                _hideRoutine = StartCoroutine(HideAfter(displaySeconds));
            }
            SetMicIndicatorVisible(false);
        }

        public void HideSttOverlay()
        {
            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
            _isListening = false;
            if (npcBorderRoot != null) npcBorderRoot.SetActive(false);
            ResetBorderFill();
            if (sttOverlay != null) sttOverlay.SetActive(false);
            SetMicIndicatorVisible(false);
        }

        public void UpdateMicVolume(float level)
        {
            if (micFillImage != null) micFillImage.fillAmount = level;
        }

        private IEnumerator HideAfter(float sec)
        {
            yield return new WaitForSeconds(sec);
            if (sttOverlay != null) sttOverlay.SetActive(false);
            _hideRoutine = null;
        }
    }
}
