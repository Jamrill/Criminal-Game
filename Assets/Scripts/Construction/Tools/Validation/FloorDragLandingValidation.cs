#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Construction;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using MeshPreview=JuegoCriminal.Construction.MeshPreview;

public static class FloorDragLandingValidation
{
    public static void Full(){Run();CornerSectionsValidation.Full();}
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/FloorDragLandingValidation.txt",report);if(!ok)throw new Exception(label);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var floor=catalog.entries.Single(FloorEditing.IsFloor);var stairs=catalog.entries.Single(StairConstruction.IsStairs);
        var plot=new GameObject("Floor drag test").AddComponent<BuildPlot>();plot.propertyId=971;plot.buildable=true;plot.catalog=catalog;plot.Volume.size=Vector3.one*160;
        var rooms=plot.gameObject.AddComponent<RoomConstruction>();
        var services=new GameObject("Isolated services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var properties=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,properties})service.GetType().GetField("_save",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(service,save);
        save.SetCurrent(new SaveData{money=1000000,ownedProperties=new[]{971}});
        void Clear(){foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())Object.DestroyImmediate(item.gameObject);var ledger=plot.GetComponent<BuildDeletionLedger>();if(ledger)Object.DestroyImmediate(ledger);Physics.SyncTransforms();rooms.Rebuild();}
        float Area(IEnumerable<List<Vector2>> pieces)=>pieces.Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)));
        RoomGeometry.Region Region(Rect r)=>new(){min=r.min,max=r.max,outline=new(){r.min,new(r.xMax,r.yMin),r.max,new(r.xMin,r.yMax)}};
        BuildRecord Record(BuildEntry entry,Vector3 p,Quaternion q)=>new(){id=Guid.NewGuid().ToString("N"),definitionId=entry.id,propertyId=plot.propertyId,position=p,rotation=q,scale=entry.prefab.transform.localScale,sceneName=scene.name,finish=-1};
        List<List<Vector2>> Planned(StairBuildPlan plan,float y)=>plan.additions.Where(r=>r.floorTriangles!=null&&Mathf.Abs(r.position.y-y)<.06f).SelectMany(r=>
            Enumerable.Range(0,r.floorTriangles.Length/3).Select(i=>r.floorTriangles.Skip(i*3).Take(3).Select(p=>r.position+r.rotation*p).Select(p=>new Vector2(p.x,p.z)).ToList())).ToList();
        foreach(int sign in new[]{-1,1})foreach(int width in new[]{1,2,3})foreach(int length in new[]{1,2,5})
        {
            var first=new Vector3(2,7,2);var last=first+new Vector3(sign*(width-1)*4,0,sign*(length-1)*4);
            var plan=FloorConstruction.Plan(floor,plot,first,last);
            Check(plan.columns==width && plan.rows==length && plan.additions.Count==width*length && FloorConstruction.Validate(plan),"Rectangle "+width+"x"+length+" direction "+sign);
            Check(plan.additions.All(r=>Mathf.Abs(r.position.y-7)<.001f),"Authored floor datum remains 7");
        }
        var start=new Vector3(2,7,2);var nine=FloorConstruction.Plan(floor,plot,start,new Vector3(10,7,10));
        Check(nine.additions.Count==9 && FloorConstruction.Plan(floor,plot,start,new Vector3(6,7,6)).additions.Count==4 && FloorConstruction.Plan(floor,plot,start,start).additions.Count==1,"Retract 3x3 to 2x2 to 1x1");
        int balance=money.Money;
        Check(plot.GetComponentsInChildren<PlacedBuildObject>().Length==0 && money.Money==balance,"Planning is non-mutating");
        Check(FloorConstruction.Build(nine,catalog,money) && money.Money==balance-nine.Cost(catalog),"One payment builds all nine tiles");Physics.SyncTransforms();
        var floorRay=new Ray(new Vector3(2,8,2),Vector3.down);
        Check(Physics.Raycast(floorRay,out var floorHit,2) && BuildPlacement.TrySurfacePoint(floor,plot,floorRay,true,floorHit,Vector3.zero,out var floorPoint) && Mathf.Abs(floorPoint.y-7)<.001f,"Aiming at existing upper floor reuses its pivot without centimetre drift");
        var extended=FloorConstruction.Plan(floor,plot,start,new Vector3(14,7,10));
        Check(extended.additions.Count==3 && extended.Cost(catalog)==3*catalog.Cost(floor),"Existing tiles skipped and not charged");
        Check(FloorConstruction.Plan(floor,plot,start,start).additions.Count==0,"Existing first cell can be a drag anchor without duplicate purchase");
        var tile=plot.GetComponentsInChildren<PlacedBuildObject>().First();var removedPosition=tile.transform.position+new Vector3(2,0,2);
        Check(ConstructionEconomy.Sell(catalog,tile,money),"Upper floor sold");Physics.SyncTransforms();
        var repair=FloorConstruction.Plan(floor,plot,removedPosition,removedPosition);
        Check(repair.additions.Count==1 && FloorConstruction.Build(repair,catalog,money),"Explicit construction restores intentionally deleted tile");
        Clear();save.SetCurrent(new SaveData{money=1,ownedProperties=new[]{971}});
        Check(!FloorConstruction.Build(nine,catalog,money) && money.Money==1 && plot.GetComponentsInChildren<PlacedBuildObject>().Length==0,"Insufficient funds never builds a partial rectangle");
        save.SetCurrent(new SaveData{money=1000000,ownedProperties=new[]{971}});
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=new Vector3(2,7,2);Physics.SyncTransforms();
        Check(!FloorConstruction.Validate(FloorConstruction.Plan(floor,plot,start,start)),"Physical obstruction is still rejected");Object.DestroyImmediate(obstacle);
        Check(!FloorConstruction.Validate(FloorConstruction.Plan(floor,plot,start,new Vector3(90,7,90))),"Outside-plot rectangle rejected");
        Check(!FloorConstruction.Validate(FloorConstruction.Plan(floor,plot,start,new Vector3(200,7,200))),"Oversized rectangle bounded safely");
        foreach(float yaw in new[]{0f,90f,180f,270f})
        {
            Clear();var r=Record(stairs,Vector3.zero,Quaternion.Euler(0,yaw,0));
            StairConstruction.Landing(catalog,plot,r,out var landing,out float level);
            Check(StairConstruction.PrefabFloors(stairs,plot,r,level).Count>0,"Embedded upper floors of revised prefab are recognised, yaw="+yaw);
            // Reproduce an old sold stair whose void lies across the new exit.
            var old=Record(stairs,r.rotation*new Vector3(0,0,8),r.rotation);
            var oldItem=ConstructionPersistence.Spawn(catalog,old,true,scene);oldItem.Demolish();Physics.SyncTransforms();
            var plan=StairConstruction.Plan(catalog,plot,r);
            Check(string.IsNullOrEmpty(plan.error),"New staircase after sold neighbour, yaw="+yaw+": "+plan.error);
            var coverage=Planned(plan,level);coverage.AddRange(StairConstruction.PrefabFloors(stairs,plot,r,level));
            Check(Area(RoomGeometry.Missing(Region(landing),coverage))<.001f,"Full required landing despite old tombstone, yaw="+yaw);
            var built=ConstructionEconomy.Build(catalog,r,plot,money);
            Check(built,"Updated prefab builds with complete exit, yaw="+yaw);Physics.SyncTransforms();
            Check(Area(RoomGeometry.Missing(Region(landing),rooms.SurfacesAt(level,false)))<.001f,"Actual live landing coverage, yaw="+yaw);
            var midpoint=new Vector3(landing.center.x,level,landing.center.y);
            Check(Physics.Raycast(midpoint+Vector3.up,Vector3.down,out var hit,1.1f),"Exit has real supporting collider, yaw="+yaw);
            // A larger selection around a live staircase preserves the opening,
            // but permits extending the existing landing without whole-cell rejection.
            var manual=FloorConstruction.Plan(floor,plot,midpoint,midpoint+r.rotation*new Vector3(4,0,8));
            Check(FloorConstruction.Validate(manual),"Manual extension from landing is valid, yaw="+yaw);
            StairConstruction.Passage(catalog,plot,r,out var opening,out _);
            Check(manual.additions.All(t=>Area(RoomGeometry.Missing(Region(opening),Enumerable.Range(0,t.floorTriangles.Length/3).Select(i=>t.floorTriangles.Skip(i*3).Take(3).Select(p=>t.position+t.rotation*p).Select(p=>new Vector2(p.x,p.z)).ToList())))>=opening.width*opening.height-.001f),"Floor extension never seals active stair hole, yaw="+yaw);
            Check(ConstructionEconomy.Sell(catalog,built,money),"Updated staircase can be sold, yaw="+yaw);Physics.SyncTransforms();
            r.id=Guid.NewGuid().ToString("N");var rebuilt=ConstructionEconomy.Build(catalog,r,plot,money);
            Check(rebuilt,"Rebuild after sale retains a usable landing, yaw="+yaw);Physics.SyncTransforms();
            Check(Area(RoomGeometry.Missing(Region(landing),rooms.SurfacesAt(level,false)))<.001f,"Rebuilt exit complete, yaw="+yaw);
        }
        Clear();
        // Cover the bug independently of the user's newly embedded landing: old
        // saves/prefabs without it still require a paid, physical exit floor.
        var originalPrefab=stairs.prefab;var legacy=Object.Instantiate(originalPrefab);legacy.name="Stairs";legacy.SetActive(false);
        foreach(var box in legacy.GetComponentsInChildren<BoxCollider>(true))
            if(box.GetComponentInParent<BuildFloorCoverage>(true) && legacy.transform.InverseTransformPoint(box.transform.TransformPoint(box.center)).y>6.9f)box.enabled=false;
        stairs.prefab=legacy;
        try
        {
            var r=Record(stairs,Vector3.zero,Quaternion.identity);StairConstruction.Landing(catalog,plot,r,out var landing,out float level);
            var old=Record(stairs,new Vector3(0,0,8),Quaternion.identity);old.deleted=true;BuildDeletionLedger.For(plot).Remember(old);
            var deletedFloor=Record(floor,new Vector3(landing.xMin,level,landing.yMin),Quaternion.identity);deletedFloor.deleted=true;BuildDeletionLedger.For(plot).Remember(deletedFloor);
            var plan=StairConstruction.Plan(catalog,plot,r);
            Check(string.IsNullOrEmpty(plan.error) && Area(RoomGeometry.Missing(Region(landing),Planned(plan,level)))<.001f,"Legacy prefab exit repaired despite sold-stair AND sold-floor history");
            Check(plan.additions.Any(t=>t.floorTriangles!=null && Mathf.Abs(t.position.y-level)<.06f),"Missing legacy landing is explicitly included in paid additions");
        }
        finally{stairs.prefab=originalPrefab;Object.DestroyImmediate(legacy);}
        Clear();
        // Exercise the controller's actual preview lifetime and cancellation.
        var controller=new GameObject("Floor stroke controller").AddComponent<ConstructionController>();controller.catalog=catalog;controller.showPlacementGuides=false;
        var ui=new GameObject("Test UI").AddComponent<BuildMenuView>();ui.status=new GameObject("Status",typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();ui.status.transform.SetParent(ui.transform);
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        var ghost=MeshPreview.Create(floor.prefab,Vector3.zero,Quaternion.identity,Vector3.one,material,"Test floor ghost");
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=controller.GetType();
        void Set(string n,object v)=>type.GetField(n,flags).SetValue(controller,v);
        object Get(string n)=>type.GetField(n,flags).GetValue(controller);
        void Call(string n,params object[] args)=>type.GetMethod(n,flags).Invoke(controller,args);
        Set("view",ui);Set("selected",floor);Set("plot",plot);Set("economy",money);Set("ghostVisual",ghost);Set("ghost",ghost.Root);Set("validMaterial",material);Set("invalidMaterial",material);Set("floorStrokeStart",start);
        balance=money.Money;
        foreach(int size in new[]{1,2,3,2,1})
        {
            Call("UpdateFloorPreview",start+new Vector3((size-1)*4,0,(size-1)*4));
            var previews=(List<MeshPreview>)Get("floorPreviews");
            Check(previews.Count==size*size && previews.All(p=>p.Root.GetComponentsInChildren<Collider>().Length==0),"Controller rectangle previews resize without colliders: "+size);
        }
        Call("CancelPlacement");
        Check(Get("floorStrokeStart")==null && ((List<MeshPreview>)Get("floorPreviews")).Count==0 && money.Money==balance && plot.GetComponentsInChildren<PlacedBuildObject>().Length==0,"Right-click cancellation path clears previews without charging or building");
        Set("view",null);Set("validMaterial",null);Set("invalidMaterial",null);Object.DestroyImmediate(ui.gameObject);Object.DestroyImmediate(material);Object.DestroyImmediate(controller.gameObject);
        Clear();Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);
        Debug.Log("FLOOR_DRAG_LANDING_OK checks="+report.Count);
    }
}
#endif
