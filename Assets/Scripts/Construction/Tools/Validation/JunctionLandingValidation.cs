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

public static class JunctionLandingValidation
{
    public static void Full(){Run();StairOccupancyValidation.Full();}
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);Debug.Log(report.Last());File.WriteAllLines("Documentation/JunctionLandingValidation.txt",report);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        BuildEntry Entry(string name)=>catalog.entries.Single(e=>e.prefab.name.Equals(name,StringComparison.OrdinalIgnoreCase));
        var stairs=Entry("Stairs");var normal=Entry("Wall_complete");
        var plot=new GameObject("Junction landing tests").AddComponent<BuildPlot>();plot.propertyId=959;plot.catalog=catalog;plot.buildable=true;plot.Volume.size=Vector3.one*200;
        var rooms=plot.gameObject.AddComponent<RoomConstruction>();
        var services=new GameObject("Fixture services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();
        typeof(EconomyService).GetField("_save",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(money,save);
        save.SetCurrent(new SaveData{money=1000000,ownedProperties=new[]{959}});
        BuildRecord Record(BuildEntry e,Vector3 p,Quaternion q)=>new(){id=Guid.NewGuid().ToString("N"),definitionId=e.id,propertyId=959,position=p,rotation=q,scale=Vector3.one,finish=-1};
        void Clear(){foreach(var p in plot.GetComponentsInChildren<PlacedBuildObject>())Object.DestroyImmediate(p.gameObject);var ledger=plot.GetComponent<BuildDeletionLedger>();if(ledger)Object.DestroyImmediate(ledger);rooms.Rebuild();Physics.SyncTransforms();}
        foreach(string name in new[]{"Corner_Wall","T_wall_complete","+_wall_complete"})for(int turn=0;turn<4;turn++)
        {
            Clear();var target=Entry(name);var basis=Quaternion.Euler(0,turn*90,0);
            var spans=WallGeometry.At(target.prefab.GetComponent<WallTopology>(),new Vector3(10,0,10),basis,Vector3.one);
            foreach(var s in spans.Take(spans.Count-1))ConstructionPersistence.Spawn(catalog,StairAdaptation.Wall(normal,plot,s.a,s.b),true,scene);
            Physics.SyncTransforms();var requested=StairAdaptation.Wall(normal,plot,spans.Last().a,spans.Last().b);
            var rough=requested.position+basis*new Vector3(.4f,0,.3f);
            var snapped=BuildPlacement.SnapWall(normal,plot,rough,requested.rotation);
            var plan=WallReconfiguration.Placement(catalog,plot,normal,snapped,requested.rotation);
            Check(Vector3.Distance(snapped,requested.position)<.001f&&plan!=null&&WallReconfiguration.Validate(plan,catalog,out _),"Magnetic join resolves "+name+" yaw="+turn*90);
            Check(plan!=null&&plan.added.Any(r=>catalog.Find(r.definitionId).prefab.name.Equals(name,StringComparison.OrdinalIgnoreCase)),"Snapped plan uses the junction prefab: "+name);
            var fill=BuildPlacementGuides.WallShape(spans,plot.Grid,out var contour);
            Check(fill.Count>0&&contour.Count>0&&contour.All(s=>
            {
                var d=s.b-s.a;var n=new Vector2(-d.y,d.x).normalized*.01f;var mid=(s.a+s.b)*.5f;
                return fill.Any(p=>RoomGeometry.Inside(p,mid+n))!=fill.Any(p=>RoomGeometry.Inside(p,mid-n));
            }),"Guide contour contains no internal joint caps: "+name+" yaw="+turn*90);
            bool overlap=false;
            for(int i=0;i<fill.Count;i++)for(int j=0;j<i;j++)
            {
                float original=Mathf.Abs(RoomGeometry.SignedArea(fill[i]));
                float remainder=RoomGeometry.Subtract(new List<List<Vector2>>{fill[i]},fill[j]).Sum(p=>Mathf.Abs(RoomGeometry.SignedArea(p)));
                if(original-remainder>.0001f)overlap=true;
            }
            Check(!overlap,"Guide fill has uniform alpha at "+name+" joins");
        }
        Clear();
        for(int turn=0;turn<4;turn++)
        {
            Clear();plot.Volume.size=new Vector3(44.5f,100,44.5f);
            var corner=Entry("Corner_Wall");var q=Quaternion.Euler(0,turn*90,0);var b=corner.bounds;
            var bounds=new Bounds(q*b.center,Vector3.zero);
            foreach(float x in new[]{b.min.x,b.max.x})foreach(float z in new[]{b.min.z,b.max.z})bounds.Encapsulate(q*new Vector3(x,b.center.y,z));
            var origin=new Vector3(-22.2499f-bounds.min.x,0,-22.2499f-bounds.min.z);
            var spans=WallGeometry.At(corner.prefab.GetComponent<WallTopology>(),origin,q,Vector3.one);
            ConstructionPersistence.Spawn(catalog,StairAdaptation.Wall(normal,plot,spans[0].a,spans[0].b),true,scene);Physics.SyncTransforms();
            var record=StairAdaptation.Wall(normal,plot,spans[1].a,spans[1].b);
            var rough=BuildPlacement.ClampToPlot(normal,plot,plot.SnapOrigin(record.position),record.rotation);
            var snapped=BuildPlacement.ClampToPlot(normal,plot,BuildPlacement.SnapWall(normal,plot,rough,record.rotation),record.rotation);
            var plan=WallReconfiguration.Placement(catalog,plot,normal,snapped,record.rotation);
            Check(plan!=null&&WallReconfiguration.Validate(plan,catalog,out _)&&plan.added.Any(r=>catalog.Find(r.definitionId).prefab.name==corner.prefab.name),"Controller snap/clamp forms a fitting corner at plot boundary, yaw="+turn*90);
        }
        Clear();plot.Volume.size=Vector3.one*200;
        using(var guides=new BuildPlacementGuides())foreach(var name in new[]{"Central_display_unit","Bed","Table","Chair"})for(int turn=0;turn<4;turn++)
        {
            var e=Entry(name);var q=Quaternion.Euler(0,turn*90,0);
            guides.Show(e,catalog,plot,Vector3.zero,q,Vector3.zero,true,null,1,24);
            Check(guides.AccessArrowCount==(name=="Chair"?1:2),"Configured access arrows: "+name+" yaw="+turn*90);
        }
        using(var guides=new BuildPlacementGuides())
        {
            guides.Show(Entry("Corner_Wall"),catalog,plot,Vector3.zero,Quaternion.identity,Vector3.zero,true,null,1,24);
            ConstructionGuidesValidation.Capture("Documentation/CornerGuidePreview.png");
            guides.Show(Entry("Table"),catalog,plot,Vector3.zero,Quaternion.identity,Vector3.zero,true,null,1,24);
            ConstructionGuidesValidation.Capture("Documentation/TwoSidedGuidePreview.png");
        }
        for(int turn=0;turn<4;turn++)foreach(float gap in new[]{4f,8f})
        {
            Clear();var basis=Quaternion.Euler(0,turn*90,0);
            var first=Record(stairs,Vector3.zero,basis);var built=StairConstruction.Build(catalog,plot,first,money);Check(built,"First staircase builds yaw="+turn*90);Physics.SyncTransforms();rooms.Rebuild();
            foreach(float shortGap in new[]{0f,1f,3f})
            {
                var blocked=Record(stairs,basis*new Vector3(-4,0,16+shortGap),basis*Quaternion.Euler(0,180,0));
                var plan=StairConstruction.Plan(catalog,plot,blocked);var balance=money.Money;
                Check(!StairConstruction.Validate(plan,catalog)&&!StairConstruction.Build(catalog,plot,blocked,money)&&money.Money==balance,"Facing stairs need full cell, gap="+shortGap+" yaw="+turn*90);
            }
            var second=Record(stairs,basis*new Vector3(-4,0,16+gap),basis*Quaternion.Euler(0,180,0));
            var good=StairConstruction.Plan(catalog,plot,second);
            Check(StairConstruction.Validate(good,catalog),"Facing stairs share a valid hall, gap="+gap+" yaw="+turn*90+" error="+good.error+" blockers="+string.Join(",",good.blockers.Where(c=>c).Select(c=>c.name+" "+c.bounds+" "+c.GetComponentInParent<PlacedBuildObject>()?.definitionId)));
            var result=StairConstruction.Build(catalog,plot,second,money);Check(result,"Facing pair builds transactionally");Physics.SyncTransforms();rooms.Rebuild();
            if(result)
            {
                bool supported=true;
                foreach(float x in new[]{-6f,-2f,2f})for(float z=8.5f;z<8+gap;z+=1)
                {
                    var p=basis*new Vector3(x,7.5f,z);
                    if(!Physics.Raycast(p,Vector3.down,out var hit,1)||!hit.collider.GetComponentInParent<BuildFloorCoverage>())supported=false;
                }
                Check(supported,"Both upper exits meet a continuous shared floor");
                var spans=plot.GetComponentsInChildren<WallTopology>().SelectMany(WallGeometry.World).Select(s=>new WallGeometry.Span(Quaternion.Inverse(basis)*s.a,Quaternion.Inverse(basis)*s.b)).ToArray();
                Check(!spans.Any(s=>Mathf.Abs(s.a.y-7)<.06f&&s.Distance3D(new Vector3(-6,7,8))<.1f||Mathf.Abs(s.a.y-7)<.06f&&s.Distance3D(new Vector3(2,7,8+gap))<.1f),"Neither upper exit is sealed by a partition");
            }
        }
        Clear();Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);
        if(report.Any(r=>r.StartsWith("FAIL")))throw new Exception("Junction/landing regression");
        Debug.Log("JUNCTION_LANDING_VALIDATION_OK checks="+report.Count);
    }
}
#endif
