using System.Collections.Generic;
using UnityEngine;
using JuegoCriminal.Services;
namespace JuegoCriminal.Construction
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BuildPlot : MonoBehaviour
    {
        public int propertyId = -1;
        public string displayName;
        [Min(0)] public int price = 1;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public bool buildable;
        [Tooltip("Origen y altura del suelo de la rejilla; usar escala 1 y rotacion solo Y.")]
        public Transform gridOrigin;
        public const float CellSize = 4;
        public static readonly HashSet<BuildPlot> Active = new HashSet<BuildPlot>();
        public BoxCollider Volume => GetComponent<BoxCollider>();
        public Transform Grid => gridOrigin ? gridOrigin : transform;
        private void Reset() { Volume.isTrigger = true; Volume.size = new Vector3(32,12,32); Volume.center = new Vector3(0,6,0); }
        private void OnEnable() { Active.Add(this); }
        private void OnDisable() { Active.Remove(this); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Clear() => Active.Clear();
        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world) - Volume.center;
            Vector3 half = Volume.size * .5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
        }
        public bool CanBuild(PropertyService properties) => buildable && propertyId >= 0 && properties && properties.IsOwned(propertyId);
        // The player's pivot can sit below floor level while its capsule is inside.
        // Keep the strict volume check for placed geometry; use the body for occupants.
        public bool ContainsPlayer(Transform player, CharacterController capsule)
            => Contains(capsule ? capsule.transform.TransformPoint(capsule.center) : player.position);
        public Vector2Int Cell(Vector3 world)
        {
            var p = Grid.InverseTransformPoint(world);
            return new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
        }
        public Vector3 Center(Vector2Int cell) => Grid.TransformPoint(new Vector3((cell.x+.5f)*CellSize,0,(cell.y+.5f)*CellSize));
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix; Gizmos.color = buildable ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(Volume.center, Volume.size);
        }
    }
}
