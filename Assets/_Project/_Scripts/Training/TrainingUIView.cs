using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Artti.AAC;
using System;

namespace Artti.Training
{
    public class TrainingUIView : MonoBehaviour
    {
        [SerializeField] private AACCardButton cardSlot1;
        [SerializeField] private AACCardButton cardSlot2;
        [SerializeField] private TMP_Text npcDialoguePanel;

        [Header("Pharmacy 4-card pool (스크롤 리스트)")]
        [SerializeField] private AACCardButton[] pharmacyCardSlots;

        [Header("Pharmacy 기타 모달")]
        [SerializeField] private Button extraButton;          // 하단 "기타" 버튼
        [SerializeField] private GameObject extraModal;       // 풀스크린 오버레이
        [SerializeField] private AACCardButton[] extraCardSlots;
        [SerializeField] private Button extraCloseButton;

        [Header("STT Overlay")]
        [SerializeField] private GameObject sttOverlay;       // 풀스크린 반투명 + 가운데 카드
        [SerializeField] private TMP_Text sttStatusText;      // "듣고 있어요..." / "이렇게 들렸어요" / "잘 못 들었어요"
        [SerializeField] private TMP_Text sttResultText;      // 인식된 텍스트
        [SerializeField] private RectTransform sttPulseCircle;// 펄스 애니메이션 대상

        [Header("Legacy mic indicator (옵션)")]
        [SerializeField] private GameObject micIndicator;
        [SerializeField] private Image micFillImage;

        public event Action<AACCard> OnCardTapped;
        public event Action OnExtraRequested;

        private bool _isListening;
        private float _pulseTime;
        private Coroutine _hideRoutine;

        private void OnEnable()
        {
            if (cardSlot1 != null) cardSlot1.OnCardSelected += HandleCardSelected;
            if (cardSlot2 != null) cardSlot2.OnCardSelected += HandleCardSelected;
            if (pharmacyCardSlots != null)
                foreach (var s in pharmacyCardSlots)
                    if (s != null) s.OnCardSelected += HandleCardSelected;
            if (extraCardSlots != null)
                foreach (var s in extraCardSlots)
                    if (s != null) s.OnCardSelected += HandleCardSelected;
            if (extraButton != null) extraButton.onClick.AddListener(HandleExtraRequested);
            if (extraCloseButton != null) extraCloseButton.onClick.AddListener(HideExtraModal);
        }

        private void OnDisable()
        {
            if (cardSlot1 != null) cardSlot1.OnCardSelected -= HandleCardSelected;
            if (cardSlot2 != null) cardSlot2.OnCardSelected -= HandleCardSelected;
            if (pharmacyCardSlots != null)
                foreach (var s in pharmacyCardSlots)
                    if (s != null) s.OnCardSelected -= HandleCardSelected;
            if (extraCardSlots != null)
                foreach (var s in extraCardSlots)
                    if (s != null) s.OnCardSelected -= HandleCardSelected;
            if (extraButton != null) extraButton.onClick.RemoveListener(HandleExtraRequested);
            if (extraCloseButton != null) extraCloseButton.onClick.RemoveListener(HideExtraModal);
        }

        private void HandleExtraRequested() => OnExtraRequested?.Invoke();

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
            if (extraModal != null) extraModal.SetActive(true);
        }

        public void HideExtraModal()
        {
            if (extraModal != null) extraModal.SetActive(false);
        }

        public bool HasExtraPool => extraCardSlots != null && extraCardSlots.Length > 0;

        private void Update()
        {
            if (!_isListening || sttPulseCircle == null) return;
            _pulseTime += Time.deltaTime;
            float s = 1f + 0.18f * Mathf.Sin(_pulseTime * 5.5f);
            sttPulseCircle.localScale = new Vector3(s, s, 1f);
        }

        private void HandleCardSelected(AACCard card) => OnCardTapped?.Invoke(card);

        public void SetNPCDialogue(string text)
        {
            if (npcDialoguePanel != null) npcDialoguePanel.text = text;
        }

        public void SetCards(AACCard card1, AACCard card2)
        {
            if (cardSlot1 != null) cardSlot1.SetCard(card1);
            if (cardSlot2 != null) cardSlot2.SetCard(card2);
        }

        // 약국 풀 갱신 — pharmacyCardSlots 배열에 순서대로 N장 채우고 남는 슬롯은 비활성
        public void SetCardList(System.Collections.Generic.IList<AACCard> cards)
        {
            if (pharmacyCardSlots == null || pharmacyCardSlots.Length == 0) return;
            for (int i = 0; i < pharmacyCardSlots.Length; i++)
            {
                var slot = pharmacyCardSlots[i];
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

        public bool HasPharmacyCardPool => pharmacyCardSlots != null && pharmacyCardSlots.Length > 0;

        // 마이크 켜진 동안 호출 — 풀스크린 오버레이 ON, 펄스 애니메이션 시작
        public void ShowMicIndicator(bool visible)
        {
            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }

            _isListening = visible;
            _pulseTime = 0f;

            if (sttOverlay != null) sttOverlay.SetActive(visible);
            if (sttStatusText != null) sttStatusText.text = "듣고 있어요...";
            if (sttResultText != null) sttResultText.text = "";
            if (sttPulseCircle != null) sttPulseCircle.localScale = Vector3.one;

            // legacy (있으면 같이 토글)
            if (micIndicator != null) micIndicator.SetActive(visible);
        }

        // STT 결과 — 잠시 표시 후 자동 숨김
        public void ShowSttResult(string text, float displaySeconds = 2.0f)
        {
            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }

            _isListening = false;
            if (sttPulseCircle != null) sttPulseCircle.localScale = Vector3.one;

            if (sttOverlay != null) sttOverlay.SetActive(true);
            bool empty = string.IsNullOrWhiteSpace(text);
            if (sttStatusText != null) sttStatusText.text = empty ? "잘 못 들었어요" : "이렇게 들렸어요";
            if (sttResultText != null) sttResultText.text = empty ? "" : $"“{text}”";

            _hideRoutine = StartCoroutine(HideAfter(displaySeconds));
        }

        public void HideSttOverlay()
        {
            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
            _isListening = false;
            if (sttOverlay != null) sttOverlay.SetActive(false);
            if (micIndicator != null) micIndicator.SetActive(false);
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
