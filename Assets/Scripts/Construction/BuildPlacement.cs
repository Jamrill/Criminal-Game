using UnityEngine;
namespace JuegoCriminal.Construction
{
    public static class BuildPlacement
    {
        public static Vector3 SnapWall(BuildEntry entry, BuildPlot plot, Vector3 position, Quaternion rotation)
        {
            var wall=entry.prefab.GetComponent<WallTopology>();
            if(!wall || wall.connections==null || plot.endpointSnapDistance<=0)return position;
            var candidates=new System.Collections.Generic.List<Vector3>();
            foreach(var other in Object.FindObjectsByType<WallTopology>(FindObjectsSortMode.None))
            {
                if(other.gameObject.scene!=plot.gameObject.scene || other.connections==null)continue;
                foreach(var end in other.connections)foreach(var local in wall.connections)
                {
                    var candidate=other.transform.TransformPoint(end)-rotation*Vector3.Scale(local,entry.prefab.transform.localScale);
                    if(Mathf.Abs(Vector3.Dot(candidate-position,plot.Grid.up))<.06f && Vector3.Distance(candidate,position)<=plot.endpointSnapDistance)
                        candidates.Add(candidate);
                }
            }
            candidates.Sort((a,b)=>(a-position).sqrMagnitude.CompareTo((b-position).sqrMagnitude));
            foreach(var candidate in candidates)
                if(IsValid(entry,plot,candidate,rotation,candidate+rotation*Vector3.Scale(wall.bottomCenter,entry.prefab.transform.localScale)))return candidate;
            return position;
        }
        public static Vector3 Position(BuildEntry entry, Vector3 cellCenter, Quaternion rotation)
        {
            var wall=entry.prefab.GetComponent<WallTopology>();
            // Wall pivots are authored as construction origins, not visual centres.
            var anchor=wall ? Vector3.zero : new Vector3(entry.bounds.center.x,entry.bounds.min.y,entry.bounds.center.z);
            return cellCenter-rotation*anchor;
        }
        public static bool IsValid(BuildEntry entry, BuildPlot plot, Vector3 position, Quaternion rotation, Vector3 cellCenter)
        {
            if (!entry.prefab || !plot) return false;
            var b=entry.bounds;
            foreach(float x in new[]{b.min.x,b.max.x}) foreach(float y in new[]{b.min.y+.02f,b.max.y}) foreach(float z in new[]{b.min.z,b.max.z})
                if(!plot.Contains(position+rotation*new Vector3(x,y,z))) return false;
            // Actual geometry determines occupancy; a shared cell is not a collider.
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
                        // A common endpoint permits ONLY the small seam at that endpoint,
                        // never all collisions against the connected wall.
                        var candidateBounds=new Bounds(position+rotation*center,Vector3.zero);
                        foreach(float x in new[]{-extents.x,extents.x})foreach(float y in new[]{-extents.y,extents.y})foreach(float z in new[]{-extents.z,extents.z})
                            candidateBounds.Encapsulate(position+rotation*center+orientation*new Vector3(x,y,z));
                        var low=Vector3.Max(candidateBounds.min,hit.bounds.min);
                        var high=Vector3.Min(candidateBounds.max,hit.bounds.max);
                        bool seam=false;
                        foreach(var end in wall.connections)foreach(var otherEnd in neighbor.connections)
                        {
                            var joint=position+rotation*Vector3.Scale(end,root.localScale);
                            if(Vector3.Distance(joint,neighbor.transform.TransformPoint(otherEnd))>=.06f)continue;
                            bool confined=true;
                            foreach(float x in new[]{low.x,high.x})foreach(float z in new[]{low.z,high.z})
                                if(Vector2.Distance(new Vector2(x,z),new Vector2(joint.x,joint.z))>.55f)confined=false;
                            seam|=confined;
                        }
                        if(!seam)return false;
                    }
                }
                return true;
            }
            return Physics.OverlapBox(position+rotation*b.center,half,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Length==0;
        }
    }
}
