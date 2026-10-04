using UnityEngine;
using UnityEngine.InputSystem;
using JuegoCriminal.Core;

namespace JuegoCriminal.Player
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ThirdPersonController))]
    public sealed class PlayerLocomotionAnimation : MonoBehaviour
    {
        public enum ActionPhase { Locomotion, Drawing, Aiming, Holstering, Crouching, Standing }
        public const float DrawSpeed = 2f;
        [SerializeField] private Animator animator;
        [SerializeField, Min(.01f)] private float movementThreshold = .08f;
        [SerializeField, Min(0)] private float blendDuration = .16f;
        [SerializeField, Min(.01f)] private float pistolDuration = 1;
        [SerializeField, Min(.01f)] private float crouchDuration = 1;
        private ThirdPersonController movement;
        private string currentState;
        private bool wantsCrouch;
        private float elapsed, actionLength;
        public ActionPhase Phase { get; private set; }
        public bool IsCrouched { get; private set; }
        public float CrouchAmount { get; private set; }
        public bool BlocksMovement => Phase != ActionPhase.Locomotion;
        public bool BlocksJump => IsCrouched || wantsCrouch || BlocksMovement;
        public void Configure(Animator target) => animator = target;
        public void ConfigureTimings(float pistol, float crouch)
        { pistolDuration = Mathf.Max(.01f, pistol); crouchDuration = Mathf.Max(.01f, crouch); }

        private void Awake()
        {
            movement = GetComponent<ThirdPersonController>();
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            if (animator) animator.applyRootMotion = false;
        }
        private bool CanUpdate => animator && animator.runtimeAnimatorController && movement &&
            movement.enabled && Time.timeScale > 0 && Cursor.lockState == CursorLockMode.Locked;
        private void Update()
        {
            if (!CanUpdate)
            {
                // Freeze action time and animation together while menus capture input.
                if (animator && movement && movement.enabled) animator.speed = 0;
                return;
            }
            animator.speed = 1;
            AdvanceInput(GameInput.Move, GameInput.GetAction(GameInputAction.Crouch)?.WasPressedThisFrame() == true,
                Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame, Time.deltaTime);
        }
        // Explicit input also allows deterministic action-sequence validation in the editor.
        public void AdvanceInput(Vector2 input, bool crouchPressed, bool aimPressed, float deltaTime)
        {
            if (crouchPressed) wantsCrouch = !wantsCrouch;
            bool moving = input.sqrMagnitude > .001f;
            elapsed += Mathf.Max(0, deltaTime);
            if (Phase == ActionPhase.Drawing && (moving || wantsCrouch))
            {
                float position = Mathf.Clamp(pistolDuration - elapsed * DrawSpeed, 0, pistolDuration);
                Begin(ActionPhase.Holstering, "Pistol Holster", pistolDuration - position, position);
            }
            else if (Phase == ActionPhase.Aiming && (moving || wantsCrouch))
                Begin(ActionPhase.Holstering, "Pistol Holster", pistolDuration);
            if (Phase == ActionPhase.Crouching) CrouchAmount = Mathf.Clamp01(elapsed / crouchDuration);
            if (Phase == ActionPhase.Standing) CrouchAmount = 1 - Mathf.Clamp01(elapsed / crouchDuration);
            if (Phase != ActionPhase.Locomotion && Phase != ActionPhase.Aiming && elapsed >= actionLength)
            {
                switch (Phase)
                {
                    case ActionPhase.Drawing: Phase = ActionPhase.Aiming; Play("Pistol Idle"); break;
                    case ActionPhase.Crouching: IsCrouched = true; CrouchAmount = 1; Phase = ActionPhase.Locomotion; break;
                    case ActionPhase.Standing: IsCrouched = false; CrouchAmount = 0; Phase = ActionPhase.Locomotion; break;
                    default: Phase = ActionPhase.Locomotion; break;
                }
            }
            if (Phase != ActionPhase.Locomotion) return;
            if (wantsCrouch != IsCrouched)
            {
                if (!wantsCrouch && movement && !movement.CanStandUp()) return;
                Begin(wantsCrouch ? ActionPhase.Crouching : ActionPhase.Standing,
                    wantsCrouch ? "Crouch Down" : "Stand Up", crouchDuration);
            }
            else if (aimPressed && !IsCrouched && !moving)
                Begin(ActionPhase.Drawing, "Pistol Draw", pistolDuration / DrawSpeed);
        }
        private void Begin(ActionPhase phase, string state, float duration, float offset = 0)
        {
            Phase = phase; elapsed = 0; actionLength = Mathf.Max(.001f, duration); Play(state, offset);
        }
        private void LateUpdate()
        {
            if (CanUpdate) UpdateLocomotion(movement.LocalAnimationVelocity);
        }
        public void UpdateLocomotion(Vector3 velocity)
        {
            if (Phase != ActionPhase.Locomotion) return;
            if (IsCrouched)
            {
                if (Mathf.Abs(velocity.z) <= movementThreshold) Play("Crouched Idle");
                else Play(velocity.z < 0 ? "Crouched Backward" : "Crouched Walking");
            }
            else if (velocity.sqrMagnitude <= movementThreshold * movementThreshold) Play("Standing Idle");
            else if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.z)) Play(velocity.x < 0 ? "Left Strafe Walk" : "Right Strafe Walk");
            else Play(velocity.z < 0 ? "Walking Backward" : "Walking");
        }
        private void Play(string state, float offset = 0)
        {
            if (currentState == state || !animator || !animator.runtimeAnimatorController) return;
            currentState = state;
            animator.CrossFadeInFixedTime("Base Layer." + state, blendDuration, 0, offset);
        }
    }
}
