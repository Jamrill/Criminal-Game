using UnityEngine;

namespace JuegoCriminal.Construction
{
    public static class BuildFootprint
    {
        public static bool Uses(BuildEntry entry)=>entry!=null && entry.prefab && entry.category==BuildCategory.Furniture && entry.mount!=BuildMount.Doorway;
        public static Vector2Int Cells(BuildEntry entry)=>new Vector2Int(Mathf.Max(1,entry.footprintCells.x),Mathf.Max(1,entry.footprintCells.y));
        public static Vector3 Anchor(BuildEntry entry,Vector3 position,Quaternion rotation)
            =>position+rotation*new Vector3(entry.bounds.center.x,entry.mount==BuildMount.Ceiling?entry.bounds.max.y:entry.bounds.min.y,entry.bounds.center.z);
        public static Vector2 SizeOnGrid(BuildEntry entry,BuildPlot plot,Quaternion rotation)
        {
            var cells=Cells(entry);var relative=Quaternion.Inverse(plot.Grid.rotation)*rotation;
            var x=relative*Vector3.right*(cells.x*BuildPlot.CellSize);
            var z=relative*Vector3.forward*(cells.y*BuildPlot.CellSize);
            return new Vector2(Mathf.Abs(x.x)+Mathf.Abs(z.x),Mathf.Abs(x.z)+Mathf.Abs(z.z));
        }
        public static Vector3 SnapAnchor(BuildEntry entry,BuildPlot plot,Vector3 point,Quaternion rotation)
        {
            if(!Uses(entry) && !FloorEditing.IsFloor(entry))return plot.SnapOrigin(point);
            var local=plot.Grid.InverseTransformPoint(point);var half=SizeOnGrid(entry,plot,rotation)*.5f;
            var origin=plot.FurnitureGridOffset;
            float x=Mathf.Round((local.x-origin.x-half.x)/BuildPlot.CellSize);
            float z=Mathf.Round((local.z-origin.y-half.y)/BuildPlot.CellSize);
            if(plot.TryFurnitureGridLayout(out _,out var count) && plot.Contains(point))
            {
                x=Mathf.Clamp(x,0,Mathf.Max(0,count.x-Mathf.RoundToInt(half.x*2/BuildPlot.CellSize)));
                z=Mathf.Clamp(z,0,Mathf.Max(0,count.y-Mathf.RoundToInt(half.y*2/BuildPlot.CellSize)));
            }
            local.x=origin.x+x*BuildPlot.CellSize+half.x;
            local.z=origin.y+z*BuildPlot.CellSize+half.y;
            return plot.Grid.TransformPoint(local);
        }
        public static Rect RectOnGrid(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation)
        {
            var center=plot.Grid.InverseTransformPoint(Anchor(entry,position,rotation));var size=SizeOnGrid(entry,plot,rotation);
            return new Rect(new Vector2(center.x,center.z)-size*.5f,size);
        }
        public static bool Overlap(Rect a,Rect b)=>a.xMin<b.xMax-.01f && a.xMax>b.xMin+.01f && a.yMin<b.yMax-.01f && a.yMax>b.yMin+.01f;
        public static bool SameSurface(BuildEntry a,Vector3 anchorA,BuildEntry b,Vector3 anchorB,BuildPlot plot)
            =>a.mount==b.mount && Mathf.Abs(Vector3.Dot(anchorA-anchorB,plot.Grid.up))<.12f;
        public static bool IsAvailable(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation,System.Collections.Generic.ICollection<Collider> blockers=null)
        {
            if(!Uses(entry))return true;
            var anchor=Anchor(entry,position,rotation);var local=plot.Grid.InverseTransformPoint(anchor);
            var rect=RectOnGrid(entry,plot,position,rotation);
            foreach(float x in new[]{rect.xMin+.01f,rect.xMax-.01f})foreach(float z in new[]{rect.yMin+.01f,rect.yMax-.01f})
                if(!plot.Contains(plot.Grid.TransformPoint(new Vector3(x,local.y,z))))return false;
            var catalog=plot.catalog;if(!catalog)return true;
            bool available=true;
            foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())
            {
                if(item.deleted || item.propertyId!=plot.propertyId || item.GetComponentInParent<BuildPlot>()!=plot)continue;
                var other=catalog.Find(item.definitionId);if(!Uses(other))continue;
                var otherAnchor=Anchor(other,item.transform.position,item.transform.rotation);
                if(SameSurface(entry,anchor,other,otherAnchor,plot) && Overlap(rect,RectOnGrid(other,plot,item.transform.position,item.transform.rotation)))
                {
                    available=false;if(blockers==null)return false;
                    foreach(var collider in item.GetComponentsInChildren<Collider>())if(collider.enabled&&!collider.isTrigger)blockers.Add(collider);
                }
            }
            return available;
        }
    }
}
