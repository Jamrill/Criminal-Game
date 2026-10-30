using System.Collections.Generic;
using UnityEngine;

namespace JuegoCriminal.Inventory
{
    [CreateAssetMenu(fileName = "InventoryCatalog", menuName = "Juego Criminal/Inventory/Item Catalog")]
    public sealed class InventoryItemCatalog : ScriptableObject
    {
        [SerializeField] private InventoryItemDefinition[] items;
        private Dictionary<string, InventoryItemDefinition> _byId;

        public InventoryItemDefinition Find(string id)
        {
            if (_byId == null) BuildIndex();
            return !string.IsNullOrWhiteSpace(id) && _byId.TryGetValue(id, out var item) ? item : null;
        }

        private void BuildIndex()
        {
            _byId = new Dictionary<string, InventoryItemDefinition>();
            if (items == null) return;
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null && !string.IsNullOrWhiteSpace(items[i].Id)) _byId[items[i].Id] = items[i];
        }

        private void OnValidate() => _byId = null;
    }
}
