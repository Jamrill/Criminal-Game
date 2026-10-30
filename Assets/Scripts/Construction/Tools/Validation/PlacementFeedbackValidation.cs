#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class PlacementFeedbackValidation
{
    public static void Full(){Run();StairJunctionValidation.Full();StairPlacementClearanceValidation.Run();}
    public static void Run()
    {
        var results=new List<string>();
        void Check(bool ok,string name){results.Add((ok?"PASS ":"FAIL ")+name);Debug.Log(results.Last());}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var wall=catalog.entries.Single(e=>e.prefab.name=="Wall_complete");
        var stairs=catalog.entries.Single(StairConstruction.IsStairs);var floor=catalog.entries.Single(FloorEditing.IsFloor);
        var plot=new GameObject("Placement feedback fixture").AddComponent<BuildPlot>();plot.propertyId=949;plot.catalog=catalog;plot.Volume.size=Vector3.one*200;
        plot.transform.position=new Vector3(474,101,507);
        var blockers=new List<Collider>();
        var local=WallGeometry.At(wall.prefab.GetComponent<WallTopology>(),Vector3.zero,Quaternion.identity,wall.prefab.transform.localScale).Single();
        bool Valid(Vector3 a,Vector3 b)
        {
            blockers.Clear();var rotation=Quaternion.AngleAxis(Vector3.SignedAngle(local.b-local.a,b-a,Vector3.up),Vector3.up);
            var position=a-rotation*local.a;
            return BuildPlacement.IsValid(wall,plot,position,rotation,position,blockers);
        }
        foreach(float angle in new[]{0f,90f,180f,270f})
        {
            var rot=Quaternion.Euler(0,angle,0);Vector3 P(float x,float y,float z)=>plot.transform.position+rot*new Vector3(x,y,z);
            var stair=ConstructionPersistence.Spawn(catalog,new BuildRecord{id="stairs-test",definitionId=stairs.id,propertyId=949,position=plot.transform.position,rotation=rot,scale=Vector3.one,finish=-1},true,scene);
            var boxes=stair.GetComponentsInChildren<BoxCollider>();var original=boxes.Select(b=>(b.center,b.size)).ToArray();Physics.SyncTransforms();
            Check(Valid(P(-8,0,0),P(-8,0,4)),"Wall beside left flight allowed, yaw="+angle);
            Check(Valid(P(4,0,0),P(4,0,4)),"Wall beside right flight allowed, yaw="+angle);
            Check(Valid(P(-8,0,-4),P(-4,0,-4)),"Wall behind landing allowed, yaw="+angle);
            Check(!Valid(P(-6,0,0),P(-6,0,4))&&blockers.Any(c=>c.GetComponentInParent<PlacedBuildObject>()==stair),"Wall through flight blocked and reports actual stairs, yaw="+angle);
            Check(!Valid(P(-7.2f,0,0),P(-7.2f,0,4)),"Excessive side intrusion still blocked, yaw="+angle);
            var red=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var visible=stair.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var materials=visible.Select(r=>r.sharedMaterials).ToArray();
            var editing=new ConstructionEditing();editing.HighlightBlockers(blockers,red);
            Check(visible.Any(r=>r.sharedMaterials.Contains(red)),"Blocking object highlighted, yaw="+angle);
            editing.Clear();Check(visible.Select((r,i)=>r.sharedMaterials.SequenceEqual(materials[i])).All(v=>v),"Original materials restored after highlight, yaw="+angle);editing.Dispose();Object.DestroyImmediate(red);
            Check(boxes.Select((b,i)=>b.center==original[i].center&&b.size==original[i].size).All(v=>v),"Physical stair colliders unchanged, yaw="+angle);
            Object.DestroyImmediate(stair.gameObject);Physics.SyncTransforms();
            foreach(float level in new[]{0f,7f,3.5f})
            {
                var tile=ConstructionPersistence.Spawn(catalog,new BuildRecord{id="test-slab",definitionId=floor.id,propertyId=949,position=P(0,level+.01f,-2),rotation=rot,scale=Vector3.one,finish=-1,
                    floorTriangles=new[]{new Vector3(0,0,0),new Vector3(4,0,0),new Vector3(4,0,4),new Vector3(0,0,0),new Vector3(4,0,4),new Vector3(0,0,4)}},true,scene);
                Physics.SyncTransforms();bool valid=Valid(P(0,0,0),P(4,0,0));
                Check(level==3.5f?!valid&&blockers.Any(c=>c.GetComponentInParent<PlacedBuildObject>()==tile):valid,"Thin slab at height "+level+" respects wall seams, yaw="+angle);
                Object.DestroyImmediate(tile.gameObject);
            }
            Physics.SyncTransforms();
        }
        // Cancellation is the same method used by right-click, before left-release
        // commits the stroke. No room/build transaction may execute on cancellation.
        var go=new GameObject("Cancellation fixture");go.SetActive(false);var controller=go.AddComponent<ConstructionController>();controller.catalog=catalog;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(ConstructionController);
        type.GetField("plot",flags).SetValue(controller,plot);type.GetField("building",flags).SetValue(controller,true);
        type.GetMethod("Select",flags).Invoke(controller,new object[]{wall});
        var stroke=(WallBuildStroke)type.GetField("wallStroke",flags).GetValue(controller);
        stroke.Begin(wall,plot,plot.transform.position,Quaternion.identity);
        type.GetMethod("QueueWall",flags).Invoke(controller,new object[]{plot.transform.position,Quaternion.identity,plot.transform.position});
        var pending=(IList)type.GetField("pendingWalls",flags).GetValue(controller);Check(pending.Count==1&&stroke.Active,"Pending stroke exists before cancel");
        type.GetMethod("CancelPlacement",flags).Invoke(controller,null);
        Check(pending.Count==0&&!stroke.Active&&type.GetField("selected",flags).GetValue(controller)==null,"Cancel discards pending purchase and selection");
        Check((bool)type.GetField("building",flags).GetValue(controller)&&!plot.GetComponentsInChildren<PlacedBuildObject>().Any(),"Cancellation keeps build HUD mode without creating objects");
        Check(((IList)type.GetField("pendingPreviews",flags).GetValue(controller)).Count==0&&type.GetField("ghost",flags).GetValue(controller)==null,"Cancellation removes ghost and stroke previews");
        Object.DestroyImmediate(go);Object.DestroyImmediate(plot.gameObject);
        File.WriteAllLines("Documentation/PlacementFeedbackValidation.txt",results);
        if(results.Any(r=>r.StartsWith("FAIL")))throw new Exception("Placement feedback regressions failed");
        Debug.Log("PLACEMENT_FEEDBACK_VALIDATION_OK checks="+results.Count);
    }
}
#endif
