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

public static class StairAlignmentValidation
{
    public static void Full(){Run();FloorFoundationValidation.RunConstruction();}
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string name){report.Add((ok?"PASS ":"FAIL ")+name);Debug.Log(report.Last());}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var entry=catalog.entries.Single(StairConstruction.IsStairs);
        var wall=catalog.entries.Single(e=>e.prefab.name=="Wall_complete");
        var plot=new GameObject("Stair alignment test").AddComponent<BuildPlot>();plot.propertyId=945;plot.catalog=catalog;plot.buildable=true;plot.Volume.size=Vector3.one*200;
        var rooms=plot.gameObject.AddComponent<RoomConstruction>();
        var floor=catalog.entries.Single(FloorEditing.IsFloor);
        var services=new GameObject("Test economy");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var property=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,property})service.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,save);
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{new SaveData{money=1000000,ownedProperties=new[]{945}}});
        foreach(float yaw in new[]{0f,90f,180f,270f})
        {
            var rotation=Quaternion.Euler(0,yaw,0);
            // Use the controller's real placement path, not a hand-authored zero pivot.
            var anchor=rotation*new Vector3(1,0,5);
            var position=BuildPlacement.Position(entry,anchor,rotation);
            Check(Vector3.Distance(position,anchor)<.001f,"Authored stair pivot is the placement origin, yaw="+yaw);
            var record=new BuildRecord{definitionId=entry.id,propertyId=945,position=position,rotation=rotation,scale=Vector3.one,sceneName=scene.name,finish=-1};
            StairConstruction.Passage(catalog,plot,record,out var opening,out float level);
            var actual=new Bounds(position+rotation*new Vector3(-8,0,-4),Vector3.zero);
            foreach(float x in new[]{-8f,4f})foreach(float z in new[]{-4f,8f})actual.Encapsulate(position+rotation*new Vector3(x,0,z));
            Check(Mathf.Abs(opening.xMin-actual.min.x)<.001f&&Mathf.Abs(opening.xMax-actual.max.x)<.001f&&Mathf.Abs(opening.yMin-actual.min.z)<.001f&&Mathf.Abs(opening.yMax-actual.max.z)<.001f,"Opening follows exact modular stair footprint even off tile grid, yaw="+yaw);
            var plan=StairConstruction.Plan(catalog,plot,record);
            Check(string.IsNullOrEmpty(plan.error),"Plan valid at non-4-unit origin, yaw="+yaw+": "+plan.error);
            var spans=plan.additions.Where(r=>catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()&&Mathf.Abs(r.position.y-level)<.06f).SelectMany(r=>WallGeometry.At(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>(),r.position,r.rotation,r.scale)).ToArray();
            Check(spans.Length>0&&spans.All(s=>new[]{s.a,s.b}.All(p=>spans.Count(t=>Vector3.Distance(p,t.a)<.001f||Vector3.Distance(p,t.b)<.001f)==2)),"All upper wall endpoints form a gap-free undirected loop, yaw="+yaw);
            var inverse=Matrix4x4.TRS(position,rotation,Vector3.one).inverse;
            Check(spans.SelectMany(s=>new[]{inverse.MultiplyPoint3x4(s.a),inverse.MultiplyPoint3x4(s.b)}).All(p=>p.x>=-8.001f&&p.x<=4.001f&&p.z>=-4.001f&&p.z<=12.001f),"No rear/side void between stair footprint and upper walls, yaw="+yaw);
            Check(Mathf.Abs(level-7)<.001f&&spans.All(s=>Mathf.Abs(s.a.y-7)<.001f),"Upper walls start at the lower wall's top, yaw="+yaw);
            var rear=new List<PlacedBuildObject>();
            for(int i=0;i<3;i++)rear.Add(ConstructionPersistence.Spawn(catalog,new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=wall.id,propertyId=945,position=position+rotation*new Vector3(-8+i*4,0,-4),rotation=rotation,scale=Vector3.one,sceneName=scene.name,finish=-1},true,scene));
            Physics.SyncTransforms();
            Check(BuildPlacement.IsValid(entry,plot,position,rotation,position),"Rear landing can touch actual lower wall without a forced gap, yaw="+yaw);
            var intruder=GameObject.CreatePrimitive(PrimitiveType.Cube);intruder.transform.SetPositionAndRotation(position+rotation*new Vector3(-2,3.5f,-3.8f),rotation);Physics.SyncTransforms();
            Check(!BuildPlacement.IsValid(entry,plot,position,rotation,position),"Rear furniture is not mistaken for the permitted wall seam, yaw="+yaw);Object.DestroyImmediate(intruder);
            Check(Vector3.Distance(StairConstruction.SnapToStorey(plot,position+Vector3.up*.02f),position)<.001f,"Slab thickness does not offset the stair base, yaw="+yaw);
            var polygon=new List<Vector2>{new(-24,-24),new(24,-24),new(24,24),new(-24,24)};
            foreach(var slab in rooms.PlanTiles(new List<List<Vector2>>{polygon},7+RoomFloorPatch.Thickness,floor))ConstructionPersistence.Spawn(catalog,slab,true,scene);
            Physics.SyncTransforms();plan=StairConstruction.Plan(catalog,plot,record);
            Check(string.IsNullOrEmpty(plan.error),"Upper walls fit over an actual generated ceiling, yaw="+yaw+": "+plan.error);
            if(string.IsNullOrEmpty(plan.error))
            {
                int before=money.Money;var built=StairConstruction.Build(catalog,plot,record,money);Physics.SyncTransforms();
                Check(built&&money.Money==before-plan.Cost(catalog),"Single transaction builds aligned stairs and cuts existing ceiling, yaw="+yaw);
                Check(!Physics.Raycast(new Vector3(opening.center.x,level+1,opening.center.y),Vector3.down,.8f),"No original ceiling crosses the central passage, yaw="+yaw);
                StairConstruction.Landing(catalog,plot,record,out var landing,out _);
                Check(Physics.Raycast(new Vector3(landing.center.x,level+1,landing.center.y),Vector3.down,out var support,1.1f)&&Mathf.Abs(support.point.y-level)<.04f,"Upper exit is supported at the correct height, yaw="+yaw);
                int balance=money.Money;rooms.Rebuild(true,money);
                Check(money.Money==balance,"Room rebuild does not create duplicate overlapping floors or extra charges, yaw="+yaw);
                Check(!StairConstruction.Validate(StairConstruction.Plan(catalog,plot,record),catalog),"A live staircase cannot be built twice, yaw="+yaw);
                ConstructionEconomy.Sell(catalog,built,money);Physics.SyncTransforms();
                var replan=StairConstruction.Plan(catalog,plot,record);
                Check(string.IsNullOrEmpty(replan.error)&&replan.additions.Count==0,"Corrected enclosure is reused without extra geometry, yaw="+yaw+": "+replan.error);
            }
            foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())Object.DestroyImmediate(item.gameObject);
            var ledger=plot.GetComponent<BuildDeletionLedger>();if(ledger)Object.DestroyImmediate(ledger);
            rooms.Rebuild();Physics.SyncTransforms();
        }
        Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        File.WriteAllLines("Documentation/StairAlignmentValidation.txt",report);
        if(report.Any(s=>s.StartsWith("FAIL")))throw new Exception("Stair alignment failures: "+report.Count(s=>s.StartsWith("FAIL")));
        Debug.Log("STAIR_ALIGNMENT_VALIDATION_OK checks="+report.Count);
    }
}
#endif
