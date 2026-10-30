#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Construction;
using JuegoCriminal.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Explicit one-shot migration; does not regenerate imported animation curves or avatars.
public static class WindowRoomAimRepair
{
    const string Controller="Assets/Animations/PlayerLocomotion.controller";
    static string[] Windows()=>AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"})
        .Select(AssetDatabase.GUIDToAssetPath).Where(p=>Path.GetFileNameWithoutExtension(p).StartsWith("Window",StringComparison.OrdinalIgnoreCase)).ToArray();
    public static void Inspect()
    {
        var lines=new List<string>();
        foreach(var path in Windows().Concat(new[]{"Assets/Prefabs/Construction/Completes/Glass_wall.prefab"}))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                    for(int i=0;i<r.sharedMaterials.Length;i++)
                    {var m=r.sharedMaterials[i];lines.Add(path+" / "+r.name+" ["+i+"] "+(m?m.name+" :: "+AssetDatabase.GetAssetPath(m):"null"));}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        File.WriteAllLines("Documentation/WindowMaterialsInspection.txt",lines);
        Debug.Log(string.Join("\n",lines));
    }
    static bool IsGlass(Material material)=>material && material.name.IndexOf("glass",StringComparison.OrdinalIgnoreCase)>=0;
    public static void Apply()
    {
        Inspect();
        var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Glass.mat");
        if(!glass)throw new Exception("Missing canonical Glass material");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Construction/Completes/Glass_wall.prefab");
        if(!source.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Contains(glass)))throw new Exception("Glass_wall does not use canonical Glass");
        string backup="Documentation/WindowRoomAimBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        File.Copy(Controller,backup+"/PlayerLocomotion.controller");
        var report=new List<string>{"Backup "+backup};
        foreach(var path in Windows())
        {
            File.Copy(path,backup+"/"+Path.GetFileName(path));
            var root=PrefabUtility.LoadPrefabContents(path);int changed=0;
            try
            {
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials=r.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)if(IsGlass(materials[i]) && materials[i]!=glass){materials[i]=glass;changed++;}
                    r.sharedMaterials=materials;PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
                // Preserve the assignment when the wall changes complete/semi/incomplete skin.
                foreach(var wall in root.GetComponentsInChildren<WallTopology>(true))
                    foreach(var variant in wall.variantMaterials)
                        if(variant?.values!=null)for(int i=0;i<variant.values.Length;i++)if(IsGlass(variant.values[i]))variant.values[i]=glass;
                PrefabUtility.SaveAsPrefabAsset(root,path);report.Add(path+" replaced glass slots="+changed);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        var machine=controller.layers[0].stateMachine;
        foreach(var entry in machine.states.ToArray())
            if(entry.state.name=="Pistol Draw" || entry.state.name=="Pistol Holster")
            {report.Add("Removed state "+entry.state.name);machine.RemoveState(entry.state);}
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        File.WriteAllLines("Documentation/WindowRoomAimRepair.txt",report);
        Validate();
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Validate()
    {
        var report=new List<string>();
        void Check(bool ok,string label){Require(ok,label);report.Add("PASS "+label);}
        var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Glass.mat");
        foreach(var path in Windows())
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var slots=root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(IsGlass).ToArray();
            Check(slots.Length>0 && slots.All(m=>m==glass),path+" uses canonical Glass");
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var plot=new GameObject("Reflection resize test").AddComponent<BuildPlot>();plot.Volume.size=Vector3.one*100;
        var manager=plot.gameObject.AddComponent<RoomConstruction>();manager.completeAutomatically=false;
        var edges=new[]{Vector3.zero,new Vector3(4,0,0),new Vector3(4,0,4),new Vector3(0,0,4)};
        var walls=new List<WallTopology>();
        for(int i=0;i<4;i++)
        {
            var wall=new GameObject("Wall "+i).AddComponent<WallTopology>();wall.transform.SetParent(plot.transform,false);
            wall.height=4;wall.segmentPoints=new[]{edges[i],edges[(i+1)%4]};walls.Add(wall);
        }
        foreach(float y in new[]{-.01f,4.01f})
        {
            var floor=new GameObject("Broad floor/ceiling").AddComponent<BuildFloorCoverage>();floor.transform.SetParent(plot.transform,false);
            floor.transform.position=new Vector3(4,y,4);floor.GetComponent<BoxCollider>().size=new Vector3(8,.02f,8);
        }
        manager.Rebuild();
        Check(manager.rooms.Count==1 && plot.GetComponentsInChildren<ReflectionZone>().Length==1,"Closed room gets one probe");
        Check(Mathf.Abs(plot.GetComponentInChildren<ReflectionZone>().Size.x-4)<.001f,"Initial reflection spans four units");
        int revision=RoomConstruction.Revision;
        foreach(var wall in walls)
        {
            wall.transform.localScale=new Vector3(2,1,1);
            typeof(WallTopology).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(wall,null);
        }
        Check(RoomConstruction.Revision>revision,"Moving/scaling walls invalidates room detection");
        typeof(RoomConstruction).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
        var zone=plot.GetComponentInChildren<ReflectionZone>();
        Check(manager.rooms.Count==1 && zone && Mathf.Abs(zone.Size.x-8)<.001f,"Automatic room scan resizes reflection to eight units");
        Check(plot.GetComponentsInChildren<ReflectionZone>().Length==1,"Old room reflection is removed on resize");
        // Exercise the actual native display probe, not just the zone gizmo.
        var type=typeof(ReflectionZone).Assembly.GetType("JuegoCriminal.Core.BlendedReflectionCapture");
        var capture=Activator.CreateInstance(type,new object[]{zone.transform,"Resize validation",zone.CapturePosition,128,0,30f,zone,null});
        var probes=zone.GetComponentsInChildren<ReflectionProbe>();
        try
        {
            zone.ConfigureRoom(new Bounds(new Vector3(10,3,5),new Vector3(12,6,10)),new Vector3(9,3,4));
            type.GetMethod("UpdateZone").Invoke(capture,new object[]{zone});
            var display=probes.Single(p=>p.mode==UnityEngine.Rendering.ReflectionProbeMode.Custom);
            var rendering=probes.Single(p=>p.mode==UnityEngine.Rendering.ReflectionProbeMode.Realtime);
            Check(display.size==zone.Size && Vector3.Distance(display.bounds.center,zone.transform.position)<.001f,"Native influence volume follows new room bounds");
            Check(rendering.transform.position==zone.CapturePosition,"Native cubemap capture origin follows resized room");
            Check(display.blendDistance==zone.BlendDistance && display.boxProjection,"Probe projection and blending remain configured");
        }
        finally
        {
            // Edit-mode validation owns these temporary GPU resources; no deferred Destroy.
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.NonPublic))
                if(field.GetValue(capture) is RenderTexture texture)Object.DestroyImmediate(texture);
            foreach(var probe in probes)Object.DestroyImmediate(probe.gameObject);
        }
        Object.DestroyImmediate(plot.gameObject);
        PlayerAnimationExpansion.Validate();
        RoomConstructionValidation.Run();
        File.WriteAllLines("Documentation/WindowRoomAimValidation.txt",report);
        Debug.Log("WINDOW_ROOM_AIM_VALIDATED "+report.Count);
    }
}
#endif
