using UnityEngine;
using JuegoCriminal.Services;

namespace JuegoCriminal.World
{
    [RequireComponent(typeof(Collider))]
    public sealed class PropertyMarker : MonoBehaviour
    {
        public int propertyId = 1;
        public int price = 500;

        private EconomyService _economy;
        private PropertyService _properties;
        private bool _playerInside;

        private void Awake()
        {
            _economy = FindAnyObjectByType<EconomyService>();
            _properties = FindAnyObjectByType<PropertyService>();

            // Asegura trigger
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                _playerInside = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
                _playerInside = false;
        }

        private void Update()
        {
            if (!_playerInside) return;
            if (Input.GetKeyDown(KeyCode.E))
                TryBuy();
        }

        private void TryBuy()
        {
            if (_economy == null || _properties == null) return;

            if (_properties.IsOwned(propertyId))
            {
                Debug.Log($"[Property] Already owned: {propertyId}");
                return;
            }

            if (!_economy.TrySpend(price))
            {
                Debug.Log($"[Property] Not enough money. Need {price}");
                return;
            }

            _properties.AddOwned(propertyId);
            Debug.Log($"[Property] Purchased property {propertyId} for {price}");
        }
    }
}