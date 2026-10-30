#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class InteriorLightingCorrection
{
    static void Tune(Light light)
    {
        light.shadowBias=.01f;light.shadowNormalBias=.02f;
        var additional=light.GetComponent<UniversalAdditionalLightData>()??light.gameObject.AddComponent<UniversalAdditionalLightData>();
        additional.usePipelineSettings=false;
    }
    public static void Run()
    {
        const string scenePath="Assets/Scenes/10_World_City.unity",floorPath="Assets/Prefabs/Floor.prefab";
        string backup="Documentation/InteriorLightingBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        void Backup(string path)
        {
            string target=backup+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(path,target);
        }
        Backup(scenePath);Backup(floorPath);var report=new List<string>();var updated=new HashSet<string>();
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Furniture"}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!asset.GetComponentsInChildren<Light>(true).Any(l=>l.type!=LightType.Directional))continue;
            Backup(path);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var light in root.GetComponentsInChildren<Light>(true))if(light.type!=LightType.Directional)Tune(light);
                PrefabUtility.SaveAsPrefabAsset(root,path);updated.Add(path);report.Add("LAMP PREFAB "+path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var floor=PrefabUtility.LoadPrefabContents(floorPath);
        try
        {
            foreach(var r in floor.GetComponentsInChildren<MeshRenderer>())r.shadowCastingMode=ShadowCastingMode.On;
            PrefabUtility.SaveAsPrefabAsset(floor,floorPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(floor);}
        var scene=EditorSceneManager.OpenScene(scenePath);int floorCount=0,lightCount=0;
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var source=PrefabUtility.GetCorrespondingObjectFromSource(r);
            if(source && AssetDatabase.GetAssetPath(source)==floorPath)
            {
                r.shadowCastingMode=ShadowCastingMode.On;PrefabUtility.RecordPrefabInstancePropertyModifications(r);floorCount++;
            }
        }
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if(light.type==LightType.Directional)continue;
            var instance=PrefabUtility.GetOutermostPrefabInstanceRoot(light);
            string path=instance?PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance):"";
            if(!updated.Contains(path) && light.name!="Luz_Calida_Real" && light.name!="Luz_Fria_Real")continue;
            Tune(light);PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light.GetComponent<UniversalAdditionalLightData>());lightCount++;
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        report.Add($"floors={floorCount}; lights={lightCount}; backup={backup}");
        File.WriteAllLines("Documentation/InteriorLightingCorrection.txt",report);Debug.Log("INTERIOR_LIGHTING_CORRECTION_OK "+report.Last());
    }
}
#endif
