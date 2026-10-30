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
        public readonly List<Collider> blockers=new();
        public readonly List<(PlacedBuildObject item,BuildRecord before,BuildRecord after)> edits=new();
        public int Cost(BuildCatalog catalog)
        {
            long value=catalog.Cost(catalog.Find(stairs.definitionId));
            foreach(var r in additions)value+=catalog.Cost(catalog.Find(r.definitionId));
            foreach(var edit in edits)
                if(edit.after.deleted)value-=catalog.Cost(catalog.Find(edit.before.definitionId));
            return (int)Math.Max(int.MinValue,Math.Min(int.MaxValue,value));
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
        static List<Vector2> Rectangle(Rect rect)=>new(){RoomGeometry.Snap(new Vector2(rect.xMin,rect.yMin)),RoomGeometry.Snap(new Vector2(rect.xMax,rect.yMin)),RoomGeometry.Snap(new Vector2(rect.xMax,rect.yMax)),RoomGeometry.Snap(new Vector2(rect.xMin,rect.yMax))};
        static BuildEntry Wall(BuildCatalog catalog)=>catalog.entries.FirstOrDefault(e=>e.prefab && e.prefab.name.Equals("Wall_complete",StringComparison.OrdinalIgnoreCase));
        internal static Rect LocalFootprint(BuildEntry entry)
        {
            // Decorative tread overhangs are not building-grid boundaries. The
            // authored stairwell is modular; round ONCE in prefab-local space,
            // never expand each placed instance to the unrelated floor-tile grid.
            const float tile=BuildPlot.CellSize;
            return Rect.MinMaxRect(Mathf.Round(entry.bounds.min.x/tile)*tile,Mathf.Round(entry.bounds.min.z/tile)*tile,
                Mathf.Round(entry.bounds.max.x/tile)*tile,Mathf.Round(entry.bounds.max.z/tile)*tile);
        }
        internal static Bounds PlacementBounds(BuildEntry entry)
        {
            var bounds=entry.bounds;var module=LocalFootprint(entry);
            // Mesh decorations extend beyond the modular last tread. They must
            // not reserve an extra strip in front of a neighbouring hall wall.
            bounds.SetMinMax(new Vector3(bounds.min.x,bounds.min.y,Mathf.Max(bounds.min.z,module.yMin)),
                new Vector3(bounds.max.x,bounds.max.y,Mathf.Min(bounds.max.z,module.yMax)));
            return bounds;
        }
        public static Vector3 SnapToStorey(BuildPlot plot,Vector3 position)
        {
            var local=plot.Grid.InverseTransformPoint(position);float best=.06f,target=local.y;
            void Consider(float y){float d=Mathf.Abs(y-local.y);if(d<best){best=d;target=y;}}
            Consider(plot.foundationHeight);
            foreach(var wall in plot.GetComponentsInChildren<WallTopology>())
            {
                Consider(plot.Grid.InverseTransformPoint(wall.Bottom).y);
                Consider(plot.Grid.InverseTransformPoint(wall.Bottom+wall.transform.up*wall.WorldHeight).y);
            }
            local.y=target;return plot.Grid.TransformPoint(local);
        }
        // Reciprocal of the placement-only stair clearance: adding a wall after
        // the stairs must allow the same narrow side/rear seam. Test EACH wall
        // collider, so T/+ arms entering the flight still block placement.
        public static bool AllowsWallBoundaryContact(BuildPlot plot,Collider hit,Vector3 center,Vector3 half,Quaternion orientation)
        {
            var item=hit.GetComponentInParent<PlacedBuildObject>();
            if(!item || item.deleted || item.GetComponentInParent<BuildPlot>()!=plot || !plot.catalog)return false;
            var entry=plot.catalog.Find(item.definitionId);if(!IsStairs(entry))return false;
            var rect=LocalFootprint(entry);var bounds=new Bounds(item.transform.InverseTransformPoint(center),Vector3.zero);
            foreach(float x in new[]{-half.x,half.x})foreach(float y in new[]{-half.y,half.y})foreach(float z in new[]{-half.z,half.z})
                bounds.Encapsulate(item.transform.InverseTransformPoint(center+orientation*new Vector3(x,y,z)));
            // Blender-local X is the stair width; -Z is the rear landing edge.
            // No front allowance: that would permit blocking the stair exit.
            return bounds.max.x<=rect.xMin+.5f || bounds.min.x>=rect.xMax-.5f || bounds.max.z<=rect.yMin+.3f;
        }
        public static bool IsRearWallSeam(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation,Collider hit)
        {
            var wall=hit.GetComponentInParent<WallTopology>();var box=hit as BoxCollider;
            if(!wall || !box)return false;
            var record=new BuildRecord{position=position,rotation=rotation,scale=entry.prefab.transform.localScale};
            if(!Exit(entry,plot,record,out _,out var direction,out _))return false;
            var localDirection=Quaternion.Inverse(rotation)*plot.Grid.TransformDirection(direction);
            bool alongX=Mathf.Abs(localDirection.x)>Mathf.Abs(localDirection.z);
            bool positive=(alongX?localDirection.x:localDirection.z)>0;
            var rect=LocalFootprint(entry);
            float rear=alongX?(positive?rect.xMin:rect.xMax):(positive?rect.yMin:rect.yMax);
            var inverse=Matrix4x4.TRS(position,rotation,record.scale).inverse;
            float Axis(Vector3 p)=>alongX?p.x:p.z;
            if(!WallGeometry.World(wall).Any(s=>Mathf.Abs(Axis(inverse.MultiplyPoint3x4(s.a))-rear)<.06f && Mathf.Abs(Axis(inverse.MultiplyPoint3x4(s.b))-rear)<.06f))return false;
            var matrix=inverse*box.transform.localToWorldMatrix;
            var bounds=new Bounds(matrix.MultiplyPoint3x4(box.center),Vector3.zero);
            var min=box.center-box.size*.5f;var max=box.center+box.size*.5f;
            foreach(float x in new[]{min.x,max.x})foreach(float y in new[]{min.y,max.y})foreach(float z in new[]{min.z,max.z})bounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(x,y,z)));
            // Permit only the rear wall's half-thickness seam. A perpendicular
            // branch, furniture or a wall deeper inside the stairs still blocks.
            return positive?Axis(bounds.max)<=rear+.3f:Axis(bounds.min)>=rear-.3f;
        }
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
            var points=new List<Vector3>();var footprint=LocalFootprint(entry);
            var matrix=plot.Grid.worldToLocalMatrix*Matrix4x4.TRS(record.position,record.rotation,record.scale);
            foreach(float x in new[]{footprint.xMin,footprint.xMax})foreach(float z in new[]{footprint.yMin,footprint.yMax})
                points.Add(matrix.MultiplyPoint3x4(new Vector3(x,0,z)));
            float x0=points.Min(p=>p.x),x1=points.Max(p=>p.x),z0=points.Min(p=>p.z),z1=points.Max(p=>p.z);
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
        internal static List<List<Vector2>> Tile(PlacedBuildObject item,Transform grid,out float top)
        {
            var patch=item.GetComponent<RoomFloorPatch>();top=grid.InverseTransformPoint(item.transform.position).y;
            if(patch){top=grid.InverseTransformPoint(patch.SurfacePoint).y;return patch.PlanarPieces(grid).ToList();}
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
            // Check before planning sales/cuts. Another staircase is an occupied
            // module, never a ceiling or a wall that this transaction may remove.
            if(!SpaceAvailable(catalog,plot,stairs,plan.blockers))
            {plan.error=T("Another staircase occupies this space","Otra escalera ocupa este espacio");return plan;}
            if(!Passage(catalog,plot,stairs,out var opening,out float level)){plan.error=T("No wall template for staircase enclosure","Falta una pared base para cerrar la escalera");return plan;}
            var manager=plot.GetComponent<RoomConstruction>();
            if(!manager){plan.error=T("Plot has no room system","El solar no tiene sistema de salas");return plan;}
            // A sale may have invalidated the graph earlier in this frame, before
            // RoomConstruction.LateUpdate. Never reuse a stale closed-room cache.
            manager.RefreshForEditing();
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
                    // Cut at the actual surface height without moving its pivot.
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
            // Add one tile only in front of the upper exit. The stairwell itself
            // supplies the other three enclosure edges: no side/back landing ring.
            // Landing is derived from the highest ramp, so quarter turns retain
            // the same exit direction instead of assuming a world-space front.
            var enclosure=Rect.MinMaxRect(Mathf.Min(opening.xMin,landing.xMin),Mathf.Min(opening.yMin,landing.yMin),
                Mathf.Max(opening.xMax,landing.xMax),Mathf.Max(opening.yMax,landing.yMax));
            var upper=existingRoom!=null?existingRoom.shape:Region(enclosure);var wallEntry=Wall(catalog);var template=wallEntry.prefab.GetComponent<WallTopology>();
            float height=template.WorldHeight;
            foreach(var point in upper.outline)
                if(!plot.Contains(plot.Grid.TransformPoint(new Vector3(point.x,level+height,point.y))))
                {plan.error=T("The plot has no space for the upper staircase enclosure","El solar no tiene espacio para el cerramiento superior de la escalera");return plan;}
            var local=WallGeometry.At(template,Vector3.zero,Quaternion.identity,wallEntry.prefab.transform.localScale).Single();
            var existingSpans=plot.GetComponentsInChildren<WallTopology>()
                .Where(w=>w.isActiveAndEnabled && w.GetComponentInParent<BuildPlot>()==plot && (!w.GetComponent<PlacedBuildObject>() || !w.GetComponent<PlacedBuildObject>().deleted))
                .SelectMany(WallGeometry.World).ToArray();
            for(int side=0;existingRoom==null && side<4;side++)
            {
                var a=upper.outline[side];var b=upper.outline[(side+1)%4];int count=Mathf.RoundToInt(Vector2.Distance(a,b)/4);
                for(int step=0;step<count;step++)
                {
                    var start=Vector2.Lerp(a,b,(float)step/count);var end=Vector2.Lerp(a,b,(float)(step+1)/count);
                    var from=plot.Grid.TransformPoint(new Vector3(start.x,level,start.y));var to=plot.Grid.TransformPoint(new Vector3(end.x,level,end.y));
                    // A long glass/window/door section can cover several modules.
                    // Keep it intact, including the installed door and its identity.
                    bool exists=existingSpans.Any(e=>e.Distance3D(from)<.06f && e.Distance3D(to)<.06f);
                    if(exists)continue;
                    var rotation=Quaternion.AngleAxis(Vector3.SignedAngle(local.b-local.a,to-from,Vector3.up),Vector3.up);
                    var position=from-rotation*local.a;
                    plan.additions.Add(new BuildRecord{definitionId=wallEntry.id,propertyId=plot.propertyId,sceneName=plot.gameObject.scene.name,position=position,rotation=rotation,scale=wallEntry.prefab.transform.localScale,finish=-1});
                }
            }
            StairAdaptation.Prepare(plan,catalog,wallEntry,opening,enclosure,level,height);
            if(!string.IsNullOrEmpty(plan.error))return plan;
            var removed=plan.edits.Where(e=>e.after.deleted&&e.item.GetComponent<WallTopology>()).Select(e=>e.item).ToArray();
            var joined=WallReconfiguration.Enclosure(catalog,plot,plan.additions,plot.Grid.rotation,removed);
            joined.removed.AddRange(removed.Where(p=>!joined.removed.Contains(p)));
            if(!WallReconfiguration.Validate(joined,catalog,out var wallError,plan.blockers)){plan.error=wallError;return plan;}
            plan.additions.Clear();plan.additions.AddRange(joined.added);
            foreach(var item in joined.removed)
            {
                StairAdaptation.Remove(plan,item);
            }
            var floor=catalog.entries.First(e=>e.prefab && e.prefab.GetComponent<BuildFloorCoverage>());
            // Never mutate the cached room outline/holes while preparing a preview.
            var floorRegion=WithPassages(upper,plot,level);floorRegion.holes.Add(hole);
            plan.additions.AddRange(manager.PlanTiles(RoomGeometry.Missing(floorRegion,manager.SurfacesAt(level,false)),level,floor));
            float roof=existingRoom!=null?existingRoom.ceiling:level+height;
            plan.additions.AddRange(manager.PlanTiles(RoomGeometry.Missing(WithPassages(upper,plot,roof),manager.SurfacesAt(roof,true)),roof,floor));
            return plan;
        }
        public static bool TouchesUpperStorey(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation,WallTopology wall)
        {
            var record=new BuildRecord{definitionId=entry.id,position=position,rotation=rotation,scale=entry.prefab.transform.localScale};
            return plot.catalog && Passage(plot.catalog,plot,record,out _,out float level) && plot.Grid.InverseTransformPoint(wall.Bottom).y>=level-.06f;
        }
        public static bool SpaceAvailable(BuildCatalog catalog,BuildPlot plot,BuildRecord record,ICollection<Collider> blockers=null)
        {
            if(!catalog || !Passage(catalog,plot,record,out var rect,out float top))return true;
            float bottom=plot.Grid.InverseTransformPoint(record.position).y;
            bool available=true;
            foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())
            {
                if(item.deleted || item.propertyId!=plot.propertyId || item.GetComponentInParent<BuildPlot>()!=plot)continue;
                if(!IsStairs(catalog.Find(item.definitionId)))continue;
                if(!Passage(catalog,plot,item.Capture(),out var other,out float otherTop))continue;
                float otherBottom=plot.Grid.InverseTransformPoint(item.transform.position).y;
                // Shared edges and separate storeys are fine. Side/rear wall
                // tolerances must never shrink stair-versus-stair occupancy.
                if(top<=otherBottom+.02f || otherTop<=bottom+.02f || !BuildFootprint.Overlap(rect,other))continue;
                available=false;
                if(blockers==null)return false;
                foreach(var collider in item.GetComponentsInChildren<Collider>())
                    if(collider.enabled&&!collider.isTrigger)blockers.Add(collider);
            }
            return available;
        }
        public static Vector3 SnapBoundary(BuildEntry entry,BuildPlot plot,Vector3 position,Quaternion rotation)
        {
            if(!plot.catalog || plot.endpointSnapDistance<=0)return position;
            var record=new BuildRecord{definitionId=entry.id,position=position,rotation=rotation,scale=entry.prefab.transform.localScale};
            if(!Passage(plot.catalog,plot,record,out var rect,out _))return position;
            float y=plot.Grid.InverseTransformPoint(position).y;
            float dx=0,dz=0,bestX=plot.endpointSnapDistance,bestZ=plot.endpointSnapDistance;
            foreach(var wall in plot.GetComponentsInChildren<WallTopology>())foreach(var span in WallGeometry.World(wall))
            {
                var a=plot.Grid.InverseTransformPoint(span.a);var b=plot.Grid.InverseTransformPoint(span.b);
                if(Mathf.Abs(a.y-y)>.06f)continue;
                if(Mathf.Abs(a.x-b.x)<.01f && Mathf.Max(a.z,b.z)>rect.yMin+.01f && Mathf.Min(a.z,b.z)<rect.yMax-.01f)
                    foreach(float edge in new[]{rect.xMin,rect.xMax})if(Mathf.Abs(a.x-edge)<bestX){dx=a.x-edge;bestX=Mathf.Abs(dx);}
                if(Mathf.Abs(a.z-b.z)<.01f && Mathf.Max(a.x,b.x)>rect.xMin+.01f && Mathf.Min(a.x,b.x)<rect.xMax-.01f)
                    foreach(float edge in new[]{rect.yMin,rect.yMax})if(Mathf.Abs(a.z-edge)<bestZ){dz=a.z-edge;bestZ=Mathf.Abs(dz);}
            }
            var candidate=position+plot.Grid.TransformVector(new Vector3(dx,0,dz));
            return BuildPlacement.FitsPlot(entry,plot,candidate,rotation)?candidate:position;
        }
        public static bool Validate(StairBuildPlan plan,BuildCatalog catalog)
        {
            if(plan==null || !string.IsNullOrEmpty(plan.error))return false;
            plan.blockers.Clear();
            var disabled=plan.edits.SelectMany(e=>e.item?e.item.GetComponentsInChildren<Collider>():Array.Empty<Collider>()).Where(c=>c.enabled).Distinct().ToArray();
            try
            {
                foreach(var c in disabled)c.enabled=false;Physics.SyncTransforms();
                var r=plan.stairs;
                return BuildPlacement.IsValid(catalog.Find(r.definitionId),plan.plot,r.position,r.rotation,r.cellCenter,plan.blockers);
            }
            finally{foreach(var c in disabled)if(c)c.enabled=true;Physics.SyncTransforms();}
        }
        public static PlacedBuildObject Build(BuildCatalog catalog,BuildPlot plot,BuildRecord record,EconomyService money)
        {
            var plan=Plan(catalog,plot,record);
            int cost=plan.Cost(catalog);
            if(!Validate(plan,catalog) || !money.TrySpend(Math.Max(0,cost)))return null;
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
                if(cost<0)money.AddMoney(-cost);
                WallTopology.RefreshScene(plot.gameObject.scene);WallTopology.MarkDirty();return created[0];
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
                money.AddMoney(Math.Max(0,cost));Debug.LogException(e);return null;
            }
        }
    }
}
