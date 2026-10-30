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
            price=GetPurchasePrice(propertyId,price);
            if (_save == null || !_save.HasCurrentGame || propertyId < 0 || price < 0 || IsOwned(propertyId)) return false;
            var economy = GetComponent<EconomyService>();
            if (!economy || !economy.TrySpend(price)) return false;
            if (AddOwned(propertyId)) return true;
            economy.AddMoney(price);
            return false;
        }

        public int GetPurchasePrice(int propertyId,int fallback)
        {
            // Both the tablet and the old for-sale signs use the same valuation.
            JuegoCriminal.Construction.BuildPlot match=null;
            foreach(var plot in JuegoCriminal.Construction.BuildPlot.Active)
                if(plot && plot.propertyId==propertyId)
                {
                    if(match)return int.MaxValue; // Duplicate IDs must never allow a cheap purchase.
                    match=plot;
                }
            return match ? match.TotalValue : fallback;
        }
    }
}
