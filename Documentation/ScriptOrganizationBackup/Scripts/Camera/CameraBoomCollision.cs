using UnityEngine;
using JuegoCriminal.Core;

namespace JuegoCriminal.CameraSystem
{
    public sealed class CameraBoomCollision : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform cameraTransform; // Main Camera

        [Header("View Distances")]
        [SerializeField] private float firstPersonDistance = 0.05f;
        [SerializeField] private float thirdPersonDistance = 6f;

        [Header("View Heights")]
        [SerializeField] private float firstPersonHeight = 1.75f;
        [SerializeField] private float thirdPersonHeight = 2.0f;

        [Header("Shoulder Camera - Third Person")]
        [SerializeField] private float thirdPersonShoulderOffset = 0.65f;
        [SerializeField] private bool startOnRightShoulder = true;

        [Header("Transition")]
        [SerializeField] private float transitionSpeed = 12f;
        [SerializeField] private float scrollThreshold = 0.1f;

        [Header("Collision - Third Person Only")]
        [SerializeField] private float sphereRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = ~0;

        public float CurrentDistance { get; private set; }
        public bool IsFirstPerson => _isFirstPerson;

        private bool _initialized;

        private bool _isFirstPerson;
        private float _targetDistance;
        private float _targetHeight;

        // -1 = hombro izquierdo, 1 = hombro derecho
        private int _shoulderSign;
        private float _currentShoulderOffset;

        private void Awake()
        {
            FindCameraIfNeeded();

            // Estado inicial: tercera persona
            _isFirstPerson = false;
            _targetDistance = thirdPersonDistance;
            _targetHeight = thirdPersonHeight;

            // Hombro inicial
            _shoulderSign = startOnRightShoulder ? 1 : -1;
            _currentShoulderOffset = _shoulderSign * thirdPersonShoulderOffset;

            CurrentDistance = thirdPersonDistance;
        }

        private void Start()
        {
            // En escenas con player/cámara generados en runtime, Camera.main puede no estar listo en Awake.
            FindCameraIfNeeded();

            ApplyImmediate();
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                FindCameraIfNeeded();
                ApplyImmediate();
                _initialized = true;
            }

            HandleScrollToggle();
            HandleShoulderSwitch();

            if (cameraTransform == null) return;

