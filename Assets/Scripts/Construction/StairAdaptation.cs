using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static JuegoCriminal.Localization.GameLanguage;

namespace JuegoCriminal.Construction
{
    // Pure planning: edits are priced/previewed before any persistent mutation.
    internal static class StairAdaptation
    {
        static bool Cuts(WallGeometry.Span span,BuildPlot plot,Rect rect,float level)
        {
            var a=plot.Grid.InverseTransformPoint(span.a);var b=plot.Grid.InverseTransformPoint(span.b);
            if(Mathf.Abs(a.y-level)>.06f || Mathf.Abs(b.y-level)>.06f)return false;
            float lo=0,hi=1;
            bool Axis(float start,float delta,float min,float max)
            {
                if(Mathf.Abs(delta)<.00001f)return start>min&&start<max;
                float x=(min-start)/delta,y=(max-start)/delta;
                lo=Mathf.Max(lo,Mathf.Min(x,y));hi=Mathf.Min(hi,Mathf.Max(x,y));return hi-lo>.001f;
            }
            return Axis(a.x,b.x-a.x,rect.xMin,rect.xMax)&&Axis(a.z,b.z-a.z,rect.yMin,rect.yMax)&&hi-lo>.001f;
        }
        static bool Conflicts(WallGeometry.Span a,WallGeometry.Span b)
        {
            if(Mathf.Abs(a.a.y-b.a.y)>.06f || a.Same(b))return false;
            var d=a.b-a.a;var e=b.b-b.a;float cross=d.x*e.z-d.z*e.x;
            if(Mathf.Abs(cross)<.001f)
            {
                if(Vector3.Cross((b.a-a.a),d.normalized).magnitude>.06f)return false;
                float x=Vector3.Dot(b.a-a.a,d.normalized),y=Vector3.Dot(b.b-a.a,d.normalized);
                return Mathf.Min(a.Length,Mathf.Max(x,y))-Mathf.Max(0,Mathf.Min(x,y))>.06f;
            }
            var c=b.a-a.a;float t=(c.x*e.z-c.z*e.x)/cross,u=(c.x*d.z-c.z*d.x)/cross;
            return t>=-.001f&&t<=1.001f&&u>=-.001f&&u<=1.001f&&((t>.01f&&t<.99f)||(u>.01f&&u<.99f));
        }
        internal static BuildRecord Wall(BuildEntry entry,BuildPlot plot,Vector3 from,Vector3 to)
        {
            var local=WallGeometry.At(entry.prefab.GetComponent<WallTopology>(),Vector3.zero,Quaternion.identity,entry.prefab.transform.localScale).Single();
            var rot=Quaternion.AngleAxis(Vector3.SignedAngle(local.b-local.a,to-from,Vector3.up),Vector3.up);
            var pos=from-rot*local.a;
            return new BuildRecord{definitionId=entry.id,propertyId=plot.propertyId,sceneName=plot.gameObject.scene.name,position=pos,rotation=rot,scale=entry.prefab.transform.localScale,cellCenter=pos,finish=-1};
        }
        internal static void Remove(StairBuildPlan plan,PlacedBuildObject item)
        {
            if(plan.edits.Any(e=>e.item==item))return;
            var before=item.Capture();var after=JsonUtility.FromJson<BuildRecord>(JsonUtility.ToJson(before));after.deleted=true;
            plan.edits.Add((item,before,after));
        }
        internal static void Prepare(StairBuildPlan plan,BuildCatalog catalog,BuildEntry wall,Rect opening,Rect enclosure,float level,float height)
        {
            var plot=plan.plot;var items=plot.GetComponentsInChildren<PlacedBuildObject>().Where(p=>!p.deleted&&p.GetComponentInParent<BuildPlot>()==plot).ToArray();
            var surfaces=new List<List<Vector2>>();
            foreach(var floor in items.Where(p=>p.ProtectedFloor))
            {
                var edit=plan.edits.FirstOrDefault(e=>e.item==floor);
                if(edit.item)
                {
                    if(edit.after.deleted)continue;
                    var r=edit.after;var matrix=plot.Grid.worldToLocalMatrix*Matrix4x4.TRS(r.position,r.rotation,r.scale);
                    for(int i=0;i+2<r.floorTriangles.Length;i+=3)
                        surfaces.Add(new[]{r.floorTriangles[i],r.floorTriangles[i+1],r.floorTriangles[i+2]}.Select(matrix.MultiplyPoint3x4).Select(p=>new Vector2(p.x,p.z)).ToList());
                }
                else
                {
                    var pieces=StairConstruction.Tile(floor,plot.Grid,out float top);
                    if(Mathf.Abs(top-level)<.06f)surfaces.AddRange(pieces);
                }
            }
            var corners=new[]{enclosure.min,new Vector2(enclosure.xMax,enclosure.yMin),enclosure.max,new Vector2(enclosure.xMin,enclosure.yMax)};
            var existing=items.Where(p=>p.GetComponent<WallTopology>()).SelectMany(p=>WallGeometry.World(p.GetComponent<WallTopology>())).ToArray();
            var upperSpans=existing.Concat(plan.additions.SelectMany(r=>WallGeometry.At(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>(),r.position,r.rotation,r.scale))).ToArray();
            // Supports descend one storey to the stair base. Existing slabs/walls
            // are reused. Leave the entrance and the exit corridor unobstructed.
            for(int side=0;side<4;side++)
            {
                var a=corners[side];var b=corners[(side+1)%4];int count=Mathf.RoundToInt(Vector2.Distance(a,b)/4);
                for(int i=0;i<count;i++)
                {
                    var start=Vector2.Lerp(a,b,(float)i/count);var end=Vector2.Lerp(a,b,(float)(i+1)/count);
                    var topFrom=plot.Grid.TransformPoint(new Vector3(start.x,level,start.y));var topTo=plot.Grid.TransformPoint(new Vector3(end.x,level,end.y));
                    if(!upperSpans.Any(s=>s.Distance3D(topFrom)<.06f&&s.Distance3D(topTo)<.06f))continue;
                    var normal=new Vector2(-(end-start).y,(end-start).x).normalized*.15f;
                    var polygon=new List<Vector2>{start-normal,end-normal,end+normal,start+normal};
                    if(RoomGeometry.SignedArea(polygon)<0)polygon.Reverse();
                    var region=new RoomGeometry.Region{outline=polygon,min=new Vector2(polygon.Min(p=>p.x),polygon.Min(p=>p.y)),max=new Vector2(polygon.Max(p=>p.x),polygon.Max(p=>p.y))};
                    if(RoomGeometry.Missing(region,surfaces).Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)))<.0001f)continue;
                    var from=plot.Grid.TransformPoint(new Vector3(start.x,level-height,start.y));var to=plot.Grid.TransformPoint(new Vector3(end.x,level-height,end.y));
                    var inverse=Matrix4x4.TRS(plan.stairs.position,plan.stairs.rotation,plan.stairs.scale).inverse;
                    var localA=inverse.MultiplyPoint3x4(from);var localB=inverse.MultiplyPoint3x4(to);
                    float minX=Mathf.Min(localA.x,localB.x),maxX=Mathf.Max(localA.x,localB.x);
                    if(Mathf.Min(localA.z,localB.z)>=7.9f && Mathf.Abs(localA.z-localB.z)<.06f &&
                        (minX<4-.06f&&maxX>0+.06f || minX<-4-.06f&&maxX>-8+.06f))continue;
                    if(existing.Any(s=>s.Distance3D(from)<.06f&&s.Distance3D(to)<.06f))continue;
                    plan.additions.Add(Wall(wall,plot,from,to));
                }
            }
            var required=plan.additions.SelectMany(r=>WallGeometry.At(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>(),r.position,r.rotation,r.scale)).ToArray();
            var clearUpper=Rect.MinMaxRect(enclosure.xMin+.06f,enclosure.yMin+.06f,enclosure.xMax-.06f,enclosure.yMax-.06f);
            var clearLower=Rect.MinMaxRect(opening.xMin+.5f,opening.yMin+.5f,opening.xMax-.5f,opening.yMax-.5f);
            foreach(var item in items)
            {
                var topology=item.GetComponent<WallTopology>();if(!topology)continue;
                var spans=WallGeometry.World(topology);
                var remaining=spans.Where(s=>!Cuts(s,plot,clearUpper,level)&&!Cuts(s,plot,clearLower,level-height)&&!required.Any(r=>Conflicts(s,r))).ToList();
                if(remaining.Count==spans.Count)continue;
                if(!WallGeometry.Solid(catalog.Find(item.definitionId)))
                {
                    plan.error=T("Move the window, doorway or glass wall blocking the staircase first","Retira primero la ventana, el hueco de puerta o el cristal que bloquea la escalera");
                    plan.blockers.AddRange(item.GetComponentsInChildren<Collider>());return;
                }
                Remove(plan,item);
                if(!WallReconfiguration.PreserveSections(catalog,plot,remaining,item.transform.rotation,item.finish,plan.additions))
                {plan.error=T("Cannot preserve the remaining wall sections","No se pueden conservar los tramos restantes");return;}
            }
        }
    }
}
