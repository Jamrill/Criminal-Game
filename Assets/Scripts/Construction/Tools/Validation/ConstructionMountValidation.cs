#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ConstructionMountValidation
{
    static readonly List<string> results=new();
    static void Check(bool value,string description)
    {
        results.Add((value?"PASS ":"FAIL ")+description);
        File.WriteAllLines("Documentation/ConstructionMountValidation.txt",results);
        if(!value)throw new Exception(description);
        Debug.Log("MOUNT_TEST_PASS "+description);
    }
    public static void ApplyAndValidate()
    {
        ConstructionMountSetup.Inspect();ConstructionMountSetup.Apply();Run();
    }
    public static void Run()
    {
        results.Clear();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var door=catalog.entries.Single(e=>e.prefab.name=="Door");
        var floor=catalog.entries.Single(e=>e.prefab.name=="Floor");
        var holes=catalog.entries.Where(e=>e.prefab.name.StartsWith("Wall_door_hole",StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(door.mount==BuildMount.Doorway && door.bounds.center.magnitude<10,"Door bounds are placement-relative, not saved world coordinates");
        Check(Vector3.Distance(floor.bounds.center,new Vector3(2,0,2))<.001f,"Floor catalog bounds corrected without changing prefab origins");
        Check(holes.Length==3 && holes.All(e=>Vector3.Distance(e.doorway.size,new Vector3(2.4f,4.8f,.5f))<.001f),"Complete, semi and incomplete doorway apertures come from colliders");
        var plot=new GameObject("MountTestSolar").AddComponent<BuildPlot>();
        plot.propertyId=847;plot.catalog=catalog;plot.Volume.size=new Vector3(100,100,100);
        PlacedBuildObject Spawn(BuildEntry e,string id,Vector3 pos,Quaternion rotation)=>ConstructionPersistence.Spawn(catalog,
            new BuildRecord{id=id,definitionId=e.id,propertyId=plot.propertyId,sceneName=scene.name,position=pos,rotation=rotation,scale=e.prefab.transform.localScale,finish=-1},true,scene);
        var roof=Spawn(floor,"roof",new Vector3(0,7,0),Quaternion.identity);
        var host=Spawn(holes.First(e=>!e.hiddenInMenu),"hole",Vector3.zero,Quaternion.identity);
        Physics.SyncTransforms();
        var ray=new Ray(new Vector3(2,2,-4),Vector3.forward);
        Check(BuildMounting.TryPose(door,catalog,plot,ray,0,null,out var pose),"Door finds the aperture even when ray passes through empty space");
        Check(Vector3.Distance(pose.anchor,new Vector3(2,0,0))<.001f,"Door anchor sits at centre/bottom of hole");
        Check(BuildMounting.IsValid(door,catalog,plot,pose),"Door fits between original wall box colliders");
        Check(BuildMounting.TryPose(door,catalog,plot,new Ray(new Vector3(2,2,4),Vector3.back),1,null,out var reversed) &&
            Quaternion.Angle(pose.rotation,reversed.rotation)>179 && Vector3.Distance(pose.anchor,reversed.anchor)<.001f && BuildMounting.IsValid(door,catalog,plot,reversed),"Door flips 180 degrees from either side without leaving its aperture");
        var built=Spawn(door,"door",pose.position,pose.rotation);
        var actual=new Bounds();bool first=true;
        foreach(var r in built.GetComponentsInChildren<MeshRenderer>())if(first){actual=r.bounds;first=false;}else actual.Encapsulate(r.bounds);
        Check(Mathf.Abs(actual.min.y)<.002f && Mathf.Abs(actual.center.x-2)<.002f && Mathf.Abs(actual.center.z)<.002f,"Spawned door geometry is in the hole, not merely its pivot");
        Check(built.transform.parent==plot.transform,"Mounted door remains a child of Solar");
        var saved=built.Capture();built.Restore(JsonUtility.FromJson<BuildRecord>(JsonUtility.ToJson(saved)),catalog);
        Check(Vector3.Distance(built.transform.position,pose.position)<.001f,"Door mounted position survives serialization and restore");
        var sheet=built.GetComponentsInChildren<Transform>().First(t=>t.name=="Sheet");sheet.localRotation=Quaternion.Euler(0,90,0);
        Check(!BuildMounting.IsValid(door,catalog,plot,pose),"An open door still reserves its aperture; no duplicate door");
        Object.DestroyImmediate(built.gameObject);
        var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(2,2,-2);blocker.transform.localScale=new Vector3(2,2,.2f);Physics.SyncTransforms();
        Check(!BuildMounting.TryPose(door,catalog,plot,ray,0,null,out _),"Cannot snap through unrelated blocking objects");Object.DestroyImmediate(blocker);
        host.transform.SetPositionAndRotation(new Vector3(10,0,10),Quaternion.Euler(0,90,0));Physics.SyncTransforms();
        ray=new Ray(host.transform.TransformPoint(new Vector3(2,2,-4)),host.transform.forward);
        Check(BuildMounting.TryPose(door,catalog,plot,ray,0,null,out var rotated) && Vector3.Distance(rotated.anchor,host.transform.TransformPoint(new Vector3(2,0,0)))<.001f &&
            BuildMounting.IsValid(door,catalog,plot,rotated),"Door respects rotated wall origin/orientation");
        foreach(var lamp in catalog.entries.Where(e=>e.prefab.name.StartsWith("Light_point",StringComparison.OrdinalIgnoreCase)))
        {
            Check(lamp.mount==BuildMount.Ceiling,lamp.title+" uses ceiling placement");
            Physics.SyncTransforms();
            Check(BuildMounting.TryPose(lamp,catalog,plot,new Ray(new Vector3(2,2,2),Vector3.up),0,null,out var lightPose),lamp.title+" finds real ceiling underside");
            Check(Mathf.Abs((lightPose.position+lightPose.rotation*new Vector3(lamp.bounds.center.x,lamp.bounds.max.y,lamp.bounds.center.z)).y-6.988f)<.002f &&
                BuildMounting.IsValid(lamp,catalog,plot,lightPose),lamp.title+" top touches underside without penetrating floor");
            var instance=Spawn(lamp,lamp.id,lightPose.position,lightPose.rotation);
            Check(instance.transform.parent==plot.transform && instance.GetComponentsInChildren<Light>().All(l=>Vector3.Dot(l.transform.forward,Vector3.down)>.99f),lamp.title+" real light points down and belongs to Solar");
            var bounds=instance.GetComponentInChildren<MeshRenderer>().bounds;
            Check(Mathf.Abs(bounds.max.y-6.988f)<.002f,lamp.title+" actual visible mesh meets the ceiling");
            var record=JsonUtility.FromJson<BuildRecord>(JsonUtility.ToJson(instance.Capture()));instance.Restore(record,catalog);
            Check(Vector3.Distance(instance.transform.position,lightPose.position)<.001f,lamp.title+" mounted position survives save/restore");
            Object.DestroyImmediate(instance.gameObject);
            Check(!BuildMounting.TryPose(lamp,catalog,plot,new Ray(new Vector3(2,10,2),Vector3.down),0,null,out _),lamp.title+" rejects floor top as a ceiling");
            Check(!BuildMounting.TryPose(lamp,catalog,plot,new Ray(new Vector3(6,2,2),Vector3.up),0,null,out _),lamp.title+" cannot float outside ceiling footprint");
            Check(BuildMounting.TryPose(lamp,catalog,plot,new Ray(new Vector3(.1f,2,2),Vector3.up),0,null,out var edge) &&
                BuildMounting.IsValid(lamp,catalog,plot,edge) && Mathf.Abs(edge.anchor.x-2)<.001f,lamp.title+" snaps edge aiming to the supported 4x4 cell centre");
        }
        Object.DestroyImmediate(host.gameObject);Physics.SyncTransforms();
        Check(!BuildMounting.TryPose(door,catalog,plot,new Ray(new Vector3(2,2,-4),Vector3.forward),0,null,out _),"Doors cannot be freely placed without a doorway");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Debug.Log("CONSTRUCTION_MOUNT_VALIDATION_OK checks="+results.Count);
    }
}
#endif
