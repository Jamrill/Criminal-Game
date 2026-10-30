#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using JuegoCriminal.Inventory;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
using MeshPreview=JuegoCriminal.Construction.MeshPreview;

public static class ScriptOrganizationValidation
{
    [Serializable] class Manifest { public Move[] moves; }
    [Serializable] class Move { public string source,destination,guid,mergedInto;public bool editorOnly; }
    static readonly List<string> report=new();
    static void Check(bool value,string message)
    {
        report.Add((value?"PASS ":"FAIL ")+message);
        File.WriteAllLines("Documentation/ScriptOrganizationValidation.txt",report);
        if(!value)throw new Exception(message);
    }
    public static void Run()
    {
        report.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Documentation/ScriptOrganizationMoves.json"));
        Check(!Directory.Exists("Assets/Editor"),"Top-level Editor folder removed");
        foreach(var move in manifest.moves)
        {
            Check(!File.Exists(move.source),"Old path removed: "+move.source);
            string path=string.IsNullOrEmpty(move.mergedInto)?move.destination:move.mergedInto;
            Check(File.Exists(path),"Script available in its functional folder: "+path);
            if(string.IsNullOrEmpty(move.mergedInto))
                Check(AssetDatabase.AssetPathToGUID(path)==move.guid,"Script GUID preserved: "+path);
            if(move.editorOnly)
            {
                string text=File.ReadAllText(path).Trim();
                Check(text.StartsWith("#if UNITY_EDITOR") && text.EndsWith("#endif"),"Editor-only compilation guard: "+path);
            }
            else Check(AssetDatabase.LoadAssetAtPath<MonoScript>(path).GetClass()!=null,"Runtime component/type still resolves: "+path);
        }
        var item=ScriptableObject.CreateInstance<ConsumableItemDefinition>();var inspector=Editor.CreateEditor(item);
        Check(inspector is InventoryItemDefinitionEditor,"Inventory custom inspector remains registered");
        Object.DestroyImmediate(inspector);Object.DestroyImmediate(item);
        Check(TypeCache.GetMethodsWithAttribute<MenuItem>().Any(m=>m.DeclaringType==typeof(DevelopmentEconomyTools)),"Development balance menu retained after consolidation");
        Check(TypeCache.GetMethodsWithAttribute<MenuItem>().Any(m=>m.DeclaringType==typeof(ConstructionSceneCapture)),"Explicit construction scene capture menu retained");
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(prefab && prefab.GetComponentsInChildren<Component>(true).All(c=>c!=null),"Prefab has no missing components: "+path);
        }
        CheckPreviews();
        ConstructionPreviewValidation.Run();
        CompilePlayer(manifest);
        Debug.Log("SCRIPT_ORGANIZATION_OK checks="+report.Count);
    }
    static void CheckPreviews()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        var red=new Material(material);
        try
        {
            foreach(var entry in catalog.entries.Where(e=>e.prefab))
            {
                var preview=MeshPreview.Create(entry.prefab,new Vector3(3,5,7),Quaternion.Euler(0,90,0),entry.prefab.transform.localScale,material,"Visual preview check");
                var root=preview.Root;
                try
                {
                    Check(root.GetComponentsInChildren<Collider>(true).Length==0 && root.GetComponentsInChildren<MonoBehaviour>(true).Length==0 && root.GetComponentsInChildren<Light>(true).Length==0,"Preview excludes physics/scripts/lights: "+entry.id);
                    Check(root.GetComponentsInChildren<MeshRenderer>(true).Length>0,"Preview contains geometry: "+entry.id);
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                        Check(renderer.sharedMaterials.Length==Mathf.Max(1,mesh.subMeshCount) && renderer.sharedMaterials.All(m=>m==material) && renderer.shadowCastingMode==ShadowCastingMode.Off && renderer.gameObject.layer==2,"Preview materials and render state: "+entry.id+"/"+renderer.name);
                    }
                    preview.SetMaterial(red);
                    Check(root.GetComponentsInChildren<MeshRenderer>(true).All(r=>r.sharedMaterials.All(m=>m==red)),"Preview tint changes for every submesh: "+entry.id);
                    // Warm up before measuring the stable per-frame path.
                    for(int i=0;i<10;i++)preview.SetMaterial(red);
                    long before=GC.GetAllocatedBytesForCurrentThread();
                    for(int i=0;i<1000;i++)preview.SetMaterial(red);
                    Check(GC.GetAllocatedBytesForCurrentThread()==before,"Repeated unchanged preview tint allocates zero managed bytes: "+entry.id);
                }
                finally{preview.Dispose();}
                Check(!root,"Preview cleanup removes its generated hierarchy: "+entry.id);
            }
        }
        finally{Object.DestroyImmediate(material);Object.DestroyImmediate(red);}
    }
    static void CompilePlayer(Manifest manifest)
    {
        string output="Documentation/ScriptOrganizationPlayerCompilation";Directory.CreateDirectory(output);
        var result=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
        {target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone,options=ScriptCompilationOptions.None},output);
        try
        {
            Check(result.assemblies!=null && result.assemblies.Count>0,"Windows player script compilation succeeds");
            var path=Directory.GetFiles(output,"Assembly-CSharp.dll",SearchOption.AllDirectories).Single();
            var assembly=System.Reflection.Assembly.ReflectionOnlyLoad(File.ReadAllBytes(path));
            Check(!assembly.GetReferencedAssemblies().Any(a=>a.Name.StartsWith("UnityEditor")),"Player assembly has no UnityEditor references");
            foreach(var move in manifest.moves.Where(m=>m.editorOnly))
            {
                string name=Path.GetFileNameWithoutExtension(move.source);
                if(name=="SimpleSeaSceneCreator")name="JuegoCriminal.EditorTools."+name;
                Check(assembly.GetType(name,false)==null,"Development tool absent from player assembly: "+name);
            }
        }
        finally{result.typeDB?.Dispose();}
    }
}
#endif
