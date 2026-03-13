using UnityEngine;

namespace JuegoCriminal.CameraSystem
{
    public sealed class CameraBoomCollision : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform; // arrastra Main Camera
        [SerializeField] private float desiredDistance = 6f;
        [SerializeField] private float sphereRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = ~0; // todo
        [SerializeField] private float smooth = 20f;

        [Header("Zoom")]
        [SerializeField] private float minDistance = 0.2f;
        [SerializeField] private float maxDistance = 6f;
        [SerializeField] private float zoomSpeed = 2f;

        [Header("Heights")]
        [SerializeField] private float thirdPersonHeight = 2.0f;
        [SerializeField] private float firstPersonHeight = 1.75f;
        [SerializeField] private float firstPersonThreshold = 0.35f;

        public float CurrentDistance { get; private set; }

        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void LateUpdate()
        {
            // 1) Zoom con rueda
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                desiredDistance = Mathf.Clamp(desiredDistance - scroll * zoomSpeed, minDistance, maxDistance);

            if (cameraTransform == null) return;

            // 2) Colisión: desde pivot hacia atrás (evitamos chocar con el propio Player)
            Vector3 origin = transform.position + Vector3.up * 0.5f; // pequeño offset para evitar suelo
            Vector3 dir = -transform.forward;

            int mask = collisionMask & ~LayerMask.GetMask("Player");
            float targetDist = desiredDistance;

            if (Physics.SphereCast(origin, sphereRadius, dir, out RaycastHit hit, desiredDistance, mask, QueryTriggerInteraction.Ignore))
            {
                // Acercar la cámara para no atravesar
                targetDist = Mathf.Max(minDistance, hit.distance);
            }

            // 3) Altura según 1ª / 3ª persona
            float height = (targetDist <= firstPersonThreshold) ? firstPersonHeight : thirdPersonHeight;

            // Cámara detrás del pivot
            Vector3 desiredLocal = new Vector3(0f, height, -targetDist);

            // 4) Aplicar (suave)
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                desiredLocal,
                smooth * Time.deltaTime
            );

            CurrentDistance = targetDist;
        }
    }
}