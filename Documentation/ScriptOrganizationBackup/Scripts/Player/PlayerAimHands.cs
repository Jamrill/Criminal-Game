using UnityEngine;

namespace JuegoCriminal.Player
{
    // A small, reversible correction after humanoid retargeting. The source clip
    // and all non-aiming animations keep their original hand poses.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAimHands : MonoBehaviour
    {
        [Tooltip("Extra separation per hand in world metres while in Pistol Idle.")]
        [SerializeField, Range(0f, .15f)] private float handSeparation = .045f;
        [SerializeField, Range(0f, 1f)] private float correctionWeight = 1f;
        private Animator animator;
        private void Awake() => animator = GetComponent<Animator>();

        public static float AimWeight(Animator target, int layer)
        {
            bool current = target.GetCurrentAnimatorStateInfo(layer).IsName("Pistol Idle");
            if (!target.IsInTransition(layer)) return current ? 1f : 0f;
            bool next = target.GetNextAnimatorStateInfo(layer).IsName("Pistol Idle");
            float t = Mathf.SmoothStep(0f, 1f, target.GetAnimatorTransitionInfo(layer).normalizedTime);
            return Mathf.Lerp(current ? 1f : 0f, next ? 1f : 0f, t);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!animator || !animator.isHuman || layerIndex != 0) return;
            float weight = AimWeight(animator, layerIndex) * correctionWeight;
            Offset(AvatarIKGoal.LeftHand, -transform.right, weight);
            Offset(AvatarIKGoal.RightHand, transform.right, weight);
        }

        private void Offset(AvatarIKGoal hand, Vector3 direction, float weight)
        {
            // Read this frame's animated effector, never the previous IK result,
            // so corrections cannot accumulate or pull unrelated poses sideways.
            Vector3 position = animator.GetIKPosition(hand);
            Quaternion rotation = animator.GetIKRotation(hand);
            animator.SetIKPositionWeight(hand, weight);
            animator.SetIKRotationWeight(hand, weight);
            animator.SetIKPosition(hand, position + direction * handSeparation);
            animator.SetIKRotation(hand, rotation);
        }
    }
}
