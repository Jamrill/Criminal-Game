using System;
using System.Collections.Generic;
using System.Linq;
using JuegoCriminal.Services;
using UnityEngine;
using static JuegoCriminal.Localization.GameLanguage;

namespace JuegoCriminal.Construction
{
    public sealed class FloorBuildPlan
    {
        public BuildPlot plot;
        public BuildEntry entry;
        public Vector3 first,last;
        public int columns,rows;
        public string error;
        public readonly List<BuildRecord> additions=new();
        public long Cost(BuildCatalog catalog)=>(long)additions.Count*catalog.Cost(entry);
    }

    // One rectangle, one storey, one transaction. Existing surfaces are subtracted
    // geometrically, so partly cut tiles can be repaired without overlapping them.
    public static class FloorConstruction
    {
        public static FloorBuildPlan Plan(BuildEntry entry,BuildPlot plot,Vector3 first,Vector3 last)
        {
            var plan=new FloorBuildPlan{plot=plot,entry=entry,first=first,last=last};
            try
            {
                var manager=plot.GetComponent<RoomConstruction>();
                if(!FloorEditing.IsFloor(entry) || !manager){plan.error=T("No floor construction system","Falta el sistema de suelos");return plan;}
                var a=plot.Grid.InverseTransformPoint(first);var b=plot.Grid.InverseTransformPoint(last);
                var origin=plot.FurnitureGridOffset;const float cell=BuildPlot.CellSize;
                int x0=Mathf.FloorToInt((a.x-origin.x)/cell),z0=Mathf.FloorToInt((a.z-origin.y)/cell);
                int x1=Mathf.FloorToInt((b.x-origin.x)/cell),z1=Mathf.FloorToInt((b.z-origin.y)/cell);
                plan.columns=Math.Abs(x1-x0)+1;plan.rows=Math.Abs(z1-z0)+1;
                if((long)plan.columns*plan.rows>1024){plan.error=T("Select at most 1024 floor tiles per drag","Selecciona como máximo 1024 losetas por arrastre");return plan;}
                var min=origin+new Vector2(Math.Min(x0,x1)*cell,Math.Min(z0,z1)*cell);
                var max=origin+new Vector2((Math.Max(x0,x1)+1)*cell,(Math.Max(z0,z1)+1)*cell);
                // Keep authored storey pivots, even when the ray hits the slab's
                // top face a centimetre above them. Dragging never changes level.
                float level=plot.Grid.InverseTransformPoint(StairConstruction.SnapToStorey(plot,first)).y;
                var region=new RoomGeometry.Region{min=min,max=max,outline=new(){min,new(max.x,min.y),max,new(min.x,max.y)}};
                foreach(var p in region.outline)
                    if(!plot.Contains(plot.Grid.TransformPoint(new Vector3(p.x,level,p.y))))
                    {plan.error=T("Floor selection is outside the plot","La selección de suelo sale del solar");return plan;}
                if(level<plot.foundationHeight-.06f){plan.error=T("Cannot build below the foundation","No se puede construir por debajo de los cimientos");return plan;}
                // Manual construction may restore old holes, but must retain the
                // headroom of stairs that still exist. Deleted stairs never block it.
                region=StairConstruction.WithPassages(region,plot,level,false);
                var pieces=RoomGeometry.Missing(region,manager.SurfacesAt(level,false));
                plan.additions.AddRange(manager.PlanTiles(pieces,level,entry));
            }
            catch(RoomGeometry.GeometryLimitException)
            {plan.error=T("Floor selection is too complex; select a smaller area","La selección de suelo es demasiado compleja; selecciona una zona menor");}
            return plan;
        }

