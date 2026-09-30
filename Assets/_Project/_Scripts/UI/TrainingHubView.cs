using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
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
            PlayEntrance();
        }

        // 변경점: 화면 등장 — 모드 카드 두 장이 차례로 아래에서 떠오르고, 아래 안내 바가 뒤따른다.
        //   카드 루트는 위치·알파만 움직인다(누름 반응은 HomeButtonGlow가 안쪽 Visual 크기로 따로 한다).
        private void PlayEntrance()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            RiseIn(accuracyBtn != null ? accuracyBtn.transform : null, 0.1f, ct);
            RiseIn(conversationBtn != null ? conversationBtn.transform : null, 0.2f, ct);
            var stage = accuracyBtn != null ? accuracyBtn.transform.parent : null;
            RiseIn(stage != null ? stage.Find("TipBar") : null, 0.35f, ct);
        }

        private static void RiseIn(Transform t, float delay, System.Threading.CancellationToken ct)
        {
            if (t is RectTransform rt) UIFx.MoveFadeIn(rt, rt.anchoredPosition, new Vector2(0f, -40f), delay, 0.45f, ct).Forget();
        }

        private static void StartTraining(TrainingMode mode)
        {
            TrainingModeContext.Mode = mode;
            SceneFader.Load("TrainingConvenienceScene");   // 변경점: 페이드 전환
        }
    }
}
