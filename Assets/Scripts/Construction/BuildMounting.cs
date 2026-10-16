using System;
using UnityEngine;

namespace JuegoCriminal.Construction
{
    public static class BuildMounting
    {
        public struct Pose
        {
            public Vector3 position,anchor;
            public Quaternion rotation;
        }
        static bool FirstHit(Ray ray,float distance,Transform player,out RaycastHit hit)
        {
            var hits=Physics.RaycastAll(ray,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var candidate in hits)
                if(!player || !candidate.collider.transform.IsChildOf(player)){hit=candidate;return true;}
            hit=default;return false;
        }
        public static bool TryPose(BuildEntry entry,BuildCatalog catalog,BuildPlot plot,Ray ray,int turns,Transform player,out Pose pose)
        {
            pose=default;
            if(entry.mount==BuildMount.Ceiling)
            {
                if(!FirstHit(ray,100,player,out var hit) || Vector3.Dot(hit.normal,plot.Grid.up)>-.7f)return false;
                if(!IsCeiling(hit.collider,plot))return false;
                var anchor=plot.SnapOrigin(hit.point);
                // Snapping may cross an edge or a stair opening. Verify the actual
                // underside again instead of floating over a missing ceiling tile.
                if(!FirstHit(new Ray(anchor+hit.normal*.1f,-hit.normal),.2f,player,out var snapped) ||
                    !IsCeiling(snapped.collider,plot) || Vector3.Dot(snapped.normal,plot.Grid.up)>-.7f)return false;
                pose.anchor=snapped.point+snapped.normal*.002f;
                pose.rotation=plot.Grid.rotation*Quaternion.Euler(0,(turns%4)*90,0);
                pose.position=pose.anchor-pose.rotation*new Vector3(entry.bounds.center.x,entry.bounds.max.y,entry.bounds.center.z);
                return true;
            }
            if(entry.mount!=BuildMount.Doorway)return false;
            bool found=false;float nearest=100;
            foreach(var host in plot.GetComponentsInChildren<PlacedBuildObject>())
            {
                if(host.deleted || host.propertyId!=plot.propertyId)continue;
                var definition=catalog.Find(host.definitionId);
                if(definition==null || definition.doorway.size.x<=0 || definition.doorway.size.y<=0)continue;
                var opening=definition.doorway;
                var plane=new Plane(host.transform.forward,host.transform.TransformPoint(opening.center));
                if(!plane.Raycast(ray,out float distance) || distance>nearest)continue;
                var local=host.transform.InverseTransformPoint(ray.GetPoint(distance))-opening.center;
                if(Mathf.Abs(local.x)>opening.extents.x+.2f || Mathf.Abs(local.y)>opening.extents.y+.2f)continue;
                if(FirstHit(ray,distance+.01f,player,out var obstruction) &&
                    obstruction.collider.GetComponentInParent<PlacedBuildObject>()!=host && obstruction.distance<distance-.05f)continue;
                float width=opening.size.x*Mathf.Abs(host.transform.lossyScale.x);
                float height=opening.size.y*Mathf.Abs(host.transform.lossyScale.y);
                if(entry.bounds.size.x>width+.02f || entry.bounds.size.y>height+.02f)continue;
                pose.anchor=host.transform.TransformPoint(opening.center-Vector3.up*opening.extents.y);
                pose.rotation=host.transform.rotation*Quaternion.Euler(0,(turns%2)*180,0);
                pose.position=pose.anchor-pose.rotation*new Vector3(entry.bounds.center.x,entry.bounds.min.y,entry.bounds.center.z);
                nearest=distance;found=true;
            }
            return found;
        }
        static bool IsCeiling(Collider collider,BuildPlot plot)
        {
            var floor=collider.GetComponentInParent<BuildFloorCoverage>();
            return floor && floor.GetComponentInParent<BuildPlot>()==plot;
        }
        public static bool IsValid(BuildEntry entry,BuildCatalog catalog,BuildPlot plot,Pose pose)
        {
            if(entry.mount==BuildMount.Ceiling)
                foreach(float x in new[]{entry.bounds.min.x+.01f,entry.bounds.max.x-.01f})
                    foreach(float z in new[]{entry.bounds.min.z+.01f,entry.bounds.max.z-.01f})
                    {
                        var start=pose.position+pose.rotation*new Vector3(x,entry.bounds.max.y-.05f,z);
                        if(!FirstHit(new Ray(start,plot.Grid.up),.15f,null,out var hit) || !IsCeiling(hit.collider,plot) ||
                            Vector3.Dot(hit.normal,plot.Grid.up)>-.7f || Mathf.Abs(Vector3.Dot(hit.point-pose.anchor,plot.Grid.up))>.025f)return false;
                    }
            // Check the reserved doorway even if the old door leaf is currently open.
            if(entry.mount==BuildMount.Doorway)
                foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())
                {
                    if(item.deleted)continue;
                    var other=catalog.Find(item.definitionId);
                    if(other==null || other.mount!=BuildMount.Doorway)continue;
                    var center=item.transform.position+item.transform.rotation*other.bounds.center;
                    if(Vector3.Distance(center,pose.position+pose.rotation*entry.bounds.center)<.15f)return false;
                }
            return BuildPlacement.IsValid(entry,plot,pose.position,pose.rotation,pose.anchor);
        }
    }
}
