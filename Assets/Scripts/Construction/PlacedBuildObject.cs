using System;
using UnityEngine;
namespace JuegoCriminal.Construction
{
    [Serializable] public sealed class BuildRecord
    {
        public string id, definitionId, sceneName;
        public int propertyId, finish;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public Vector3 cellCenter;
    }
    public sealed class PlacedBuildObject : MonoBehaviour
    {
        public string persistentId, definitionId;
        public int propertyId, finish;
        public Vector3 cellCenter;
        public bool runtimePlaced;
        public BuildRecord Capture() => new BuildRecord { id=persistentId,definitionId=definitionId,propertyId=propertyId,finish=finish,
            position=transform.position,rotation=transform.rotation,scale=transform.localScale,cellCenter=cellCenter,sceneName=gameObject.scene.name };
        public void Restore(BuildRecord r, BuildCatalog catalog)
        {
            persistentId=r.id; definitionId=r.definitionId; propertyId=r.propertyId; finish=r.finish; cellCenter=r.cellCenter;
            transform.SetPositionAndRotation(r.position,r.rotation); transform.localScale=r.scale;
            WallTopology.MarkDirty();
            if (finish>=0 && finish<catalog.finishes.Length)
                foreach(var surface in GetComponentsInChildren<BuildSurface>(true)) surface.Apply(catalog.finishes[finish]);
        }
    }
}
