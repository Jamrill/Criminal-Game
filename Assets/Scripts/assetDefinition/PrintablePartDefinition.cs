using UnityEngine;

namespace JuegoCriminal.Printing
{
    [CreateAssetMenu(
        fileName = "Part_New",
        menuName = "Juego Criminal/Printing/Printable Part"
    )]
    public sealed class PrintablePartDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "part_new";
        [SerializeField] private string displayName = "New Part";

        [Header("Visuals")]
        [SerializeField] private Sprite inventorySprite;
        [SerializeField] private Sprite printerPreviewSprite;

        [Header("Prefab")]
        [SerializeField] private GameObject printedPartPrefab;

        [Header("Requirements")]
        [Min(1)]
        [SerializeField] private int requiredPrinterLevel = 1;

        [SerializeField] private bool unlockedByDefault = true;

        [Header("Printing")]
        [Min(0.1f)]
        [SerializeField] private float printDuration = 8f;

        [Header("Cost")]
        [Min(0f)]
        [SerializeField] private float materialCost = 0.5f;

        public string Id => id;
        public string DisplayName => displayName;

        public Sprite InventorySprite => inventorySprite;
        public Sprite PrinterPreviewSprite => printerPreviewSprite;

        public GameObject PrintedPartPrefab => printedPartPrefab;

        public int RequiredPrinterLevel => requiredPrinterLevel;
        public bool UnlockedByDefault => unlockedByDefault;

        public float PrintDuration => printDuration;
        public float MaterialCost => materialCost;
    }
}