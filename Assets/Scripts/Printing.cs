using UnityEngine;

namespace JuegoCriminal.Printing
{
    [CreateAssetMenu(
        fileName = "PrintRecipe_New",
        menuName = "Juego Criminal/Printing/Print Recipe"
    )]
    public sealed class PrintRecipe : ScriptableObject
    {
        [Header("Info")]
        public string displayName = "New Print";
        public Sprite previewSprite;

        [Header("Requirements")]
        [Min(1)] public int requiredPrinterLevel = 1;
        public bool unlockedByDefault = true;

        [Header("Printing")]
        [Min(0.1f)] public float printDuration = 8f;
        public GameObject completedPrintPrefab;

        [Header("Future Economy")]
        public int materialCost = 0;
        public int sellValue = 100;
    }
}