        public static bool Validate(FloorBuildPlan plan,ICollection<Collider> blockers=null)
        {
            if(plan==null || !string.IsNullOrEmpty(plan.error) || !plan.plot)return false;
            bool valid=true;var grid=plan.plot.Grid;
            foreach(var r in plan.additions)
            {
                var matrix=grid.worldToLocalMatrix*Matrix4x4.TRS(r.position,r.rotation,r.scale);
                var triangles=r.floorTriangles.Select(matrix.MultiplyPoint3x4).ToArray();
                float level=triangles[0].y;
                var bounds=new Bounds(triangles[0],Vector3.zero);foreach(var p in triangles)bounds.Encapsulate(p);
                var half=new Vector3(Mathf.Max(.005f,bounds.extents.x-.001f),.005f,Mathf.Max(.005f,bounds.extents.z-.001f));
                foreach(var hit in Physics.OverlapBox(grid.TransformPoint(bounds.center-Vector3.up*.01f),half,grid.rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    var owner=hit.GetComponentInParent<PlacedBuildObject>();if(owner&&owner.deleted)continue;
                    var slab=hit.GetComponentInParent<BuildFloorCoverage>();
                    // Existing tile coverage was already removed from the plan.
                    if(slab && slab.GetComponentInParent<BuildPlot>()==plan.plot)continue;
                    var wall=hit.GetComponentInParent<WallTopology>();
                    if(wall && (Mathf.Abs(grid.InverseTransformPoint(wall.Bottom).y-level)<.06f ||
                        Mathf.Abs(grid.InverseTransformPoint(wall.Bottom+wall.transform.up*wall.WorldHeight).y-level)<.06f))continue;
                    if(owner && StairConstruction.Passage(plan.plot.catalog,plan.plot,owner.Capture(),out _,out float top) && Mathf.Abs(top-level)<.06f)continue;
                    // Test actual planar overlap, not the tile/triangle's enclosing
                    // rectangle, which includes empty space around a stair cut.
                    var ob=hit.bounds;var polygon=new List<Vector2>();
                    if(hit is BoxCollider box && Mathf.Abs(Vector3.Dot(box.transform.up,grid.up))>.999f)
                    {
                        var lo=box.center-box.size*.5f;var hi=box.center+box.size*.5f;
                        polygon=new[]{new Vector3(lo.x,lo.y,lo.z),new Vector3(hi.x,lo.y,lo.z),new Vector3(hi.x,lo.y,hi.z),new Vector3(lo.x,lo.y,hi.z)}
                            .Select(box.transform.TransformPoint).Select(grid.InverseTransformPoint).Select(p=>new Vector2(p.x,p.z)).ToList();
                    }
                    else
                    {
                        var local=new Bounds(grid.InverseTransformPoint(ob.center),Vector3.zero);
                        foreach(float x in new[]{ob.min.x,ob.max.x})foreach(float y in new[]{ob.min.y,ob.max.y})foreach(float z in new[]{ob.min.z,ob.max.z})local.Encapsulate(grid.InverseTransformPoint(new Vector3(x,y,z)));
                        polygon=new(){new(local.min.x,local.min.z),new(local.max.x,local.min.z),new(local.max.x,local.max.z),new(local.min.x,local.max.z)};
                    }
                    if(RoomGeometry.SignedArea(polygon)<0)polygon.Reverse();
                    bool overlaps=false;
                    for(int i=0;i+2<triangles.Length && !overlaps;i+=3)
                    {
                        var piece=new[]{triangles[i],triangles[i+1],triangles[i+2]}.Select(p=>new Vector2(p.x,p.z)).ToList();
                        if(RoomGeometry.SignedArea(piece)<0)piece.Reverse();
                        for(int j=0;j<polygon.Count && piece.Count>=3;j++)piece=RoomGeometry.Clip(piece,polygon[j],polygon[(j+1)%polygon.Count]);
                        overlaps=piece.Count>=3 && Mathf.Abs(RoomGeometry.SignedArea(piece))>.001f;
                    }
                    if(!overlaps)continue;
                    valid=false;if(blockers==null)return false;if(!blockers.Contains(hit))blockers.Add(hit);
                }
            }
            return valid;
        }

        public static bool Build(FloorBuildPlan preview,BuildCatalog catalog,EconomyService money)
        {
            if(preview==null || !money || !preview.plot || !preview.plot.CanBuild(money.GetComponent<PropertyService>()))return false;
            // Re-plan on release: sales/construction since the preview must not
            // leave stale prices, duplicate tiles or newly obstructed stair holes.
            var plan=Plan(preview.entry,preview.plot,preview.first,preview.last);
            long cost=plan.Cost(catalog);
            if(plan.additions.Count==0 || !Validate(plan) || cost>int.MaxValue || !money.TrySpend((int)cost))return false;
            var created=new List<PlacedBuildObject>();bool complete=false;
            try
            {
                foreach(var r in plan.additions)
                {
                    var item=ConstructionPersistence.Spawn(catalog,r,true,plan.plot.gameObject.scene);
                    if(!item)return false;created.Add(item);
                }
                complete=true;WallTopology.MarkDirty();return true;
            }
            finally
            {
                if(!complete)
                {
                    foreach(var item in created)
                    {item.deleted=true;item.gameObject.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(item.gameObject);else UnityEngine.Object.DestroyImmediate(item.gameObject);}
                    money.AddMoney((int)cost);WallTopology.MarkDirty();
                }
            }
        }
    }
}
