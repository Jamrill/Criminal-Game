using UnityEngine;

namespace JuegoCriminal.Inventory
{
    [CreateAssetMenu(fileName = "Clothing_New", menuName = "Juego Criminal/Inventory/Clothing")]
    public sealed class ClothingItemDefinition : InventoryItemDefinition
    {
        [SerializeField] private GameObject equippedVisualPrefab;
        public GameObject EquippedVisualPrefab => equippedVisualPrefab;
    }
}
