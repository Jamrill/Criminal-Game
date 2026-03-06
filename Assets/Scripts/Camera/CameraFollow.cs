using UnityEngine;

namespace JuegoCriminal.CameraSystem
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float followSpeed = 12f;
        [SerializeField] private float yawFollowSpeed = 12f;

        private JuegoCriminal.CameraSystem.CameraBoomCollision _boom;

        private bool _snapped;

        private void Awake()
        {
            _boom = GetComponentInChildren<JuegoCriminal.CameraSystem.CameraBoomCollision>(true);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                var p0 = GameObject.Find("Player_0");
                if (p0 != null) target = p0.transform;
                else
                {
                    var p = GameObject.FindGameObjectWithTag("Player");
                    if (p != null) target = p.transform;
                }
                if (target == null) return;
            }

            bool firstPerson = (_boom != null && _boom.CurrentDistance <= 0.35f);

            // Snap al entrar (1 frame) — mejor que respete el modo actual
            if (!_snapped)
            {
                transform.position = target.position;

                float yaw = target.eulerAngles.y;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                _snapped = true;
                // no return: dejamos que aplique la lógica de este frame también
            }

            // 1) Posición
            if (firstPerson)
                transform.position = target.position; // pegado, sin delay
            else
                transform.position = Vector3.Lerp(transform.position, target.position, followSpeed * Time.deltaTime);

            // 2) Rotación (yaw)
            float targetYaw = target.eulerAngles.y;

            if (firstPerson)
            {
                // En 1ª persona: sin suavizado, para que no "se adelante" el cuerpo/cámara
                transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            }
            else
            {
                float currentYaw = transform.eulerAngles.y;
                float newYaw = Mathf.LerpAngle(currentYaw, targetYaw, yawFollowSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }
        }
    }
}