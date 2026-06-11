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

        [Header("STT inline (NPC 패널 외곽 진행 테두리)")]
        [SerializeField] private GameObject npcBorderRoot;    // 4 edge 부모 — 듣는 동안만 SetActive
        [SerializeField] private Image npcBorderTop;          // Filled Horizontal, Origin Left
        [SerializeField] private Image npcBorderRight;        // Filled Vertical,   Origin Top
        [SerializeField] private Image npcBorderBottom;       // Filled Horizontal, Origin Right
        [SerializeField] private Image npcBorderLeft;         // Filled Vertical,   Origin Bottom
        [SerializeField] private float npcBorderCycleSeconds = 1.6f; // 한 바퀴 시간

        [Header("STT Overlay (legacy — 약국씬 호환용. 신규 씬은 비워두면 자동 무시)")]
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
        private float _borderTime;
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
            if (!_isListening) return;

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

            // 레거시: 풀스크린 펄스 (약국씬 등에 와이어링돼 있을 때만)
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

        // STT 수집 중: 모든 카드 잠금, 선택 카드 외에는 은은히 흐림 (편의점 시안 3.png)
        public void SetPoolLocked(AACCard selected)
        {
            if (pharmacyCardSlots == null) return;
            foreach (var slot in pharmacyCardSlots)
                if (slot != null && slot.gameObject.activeSelf)
                    slot.SetLocked(true, dim: slot.Card != selected);
        }

        public void UnlockPool()
        {
            if (pharmacyCardSlots == null) return;
            foreach (var slot in pharmacyCardSlots)
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
            if (visible && npcDialoguePanel != null) npcDialoguePanel.text = "듣고 있어요...";

            // 레거시: 풀스크린 오버레이 (약국씬 등에 와이어링됐을 때만)
            if (sttOverlay != null) sttOverlay.SetActive(visible);
            if (sttStatusText != null) sttStatusText.text = "듣고 있어요...";
            if (sttResultText != null) sttResultText.text = "";
            if (sttPulseCircle != null) sttPulseCircle.localScale = Vector3.one;

            if (micIndicator != null) micIndicator.SetActive(visible);
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
        }

        public void HideSttOverlay()
        {
            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
            _isListening = false;
            if (npcBorderRoot != null) npcBorderRoot.SetActive(false);
            ResetBorderFill();
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
