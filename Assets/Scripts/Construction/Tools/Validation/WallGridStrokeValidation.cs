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

public static class WallGridStrokeValidation
{
    public static void Full(){Run();JunctionLandingValidation.Full();ConstructionEditingUpgradeValidation.Run();}
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string name){report.Add((ok?"PASS ":"FAIL ")+name);Debug.Log(report.Last());File.WriteAllLines("Documentation/WallGridStrokeValidation.txt",report);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        BuildEntry Entry(string name)=>catalog.entries.Single(e=>e.prefab.name.Equals(name,StringComparison.OrdinalIgnoreCase));
        var plot=new GameObject("Wall lattice fixture").AddComponent<BuildPlot>();plot.propertyId=961;plot.catalog=catalog;plot.buildable=true;plot.fitFurnitureGridToPlot=true;
        plot.Volume.size=new Vector3(44.5f,100,44.5f);plot.transform.SetPositionAndRotation(new Vector3(474,101,507),Quaternion.Euler(0,90,0));
        plot.gameObject.AddComponent<RoomConstruction>();
        var services=new GameObject("Fixture services");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var property=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,property})service.GetType().GetField("_save",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(service,save);
        save.SetCurrent(new SaveData{money=1000000,ownedProperties=new[]{961}});
        bool OnGrid(Vector3 p){var v=plot.Grid.InverseTransformPoint(p);var offset=plot.FurnitureGridOffset;return Mathf.Abs((v.x-offset.x)/4-Mathf.Round((v.x-offset.x)/4))<.001f&&Mathf.Abs((v.z-offset.y)/4-Mathf.Round((v.z-offset.y)/4))<.001f;}
        void Clear(){foreach(var p in plot.GetComponentsInChildren<PlacedBuildObject>())Object.DestroyImmediate(p.gameObject);var ledger=plot.GetComponent<BuildDeletionLedger>();if(ledger)Object.DestroyImmediate(ledger);Physics.SyncTransforms();}
        foreach(string shape in new[]{"Wall_complete","Corner_Wall","T_wall_complete","+_wall_complete","Glass_Wall_door_complete"})for(int turn=0;turn<4;turn++)
        {
            var entry=Entry(shape);var rotation=plot.Grid.rotation*Quaternion.Euler(0,turn*90,0);
            foreach(var aim in new[]{new Vector3(1,0,3),new Vector3(-5,0,1),new Vector3(-22,0,-22),new Vector3(22,0,22)})
            {
                var rough=BuildPlacement.ClampToPlot(entry,plot,plot.SnapOrigin(plot.Grid.TransformPoint(aim)),rotation);
                var position=BuildPlacement.ResolveWallPosition(entry,plot,rough,rotation);
                var spans=WallGeometry.At(entry.prefab.GetComponent<WallTopology>(),position,rotation,entry.prefab.transform.localScale);
                Check(spans.All(s=>OnGrid(s.a)&&OnGrid(s.b))&&BuildPlacement.FitsPlot(entry,plot,position,rotation),"Visible lattice and plot limits: "+shape+" yaw="+turn*90+" aim="+aim);
            }
        }
        var normal=Entry("Wall_complete");
        // Exercise the controller's actual pending records and mesh previews, not
        // only the stroke index calculation. No real builds or money writes.
        foreach(string definition in new[]{"Wall_complete","Glass_wall"})for(int turn=0;turn<4;turn++)
        {
            var entry=Entry(definition);var rotation=plot.Grid.rotation*Quaternion.Euler(0,90*turn,0);
            var controller=new GameObject("Retractable stroke fixture").AddComponent<ConstructionController>();
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var type=typeof(ConstructionController);
            object Field(string name)=>type.GetField(name,flags).GetValue(controller);
            void Call(string name,params object[] args)=>type.GetMethod(name,flags).Invoke(controller,args);
            type.GetField("selected",flags).SetValue(controller,entry);type.GetField("plot",flags).SetValue(controller,plot);
            var stroke=(WallBuildStroke)Field("wallStroke");var pending=(List<BuildRecord>)Field("pendingWalls");
            var previews=(List<JuegoCriminal.Construction.MeshPreview>)Field("pendingPreviews");
            var origin=plot.Grid.TransformPoint(new Vector3(-6,0,-6));
            var span=WallGeometry.Local(entry.prefab.GetComponent<WallTopology>())[0];
            var step=rotation*Vector3.Scale(span.b-span.a,entry.prefab.transform.localScale);
            Check(stroke.Begin(entry,plot,origin,rotation),"Begin retractable stroke "+definition+" yaw="+turn*90);
            Call("QueueWall",origin,rotation,origin);
            void Aim(int target)
            {
                Call("TrimWallStroke",origin+step*target);
                while(stroke.Next(origin+step*target,out var p,out int index)){Call("QueueWall",p,rotation,p);stroke.Commit(index);}
            }
            int balance=money.Money;
            foreach(int target in new[]{2,1,0,2,0,-2,-1,0})
            {
                Aim(target);
                Check(pending.Count==Math.Abs(target)+1 && pending.Any(r=>r.position==origin),"Preview retracts/extends and retains first wall target="+target);
                Check(previews.Count==(WallGeometry.Solid(entry)?0:pending.Count),"Individual glass preview count follows pending records");
                Check(money.Money==balance && plot.GetComponentsInChildren<PlacedBuildObject>().Length==0,"Gesture does not build or charge");
            }
            Call("CancelWallStroke");Check(!stroke.Active&&pending.Count==0&&previews.Count==0,"Cancellation clears entire gesture");
            Object.DestroyImmediate(controller.gameObject);
        }
        foreach(string shape in new[]{"Corner_Wall","T_wall_complete","+_wall_complete"})for(int turn=0;turn<4;turn++)
        {
            Clear();var target=Entry(shape);var basis=plot.Grid.rotation*Quaternion.Euler(0,turn*90,0);
            var spans=WallGeometry.At(target.prefab.GetComponent<WallTopology>(),plot.Grid.TransformPoint(new Vector3(2,0,2)),basis,Vector3.one);
            var first=StairAdaptation.Wall(normal,plot,spans[0].a,spans[0].b);first.id=Guid.NewGuid().ToString("N");
            var existing=ConstructionPersistence.Spawn(catalog,first,true,scene);Physics.SyncTransforms();
            var batch=spans.Skip(1).Select(s=>StairAdaptation.Wall(normal,plot,s.a,s.b)).ToArray();
            var snapshot=JsonUtility.ToJson(new SaveData{buildings=BuildDeletionLedger.CaptureScene(scene)});int balance=money.Money;
            var plan=WallReconfiguration.Stroke(catalog,plot,batch);
            Check(WallReconfiguration.Validate(plan,catalog,out _)&&plan.added.Count==1&&catalog.Find(plan.added[0].definitionId).prefab.name==target.prefab.name,"Drag preview consolidates "+shape+" yaw="+turn*90);
            Check(snapshot==JsonUtility.ToJson(new SaveData{buildings=BuildDeletionLedger.CaptureScene(scene)})&&balance==money.Money,"Queued preview does not mutate or charge");
            Check(ConstructionEconomy.BuildWalls(catalog,batch,plot,money)&&money.Money==balance-plan.Difference(catalog),"Mouse release applies one consolidated transaction");
            var live=plot.GetComponentsInChildren<PlacedBuildObject>().Where(p=>!p.deleted).ToArray();
            Check(live.Length==1&&catalog.Find(live[0].definitionId).prefab.name==target.prefab.name,"No overlapping standalone planes remain: "+shape);
            Check(BuildDeletionLedger.Deleted(plot).Any(r=>r.id==first.id),"Old straight wall is persistently replaced");
        }
        Clear();
        var legacy=StairAdaptation.Wall(normal,plot,plot.Grid.TransformPoint(new Vector3(.3f,0,.3f)),plot.Grid.TransformPoint(new Vector3(4.3f,0,.3f)));
        ConstructionPersistence.Spawn(catalog,legacy,true,scene);Physics.SyncTransforms();
        var continuation=StairAdaptation.Wall(normal,plot,plot.Grid.TransformPoint(new Vector3(4.3f,0,.3f)),plot.Grid.TransformPoint(new Vector3(4.3f,0,4.3f)));
        var legacySnap=BuildPlacement.ResolveWallPosition(normal,plot,continuation.position+plot.Grid.right*.2f,continuation.rotation);
        Check(Vector3.Distance(legacySnap,continuation.position)<.001f,"An existing legacy seam takes priority without relocating the old wall");
        var joined=ConstructionEconomy.Build(catalog,continuation,plot,money);
        Check(joined&&plot.GetComponentsInChildren<PlacedBuildObject>().Count(p=>!p.deleted)==1,"Direct single-wall build uses the same junction transaction");
        Clear();Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);
        if(report.Any(r=>r.StartsWith("FAIL")))throw new Exception("Wall grid/stroke regression");
        Debug.Log("WALL_GRID_STROKE_VALIDATION_OK checks="+report.Count);
    }
}
#endif
