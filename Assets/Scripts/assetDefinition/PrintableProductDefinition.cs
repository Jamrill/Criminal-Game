using UnityEngine;

namespace JuegoCriminal.Printing
{
    [CreateAssetMenu(
        fileName = "Product_New",
        menuName = "Juego Criminal/Printing/Printable Product"
    )]
    public sealed class PrintableProductDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "product_new";
        [SerializeField] private string displayName = "New Product";

        [Header("Visuals")]
        [SerializeField] private Sprite inventorySprite;
        [SerializeField] private Sprite workbenchPreviewSprite;

        [Header("Prefab")]
        [SerializeField] private GameObject assembledPrefab;

        [Header("Requirements")]
        [Min(1)]
        [SerializeField] private int requiredWorkbenchLevel = 1;

        [SerializeField] private bool unlockedByDefault = true;

        [Header("Required Parts")]
        [SerializeField] private PrintablePartDefinition[] requiredParts;

        [Header("Economy")]
        [Min(0)]
        [SerializeField] private int baseSellValue = 100;

        public string Id => id;
        public string DisplayName => displayName;

        public Sprite InventorySprite => inventorySprite;
        public Sprite WorkbenchPreviewSprite => workbenchPreviewSprite;

        public GameObject AssembledPrefab => assembledPrefab;

        public int RequiredWorkbenchLevel => requiredWorkbenchLevel;
        public bool UnlockedByDefault => unlockedByDefault;

        public PrintablePartDefinition[] RequiredParts => requiredParts;

        public int BaseSellValue => baseSellValue;
    }
}