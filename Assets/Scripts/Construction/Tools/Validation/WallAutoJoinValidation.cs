#if UNITY_EDITOR
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

public static class WallAutoJoinValidation
{
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string description){report.Add((ok?"PASS ":"FAIL ")+description);File.WriteAllLines("Documentation/WallAutoJoinValidation.txt",report);if(!ok)throw new Exception(description);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var plot=new GameObject("Autojoin test plot").AddComponent<BuildPlot>();plot.Volume.isTrigger=true;plot.Volume.size=Vector3.one*200;plot.propertyId=319;plot.buildable=true;plot.catalog=catalog;
        var services=new GameObject("Test economy");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var property=services.AddComponent<PropertyService>();
        typeof(EconomyService).GetField("_save",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(money,save);
        typeof(PropertyService).GetField("_save",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(property,save);
        save.SetCurrent(new SaveData{slotId=1,money=100000,ownedProperties=new[]{319}});
        BuildEntry Entry(string name)=>catalog.entries.First(e=>e.prefab.name.Equals(name,StringComparison.OrdinalIgnoreCase));
        var normal=Entry("Wall_complete");
        BuildRecord Record(BuildEntry entry,Vector3 position,Quaternion rotation)=>new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=entry.id,propertyId=319,sceneName=scene.name,position=position,rotation=rotation,scale=entry.prefab.transform.localScale,finish=-1};
        PlacedBuildObject Spawn(BuildEntry entry,Vector3 pos,Quaternion rot)=>ConstructionPersistence.Spawn(catalog,Record(entry,pos,rot),false,scene);
        BuildRecord Segment(WallGeometry.Span span)
        {var rotation=Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.right,span.b-span.a,Vector3.up),Vector3.up);return Record(normal,span.a,rotation);}
        PlacedBuildObject[] Items()=>plot.GetComponentsInChildren<PlacedBuildObject>().Where(p=>!p.deleted).ToArray();
        void Clear(){foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>(true))Object.DestroyImmediate(item.gameObject);Physics.SyncTransforms();}
        try
        {
            foreach(string shape in new[]{"Corner_Wall","T_wall_complete","+_wall_complete"})for(int turn=0;turn<4;turn++)
            {
                Clear();var target=Entry(shape);var expected=WallGeometry.At(target.prefab.GetComponent<WallTopology>(),new Vector3(20,0,20),Quaternion.Euler(0,turn*90,0),Vector3.one);
                // Begin with the first straight arm; place remaining arms using
                // exactly the same pre-placement planner as the gameplay code.
                var first=Segment(expected[0]);var original=Spawn(normal,first.position,first.rotation);
                for(int arm=1;arm<expected.Count;arm++)
                {
                    var next=Segment(expected[arm]);Physics.SyncTransforms();
                    var plan=WallReconfiguration.Placement(catalog,plot,normal,next.position,next.rotation);
                    if(plan!=null)
                    {
                        Check(WallReconfiguration.Validate(plan,catalog,out var reason),shape+" turn="+turn+" arm="+arm+" connection plan is physically valid: "+reason);
                        int cost=plan.Difference(catalog),before=money.Money;
                        Check(WallReconfiguration.Apply(plan,catalog,money,out reason) && money.Money==before-cost,shape+" connection applies atomically and charges its value difference");
                    }
                    else Spawn(normal,next.position,next.rotation);
                }
                var active=Items();
                Check(active.Length==1 && catalog.Find(active[0].definitionId).prefab.name==shape,shape+" turn="+turn+" consolidates ordinary wall endpoints into one correct prefab");
                var actual=WallGeometry.World(active[0].GetComponent<WallTopology>());
                Check(actual.Count==expected.Count && expected.All(a=>actual.Any(b=>a.Same(b))),shape+" turn="+turn+" keeps every arm at exactly the original position");
                var record=JsonUtility.FromJson<BuildRecord>(JsonUtility.ToJson(active[0].Capture()));active[0].Restore(record,catalog);
                Check(Items().Length==1 && catalog.Find(active[0].definitionId).prefab.name==shape,shape+" turn="+turn+" keeps the merged shape after save/restore");
            }
            Clear();var old=Spawn(normal,Vector3.zero,Quaternion.identity);Physics.SyncTransforms();
            Check(WallReconfiguration.Placement(catalog,plot,normal,new Vector3(4,0,0),Quaternion.identity)==null,"Collinear walls remain separate when no longer straight template exists");
            var duplicate=WallReconfiguration.Placement(catalog,plot,normal,Vector3.zero,Quaternion.identity);
            Check(duplicate!=null && !WallReconfiguration.Validate(duplicate,catalog,out _),"An exact duplicate remains red rather than being treated as an endpoint join");
            Check(WallReconfiguration.Placement(catalog,plot,normal,new Vector3(4,7,0),Quaternion.Euler(0,-90,0))==null,"Different storeys are never merged at endpoints");
            Clear();var window=Spawn(Entry("Window_Wall"),Vector3.zero,Quaternion.identity);Physics.SyncTransforms();
            Check(WallReconfiguration.Placement(catalog,plot,normal,new Vector3(4,0,0),Quaternion.Euler(0,-90,0))==null && !window.deleted,"Automatic solid joins do not remove or overwrite existing windows");
            Clear();old=Spawn(normal,Vector3.zero,Quaternion.identity);old.finish=1;
            var painted=WallReconfiguration.Placement(catalog,plot,normal,new Vector3(4,0,0),Quaternion.Euler(0,-90,0));
            Check(painted!=null && painted.added.All(r=>r.finish==1),"Joining a painted wall preserves its finish on the merged piece");
            Clear();
            plot.transform.position=new Vector3(474,101,507);plot.Volume.center=new Vector3(0,14,0);plot.Volume.size=new Vector3(44.5f,28,44.5f);
            var positions=new[]{new Vector3(452,101,501),new Vector3(496,101,513),new Vector3(468,101,485),new Vector3(480,101,529)};
            var rotations=new[]{Quaternion.Euler(0,-90,0),Quaternion.Euler(0,90,0),Quaternion.identity,Quaternion.Euler(0,180,0)};
            foreach(var entry in catalog.entries.Where(e=>e.prefab.name.StartsWith("Glass_Wall_door",StringComparison.OrdinalIgnoreCase)))
            {
                Check(Mathf.Abs(entry.bounds.size.z-.5f)<.0001f,entry.title+" whole visible prefab now has normal wall thickness, not 0.6");
                for(int edge=0;edge<4;edge++)
                {
                    Clear();var position=positions[edge];var rotation=rotations[edge];var anchor=position+rotation*entry.prefab.GetComponent<WallTopology>().bottomCenter;
                    var expanded=new BuildEntry{prefab=entry.prefab,category=entry.category,bounds=new Bounds(entry.bounds.center,new Vector3(entry.bounds.size.x,entry.bounds.size.y,.6f))};
                    Check(!BuildPlacement.IsValid(expanded,plot,position,rotation,anchor),entry.title+" edge="+edge+" reproduces the previous red-border rejection at 0.6 thickness");
                    Check(BuildPlacement.IsValid(entry,plot,position,rotation,anchor),entry.title+" edge="+edge+" now fits the original plot boundary without expanding the plot");
                    var span=WallGeometry.At(entry.prefab.GetComponent<WallTopology>(),position,rotation,Vector3.one).Single();
                    var neighbour=Spawn(normal,span.a-rotation*Vector3.right*4,rotation);Physics.SyncTransforms();
                    Check(BuildPlacement.IsValid(entry,plot,position,rotation,anchor),entry.title+" edge="+edge+" fits next to an existing endpoint wall");
                    var outward=rotation*(edge<2?Vector3.forward:Vector3.back)*.1f;
                    Check(!BuildPlacement.IsValid(entry,plot,position+outward,rotation,anchor+outward),entry.title+" edge="+edge+" still rejects genuine construction outside the plot");
                }
            }
            Clear();var glass=Entry("Glass_Wall_door_complete");Spawn(glass,positions[0],rotations[0]);Physics.SyncTransforms();
            Check(!BuildPlacement.IsValid(glass,plot,positions[0],rotations[0],positions[0]),"Glass-door duplicate geometry remains blocked");
        }
        finally{Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        Debug.Log("WALL_AUTO_JOIN_VALIDATION_OK checks="+report.Count);
    }
    public static void Full(){Run();WallFamilyValidation.Full();Debug.Log("WALL_AUTO_JOIN_REGRESSIONS_OK");}
}
#endif
