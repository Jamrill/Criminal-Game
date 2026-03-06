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

        private void Update()
        {
            if (!_playerInside) return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("[Property] E pressed, trying to buy...");
                TryBuy();
            }
        }

        public void TryBuyFromInteractor()
        {
            TryBuy();
        }

        private void TryBuy()
        {
            Debug.Log($"[Property] TryBuy called. economy={_economy != null}, properties={_properties != null}");

            if (_economy == null || _properties == null) return;

            Debug.Log($"[Property] Owned? {_properties.IsOwned(propertyId)}  Price={price}  Money={_economy.Money}");

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

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInside = true;
                Debug.Log("[Property] Player inside = TRUE");
            }
        }
        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInside = false;
                Debug.Log("[Property] Player inside = FALSE");
            }
        }
    }
}