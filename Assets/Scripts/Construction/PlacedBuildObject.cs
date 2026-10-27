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
        public Vector3[] floorTriangles;
    }
    public sealed class PlacedBuildObject : MonoBehaviour
    {
        public string persistentId, definitionId;
        public int propertyId, finish;
        public Vector3 cellCenter;
        public bool runtimePlaced;
        public bool deleted;
        public BuildRecord Capture() => new BuildRecord { id=persistentId,definitionId=definitionId,propertyId=propertyId,finish=finish,
            position=transform.position,rotation=transform.rotation,scale=transform.lossyScale,cellCenter=cellCenter,sceneName=gameObject.scene.name,deleted=deleted,
            floorTriangles=GetComponent<RoomFloorPatch>() is RoomFloorPatch patch?patch.triangles:null };
        // Nested floor landings belong to Stairs, not to a separately editable tile.
        public bool ProtectedFloor=>GetComponent<BuildFloorCoverage>();
        public void Demolish()
        {
            if(deleted || (ProtectedFloor && !FloorEditing.CanRemove(this)))return;
            if(string.IsNullOrEmpty(persistentId))persistentId=Guid.NewGuid().ToString("N");
            var record=Capture();record.deleted=true;BuildDeletionLedger.RemoveObject(this,record);
        }
        public void Restore(BuildRecord r, BuildCatalog catalog)
        {
            if(r.deleted){BuildDeletionLedger.RemoveObject(this,r);return;}
            persistentId=r.id; definitionId=r.definitionId; propertyId=r.propertyId; finish=r.finish; cellCenter=r.cellCenter;
            deleted=r.deleted;gameObject.SetActive(!deleted);
            // Records use world transforms, including old saves whose objects were roots.
            transform.SetParent(null,true);
            transform.SetPositionAndRotation(r.position,r.rotation); transform.localScale=r.scale;
            ConstructionPersistence.ParentToPlot(this);
            var plot=GetComponentInParent<BuildPlot>();if(plot)BuildDeletionLedger.For(plot).Forget(r.id);
            if(r.floorTriangles!=null && r.floorTriangles.Length>=3)
            {
                var patch=GetComponent<RoomFloorPatch>();
                if(!patch)
                {
                    var renderer=GetComponentInChildren<MeshRenderer>(true);
                    var mats=renderer?renderer.sharedMaterials:System.Array.Empty<Material>();
                    patch=gameObject.AddComponent<RoomFloorPatch>();patch.materials=mats;
                }
                patch.triangles=r.floorTriangles;patch.Rebuild();
            }
            else if(GetComponent<RoomFloorPatch>() is RoomFloorPatch oldPatch)
            {
                oldPatch.enabled=false;
                foreach(var renderer in GetComponentsInChildren<Renderer>(true))if(renderer.gameObject.hideFlags==HideFlags.None)renderer.enabled=true;
                foreach(var collider in GetComponentsInChildren<Collider>(true))if(collider.gameObject.hideFlags==HideFlags.None)collider.enabled=true;
                if(Application.isPlaying)Destroy(oldPatch);else DestroyImmediate(oldPatch);
            }
            WallTopology.MarkDirty();
            if (finish>=0 && finish<catalog.finishes.Length)
                foreach(var surface in GetComponentsInChildren<BuildSurface>(true)) surface.Apply(catalog.finishes[finish]);
        }
    }
}
