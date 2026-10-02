using UnityEngine;

namespace JuegoCriminal.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ThirdPersonController))]
    public sealed class PlayerLocomotionAnimation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.01f)] private float movementThreshold = 0.08f;
        [SerializeField] private bool reverseWalkingWhenBackingUp = true;
        private ThirdPersonController movement;
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");
        private static readonly int PlaybackSpeed = Animator.StringToHash("PlaybackSpeed");

        public void Configure(Animator target) => animator = target;

        private void Awake()
        {
            movement = GetComponent<ThirdPersonController>();
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            if (animator) animator.applyRootMotion = false;
        }

        private void LateUpdate()
        {
            if (!animator || !animator.runtimeAnimatorController || Time.timeScale == 0f) return;
            Vector3 velocity = movement.enabled ? movement.LocalAnimationVelocity : Vector3.zero;
            bool moving = velocity.sqrMagnitude > movementThreshold * movementThreshold;
            animator.SetBool(IsMoving, moving);
            animator.SetFloat(PlaybackSpeed,
                moving && reverseWalkingWhenBackingUp && velocity.z < -movementThreshold ? -1f : 1f);
        }
    }
}
