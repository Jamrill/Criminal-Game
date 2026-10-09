using System;
using UnityEngine;
namespace JuegoCriminal.Construction
{
    // Explicit slots preserve glass, trim and door materials when repainting.
    public sealed class BuildSurface : MonoBehaviour
    {
        [Serializable] public struct Slot { public Renderer renderer; public int index; }
        public Slot[] slots = Array.Empty<Slot>();
        public void Apply(Material material)
        {
            if (!material) return;
            foreach (var slot in slots)
            {
                if (!slot.renderer) continue;
                var materials = slot.renderer.sharedMaterials;
                if (slot.index < 0 || slot.index >= materials.Length) continue;
                materials[slot.index] = material; slot.renderer.sharedMaterials = materials;
            }
        }
    }
}