            UpdatePivotHeight();
            UpdateDistance();
            UpdateShoulderOffset();
            UpdateCameraPosition();
        }

        // -------------------------
        // Main camera update pieces
        // -------------------------

        private void UpdatePivotHeight()
        {
            // La altura vive en CameraPivot, no en MainCamera.
            float newHeight = Mathf.Lerp(
                transform.localPosition.y,
                _targetHeight,
                transitionSpeed * Time.deltaTime
            );

            transform.localPosition = new Vector3(0f, newHeight, 0f);
        }

        private void UpdateDistance()
        {
            CurrentDistance = Mathf.Lerp(
                CurrentDistance,
                _targetDistance,
                transitionSpeed * Time.deltaTime
            );
        }

        private void UpdateShoulderOffset()
        {
            // En primera persona la cámara se centra.
            // En tercera persona se desplaza al hombro actual.
            float targetShoulderOffset = _isFirstPerson
                ? 0f
                : _shoulderSign * thirdPersonShoulderOffset;

            _currentShoulderOffset = Mathf.Lerp(
                _currentShoulderOffset,
                targetShoulderOffset,
                transitionSpeed * Time.deltaTime
            );
        }

        private void UpdateCameraPosition()
        {
            // Posición local deseada:
            // X = hombro
            // Y = 0 porque la altura está en el CameraPivot
            // Z = distancia hacia atrás
            Vector3 desiredLocalPos = new Vector3(
                _currentShoulderOffset,
                0f,
                -CurrentDistance
            );

            if (!_isFirstPerson)
            {
                desiredLocalPos = GetCollisionAdjustedLocalPosition(desiredLocalPos);
            }
            else
            {
                //desiredLocalPos = GetFirstPersonSafeLocalPosition(desiredLocalPos);
            }

            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                desiredLocalPos,
                transitionSpeed * Time.deltaTime
            );
        }

        // -------------------------
        // Input
        // -------------------------

        private void HandleScrollToggle()
        {
            // No cambiar cámara mientras el juego está pausado o el cursor está libre.
            if (Time.timeScale == 0f) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float scroll = GameInput.CameraZoom;
            if (Mathf.Abs(scroll) < scrollThreshold) return;

            if (scroll > 0f)
                SetFirstPerson();
            else
                SetThirdPerson();
        }

        private void HandleShoulderSwitch()
        {
            // Solo cambiar hombro en tercera persona.
            if (_isFirstPerson) return;

            if (Time.timeScale == 0f) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            if (GameInput.SwitchShoulderPressed)
                _shoulderSign *= -1;
        }

        // -------------------------
        // View modes
        // -------------------------

        private void SetFirstPerson()
        {
            _isFirstPerson = true;
            _targetDistance = firstPersonDistance;
            _targetHeight = firstPersonHeight;
        }

        private void SetThirdPerson()
        {
            _isFirstPerson = false;
            _targetDistance = thirdPersonDistance;
            _targetHeight = thirdPersonHeight;
        }

        // -------------------------
        // Collision
        // -------------------------

        private Vector3 GetCollisionAdjustedLocalPosition(Vector3 desiredLocalPos)
        {
            Vector3 origin = transform.position;
            Vector3 desiredWorldPos = transform.TransformPoint(desiredLocalPos);

            Vector3 direction = desiredWorldPos - origin;
            float desiredMagnitude = direction.magnitude;

            if (desiredMagnitude <= 0.001f)
                return desiredLocalPos;

            direction.Normalize();

            // Excluir Player para que la cámara no choque con el propio personaje.
            int mask = collisionMask & ~LayerMask.GetMask("Player");

            if (Physics.SphereCast(
                    origin,
                    sphereRadius,
                    direction,
                    out RaycastHit hit,
                    desiredMagnitude,
                    mask,
                    QueryTriggerInteraction.Ignore))
            {
                float safeDistance = Mathf.Max(0.25f, hit.distance);
                Vector3 adjustedWorldPos = origin + direction * safeDistance;

                return transform.InverseTransformPoint(adjustedWorldPos);
            }

            return desiredLocalPos;
        }

        private Vector3 GetFirstPersonSafeLocalPosition(Vector3 desiredLocalPos)
        {
            if (cameraTransform == null)
                return desiredLocalPos;

            Camera cam = cameraTransform.GetComponent<Camera>();

            if (cam == null)
                return desiredLocalPos;

            int mask = collisionMask & ~LayerMask.GetMask("Player");

            Vector3 cameraWorldPos = transform.TransformPoint(desiredLocalPos);

            // Distancia de seguridad frente a paredes.
            // Debe ser algo mayor que el Near Clip Plane.
            float safeDistance = Mathf.Max(cam.nearClipPlane + 0.08f, 0.12f);

            // Puntos del viewport que vamos a comprobar.
            // Centro + bordes básicos del near plane.
            Vector3[] viewportPoints =
            {
        new Vector3(0.5f, 0.5f, safeDistance), // centro
        new Vector3(0.5f, 0.85f, safeDistance), // arriba
        new Vector3(0.5f, 0.15f, safeDistance), // abajo
        new Vector3(0.15f, 0.5f, safeDistance), // izquierda
        new Vector3(0.85f, 0.5f, safeDistance), // derecha
    };

            float strongestPushBack = 0f;

            for (int i = 0; i < viewportPoints.Length; i++)
            {
                Vector3 pointWorld = cam.ViewportToWorldPoint(viewportPoints[i]);

                Vector3 direction = pointWorld - cameraWorldPos;
                float distance = direction.magnitude;

                if (distance <= 0.001f)
                    continue;

                direction.Normalize();

                if (Physics.SphereCast(
                        cameraWorldPos,
                        sphereRadius * 0.25f,
                        direction,
                        out RaycastHit hit,
                        distance,
                        mask,
                        QueryTriggerInteraction.Ignore))
                {
                    float pushBack = distance - hit.distance;

                    if (pushBack > strongestPushBack)
                        strongestPushBack = pushBack;
                }
            }

            if (strongestPushBack > 0f)
            {
                // Empujamos la cámara hacia atrás en su eje local Z.
                // En primera persona esto evita que el near plane corte paredes.
                desiredLocalPos += new Vector3(0f, 0f, -strongestPushBack);
            }

            return desiredLocalPos;
        }

        // -------------------------
        // Setup helpers
        // -------------------------

        private void FindCameraIfNeeded()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void ApplyImmediate()
        {
            // Recalcular estado inicial por si se cambió el bool en Inspector.
            _shoulderSign = startOnRightShoulder ? 1 : -1;

            _isFirstPerson = false;
            _targetDistance = thirdPersonDistance;
            _targetHeight = thirdPersonHeight;

            CurrentDistance = _targetDistance;
            _currentShoulderOffset = _shoulderSign * thirdPersonShoulderOffset;

            // CameraPivot: altura.
            transform.localPosition = new Vector3(0f, _targetHeight, 0f);

            // MainCamera: hombro + distancia.
            if (cameraTransform != null)
            {
                cameraTransform.localPosition = new Vector3(
                    _currentShoulderOffset,
                    0f,
                    -_targetDistance
                );
            }
        }
    }
}
