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

public static class StairPlacementClearanceValidation
{
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string label){if(!ok)throw new Exception(label);report.Add("PASS "+label);}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var stairs=catalog.entries.Single(StairConstruction.IsStairs);
        var plot=new GameObject("Stair placement test plot").AddComponent<BuildPlot>();plot.Volume.size=Vector3.one*200;plot.catalog=catalog;
        var blocker=new GameObject("Adjacent wall").AddComponent<BoxCollider>();
        var boxes=stairs.prefab.GetComponentsInChildren<BoxCollider>(true);
        var original=boxes.Select(b=>(b.center,b.size)).ToArray();
        try
        {
            foreach(float angle in new[]{0f,90f,180f,270f})
            {
                var rotation=Quaternion.Euler(0,angle,0);
                bool Valid()=>BuildPlacement.IsValid(stairs,plot,Vector3.zero,rotation,rotation*stairs.bounds.center);
                foreach(int side in new[]{-1,1})
                {
                    var center=stairs.bounds.center+Vector3.right*side*(stairs.bounds.extents.x+.1f-.4f);
                    blocker.transform.SetPositionAndRotation(rotation*center,rotation);blocker.size=new Vector3(.2f,2,2);Physics.SyncTransforms();
                    Check(Valid(),"Stairs allows 0.4 side overlap: yaw="+angle+" side="+side);
                    blocker.transform.position-=rotation*Vector3.right*side*.25f;Physics.SyncTransforms();
                    Check(!Valid(),"Stairs rejects 0.65 side overlap: yaw="+angle+" side="+side);
                }
                foreach(int end in new[]{-1,1})
                {
                    blocker.size=new Vector3(2,2,.2f);
                    var modular=StairConstruction.PlacementBounds(stairs);
                    blocker.transform.SetPositionAndRotation(rotation*(modular.center+Vector3.forward*end*(modular.extents.z+.1f-.3f)),rotation);Physics.SyncTransforms();
                    Check(!Valid(),"Stairs still blocks front/back overlap: yaw="+angle+" end="+end);
                }
            }
            blocker.size=new Vector3(2,2,2);blocker.transform.SetPositionAndRotation(stairs.bounds.center,Quaternion.identity);Physics.SyncTransforms();
            Check(!BuildPlacement.IsValid(stairs,plot,Vector3.zero,Quaternion.identity,stairs.bounds.center),"Central obstruction still blocks stairs");
            var ordinary=new BuildEntry{prefab=new GameObject("Ordinary furniture"),bounds=stairs.bounds,category=BuildCategory.Furniture};
            blocker.size=new Vector3(.2f,2,2);blocker.transform.position=stairs.bounds.center+Vector3.right*(stairs.bounds.extents.x-.3f);Physics.SyncTransforms();
            Check(!BuildPlacement.IsValid(ordinary,plot,Vector3.zero,Quaternion.identity,stairs.bounds.center),"Other furniture receives no extra clearance");
            Object.DestroyImmediate(ordinary.prefab);
            Check(boxes.Select((b,i)=>b.center==original[i].center && b.size==original[i].size).All(v=>v),"Real stair colliders remain unchanged");
        }
        finally{Object.DestroyImmediate(blocker.gameObject);Object.DestroyImmediate(plot.gameObject);}
        RoomConstructionValidation.Run();
        File.WriteAllLines("Documentation/StairPlacementClearanceValidation.txt",report);
        Debug.Log("STAIR_PLACEMENT_CLEARANCE_OK checks="+report.Count);
    }
}
#endif
