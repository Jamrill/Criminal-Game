using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JuegoCriminal.Construction
{
    // Plain saved data on the plot: sold objects need no GameObject, renderer or
    // collider. The records still suppress authored objects and preserve openings.
    [DisallowMultipleComponent]
    public sealed class BuildDeletionLedger : MonoBehaviour
    {
        [SerializeField] private List<BuildRecord> records=new();
        public static BuildDeletionLedger For(BuildPlot plot)
            =>plot.GetComponent<BuildDeletionLedger>()??plot.gameObject.AddComponent<BuildDeletionLedger>();
        public void Remember(BuildRecord record)
        {
            var copy=JsonUtility.FromJson<BuildRecord>(JsonUtility.ToJson(record));copy.deleted=true;
            copy.position=transform.InverseTransformPoint(copy.position);copy.cellCenter=transform.InverseTransformPoint(copy.cellCenter);
            copy.rotation=Quaternion.Inverse(transform.rotation)*copy.rotation;
            var scale=transform.lossyScale;
            copy.scale=new Vector3(copy.scale.x/scale.x,copy.scale.y/scale.y,copy.scale.z/scale.z);
            records.RemoveAll(r=>r.id==copy.id);records.Add(copy);WallTopology.MarkDirty();
        }
        BuildRecord WorldRecord(BuildRecord local)
        {
            return new BuildRecord{id=local.id,definitionId=local.definitionId,sceneName=gameObject.scene.name,propertyId=local.propertyId,finish=local.finish,deleted=true,
                position=transform.TransformPoint(local.position),cellCenter=transform.TransformPoint(local.cellCenter),
                rotation=transform.rotation*local.rotation,scale=Vector3.Scale(local.scale,transform.lossyScale),floorTriangles=local.floorTriangles};
        }
        public void Forget(string id)=>records.RemoveAll(r=>r.id==id);
        public static IEnumerable<BuildRecord> Deleted(BuildPlot plot)
        {
            var saved=plot.GetComponent<BuildDeletionLedger>();
            var all=saved?saved.records.Select(saved.WorldRecord):Enumerable.Empty<BuildRecord>();
            // Backward compatibility before an old scene has been migrated.
            return all.Concat(plot.GetComponentsInChildren<PlacedBuildObject>(true)
                .Where(p=>p.deleted && p.GetComponentInParent<BuildPlot>()==plot).Select(p=>p.Capture())).GroupBy(r=>r.id).Select(g=>g.First());
        }
        public static BuildRecord[] CaptureScene(Scene scene)
        {
            var live=Object.FindObjectsByType<PlacedBuildObject>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(p=>p.gameObject.scene==scene && !p.deleted).Select(p=>p.Capture());
            var deleted=Object.FindObjectsByType<BuildPlot>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(p=>p.gameObject.scene==scene).SelectMany(Deleted);
            // A restored/replaced live identity supersedes an older deleted record.
            return live.Concat(deleted).GroupBy(r=>r.id).Select(g=>g.First()).ToArray();
        }
        public static void RemoveObject(PlacedBuildObject item,BuildRecord record=null)
        {
            if(!item)return;
            var plot=item.GetComponentInParent<BuildPlot>();
            if(record!=null && plot)For(plot).Remember(record);
            item.deleted=true;item.gameObject.SetActive(false);
            if(Application.isPlaying)Object.Destroy(item.gameObject);else Object.DestroyImmediate(item.gameObject);
            WallTopology.MarkDirty();
        }
        public static void Migrate(BuildPlot plot)
        {
            foreach(var old in plot.GetComponentsInChildren<PlacedBuildObject>(true).Where(p=>p.deleted && p.GetComponentInParent<BuildPlot>()==plot).ToArray())
                RemoveObject(old,old.Capture());
        }
    }
}
