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

public static class CornerSectionsValidation
{
    const BindingFlags Fields=BindingFlags.NonPublic|BindingFlags.Instance;
    public static void Full(){Run();WallGridStrokeValidation.Full();}
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/CornerSectionsValidation.txt",report);if(!ok)throw new Exception(label);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        BuildEntry Entry(string name)=>catalog.entries.Single(e=>e.prefab.name.Equals(name,StringComparison.OrdinalIgnoreCase));
        var normal=Entry("Wall_complete");
        var plot=new GameObject("Corner priority fixture").AddComponent<BuildPlot>();plot.propertyId=969;plot.buildable=true;plot.catalog=catalog;
        plot.Volume.size=new Vector3(64,100,64);plot.transform.position=new Vector3(500,101,500);
        var services=new GameObject("Isolated services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var property=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,property})service.GetType().GetField("_save",Fields).SetValue(service,save);
        void Balance(int value)=>save.SetCurrent(new SaveData{money=value,ownedProperties=new[]{969}});
        Balance(100000);
        void Clear(){foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())Object.DestroyImmediate(item.gameObject);var ledger=plot.GetComponent<BuildDeletionLedger>();if(ledger)Object.DestroyImmediate(ledger);Physics.SyncTransforms();}
        List<WallGeometry.Span> Spans(BuildRecord r)=>WallGeometry.At(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>(),r.position,r.rotation,r.scale);
        BuildRecord Record(BuildEntry e,Vector3 p,Quaternion q,int finish=-1)=>new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=e.id,propertyId=plot.propertyId,position=p,rotation=q,scale=e.prefab.transform.localScale,cellCenter=p+q*e.prefab.GetComponent<WallTopology>().bottomCenter,sceneName=scene.name,finish=finish};
        PlacedBuildObject Spawn(BuildRecord r){var item=ConstructionPersistence.Spawn(catalog,r,true,scene);Physics.SyncTransforms();return item;}
        bool Coverage(IEnumerable<WallGeometry.Span> actual,IEnumerable<WallGeometry.Span> expected)
        {var a=actual.ToArray();var b=expected.ToArray();return a.Length==b.Length && b.All(s=>a.Count(t=>t.Same(s))==1);}

        // User's drawing: T at (4,0), a shared horizontal arm, and the new
        // perpendicular arm at (0,0). The left corner must own that shared arm.
        for(int turn=0;turn<4;turn++)foreach(bool batch in new[]{false,true})
        {
            Clear();var q=Quaternion.Euler(0,turn*90,0);var p=plot.transform.position;
            var tee=Record(Entry("T_wall_complete"),p,q);var existing=Spawn(tee);
            var add=StairAdaptation.Wall(normal,plot,p,p+q*Vector3.forward*4);
            var plan=batch?WallReconfiguration.Stroke(catalog,plot,new[]{add}):WallReconfiguration.Placement(catalog,plot,normal,add.position,add.rotation);
            Check(plan!=null && WallReconfiguration.Validate(plan,catalog,out _),"T end joins validly, rotation="+turn+" batch="+batch);
            var expected=Spans(tee).Concat(Spans(add)).ToArray();
            Check(Coverage(plan.added.SelectMany(Spans),expected),"All four spans preserved exactly once");
            var corners=plan.added.Where(r=>catalog.Find(r.definitionId).prefab.name=="Corner_Wall").ToArray();
            Check(corners.Any(r=>Spans(r).Any(s=>s.Same(Spans(add)[0])) && Spans(r).Any(s=>s.Same(Spans(tee)[0]))),"Outer corner owns shared T arm");
            int balance=money.Money,delta=plan.Difference(catalog);
            Check(WallReconfiguration.Apply(plan,catalog,money,out _),"Apply corner redistribution");
            Check(money.Money==balance-delta && BuildDeletionLedger.Deleted(plot).Any(r=>r.id==tee.id),"Price and old T deletion persist");
            Check(Coverage(plot.GetComponentsInChildren<WallTopology>().SelectMany(WallGeometry.World),expected),"Live geometry retains coverage");
        }

        Clear();
        var basePoint=plot.transform.position;
        Spawn(StairAdaptation.Wall(normal,plot,basePoint,basePoint+Vector3.right*4));
        Spawn(StairAdaptation.Wall(normal,plot,basePoint,basePoint+Vector3.forward*4));
        var explicitT=WallReconfiguration.Placement(catalog,plot,Entry("T_wall_complete"),basePoint,Quaternion.identity);
        Check(WallReconfiguration.Validate(explicitT,catalog,out _) && explicitT.added.Any(r=>catalog.Find(r.definitionId).prefab.name=="Corner_Wall"),"Explicit overlapping T also honours the existing outer turn");

        // Every branch is independently selectable, including painted T/+ and corners.
        foreach(string shape in new[]{"T_wall_complete","+_wall_complete","Corner_Wall"})for(int turn=0;turn<4;turn++)
        {
            var entry=Entry(shape);var q=Quaternion.Euler(0,turn*90,0);int arms=WallGeometry.Local(entry.prefab.GetComponent<WallTopology>()).Count;
            for(int arm=0;arm<arms;arm++)
            {
                Clear();var source=Record(entry,plot.transform.position,q,1);var item=Spawn(source);var original=WallGeometry.World(item.GetComponent<WallTopology>());
                var chosen=original[arm];var hit=(chosen.a+chosen.b)*.5f+Vector3.up*2;
                var plan=WallReconfiguration.Removal(catalog,plot,item,hit,out var red);
                Check(WallReconfiguration.Validate(plan,catalog,out _),"Single arm removal valid "+shape+" rotation="+turn+" arm="+arm);
                Check(Coverage(Spans(red),new[]{chosen}) && Coverage(plan.added.SelectMany(Spans),original.Where((s,i)=>i!=arm)),"Only selected arm removed; remainder exact");
                Check(plan.added.All(r=>r.finish==1),"Paint preserved");
                var renderer=item.GetComponentsInChildren<Renderer>().First(r=>r.enabled);var materials=renderer.sharedMaterials;
                var tint=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                var edit=new ConstructionEditing();
                var direction=Vector3.Cross(chosen.b-chosen.a,Vector3.up).normalized;
                var ray=new Ray(hit+direction*2,-direction);
                edit.Tick(ray,plot,catalog,null,tint,tint,false,null,money);
                var previews=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="WallChangePreview" || r.transform.root.name=="WallChangePreview").ToArray();
                Check(previews.Any(r=>r.sharedMaterials.Contains(tint)) && previews.Any(r=>r.sharedMaterials.Contains(catalog.finishes[1])),"Red selected section and untinted painted remainder previews");
                edit.Clear();Check(renderer.enabled && renderer.sharedMaterials.SequenceEqual(materials),"Cancel preview restores original visibility/materials");edit.Dispose();Object.DestroyImmediate(tint);
                int balance=money.Money,delta=plan.Difference(catalog);
                Check(WallReconfiguration.Apply(plan,catalog,money,out _) && money.Money==balance-delta,"Removal transaction and refund");
                Check(BuildDeletionLedger.Deleted(plot).Any(r=>r.id==source.id),"Sold joined source is tombstoned for save/load");
                Check(Coverage(plot.GetComponentsInChildren<WallTopology>().SelectMany(WallGeometry.World),original.Where((s,i)=>i!=arm)),"Live remainder after sale");
            }
        }

        // The same controller entry point is used for plain placement and a snapped
        // corner. Budget truncates the pending stroke, never the purchased world.
        Clear();Balance(120);
        var first=StairAdaptation.Wall(normal,plot,plot.transform.position+Vector3.left*4,plot.transform.position);Spawn(first);
        var begin=StairAdaptation.Wall(normal,plot,plot.transform.position,plot.transform.position+Vector3.forward*4);
        var controller=new GameObject("Corner drag fixture").AddComponent<ConstructionController>();controller.catalog=catalog;
        var type=controller.GetType();
        foreach(var pair in new[]{("selected",(object)normal),("plot",plot),("economy",money)})type.GetField(pair.Item1,Fields).SetValue(controller,pair.Item2);
        object Call(string name,params object[] args)=>type.GetMethod(name,Fields).Invoke(controller,args);
        var pending=(List<BuildRecord>)type.GetField("pendingWalls",Fields).GetValue(controller);
        var stroke=(WallBuildStroke)type.GetField("wallStroke",Fields).GetValue(controller);
        Check((bool)Call("BeginWallStroke",begin.position,begin.rotation,begin.cellCenter),"Drag can begin by forming a corner");
        var step=begin.rotation*(WallGeometry.Local(normal.prefab.GetComponent<WallTopology>())[0].b-WallGeometry.Local(normal.prefab.GetComponent<WallTopology>())[0].a);
        for(int i=1;i<=3;i++)
        {
            bool queued=(bool)Call("QueueAffordableWall",begin.position+step*i,begin.rotation,begin.cellCenter+step*i);
            if(queued)stroke.Commit(i);
            Check(queued==(i<3),"Budget accepts only affordable extension "+i);
        }
        Check(pending.Count==3 && money.Money==120 && plot.GetComponentsInChildren<PlacedBuildObject>().Length==1,"No charge or world mutation until release");
        Call("TrimWallStroke",begin.position+step);Check(pending.Count==2,"Corner-started stroke retracts");
        Check(ConstructionEconomy.BuildWalls(catalog,pending,plot,money) && money.Money==50,"Release builds corner plus retained span for net $70");
        Call("CancelWallStroke");Object.DestroyImmediate(controller.gameObject);
        Clear();Balance(1000);first.finish=1;Spawn(first);
        begin.finish=1;
        var painted=WallReconfiguration.Stroke(catalog,plot,new[]{begin});
        Check(WallReconfiguration.Validate(painted,catalog,out _) && painted.added.All(r=>r.finish==1),"Corner stroke also preserves the existing painted finish");
        Check(WallReconfiguration.Apply(painted,catalog,money,out _),"Painted corner stroke applies atomically");
        Clear();Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);
        Debug.Log("CORNER_SECTIONS_VALIDATION_OK checks="+report.Count);
    }
}
#endif
