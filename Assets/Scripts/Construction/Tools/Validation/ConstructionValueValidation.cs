#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using JuegoCriminal.Construction;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class ConstructionValueValidation
{
    static readonly List<string> results=new();
    static void Check(bool pass,string name)
    {
        results.Add((pass?"PASS ":"FAIL ")+name);
        File.WriteAllLines("Documentation/ConstructionValueValidation.txt",results);
        if(!pass)throw new Exception(name);
        Debug.Log("VALUE_TEST_PASS "+name);
    }
    static void SetCurrent(SaveService save,SaveData data)=>typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{data});
    static void BindSave(Component service,SaveService save)=>service.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,save);
    public static void Run()
    {
        results.Clear();
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var world=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        var solar=Object.FindObjectsByType<BuildPlot>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        Check(solar.catalog==catalog,"Solar references the priced catalog");
        var markers=solar.GetComponentsInChildren<PlacedBuildObject>(true);
        long realValue=Mathf.Max(0,solar.price)+markers.Where(p=>!p.deleted).Sum(p=>catalog.Find(p.definitionId)?.price??0);
        Check(realValue>1000 && solar.TotalValue==(catalog.FreeConstruction ? 0 : realValue),"Real building price exceeds initial balance; development override is respected");
        Check(markers.All(p=>!string.IsNullOrEmpty(p.persistentId)) && markers.Select(p=>p.persistentId).Distinct().Count()==markers.Length,"Stable unique scene identities");
        Check(markers.All(p=>p.propertyId==solar.propertyId && catalog.Find(p.definitionId)!=null),"All registered elements have a definition and correct property");
        var floors=solar.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)=="Assets/Prefabs/Floor.prefab" && Mathf.Abs(t.position.y-101)<.01f).ToArray();
        Check(floors.Length==121 && floors.Select(f=>f.position).Distinct().Count()==121,"Base floor: 121 tiles without duplicates");
        Check(floors.All(f=>Mathf.Abs((f.position.x-452)/4-Mathf.Round((f.position.x-452)/4))<.001f &&
            Mathf.Abs((f.position.z-485)/4-Mathf.Round((f.position.z-485)/4))<.001f),"Existing four-unit tile lattice preserved");
        Check(floors.All(f=>f.parent==solar.transform && f.GetComponent<PlacedBuildObject>()),"All base tiles belong to Solar and can be saved/sold");
        Check(catalog.entries.Where(e=>e.prefab.name.StartsWith("Corner_Wall",StringComparison.OrdinalIgnoreCase)).All(e=>e.price==70),"All corner variants share price 70");
        Check(catalog.entries.Where(e=>e.prefab.name.StartsWith("Wall_door_hole",StringComparison.OrdinalIgnoreCase)).All(e=>e.price==200),"All doorway variants share price 200");

        // Isolated in-memory services: never read/write the user's save slots.
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var testCatalog=Object.Instantiate(catalog);
        testCatalog.developmentFreeConstruction=false;
        var plot=new GameObject("TestSolar").AddComponent<BuildPlot>();
        plot.propertyId=71;plot.price=100;plot.catalog=testCatalog;plot.buildable=true;
        plot.Volume.size=new Vector3(100,100,100);
        plot.transform.localScale=new Vector3(2,1.5f,2);
        BuildPlot.Active.Add(plot);
        var services=new GameObject("TestServices");
        var save=services.AddComponent<SaveService>();
        var money=services.AddComponent<EconomyService>();BindSave(money,save);
        var property=services.AddComponent<PropertyService>();BindSave(property,save);
        SetCurrent(save,new SaveData{slotId=1,money=1000});
        var wall=testCatalog.entries.First(e=>e.prefab.name=="Wall_complete");
        var window=testCatalog.entries.First(e=>e.IsOpening && e.prefab.name.StartsWith("Window") && !e.hiddenInMenu);
        BuildRecord Record(string id,BuildEntry e)=>new BuildRecord{id=id,definitionId=e.id,propertyId=71,sceneName=scene.name,position=new Vector3(8,3,8),rotation=Quaternion.identity,scale=Vector3.one,finish=-1};
        Check(!ConstructionEconomy.Build(testCatalog,Record("denied",wall),plot,money) && money.Money==1000,"Cannot build on unowned plot; no debit");
        Check(property.TryBuy(71,1) && money.Money==900,"Property purchase resolves actual value, not caller's cheap price");
        var built=ConstructionEconomy.Build(testCatalog,Record("runtime",wall),plot,money);
        Check(built && built.transform.parent==plot.transform && money.Money==850 && plot.TotalValue==150,"Build parents to Solar, debits 50 and adds value 50");
        var captured=built.Capture();built.Restore(captured,testCatalog);
        Check(Vector3.Distance(built.transform.lossyScale,Vector3.one)<.001f && Vector3.Distance(built.transform.position,captured.position)<.001f,"Save/restore world transform under scaled Solar");
        Check(ConstructionEconomy.Replace(testCatalog,built,window,money) && money.Money==800 && plot.TotalValue==200,"Window conversion charges only price difference and updates value");
        built=plot.GetComponentsInChildren<PlacedBuildObject>(true).Single();
        Check(built.persistentId=="runtime" && built.definitionId==window.id,"Conversion preserves persistent identity");
        Check(ConstructionEconomy.Sell(testCatalog,built,money) && money.Money==900 && plot.TotalValue==100,"Sale pays value once and removes building value");
        Check(!ConstructionEconomy.Sell(testCatalog,built,money) && money.Money==900,"Repeated sale cannot duplicate money");
        save.Current.money=0;
        Check(!ConstructionEconomy.Build(testCatalog,Record("poor",wall),plot,money) && plot.TotalValue==100,"Insufficient funds creates nothing");
        testCatalog.developmentFreeConstruction=true;
        var free=ConstructionEconomy.Build(testCatalog,Record("free",wall),plot,money);
        Check(free && money.Money==0 && plot.TotalValue==0 && wall.price==50,"Development toggle: free construction/value without losing real prices");
        Check(ConstructionEconomy.Sell(testCatalog,free,money) && money.Money==0,"Free mode sale pays zero");
        testCatalog.developmentFreeConstruction=false;save.Current.money=500;
        var kept=ConstructionEconomy.Build(testCatalog,Record("kept",wall),plot,money);
        var authored=ConstructionPersistence.Spawn(testCatalog,Record("authored",wall),false,scene);
        ConstructionEconomy.Sell(testCatalog,authored,money);
        var collector=services.AddComponent<WorldSaveService>();
        Check(collector.CaptureCurrentWorld(save),"World save captures construction and balance");
        string json=JsonUtility.ToJson(save.Current,true);
        File.WriteAllText("Documentation/ConstructionValueSaveSample.json",json);
        var data=JsonUtility.FromJson<SaveData>(json);
        Check(data.buildings.Length==4 && data.buildings.Count(r=>r.deleted)==3 && data.money==500,"Serialized save includes built objects, sold tombstones and balance together");
        foreach(var marker in plot.GetComponentsInChildren<PlacedBuildObject>(true))Object.DestroyImmediate(marker.gameObject);
        // The authored wall exists again when its scene is loaded; restore must hide it.
        authored=ConstructionPersistence.Spawn(testCatalog,Record("authored",wall),false,scene);
        var persistence=services.AddComponent<ConstructionPersistence>();persistence.catalog=testCatalog;
        persistence.RestoreScene(data,scene);
        var restored=plot.GetComponentsInChildren<PlacedBuildObject>(true);
        Check(restored.Length==1 && restored.All(p=>p.transform.parent==plot.transform) && BuildDeletionLedger.Deleted(plot).Count()==3,"Load restores live objects and separate deletion data under their Solar");
        Check(!authored && restored.Count(p=>!p.deleted)==1 && plot.TotalValue==150,"Sold authored objects are destroyed; loaded value matches remaining walls");
        persistence.RestoreScene(data,scene);
        Check(plot.GetComponentsInChildren<PlacedBuildObject>(true).Length==1,"Repeated restoration does not duplicate objects");
        Object.DestroyImmediate(testCatalog);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Debug.Log("CONSTRUCTION_VALUE_VALIDATION_OK checks="+results.Count);
    }
}
#endif
