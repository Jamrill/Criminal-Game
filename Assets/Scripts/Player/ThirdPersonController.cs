using UnityEngine;
using JuegoCriminal.Core;

namespace JuegoCriminal.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float runSpeed = 6.0f;
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float deceleration = 18f;

        [Header("Crouch")]
        [SerializeField, Range(0.25f, 0.9f)] private float crouchedHeightMultiplier = 0.5f;
        [SerializeField, Min(0.1f)] private float crouchTransitionSpeed = 8f;
        [SerializeField, Min(0.1f)] private float crouchMoveSpeed = 1.75f;
        [SerializeField, Min(0)] private float crouchTurnSpeed = 110f;
        private PlayerLocomotionAnimation animationDriver;

        [Header("Jump / Gravity")]
        [SerializeField] private bool canJump = true;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Look")]
        [SerializeField] private float mouseLookSensitivity = 0.1f;
        [SerializeField] private float gamepadLookSpeed = 120f;

        [Header("Camera Pitch")]
        [SerializeField] private Transform cameraRig;   // CameraRig
        [SerializeField] private Transform cameraPivot; // CameraPivot
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;

        private CharacterController _cc;

        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;
        private float _standingCapsuleHeight;
        private Vector3 _standingCapsuleCenter;

        public float LookPitch => _pitch;
        public Vector3 LocalAnimationVelocity { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            animationDriver = GetComponent<PlayerLocomotionAnimation>();
            _standingCapsuleHeight = _cc.height;
            _standingCapsuleCenter = _cc.center;

            FindCameraReferencesIfNeeded();
        }

        private void Start()
        {
            FindCameraReferencesIfNeeded();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            LocalAnimationVelocity = Vector3.zero;
            if (!CanReceiveInput())
                return;

            FindCameraReferencesIfNeeded();

            Look();
            UpdateCrouch();
            Move();
        }

        private bool CanReceiveInput()
        {
            if (Time.timeScale == 0f)
                return false;

            if (Cursor.lockState != CursorLockMode.Locked)
                return false;

            return true;
        }

        private void Look()
        {
            Vector2 lookInput = GameInput.Look;
            float sensitivity = GameInput.IsLookFromPointer
                ? mouseLookSensitivity
                : gamepadLookSpeed * Time.deltaTime;

            float mx = lookInput.x * sensitivity;
            float my = lookInput.y * sensitivity;

            // De momento mantenemos el sistema actual:
            // el ratón rota al jugador en Y, y la cámara sigue al jugador.
            transform.Rotate(0f, mx, 0f);

            // Pitch vertical del CameraPivot.
            _pitch -= my;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move()
        {
            Vector2 input = ReadMoveInput();
            if (animationDriver && animationDriver.BlocksMovement)
            {
                input = Vector2.zero;
                _horizontalVelocity = Vector3.zero;
            }
            else if (animationDriver && animationDriver.IsCrouched)
            {
                transform.Rotate(0, input.x * crouchTurnSpeed * Time.deltaTime, 0);
                input.x = 0;
            }

            Vector3 targetHorizontalVelocity = CalculateTargetHorizontalVelocity(input);
            UpdateHorizontalVelocity(targetHorizontalVelocity);
            UpdateVerticalVelocity();

            Vector3 finalVelocity = _horizontalVelocity;
            finalVelocity.y = _verticalVelocity;

            _cc.Move(finalVelocity * Time.deltaTime);
            Vector3 horizontal = _cc.velocity;
            horizontal.y = 0f;
            LocalAnimationVelocity = transform.InverseTransformDirection(horizontal);
        }

        private Vector2 ReadMoveInput()
        {
            Vector2 input = GameInput.Move;

            if (input.sqrMagnitude > 1f)
                input.Normalize();

            return input;
        }

        private Vector3 CalculateTargetHorizontalVelocity(Vector2 input)
        {
            if (input.sqrMagnitude <= 0.001f)
                return Vector3.zero;

            // Movimiento relativo al jugador.
            // Como la cámara sigue el yaw del jugador, esto encaja con la cámara actual.
            Vector3 moveDirection =
                transform.right * input.x +
                transform.forward * input.y;

            moveDirection.y = 0f;
            moveDirection.Normalize();

            float speed = animationDriver && animationDriver.IsCrouched ? crouchMoveSpeed :
                (GameInput.SprintHeld ? runSpeed : walkSpeed);

            return moveDirection * speed;
        }

        private void UpdateHorizontalVelocity(Vector3 targetHorizontalVelocity)
        {
            float rate = targetHorizontalVelocity.sqrMagnitude > 0.001f
                ? acceleration
                : deceleration;

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity,
                targetHorizontalVelocity,
                rate * Time.deltaTime
            );
        }

        private void UpdateVerticalVelocity()
        {
            if (_cc.isGrounded)
            {
                if (_verticalVelocity < 0f)
                    _verticalVelocity = groundedStickForce;

                if (canJump && (!animationDriver || !animationDriver.BlocksJump) && GameInput.JumpPressed)
                {
                    // Fórmula física básica para alcanzar jumpHeight.
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }
        }

        private void FindCameraReferencesIfNeeded()
        {
            if (cameraRig == null)
            {
                GameObject rig = GameObject.Find("CameraRig");

                if (rig != null)
                    cameraRig = rig.transform;
                else if (Camera.main != null)
                    cameraRig = Camera.main.transform;
            }

            if (cameraPivot == null)
            {
                GameObject pivot = GameObject.Find("CameraPivot");

                if (pivot != null)
                    cameraPivot = pivot.transform;
            }
        }

        private void UpdateCrouch()
        {
            float crouchedHeight = _standingCapsuleHeight * crouchedHeightMultiplier;
            float amount = animationDriver ? animationDriver.CrouchAmount : (GameInput.CrouchHeld ? 1 : 0);
            float targetHeight = Mathf.Lerp(_standingCapsuleHeight, crouchedHeight, amount);
            float standingBottom = _standingCapsuleCenter.y - _standingCapsuleHeight * 0.5f;
            Vector3 targetCenter = _standingCapsuleCenter;
            targetCenter.y = standingBottom + targetHeight * 0.5f;

            _cc.height = Mathf.MoveTowards(_cc.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            _cc.center = Vector3.MoveTowards(_cc.center, targetCenter, crouchTransitionSpeed * Time.deltaTime);
        }

        public bool CanStandUp()
        {
            if (!_cc) return true;
            float scale = Mathf.Abs(transform.lossyScale.y);
            float radius = _cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) * .95f;
            Vector3 center = transform.TransformPoint(_standingCapsuleCenter);
            float half = Mathf.Max(0, _standingCapsuleHeight * scale * .5f - radius);
            // Test only the space above the current capsule; the floor must not block standing up.
            Vector3 currentTop = transform.TransformPoint(_cc.center) + transform.up *
                Mathf.Max(0, _cc.height * scale * .5f - radius);
            foreach (var hit in Physics.OverlapCapsule(currentTop, center + transform.up * half,
                         radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit != _cc && !hit.transform.IsChildOf(transform)) return false;
            return true;
        }

        public void SetLookRotation(float yaw, float pitch)
        {
            FindCameraReferencesIfNeeded();

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
