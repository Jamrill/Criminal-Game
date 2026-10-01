using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time, editor-only migration. Never runs in a player build.
[InitializeOnLoad]
public static class ColoredLightSetup
{
    const string ColdMaterial = "Assets/Materials/Luz_Lampara_Fria.mat";
    const string ColdPrefab = "Assets/Prefabs/Furniture/Light_point_Cold.prefab";
    const string ScenePath = "Assets/Scenes/10_World_City.unity";
    const string Done = "Assets/Editor/ColoredLightSetup.completed.txt";

    static ColoredLightSetup() { EditorApplication.delayCall += AutoRun; }

    static void AutoRun()
    {
        if (System.IO.File.Exists(Done) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.Log("Luces: guarda la escena y ejecuta Tools/Criminal Game/Configurar luces de colores y fria.");
                return;
            }
        Run();
    }

    [MenuItem("Tools/Criminal Game/Configurar luces de colores y fria")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isDirty)
        {
            Debug.LogWarning("Guarda primero 10_World_City para conservar tus cambios.");
            return;
        }
        bool opened = !scene.IsValid() || !scene.isLoaded;
        var previous = SceneManager.GetActiveScene();
        try
        {
            CreateColdLamp();
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer || !filter.sharedMesh) continue;
                    var mesh = filter.sharedMesh;
                    var materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < Mathf.Min(materials.Length, mesh.subMeshCount); slot++)
                    {
                        var material = materials[slot];
                        if (!material) continue;
                        string name = material.name.ToLowerInvariant();
                        Color color;
                        if (name == "luz roja") color = new Color(1f, .015f, .008f);
                        else if (name == "luz verde") color = new Color(.015f, 1f, .06f);
                        else if (name == "luz ámbar" || name == "luz ambar") color = new Color(1f, .38f, .008f);
                        else continue;
                        string childName = "Luz_Real_" + slot;
                        if (filter.transform.Find(childName)) continue;
                        // Editor access to imported meshes is allowed without enabling Read/Write in builds.
                        var vertices = mesh.vertices;
                        var triangles = mesh.GetTriangles(slot);
                        Vector3 center = Vector3.zero, normal = Vector3.zero;
                        float total = 0;
                        for (int t = 0; t < triangles.Length; t += 3)
                        {
                            Vector3 a = filter.transform.TransformPoint(vertices[triangles[t]]);
                            Vector3 b = filter.transform.TransformPoint(vertices[triangles[t + 1]]);
                            Vector3 c = filter.transform.TransformPoint(vertices[triangles[t + 2]]);
                            var cross = Vector3.Cross(b - a, c - a);
                            float area = cross.magnitude;
                            center += (a + b + c) / 3 * area;
                            normal += cross;
                            total += area;
                        }
                        if (total < .000001f || normal.magnitude < total * .05f)
                            throw new InvalidOperationException("No se puede determinar el frente de " + filter.name + "/" + material.name);
                        center /= total;
                        normal.Normalize();
                        // Move in front of the outermost lens vertex to avoid self-shadowing.
                        float front = 0;
                        foreach (int index in triangles)
                            front = Mathf.Max(front, Vector3.Dot(filter.transform.TransformPoint(vertices[index]) - center, normal));
                        var go = new GameObject(childName, typeof(Light));
                        go.transform.SetParent(filter.transform, false);
                        go.transform.position = center + normal * (front + .08f);
                        go.transform.rotation = Quaternion.LookRotation(normal);
                        var light = go.GetComponent<Light>();
                        light.type = LightType.Spot;
                        light.color = color;
                        light.useColorTemperature = false;
                        light.intensity = 10;
                        light.range = 20;
                        light.spotAngle = 120;
                        light.innerSpotAngle = 75;
                        light.shadows = LightShadows.Soft;
                        light.shadowBias = .03f;
                        light.shadowNormalBias = .1f;
                        light.lightmapBakeType = LightmapBakeType.Realtime;
                        count++;
                    }
                }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("No se pudo guardar la escena.");
            AssetDatabase.SaveAssets();
            System.IO.File.WriteAllText(Done, "Configured colored spotlights and cold lamp.\n");
            Debug.Log($"Luces: {count} focos de colores añadidos. Plafón frío: {ColdPrefab}");
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    static void CreateColdLamp()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(ColdMaterial);
        if (!material)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Luz_Lampara_Calida.mat"));
            material.name = "Luz_Lampara_Fria";
            var color = new Color(.78f, .88f, 1);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetColor("_EmissionColor", color * 6);
            material.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(material, ColdMaterial);
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ColdPrefab)) return;
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Furniture/Light_point.prefab");
        try
        {
            root.name = "Light_point_Cold";
            root.transform.position = Vector3.zero;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] && materials[i].name == "Luz_Lampara_Calida") materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                light.name = "Luz_Fria_Real";
                light.color = new Color(.78f, .88f, 1);
                light.useColorTemperature = false;
                light.intensity = 12;
                light.range = 30;
            }
            PrefabUtility.SaveAsPrefabAsset(root, ColdPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
