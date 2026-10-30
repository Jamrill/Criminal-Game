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

public static class ConstructionEditingUpgradeValidation
{
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/ConstructionEditingUpgradeValidation.txt",report);if(!ok)throw new Exception(label);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var wall=catalog.entries.First(e=>e.prefab.name=="Wall_complete");
        var plot=new GameObject("Data-only deletion and brush test").AddComponent<BuildPlot>();plot.propertyId=713;plot.buildable=true;plot.catalog=catalog;plot.Volume.size=new Vector3(44.5f,28,44.5f);
        plot.gameObject.AddComponent<RoomConstruction>();
        var services=new GameObject("Upgrade test services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var properties=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,properties})service.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,save);
        var data=new SaveData{slotId=1,money=1000000,ownedProperties=new[]{713}};typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{data});
        BuildRecord Record(string id,Vector3 pos,Quaternion rot)=>new(){id=id,definitionId=wall.id,propertyId=713,sceneName=scene.name,position=pos,rotation=rot,scale=wall.prefab.transform.localScale,finish=-1};
        try
        {
            var sold=ConstructionEconomy.Build(catalog,Record("sold",new Vector3(-16,0,-16),Quaternion.identity),plot,money);
            int balance=money.Money;Check(sold && ConstructionEconomy.Sell(catalog,sold,money) && !sold,"Sold model and all its children are actually destroyed");
            Check(money.Money==balance+catalog.Cost(wall) && !ConstructionEconomy.Sell(catalog,sold,money),"Deletion refunds once, never twice");
            Check(BuildDeletionLedger.Deleted(plot).Any(r=>r.id=="sold") && plot.GetComponentsInChildren<PlacedBuildObject>(true).Length==0,"Only plain deletion data remains, no inactive construction object");
            var snapshot=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData{buildings=BuildDeletionLedger.CaptureScene(scene)}));
            var authored=ConstructionPersistence.Spawn(catalog,Record("sold",new Vector3(-16,0,-16),Quaternion.identity),false,scene);
            var persistence=services.AddComponent<ConstructionPersistence>();persistence.catalog=catalog;persistence.RestoreScene(snapshot,scene);
            Check(!authored && BuildDeletionLedger.CaptureScene(scene).Count(r=>r.deleted)==1,"Old save tombstones delete authored scene models on load without recreating hidden prefabs");
            var old=ConstructionPersistence.Spawn(catalog,Record("legacy",new Vector3(-12,0,-16),Quaternion.identity),false,scene);old.deleted=true;old.gameObject.SetActive(false);
            BuildDeletionLedger.Migrate(plot);
            Check(!old && BuildDeletionLedger.Deleted(plot).Any(r=>r.id=="legacy"),"Legacy inactive sold models migrate to data-only records");
            plot.transform.SetPositionAndRotation(new Vector3(450,100,500),Quaternion.Euler(0,30,0));plot.transform.localScale=new Vector3(1.2f,1,.8f);
            Check(Vector3.Distance(BuildDeletionLedger.Deleted(plot).Single(r=>r.id=="sold").position,plot.transform.TransformPoint(new Vector3(-16,0,-16)))<.001f,"Deleted-opening data follows a moved, rotated and scaled Solar without retaining its old model");
            foreach(var entry in catalog.entries.Where(e=>e.prefab && e.prefab.GetComponent<WallTopology>()))
            foreach(float angle in new[]{0f,90f,180f,270f})
            {
                var rotation=plot.Grid.rotation*Quaternion.Euler(0,angle,0);
                var outside=plot.transform.TransformPoint(new Vector3(35,0,35));
                var clamped=BuildPlacement.ClampToPlot(entry,plot,outside,rotation);
                Check(BuildPlacement.FitsPlot(entry,plot,clamped,rotation),"Full bounds fit rotated/scaled plot edge: "+entry.prefab.name+" / "+angle);
                Check(Mathf.Abs(Vector3.Dot(clamped-outside,plot.Grid.up))<.001f,"Boundary clamping preserves floor height");
            }
            balance=money.Money;
            Check(!ConstructionEconomy.Build(catalog,Record("outside",plot.transform.TransformPoint(new Vector3(35,0,35)),Quaternion.identity),plot,money) && money.Money==balance,"Economy independently rejects outside-plot construction without a debit");
            plot.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);plot.transform.localScale=Vector3.one;
            var sourceSpan=WallGeometry.Local(wall.prefab.GetComponent<WallTopology>()).Single();
            var corners=new[]{new Vector3(-22,0,-22),new Vector3(22,0,-22),new Vector3(22,0,22),new Vector3(-22,0,22)};
            var graph=new List<RoomGeometry.Edge>();
            for(int side=0;side<4;side++)for(int segment=0;segment<11;segment++)
            {
                var from=Vector3.Lerp(corners[side],corners[(side+1)%4],segment/11f);var to=Vector3.Lerp(corners[side],corners[(side+1)%4],(segment+1)/11f);
                var rotation=Quaternion.FromToRotation(sourceSpan.b-sourceSpan.a,to-from);
                var origin=BuildPlacement.ClampToPlot(wall,plot,from-rotation*sourceSpan.a,rotation);
                foreach(var piece in WallGeometry.At(wall.prefab.GetComponent<WallTopology>(),origin,rotation,wall.prefab.transform.localScale))
                    graph.Add(new RoomGeometry.Edge(new Vector2(piece.a.x,piece.a.z),new Vector2(piece.b.x,piece.b.z)));
            }
            Check(RoomGeometry.Find(graph).Count==1,"Boundary adjustment keeps a complete perimeter closed for automatic room detection");
            var stroke=new WallBuildStroke();var start=new Vector3(-12,0,-8);var first=ConstructionEconomy.Build(catalog,Record("stroke-0",start,Quaternion.identity),plot,money);
            Check(first && stroke.Begin(wall,plot,start,Quaternion.identity),"Successful straight-wall click begins a stroke");
            var span=WallGeometry.Local(wall.prefab.GetComponent<WallTopology>()).Single();var step=Vector3.Scale(span.b-span.a,wall.prefab.transform.localScale);
            balance=money.Money;
            for(int i=1;i<=3;i++)
            {
                Check(stroke.Next(start+step*3,out var next,out int index) && index==i && Vector3.Distance(next,start+step*i)<.001f,"Camera jump fills consecutive span "+i+" instead of skipping walls");
                Check(BuildPlacement.IsValid(wall,plot,next,Quaternion.identity,next),"Brush span joins its neighbour without overlapping it");
                var placed=ConstructionEconomy.Build(catalog,Record("stroke-"+i,next,Quaternion.identity),plot,money);Check(placed,"Brush placement succeeds");stroke.Commit(index);
            }
            Check(!stroke.Next(start+step*3,out _,out _) && money.Money==balance-3*catalog.Cost(wall),"Holding over an existing stamped span neither duplicates nor charges it again");
            Check(stroke.Next(start-step*2,out var reverse,out int negative) && negative==-1 && Vector3.Distance(reverse,start-step)<.001f,"Stroke can extend backwards from the original click");
            stroke.Reset();Check(!stroke.Active,"Release/cancel resets the wall stroke");
            var batch=Enumerable.Range(0,3).Select(i=>Record("deferred-"+i,new Vector3(-12,0,8)+step*i,Quaternion.identity)).ToArray();
            balance=money.Money;int count=plot.GetComponentsInChildren<PlacedBuildObject>().Length;
            Check(money.Money==balance && plot.GetComponentsInChildren<PlacedBuildObject>().Length==count,"Pending wall records do not build or charge before release");
            Check(ConstructionEconomy.BuildWalls(catalog,batch,plot,money) && money.Money==balance-3*catalog.Cost(wall),"Release builds all pending walls with one combined price");
            balance=money.Money;count=plot.GetComponentsInChildren<PlacedBuildObject>().Length;
            Check(!ConstructionEconomy.BuildWalls(catalog,batch,plot,money) && money.Money==balance && plot.GetComponentsInChildren<PlacedBuildObject>().Length==count,"Invalid batch neither builds nor charges");
            var overlapping=new[]{Record("rollback-a",new Vector3(-12,0,16),Quaternion.identity),Record("rollback-b",new Vector3(-12,0,16),Quaternion.identity)};
            Check(!ConstructionEconomy.BuildWalls(catalog,overlapping,plot,money) && money.Money==balance && plot.GetComponentsInChildren<PlacedBuildObject>().Length==count,"Failure between pending walls rolls back the whole batch and payment");
            data.money=0;
            Check(!ConstructionEconomy.BuildWalls(catalog,new[]{Record("unaffordable",new Vector3(-12,0,16),Quaternion.identity)},plot,money) && money.Money==0 && plot.GetComponentsInChildren<PlacedBuildObject>().Length==count,"Insufficient total funds leaves no partial construction");
        }
        finally{Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);}
        LargeRoofValidation.Run();LargeRoofValidation.ActualScene();FloorFoundationValidation.RunConstruction();
        Debug.Log("CONSTRUCTION_EDITING_UPGRADE_OK checks="+report.Count);
    }
}
#endif
