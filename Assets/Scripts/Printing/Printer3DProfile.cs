using UnityEngine;

namespace JuegoCriminal.Printing
{
    [CreateAssetMenu(fileName = "Printer_New", menuName = "Juego Criminal/Printing/Printer Profile")]
    public sealed class Printer3DProfile : ScriptableObject
    {
        [SerializeField] private string displayName = "3D Printer";
        [SerializeField, Min(1)] private int printerLevel = 1;
        [Tooltip("2 imprime el doble de rápido; 0.5 tarda el doble.")]
        [SerializeField, Min(0.01f)] private float printSpeedMultiplier = 1f;

        public string DisplayName => displayName;
        public int PrinterLevel => printerLevel;
        public float PrintSpeedMultiplier => Mathf.Max(0.01f, printSpeedMultiplier);
    }
}
