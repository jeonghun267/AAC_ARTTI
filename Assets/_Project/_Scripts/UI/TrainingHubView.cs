using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Artti.Common;

namespace Artti.UI
{
    // 말하기 훈련 허브. 편의점 카드 2장(정확도 모드 / 대화 모드) 중 하나를 누르면
    // 선택한 모드를 TrainingModeContext에 저장하고 편의점 훈련 씬으로 이동한다.
    public class TrainingHubView : MonoBehaviour
    {
        // 기존 convenienceBtn 와이어링은 빌더 재실행 전까지 정확도 모드 버튼으로 유지된다.
        [FormerlySerializedAs("convenienceBtn")]
        [SerializeField] private Button accuracyBtn;
        [SerializeField] private Button conversationBtn;

        private void Start()
        {
            if (accuracyBtn != null)
                accuracyBtn.onClick.AddListener(() => StartTraining(TrainingMode.Accuracy));
            if (conversationBtn != null)
                conversationBtn.onClick.AddListener(() => StartTraining(TrainingMode.Conversation));
        }

        private static void StartTraining(TrainingMode mode)
        {
            TrainingModeContext.Mode = mode;
            SceneManager.LoadScene("TrainingConvenienceScene");
        }
    }
}
