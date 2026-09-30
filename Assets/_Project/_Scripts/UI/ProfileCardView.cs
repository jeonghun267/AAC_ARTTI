using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 사용자 카드 한 장(세로형 다크 네온). 빌더가 만든 프리팹의 컴포넌트.
    // 선택되면 테두리 빛 + 우상단 체크 + 삭제 칩이 켜지고 살짝 커진다.
    public class ProfileCardView : MonoBehaviour
    {
        const float SelectedScale = 1.08f;

        public TMP_Text nameText;
        public TMP_Text introText;        // 한마디 소개(없으면 마지막 사용일)
        public Image avatarImage;
        public GameObject selectedBorder; // 선택 테두리 빛
        public GameObject checkMark;      // 우상단 체크
        public Button selectButton;       // 카드 전체 = 선택
        public Button deleteButton;       // 삭제 칩 (선택된 카드에만)

        public void SetSelected(bool on)
        {
            if (selectedBorder != null) selectedBorder.SetActive(on);
            if (checkMark != null) checkMark.SetActive(on);
            if (deleteButton != null) deleteButton.gameObject.SetActive(on);
            transform.localScale = Vector3.one * (on ? SelectedScale : 1f);
        }
    }
}
