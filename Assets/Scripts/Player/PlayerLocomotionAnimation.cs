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
        [SerializeField] private Animator animator;
        [SerializeField, Min(.01f)] private float movementThreshold = .08f;
        [SerializeField, Min(0)] private float blendDuration = .16f;
        [SerializeField, Range(.1f, 1f)] private float aimBlendDuration = .4f;
        [SerializeField, Range(0f, .3f)] private float locomotionBlendDuration = .18f;
        [SerializeField, Range(.1f, .8f)] private float runningBlendDuration = .3f;
        [SerializeField, Range(0f, 60f)] private float diagonalBodyAngle = 35f;
        [SerializeField, Min(1f)] private float bodyTurnSpeed = 240f;
        private Quaternion modelRestRotation;
        private Transform orientedModel;
        private float bodyYaw, targetBodyYaw;
        private float bodyYawVelocity;
        [SerializeField, Min(.01f)] private float crouchDuration = 1;
        [SerializeField, Min(.01f)] private float stationaryJumpDuration = 1;
        [SerializeField, Min(.01f)] private float movingJumpDuration = 1;
        [SerializeField] private Vector2 stationaryJumpContact = new Vector2(.15f, .8f);
        [SerializeField] private Vector2 movingJumpContact = new Vector2(.1f, .8f);
        private float jumpClock, jumpTakeoff, jumpLanding, jumpDuration, jumpFlightDuration;
        private bool jumpLaunched, jumpRecovering;
        public bool IsJumping { get; private set; }
        public bool IsMovingJump { get; private set; }
        public bool IsBackwardRunning { get; private set; }
        private ThirdPersonController movement;
        private string currentState;
        private bool wantsCrouch;
        private float elapsed, actionLength;
        public ActionPhase Phase { get; private set; }
        public bool IsCrouched { get; private set; }
        public float CrouchAmount { get; private set; }
        public bool BlocksMovement => (IsJumping && !IsMovingJump) || Phase != ActionPhase.Locomotion;
        public bool BlocksJump => IsJumping || IsCrouched || wantsCrouch || BlocksMovement;
        public void Configure(Animator target) => animator = target;
        public void ConfigureTimings(float crouch)
        { crouchDuration = Mathf.Max(.01f, crouch); }
        public void ConfigureJumpTimings(float stationary, float moving)
        { stationaryJumpDuration = stationary; movingJumpDuration = moving; }
        public void ConfigureJumpContacts(float takeoff, float landing, float movingTakeoff, float movingLanding)
        { stationaryJumpContact = new Vector2(takeoff, landing); movingJumpContact = new Vector2(movingTakeoff, movingLanding); }
        public void BeginJump(bool moving, float flightDuration)
        {
            if (BlocksJump || !animator) return;
            IsJumping = true;
            IsMovingJump = moving;
            jumpClock = 0; jumpLaunched = false; jumpRecovering = false;
            var contact = moving ? movingJumpContact : stationaryJumpContact;
            jumpTakeoff = contact.x; jumpLanding = contact.y;
            jumpDuration = moving ? movingJumpDuration : stationaryJumpDuration;
            jumpFlightDuration = Mathf.Max(.1f, flightDuration);
            animator.SetFloat("JumpSpeed", 1);
            if(!moving)movement.StopHorizontalMotion();
            Play(moving ? "Jump Moving" : "Jump On Site");
        }
        public bool ConsumeJumpTakeoff()
        {
            if (!IsJumping || jumpLaunched || jumpClock < jumpTakeoff) return false;
            jumpLaunched = true;
            animator.SetFloat("JumpSpeed", (jumpLanding - jumpTakeoff) / jumpFlightDuration);
            return true;
        }
        public void UpdateJumpContact(bool grounded, float verticalVelocity)
        {
            if (!IsJumping || !jumpLaunched || jumpRecovering || !grounded || verticalVelocity > 0) return;
            jumpRecovering = true; jumpClock = 0;
            animator.SetFloat("JumpSpeed", 1);
            animator.CrossFadeInFixedTime("Base Layer." + currentState, .06f, 0, jumpLanding);
        }

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
            if(GameInput.ConstructionControlsActive) wantsCrouch=false;
            AdvanceInput(GameInput.Move, GameInput.CrouchPressed,
                !GameInput.ConstructionControlsActive && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame, Time.deltaTime);
            ApplyBodyOrientation();
        }
        // Explicit input also allows deterministic action-sequence validation in the editor.
        public void AdvanceInput(Vector2 input, bool crouchPressed, bool aimPressed, float deltaTime)
        {
            if (IsJumping)
            {
                jumpClock += Mathf.Max(0, deltaTime);
                if (jumpRecovering && jumpClock >= jumpDuration - jumpLanding) IsJumping = false;
                return;
            }
            // Aiming is a toggle, not a draw/holster sequence. Movement and crouch
            // cannot cancel the pose until aiming locomotion is implemented.
            if (Phase == ActionPhase.Aiming)
            {
                if (aimPressed || GameInput.ConstructionControlsActive)
                { Phase = ActionPhase.Locomotion; Play("Standing Idle"); }
                return;
            }
            if (crouchPressed) wantsCrouch = !wantsCrouch;
            elapsed += Mathf.Max(0, deltaTime);
            if (Phase == ActionPhase.Crouching) CrouchAmount = Mathf.Clamp01(elapsed / crouchDuration);
            if (Phase == ActionPhase.Standing) CrouchAmount = 1 - Mathf.Clamp01(elapsed / crouchDuration);
            if (Phase != ActionPhase.Locomotion && Phase != ActionPhase.Aiming && elapsed >= actionLength)
            {
                switch (Phase)
                {
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
            else if (aimPressed && !IsCrouched && !GameInput.ConstructionControlsActive)
            {
                movement.StopHorizontalMotion();
                Phase = ActionPhase.Aiming;
                Play("Pistol Idle");
            }
        }
        private void Begin(ActionPhase phase, string state, float duration, float offset = 0)
        {
            Phase = phase; elapsed = 0; actionLength = Mathf.Max(.001f, duration); Play(state, offset);
        }
        private void LateUpdate()
        {
            if (!CanUpdate) return;
            UpdateLocomotion(movement.LocalAnimationVelocity, GameInput.Move, GameInput.SprintHeld);
        }
        // Rotate the visual root before Animator/OnAnimatorIK evaluates the head,
        // never afterwards (which would rotate an already solved gaze).
        private void ApplyBodyOrientation()
        {
            if(IsJumping)return;
            if (animator && animator.transform != transform)
            {
                if (orientedModel != animator.transform)
                {
                    orientedModel = animator.transform;
                    modelRestRotation = orientedModel.localRotation;
                    bodyYaw = 0;
                }
                bodyYaw = Mathf.SmoothDampAngle(bodyYaw, targetBodyYaw, ref bodyYawVelocity, .2f, bodyTurnSpeed, Time.deltaTime);
                orientedModel.localRotation = Quaternion.AngleAxis(bodyYaw, Vector3.up) * modelRestRotation;
            }
        }
        public void UpdateLocomotion(Vector3 velocity)
            => UpdateLocomotion(velocity, new Vector2(velocity.x, velocity.z));

        public void UpdateLocomotion(Vector3 velocity, Vector2 input)
            => UpdateLocomotion(velocity, input, false);

        public void UpdateLocomotion(Vector3 velocity, Vector2 input, bool sprinting)
        {
            if (IsJumping) return;
            IsBackwardRunning=false;
            targetBodyYaw = 0;
            if (Phase != ActionPhase.Locomotion) return;
            if (IsCrouched)
            {
                if (Mathf.Abs(velocity.z) <= movementThreshold) Play("Crouched Idle");
                else Play(velocity.z < 0 ? "Crouched Backward" : "Crouched Walking");
            }
            else if (velocity.sqrMagnitude <= movementThreshold * movementThreshold)
                Play("Standing Idle");
            else if (input.sqrMagnitude > .001f)
            {
                bool backward=input.y<-.01f;
                IsBackwardRunning=backward && sprinting;
                // Sideways motion uses the forward gait with the visual body turned;
                // S retains the reversed walking gait, including backward diagonals.
                targetBodyYaw=IsBackwardRunning ? Mathf.Atan2(input.x,input.y)*Mathf.Rad2Deg :
                    Mathf.Atan2(backward?-input.x:input.x,Mathf.Abs(input.y))*Mathf.Rad2Deg;
                Play(sprinting ? "Fast Run" : backward ? "Walking Backward" : "Walking");
            }
            else Play("Standing Idle");
        }
        private void Play(string state, float offset = 0)
        {
            if (currentState == state || !animator || !animator.runtimeAnimatorController) return;
            bool runBlend = IsRunningState(state) || IsRunningState(currentState);
            bool aimBlend = state == "Pistol Idle" || currentState == "Pistol Idle";
            bool urgentStop = state.StartsWith("Jump");
            currentState = state;
            animator.CrossFadeInFixedTime("Base Layer." + state,
                aimBlend ? aimBlendDuration : Phase == ActionPhase.Locomotion ? (runBlend && !urgentStop ? runningBlendDuration : locomotionBlendDuration) : blendDuration, 0, offset);
        }
        private static bool IsRunningState(string state) => state == "Fast Run";
    }
}
