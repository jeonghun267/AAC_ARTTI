using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 레포트 목록의 단일 세션 카드. ReportView가 동적 생성 후 값 주입.
    public class SessionCardView : MonoBehaviour
    {
        public Image statusBg;
        public TMP_Text statusText;
        public TMP_Text nameText;
        public TMP_Text dateText;
        public Button selectButton;
    }
}
