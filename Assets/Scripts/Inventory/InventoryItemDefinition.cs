using UnityEngine;

namespace JuegoCriminal.Inventory
{
    public enum InventoryItemType { WeaponPart, Weapon, Consumable, Clothing, Miscellaneous }
    public enum EquipmentSlot { None, Pants, Shirt, Jacket, Backpack }

    public abstract class InventoryItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "item_new";
        [SerializeField] private string displayName = "New Item";
        [SerializeField] private Sprite inventorySprite;
        [SerializeField] private InventoryItemType itemType;

        [Header("Inventory Shape")]
        [SerializeField, Min(1)] private int gridWidth = 1;
        [SerializeField, Min(1)] private int gridHeight = 1;
        [Tooltip("Vacío = rectángulo completo. Orden por filas, desde arriba a la izquierda.")]
        [SerializeField] private bool[] occupiedCells;
        [SerializeField] private bool canRotate = true;

        [Header("Equipment")]
        [SerializeField] private EquipmentSlot equipmentSlot;
        [SerializeField, Min(0)] private int inventoryCapacityBonus;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite InventorySprite => inventorySprite;
        public InventoryItemType ItemType => itemType;
        public int GridWidth => Mathf.Max(1, gridWidth);
        public int GridHeight => Mathf.Max(1, gridHeight);
        public bool CanRotate => canRotate;
        public EquipmentSlot EquipmentSlot => equipmentSlot;
        public int InventoryCapacityBonus => Mathf.Max(0, inventoryCapacityBonus);
        public int Width(bool rotated) => rotated ? GridHeight : GridWidth;
        public int Height(bool rotated) => rotated ? GridWidth : GridHeight;

        public bool Occupies(int x, int y, bool rotated)
        {
            int sourceX = rotated ? y : x;
            int sourceY = rotated ? GridHeight - 1 - x : y;
            if (sourceX < 0 || sourceX >= GridWidth || sourceY < 0 || sourceY >= GridHeight) return false;
            return occupiedCells == null || occupiedCells.Length != GridWidth * GridHeight
                || occupiedCells[sourceY * GridWidth + sourceX];
        }
    }
}
