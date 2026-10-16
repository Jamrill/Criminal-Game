using System;
using UnityEngine;
namespace JuegoCriminal.Construction
{
    [Serializable] public sealed class BuildRecord
    {
        public string id, definitionId, sceneName;
        public int propertyId, finish;
        public bool deleted;
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
        public bool deleted;
        public BuildRecord Capture() => new BuildRecord { id=persistentId,definitionId=definitionId,propertyId=propertyId,finish=finish,
            position=transform.position,rotation=transform.rotation,scale=transform.lossyScale,cellCenter=cellCenter,sceneName=gameObject.scene.name,deleted=deleted };
        public void Demolish() { deleted=true;gameObject.SetActive(false);WallTopology.MarkDirty(); }
        public void Restore(BuildRecord r, BuildCatalog catalog)
        {
            persistentId=r.id; definitionId=r.definitionId; propertyId=r.propertyId; finish=r.finish; cellCenter=r.cellCenter;
            deleted=r.deleted;gameObject.SetActive(!deleted);
            // Records use world transforms, including old saves whose objects were roots.
            transform.SetParent(null,true);
            transform.SetPositionAndRotation(r.position,r.rotation); transform.localScale=r.scale;
            ConstructionPersistence.ParentToPlot(this);
            WallTopology.MarkDirty();
            if (finish>=0 && finish<catalog.finishes.Length)
                foreach(var surface in GetComponentsInChildren<BuildSurface>(true)) surface.Apply(catalog.finishes[finish]);
        }
    }
}
