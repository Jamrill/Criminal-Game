using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JuegoCriminal.Construction
{
    // Preview the same immutable plan that supplies the quoted transaction.
    // Floor cuts tint ONLY removed triangles, never the unchanged remainder.
    internal sealed class StairChangePreview
    {
        StairBuildPlan current;
        readonly List<MeshPreview> additions=new(),cuts=new();
        internal int AdditionCount=>additions.Count;
        internal int CutCount=>cuts.Count;
        internal void Hide(){foreach(var p in additions.Concat(cuts))if(p.Root)p.Root.SetActive(false);}
        internal void Dispose(){foreach(var p in additions.Concat(cuts))p.Dispose();additions.Clear();cuts.Clear();current=null;}
        internal void Show(StairBuildPlan plan,BuildCatalog catalog,Material green,Material red)
        {
            if(current!=plan)
            {
                Dispose();current=plan;
                foreach(var r in plan.additions)
                    additions.Add(r.floorTriangles!=null?MeshPreview.FromTriangles(r,r.floorTriangles,green,"Stair addition: floor"):
                        MeshPreview.Create(catalog.Find(r.definitionId).prefab,r.position,r.rotation,r.scale,green,"Stair addition: wall"));
                foreach(var edit in plan.edits)
                {
                    if(!edit.item || !edit.item.ProtectedFloor)continue;
                    var pieces=StairConstruction.Tile(edit.item,plan.plot.Grid,out float y);
                    if(!edit.after.deleted)
                    {
                        var r=edit.after;var matrix=plan.plot.Grid.worldToLocalMatrix*Matrix4x4.TRS(r.position,r.rotation,r.scale);
                        for(int i=0;i+2<r.floorTriangles.Length;i+=3)
                            pieces=RoomGeometry.Subtract(pieces,new[]{r.floorTriangles[i],r.floorTriangles[i+1],r.floorTriangles[i+2]}.Select(matrix.MultiplyPoint3x4).Select(p=>new Vector2(p.x,p.z)).ToArray());
                    }
                    var inverse=Matrix4x4.TRS(edit.before.position,edit.before.rotation,edit.before.scale).inverse;var triangles=new List<Vector3>();
                    foreach(var polygon in pieces)for(int i=1;i+1<polygon.Count;i++)
                    {
                        if(!RoomGeometry.HasTriangleSurface(polygon[0],polygon[i],polygon[i+1]))continue;
                        foreach(var p in new[]{polygon[0],polygon[i],polygon[i+1]})triangles.Add(inverse.MultiplyPoint3x4(plan.plot.Grid.TransformPoint(new Vector3(p.x,y,p.y))));
                    }
                    if(triangles.Count>0)cuts.Add(MeshPreview.FromTriangles(edit.before,triangles.ToArray(),red,"Stair removal: floor",true));
                }
            }
            foreach(var p in additions){p.Root.SetActive(true);p.SetMaterial(green);}
            foreach(var p in cuts){p.Root.SetActive(true);p.SetMaterial(red);}
        }
    }
}
