using UnityEngine;

namespace JuegoCriminal.Inventory
{
    [CreateAssetMenu(fileName = "Consumable_New", menuName = "Juego Criminal/Inventory/Consumable")]
    public sealed class ConsumableItemDefinition : InventoryItemDefinition
    {
        [SerializeField, Min(0f)] private float healingAmount;
        public float HealingAmount => healingAmount;
    }
}
