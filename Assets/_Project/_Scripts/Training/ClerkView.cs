using System.Collections.Generic;
using UnityEngine;

namespace Artti.Training
{
    // 점원(Clerk) 애니메이션 뷰. TrainingSceneRoot가 의미 단위 메서드로 호출한다.
    // Animator 트리거:
    //   v09 ARTTI_Clerk.controller : Greeting / Wave / Smile (+ IsTalking Bool, 아직 전환에는 미사용)
    //   구 ClerkController.controller : Greeting / HandOver / Nod
    // 컨트롤러에 없는 트리거는 Awake에서 확인해 두고 호출 시 대체 트리거로 바꾼다 (Nod, HandOver -> Smile).
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
        static readonly int Wave     = Animator.StringToHash("Wave");
        static readonly int Smile    = Animator.StringToHash("Smile");

        // 현재 컨트롤러가 가진 트리거 해시 (Awake에서 캐싱)
        private readonly HashSet<int> _triggers = new HashSet<int>();
        private RuntimeAnimatorController _cachedController;
        private bool _triggersCached;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (handHeldItem != null) handHeldItem.SetActive(false); // 시작 시 숨김
            CacheTriggers();
        }

        private void CacheTriggers()
        {
            _triggers.Clear();
            _cachedController = animator != null ? animator.runtimeAnimatorController : null;
            _triggersCached = true;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger) _triggers.Add(p.nameHash);
            }
        }

        private void EnsureTriggers()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (!Application.isPlaying || !_triggersCached ||
                _cachedController != (animator != null ? animator.runtimeAnimatorController : null))
                CacheTriggers();
        }

        public bool HasTrigger(string name)
        {
            EnsureTriggers();
            return _triggers.Contains(Animator.StringToHash(name));
        }

        public void PlayGreeting() => Fire(HasTrigger("Greeting") ? Greeting : Wave);
        public void PlayWave()     => Fire(Wave);
        public void PlaySmile()    => Fire(Smile);

        // 끄덕임: 구 컨트롤러는 Nod, v09 컨트롤러는 Smile(미소 반응)로 대체
        public void PlayNod() => Fire(HasTrigger("Nod") ? Nod : Smile);

        // 물건 건네기: 애니 트리거 + 손 소품을 잠깐 표시. v09 컨트롤러에는 HandOver가 없어 Smile로 대체
        public void PlayHandOver()
        {
            Fire(HasTrigger("HandOver") ? HandOver : Smile);
            ShowHandItem();
        }

        private void Fire(int trigger)
        {
            EnsureTriggers();
            if (animator == null) return;
            if (!_triggers.Contains(trigger)) return; // 빈 컨트롤러를 포함해 없는 트리거는 무시
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
            if (GUI.Button(new Rect(20, 86, w, h),  "손 흔들기 (Wave)")) PlayWave();
            if (GUI.Button(new Rect(20, 152, w, h), "미소 (Smile)"))     PlaySmile();
            if (GUI.Button(new Rect(20, 218, w, h), "건네기 (HandOver)")) PlayHandOver();
            if (GUI.Button(new Rect(20, 284, w, h), "끄덕임 (Nod)"))      PlayNod();
        }
    }
}
