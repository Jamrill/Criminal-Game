using System;
using System.Collections.Generic;
using System.Linq;
using JuegoCriminal.Services;
using UnityEngine;
using static JuegoCriminal.Localization.GameLanguage;

namespace JuegoCriminal.Construction
{
    public sealed class StairBuildPlan
    {
        public BuildPlot plot;
        public BuildRecord stairs;
        public string error;
        public readonly List<BuildRecord> additions=new();
        public readonly List<(PlacedBuildObject item,BuildRecord before,BuildRecord after)> edits=new();
        public int Cost(BuildCatalog catalog)
        {
            long value=catalog.Cost(catalog.Find(stairs.definitionId));
            foreach(var r in additions)value+=catalog.Cost(catalog.Find(r.definitionId));
            return (int)Math.Min(int.MaxValue,value);
        }
    }
    public static class StairConstruction
    {
        public static bool IsStairs(BuildEntry entry)=>entry?.prefab && entry.prefab.name.Equals("Stairs",StringComparison.OrdinalIgnoreCase);
        public static Vector3 SnapVacatedPosition(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation)
        {
            if(!IsStairs(entry))return position;
            var up=plot.Grid.up;
            foreach(var old in BuildDeletionLedger.Deleted(plot)
                .Where(p=>IsStairs(plot.catalog.Find(p.definitionId)))
                .OrderBy(p=>Vector3.SqrMagnitude(p.position-position)))
            {
                var delta=old.position-position;
                // Reuse the authored pivot, not a rounded visual-bounds centre.
                // Deliberate rotation and placement on another floor are preserved.
                if(Mathf.Abs(Vector3.Dot(delta,up))>.15f || Vector3.ProjectOnPlane(delta,up).magnitude>1f || Quaternion.Angle(old.rotation,rotation)>1f)continue;
                if(BuildPlacement.IsValid(entry,plot,old.position,rotation,old.cellCenter))return old.position;
            }
            return position;
        }
        static List<Vector2> Rectangle(Rect rect)=>new(){new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax)};
        static BuildEntry Wall(BuildCatalog catalog)=>catalog.entries.FirstOrDefault(e=>e.prefab && e.prefab.name.Equals("Wall_complete",StringComparison.OrdinalIgnoreCase));
        // Derive the upper exit from the highest sloping collider, not the mesh's
        // decorative bounds. Those extend past the last step and erased the hall.
        static bool Exit(BuildEntry entry,BuildPlot plot,BuildRecord record,out Vector3 point,out Vector3 direction,out float width)
        {
            point=direction=default;width=4;float highest=float.NegativeInfinity;
            var root=entry.prefab.transform;
            foreach(var box in entry.prefab.GetComponentsInChildren<BoxCollider>(true))
            {
                if(!box.enabled || box.isTrigger)continue;
                var axis=box.size.z>=box.size.x?Vector3.forward:Vector3.right;
                float length=box.size.z>=box.size.x?box.size.z:box.size.x;
                var a=root.InverseTransformPoint(box.transform.TransformPoint(box.center-axis*length*.5f));
                var b=root.InverseTransformPoint(box.transform.TransformPoint(box.center+axis*length*.5f));
                if(Mathf.Abs(a.y-b.y)<.2f)continue;
                var high=a.y>b.y?a:b;var low=a.y>b.y?b:a;
                if(high.y<=highest)continue;highest=high.y;
                var localDirection=Vector3.ProjectOnPlane(high-low,Vector3.up).normalized;
                bool alongX=Mathf.Abs(localDirection.x)>Mathf.Abs(localDirection.z);
                // The ramp collider extends slightly beyond the modular last tread.
                float end=alongX?high.x:high.z;
                float modular=Mathf.Round(end/4)*4;if(Mathf.Abs(end-modular)<.15f)end=modular;
                if(alongX)high.x=end;else high.z=end;
                var matrix=plot.Grid.worldToLocalMatrix*Matrix4x4.TRS(record.position,record.rotation,record.scale);
                point=matrix.MultiplyPoint3x4(high);direction=matrix.MultiplyVector(localDirection).normalized;
                var across=axis==Vector3.forward?Vector3.right:Vector3.forward;
                float breadth=axis==Vector3.forward?box.size.x:box.size.z;
                var localAcross=root.InverseTransformVector(box.transform.TransformVector(across*breadth));
                width=matrix.MultiplyVector(localAcross).magnitude;
            }
            return highest>float.NegativeInfinity;
        }
        public static bool Landing(BuildCatalog catalog,BuildPlot plot,BuildRecord record,out Rect landing,out float level)
        {
            landing=default;level=0;var entry=catalog.Find(record.definitionId);
            if(!IsStairs(entry) || !Passage(catalog,plot,record,out _,out level) || !Exit(entry,plot,record,out var p,out var dir,out float width))return false;
            if(Mathf.Abs(dir.x)>Mathf.Abs(dir.z))
                landing=Rect.MinMaxRect(Mathf.Min(p.x,p.x+Mathf.Sign(dir.x)*4),p.z-width*.5f,Mathf.Max(p.x,p.x+Mathf.Sign(dir.x)*4),p.z+width*.5f);
            else landing=Rect.MinMaxRect(p.x-width*.5f,Mathf.Min(p.z,p.z+Mathf.Sign(dir.z)*4),p.x+width*.5f,Mathf.Max(p.z,p.z+Mathf.Sign(dir.z)*4));
            return true;
        }
        public static bool Passage(BuildCatalog catalog,BuildPlot plot,BuildRecord record,out Rect opening,out float level)
        {
            opening=default;level=0;var entry=catalog.Find(record.definitionId);var wall=Wall(catalog);
            if(!IsStairs(entry) || wall==null)return false;
            var points=new List<Vector3>();
            foreach(float x in new[]{entry.bounds.min.x,entry.bounds.max.x})foreach(float z in new[]{entry.bounds.min.z,entry.bounds.max.z})
                points.Add(plot.Grid.InverseTransformPoint(record.position+record.rotation*new Vector3(x,entry.bounds.min.y,z)));
            // Reserve full modular tiles, including the landing at the top of the ramp.
            var offset=plot.FurnitureGridOffset;
            float x0=offset.x+Mathf.Floor((points.Min(p=>p.x)-offset.x+.001f)/4)*4;
            float x1=offset.x+Mathf.Ceil((points.Max(p=>p.x)-offset.x-.001f)/4)*4;
            float z0=offset.y+Mathf.Floor((points.Min(p=>p.z)-offset.y+.001f)/4)*4;
            float z1=offset.y+Mathf.Ceil((points.Max(p=>p.z)-offset.y-.001f)/4)*4;
            opening=Rect.MinMaxRect(x0,z0,x1,z1);
            if(Exit(entry,plot,record,out var exit,out var direction,out _))
            {
                if(Mathf.Abs(direction.x)>Mathf.Abs(direction.z))
                {if(direction.x>0)opening.xMax=Mathf.Min(opening.xMax,exit.x);else opening.xMin=Mathf.Max(opening.xMin,exit.x);}
                else {if(direction.z>0)opening.yMax=Mathf.Min(opening.yMax,exit.z);else opening.yMin=Mathf.Max(opening.yMin,exit.z);}
            }
            level=Mathf.Round(points[0].y*20)/20f+wall.prefab.GetComponent<WallTopology>().WorldHeight;
            return true;
        }
        public static RoomGeometry.Region WithPassages(RoomGeometry.Region source,BuildPlot plot,float level)
        {
            var result=new RoomGeometry.Region{outline=source.outline,min=source.min,max=source.max};result.holes.AddRange(source.holes);
            if(!plot.catalog)return result;
            var records=plot.GetComponentsInChildren<PlacedBuildObject>().Where(p=>!p.deleted).Select(p=>p.Capture()).Concat(BuildDeletionLedger.Deleted(plot));
            foreach(var record in records)
                if(Passage(plot.catalog,plot,record,out var opening,out float y) && Mathf.Abs(y-level)<.06f)
                    result.holes.Add(Rectangle(opening));
            return FloorEditing.WithRemovedFloors(result,plot,level);
        }
        static RoomGeometry.Region Region(Rect rect)
            =>new(){outline=Rectangle(rect),min=rect.min,max=rect.max};
        static List<List<Vector2>> Tile(PlacedBuildObject item,Transform grid,out float top)
        {
            var patch=item.GetComponent<RoomFloorPatch>();top=grid.InverseTransformPoint(item.transform.position).y;
            if(patch)return patch.PlanarPieces(grid).ToList();
            var result=new List<List<Vector2>>();
            foreach(var box in item.GetComponentsInChildren<BoxCollider>())
            {
                if(!box.enabled || box.isTrigger)continue;
                var c=box.center;var e=box.size*.5f;
                top=grid.InverseTransformPoint(box.transform.TransformPoint(c+Vector3.up*e.y)).y;
                var points=new[]{new Vector3(c.x-e.x,c.y+e.y,c.z-e.z),new Vector3(c.x+e.x,c.y+e.y,c.z-e.z),new Vector3(c.x+e.x,c.y+e.y,c.z+e.z),new Vector3(c.x-e.x,c.y+e.y,c.z+e.z)}
                    .Select(p=>grid.InverseTransformPoint(box.transform.TransformPoint(p))).Select(p=>RoomGeometry.Snap(new Vector2(p.x,p.z))).ToList();
                if(RoomGeometry.SignedArea(points)<0)points.Reverse();result.Add(points);
            }
            return result;
        }
        public static StairBuildPlan Plan(BuildCatalog catalog,BuildPlot plot,BuildRecord stairs)
        {
            try{return PlanGeometry(catalog,plot,stairs);}
            catch(RoomGeometry.GeometryLimitException)
            {return new StairBuildPlan{plot=plot,stairs=stairs,error=T("Stair enclosure is too complex to calculate safely","El cerramiento de la escalera es demasiado complejo para calcularlo con seguridad")};}
        }
        static StairBuildPlan PlanGeometry(BuildCatalog catalog,BuildPlot plot,BuildRecord stairs)
        {
            var plan=new StairBuildPlan{plot=plot,stairs=stairs};
            if(!Passage(catalog,plot,stairs,out var opening,out float level)){plan.error=T("No wall template for staircase enclosure","Falta una pared base para cerrar la escalera");return plan;}
            var manager=plot.GetComponent<RoomConstruction>();
            if(!manager){plan.error=T("Plot has no room system","El solar no tiene sistema de salas");return plan;}
            var hole=Rectangle(opening);
            foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())
            {
                if(item.deleted || !item.ProtectedFloor)continue;
                var pieces=Tile(item,plot.Grid,out float top);if(Mathf.Abs(top-level)>.06f)continue;
                var remaining=RoomGeometry.Subtract(pieces,hole);
                float oldArea=pieces.Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p))),newArea=remaining.Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)));
                if(oldArea-newArea<.00001f)continue;
                var before=item.Capture();var after=JsonUtility.FromJson<BuildRecord>(JsonUtility.ToJson(before));
                if(newArea<.00001f)after.deleted=true;
                else
                {
                    after.position+=plot.Grid.up*(top-plot.Grid.InverseTransformPoint(after.position).y);
                    var matrix=Matrix4x4.TRS(after.position,after.rotation,after.scale).inverse;var triangles=new List<Vector3>();
                    foreach(var polygon in remaining)for(int i=1;i<polygon.Count-1;i++)foreach(var p in new[]{polygon[0],polygon[i],polygon[i+1]})
                        triangles.Add(matrix.MultiplyPoint3x4(plot.Grid.TransformPoint(new Vector3(p.x,top,p.y))));
                    after.floorTriangles=triangles.ToArray();
                }
                plan.edits.Add((item,before,after));
            }
            if(!Landing(catalog,plot,stairs,out var landing,out _))
            {plan.error=T("Cannot locate the upper stair exit","No se encuentra la salida superior de la escalera");return plan;}
            var existingRoom=manager.rooms.FirstOrDefault(r=>Mathf.Abs(r.floor-level)<.06f &&
                RoomGeometry.AreaInRectangle(r.shape,opening.min,opening.max)>=opening.width*opening.height-.0001f &&
                RoomGeometry.AreaInRectangle(r.shape,landing.min,landing.max)>=landing.width*landing.height-.0001f);
            // A tile-wide landing ring keeps the new walls away from the stair exit.
            var enclosure=Rect.MinMaxRect(opening.xMin-4,opening.yMin-4,opening.xMax+4,opening.yMax+4);
            var upper=existingRoom!=null?existingRoom.shape:Region(enclosure);var wallEntry=Wall(catalog);var template=wallEntry.prefab.GetComponent<WallTopology>();
            float height=template.WorldHeight;
            foreach(var point in upper.outline)
                if(!plot.Contains(plot.Grid.TransformPoint(new Vector3(point.x,level+height,point.y))))
                {plan.error=T("The plot has no space for the upper staircase enclosure","El solar no tiene espacio para el cerramiento superior de la escalera");return plan;}
            var local=WallGeometry.At(template,Vector3.zero,Quaternion.identity,wallEntry.prefab.transform.localScale).Single();
            for(int side=0;existingRoom==null && side<4;side++)
            {
                var a=upper.outline[side];var b=upper.outline[(side+1)%4];int count=Mathf.RoundToInt(Vector2.Distance(a,b)/4);
                for(int step=0;step<count;step++)
                {
                    var start=Vector2.Lerp(a,b,(float)step/count);var end=Vector2.Lerp(a,b,(float)(step+1)/count);
                    var from=plot.Grid.TransformPoint(new Vector3(start.x,level,start.y));var to=plot.Grid.TransformPoint(new Vector3(end.x,level,end.y));
                    bool exists=plot.GetComponentsInChildren<WallTopology>().SelectMany(WallGeometry.World).Any(e=>e.Same(new WallGeometry.Span(from,to)));
                    if(exists)continue;
                    var rotation=Quaternion.AngleAxis(Vector3.SignedAngle(local.b-local.a,to-from,Vector3.up),Vector3.up);
                    var position=from-rotation*local.a;
                    if(!BuildPlacement.IsValid(wallEntry,plot,position,rotation,position))
                    {plan.error=T("The upper staircase walls are obstructed","Las paredes superiores de la escalera están ocupadas");return plan;}
                    plan.additions.Add(new BuildRecord{definitionId=wallEntry.id,propertyId=plot.propertyId,sceneName=plot.gameObject.scene.name,position=position,rotation=rotation,scale=wallEntry.prefab.transform.localScale,finish=-1});
                }
            }
            var floor=catalog.entries.First(e=>e.prefab && e.prefab.GetComponent<BuildFloorCoverage>());
            // Never mutate the cached room outline/holes while preparing a preview.
            var floorRegion=WithPassages(upper,plot,level);floorRegion.holes.Add(hole);
            plan.additions.AddRange(manager.PlanTiles(RoomGeometry.Missing(floorRegion,manager.SurfacesAt(level,false)),level,floor));
            float roof=existingRoom!=null?existingRoom.ceiling:level+height;
            plan.additions.AddRange(manager.PlanTiles(RoomGeometry.Missing(WithPassages(upper,plot,roof),manager.SurfacesAt(roof,true)),roof+RoomFloorPatch.Thickness,floor));
            return plan;
        }
        public static PlacedBuildObject Build(BuildCatalog catalog,BuildPlot plot,BuildRecord record,EconomyService money)
        {
            var plan=Plan(catalog,plot,record);
            if(!string.IsNullOrEmpty(plan.error) || !money.TrySpend(plan.Cost(catalog)))return null;
            var created=new List<PlacedBuildObject>();
            try
            {
                foreach(var edit in plan.edits)edit.item.Restore(edit.after,catalog);
                foreach(var addition in new[]{record}.Concat(plan.additions))
                {
                    if(string.IsNullOrEmpty(addition.id))addition.id=Guid.NewGuid().ToString("N");
                    var item=ConstructionPersistence.Spawn(catalog,addition,true,plot.gameObject.scene);
                    if(!item)throw new InvalidOperationException("Staircase construction failed");created.Add(item);
                }
                WallTopology.MarkDirty();return created[0];
            }
            catch(Exception e)
            {
                foreach(var item in created){item.gameObject.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(item.gameObject);else UnityEngine.Object.DestroyImmediate(item.gameObject);}
                foreach(var edit in plan.edits)
                {
                    // A deferred Destroy cannot be cancelled by reactivating an
                    // object. Restore a fresh identity on rollback instead.
                    if(edit.item)BuildDeletionLedger.RemoveObject(edit.item);
                    ConstructionPersistence.Spawn(catalog,edit.before,true,plot.gameObject.scene);
                }
                money.AddMoney(plan.Cost(catalog));Debug.LogException(e);return null;
            }
        }
    }
}
