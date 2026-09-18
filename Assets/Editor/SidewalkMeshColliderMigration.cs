using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot, explicitly requested replacement of bounding boxes with exact static meshes.
[InitializeOnLoad]
public static class SidewalkMeshColliderMigration
{
    const string Done = "Documentation/SidewalkMeshColliders.done";
    static readonly string[] Names = {
        "SideWalk_incomplete", "SideWalk_round_incomplete",
        "SideWalk_inner_incomplete", "SideWalk_big_round_incomplete"
    };

    static SidewalkMeshColliderMigration()
    {
        if (!File.Exists(Done) && !Application.isBatchMode)
            EditorApplication.delayCall += TryRun;
    }

    static void TryRun()
    {
        if (File.Exists(Done)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }
        Run();
    }

    [MenuItem("Tools/Criminal Game/Ajustar colision exacta de Sidewalk")]
    public static void Run()
    {
        if (File.Exists(Done)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)
            throw new InvalidOperationException("Salir de Play y Prefab Mode antes de ajustar las aceras.");
        Directory.CreateDirectory("Documentation/SidewalkColliderBackup");
        foreach (string name in Names)
        {
            string path = "Assets/Prefabs/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) throw new Exception("Prefab no disponible: " + path);
            string backup="Documentation/SidewalkColliderBackup/" + name + ".prefab";
            if (!File.Exists(backup)) File.Copy(path,backup);
            if (!File.Exists(backup+".meta")) File.Copy(path+".meta",backup+".meta");
        }
        foreach (string name in Names)
        {
            string path="Assets/Prefabs/"+name+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var meshes=root.GetComponentsInChildren<MeshFilter>(true);
                if(meshes.Length==0)throw new Exception("Sin mallas: "+name);
                foreach(var f in meshes)if(f.sharedMesh==null)throw new Exception("Malla perdida: "+name);
                foreach(var collider in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                foreach(var f in meshes)
                {
                    var collider=f.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh=f.sharedMesh;
                    collider.convex=false;
                    collider.isTrigger=false;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        foreach(string name in Names)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+name+".prefab");
            if(asset.GetComponentsInChildren<BoxCollider>(true).Length!=0)throw new Exception("Caja restante: "+name);
            foreach(var f in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                var c=f.GetComponent<MeshCollider>();
                if(c==null || c.convex || c.sharedMesh!=f.sharedMesh)throw new Exception("Colision incorrecta: "+name);
            }
        }
        File.WriteAllText(Done,"PASS: cuatro prefabs con MeshCollider no convexo, misma malla que el MeshFilter; sin BoxCollider. "+DateTime.Now.ToString("O"));
        Debug.Log("Sidewalk: colision ajustada a las mallas, sin rellenar zonas vacias.");
    }
}
