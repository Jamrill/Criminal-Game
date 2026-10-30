using System.Linq;
using UnityEngine;
using static JuegoCriminal.Localization.GameLanguage;

namespace JuegoCriminal.Construction
{
    public static class FloorEditing
    {
        public static bool IsFloor(BuildEntry entry)=>entry?.prefab && entry.prefab.GetComponent<BuildFloorCoverage>();
        public static string RemovalWarning=>T("Foundation floors cannot be removed","El suelo de los cimientos no se puede quitar");
        static float Top(PlacedBuildObject item,BuildPlot plot)
        {
            var patch=item.GetComponent<RoomFloorPatch>();
            if(patch)return plot.Grid.InverseTransformPoint(patch.transform.position).y;
            return item.GetComponentsInChildren<BoxCollider>(true).Where(b=>!b.isTrigger)
                .Select(b=>plot.Grid.InverseTransformPoint(b.transform.TransformPoint(b.center+Vector3.up*b.size.y*.5f)).y).DefaultIfEmpty(float.NaN).Max();
        }
        public static bool CanRemove(PlacedBuildObject item)
        {
            if(!item || item.deleted)return false;if(!item.ProtectedFloor)return true;
            var plot=item.GetComponentInParent<BuildPlot>();if(!plot)return false;
            float height=Top(item,plot);
            return !float.IsNaN(height) && Mathf.Abs(height-plot.foundationHeight)>.06f;
        }
        // Only saved data is needed to preserve intentional holes.
        public static RoomGeometry.Region WithRemovedFloors(RoomGeometry.Region source,BuildPlot plot,float height)
        {
            if(!plot.catalog || Mathf.Abs(height-plot.foundationHeight)<.06f)return source;
            foreach(var record in BuildDeletionLedger.Deleted(plot))
            {
                var entry=plot.catalog.Find(record.definitionId);if(!IsFloor(entry))continue;
                var matrix=plot.Grid.worldToLocalMatrix*Matrix4x4.TRS(record.position,record.rotation,record.scale);
                if(record.floorTriangles!=null && record.floorTriangles.Length>=3)
                {
                    for(int i=0;i+2<record.floorTriangles.Length;i+=3)
                    {
                        if(Mathf.Abs(matrix.MultiplyPoint3x4(record.floorTriangles[i]).y-height)>.06f)continue;
                        var polygon=new[]{record.floorTriangles[i],record.floorTriangles[i+1],record.floorTriangles[i+2]}.Select(p=>matrix.MultiplyPoint3x4(p)).Select(p=>RoomGeometry.Snap(new Vector2(p.x,p.z))).ToList();
                        if(Mathf.Abs(RoomGeometry.SignedArea(polygon))<=RoomGeometry.MinimumArea)continue;
                        if(RoomGeometry.SignedArea(polygon)<0)polygon.Reverse();source.holes.Add(polygon);
                    }
                }
                else
                {
                    var bounds=entry.bounds;
                    if(Mathf.Abs(matrix.MultiplyPoint3x4(bounds.max).y-height)>.06f)continue;
                    var polygon=new[]{new Vector3(bounds.min.x,bounds.max.y,bounds.min.z),new Vector3(bounds.max.x,bounds.max.y,bounds.min.z),new Vector3(bounds.max.x,bounds.max.y,bounds.max.z),new Vector3(bounds.min.x,bounds.max.y,bounds.max.z)}
                        .Select(p=>matrix.MultiplyPoint3x4(p)).Select(p=>RoomGeometry.Snap(new Vector2(p.x,p.z))).ToList();
                    if(RoomGeometry.SignedArea(polygon)<0)polygon.Reverse();source.holes.Add(polygon);
                }
            }
            return source;
        }
        public static bool CanPlace(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation)
        {
            if(!plot.catalog)return true;
            var b=entry.bounds;var corners=new[]{new Vector3(b.min.x,b.max.y,b.min.z),new Vector3(b.max.x,b.max.y,b.min.z),new Vector3(b.max.x,b.max.y,b.max.z),new Vector3(b.min.x,b.max.y,b.max.z)}
                .Select(p=>plot.Grid.InverseTransformPoint(position+rotation*p)).ToArray();
            var polygon=corners.Select(p=>new Vector2(p.x,p.z)).ToList();if(RoomGeometry.SignedArea(polygon)<0)polygon.Reverse();
            var region=new RoomGeometry.Region{outline=polygon,min=new Vector2(polygon.Min(p=>p.x),polygon.Min(p=>p.y)),max=new Vector2(polygon.Max(p=>p.x),polygon.Max(p=>p.y))};
            foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())
                if(!item.deleted && StairConstruction.Passage(plot.catalog,plot,item.Capture(),out var hole,out float height) && Mathf.Abs(height-corners[0].y)<.06f &&
                    RoomGeometry.AreaInRectangle(region,hole.min,hole.max)>.00001f)return false;
            return true;
        }
    }
}
