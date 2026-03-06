using UnityEngine;

namespace JuegoCriminal.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 2f;

        [Header("Camera Pitch")]
        [SerializeField] private Transform cameraRig;   // el CameraRig (o la cámara si no hay rig)
        [SerializeField] private Transform cameraPivot; // arrastra CameraPivot aquí
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;

        private CharacterController _cc;
        private float _verticalVelocity;
        private float _pitch;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            // Si no asignas cameraRig, intenta encontrar uno
            if (cameraRig == null)
            {
                var rig = GameObject.Find("CameraRig");
                if (rig != null) cameraRig = rig.transform;
                else if (Camera.main != null) cameraRig = Camera.main.transform;
            }
        }

        private void Start()
        {
            if (cameraPivot == null)
            {
                // Busca por nombre (simple y robusto)
                var pivotGo = GameObject.Find("CameraPivot");
                if (pivotGo != null) cameraPivot = pivotGo.transform;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /*private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }*/

        private void Update()
        {
            if (Time.timeScale == 0f) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Look();
            Move();
        }

        private void Look()
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Yaw del player
            transform.Rotate(0f, mx, 0f);

            // Pitch del pivot
            _pitch -= my;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 input = new Vector3(h, 0f, v);
            if (input.sqrMagnitude > 1f) input.Normalize();

            // Movimiento relativo al player
            Vector3 move = transform.TransformDirection(input) * moveSpeed;

            // Gravedad
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            _verticalVelocity += gravity * Time.deltaTime;
            move.y = _verticalVelocity;

            _cc.Move(move * Time.deltaTime);
        }
    }
}