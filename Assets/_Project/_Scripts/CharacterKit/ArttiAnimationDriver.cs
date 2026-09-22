using UnityEngine;

namespace Artti.CharacterKit
{
    [RequireComponent(typeof(Animator))]
    public sealed class ArttiAnimationDriver : MonoBehaviour
    {
        private Animator animator;
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int WaveTrigger = Animator.StringToHash("Wave");
        private void Awake() { animator = GetComponent<Animator>(); animator.applyRootMotion = false; }
        public void Idle() => SetSpeed(0);
        public void Walk() => SetSpeed(1);
        public void Run() => SetSpeed(2);
        public void SetSpeed(float speed)
        {
            if (!animator) animator = GetComponent<Animator>();
            animator.SetFloat(Speed, Mathf.Clamp(speed, 0, 2));
        }
        public void Wave()
        {
            if (!animator) animator = GetComponent<Animator>();
            animator.SetFloat(Speed, 0);
            animator.ResetTrigger(WaveTrigger);
            animator.SetTrigger(WaveTrigger);
        }
    }
}
