using UnityEngine;

namespace JuegoCriminal.Services
{
    public sealed class PropertyService : MonoBehaviour
    {
        private SaveService _save;
        public event System.Action<int> OnOwnershipChanged;

        private void Awake()
        {
            _save = GetComponent<SaveService>();
        }

        public bool IsOwned(int propertyId)
        {
            return _save != null && _save.IsPropertyOwned(propertyId);
        }

        public bool AddOwned(int propertyId)
        {
            if (_save == null || !_save.TryAddOwnedProperty(propertyId)) return false;
            OnOwnershipChanged?.Invoke(propertyId);
            return true;
        }

        public bool TryBuy(int propertyId, int price)
        {
            if (_save == null || !_save.HasCurrentGame || propertyId < 0 || price < 0 || IsOwned(propertyId)) return false;
            var economy = GetComponent<EconomyService>();
            if (!economy || !economy.TrySpend(price)) return false;
            if (AddOwned(propertyId)) return true;
            economy.AddMoney(price);
            return false;
        }
    }
}
