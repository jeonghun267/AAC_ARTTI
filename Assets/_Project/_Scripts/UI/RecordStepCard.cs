using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 연습 상세 기록 타임라인의 단계 카드. 빌더가 와이어링하고 RecordDetailView가 값을 채운다.
    public class RecordStepCard : MonoBehaviour
    {
        public TMP_Text numberText;     // 01, 02 ...
        public TMP_Text objectiveText;  // 인사하기 등
        public TMP_Text userText;       // 사용자 발화
        public TMP_Text timeText;       // 12:01
        public TMP_Text ratingText;     // 잘했어요😊
        public Button speakerButton;    // TTS 재생
    }
}
