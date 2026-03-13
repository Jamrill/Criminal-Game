using UnityEngine;
using JuegoCriminal.Services;
using JuegoCriminal.World;
using JuegoCriminal.UI;

namespace JuegoCriminal.Player
{
    public sealed class InteractorRaycast : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private LayerMask interactMask;

        [Header("World Prompt Prefab")]
        [SerializeField] private WorldPromptUI promptPrefab;

        [Header("Icon")]
        [SerializeField] private Sprite iconCanBuy; // tu sprite "E"

        private EconomyService _economy;
        private Camera _cam;

        private PropertyMarker _current;
        private WorldPromptUI _prompt;

        private void Awake()
        {
            _economy = FindAnyObjectByType<EconomyService>();
            _cam = Camera.main;
        }

        private void Update()
        {
            // No interactuar si hay pausa/menú
            if (Time.timeScale == 0f || Cursor.lockState != CursorLockMode.Locked)
            {
                HidePrompt();
                return;
            }

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            _current = GetLookAtProperty();
            if (_current == null)
            {
                HidePrompt();
                return;
            }

            int money = _economy != null ? _economy.Money : 0;

            // Solo mostramos el prompt si:
            // - NO está comprada
            // - y tienes dinero suficiente
            if (_current.IsOwned || money < _current.price)
            {
                HidePrompt();
                return;
            }

            // Mostrar prompt y asignar icono "E"
            ShowPromptOver(_current);
            if (_prompt != null) _prompt.SetIcon(iconCanBuy);

            // Comprar
            if (Input.GetKeyDown(KeyCode.E))
            {
                bool bought = _current.TryBuy();
                if (bought)
                    HidePrompt();
            }
        }

        private PropertyMarker GetLookAtProperty()
        {
            if (_cam == null) return null;

            // Centro de pantalla (crosshair)
            Ray ray = _cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactMask, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<PropertyMarker>();

            return null;
        }

        private void ShowPromptOver(PropertyMarker marker)
        {
            if (promptPrefab == null) return;

            if (_prompt == null)
                _prompt = Instantiate(promptPrefab);

            // Anchor opcional (Empty hijo llamado PromptAnchor)
            Transform anchor = marker.transform.Find("PromptAnchor");
            if (anchor == null) anchor = marker.transform;

            _prompt.gameObject.SetActive(true);
            _prompt.Attach(anchor, _cam);
        }

        private void HidePrompt()
        {
            if (_prompt != null)
                _prompt.gameObject.SetActive(false);
        }
    }
}