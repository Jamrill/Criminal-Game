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

public static class PerimeterPlacementValidation
{
    public static void Full()
    {
        Run();ConstructionMountValidation.Run();WallFamilyValidation.Run();
        ConstructionEditingUpgradeValidation.Run();
    }
    public static void Run()
    {
        var results=new List<string>();
        void Check(bool ok,string name){results.Add((ok?"PASS ":"FAIL ")+name);Debug.Log(results.Last());}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));
        catalog.developmentFreeConstruction=false;
        BuildEntry Entry(string name)=>catalog.entries.First(e=>e.prefab.name.Equals(name,StringComparison.OrdinalIgnoreCase));
        var plot=new GameObject("PerimeterTest").AddComponent<BuildPlot>();
        plot.propertyId=986;plot.catalog=catalog;plot.buildable=true;plot.Volume.size=new Vector3(32,40,32);
        PlacedBuildObject Spawn(BuildEntry e,Vector3 pos,Quaternion rot)=>ConstructionPersistence.Spawn(catalog,new BuildRecord{
            id=Guid.NewGuid().ToString("N"),definitionId=e.id,propertyId=plot.propertyId,sceneName=scene.name,
            position=pos,rotation=rot,scale=e.prefab.transform.localScale,finish=-1},true,scene);
        void Clear(){foreach(var p in plot.GetComponentsInChildren<PlacedBuildObject>(true))Object.DestroyImmediate(p.gameObject);Physics.SyncTransforms();}
        var wall=Entry("Wall_complete");var hole=Entry("Wall_door_hole");var door=Entry("Door");var large=Entry("Glass_Wall_door_complete");
        var terrain=GameObject.CreatePrimitive(PrimitiveType.Cube);terrain.name="Terrain below foundation";
        terrain.transform.position=new Vector3(0,-2,0);terrain.transform.localScale=new Vector3(50,1,50);Physics.SyncTransforms();
        var ray=new Ray(new Vector3(0,4,0),new Vector3(1,-1,0));Physics.Raycast(ray,out var hit,100);
        Check(BuildPlacement.TrySurfacePoint(wall,plot,ray,true,hit,Vector3.zero,out var point)&&Mathf.Abs(point.y)<.001f,"Wall ignores terrain below foundation");
        Check(BuildPlacement.TrySurfacePoint(wall,plot,ray,true,hit,new Vector3(0,3,0),out point)&&Mathf.Abs(point.y-3)<.001f,"Terrain at perimeter does not pull an upper-storey preview down");
        Check(!BuildPlacement.FitsPlot(wall,plot,new Vector3(0,-1,0),Quaternion.identity),"Cannot commit wall below foundation");
        Check(Mathf.Abs(BuildPlacement.WallSupportLevel(plot,new Vector3(0,.1f,0)).y)<.001f,"No supporting tile falls back to foundation, not terrain");
        var floor=Spawn(Entry("Floor"),new Vector3(0,7,0),Quaternion.identity);Physics.SyncTransforms();
        Check(Mathf.Abs(BuildPlacement.WallSupportLevel(plot,new Vector3(2,7.1f,2)).y-7)<.02f,"Player's upper-storey floor supplies fallback height");
        ray=new Ray(new Vector3(2,10,2),Vector3.down);Physics.Raycast(ray,out hit,100);
        Check(BuildPlacement.TrySurfacePoint(wall,plot,ray,true,hit,Vector3.zero,out point)&&Mathf.Abs(point.y-7)<.02f,"Directly targeted upper floor remains usable");
        Object.DestroyImmediate(floor.gameObject);
        Object.DestroyImmediate(terrain);
        for(int turn=0;turn<4;turn++)
        {
            Clear();var rot=Quaternion.Euler(0,turn*90,0);
            var host=Spawn(hole,rot*new Vector3(-2,0,-15.7499f),rot);Physics.SyncTransforms();
            for(int side=0;side<2;side++)
            {
                var r=new Ray(host.transform.TransformPoint(new Vector3(2,2,side==0?-4:4)),host.transform.forward*(side==0?1:-1));
                bool poseFound=BuildMounting.TryPose(door,catalog,plot,r,side,null,out var pose);
                Check(poseFound&&BuildMounting.IsValid(door,catalog,plot,pose),"Door fits perimeter, rotation "+turn+", side "+side);
                if(poseFound)Check(BuildPlacement.FitsPlot(door,plot,pose.position,pose.rotation),"Entire door stays within plot "+turn+"/"+side);
            }
        }
        for(int turn=0;turn<4;turn++)for(int count=1;count<=3;count++)
        {
            Clear();var rot=Quaternion.Euler(0,turn*90,0);PlacedBuildObject first=null;
            for(int i=0;i<count;i++)
            {
                var item=Spawn(i==0?wall:i==1?Entry("Window_Wall"):hole,rot*new Vector3(-6+i*4,0,-15.7499f),rot);
                if(first==null)first=item;
            }
            Physics.SyncTransforms();var aim=first.transform.TransformPoint(new Vector3(2,2,0));
            var plan=WallReconfiguration.Opening(catalog,plot,first,large,aim);
            bool valid=WallReconfiguration.Validate(plan,catalog,out var reason);
            Check(valid&&plan.removed.Count==count&&plan.added.Count==1,"Large glass replaces "+count+" mixed walls at rotation "+turn+": "+reason);
            if(valid)Check(plan.Difference(catalog)==catalog.Cost(large)-plan.removed.Sum(p=>catalog.Cost(catalog.Find(p.definitionId))),"Replacement charges only catalogue difference");
        }
        // Corner fit, neighbouring walls, same-type no-op and real blockers.
        for(int turn=0;turn<4;turn++)
        {
            Clear();var rot=Quaternion.Euler(0,turn*90,0);
            var first=Spawn(wall,rot*new Vector3(-15.9998f,0,-15.7499f),rot);Physics.SyncTransforms();
            var plan=WallReconfiguration.Opening(catalog,plot,first,large,first.transform.TransformPoint(new Vector3(2,2,0)));
            Check(WallReconfiguration.Validate(plan,catalog,out _),"Corner selects inward extension "+turn);
            if(plan.added.Count>0)Check(BuildPlacement.FitsPlot(large,plot,plan.added[0].position,plan.added[0].rotation),"Corner extension never leaves plot "+turn);
        }
        Clear();var existing=Spawn(large,new Vector3(-6,0,0),Quaternion.identity);Physics.SyncTransforms();
        Check(!WallReconfiguration.Validate(WallReconfiguration.Opening(catalog,plot,existing,large,new Vector3(0,2,0)),catalog,out _),"Large glass cannot replace itself");
        Clear();var original=Spawn(wall,Vector3.zero,Quaternion.identity);Physics.SyncTransforms();
        var direct=WallReconfiguration.Placement(catalog,plot,large,Vector3.zero,Quaternion.identity);
        Check(direct!=null&&WallReconfiguration.Validate(direct,catalog,out _),"Free placement also recognizes short-wall overlap");
        var partial=WallReconfiguration.Placement(catalog,plot,large,new Vector3(2,0,0),Quaternion.identity);
        Check(partial!=null&&!WallReconfiguration.Validate(partial,catalog,out _),"Partial cuts never silently delete the rest of a wall");
        var upper=Spawn(wall,new Vector3(0,7,0),Quaternion.identity);Physics.SyncTransforms();
        direct=WallReconfiguration.Placement(catalog,plot,large,Vector3.zero,Quaternion.identity);
        Check(direct!=null&&!direct.removed.Contains(upper),"Does not consume upper-storey walls");
        Object.DestroyImmediate(upper.gameObject);
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=new Vector3(6,2,0);Physics.SyncTransforms();
        Check(!WallReconfiguration.Validate(direct,catalog,out _),"Unrelated furniture still blocks long replacement");Object.DestroyImmediate(obstacle);
        plot.Volume.size=new Vector3(8,40,8);Physics.SyncTransforms();
        Check(!WallReconfiguration.Validate(WallReconfiguration.Opening(catalog,plot,original,large,new Vector3(2,2,0)),catalog,out _),"Plot too small rejects replacement");
        plot.Volume.size=new Vector3(32,40,32);
        Clear();var tee=Spawn(Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);Physics.SyncTransforms();
        var arm=WallGeometry.World(tee.GetComponent<WallTopology>())[0];
        var teePlan=WallReconfiguration.Opening(catalog,plot,tee,large,(arm.a+arm.b)*.5f+Vector3.up*2);
        Check(WallReconfiguration.Validate(teePlan,catalog,out _)&&teePlan.added.Count>1,"Extending a T arm preserves the other branches");
        // Real transaction, installed door, ledger and save/load.
        Clear();var services=new GameObject("PerimeterServices");var save=services.AddComponent<SaveService>();
        var money=services.AddComponent<EconomyService>();var property=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,property})service.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,save);
        var data=new SaveData{slotId=1,money=100000,ownedProperties=new[]{986}};
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{data});
        var hostDoor=Spawn(hole,new Vector3(-6,0,0),Quaternion.identity);
        var window=Spawn(Entry("Window_Wall"),new Vector3(-2,0,0),Quaternion.identity);
        var plain=Spawn(wall,new Vector3(2,0,0),Quaternion.identity);Physics.SyncTransforms();
        BuildMounting.TryPose(door,catalog,plot,new Ray(new Vector3(-4,2,-4),Vector3.forward),0,null,out var mounted);
        var installed=Spawn(door,mounted.position,mounted.rotation);Physics.SyncTransforms();
        var transaction=WallReconfiguration.Opening(catalog,plot,hostDoor,large,new Vector3(-4,2,0));
        Check(WallReconfiguration.Validate(transaction,catalog,out _)&&transaction.removed.Count==4&&transaction.removed.Contains(installed),"Integrated glass replaces three sections including installed door");
        int delta=transaction.Difference(catalog),before=money.Money;
        var removedIds=transaction.removed.Select(p=>p.persistentId).ToArray();
        Check(WallReconfiguration.Apply(transaction,catalog,money,out var error)&&money.Money==before-delta,"Atomic replacement charges once, including old door refund: "+error);
        var records=BuildDeletionLedger.CaptureScene(scene);
        Check(removedIds.All(id=>records.Any(r=>r.id==id&&r.deleted))&&records.Count(r=>!r.deleted)==1,"Save contains one new wall and deletion records for all replaced objects");
        var snapshot=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData{buildings=records}));
        Clear();var persistence=services.AddComponent<ConstructionPersistence>();persistence.catalog=catalog;persistence.RestoreScene(snapshot,scene);
        Check(plot.GetComponentsInChildren<PlacedBuildObject>().Length==1&&plot.GetComponentsInChildren<PlacedBuildObject>()[0].definitionId==large.id,"Save restore keeps replacement under Solar without resurrecting old walls/door");
        Clear();Object.DestroyImmediate(catalog);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        File.WriteAllLines("Documentation/PerimeterPlacementValidation.txt",results);
        if(results.Any(s=>s.StartsWith("FAIL")))throw new Exception("Perimeter placement regression failures: "+results.Count(s=>s.StartsWith("FAIL")));
        Debug.Log("PERIMETER_PLACEMENT_VALIDATION_OK checks="+results.Count);
    }
}
#endif
