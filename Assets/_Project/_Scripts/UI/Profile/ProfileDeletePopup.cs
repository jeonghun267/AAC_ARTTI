using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 프로필 삭제 확인 팝업 (가운데 모달, 52 판에 제목/안내/입력칸이 그려져 있음). '동의한다'를 정확히 입력해야 삭제를 알린다.
    // 변경점: 사용자 선택 화면(ProfileSelectView)에 있던 로직을 프로필 설정 화면에서도 쓰도록 컴포넌트로 뺐다.
    // 팝업 루트에 붙고 루트는 평소 꺼져 있어, 버튼 연결은 처음 열 때 한다.
    public class ProfileDeletePopup : MonoBehaviour
    {
        [SerializeField] private TMP_InputField input;
        [SerializeField] private GameObject fieldCover;   // 52 입력칸 속 '동의한다' 안내 글자를 가리는 조각
        [SerializeField] private Button confirmButton;    // 삭제
        [SerializeField] private Button cancelButton;     // 취소
        [SerializeField] private Button closeButton;      // X

        private const string ConfirmWord = "동의한다";
        private const string WrongHint = "'동의한다'를 정확히 입력해 주세요";

        private Action _onConfirmed;
        private bool _hintShown;
        private bool _wired;

        public void Open(Action onConfirmed)
        {
            Wire();
            _onConfirmed = onConfirmed;
            if (input != null) input.text = "";
            SetHint(false);
            UIPopupMotion.Open(gameObject);
        }

        public void Close()
        {
            _onConfirmed = null;
            UIPopupMotion.Close(gameObject);
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (input != null) input.onValueChanged.AddListener(_ => UpdateFieldCover());
        }

        private void OnConfirm()
        {
            var typed = input != null ? input.text.Trim() : "";
            if (typed != ConfirmWord)
            {
                // 칸을 비우고 칸 안에 안내를 띄운다(다시 치기 시작하면 안내는 사라진다)
                if (input != null) input.text = "";
                SetHint(true);
                return;
            }
            var done = _onConfirmed;
            Close();
            done?.Invoke();
        }

        // 틀렸다는 안내는 입력칸의 placeholder로 띄운다(칸이 비어 있을 때만 보임).
        private void SetHint(bool on)
        {
            _hintShown = on;
            if (input != null && input.placeholder is TMP_Text ph) ph.text = on ? WrongHint : "";
            UpdateFieldCover();
        }

        // 52에 그려진 '동의한다' 안내 글자는 글자를 치는 중이거나 틀렸다는 안내를 띄울 때 가린다.
        private void UpdateFieldCover()
        {
            if (fieldCover == null) return;
            bool typing = input != null && !string.IsNullOrEmpty(input.text);
            fieldCover.SetActive(typing || _hintShown);
        }
    }
}
