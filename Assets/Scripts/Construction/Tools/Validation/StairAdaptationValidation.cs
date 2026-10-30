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
public static class StairAdaptationValidation
{
    public static void Full(){Run();StairStoreyValidation.Full();}
    public static void Run()
    {
        var report=new List<string>();void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);Debug.Log(report.Last());}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var plot=new GameObject("Stair adaptation fixture").AddComponent<BuildPlot>();plot.propertyId=953;plot.catalog=catalog;plot.buildable=true;plot.Volume.size=Vector3.one*200;
        var rooms=plot.gameObject.AddComponent<RoomConstruction>();var stairs=catalog.entries.Single(StairConstruction.IsStairs);
        var services=new GameObject("Fixture economy");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var property=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,property})service.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,save);
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{new SaveData{money=1000000,ownedProperties=new[]{953}}});
        BuildRecord Record(Vector3 p,float angle)=>new(){id=Guid.NewGuid().ToString("N"),definitionId=stairs.id,propertyId=953,position=p,rotation=Quaternion.Euler(0,angle,0),scale=Vector3.one,finish=-1};
        foreach(var next in new[]{Record(Vector3.zero,90),Record(Vector3.right*4,0),Record(Vector3.right,0),Record(Vector3.right*12,0)})
        {
            var initial=Record(Vector3.zero,0);var plan=StairConstruction.Plan(catalog,plot,initial);
            Check(string.IsNullOrEmpty(plan.error),"Initial plan: "+plan.error);
            var built=StairConstruction.Build(catalog,plot,initial,money);Check(built,"Initial staircase builds");Physics.SyncTransforms();rooms.Rebuild();
            Check(plan.additions.Any(r=>catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()&&Mathf.Abs(r.position.y)<.06f),"Missing wall supports included below upper enclosure");
            var lower=plan.additions.Where(r=>catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()&&Mathf.Abs(r.position.y)<.06f)
                .SelectMany(r=>WallGeometry.At(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>(),r.position,r.rotation,r.scale)).ToArray();
            Check(!lower.Any(s=>s.Distance3D(new Vector3(2,0,12))<.06f||s.Distance3D(new Vector3(-6,0,12))<.06f),"Entry and exit corridors remain open");
            bool adjacent=next.position.x==12;
            if(!adjacent)Check(ConstructionEconomy.Sell(catalog,built,money),"Stair sold without removing enclosure");
            Physics.SyncTransforms();rooms.Rebuild();plan=StairConstruction.Plan(catalog,plot,next);
            bool valid=StairConstruction.Validate(plan,catalog);
            string details=plan.error+" blockers="+string.Join(",",plan.blockers.Where(c=>c).Select(c=>c.name+" "+c.GetComponentInParent<PlacedBuildObject>()?.definitionId+" "+c.bounds));
            Check(valid,"Reposition "+next.position+" rotation="+next.rotation.eulerAngles.y+": "+details);
            if(valid)
            {
                var green=new Material(Shader.Find("Universal Render Pipeline/Unlit"));var red=new Material(green);
                var renderers=plot.GetComponentsInChildren<MeshRenderer>();var original=renderers.Select(r=>r.sharedMaterials).ToArray();
                var snapshot=JsonUtility.ToJson(new SaveData{buildings=BuildDeletionLedger.CaptureScene(scene)});int originalBalance=money.Money;
                var preview=new StairChangePreview();preview.Show(plan,catalog,green,red);
                Check(preview.AdditionCount==plan.additions.Count,"Every added wall/floor appears in the green preview");
                Check(!Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Any(c=>c.name.StartsWith("Stair addition:")||c.name.StartsWith("Stair removal:")),"Plan preview adds no colliders");
                Check(renderers.Select((r,i)=>r.sharedMaterials.SequenceEqual(original[i])).All(v=>v),"Preview leaves retained surfaces and materials unchanged");
                preview.Hide();preview.Show(plan,catalog,green,red);preview.Dispose();
                Check(snapshot==JsonUtility.ToJson(new SaveData{buildings=BuildDeletionLedger.CaptureScene(scene)})&&money.Money==originalBalance,"Preview/cancel changes neither persistent objects nor money");
                Object.DestroyImmediate(green);Object.DestroyImmediate(red);
                save.Current.money=Mathf.Max(0,plan.Cost(catalog)-1);
                Check(!StairConstruction.Build(catalog,plot,next,money)&&snapshot==JsonUtility.ToJson(new SaveData{buildings=BuildDeletionLedger.CaptureScene(scene)}),"Insufficient funds do not partially sell or build the adaptation");
                save.Current.money=originalBalance;
                int balance=money.Money;var result=StairConstruction.Build(catalog,plot,next,money);
                Check(result&&money.Money==balance-plan.Cost(catalog),"Adaptation uses one quoted transaction");Physics.SyncTransforms();rooms.Rebuild();
                StairConstruction.Landing(catalog,plot,next,out var landing,out float y);
                Check(Physics.Raycast(new Vector3(landing.center.x,y+1,landing.center.y),Vector3.down,out var hit,1.1f)&&Mathf.Abs(hit.point.y-y)<.06f,"New exit has a usable landing after adapting old construction");
                Check(rooms.rooms.Any(r=>Mathf.Abs(r.floor-y)<.06f&&r.shape.Contains(landing.center)&&r.ceilingComplete),"Adapted upper landing still belongs to a closed roofed room");
                var records=BuildDeletionLedger.CaptureScene(scene);
                var loaded=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData{buildings=records}));
                foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())Object.DestroyImmediate(item.gameObject);
                var previousLedger=plot.GetComponent<BuildDeletionLedger>();if(previousLedger)Object.DestroyImmediate(previousLedger);
                foreach(var r in loaded.buildings)
                    if(r.deleted)BuildDeletionLedger.For(plot).Remember(r);else ConstructionPersistence.Spawn(catalog,r,true,scene);
                rooms.Rebuild();
                Check(plot.GetComponentsInChildren<PlacedBuildObject>().Count(p=>StairConstruction.IsStairs(catalog.Find(p.definitionId)))==(adjacent?2:1)&&
                    rooms.rooms.Any(r=>Mathf.Abs(r.floor-y)<.06f&&r.shape.Contains(landing.center)&&r.ceilingComplete),"Save/load preserves adaptation, stair count and upper enclosure");
            }
            foreach(var p in plot.GetComponentsInChildren<PlacedBuildObject>())Object.DestroyImmediate(p.gameObject);
            var ledger=plot.GetComponent<BuildDeletionLedger>();if(ledger)Object.DestroyImmediate(ledger);rooms.Rebuild();Physics.SyncTransforms();
        }
        var wall=catalog.entries.Single(e=>e.prefab.name=="Wall_complete");
        var supports=new List<PlacedBuildObject>();
        foreach(float x in new[]{0f,4f,8f})supports.Add(ConstructionPersistence.Spawn(catalog,new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=wall.id,propertyId=953,position=new Vector3(x,0,0),scale=Vector3.one,finish=-1},true,scene));
        var upper=ConstructionPersistence.Spawn(catalog,new BuildRecord{id="top",definitionId=wall.id,propertyId=953,position=new Vector3(4,7,0),scale=Vector3.one,finish=-1},true,scene);
        WallTopology.RefreshScene(scene);Check(supports[1].GetComponent<WallTopology>().currentVariant==2,"Covered lower wall selects incomplete skin");
        ConstructionEconomy.Sell(catalog,upper,money);WallTopology.RefreshScene(scene);Physics.SyncTransforms();
        Check(supports[1].GetComponent<WallTopology>().currentVariant==1,"Selling upper wall restores lower wall top cap");
        Check(BuildPlacement.IsValid(wall,plot,new Vector3(4,7,0),Quaternion.identity,new Vector3(4,7,0)),"Wall can be placed again on the vacated upper seam");
        Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);
        File.WriteAllLines("Documentation/StairAdaptationValidation.txt",report);
        if(report.Any(r=>r.StartsWith("FAIL")))throw new Exception("Stair adaptation failed");
        Debug.Log("STAIR_ADAPTATION_VALIDATION_OK checks="+report.Count);
    }
}
#endif
