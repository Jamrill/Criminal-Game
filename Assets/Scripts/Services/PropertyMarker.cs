using UnityEngine;
using JuegoCriminal.Services;
using JuegoCriminal.Interaction;

namespace JuegoCriminal.World
{
    public sealed class PropertyMarker : MonoBehaviour, IInteractable
    {
        public int propertyId = 1;
        public int price = 500;

        private EconomyService _economy;
        private PropertyService _properties;

        private void Awake()
        {
            _economy = FindAnyObjectByType<EconomyService>();
            _properties = FindAnyObjectByType<PropertyService>();
        }

        private void EnsureServices()
        {
            if (_economy == null) _economy = FindAnyObjectByType<EconomyService>();
            if (_properties == null) _properties = FindAnyObjectByType<PropertyService>();
        }

        public bool IsOwned
        {
            get
            {
                EnsureServices();
                return _properties != null && _properties.IsOwned(propertyId);
            }
        }

        public bool CanShowPrompt()
        {
            return !IsOwned;
        }

        public bool CanInteract()
        {
            EnsureServices();

            if (_economy == null || _properties == null)
                return false;

            if (IsOwned)
                return false;

            return _economy.Money >= price;
        }

        public void Interact()
        {
            TryBuy();
        }

        public string GetInteractionText()
        {
            EnsureServices();

            int money = _economy != null ? _economy.Money : 0;

            if (IsOwned) return "Owned";
            if (money < price) return $"Need ${price}";
            return $"Buy ${price}";
        }

        public bool TryBuy()
        {
            EnsureServices();
            if (_economy == null || _properties == null) return false;

            if (_properties.IsOwned(propertyId)) return false;
            if (!_economy.TrySpend(price)) return false;

            _properties.AddOwned(propertyId);

            var visual = GetComponentInChildren<PropertyVisual>();
            if (visual != null) visual.Refresh();

            return true;
        }
    }
}