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
        public TMP_Text modeText;         // 가장 많이 사용한 모드
        public Image avatarImage;
        public Image starBadge;           // 아바타 좌상단 별 배지 (장식)
        public GameObject defaultPill;    // "기본 프로필" 칩 (활성 프로필에만)
        public GameObject selectedBorder; // primary 굵은 테두리
        public GameObject checkMark;      // primary 체크 (좌측 상단)
        public Button selectButton;       // 카드 본문 선택
        public Button editButton;         // 수정
        public Button deleteButton;       // 삭제

        public void SetSelected(bool on)
        {
            if (selectedBorder != null) selectedBorder.SetActive(on);
            if (checkMark != null) checkMark.SetActive(on);
            if (defaultPill != null) defaultPill.SetActive(on);
        }
    }
}
