using System;
using UnityEngine;
namespace JuegoCriminal.Construction
{
    public enum BuildCategory { Walls, Furniture }
    [Serializable] public sealed class BuildEntry
    {
        public string id;
        public string title;
        public BuildCategory category;
        public GameObject prefab;
        public Bounds bounds;
        public bool hiddenInMenu;
    }
    [CreateAssetMenu(menuName="Criminal Game/Build Catalog")]
    public sealed class BuildCatalog : ScriptableObject
    {
        public BuildEntry[] entries = Array.Empty<BuildEntry>();
        public Material[] finishes = Array.Empty<Material>();
        public BuildEntry Find(string id) => Array.Find(entries, e => e.id == id);
    }
}
