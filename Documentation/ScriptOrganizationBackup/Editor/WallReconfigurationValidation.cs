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
using Object=UnityEngine.Object;

public static class WallReconfigurationValidation
{
    static BuildCatalog catalog;
    static BuildPlot plot;
    static SaveService save;
    static EconomyService money;
    static readonly List<string> results=new();
    static int serial;
    static void Check(bool value,string description)
    {
        results.Add((value?"PASS ":"FAIL ")+description);File.WriteAllLines("Documentation/WallReconfigurationValidation.txt",results);
        if(!value)throw new Exception(description);
        Debug.Log("WALL_GRAPH_TEST_PASS "+description);
    }
    static BuildEntry Entry(string name)=>catalog.entries.First(e=>e.prefab.name.Equals(name,StringComparison.OrdinalIgnoreCase));
    static PlacedBuildObject[] Items()=>plot.GetComponentsInChildren<PlacedBuildObject>().Where(p=>!p.deleted).ToArray();
    static void Reset()
    {
        if(catalog)Object.DestroyImmediate(catalog);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        plot=new GameObject("GraphTestSolar").AddComponent<BuildPlot>();plot.propertyId=881;plot.catalog=catalog;plot.buildable=true;plot.Volume.size=new Vector3(150,150,150);BuildPlot.Active.Add(plot);
        var services=new GameObject("Services");save=services.AddComponent<SaveService>();money=services.AddComponent<EconomyService>();
        var properties=services.AddComponent<PropertyService>();
        typeof(EconomyService).GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(money,save);
        typeof(PropertyService).GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(properties,save);
        SetData(new SaveData{slotId=1,money=10000,ownedProperties=new[]{881}});
        serial=0;
    }
    static void SetData(SaveData data)=>typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{data});
    static PlacedBuildObject Spawn(BuildEntry entry,Vector3 pos,Quaternion rot,string id=null)
    {
        var item=ConstructionPersistence.Spawn(catalog,new BuildRecord{id=id??"test-"+(serial++),definitionId=entry.id,propertyId=881,
            sceneName=plot.gameObject.scene.name,position=pos,rotation=rot,scale=entry.prefab.transform.localScale,finish=-1},false,plot.gameObject.scene);
        foreach(var floor in item.GetComponentsInChildren<BuildFloorCoverage>())BuildFloorCoverage.Active.Add(floor);
        Physics.SyncTransforms();return item;
    }
    static PlacedBuildObject Straight(WallGeometry.Span span)
    {
        var rot=Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.right,span.b-span.a,Vector3.up),Vector3.up);
        return Spawn(Entry("Wall_complete"),span.a,rot);
    }
    static bool SameCoverage(IEnumerable<WallGeometry.Span> a,IEnumerable<WallGeometry.Span> b)
    {
        var first=a.ToList();var second=b.ToList();return first.Count==second.Count&&first.All(s=>second.Any(t=>s.Same(t)));
    }
    public static void Run()
    {
        results.Clear();Reset();
        var original=Spawn(Entry("Wall_complete"),Vector3.zero,Quaternion.identity,"authored");
        var plan=WallReconfiguration.Placement(catalog,plot,Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);
        Check(plan!=null && plan.removed.Count==1 && plan.added.Count==1 && plan.Difference(catalog)==70,"Straight wall -> T, price difference 70");
        Check(WallReconfiguration.Apply(plan,catalog,money,out var reason),"Replace wall atomically: "+reason);
        Check(original.deleted && money.Money==9930 && Items().Length==1,"Original is a tombstone, not a duplicate; correct debit");
        var tee=Items().Single();var coverage=WallGeometry.World(tee.GetComponent<WallTopology>());
        plan=WallReconfiguration.Opening(catalog,plot,tee,Entry("Window_Wall"),new Vector3(4,2,2));
        Check(plan.error==null && plan.added.Count==2 && plan.Difference(catalog)==50,"T arm -> Corner + Window, price difference 50");
        Check(WallReconfiguration.Apply(plan,catalog,money,out reason),"First illustrated split applies: "+reason);
        Check(SameCoverage(coverage,Items().SelectMany(i=>WallGeometry.World(i.GetComponent<WallTopology>()))),"First split preserves exactly all original segments");
        var corner=Items().Single(i=>catalog.Find(i.definitionId).prefab.name.StartsWith("Corner"));
        plan=WallReconfiguration.Opening(catalog,plot,corner,Entry("Wall_door_hole"),new Vector3(2,2,0));
        Check(plan.error==null && plan.added.Count==2 && plan.Difference(catalog)==180,"Corner arm -> Straight + Doorway, price difference 180");
        Check(WallReconfiguration.Apply(plan,catalog,money,out reason),"Second illustrated split applies: "+reason);
        Check(Items().Length==3 && Items().Count(i=>catalog.Find(i.definitionId).IsOpening)==2 && money.Money==9700 && plot.TotalValue==351,"Final result: Straight + Window + Doorway; conserved value/balance");
        Check(SameCoverage(coverage,Items().SelectMany(i=>WallGeometry.World(i.GetComponent<WallTopology>()))),"Second split preserves geometry, no extra or lost arms");
        Check(Items().All(i=>i.transform.parent==plot.transform),"Every resulting piece is a child of Solar");
        var collector=money.gameObject.AddComponent<WorldSaveService>();collector.CaptureCurrentWorld(save);
        var snapshot=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Current));
        Check(snapshot.buildings.Count(r=>r.deleted)==3 && snapshot.buildings.Count(r=>!r.deleted)==3,"Save records three tombstones and three final pieces");
        File.WriteAllText("Documentation/WallReconfigurationSaveSample.json",JsonUtility.ToJson(snapshot,true));
        Reset();SetData(snapshot);original=Spawn(Entry("Wall_complete"),Vector3.zero,Quaternion.identity,"authored");
        var persistence=money.gameObject.AddComponent<ConstructionPersistence>();persistence.catalog=catalog;persistence.RestoreScene(snapshot,plot.gameObject.scene);
        Check(original.deleted && Items().Length==3 && plot.TotalValue==351 && money.Money==9700,"Reload does not resurrect original wall and restores building value/balance");
        persistence.RestoreScene(snapshot,plot.gameObject.scene);Check(Items().Length==3,"Repeated load creates no duplicates");

        foreach(string shape in new[]{"T_wall_complete","+_wall_complete","Corner_Wall","Wall_complete"})
            for(int turn=0;turn<4;turn++)
            {
                Reset();var entry=Entry(shape);var rotation=Quaternion.Euler(0,turn*90,0);
                var spans=WallGeometry.At(entry.prefab.GetComponent<WallTopology>(),Vector3.zero,rotation,Vector3.one);
                for(int arm=0;arm<spans.Count;arm++)
                {
                    var item=Spawn(entry,Vector3.zero,rotation);
                    var change=WallReconfiguration.Opening(catalog,plot,item,Entry("Window_Wall"),(spans[arm].a+spans[arm].b)*.5f+Vector3.up*2);
                    bool valid=WallReconfiguration.Validate(change,catalog,out reason);
                    Check(valid && SameCoverage(spans,change.added.SelectMany(r=>WallGeometry.At(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>(),r.position,r.rotation,r.scale))),shape+" rotation="+(turn*90)+" arm="+arm+" split is valid and exact: "+reason);
                    Object.DestroyImmediate(item.gameObject);Physics.SyncTransforms();
                }
            }

        Reset();var template=Entry("T_wall_complete");
        foreach(var span in WallGeometry.Local(template.prefab.GetComponent<WallTopology>()))Straight(span);
        plan=WallReconfiguration.Placement(catalog,plot,template,Vector3.zero,Quaternion.identity);
        Check(plan.removed.Count==3 && plan.added.Count==1 && plan.Difference(catalog)==-30 && WallReconfiguration.Apply(plan,catalog,money,out reason),"Three ordinary walls merge to T and refund 30");
        Check(money.Money==10030 && Items().Length==1,"Merge has no duplicated solids or duplicated charges");
        plan=WallReconfiguration.Placement(catalog,plot,template,Vector3.zero,Quaternion.identity);
        Check(plan!=null && !WallReconfiguration.Validate(plan,catalog,out _),"Identical T stays red");
        plan=WallReconfiguration.Placement(catalog,plot,Entry("Wall_complete"),Vector3.zero,Quaternion.identity);
        Check(plan!=null && !WallReconfiguration.Validate(plan,catalog,out _),"Ordinary wall over a T arm stays red");

        Reset();var host=Spawn(Entry("Wall_complete"),Vector3.zero,Quaternion.identity);
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=new Vector3(4,2,2);Physics.SyncTransforms();
        plan=WallReconfiguration.Placement(catalog,plot,Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);
        Check(!WallReconfiguration.Apply(plan,catalog,money,out _) && !host.deleted && money.Money==10000 && Items().Length==1,"Obstacle rejects replacement without modifying old wall or money");
        Object.DestroyImmediate(obstacle);Physics.SyncTransforms();save.Current.money=0;
        Check(!WallReconfiguration.Apply(plan,catalog,money,out _) && !host.deleted,"Insufficient funds leaves originals intact");
        catalog.developmentFreeConstruction=true;
        Check(WallReconfiguration.Apply(plan,catalog,money,out reason) && money.Money==0 && plot.TotalValue==0,"Free construction also applies to wall restructuring");

        Reset();host=Spawn(Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);host.finish=1;
        plan=WallReconfiguration.Opening(catalog,plot,host,Entry("Window_Wall"),new Vector3(4,2,2));
        Check(plan.added.All(r=>r.finish==1),"All split pieces preserve painted finish");
        Check(WallReconfiguration.Apply(plan,catalog,money,out reason),"Painted split applies: "+reason);
        var lower=Items();
        foreach(var item in lower)
            foreach(var end in item.GetComponent<WallTopology>().connections)
            {
                var point=item.transform.TransformPoint(end);
                if(Items().Any(other=>other!=item && WallGeometry.Contacts(other.GetComponent<WallTopology>()).Any(p=>WallGeometry.Near(point,other.transform.TransformPoint(p)))))continue;
                var span=WallGeometry.World(item.GetComponent<WallTopology>()).First(s=>WallGeometry.Near(s.a,point)||WallGeometry.Near(s.b,point));
                var inside=WallGeometry.Near(span.a,point)?span.b:span.a;
                Straight(new WallGeometry.Span(point,point+(point-inside).normalized*4));
            }
        WallTopology.RefreshScene(plot.gameObject.scene);
        Check(lower.All(i=>i.GetComponent<WallTopology>().currentVariant==1),"Split pieces use semi variant when all outer ends are covered, including internal corner joints");
        Spawn(Entry("T_wall_complete"),Vector3.up*7,Quaternion.identity);WallTopology.RefreshScene(plot.gameObject.scene);
        Check(lower.All(i=>i.GetComponent<WallTopology>().currentVariant==2),"One T above covers a split Corner + Window: incomplete variants");
        Reset();host=Spawn(Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);
        Check(host.GetComponent<WallTopology>().currentVariant==0,"Unconnected compound stays complete");
        var upper=Spawn(Entry("T_wall_complete"),Vector3.up*7,Quaternion.identity);
        plan=WallReconfiguration.Opening(catalog,plot,upper,Entry("Window_Wall"),new Vector3(4,9,2));
        Check(WallReconfiguration.Apply(plan,catalog,money,out reason) && WallGeometry.CoveredAbove(host.GetComponent<WallTopology>(),Items().Select(i=>i.GetComponent<WallTopology>())) ,"Split upper floor still covers all segments of lower T");
        Check(!host.deleted && Items().Any(i=>i==host),"Editing upstairs never replaces the lower storey");
        foreach(string compound in new[]{"Corner_Wall","T_wall_complete","+_wall_complete"})
            for(int turn=0;turn<4;turn++)
            {
                Reset();var rotation=Quaternion.Euler(0,turn*90,0);var origin=new Vector3(10,0,10);
                host=Spawn(Entry("Wall_complete"),origin,rotation);
                var requested=origin+rotation*new Vector3(2,0,0);
                var snapped=WallReconfiguration.SnapReplacement(catalog,Entry(compound),host,requested,rotation,requested+Vector3.up*2);
                plan=WallReconfiguration.Placement(catalog,plot,Entry(compound),snapped,rotation);
                Check(plan!=null && WallReconfiguration.Apply(plan,catalog,money,out _) && Items().Length==1,compound+" snaps and replaces a pointed wall at rotation "+(turn*90));
            }
        Reset();var window=Spawn(Entry("Window_Wall"),Vector3.zero,Quaternion.identity);
        plan=WallReconfiguration.Placement(catalog,plot,Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);
        Check(plan!=null && WallReconfiguration.Apply(plan,catalog,money,out reason) && window.deleted && Items().Length==1 && Items().All(i=>!catalog.Find(i.definitionId).IsOpening),
            "Explicit compound substitution replaces an existing window with the selected solid type: "+reason);
        Reset();host=Spawn(Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);
        var tool=new ConstructionEditing();var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        var text=tool.Tick(new Ray(new Vector3(8,2,2),Vector3.left),plot,catalog,Entry("Window_Wall"),material,material,false,null,money);
        Check(text.StartsWith(JuegoCriminal.Localization.GameLanguage.T("Click: replace section","Clic: sustituir tramo")) && !host.GetComponentInChildren<MeshRenderer>().enabled,"Real aimed editing previews the chosen arm and temporarily hides old geometry");
        tool.Clear();Check(host.GetComponentInChildren<MeshRenderer>().enabled,"Clearing preview restores original renderer");
        tool.Tick(new Ray(new Vector3(8,2,2),Vector3.left),plot,catalog,Entry("Window_Wall"),material,material,true,null,money);
        Check(host.deleted && Items().Length==2,"Real click converts aimed T arm through ConstructionEditing");
        tool.Dispose();Object.DestroyImmediate(material);
        Reset();host=Spawn(Entry("Wall_complete"),Vector3.zero,Quaternion.identity);
        plan=WallReconfiguration.Placement(catalog,plot,Entry("T_wall_complete"),new Vector3(.5f,0,0),Quaternion.identity);
        Check(plan==null && !BuildPlacement.IsValid(Entry("T_wall_complete"),plot,new Vector3(.5f,0,0),Quaternion.identity,Vector3.zero) && !host.deleted,
            "Partial misalignment does not erase a wall or allow overlap");
        Reset();host=Spawn(Entry("Wall_complete"),Vector3.zero,Quaternion.identity);
        var other=Spawn(Entry("Wall_complete"),new Vector3(4,0,0),Quaternion.Euler(0,90,0));host.finish=1;other.finish=2;
        plan=WallReconfiguration.Placement(catalog,plot,Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);
        Check(plan!=null && !WallReconfiguration.Apply(plan,catalog,money,out _) && !host.deleted && !other.deleted,
            "Incompatible painted finishes are not silently discarded during a merge");
        host.finish=-1;other.finish=0;
        plan=WallReconfiguration.Placement(catalog,plot,Entry("T_wall_complete"),Vector3.zero,Quaternion.identity);
        Check(plan!=null && WallReconfiguration.Apply(plan,catalog,money,out _),"Authored default finish and old runtime base finish can merge");
        Reset();host=Spawn(Entry("Diagonal_Wall"),Vector3.zero,Quaternion.identity);
        plan=WallReconfiguration.Opening(catalog,plot,host,Entry("Window_Wall"),new Vector3(2,2,2));
        Check(!WallReconfiguration.Validate(plan,catalog,out _) && !host.deleted,"Incompatible diagonal opening is safely refused instead of changing wall length");
        if(catalog)Object.DestroyImmediate(catalog);catalog=null;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Debug.Log("WALL_RECONFIGURATION_VALIDATION_OK checks="+results.Count);
    }
}
