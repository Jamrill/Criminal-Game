#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using JuegoCriminal.Construction;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class StairJunctionValidation
{
    public static void Full(){Run();StairAlignmentValidation.Full();}
    public static void Run()
    {
        var results=new List<string>();
        void Check(bool ok,string label){results.Add((ok?"PASS ":"FAIL ")+label);Debug.Log(results.Last());}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var plot=new GameObject("Upper enclosure regression").AddComponent<BuildPlot>();plot.propertyId=948;plot.catalog=catalog;plot.Volume.size=Vector3.one*200;
        plot.transform.position=new Vector3(474,101,507);
        var rooms=plot.gameObject.AddComponent<RoomConstruction>();
        var stairs=catalog.entries.Single(StairConstruction.IsStairs);
        var straight=catalog.entries.Single(e=>e.prefab.name=="Wall_complete");
        var floor=catalog.entries.Single(FloorEditing.IsFloor);
        var services=new GameObject("Test economy");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var property=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,property})service.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,save);
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{new SaveData{money=1000000,ownedProperties=new[]{948}}});
        List<BuildRecord> Walls(StairBuildPlan plan)=>plan.additions.Where(r=>catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()&&Mathf.Abs(plot.Grid.InverseTransformPoint(r.position).y-7)<.06f).ToList();
        List<WallGeometry.Span> Spans(BuildRecord r)=>WallGeometry.At(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>(),r.position,r.rotation,r.scale);
        void Clear()
        {
            foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>(true))Object.DestroyImmediate(item.gameObject);
            var ledger=plot.GetComponent<BuildDeletionLedger>();if(ledger)Object.DestroyImmediate(ledger);
            rooms.Rebuild();Physics.SyncTransforms();
        }
        PlacedBuildObject Span(BuildEntry entry,Vector3 a,Vector3 b)
        {
            var local=WallGeometry.At(entry.prefab.GetComponent<WallTopology>(),Vector3.zero,Quaternion.identity,entry.prefab.transform.localScale).Single();
            var rot=Quaternion.AngleAxis(Vector3.SignedAngle(local.b-local.a,b-a,Vector3.up),Vector3.up);
            var record=new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=entry.id,propertyId=948,position=a-rot*local.a,rotation=rot,scale=entry.prefab.transform.localScale,finish=-1};
            var item=ConstructionPersistence.Spawn(catalog,record,true,scene);Physics.SyncTransforms();return item;
        }
        foreach(float yaw in new[]{0f,90f,180f,270f})
        {
            var record=new BuildRecord{definitionId=stairs.id,propertyId=948,position=plot.transform.position+new Vector3(1,0,5),rotation=Quaternion.Euler(0,yaw,0),scale=Vector3.one,finish=-1};
            var plan=StairConstruction.Plan(catalog,plot,record);
            Check(string.IsNullOrEmpty(plan.error),"Upper enclosure valid at real world offset, yaw="+yaw+": "+plan.error);
            Check(Walls(plan).Count(r=>Spans(r).Count==2)==4,"Four real Corner prefabs, yaw="+yaw);
            Check(plan.additions.Where(r=>r.floorTriangles!=null).All(r=>Enumerable.Range(0,r.floorTriangles.Length/3).All(i=>
            {
                var a=r.floorTriangles[i*3];var b=r.floorTriangles[i*3+1];var c=r.floorTriangles[i*3+2];
                return RoomGeometry.HasTriangleSurface(new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z));
            })),"No numerical floor/roof ribbons, yaw="+yaw);
            StairConstruction.Passage(catalog,plot,record,out var opening,out float level);
            StairConstruction.Landing(catalog,plot,record,out var landing,out _);
            var corner=plot.Grid.TransformPoint(new Vector3(Mathf.Min(opening.xMin,landing.xMin),level,Mathf.Min(opening.yMin,landing.yMin)));
            var west=Span(straight,corner,corner+Vector3.left*4);string westId=west.persistentId;
            plan=StairConstruction.Plan(catalog,plot,record);
            Check(string.IsNullOrEmpty(plan.error)&&Walls(plan).Any(r=>Spans(r).Count==3),"Existing third arm becomes T, yaw="+yaw+": "+plan.error);
            var south=Span(straight,corner,corner+Vector3.back*4);string southId=south.persistentId;
            plan=StairConstruction.Plan(catalog,plot,record);
            Check(string.IsNullOrEmpty(plan.error)&&Walls(plan).Any(r=>Spans(r).Count==4),"Existing fourth arm becomes +, yaw="+yaw+": "+plan.error);
            Check(plan.edits.Count(e=>e.after.deleted&&WallGeometry.Solid(catalog.Find(e.before.definitionId)))==2,"Only consolidated existing arms are replaced, yaw="+yaw);
            int expected=catalog.Cost(stairs)+plan.additions.Sum(r=>catalog.Cost(catalog.Find(r.definitionId)))-2*catalog.Cost(straight);
            Check(plan.Cost(catalog)==expected,"Consolidated arms credited in total price, yaw="+yaw);
            int balance=money.Money;var built=StairConstruction.Build(catalog,plot,record,money);
            Check(built&&money.Money==balance-expected,"Actual build uses one transaction, yaw="+yaw);
            var saved=BuildDeletionLedger.CaptureScene(scene);
            Check(saved.Any(r=>r.id==westId&&r.deleted)&&saved.Any(r=>r.id==southId&&r.deleted)&&saved.Any(r=>!r.deleted&&catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()&&Spans(r).Count==4),"Save records preserve + and replaced-arm tombstones, yaw="+yaw);
            Clear();
            var window=catalog.entries.First(e=>!e.hiddenInMenu&&WallGeometry.Family(e)=="window");
            var doorway=catalog.entries.First(e=>!e.hiddenInMenu&&WallGeometry.Family(e)=="doorway");
            var w=Span(window,corner,corner+Vector3.right*4);var d=Span(doorway,corner,corner+Vector3.forward*4);
            plan=StairConstruction.Plan(catalog,plot,record);
            Check(string.IsNullOrEmpty(plan.error)&&!plan.edits.Any(e=>e.item==w||e.item==d),"Window and doorway remain intact at corner, yaw="+yaw+": "+plan.error);
            var preserved=WallGeometry.World(w.GetComponent<WallTopology>()).Concat(WallGeometry.World(d.GetComponent<WallTopology>())).ToArray();
            Check(!Walls(plan).SelectMany(Spans).Any(s=>preserved.Any(s.Same)),"No generated solid covers window/doorway, yaw="+yaw);
            Clear();
        }
        // Legacy near-zero-width fragments must not extrude an edge or return as
        // full 4x4 tiles after Capture -> JSON -> Restore.
        var old=new BuildRecord{id="legacy-ribbon",definitionId=floor.id,propertyId=948,position=plot.transform.position+Vector3.up*7,scale=Vector3.one,finish=-1,
            floorTriangles=new[]{Vector3.zero,new Vector3(4,0,0),new Vector3(4,0,.000003f)}};
        var ribbon=ConstructionPersistence.Spawn(catalog,old,true,scene);
        Check(!ribbon.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),"Legacy ribbon has no visible extruded face");
        var captured=JsonUtility.FromJson<BuildRecord>(JsonUtility.ToJson(ribbon.Capture()));Object.DestroyImmediate(ribbon.gameObject);
        ribbon=ConstructionPersistence.Spawn(catalog,captured,true,scene);
        Check(!ribbon.GetComponentsInChildren<Renderer>().Any(r=>r.enabled)&&!ribbon.GetComponentsInChildren<Collider>().Any(c=>c.enabled),"Empty legacy patch stays empty after save/load");
        Check(RoomGeometry.HasTriangleSurface(new Vector2(0,0),new Vector2(4,0),new Vector2(4,.01f)),"Legitimate centimetre edge remains buildable");
        Clear();Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);
        File.WriteAllLines("Documentation/StairJunctionValidation.txt",results);
        if(results.Any(r=>r.StartsWith("FAIL")))throw new Exception("Stair junction regression failures");
        Debug.Log("STAIR_JUNCTION_VALIDATION_OK checks="+results.Count);
    }
    public static void Inspect()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var plot=new GameObject("Stair junction fixture").AddComponent<BuildPlot>();
        plot.propertyId=948;plot.catalog=catalog;plot.Volume.size=Vector3.one*200;
        plot.transform.position=new Vector3(474,101,507);
        var rooms=plot.gameObject.AddComponent<RoomConstruction>();
        var floor=catalog.entries.Single(FloorEditing.IsFloor);
        var stair=catalog.entries.Single(StairConstruction.IsStairs);
        foreach(float yaw in new[]{0f,90f,180f,270f})
        {
            var record=new BuildRecord{definitionId=stair.id,propertyId=948,position=plot.transform.position+new Vector3(1,0,5),rotation=Quaternion.Euler(0,yaw,0),scale=Vector3.one,finish=-1};
            var plan=StairConstruction.Plan(catalog,plot,record);
            Debug.Log("JUNCTION_INSPECT yaw="+yaw+" error="+plan.error+" additions="+plan.additions.Count);
            foreach(var r in plan.additions.Where(r=>r.floorTriangles!=null))
            {
                var t=r.floorTriangles;float smallest=float.MaxValue;int narrow=0;
                for(int i=0;i+2<t.Length;i+=3)
                {
                    float longest=Mathf.Max((t[i]-t[i+1]).magnitude,Mathf.Max((t[i+1]-t[i+2]).magnitude,(t[i+2]-t[i]).magnitude));
                    float width=Vector3.Cross(t[i+1]-t[i],t[i+2]-t[i]).magnitude/longest;
                    smallest=Mathf.Min(smallest,width);if(width<.001f)narrow++;
                }
                if(narrow>0)Debug.Log("NARROW_PATCH "+JsonUtility.ToJson(r)+" width="+smallest);
            }
        }
        Object.DestroyImmediate(plot.gameObject);
        Debug.Log("JUNCTION_INSPECTION_DONE");
    }
}
#endif
