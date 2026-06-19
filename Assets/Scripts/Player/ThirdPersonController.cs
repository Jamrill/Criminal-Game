using UnityEngine;

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

        [Header("Jump / Gravity")]
        [SerializeField] private bool canJump = true;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 2f;

        [Header("Camera Pitch")]
        [SerializeField] private Transform cameraRig;   // CameraRig
        [SerializeField] private Transform cameraPivot; // CameraPivot
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Input")]
        [SerializeField] private KeyCode runKey = KeyCode.LeftShift;

        private CharacterController _cc;

        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

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
            if (!CanReceiveInput())
                return;

            FindCameraReferencesIfNeeded();

            Look();
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
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

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

            Vector3 targetHorizontalVelocity = CalculateTargetHorizontalVelocity(input);
            UpdateHorizontalVelocity(targetHorizontalVelocity);
            UpdateVerticalVelocity();

            Vector3 finalVelocity = _horizontalVelocity;
            finalVelocity.y = _verticalVelocity;

            _cc.Move(finalVelocity * Time.deltaTime);
        }

        private Vector2 ReadMoveInput()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector2 input = new Vector2(h, v);

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

            float speed = Input.GetKey(runKey) ? runSpeed : walkSpeed;

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

                if (canJump && Input.GetKeyDown(jumpKey))
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
    }
}