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

        [Header("Distance for first/third person")]
        [SerializeField] private float minDistance = 0.2f;
        [SerializeField] private float maxDistance = 6f;
        [SerializeField] private float zoomSpeed = 2f;

        [Header("Heights")]
        [SerializeField] private float thirdPersonHeight = 2.0f;
        [SerializeField] private float firstPersonHeight = 1.75f;   // <- ajusta aquí
        [SerializeField] private float firstPersonThreshold = 0.35f; // distancia <= esto = 1ª persona


        public float CurrentDistance { get; private set; }

        private Vector3 _defaultLocalPos;

        private void Awake()
        {
            if (cameraTransform == null)
                cameraTransform = Camera.main != null ? Camera.main.transform : null;

            if (cameraTransform != null)
                _defaultLocalPos = cameraTransform.localPosition;
        }

        private void LateUpdate()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                desiredDistance = Mathf.Clamp(desiredDistance - scroll * zoomSpeed, minDistance, maxDistance);
            }

            if (cameraTransform == null) return;

            // Dirección hacia la cámara (local -Z del pivot)
            Vector3 origin = transform.position;
            Vector3 dir = -transform.forward;

            float targetDist = desiredDistance;

            int mask = collisionMask & ~LayerMask.GetMask("Player");

            if (Physics.SphereCast(origin, sphereRadius, dir, out RaycastHit hit, desiredDistance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                Physics.SphereCast(origin, sphereRadius, dir, out hit, desiredDistance, mask, QueryTriggerInteraction.Ignore);
                // Acercar la cámara para no atravesar
                targetDist = Mathf.Max(0.5f, hit.distance);
            }

            // La cámara está detrás del pivot
            float height = (targetDist <= firstPersonThreshold) ? firstPersonHeight : thirdPersonHeight;
            Vector3 desiredLocal = new Vector3(0f, height, -targetDist);

            /*Vector3 desiredLocal = new Vector3(0f, _defaultLocalPos.y, -targetDist);

            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, desiredLocal, smooth * Time.deltaTime);*/

            CurrentDistance = targetDist;
        }
    }
}