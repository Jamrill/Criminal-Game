using UnityEngine;
using JuegoCriminal.Services;

namespace JuegoCriminal.World
{
    public sealed class PropertyMarker : MonoBehaviour
    {
        public int propertyId = 1;
        public int price = 500;

        private EconomyService _economy;
        private PropertyService _properties;

        private void Awake()
        {
            // No pasa nada si aquí es null; lo reintentamos cuando interactúen
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

        public string GetPromptText(int money)
        {
            if (IsOwned) return "Owned";
            if (money < price) return $"Need ${price}";
            return $"Press E to buy (${price})";
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