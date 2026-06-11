using TMPro;
using UnityEngine;

namespace Artti.UI
{
    // 학습 상세 '진행 흐름'의 단일 단계 행. ReportView가 동적 생성 후 값 주입.
    public class ReportStepView : MonoBehaviour
    {
        public TMP_Text objectiveText;
        public GameObject retryBadge;
        public TMP_Text retryText;
        public RectTransform bubbleContainer;
    }
}
