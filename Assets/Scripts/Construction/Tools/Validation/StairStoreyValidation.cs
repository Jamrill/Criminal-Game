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

public static class StairStoreyValidation
{
    public static void Full(){Run();PlacementFeedbackValidation.Full();}
    public static void Run()
    {
        var report=new List<string>();void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);Debug.Log(report.Last());}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;
        var plot=new GameObject("Storey fixture").AddComponent<BuildPlot>();plot.propertyId=951;plot.catalog=catalog;plot.buildable=true;plot.Volume.size=Vector3.one*200;
        plot.transform.position=new Vector3(474,109,507);plot.gridOrigin=new GameObject("Grid origin").transform;plot.gridOrigin.SetParent(plot.transform,false);plot.gridOrigin.localPosition=Vector3.down*8;
        var manager=plot.gameObject.AddComponent<RoomConstruction>();
        var stair=catalog.entries.Single(StairConstruction.IsStairs);var wall=catalog.entries.Single(e=>e.prefab.name=="Wall_complete");var floor=catalog.entries.Single(FloorEditing.IsFloor);
        var window=catalog.entries.First(e=>!e.hiddenInMenu&&WallGeometry.Family(e)=="window");
        var services=new GameObject("Fixture economy");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();var properties=services.AddComponent<PropertyService>();
        foreach(Component service in new Component[]{money,properties})service.GetType().GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,save);
        typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{new SaveData{money=1000000,ownedProperties=new[]{951}}});
        foreach(float yaw in new[]{0f,90f,180f,270f})
        {
            var rotation=Quaternion.Euler(0,yaw,0);Vector3 P(Vector2 p,float y=0)=>plot.Grid.position+rotation*new Vector3(p.x,y,p.y);
            PlacedBuildObject Span(BuildEntry e,Vector3 a,Vector3 b)
            {
                var template=WallGeometry.At(e.prefab.GetComponent<WallTopology>(),Vector3.zero,Quaternion.identity,e.prefab.transform.localScale).Single();
                var r=Quaternion.AngleAxis(Vector3.SignedAngle(template.b-template.a,b-a,Vector3.up),Vector3.up);
                return ConstructionPersistence.Spawn(catalog,new BuildRecord{id=Guid.NewGuid().ToString("N"),definitionId=e.id,propertyId=951,position=a-r*template.a,rotation=r,scale=e.prefab.transform.localScale,finish=-1},true,scene);
            }
            var corners=new[]{new Vector2(-8,-4),new Vector2(16,-4),new Vector2(16,20),new Vector2(-8,20)};
            for(int side=0;side<4;side++)for(int i=0;i<6;i++)Span(wall,P(Vector2.Lerp(corners[side],corners[(side+1)%4],i/6f)),P(Vector2.Lerp(corners[side],corners[(side+1)%4],(i+1)/6f)));
            Physics.SyncTransforms();manager.Rebuild(true,money);
            Check(manager.rooms.Count==1&&manager.rooms[0].ceilingComplete,"Fully enclosed lower floor, yaw="+yaw);
            var upperSlabs=plot.GetComponentsInChildren<RoomFloorPatch>().Where(p=>Mathf.Abs(plot.Grid.InverseTransformPoint(p.transform.position).y-7)<.06f).ToArray();
            Check(upperSlabs.Length>0&&upperSlabs.All(p=>Mathf.Abs(p.transform.localPosition.y+1)<.0001f),"Upper slabs have local Y -1, not -0.98, yaw="+yaw);
            Check(upperSlabs.All(p=>p.triangles.All(t=>Mathf.Abs(t.y-floor.bounds.max.y)<.0001f)),"Generated slab surface keeps authored Floor local height, yaw="+yaw);
            var record=new BuildRecord{id="corner-stairs",definitionId=stair.id,propertyId=951,position=plot.Grid.position,rotation=rotation,scale=Vector3.one,finish=-1};
            var plan=StairConstruction.Plan(catalog,plot,record);
            var added=plan.additions.Where(r=>catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()&&Mathf.Abs(r.position.y-108)<.06f).ToArray();
            var spans=added.SelectMany(r=>WallGeometry.At(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>(),r.position,r.rotation,r.scale)).ToArray();
            Check(string.IsNullOrEmpty(plan.error),"Corner staircase plan valid, yaw="+yaw+": "+plan.error);
            Check(spans.Length==14&&spans.All(s=>Mathf.Abs(s.a.y-108)<.001f),"Lower walls cannot suppress any upper enclosure arms, yaw="+yaw);
            Check(added.Count(r=>WallGeometry.Local(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()).Count==2)==4,"Corner enclosure retains four Corner prefabs, yaw="+yaw);
            var preserved=Span(window,P(new Vector2(-8,-4),7),P(new Vector2(-4,-4),7));Physics.SyncTransforms();
            plan=StairConstruction.Plan(catalog,plot,record);
            Check(!plan.edits.Any(e=>e.item==preserved)&&plan.additions.Where(r=>catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()&&Mathf.Abs(r.position.y-108)<.06f).Sum(r=>WallGeometry.Local(catalog.Find(r.definitionId).prefab.GetComponent<WallTopology>()).Count)==13,"Real upper window is reused, lower walls are not, yaw="+yaw);
            int balance=money.Money;var built=StairConstruction.Build(catalog,plot,record,money);
            Check(built&&money.Money==balance-plan.Cost(catalog),"Staircase and complete upper enclosure charged once, yaw="+yaw);
            manager.Rebuild(true,money);
            Check(manager.rooms.Any(r=>Mathf.Abs(r.floor-7)<.001f&&r.ceilingComplete),"Built upper enclosure forms a closed room with roof, yaw="+yaw);
            Check(plot.GetComponentsInChildren<RoomFloorPatch>().All(p=>Mathf.Abs(plot.Grid.InverseTransformPoint(p.transform.position).y/7-Mathf.Round(plot.Grid.InverseTransformPoint(p.transform.position).y/7))<.0001f),"Cutting and completing slabs never moves their authored storey origins, yaw="+yaw);
            var records=BuildDeletionLedger.CaptureScene(scene);foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())Object.DestroyImmediate(item.gameObject);
            foreach(var r in JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData{buildings=records})).buildings.Where(r=>!r.deleted))ConstructionPersistence.Spawn(catalog,r,true,scene);
            manager.Rebuild();Check(manager.rooms.Any(r=>Mathf.Abs(r.floor-7)<.001f&&r.ceilingComplete),"Save/load retains complete upper enclosure and slab coverage, yaw="+yaw);
            foreach(var item in plot.GetComponentsInChildren<PlacedBuildObject>())Object.DestroyImmediate(item.gameObject);
            var ledger=plot.GetComponent<BuildDeletionLedger>();if(ledger)Object.DestroyImmediate(ledger);manager.Rebuild();Physics.SyncTransforms();
        }
        Object.DestroyImmediate(plot.gameObject);Object.DestroyImmediate(services);Object.DestroyImmediate(catalog);
        File.WriteAllLines("Documentation/StairStoreyValidation.txt",report);
        if(report.Any(r=>r.StartsWith("FAIL")))throw new Exception("Stair storey validation failed");
        Debug.Log("STAIR_STOREY_VALIDATION_OK checks="+report.Count);
    }
}
#endif
