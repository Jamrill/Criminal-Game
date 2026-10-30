#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class StairOccupancyValidation
{
    public static void Baseline()=>Run(false);
    public static void Run()=>Run(true);
    public static void Full(){Run();ConstructionGuidesValidation.Run();StairAdaptationValidation.Full();}
    static void Run(bool enforce)
    {
        var report=new List<string>();
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);Debug.Log(report.Last());}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var stairs=catalog.entries.Single(StairConstruction.IsStairs);
        var plot=new GameObject("Stair occupancy test").AddComponent<BuildPlot>();plot.propertyId=957;plot.catalog=catalog;plot.Volume.size=Vector3.one*200;
        plot.gameObject.AddComponent<RoomConstruction>();
        BuildRecord Record(Vector3 p,float yaw)=>new(){id=Guid.NewGuid().ToString("N"),definitionId=stairs.id,propertyId=957,position=p,rotation=Quaternion.Euler(0,yaw,0),scale=Vector3.one,finish=-1};
        var first=ConstructionPersistence.Spawn(catalog,Record(Vector3.zero,0),true,scene);Physics.SyncTransforms();
        foreach(var pose in new[]{Record(Vector3.zero,0),Record(new Vector3(-4,0,4),180),Record(new Vector3(-4,0,12),180),Record(new Vector3(-4,0,15.8f),180),Record(new Vector3(7.8f,0,4),180),Record(new Vector3(11.8f,0,0),0),Record(new Vector3(0,0,4),90)})
        {
            var snapshot=JsonUtility.ToJson(new JuegoCriminal.Services.SaveData{buildings=BuildDeletionLedger.CaptureScene(scene)});
            var blockers=new List<Collider>();
            Check(!BuildPlacement.IsValid(stairs,plot,pose.position,pose.rotation,pose.position,blockers),"Reject intersecting stair modules: "+pose.position+" yaw="+pose.rotation.eulerAngles.y);
            var plan=StairConstruction.Plan(catalog,plot,pose);
            Check(!StairConstruction.Validate(plan,catalog),"Transaction rejects intersecting stair modules: "+pose.position);
            Check(snapshot==JsonUtility.ToJson(new JuegoCriminal.Services.SaveData{buildings=BuildDeletionLedger.CaptureScene(scene)}),"Rejected preview leaves existing stairs and floor intact");
        }
        Check(BuildPlacement.IsValid(stairs,plot,Vector3.right*12,Quaternion.identity,Vector3.right*12),"Side by side stairs remain legal at shared modular boundary");
        if(enforce)
        {
            Check(StairConstruction.SpaceAvailable(catalog,plot,Record(Vector3.up*7,0)),"Independent storeys do not overlap the reserved volume");
            Check(!StairConstruction.SpaceAvailable(catalog,plot,Record(Vector3.up*3.5f,180)),"Mid-storey overlap is rejected");
            var wall=catalog.entries.Single(e=>e.prefab.name=="Wall_complete");
            var rear=ConstructionPersistence.Spawn(catalog,new BuildRecord{id="rear",definitionId=wall.id,propertyId=957,position=new Vector3(-4,0,-4),rotation=Quaternion.identity,scale=Vector3.one,finish=-1},true,scene);
            Check(Vector3.Distance(StairConstruction.SnapBoundary(stairs,plot,new Vector3(0,0,.2f),Quaternion.identity),Vector3.zero)<.001f,"Small rear offset snaps modular stair seam to existing wall without recentering mesh");
            Object.DestroyImmediate(rear.gameObject);
            plot.Volume.size=new Vector3(200,200,100);plot.Volume.center=new Vector3(0,0,-42);
            Check(Vector3.Distance(BuildPlacement.ClampToPlot(stairs,plot,Vector3.zero,Quaternion.identity),Vector3.zero)<.001f,"Plot clamp uses the 8-unit modular front, not the decorative tread overhang");
            plot.Volume.size=Vector3.one*200;plot.Volume.center=Vector3.zero;
            for(int turn=0;turn<4;turn++)
            {
                var rotation=Quaternion.Euler(0,turn*90,0);first.transform.rotation=rotation;Physics.SyncTransforms();
                var facing=Record(rotation*new Vector3(-4,0,15.8f),turn*90+180);
                var plan=StairConstruction.Plan(catalog,plot,facing);
                Check(!StairConstruction.Validate(plan,catalog)&&plan.blockers.Any(c=>c&&c.transform.IsChildOf(first.transform)),"Facing partial overlap rejected and existing staircase highlighted at yaw="+turn*90);
            }
        }
        Object.DestroyImmediate(first.gameObject);
        // Inspect authored model directions without changing meshes/pivots.
        foreach(var e in catalog.entries.Where(e=>e.prefab&&(e.prefab.name.IndexOf("shelv",StringComparison.OrdinalIgnoreCase)>=0||e.prefab.name=="Chair"||e.prefab.name=="Sofa")))
        {
            report.Add("MODEL "+e.prefab.name+" bounds="+e.bounds);
            foreach(var mf in e.prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                report.Add("MESH "+mf.name+" local="+mf.transform.localPosition+" bounds="+mf.sharedMesh.bounds);
                var verts=mf.sharedMesh.vertices;var high=verts.Where(v=>v.y>mf.sharedMesh.bounds.center.y+mf.sharedMesh.bounds.extents.y*.5f).ToArray();
                if(high.Length>0)report.Add("HIGH centre="+high.Aggregate(Vector3.zero,(a,b)=>a+b)/high.Length);
            }
        }
        Object.DestroyImmediate(plot.gameObject);
        File.WriteAllLines("Documentation/StairOccupancyValidation.txt",report);
        if(enforce&&report.Any(r=>r.StartsWith("FAIL")))throw new Exception("Stair occupancy validation failed");
        Debug.Log("STAIR_OCCUPANCY_VALIDATION_DONE checks="+report.Count);
    }
}
#endif
