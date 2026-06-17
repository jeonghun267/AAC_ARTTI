using UnityEngine;

namespace Artti.Training
{
    // 점원(Clerk) 애니메이션 뷰. TrainingSceneRoot가 의미 단위 메서드로 호출한다.
    // Animator 트리거: Greeting / HandOver / Nod (ClerkController.controller)
    // 비즈니스 로직 없음 — 입력/상태 판단은 호출자(TrainingSceneRoot) 책임.
    [RequireComponent(typeof(Animator))]
    public class ClerkView : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Header("HandOver 소품 (옵션 — 점원 손 본에 붙인 물건. 미연결 시 무동작)")]
        [Tooltip("HandOver 애니 중에만 보이게 할 물건 오브젝트(콜라/봉투 등). 씬에서 손 본 자식으로 붙여 연결")]
        [SerializeField] private GameObject handHeldItem;
        [Tooltip("물건을 보여줄 시간(초). HandOver 클립 길이에 맞춰 조정")]
        [SerializeField] private float handHeldVisibleSeconds = 2.5f;

        [Header("Debug")]
        [Tooltip("Play 중 화면 좌상단에 테스트 버튼 표시 — 검증 끝나면 끄기")]
        [SerializeField] private bool showDebugButtons = true;

        static readonly int Greeting = Animator.StringToHash("Greeting");
        static readonly int HandOver = Animator.StringToHash("HandOver");
        static readonly int Nod      = Animator.StringToHash("Nod");

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (handHeldItem != null) handHeldItem.SetActive(false); // 시작 시 숨김
        }

        public void PlayGreeting() => Fire(Greeting);
        public void PlayNod()      => Fire(Nod);

        // 물건 건네기: 애니 트리거 + 손 소품을 잠깐 표시
        public void PlayHandOver()
        {
            Fire(HandOver);
            ShowHandItem();
        }

        private void Fire(int trigger)
        {
            if (animator == null) return;
            animator.SetTrigger(trigger);
        }

        private void ShowHandItem()
        {
            if (handHeldItem == null) return;
            handHeldItem.SetActive(true);
            CancelInvoke(nameof(HideHandItem));
            Invoke(nameof(HideHandItem), handHeldVisibleSeconds);
        }

        private void HideHandItem()
        {
            if (handHeldItem != null) handHeldItem.SetActive(false);
        }

        private void OnGUI()
        {
            if (!showDebugButtons) return;
            const float w = 200f, h = 56f;
            if (GUI.Button(new Rect(20, 20, w, h),  "인사 (Greeting)"))  PlayGreeting();
            if (GUI.Button(new Rect(20, 86, w, h),  "건네기 (HandOver)")) PlayHandOver();
            if (GUI.Button(new Rect(20, 152, w, h), "끄덕임 (Nod)"))      PlayNod();
        }
    }
}
