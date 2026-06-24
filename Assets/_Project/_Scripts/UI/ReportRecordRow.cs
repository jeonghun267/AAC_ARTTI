using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 최근 학습 기록 한 줄. 빌더가 텍스트/버튼을 와이어링하고, ReportView가 값을 채운다.
    public class ReportRecordRow : MonoBehaviour
    {
        public TMP_Text nameText;
        public TMP_Text dateText;
        public TMP_Text pointsText;
        public Button button;
    }
}
