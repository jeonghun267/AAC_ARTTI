using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 사용자 카드 한 장(세로형 다크 네온). 빌더가 만든 프리팹의 컴포넌트.
    // 선택되면 테두리 빛 + 우상단 체크가 켜지고 위로 떠오른다(살짝 커지며 약 24px 올라감).
    //   - 카드 아래 바깥(LiftPivot, 카드 높이 절반만큼 아래)을 기준으로 커져서, 커지는 만큼 위로 올라간다.
    //     카드 줄은 가로 레이아웃이라 위치를 직접 옮기면 레이아웃이 되돌리므로 기준점으로 올린다.
    //   - 사용자가 많으면 카드 줄이 원통으로 돈다(CylinderCarousel). 원통 앞 카드가 선택되고, 이 카드는 그 안에서 위로 떠오른다.
    // 변경점: 삭제는 프로필 설정 화면으로 옮겨 카드의 삭제 칩은 쓰지 않는다(늘 꺼 둠).
    public class ProfileCardView : MonoBehaviour
    {
        const float SelectedScale = 1.08f;
        static readonly Vector2 LiftPivot = new Vector2(0.5f, -0.5f);
        // 변경점: 선택 크기 변화와 등장 연출(톡 나타나기)이 같은 크기를 두고 다투지 않게, 크기 = 선택 배율 x 등장 배율로 이 카드가 직접 합친다.
        const float SelectSeconds = 0.18f;
        const float IntroSeconds = 0.34f;
        const float IntroFromScale = 0.85f;

        public TMP_Text nameText;
        public TMP_Text introText;        // 한마디 소개(없으면 마지막 사용일)
        public Image avatarImage;
        public GameObject selectedBorder; // 선택 테두리 빛
        public GameObject checkMark;      // 우상단 체크
        public Button selectButton;       // 카드 전체 = 선택
        public Button deleteButton;       // 삭제 칩 (쓰지 않음. 삭제는 프로필 설정 화면에서)

        private float _selectK = 1f;
        private float _introK = 1f;
        private CancellationTokenSource _selectCts;

        private void Awake() => ((RectTransform)transform).pivot = LiftPivot;

        public void SetSelected(bool on)
        {
            ApplyMarks(on);

            // 변경점: 선택 크기를 바로 바꾸지 않고 짧게 키우거나 줄인다(커지는 만큼 위로 떠오름)
            float from = _selectK, to = on ? SelectedScale : 1f;
            if (Mathf.Approximately(from, to)) return;
            var ct = UIFx.Restart(ref _selectCts, this);
            UIFx.Tween(this, 0f, SelectSeconds, k =>
            {
                _selectK = Mathf.LerpUnclamped(from, to, on ? UIFx.EaseOutBack(k) : UIFx.EaseOutCubic(k));
                ApplyScale();
            }, ct).Forget();
        }

        // 에디터 캡처용: 연출 없이 바로 선택/해제 모양으로 (에디터에서는 Awake가 돌지 않아 기준점도 여기서 맞춘다)
        public void SetSelectedImmediate(bool on)
        {
            ((RectTransform)transform).pivot = LiftPivot;
            UIFx.Cancel(ref _selectCts);
            ApplyMarks(on);
            _selectK = on ? SelectedScale : 1f;
            ApplyScale();
        }

        private void ApplyMarks(bool on)
        {
            if (selectedBorder != null) selectedBorder.SetActive(on);
            if (checkMark != null) checkMark.SetActive(on);
            if (deleteButton != null) deleteButton.gameObject.SetActive(false);
        }

        // 변경점: 화면 등장 때 차례로 톡 나타나기(알파 + 크기). 카드 줄은 가로 레이아웃이라 위치는 건드리지 않는다.
        public void PlayIntro(float delay, CancellationToken ct)
        {
            var group = UIFx.Group(this);
            UIFx.Tween(this, delay, IntroSeconds, k =>
            {
                group.alpha = Mathf.Clamp01(k * 2f);
                _introK = Mathf.LerpUnclamped(IntroFromScale, 1f, UIFx.EaseOutBack(k));
                ApplyScale();
            }, ct).Forget();
        }

        private void ApplyScale()
        {
            float s = _selectK * _introK;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
