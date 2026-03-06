using UnityEngine;

namespace JuegoCriminal.Player
{
    public sealed class Interactor : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private LayerMask interactMask;

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
                TryInteract();
        }

        private void TryInteract()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactMask))
            {
                // Busca un componente interactuable en el objeto hit
                var buy = hit.collider.GetComponent<JuegoCriminal.World.PropertyMarker>();
                if (buy != null)
                {
                    buy.TryBuyFromInteractor(); // método nuevo
                }
            }
        }
    }
}