using UnityEngine;
using JuegoCriminal.Interaction;
using JuegoCriminal.Services;

namespace JuegoCriminal.World
{
    public sealed class PropertyMarker : MonoBehaviour
    {
        [Header("Property Data")]
        public int propertyId = 1;
        public int price = 500;

        [Header("Refs")]
        [SerializeField] private InteractableObject interactableObject;
        [SerializeField] private PropertyVisual propertyVisual;

        [Header("Prompt Text")]
        [SerializeField] private string buyText = "Buy";
        [SerializeField] private string ownedText = "Owned";
        [SerializeField] private string needMoneyText = "Need";

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        private EconomyService _economy;
        private PropertyService _properties;

        public bool IsOwned
        {
            get
            {
                EnsureServices();
                return _properties != null && _properties.IsOwned(propertyId);
            }
        }

        private void Reset()
        {
            interactableObject = GetComponent<InteractableObject>();
            propertyVisual = GetComponentInChildren<PropertyVisual>();
        }

        private void Awake()
        {
            if (interactableObject == null)
                interactableObject = GetComponent<InteractableObject>();

            if (propertyVisual == null)
                propertyVisual = GetComponentInChildren<PropertyVisual>();

            EnsureServices();
        }

        private void OnEnable()
        {
            EnsureServices();

            if (_economy != null)
                _economy.OnMoneyChanged += HandleMoneyChanged;
        }

        private void OnDisable()
        {
            if (_economy != null)
                _economy.OnMoneyChanged -= HandleMoneyChanged;
        }

        private void Start()
        {
            RefreshState();
        }

        private void EnsureServices()
        {
            if (_economy == null)
                _economy = FindAnyObjectByType<EconomyService>();

            if (_properties == null)
                _properties = FindAnyObjectByType<PropertyService>();
        }

        private void HandleMoneyChanged(int money)
        {
            RefreshState();
        }

        public void BuyFromInteraction()
        {
            TryBuy();
        }

        public bool TryBuy()
        {
            EnsureServices();

            if (_economy == null || _properties == null)
            {
                if (debugLogs)
                    Debug.LogWarning($"PropertyMarker: faltan servicios en {name}.", this);

                return false;
            }

            if (_properties.IsOwned(propertyId))
            {
                RefreshState();
                return false;
            }

            if (!_economy.TrySpend(price))
            {
                RefreshState();
                return false;
            }

            _properties.AddOwned(propertyId);

            if (debugLogs)
                Debug.Log($"PropertyMarker: propiedad {propertyId} comprada por ${price}.", this);

            RefreshState();

            return true;
        }

        public void RefreshState()
        {
            EnsureServices();

            bool owned = IsOwned;
            int money = _economy != null ? _economy.Money : 0;
            bool canAfford = money >= price;

            if (propertyVisual != null)
            {
                if (owned)
                    propertyVisual.ShowSold();
                else
                    propertyVisual.ShowForSale();
            }

            if (interactableObject == null)
                return;

            if (owned)
            {
                interactableObject.SetInteractionText(ownedText);
                interactableObject.SetCanInteract(false);
                interactableObject.SetCanShowPrompt(false);
                return;
            }

            interactableObject.SetCanShowPrompt(true);

            if (canAfford)
            {
                interactableObject.SetInteractionText($"{buyText} ${price}");
                interactableObject.SetCanInteract(true);
            }
            else
            {
                interactableObject.SetInteractionText($"{needMoneyText} ${price}");
                interactableObject.SetCanInteract(false);
            }
        }
    }
}