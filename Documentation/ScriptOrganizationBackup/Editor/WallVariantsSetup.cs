using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JuegoCriminal.Construction;
using Object=UnityEngine.Object;

public static class WallVariantsSetup
{
    static readonly string[] Folders={"Completes","Semincompletes","Incompletes"};
    static readonly string[] Suffix={"complete","semincomplete","incomplete"};
    static string Key(string name) => Regex.Replace(name.ToLowerInvariant().Replace(' ','_'),"_(semincomplete|incomplete|complete)$","");
    static MeshFilter Main(GameObject go) => go.GetComponentsInChildren<MeshFilter>(true).First(f=>f.sharedMesh && f.sharedMesh.name.ToLowerInvariant().Contains("wall"));
    public static Matrix4x4 CornerAlignment(Vector3[] points)
    {
        return points.Min(p=>p.x)<-.1f && points.Max(p=>p.x)<4.1f
            ? Matrix4x4.TRS(new Vector3(4,0,0),Quaternion.Euler(0,-90,0),Vector3.one)
            : Matrix4x4.identity;
    }
    static void Folder(string path) { if(AssetDatabase.IsValidFolder(path))return; Folder(Path.GetDirectoryName(path).Replace('\\','/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path)); }
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salir de Play.");
        Folder("Assets/Construction/WallMeshes"); Folder("Assets/Prefabs/Construction/Incompletes");
        var report=new List<string>();
        var paths=AssetDatabase.FindAssets("t:Model",new[]{"Assets/Original_assets/Walls"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.Contains("/Old/")).ToArray();
        var groups=paths.GroupBy(p=>Key(Path.GetFileNameWithoutExtension(p))).ToArray();
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wall_Building.mat");
        var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Glass.mat");
        var border=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bordillo.mat");
        var prefabPaths=new List<string>();
        foreach(var group in groups)
        {
            var models=new GameObject[3];
            for(int v=0;v<3;v++)
            {
                var sourcePath=group.Single(p=>Path.GetFileNameWithoutExtension(p).EndsWith("_"+Suffix[v],StringComparison.OrdinalIgnoreCase));
                models[v]=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            }
            var existing=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Construction"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>Key(Path.GetFileNameWithoutExtension(p))==group.Key).ToArray();
            for(int v=0;v<3;v++)
            {
                string folder="Assets/Prefabs/Construction/"+Folders[v]; Folder(folder);
                string path=existing.FirstOrDefault(p=>p.StartsWith(folder+"/",StringComparison.OrdinalIgnoreCase));
                if(path==null)
                {
                    path=folder+"/"+models[v].name+".prefab";
                    if(existing.Length>0) AssetDatabase.CopyAsset(existing[0],path);
                    else
                    {
                        var go=new GameObject(models[v].name);
                        var visual=new GameObject("FixedVisual",typeof(MeshFilter),typeof(MeshRenderer)); visual.transform.SetParent(go.transform,false);
                        // The only family without a pre-existing template is the cross wall.
                        var first=go.AddComponent<BoxCollider>(); first.center=new Vector3(4,3.5f,0);first.size=new Vector3(8,7,.5f);
                        var second=go.AddComponent<BoxCollider>();second.center=new Vector3(4,3.5f,0);second.size=new Vector3(.5f,7,8);
                        PrefabUtility.SaveAsPrefabAsset(go,path); Object.DestroyImmediate(go);
                    }
                }
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.name=Path.GetFileNameWithoutExtension(path);
                    var target=root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>f.sharedMesh && f.sharedMesh.name.ToLowerInvariant().Contains("wall")) ?? root.GetComponentInChildren<MeshFilter>();
                    var renderer=target.GetComponent<MeshRenderer>();
                    var topology=root.GetComponent<WallTopology>() ?? root.AddComponent<WallTopology>();
                    topology.wall=target; topology.variants=new Mesh[3]; topology.variantMaterials=new WallTopology.Materials[3];
                    var toLocal=target.transform.worldToLocalMatrix*root.transform.localToWorldMatrix;
                    for(int state=0;state<3;state++)
                    {
                        var source=Main(models[state]);
                        var conversion=toLocal*source.transform.localToWorldMatrix;
                        if(group.Key=="corner_wall" && state==2)
                            conversion=toLocal*CornerAlignment(source.sharedMesh.vertices.Select(source.transform.localToWorldMatrix.MultiplyPoint3x4).ToArray())*source.transform.localToWorldMatrix;
                        var mesh=Object.Instantiate(source.sharedMesh); mesh.name=Path.GetFileNameWithoutExtension(path)+"_"+Suffix[state];
                        mesh.vertices=source.sharedMesh.vertices.Select(conversion.MultiplyPoint3x4).ToArray();
                        if(mesh.normals.Length>0) mesh.normals=source.sharedMesh.normals.Select(n=>conversion.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                        mesh.RecalculateBounds(); mesh.RecalculateTangents();
                        string meshPath="Assets/Construction/WallMeshes/"+mesh.name+".asset";
                        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if(saved) { EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                        else { saved=mesh; AssetDatabase.CreateAsset(saved,meshPath); }
                        topology.variants[state]=saved;
                        topology.variantMaterials[state]=new WallTopology.Materials{values=source.GetComponent<Renderer>().sharedMaterials.Select(m=>m && m.name.ToLowerInvariant().Contains("glass") ? glass : m && m.name.ToLowerInvariant().Contains("bordillo") ? border : material).ToArray()};
                    }
                    target.sharedMesh=topology.variants[v]; renderer.sharedMaterials=topology.variantMaterials[v].values; topology.currentVariant=v;
                    if(group.Key.Contains("diagonal"))
                    {
                        var box=root.GetComponentInChildren<BoxCollider>();
                        if(box)
                        {
                            var points=target.sharedMesh.vertices.Select(p=>box.transform.InverseTransformPoint(target.transform.TransformPoint(p))).ToArray();
                            var bounds=new Bounds(points[0],Vector3.zero); foreach(var p in points) bounds.Encapsulate(p);
                            box.center=bounds.center; box.size=bounds.size;
                        }
                    }
                    bool corner=group.Key.Contains("corner") || group.Key.Contains("diagonal");
                    bool tee=group.Key.StartsWith("t_"); bool cross=group.Key.StartsWith("+"); bool wide=group.Key=="glass_wall_door";
                    topology.connections=cross ? new[]{Vector3.zero,new Vector3(8,0,0),new Vector3(4,0,-4),new Vector3(4,0,4)} : tee ? new[]{Vector3.zero,new Vector3(4,0,-4),new Vector3(4,0,4)} : new[]{Vector3.zero,new Vector3(corner?4:wide?12:4,0,corner?4:0)};
                    topology.bottomCenter=new Vector3(cross?4:wide?6:2,0,corner?2:0); topology.height=7;
                    var surface=root.GetComponent<BuildSurface>() ?? root.AddComponent<BuildSurface>();
                    surface.slots=renderer.sharedMaterials.Select((m,i)=>new{m,i}).Where(x=>x.m==material).Select(x=>new BuildSurface.Slot{renderer=renderer,index=x.i}).ToArray();
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                    prefabPaths.Add(path); report.Add(path+" updated; boxes="+root.GetComponentsInChildren<BoxCollider>(true).Length);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var entries=catalog.entries.ToList();
        foreach(var path in prefabPaths)
        {
            string id=AssetDatabase.AssetPathToGUID(path); var entry=entries.FirstOrDefault(e=>e.id==id);
            if(entry==null) { entry=new BuildEntry{id=id}; entries.Add(entry); }
            entry.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path); entry.title=entry.prefab.name; entry.category=BuildCategory.Walls;
            entry.hiddenInMenu=!path.Contains("/Completes/");
            bool first=true; Bounds bounds=default;
            foreach(var f in entry.prefab.GetComponentsInChildren<MeshFilter>(true)) if(f.sharedMesh)
                foreach(var vertex in f.sharedMesh.vertices)
                {
                    var p=Quaternion.Inverse(entry.prefab.transform.rotation)*(f.transform.TransformPoint(vertex)-entry.prefab.transform.position);
                    if(first) { bounds=new Bounds(p,Vector3.zero); first=false; } else bounds.Encapsulate(p);
                }
            entry.bounds=bounds;
        }
        catalog.entries=entries.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        File.WriteAllLines("Documentation/WallVariantsSetup.txt",report);
        Debug.Log("WALL_VARIANTS_SETUP_OK prefabs="+prefabPaths.Count);
    }
}
