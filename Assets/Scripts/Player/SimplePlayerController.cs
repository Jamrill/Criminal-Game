using UnityEngine;

namespace JuegoCriminal.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SimplePlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float gravity = -9.81f;

        private CharacterController _cc;
        private float _verticalVelocity;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S

            Vector3 move = new Vector3(h, 0f, v);
            if (move.sqrMagnitude > 1f) move.Normalize();

            // movimiento en el plano XZ (sin cámara por ahora)
            Vector3 velocity = move * moveSpeed;

            // gravedad
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f; // pegado al suelo
            _verticalVelocity += gravity * Time.deltaTime;

            velocity.y = _verticalVelocity;

            _cc.Move(velocity * Time.deltaTime);
        }
    }
}
