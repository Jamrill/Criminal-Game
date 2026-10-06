using UnityEngine;

namespace JuegoCriminal.Player
{
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerHeadLook : MonoBehaviour
    {
        [SerializeField] private Transform aimTarget;
        [SerializeField, Range(0, 1)] private float weight = .85f;
        [SerializeField, Min(.1f)] private float response = 6f;
        [SerializeField, Range(10, 80)] private float maxYaw = 65;
        [SerializeField, Range(5, 50)] private float maxPitch = 35;
        private Animator animator;
        private ThirdPersonController movement;
        private PlayerLocomotionAnimation locomotion;
        private float currentWeight;
        private Vector3 lookDirection;
        public void SetAimTarget(Transform target) => aimTarget = target;
        private void Awake()
        {
            animator = GetComponent<Animator>();
            movement = GetComponentInParent<ThirdPersonController>();
            locomotion = GetComponentInParent<PlayerLocomotionAnimation>();
            lookDirection = transform.forward;
        }
        private void OnAnimatorIK(int layerIndex)
        {
            if (!animator || !animator.isHuman || !movement || !locomotion) return;
            if (Time.timeScale <= 0) return;
            var camera = Camera.main;
            bool active = (aimTarget || camera) && movement.enabled && Time.timeScale > 0 &&
                !locomotion.IsJumping && !locomotion.IsCrouched && !locomotion.BlocksMovement &&
                Cursor.lockState == CursorLockMode.Locked;
            // Keep the gaze through idle and direction changes. The measured
            // velocity can briefly reach zero when reversing A/D.
            currentWeight = Mathf.MoveTowards(currentWeight, active ? weight : 0, response * Time.deltaTime);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (!head) return;
            if (active)
            {
                Vector3 target = aimTarget ? aimTarget.position : camera.ViewportToWorldPoint(new Vector3(.5f, .5f, 20));
                Vector3 local = transform.InverseTransformDirection(target - head.position).normalized;
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
                float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1, 1)) * Mathf.Rad2Deg, -maxPitch, maxPitch);
                Vector3 desired = transform.TransformDirection(Quaternion.Euler(pitch, yaw, 0) * Vector3.forward);
                lookDirection = Vector3.Slerp(lookDirection, desired, 1 - Mathf.Exp(-response * Time.deltaTime));
            }
            // Even during fade-out, supply the last valid target every IK pass.
            animator.SetLookAtPosition(head.position + lookDirection * 10);
            animator.SetLookAtWeight(currentWeight, 0, 1, 0, .55f);
        }
    }
}
