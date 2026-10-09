using UnityEngine;
namespace JuegoCriminal.Construction
{
    public static class BuildPlacement
    {
        public static Vector3 Position(BuildEntry entry, Vector3 cellCenter, Quaternion rotation)
        {
            var wall=entry.prefab.GetComponent<WallTopology>();
            var anchor=wall ? Vector3.Scale(wall.bottomCenter,entry.prefab.transform.localScale) : new Vector3(entry.bounds.center.x,entry.bounds.min.y,entry.bounds.center.z);
            return cellCenter-rotation*anchor;
        }
        public static bool IsValid(BuildEntry entry, BuildPlot plot, Vector3 position, Quaternion rotation, Vector3 cellCenter)
        {
            if (!entry.prefab || !plot) return false;
            var b=entry.bounds;
            foreach(float x in new[]{b.min.x,b.max.x}) foreach(float y in new[]{b.min.y+.02f,b.max.y}) foreach(float z in new[]{b.min.z,b.max.z})
                if(!plot.Contains(position+rotation*new Vector3(x,y,z))) return false;
            foreach(float x in new[]{-1.99f,1.99f}) foreach(float z in new[]{-1.99f,1.99f})
                if(!plot.Contains(cellCenter+plot.Grid.rotation*new Vector3(x,.05f,z))) return false;
            foreach(var placed in Object.FindObjectsByType<PlacedBuildObject>(FindObjectsSortMode.None))
                if(Vector3.Distance(placed.cellCenter,cellCenter)<.1f) return false;
            // Shrink very slightly so touching the supporting floor/adjacent wall is legal.
            var half=Vector3.Max(b.extents-Vector3.one*.02f,Vector3.one*.005f);
            var wall=entry.prefab.GetComponent<WallTopology>();
            if(wall)
            {
                var root=entry.prefab.transform;
                foreach(var box in entry.prefab.GetComponentsInChildren<BoxCollider>(true))
                {
                    if(!box.enabled || box.isTrigger) continue;
                    var center=Quaternion.Inverse(root.rotation)*(box.transform.TransformPoint(box.center)-root.position);
                    var extents=Vector3.Scale(box.size*.5f,box.transform.lossyScale);
                    extents=Vector3.Max(new Vector3(Mathf.Abs(extents.x),Mathf.Abs(extents.y),Mathf.Abs(extents.z))-Vector3.one*.02f,Vector3.one*.005f);
                    var orientation=rotation*Quaternion.Inverse(root.rotation)*box.transform.rotation;
                    foreach(var hit in Physics.OverlapBox(position+rotation*center,extents,orientation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    {
                        var neighbor=hit.GetComponentInParent<WallTopology>();
                        if(!wall.JoinsAt(position,rotation,root.localScale,neighbor)) return false;
                    }
                }
                return true;
            }
            return Physics.OverlapBox(position+rotation*b.center,half,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Length==0;
        }
    }
}
