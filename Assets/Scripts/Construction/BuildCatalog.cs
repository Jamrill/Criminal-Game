using System;
using UnityEngine;
namespace JuegoCriminal.Construction
{
    public enum BuildCategory { Walls, Furniture }
    public enum BuildMount { Ground, Doorway, Ceiling }
    [Serializable] public sealed class BuildEntry
    {
        public string id;
        public string title;
        public BuildCategory category;
        public GameObject prefab;
        public Bounds bounds;
        public bool hiddenInMenu;
        [Min(0)] public int price;
        public BuildMount mount;
        [Tooltip("Reserved 4x4 cells along the prefab's local X/Z axes. Rotation rotates this footprint.")]
        public Vector2Int footprintCells=Vector2Int.one;
        [Tooltip("Hueco disponible en el marco de colocacion del prefab; solo paredes con hueco de puerta.")]
        public Bounds doorway;
        public bool IsOpening => prefab && (prefab.name.StartsWith("Wall_door_hole",StringComparison.OrdinalIgnoreCase) || prefab.name.StartsWith("Window_Wall",StringComparison.OrdinalIgnoreCase));
    }
    [CreateAssetMenu(menuName="Criminal Game/Build Catalog")]
    public sealed class BuildCatalog : ScriptableObject
    {
        public BuildEntry[] entries = Array.Empty<BuildEntry>();
        public Material[] finishes = Array.Empty<Material>();
        [Tooltip("Editor only. Ignored in every exported build; real catalogue prices are unchanged.")]
        public bool developmentFreeConstruction;
        public bool FreeConstruction
        {
            get
            {
#if UNITY_EDITOR
                return developmentFreeConstruction;
#else
                return false;
#endif
            }
        }
        public int Cost(BuildEntry entry) => FreeConstruction || entry==null ? 0 : Mathf.Max(0,entry.price);
        public BuildEntry Find(string id) => Array.Find(entries, e => e.id == id);
    }
}
