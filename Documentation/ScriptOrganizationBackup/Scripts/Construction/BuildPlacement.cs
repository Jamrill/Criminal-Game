using UnityEngine;
namespace JuegoCriminal.Construction
{
    public static class BuildPlacement
    {
        public static bool FitsPlot(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation)
        {
            var b=entry.bounds;
            foreach(float x in new[]{b.min.x,b.max.x})foreach(float y in new[]{b.min.y+.02f,b.max.y})foreach(float z in new[]{b.min.z,b.max.z})
                if(!plot.Contains(position+rotation*new Vector3(x,y,z)))return false;
            return true;
        }
        public static Vector3 ClampToPlot(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation)
        {
            var b=entry.bounds;var local=new Bounds(plot.transform.InverseTransformPoint(position+rotation*b.center),Vector3.zero);
            foreach(float x in new[]{b.min.x,b.max.x})foreach(float y in new[]{b.min.y,b.max.y})foreach(float z in new[]{b.min.z,b.max.z})
                local.Encapsulate(plot.transform.InverseTransformPoint(position+rotation*new Vector3(x,y,z)));
            var min=plot.Volume.center-plot.Volume.size*.5f;var max=plot.Volume.center+plot.Volume.size*.5f;
            // Sub-millimetre clearance avoids float jitter without opening a gap
            // between perpendicular perimeter walls in the room graph (1 mm grid).
            const float clearance=.0001f;
            if(local.size.x>max.x-min.x-clearance*2 || local.size.z>max.z-min.z-clearance*2)return position;
            var delta=Vector3.zero;
            if(local.min.x<min.x+clearance)delta.x=min.x+clearance-local.min.x;
            else if(local.max.x>max.x-clearance)delta.x=max.x-clearance-local.max.x;
            if(local.min.z<min.z+clearance)delta.z=min.z+clearance-local.min.z;
            else if(local.max.z>max.z-clearance)delta.z=max.z-clearance-local.max.z;
            return position+plot.transform.TransformVector(delta);
        }
        public static bool TrySurfacePoint(BuildEntry entry,BuildPlot plot,Ray ray,bool hasHit,RaycastHit hit,Vector3 fallbackLevel,out Vector3 point)
        {
            var up=plot.Grid.up;
            var support=hasHit?hit.collider.GetComponentInParent<WallTopology>():null;
            if(FloorEditing.IsFloor(entry) && support)
            {
                // A wall face may be hit by a horizontal ray that cannot meet the
                // player's floor plane. Resolve the storey before that fallback.
                float elevation=Vector3.Dot(hit.point-support.Bottom,up);
                float height=elevation>support.WorldHeight*.5f?support.WorldHeight:0;
                point=hit.point+up*(height-elevation);return true;
            }
            if(FloorEditing.IsFloor(entry) && hasHit && hit.collider.GetComponentInParent<BuildFloorCoverage>())
            {
                point=hit.point;
                if(Vector3.Dot(hit.normal,up)<-.7f)point+=up*RoomFloorPatch.Thickness;
                return true;
            }
            if(entry.prefab.GetComponent<WallTopology>() && support)
            {point=hit.point-up*Vector3.Dot(hit.point-support.Bottom,up);return true;}
            if(hasHit && Vector3.Dot(hit.normal,up)>.7f){point=hit.point;return true;}
            if(new Plane(up,fallbackLevel).Raycast(ray,out float distance) && distance<=100)
            {point=ray.GetPoint(distance);return true;}
            point=default;return false;
        }
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
            var anchor=wall ? Vector3.zero : new Vector3(entry.bounds.center.x,FloorEditing.IsFloor(entry)?entry.bounds.max.y:entry.bounds.min.y,entry.bounds.center.z);
            return cellCenter-rotation*anchor;
        }
        public static bool IsValid(BuildEntry entry, BuildPlot plot, Vector3 position, Quaternion rotation, Vector3 cellCenter)
        {
            if (!entry.prefab || !plot) return false;
            if(FloorEditing.IsFloor(entry) && !FloorEditing.CanPlace(entry,plot,position,rotation))return false;
            if(!BuildFootprint.IsAvailable(entry,plot,position,rotation))return false;
            var b=entry.bounds;
            if(!FitsPlot(entry,plot,position,rotation))return false;
            // Actual geometry determines occupancy; a shared cell is not a collider.
            // Shrink very slightly so touching the supporting floor/adjacent wall is legal.
            var half=Vector3.Max(b.extents-Vector3.one*.02f,Vector3.one*.005f);
            // Placement-only side clearance for Stairs (local X, also after rotation).
            // Keep the real prefab colliders, footprint and plot limits unchanged.
            if(StairConstruction.IsStairs(entry))half.x=Mathf.Max(.005f,half.x-.5f);
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
                        foreach(var end in WallGeometry.Contacts(wall))foreach(var otherEnd in WallGeometry.Contacts(neighbor))
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
            var collisions=Physics.OverlapBox(position+rotation*b.center,half,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            foreach(var hit in collisions)
            {
                if(FloorEditing.IsFloor(entry) && hit.GetComponentInParent<WallTopology>() is WallTopology support &&
                    Mathf.Abs(Vector3.Dot(position+rotation*new Vector3(b.center.x,b.max.y,b.center.z)-support.Bottom,plot.Grid.up)-support.WorldHeight)<.06f)continue;
                // A staircase opens the intervening slab transactionally. It must
                // still be blocked by walls/furniture, not by that removable ceiling.
                if(StairConstruction.IsStairs(entry) && hit.GetComponentInParent<BuildFloorCoverage>() &&
                    Vector3.Dot(hit.bounds.center-(position+rotation*new Vector3(0,b.min.y,0)),plot.Grid.up)>1)continue;
                return false;
            }
            return true;
        }
    }
}
