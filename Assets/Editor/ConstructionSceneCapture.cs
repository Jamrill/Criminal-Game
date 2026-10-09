using System;
using System.Linq;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ConstructionSceneCapture
{
    [Serializable] class Snapshot { public string scenePath; public BuildRecord[] records; }
    static string Key => "CriminalGame.BuildCapture."+Application.dataPath;
    static ConstructionSceneCapture() { EditorApplication.playModeStateChanged+=OnPlayState; }
    [MenuItem("Tools/Criminal Game/Construccion/Guardar construccion en la escena")]
    public static void Capture()
    {
        if(!EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Construccion","Usa esta opcion durante Play. Al salir se aplicara a la escena y podras guardarla con Ctrl+S.","Aceptar"); return; }
        var scene=SceneManager.GetActiveScene();
        var records=UnityEngine.Object.FindObjectsByType<PlacedBuildObject>(FindObjectsSortMode.None)
            .Where(p=>p.gameObject.scene==scene).Select(p=>p.Capture()).ToArray();
        EditorPrefs.SetString(Key,JsonUtility.ToJson(new Snapshot{scenePath=scene.path,records=records}));
        Debug.Log("Construccion capturada: "+records.Length+" objetos. Sal de Play para aplicarla; despues guarda la escena.");
    }
    static void OnPlayState(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode || !EditorPrefs.HasKey(Key)) return;
        var data=JsonUtility.FromJson<Snapshot>(EditorPrefs.GetString(Key));
        var scene=SceneManager.GetSceneByPath(data.scenePath);
        if(!scene.IsValid() || !scene.isLoaded) { Debug.LogWarning("Abre la escena capturada antes de aplicar la construccion."); return; }
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        if(!catalog) return;
        Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Conservar construccion de Play");
        foreach(var record in data.records)
        {
            var entry=catalog.Find(record.definitionId); if(entry==null || !entry.prefab) continue;
            var marker=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlacedBuildObject>(true)).FirstOrDefault(p=>p.persistentId==record.id);
            if(!marker)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(entry.prefab,scene);
                Undo.RegisterCreatedObjectUndo(go,"Construir"); marker=Undo.AddComponent<PlacedBuildObject>(go);
            }
            Undo.RegisterFullObjectHierarchyUndo(marker.gameObject,"Actualizar construccion");
            marker.Restore(record,catalog); marker.runtimePlaced=false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker.transform);
            foreach(var r in marker.GetComponentsInChildren<Renderer>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        foreach(var root in scene.GetRootGameObjects()) foreach(var wall in root.GetComponentsInChildren<WallTopology>())
            Undo.RegisterFullObjectHierarchyUndo(wall.gameObject,"Actualizar variantes de pared");
        WallTopology.RefreshScene(scene);
        foreach(var root in scene.GetRootGameObjects()) foreach(var wall in root.GetComponentsInChildren<WallTopology>())
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(wall);
            if(wall.wall) { PrefabUtility.RecordPrefabInstancePropertyModifications(wall.wall); PrefabUtility.RecordPrefabInstancePropertyModifications(wall.wall.GetComponent<Renderer>()); }
        }
        Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene); EditorPrefs.DeleteKey(Key);
        Debug.Log("Construccion aplicada. Guarda la escena con Ctrl+S; puedes deshacerla con Ctrl+Z.");
    }
}

public sealed class ConstructionToolsWindow : EditorWindow
{
    [MenuItem("Tools/Criminal Game/Construccion/Abrir herramienta")]
    static void Open() => GetWindow<ConstructionToolsWindow>("Construccion");
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Durante Play, pulsa el boton para capturar lo construido. Al salir se aplicara a la escena. Despues guarda con Ctrl+S. Las pruebas sin captura no modifican la escena.",MessageType.Info);
        using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            if(GUILayout.Button("Guardar construccion en la escena",GUILayout.Height(40))) ConstructionSceneCapture.Capture();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Solar",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Buildable activado, Property Id de la propiedad comprada, Box Collider Trigger y Grid Origin al nivel del suelo. Escala 1 en solar y origen.",MessageType.None);
    }
}
