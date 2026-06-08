using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 사용자 카드 한 장. 빌더가 만든 프리팹의 컴포넌트.
    public class ProfileCardView : MonoBehaviour
    {
        public TMP_Text nameText;
        public TMP_Text dateText;
        public Image avatarImage;
        public GameObject selectedBorder; // primary 굵은 테두리
        public GameObject checkMark;      // primary 체크 (좌측 상단)
        public Button selectButton;       // 카드 본문 선택
        public Button editButton;         // 수정
        public Button reportButton;       // 리포트

        public void SetSelected(bool on)
        {
            if (selectedBorder != null) selectedBorder.SetActive(on);
            if (checkMark != null) checkMark.SetActive(on);
        }
    }
}
