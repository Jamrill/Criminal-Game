using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FurnitureBasicSetup
{
    const string Report = "Documentation/FurnitureBasicSetup.txt";
    static readonly string[] Names = { "Counter", "Central_display_unit", "Bed", "Chair", "Table", "Sofa", "Toilet" };
    static FurnitureBasicSetup() { EditorApplication.update += Once; }
    static void Once()
    {
        if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        EditorApplication.update -= Once;
        if (File.Exists(Report) && File.ReadAllText(Report).StartsWith("PASS FurnitureBasic.v1")) return;
        if (SessionState.GetBool("FurnitureBasic.v1", false)) return;
        SessionState.SetBool("FurnitureBasic.v1", true);
        try { Apply(); } catch (Exception e) { File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Criminal Game/Ajustar siete muebles basicos")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Sal de Play y de Prefab Mode.");
        Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + name + ".mat") ?? throw new Exception("Missing material " + name);
        var wood = Load("Furniture/Madera_Miel"); var cream = Load("Furniture/Crema_Mate");
        var metal = Load("Furniture/Metal_Grafito_Satinado"); var cloth = Load("cloth");
        var leather = Load("Leather"); var white = Load("White"); var glass = Load("Glass");
        var screen = Load("Furniture/Pantalla_Apagada");
        var report = new List<string>();
        foreach (var name in Names)
        {
            string path = "Assets/Prefabs/Furniture/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh && f.GetComponent<MeshRenderer>()).ToArray();
                if (filters.Length == 0) throw new Exception("No meshes in " + path);
                // Replace only solid collision; preserve interaction triggers, scripts,
                // hierarchy, mesh references, pivots and the original FBX assets.
                foreach (var collider in root.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var filter in filters)
                {
                    var bounds = filter.sharedMesh.bounds;
                    var box = filter.gameObject.AddComponent<BoxCollider>();
                    box.center = bounds.center;
                    box.size = Vector3.Max(bounds.size, Vector3.one * .001f);
                    var renderer = filter.GetComponent<MeshRenderer>();
                    var previous = renderer.sharedMaterials;
                    var materials = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material old = i < previous.Length ? previous[i] : null;
                        string label = old ? old.name.ToLowerInvariant() : "";
                        Material selected;
                        if (label.Contains("pantalla_apagada") || (name == "Counter" && filter.name == "Cash_register" && i == 1)) selected = screen;
                        else if (old && AssetDatabase.GetAssetPath(old).StartsWith("Assets/Materials/")) selected = old;
                        else if (label.Contains("metal") || label.Contains("steel")) selected = metal;
                        else if (label.Contains("water") || label.Contains("glass")) selected = glass;
                        else if (name == "Toilet") selected = white;
                        else if (label.Contains("roble") || label.Contains("wood")) selected = wood;
                        else if (label.Contains("cuero") || label.Contains("leather")) selected = leather;
                        else if (label.Contains("cojin") || label.Contains("lino") || name == "Sofa" || filter.name.ToLowerInvariant().Contains("colchon") || filter.name.ToLowerInvariant().Contains("almohada")) selected = cloth;
                        else if (label.Contains("pantalla") || label.Contains("crema")) selected = cream;
                        else selected = wood;
                        materials[i] = selected;
                        report.Add($"{name}/{filter.name} material {i}: {(old ? old.name : "none")} -> {selected.name}");
                    }
                    renderer.sharedMaterials = materials;
                    report.Add($"{name}/{filter.name} Box center={box.center:F4} size={box.size:F4}");
                }
                var solids = root.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
                if (solids.Length != filters.Length || solids.Any(c => !(c is BoxCollider))) throw new Exception("Collider validation failed: " + name);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Add("PASS " + name + ": " + solids.Length + " BoxCollider(s)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        foreach (var name in Names)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Furniture/" + name + ".prefab");
            if (prefab.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger && !(c is BoxCollider))) throw new Exception("Saved prefab has non-box collision " + name);
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.sharedMaterials.Any(m => !m || !AssetDatabase.GetAssetPath(m).StartsWith("Assets/Materials/"))) throw new Exception("Saved prefab has missing/external material " + name);
        }
        report.Insert(0, "PASS FurnitureBasic.v1 " + DateTime.Now.ToString("O"));
        File.WriteAllLines(Report, report);
        Debug.Log("FURNITURE_BASIC_PASSED: seven prefabs saved and verified.");
    }
}
