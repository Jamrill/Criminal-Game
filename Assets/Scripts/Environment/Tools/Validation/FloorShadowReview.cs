#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class FloorShadowReview
{
    public static void InspectWallOrigins()
    {
        var report=new System.Collections.Generic.List<string>();
        void Check(JuegoCriminal.Construction.WallTopology w,string label)
        {
            float difference=Mathf.Abs(w.Bottom.y-w.wall.transform.position.y);
            report.Add(label+" base="+w.Bottom.y+" rendererOrigin="+w.wall.transform.position.y+" difference="+difference);
            if(difference>.001f)throw new Exception("Wall origin needs explicit shader base height: "+label);
        }
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Construction"}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var wall=root.GetComponent<JuegoCriminal.Construction.WallTopology>();if(wall && wall.wall)Check(wall,path);
        }
        EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        foreach(var w in Object.FindObjectsByType<JuegoCriminal.Construction.WallTopology>(FindObjectsSortMode.None))if(w.wall)Check(w,w.name);
        File.WriteAllLines("Documentation/WallShadowOrigins.txt",report);Debug.Log("WALL_SHADOW_ORIGINS_OK");
    }
    public static void ApplyContactCorrection()
    {
        string backup="Documentation/WallContactCorrectionBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        foreach(var p in new[]{"Assets/Materials/Wall_Building.mat","Assets/Prefabs/Floor.prefab","Assets/Scenes/10_World_City.unity"})File.Copy(p,backup+"/"+Path.GetFileName(p));
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wall_Building.mat");
        material.SetFloat("_ShadowReceiverInset",.05f);EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Floor.prefab");
        try
        {
            var proxy=root.transform.Find("Floor_ShadowBlocker");if(proxy)Object.DestroyImmediate(proxy.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Floor.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        if(Array.Exists(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None),r=>r.name=="Floor_ShadowBlocker"))throw new Exception("Unexpected orphan blocker");
        // Keep geometry, lights, cascade distribution and the user's current bias.
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Documentation/WallContactCorrection.txt","Backup="+backup+"\nWall_Building shadow contact offset=.05m downward along vertical surfaces. Floor_ShadowBlocker removed from prefab and inherited scene instances. PC shadow atlas retained at 4096.\n");
        Debug.Log("WALL_CONTACT_CORRECTION_OK");
    }
    public static void DawnComparison()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light sun=null;
        foreach(var l in lights) { if(l.type==LightType.Directional && l.name!="Moon")sun=l; l.enabled=false; }
        if(!sun)throw new Exception("Sun not found");
        sun.enabled=true;sun.intensity=1;sun.transform.rotation=Quaternion.Euler(8,-60,0);
        Capture("dawn_sun_only");
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))if(r.name=="Floor_ShadowBlocker")r.enabled=false;
        Capture("dawn_no_blocker");
        var matte=new Material(Shader.Find("Universal Render Pipeline/Lit"));matte.SetFloat("_Smoothness",0);
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var mats=r.sharedMaterials;bool changed=false;
            for(int i=0;i<mats.Length;i++)if(mats[i] && mats[i].shader.name=="CriminalGame/World Wall"){mats[i]=matte;changed=true;}
            if(changed)r.sharedMaterials=mats;
        }
        Capture("dawn_standard_lit");
        sun.enabled=false;
        foreach(var l in lights)if(l.type!=LightType.Directional)l.enabled=true;
        Capture("lamps_standard_lit");
        // Diagnostics never save scene/material changes.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Object.DestroyImmediate(matte);
        Debug.Log("DAWN_COMPARISON_OK no scene changes saved");
    }
    static void Capture(string label)
    {
        var go=new GameObject("FloorShadowDiagnosticCamera");var camera=go.AddComponent<Camera>();
        camera.enabled=false;camera.fieldOfView=60;camera.nearClipPlane=.05f;camera.farClipPlane=100;
        var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try
        {
            camera.targetTexture=rt;
            for(int i=0;i<2;i++)
            {
                go.transform.position=new Vector3(482,104,i==0?503:495);
                go.transform.LookAt(new Vector3(482,107.8f,509));camera.Render();
                RenderTexture.active=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                File.WriteAllBytes("Documentation/FloorShadow_"+label+"_"+i+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
        }
        finally{ShaderUtil.allowAsyncCompilation=async;RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
    }
    public static void Fix()
    {
        const string scenePath="Assets/Scenes/10_World_City.unity", prefabPath="Assets/Prefabs/Floor.prefab", pipelinePath="Assets/Settings/PC_RPAsset.asset";
        string backup="Documentation/FloorShadowBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        foreach(var path in new[]{scenePath,prefabPath,pipelinePath})File.Copy(path,backup+"/"+Path.GetFileName(path));
        var scene=EditorSceneManager.OpenScene(scenePath);Capture("before");
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        Material[] materials;
        try
        {
            var renderer=root.GetComponentInChildren<MeshRenderer>();var original=renderer.sharedMaterials;
            materials=new Material[original.Length];
            for(int i=0;i<original.Length;i++)
            {
                string path="Assets/Materials/Floor_"+original[i].name+"_Matte.mat";
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!m){m=new Material(original[i]);AssetDatabase.CreateAsset(m,path);}
                m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.12f);m.SetFloat("_Glossiness",.12f);
                EditorUtility.SetDirty(m);materials[i]=m;
            }
            renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            var previous=root.transform.Find("Floor_ShadowBlocker");
            if(!previous)
            {
                // Invisible closed volume: top stays below the visible upper face,
                // depth is below the ceiling to close shadow-map leaks at the junction.
                var proxy=GameObject.CreatePrimitive(PrimitiveType.Cube);proxy.name="Floor_ShadowBlocker";
                Object.DestroyImmediate(proxy.GetComponent<Collider>());proxy.transform.SetParent(root.transform,false);
                proxy.transform.localPosition=new Vector3(2,-.15f,2);proxy.transform.localScale=new Vector3(4.08f,.30f,4.08f);
                var r=proxy.GetComponent<MeshRenderer>();r.sharedMaterial=materials[0];
                r.shadowCastingMode=ShadowCastingMode.ShadowsOnly;r.receiveShadows=false;
                r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        var data=new SerializedObject(pipeline);data.FindProperty("m_MainLightShadowmapResolution").intValue=4096;data.ApplyModifiedPropertiesWithoutUndo();
        // Preserve all transforms and unrelated per-instance overrides; only restore
        // the floor's material/casting settings after the user's diagnostic tests.
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if(r.name=="Floor_ShadowBlocker")continue;
            var source=PrefabUtility.GetCorrespondingObjectFromSource(r);
            if(!source || AssetDatabase.GetAssetPath(source)!=prefabPath)continue;
            r.sharedMaterials=materials;r.shadowCastingMode=ShadowCastingMode.TwoSided;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        scene=EditorSceneManager.OpenScene(scenePath);Capture("after");
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var blocker=asset.transform.Find("Floor_ShadowBlocker");
        if(!blocker || blocker.GetComponent<Collider>() || blocker.GetComponent<MeshRenderer>().shadowCastingMode!=ShadowCastingMode.ShadowsOnly)throw new Exception("Invalid floor blocker");
        File.WriteAllText("Documentation/FloorShadowFix.txt","Backup="+backup+"\nMetallic=0; smoothness=.12. Invisible shadow blocker 4.08 x .30 x 4.08, no collider. PC main shadow atlas 4096. Scene transforms unchanged.\n");
        Debug.Log("FLOOR_SHADOW_FIX_OK");
    }
    public static void Inspect()
    {
        var b=new StringBuilder();
        foreach(string path in new[]{"Assets/Original_assets/Floor.fbx","Assets/Prefabs/Floor.prefab"})
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                b.AppendLine(path+" / "+r.name+" bounds="+r.localBounds+" shadows="+r.shadowCastingMode);
                foreach(var m in r.sharedMaterials)
                {
                    b.AppendLine("MATERIAL "+m.name+" path="+AssetDatabase.GetAssetPath(m)+" shader="+m.shader.name);
                    foreach(var p in new[]{"_Metallic","_Smoothness","_Glossiness","_Surface","_Cull","_ReceiveShadows"})if(m.HasProperty(p))b.AppendLine(p+"="+m.GetFloat(p));
                }
            }
        }
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional)
            b.AppendLine("LIGHT "+l.name+" shadows="+l.shadows+" bias="+l.shadowBias+" normal="+l.shadowNormalBias+" pipeline="+l.GetComponent<UniversalAdditionalLightData>()?.usePipelineSettings);
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))if(r.name.StartsWith("Floor"))
            b.AppendLine("SCENE "+r.name+" "+r.bounds+" shadows="+r.shadowCastingMode+" materials="+string.Join(",",Array.ConvertAll(r.sharedMaterials,m=>m?m.name:"null")));
        File.WriteAllText("Documentation/FloorShadowInspection.txt",b.ToString());
        Debug.Log("FLOOR_SHADOW_INSPECTION_OK");
    }
}
#endif
