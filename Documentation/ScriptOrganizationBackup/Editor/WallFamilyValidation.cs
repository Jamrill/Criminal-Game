using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using JuegoCriminal.Construction;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class WallFamilyValidation
{
    public static void Full(){Run();WallReconfigurationRegression.Full();}
    public static void Run()
    {
        var results=new List<string>();
        void Check(bool value,string description)
        {
            results.Add((value?"PASS ":"FAIL ")+description);File.WriteAllLines("Documentation/WallFamilyValidation.txt",results);
            if(!value)throw new Exception(description);
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var plot=new GameObject("FamilyTestSolar").AddComponent<BuildPlot>();plot.catalog=catalog;plot.propertyId=991;plot.buildable=true;plot.Volume.size=Vector3.one*150;BuildPlot.Active.Add(plot);
        var services=new GameObject("Services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var property=services.AddComponent<PropertyService>();
        typeof(EconomyService).GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(money,save);
        typeof(PropertyService).GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(property,save);
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{new SaveData{slotId=1,money=100000,ownedProperties=new[]{991}}});
        BuildEntry Entry(string name)=>catalog.entries.First(e=>e.prefab.name.Equals(name,StringComparison.OrdinalIgnoreCase));
        PlacedBuildObject Spawn(BuildEntry e,Vector3 pos,Quaternion rot)=>ConstructionPersistence.Spawn(catalog,new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=e.id,propertyId=991,sceneName=scene.name,position=pos,rotation=rot,scale=e.prefab.transform.localScale,finish=-1},false,scene);
        void Clear(){foreach(var p in plot.GetComponentsInChildren<PlacedBuildObject>(true))Object.DestroyImmediate(p.gameObject);Physics.SyncTransforms();}
        var names=new[]{"Wall_complete","Window_Wall","Wall_door_hole","Glass_wall"};
        foreach(var from in names)foreach(var to in names)for(int turn=0;turn<4;turn++)
        {
            Clear();var old=Entry(from);var next=Entry(to);var rotation=Quaternion.Euler(0,turn*90,0);
            var item=Spawn(old,Vector3.zero,rotation);var aim=rotation*new Vector3(2,2,0);Physics.SyncTransforms();
            var change=WallReconfiguration.Opening(catalog,plot,item,next,aim);
            bool valid=WallReconfiguration.Validate(change,catalog,out var reason);
            if(from==to)Check(!valid && !item.deleted,from+" cannot replace itself at "+turn);
            else
            {
                Check(valid,from+" -> "+to+" pose valid at "+turn+": "+reason);
                int before=money.Money;int delta=catalog.Cost(next)-catalog.Cost(old);
                Check(WallReconfiguration.Apply(change,catalog,money,out reason) && item.deleted && money.Money==before-delta,from+" -> "+to+" transaction at "+turn+": "+reason);
            }
        }
        foreach(var shape in new[]{"Corner_Wall","T_wall_complete","+_wall_complete"})
        {
            Clear();var item=Spawn(Entry(shape),Vector3.zero,Quaternion.identity);
            var spans=WallGeometry.World(item.GetComponent<WallTopology>());
            var chosen=spans[0];var change=WallReconfiguration.Opening(catalog,plot,item,Entry("Glass_wall"),(chosen.a+chosen.b)*.5f+Vector3.up*2);
            Check(WallReconfiguration.Apply(change,catalog,money,out var reason) && plot.GetComponentsInChildren<PlacedBuildObject>().Any(p=>WallGeometry.Family(catalog.Find(p.definitionId))=="glass"),shape+" arm converts to glass and preserves the remainder: "+reason);
        }
        Clear();var hole=Spawn(Entry("Wall_door_hole"),Vector3.zero,Quaternion.identity);
        var doorEntry=Entry("Door");var socket=Entry("Wall_door_hole").doorway;var anchor=socket.center-Vector3.up*socket.extents.y;
        var door=Spawn(doorEntry,anchor-new Vector3(doorEntry.bounds.center.x,doorEntry.bounds.min.y,doorEntry.bounds.center.z),Quaternion.identity);
        door.GetComponentsInChildren<Transform>().First(t=>t.name=="Sheet").localRotation=Quaternion.Euler(0,90,0);Physics.SyncTransforms();
        var blocked=WallReconfiguration.Opening(catalog,plot,hole,Entry("Glass_wall"),new Vector3(2,2,0));
        Check(!WallReconfiguration.Validate(blocked,catalog,out var message) && message==JuegoCriminal.Localization.GameLanguage.T("Remove or sell the installed door first","Retira o vende primero la puerta instalada en ese hueco") && !hole.deleted,"Installed door blocks removal of its supporting hole even while open");
        ConstructionEconomy.Sell(catalog,door,money);Physics.SyncTransforms();
        Check(WallReconfiguration.Apply(blocked,catalog,money,out _),"Selling the door permits conversion of its old doorway");
        Clear();hole=Spawn(Entry("Wall_door_hole"),Vector3.zero,Quaternion.identity);
        var ray=new Ray(new Vector3(2,2,-5),Vector3.forward);
        Check(ConstructionEditing.FindWall(ray,plot,catalog,null,out var target,out _) && target==hole,"Ray through an empty doorway still finds the supporting wall");
        var tool=new ConstructionEditing();var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        tool.Tick(ray,plot,catalog,Entry("Glass_wall"),mat,mat,true,null,money);
        Check(hole.deleted && plot.GetComponentsInChildren<PlacedBuildObject>().Any(p=>WallGeometry.Family(catalog.Find(p.definitionId))=="glass"),"Real aimed click replaces an empty doorway with glass");
        tool.Dispose();Object.DestroyImmediate(mat);
        Clear();var plain=Spawn(Entry("Wall_complete"),Vector3.zero,Quaternion.identity);
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=new Vector3(2,2,-2);Physics.SyncTransforms();
        Check(!ConstructionEditing.FindWall(ray,plot,catalog,null,out _,out _),"Virtual wall targeting cannot pass through furniture/obstacles");Object.DestroyImmediate(obstacle);
        Clear();var semi=Spawn(catalog.entries.First(e=>e.prefab.name.Equals("Glass_wall_semincomplete",StringComparison.OrdinalIgnoreCase)),Vector3.zero,Quaternion.identity);
        Check(!WallReconfiguration.Validate(WallReconfiguration.Opening(catalog,plot,semi,Entry("Glass_wall"),new Vector3(2,2,0)),catalog,out _),"Glass semi/complete variants count as the same type");
        Clear();var tee=Spawn(Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);Physics.SyncTransforms();
        Check(ConstructionEditing.FindWall(new Ray(new Vector3(4,2,7),Vector3.back),plot,catalog,null,out var endTarget,out var endPoint) &&
            endTarget==tee && endPoint.z>3.5f,"An end-cap hit selects the actual front arm, not a virtual wall behind it");
        Clear();Object.DestroyImmediate(catalog);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Debug.Log("WALL_FAMILY_VALIDATION_OK checks="+results.Count);
    }
}
