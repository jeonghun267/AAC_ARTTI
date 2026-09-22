using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 최근 연습 기록 한 줄. 빌더가 텍스트/아이콘/버튼을 와이어링하고, ReportView가 값을 채운다.
    public class ReportRecordRow : MonoBehaviour
    {
        public Image iconImage;       // 시나리오 아이콘 (좌측 보라 박스 안)
        public TMP_Text nameText;     // 시나리오 이름
        public TMP_Text subText;      // 한 줄 코멘트
        public TMP_Text statusText;   // 완료 / 학습중 (초록 칩 안)
        public TMP_Text accuracyText; // 정답률 "90%"
        public TMP_Text dateText;     // "MM.dd"
        public TMP_Text pointsText;   // (레거시) 포인트. 새 레이아웃에선 미사용
        public Button button;
    }
}
