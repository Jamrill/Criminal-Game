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

public static class FloorFoundationValidation
{
    public static void Run()
    {RunConstruction();ButtonPrefabMigration.Validate();}
    public static void RunConstruction()
    {
        var report=new List<string>();
        void Check(bool value,string text)
        {report.Add((value?"PASS ":"FAIL ")+text);File.WriteAllLines("Documentation/FloorFoundationValidation.txt",report);if(!value)throw new Exception(text);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var floor=catalog.entries.Single(FloorEditing.IsFloor);var stairs=catalog.entries.Single(StairConstruction.IsStairs);
        var plot=new GameObject("Foundation rules test").AddComponent<BuildPlot>();plot.propertyId=992;plot.buildable=true;plot.catalog=catalog;plot.Volume.size=Vector3.one*200;
        var rooms=plot.gameObject.AddComponent<RoomConstruction>();
        var services=new GameObject("Isolated foundation test services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var properties=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,properties})service.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,save);
        var data=new SaveData{slotId=1,money=1000000,ownedProperties=new[]{992}};
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{data});
        BuildRecord Record(BuildEntry entry,string id,Vector3 position)=>new(){id=id,definitionId=entry.id,propertyId=992,sceneName=scene.name,position=position,rotation=Quaternion.identity,scale=Vector3.one,finish=-1};
        void Reload()
        {
            var records=BuildDeletionLedger.CaptureScene(scene);
            var copy=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData{buildings=records}));
            foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>(true))Object.DestroyImmediate(item.gameObject);
            foreach(var record in copy.buildings)ConstructionPersistence.Spawn(catalog,record,true,scene);
        }
        try
        {
            var points=new[]{Vector3.zero,new Vector3(4,0,0),new Vector3(4,0,4),new Vector3(0,0,4)};
            for(int i=0;i<4;i++)
            {var wall=new GameObject("Closed room wall").AddComponent<WallTopology>();wall.transform.SetParent(plot.transform,false);wall.height=7;wall.segmentPoints=new[]{points[i],points[(i+1)%4]};}
            rooms.Rebuild(true,money);
            var tiles=plot.GetComponentsInChildren<PlacedBuildObject>().Where(p=>p.ProtectedFloor).ToArray();
            var foundation=tiles.Single(p=>Mathf.Abs(p.transform.position.y)<.06f);
            var roof=tiles.Single(p=>p.transform.position.y>6);
            int balance=money.Money;
            Check(!FloorEditing.CanRemove(foundation) && !ConstructionEconomy.Sell(catalog,foundation,money) && money.Money==balance,"Foundation tile cannot be sold");
            foundation.Demolish();Check(!foundation.deleted && foundation.gameObject.activeSelf,"Direct demolition also protects foundations");
            Check(FloorEditing.CanRemove(roof) && ConstructionEconomy.Sell(catalog,roof,money),"A closed room's upper slab can be sold");
            balance=money.Money;rooms.Rebuild(true,money);
            Check(roof.deleted && money.Money==balance && plot.GetComponentsInChildren<PlacedBuildObject>().Count(p=>p.ProtectedFloor)==1,"Automatic room completion does not refill or charge for a sold upper slab");
            Reload();balance=money.Money;rooms.Rebuild(true,money);
            Check(money.Money==balance && plot.GetComponentsInChildren<PlacedBuildObject>().Count(p=>p.ProtectedFloor)==1,"Removed upper slab remains absent after JSON save/load");
            var stairRecord=Record(stairs,"persistent-stair",new Vector3(32,0,24));
            var built=ConstructionEconomy.Build(catalog,stairRecord,plot,money);
            Check(built,"Stair with hall builds successfully");
            StairConstruction.Passage(catalog,plot,stairRecord,out var opening,out float height);
            Check(ConstructionEconomy.Sell(catalog,built,money) && built.deleted,"Stair can be sold independently of its hall");
            balance=money.Money;rooms.Rebuild(true,money);Physics.SyncTransforms();
            Check(BuildPlacement.IsValid(stairs,plot,stairRecord.position,stairRecord.rotation,stairRecord.position),"Sold staircase can be placed again at its exact original pose");
            var replacementPlan=StairConstruction.Plan(catalog,plot,stairRecord);
            Check(string.IsNullOrEmpty(replacementPlan.error),"Sold staircase can reuse its existing hall: "+replacementPlan.error);
            var rounded=stairRecord.position+new Vector3(.2f,0,-.3f);
            Check(StairConstruction.SnapVacatedPosition(stairs,plot,rounded,stairRecord.rotation)==stairRecord.position,"Nearby rounded preview snaps to sold stair's original pivot");
            Check(StairConstruction.SnapVacatedPosition(stairs,plot,rounded+Vector3.up*7,stairRecord.rotation)==rounded+Vector3.up*7,"Vacated-position snap does not pull a stair down from another storey");
            Check(StairConstruction.SnapVacatedPosition(stairs,plot,rounded,Quaternion.Euler(0,90,0))==rounded,"Vacated-position snap respects deliberate rotation");
            Check(money.Money==balance && !Physics.Raycast(new Vector3(opening.center.x,height+1,opening.center.y),Vector3.down,2),"Removing the stair leaves its opening empty without a refill charge");
            var manual=BuildPlacement.Position(floor,new Vector3(opening.center.x,height,opening.center.y),Quaternion.identity);
            Check(FloorEditing.CanPlace(floor,plot,manual,Quaternion.identity),"Player may manually close an opening after the stair is removed");
            Reload();balance=money.Money;rooms.Rebuild(true,money);Physics.SyncTransforms();
            Check(money.Money==balance && !Physics.Raycast(new Vector3(opening.center.x,height+1,opening.center.y),Vector3.down,2),"Sold-stair opening remains open after save/load and automatic completion");
            var upper=catalog.entries.Single(FloorEditing.IsFloor);
            var plain=ConstructionPersistence.Spawn(catalog,Record(upper,"plain-upper",new Vector3(52,6.99f,40)),true,scene);
            Check(FloorEditing.CanRemove(plain) && ConstructionEconomy.Sell(catalog,plain,money),"Ordinary non-procedural upper floor is removable too");
            var shape=new RoomGeometry.Region{min=new Vector2(52,40),max=new Vector2(56,44),outline=new(){new(52,40),new(56,40),new(56,44),new(52,44)}};
            var excluded=StairConstruction.WithPassages(shape,plot,7);
            Check(RoomGeometry.Missing(excluded,new List<List<Vector2>>()).Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)))<.0001f,"Deleted ordinary tile contributes its exact intentional opening");
        }
        finally{Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);}
        StairHallValidation.Run();WallAutoJoinValidation.Full();
        Debug.Log("FLOOR_FOUNDATION_VALIDATION_OK checks="+report.Count);
    }
}
#endif
