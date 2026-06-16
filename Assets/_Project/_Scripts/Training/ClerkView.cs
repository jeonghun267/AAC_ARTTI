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

        [Header("Debug")]
        [Tooltip("Play 중 화면 좌상단에 테스트 버튼 표시 — 검증 끝나면 끄기")]
        [SerializeField] private bool showDebugButtons = true;

        static readonly int Greeting = Animator.StringToHash("Greeting");
        static readonly int HandOver = Animator.StringToHash("HandOver");
        static readonly int Nod      = Animator.StringToHash("Nod");

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        public void PlayGreeting() => Fire(Greeting);
        public void PlayHandOver() => Fire(HandOver);
        public void PlayNod()      => Fire(Nod);

        private void Fire(int trigger)
        {
            if (animator == null) return;
            animator.SetTrigger(trigger);
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
