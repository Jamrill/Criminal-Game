using System;
using System.Collections.Generic;
using System.Linq;
using JuegoCriminal.Core;
using JuegoCriminal.Services;
using UnityEngine;
using static JuegoCriminal.Localization.GameLanguage;

namespace JuegoCriminal.Construction
{
    [DisallowMultipleComponent]
    public sealed class RoomConstruction : MonoBehaviour
    {
        [Serializable] public sealed class Room
        {
            public string key;
            public float floor,ceiling,area;
            public bool floorComplete,ceilingComplete;
            [NonSerialized]public RoomGeometry.Region shape;
            public float missingFloorArea,missingCeilingArea;
        }
        public ReflectionZone reflectionTemplate;
        public bool completeAutomatically=true;
        [Min(.01f)] public float minimumArea=1;
        public List<Room> rooms=new();
        public string Warning {get;private set;}
        public int PendingCost {get;private set;}
        static int revision;
        public static int Revision=>revision;
        int observed=-1,lastMoney=int.MinValue;float nextCheck;
        bool lastOwned;
        BuildPlot plot;
        readonly Dictionary<string,GameObject> reflections=new();
        public static void MarkDirty(){unchecked{revision++;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetRevision()=>revision=0;
        void OnEnable(){plot=GetComponent<BuildPlot>();observed=-1;}
        void OnDisable(){foreach(var go in reflections.Values)Remove(go);reflections.Clear();}
        static void Remove(GameObject go)
        {if(!go)return;go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
        void LateUpdate()
        {
            if(!plot || Time.timeScale<=0 || Time.unscaledTime<nextCheck)return;
            nextCheck=Time.unscaledTime+.3f;
            var app=Bootstrapper.Instance;
            var money=app?app.GetComponent<EconomyService>():null;
            var properties=app?app.GetComponent<PropertyService>():null;
            bool owned=plot.CanBuild(properties);
            int balance=money?money.Money:0;
            if(observed==revision && owned==lastOwned && (PendingCost==0 || balance==lastMoney))return;
            Rebuild(completeAutomatically && owned && app && app.SaveService && app.SaveService.HasCurrentGame,money);
            observed=revision;lastMoney=money?money.Money:0;lastOwned=owned;
        }
        sealed class Surface
        {public float bottom,top;public PlacedBuildObject owner;public List<List<Vector2>> pieces;}
        List<Surface> Surfaces()
        {
            var result=new List<Surface>();
            if(!plot)plot=GetComponent<BuildPlot>();if(!plot)return result;
            foreach(var floor in plot.GetComponentsInChildren<BuildFloorCoverage>())
            {
                if(floor.GetComponentInParent<BuildPlot>()!=plot)continue;
                var marker=floor.GetComponentInParent<PlacedBuildObject>();if(marker && marker.deleted)continue;
                var patch=floor.GetComponent<RoomFloorPatch>();
                if(patch)
                {
                    float top=plot.Grid.InverseTransformPoint(patch.transform.position).y;
                    result.Add(new Surface{owner=marker,bottom=top-RoomFloorPatch.Thickness,top=top,pieces=patch.PlanarPieces(plot.Grid).ToList()});continue;
                }
                foreach(var box in floor.GetComponentsInChildren<BoxCollider>())
                {
                    if(!box.enabled || box.isTrigger)continue;
                    var matrix=plot.Grid.worldToLocalMatrix*box.transform.localToWorldMatrix;
                    if(Mathf.Abs(matrix.MultiplyVector(Vector3.up).normalized.y)<.999f)continue;
                    var low=box.center-box.size*.5f;var high=box.center+box.size*.5f;
                    var points=new[]{new Vector3(low.x,low.y,low.z),new Vector3(high.x,low.y,low.z),new Vector3(high.x,low.y,high.z),new Vector3(low.x,low.y,high.z)}.Select(matrix.MultiplyPoint3x4).ToArray();
                    var polygon=points.Select(p=>RoomGeometry.Snap(new Vector2(p.x,p.z))).ToList();if(RoomGeometry.SignedArea(polygon)<0)polygon.Reverse();
                    float y0=matrix.MultiplyPoint3x4(low).y,y1=matrix.MultiplyPoint3x4(high).y;
                    result.Add(new Surface{owner=marker,bottom=Mathf.Min(y0,y1),top=Mathf.Max(y0,y1),pieces=new List<List<Vector2>>{polygon}});
                }
            }
            return result;
        }
        static IEnumerable<List<Vector2>> At(List<Surface> surfaces,float height,bool ceiling)
            =>surfaces.Where(s=>Mathf.Abs((ceiling?s.bottom:s.top)-height)<.06f).SelectMany(s=>s.pieces);
        public List<List<Vector2>> SurfacesAt(float height,bool ceiling)=>At(Surfaces(),height,ceiling).ToList();
        public List<List<Vector2>> SurfacesWithout(float height,bool ceiling,PlacedBuildObject excluded)
            =>At(Surfaces().Where(s=>s.owner!=excluded).ToList(),height,ceiling).ToList();
        public void RefreshForEditing()
        {
            // Hover validation must not clear a pending automatic-completion
            // payment, otherwise a later balance increase would never retry it.
            string warning=Warning;int cost=PendingCost;
            Rebuild();Warning=warning;PendingCost=cost;
        }
        public void Rebuild(bool complete=false,EconomyService money=null)
        {
            try{RebuildGeometry(complete,money);}
            catch(RoomGeometry.GeometryLimitException)
            {
                Warning=T("Room too complex to complete safely; simplify its walls or openings","Sala demasiado compleja para completarla con seguridad; simplifica sus paredes o huecos");
                PendingCost=0;
            }
        }
        void RebuildGeometry(bool complete,EconomyService money)
        {
            if(!plot)plot=GetComponent<BuildPlot>();if(!plot)return;
            Warning=null;PendingCost=0;rooms.Clear();
            var walls=plot.GetComponentsInChildren<WallTopology>().Where(w=>w.isActiveAndEnabled && w.GetComponentInParent<BuildPlot>()==plot && (!w.GetComponent<PlacedBuildObject>() || !w.GetComponent<PlacedBuildObject>().deleted));
            var groups=walls.GroupBy(w=>(bottom:Mathf.RoundToInt(plot.Grid.InverseTransformPoint(w.Bottom).y*20),top:Mathf.RoundToInt(plot.Grid.InverseTransformPoint(w.Bottom+plot.Grid.up*w.WorldHeight).y*20)));
            foreach(var group in groups)
            {
                var edges=group.SelectMany(WallGeometry.World).Select(span=>
                {var a=plot.Grid.InverseTransformPoint(span.a);var b=plot.Grid.InverseTransformPoint(span.b);return new RoomGeometry.Edge(new Vector2(a.x,a.z),new Vector2(b.x,b.z));});
                foreach(var region in RoomGeometry.Find(edges))
                    if(region.Area>=minimumArea && region.outline.All(p=>plot.Contains(plot.Grid.TransformPoint(new Vector3(p.x,group.Key.bottom/20f,p.y)))))
                        rooms.Add(new Room{shape=region,key=group.Key.bottom+":"+group.Key.top+":"+region.Key,floor=group.Key.bottom/20f,ceiling=group.Key.top/20f,area=region.Area});
            }
            var surfaces=Surfaces();
            foreach(var room in rooms)
            {
                var missingFloor=RoomGeometry.Missing(StairConstruction.WithPassages(room.shape,plot,room.floor),At(surfaces,room.floor,false));
                var missingRoof=RoomGeometry.Missing(StairConstruction.WithPassages(room.shape,plot,room.ceiling),At(surfaces,room.ceiling,true));
                room.missingFloorArea=missingFloor.Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)));
                room.missingCeilingArea=missingRoof.Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)));
                room.floorComplete=room.missingFloorArea<.00001f;
                room.ceilingComplete=room.missingCeilingArea<.00001f;
                if(complete && (!room.floorComplete || !room.ceilingComplete))
                {
                    if(Complete(room,missingFloor,missingRoof,money))
                    {surfaces=Surfaces();room.floorComplete=room.ceilingComplete=true;}
                }
            }
            SyncReflections();
            if(Application.isPlaying)Physics.SyncTransforms();
        }
        public List<BuildRecord> PlanTiles(List<List<Vector2>> pieces,float height,BuildEntry entry)
        {
            var result=new List<BuildRecord>();if(pieces.Count==0)return result;
            var offset=plot.FurnitureGridOffset;
            int x0=Mathf.FloorToInt((pieces.SelectMany(p=>p).Min(p=>p.x)-offset.x)/4),x1=Mathf.CeilToInt((pieces.SelectMany(p=>p).Max(p=>p.x)-offset.x)/4);
            int z0=Mathf.FloorToInt((pieces.SelectMany(p=>p).Min(p=>p.y)-offset.y)/4),z1=Mathf.CeilToInt((pieces.SelectMany(p=>p).Max(p=>p.y)-offset.y)/4);
            if((long)(x1-x0)*(z1-z0)>4096)throw new RoomGeometry.GeometryLimitException();
            for(int x=x0;x<x1;x++)for(int z=z0;z<z1;z++)
            {
                var origin=offset+new Vector2(x*4,z*4);var polygon=new[]{origin,origin+new Vector2(4,0),origin+new Vector2(4,4),origin+new Vector2(0,4)};
                var triangles=new List<Vector3>();
                foreach(var piece in pieces)
                {
                    var clipped=piece;
                    for(int side=0;side<4 && clipped.Count>=3;side++)clipped=RoomGeometry.Clip(clipped,polygon[side],polygon[(side+1)%4]);
                    if(clipped.Count<3 || Mathf.Abs(RoomGeometry.SignedArea(clipped))<.000001f)continue;
                    for(int i=1;i<clipped.Count-1;i++)
                    {
                        var triangle=new[]{clipped[0],clipped[i],clipped[i+1]};
                        if(Mathf.Abs(RoomGeometry.SignedArea(triangle))<=RoomGeometry.MinimumArea)continue;
                        foreach(var point in triangle)triangles.Add(new Vector3(point.x-origin.x,0,point.y-origin.y));
                    }
                }
                if(triangles.Count==0)continue;
                var position=plot.Grid.TransformPoint(new Vector3(origin.x,height,origin.y));
                result.Add(new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=entry.id,propertyId=plot.propertyId,sceneName=gameObject.scene.name,finish=-1,
                    position=position,rotation=plot.Grid.rotation,scale=Vector3.one,cellCenter=position,floorTriangles=triangles.ToArray()});
            }
            return result;
        }
        bool Complete(Room room,List<List<Vector2>> ground,List<List<Vector2>> roof,EconomyService money)
        {
            if(!money || !plot.CanBuild(money.GetComponent<PropertyService>()))return false;
            var entry=plot.catalog?plot.catalog.entries.FirstOrDefault(e=>e.prefab && e.prefab.GetComponent<BuildFloorCoverage>()):null;
            if(entry==null){Warning=T("Room needs a floor prefab in its construction catalogue","La sala necesita un prefab de suelo en su catálogo");return false;}
            var plan=PlanTiles(ground,room.floor,entry);plan.AddRange(PlanTiles(roof,room.ceiling+RoomFloorPatch.Thickness,entry));
            long cost=(long)plan.Count*plot.catalog.Cost(entry);
            if(cost>int.MaxValue || !money || !money.CanAfford((int)cost))
            {
                PendingCost=cost>int.MaxValue?int.MaxValue:(int)cost;
                Warning=T("Room: insufficient funds for floor and ceiling (${0})","Sala: saldo insuficiente para suelo y techo (${0})",PendingCost);return false;
            }
            if(!money.TrySpend((int)cost))return false;
            var created=new List<PlacedBuildObject>();
            try
            {
                foreach(var record in plan)
                {
                    var item=ConstructionPersistence.Spawn(plot.catalog,record,true,gameObject.scene);
                    if(!item)throw new InvalidOperationException("Could not create room floor tile");created.Add(item);
                }
                WallTopology.MarkDirty();return true;
            }
            catch(Exception exception)
            {
                foreach(var item in created)Remove(item.gameObject);money.AddMoney((int)cost);
                Warning=T("Room floor creation failed; the charge was refunded","No se pudo completar la sala; se ha devuelto el importe");Debug.LogException(exception,this);return false;
            }
        }
        void SyncReflections()
        {
            var valid=new HashSet<string>();
            foreach(var room in rooms.Where(r=>r.floorComplete && r.ceilingComplete))
            {
                valid.Add(room.key);
                var shape=room.shape;var center=(shape.min+shape.max)*.5f;
                var world=plot.Grid.TransformPoint(new Vector3(center.x,(room.floor+room.ceiling)*.5f,center.y));
                var bounds=new Bounds(world,Vector3.zero);
                foreach(float x in new[]{shape.min.x,shape.max.x})foreach(float z in new[]{shape.min.y,shape.max.y})foreach(float y in new[]{room.floor+.03f,room.ceiling-.03f})
                    bounds.Encapsulate(plot.Grid.TransformPoint(new Vector3(x,y,z)));
                var point=shape.InteriorPoint;
                var capture=plot.Grid.TransformPoint(new Vector3(point.x,(room.floor+room.ceiling)*.5f,point.y));
                if(!reflections.TryGetValue(room.key,out var go) || !go)
                {
                    go=new GameObject("Room reflection "+(reflections.Count+1));go.transform.SetParent(transform,false);
                    go.AddComponent<ReflectionZone>();reflections[room.key]=go;
                }
                // Also update unchanged local outlines when the plot/grid moves or scales.
                go.GetComponent<ReflectionZone>().ConfigureRoom(bounds,capture,reflectionTemplate);
            }
            foreach(var key in reflections.Keys.Where(k=>!valid.Contains(k)).ToArray()){Remove(reflections[key]);reflections.Remove(key);}
        }
    }
}
