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

public static class SolarOrganizationRevision
{
    const string ScenePath="Assets/Scenes/10_World_City.unity";
    static string Key(string name)=>Regex.Replace(name.ToLowerInvariant(),"_(semincomplete|incomplete|complete)$","");
    static readonly List<string> report=new();
    public static void InspectCorners()
    {
        var lines=new List<string>();
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Construction"}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);if(Key(Path.GetFileNameWithoutExtension(path))!="corner_wall")continue;
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var wall=root.GetComponent<WallTopology>();
            for(int i=0;i<3;i++)
            {
                var points=wall.variants[i].vertices.Select(p=>root.transform.InverseTransformPoint(wall.wall.transform.TransformPoint(p))).ToArray();
                var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);
                lines.Add(path+" variant="+i+" bounds="+b+" X="+string.Join(";",points.Select(p=>Math.Round(p.x,3)).Distinct().OrderBy(x=>x))+" Z="+string.Join(";",points.Select(p=>Math.Round(p.z,3)).Distinct().OrderBy(x=>x)));
            }
            foreach(var box in CornerBoxes(root))lines.Add("derived box "+box);
        }
        File.WriteAllLines("Documentation/CornerGeometryInspection.txt",lines);
    }
    static bool InMesh(Vector3 p,Vector3[] vertices,int[] triangles)
    {
        // Test the complete skin, which retains its end caps, at mid-wall height.
        Vector3 direction=new Vector3(1,0,.01731f).normalized;
        var hits=new List<float>();
        for(int i=0;i<triangles.Length;i+=3)
        {
            Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
            Vector3 edge1=b-a,edge2=c-a,h=Vector3.Cross(direction,edge2);
            float det=Vector3.Dot(edge1,h);if(Mathf.Abs(det)<.000001f)continue;
            Vector3 s=p-a;float u=Vector3.Dot(s,h)/det;if(u<0 || u>1)continue;
            Vector3 q=Vector3.Cross(s,edge1);float v=Vector3.Dot(direction,q)/det;if(v<0 || u+v>1)continue;
            float t=Vector3.Dot(edge2,q)/det;
            if(t>.00001f && !hits.Any(x=>Mathf.Abs(x-t)<.0001f))hits.Add(t);
        }
        return hits.Count%2==1;
    }
    static Bounds[] CornerBoxes(GameObject root)
    {
        var wall=root.GetComponent<WallTopology>(); var mesh=wall.variants[0];
        var points=mesh.vertices.Select(p=>root.transform.InverseTransformPoint(wall.wall.transform.TransformPoint(p))).ToArray();
        var xs=points.Select(p=>(float)Math.Round(p.x,4)).Distinct().OrderBy(x=>x).ToArray();
        var zs=points.Select(p=>(float)Math.Round(p.z,4)).Distinct().OrderBy(x=>x).ToArray();
        float low=points.Min(p=>p.y),high=points.Max(p=>p.y); var boxes=new List<Bounds>();
        for(int z=0;z<zs.Length-1;z++)
        {
            int start=-1;
            for(int x=0;x<xs.Length;x++)
            {
                bool solid=x<xs.Length-1 && InMesh(new Vector3((xs[x]+xs[x+1])*.5f,(low+high)*.5f,(zs[z]+zs[z+1])*.5f),points,mesh.triangles);
                if(solid && start<0)start=x;
                if(!solid && start>=0)
                {
                    var min=new Vector3(xs[start],low,zs[z]);var max=new Vector3(xs[x],high,zs[z+1]);
                    int previous=boxes.FindIndex(b=>Mathf.Abs(b.min.x-min.x)<.0001f && Mathf.Abs(b.max.x-max.x)<.0001f && Mathf.Abs(b.max.z-min.z)<.0001f);
                    if(previous>=0) { var b=boxes[previous];b.SetMinMax(b.min,max);boxes[previous]=b; }
                    else { var b=new Bounds();b.SetMinMax(min,max);boxes.Add(b); }
                    start=-1;
                }
            }
        }
        if(boxes.Count!=2)throw new Exception(root.name+": se esperaban 2 brazos; calculados "+boxes.Count);
        return boxes.ToArray();
    }
    static void FitCorner(GameObject root)
    {
        var geometry=CornerBoxes(root);var boxes=root.GetComponentsInChildren<BoxCollider>(true);
        if(boxes.Length!=2)throw new Exception(root.name+": revisar colliders adicionales antes de sustituir.");
        for(int i=0;i<2;i++)
        {
            report.Add(root.name+" collider BEFORE "+boxes[i].center+" / "+boxes[i].size);
            var b=geometry[i];var matrix=boxes[i].transform.worldToLocalMatrix*root.transform.localToWorldMatrix;
            var local=new Bounds(matrix.MultiplyPoint3x4(b.min),Vector3.zero);
            foreach(float x in new[]{b.min.x,b.max.x})foreach(float y in new[]{b.min.y,b.max.y})foreach(float z in new[]{b.min.z,b.max.z})local.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(x,y,z)));
            boxes[i].center=local.center;boxes[i].size=local.size;
            report.Add(root.name+" collider AFTER "+boxes[i].center+" / "+boxes[i].size);
        }
        var wall=root.GetComponent<WallTopology>();
        foreach(var mesh in wall.variants)foreach(var p in mesh.vertices)
        {
            Vector3 world=wall.wall.transform.TransformPoint(p);
            if(!boxes.Any(c=> {var bounds=new Bounds(c.center,c.size+Vector3.one*.001f);return bounds.Contains(c.transform.InverseTransformPoint(world));}))
                throw new Exception("Vertice sin cubrir en "+root.name+": "+world);
        }
    }
    static void AlignCorner(GameObject root,string backup)
    {
        var wall=root.GetComponent<WallTopology>();var mesh=wall.variants[2];
        var toRoot=root.transform.worldToLocalMatrix*wall.wall.transform.localToWorldMatrix;
        var alignment=WallVariantsSetup.CornerAlignment(mesh.vertices.Select(toRoot.MultiplyPoint3x4).ToArray());
        if(alignment==Matrix4x4.identity)return;
        var path=AssetDatabase.GetAssetPath(mesh);
        if(!path.StartsWith("Assets/Construction/WallMeshes/"))throw new Exception("No modificar malla original: "+path);
        Backup(path,backup);
        var conversion=toRoot.inverse*alignment*toRoot;
        mesh.vertices=mesh.vertices.Select(conversion.MultiplyPoint3x4).ToArray();
        mesh.normals=mesh.normals.Select(n=>conversion.inverse.transpose.MultiplyVector(n).normalized).ToArray();
        mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
        report.Add("ALIGNED generated mesh: "+path);
    }
    static void SheetMaterials(GameObject root)
    {
        var wall=root.GetComponent<WallTopology>(); if(!wall)return;
        var reference=wall.variantMaterials[0].values;
        var glass=reference.First(m=>m.name.Equals("Glass",StringComparison.OrdinalIgnoreCase));
        var frame=reference.First(m=>!m.name.Equals("Glass",StringComparison.OrdinalIgnoreCase));
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Sheet",StringComparison.OrdinalIgnoreCase)))
        {
            if(renderer.sharedMaterials.Length!=2)throw new Exception("Sheet con slots inesperados: "+root.name);
            report.Add(root.name+" / "+renderer.name+": "+string.Join(",",renderer.sharedMaterials.Select(m=>m?AssetDatabase.GetAssetPath(m):"NULL"))+" -> Glass + "+frame.name);
            renderer.sharedMaterials=new[]{glass,frame};
            if(PrefabUtility.IsPartOfPrefabInstance(renderer))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }
    static void Backup(string path,string folder)
    {
        string destination=folder+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(path,destination,false);
        if(File.Exists(path+".meta"))File.Copy(path+".meta",destination+".meta",false);
    }
    public static void PhaseOne() => RunInternal(false);
    public static void Run() => RunInternal(true);
    static void RunInternal(bool finalize)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Salir de Play.");
        report.Clear();
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Construction"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        string backup="Documentation/SolarOrganizationBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Backup(ScenePath,backup);
        foreach(var path in paths.Where(p=>Key(Path.GetFileNameWithoutExtension(p))=="corner_wall" || Key(Path.GetFileNameWithoutExtension(p))=="glass_wall_door"))
        {
            if(!finalize && Key(Path.GetFileNameWithoutExtension(path))=="corner_wall")continue;
            Backup(path,backup);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(Key(Path.GetFileNameWithoutExtension(path))=="corner_wall") { AlignCorner(root,backup);FitCorner(root); } else SheetMaterials(root);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var solar=scene.GetRootGameObjects().Single(g=>g.name=="Solar");
        var incomplete=paths.Where(p=>p.Contains("/Incompletes/")).ToDictionary(p=>Key(Path.GetFileNameWithoutExtension(p)),p=>AssetDatabase.LoadAssetAtPath<GameObject>(p));
        var wallRoots=finalize ? scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WallTopology>(true)).Select(t=>t.gameObject).ToArray() : Array.Empty<GameObject>();
        int changed=0,parented=0;
        foreach(var original in wallRoots)
        {
            var go=original;string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            string family=Key(Path.GetFileNameWithoutExtension(source));
            if(!incomplete.TryGetValue(family,out var target))throw new Exception("Sin variante incomplete: "+source);
            string name=go.name;var position=go.transform.position;var rotation=go.transform.rotation;var scale=go.transform.lossyScale;
            if(source!=AssetDatabase.GetAssetPath(target))
            {
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go,target,new PrefabReplacingSettings{objectMatchMode=ObjectMatchMode.ByHierarchy,changeRootNameToAssetName=false},InteractionMode.AutomatedAction);
                changed++;
            }
            go.name=name;
            var topology=go.GetComponent<WallTopology>();
            var sourceTopology=target.GetComponent<WallTopology>();
            string meshPath=AnimationUtility.CalculateTransformPath(sourceTopology.wall.transform,target.transform);
            var meshTransform=string.IsNullOrEmpty(meshPath)?go.transform:go.transform.Find(meshPath);
            if(!meshTransform)
            {
                PrefabUtility.RevertRemovedGameObject(go,sourceTopology.wall.gameObject,InteractionMode.AutomatedAction);
                meshTransform=go.transform.Find(meshPath);
            }
            topology.wall=meshTransform?meshTransform.GetComponent<MeshFilter>():go.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f=>f.sharedMesh && f.sharedMesh.name.ToLowerInvariant().Contains("wall"));
            if(!topology.wall)throw new Exception("No se encuentra malla de pared: "+name+" target="+meshPath+" children="+string.Join(",",go.GetComponentsInChildren<Transform>(true).Select(t=>t.name)));
            topology.variants=sourceTopology.variants;topology.variantMaterials=sourceTopology.variantMaterials;
            topology.automaticVariants=false;topology.SetVariant(2);PrefabUtility.RecordPrefabInstancePropertyModifications(topology);
            PrefabUtility.RecordPrefabInstancePropertyModifications(topology.wall);
            PrefabUtility.RecordPrefabInstancePropertyModifications(topology.wall.GetComponent<Renderer>());
            if(family=="corner_wall") { FitCorner(go);foreach(var box in go.GetComponentsInChildren<BoxCollider>())PrefabUtility.RecordPrefabInstancePropertyModifications(box); }
            if(family=="glass_wall_door")SheetMaterials(go);
            if(Vector3.Distance(position,go.transform.position)>.0001f || Quaternion.Angle(rotation,go.transform.rotation)>.001f || Vector3.Distance(scale,go.transform.lossyScale)>.0001f)throw new Exception("Transform alterado: "+name);
        }
        foreach(var go in scene.GetRootGameObjects())
        {
            if(go==solar)continue;
            string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            bool physical=source.StartsWith("Assets/Prefabs/") && !source.Contains("/UI/") && !source.EndsWith("/Player.prefab");
            if(!physical)continue;
            var before=go.transform.localToWorldMatrix;
            go.transform.SetParent(solar.transform,true);
            var after=go.transform.localToWorldMatrix;
            for(int i=0;i<16;i++)if(Mathf.Abs(before[i]-after[i])>.001f)throw new Exception("Reparent cambio transform: "+go.name);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);report.Add("PARENT Solar / "+go.name);parented++;
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        scene=EditorSceneManager.OpenScene(ScenePath);
        if(finalize)foreach(var wall in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WallTopology>(true)))
            if(wall.automaticVariants || wall.currentVariant!=2 || !PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(wall.gameObject).Contains("/Incompletes/") || wall.wall.sharedMesh!=wall.variants[2])throw new Exception("Variante incorrecta tras guardar: "+wall.name);
        foreach(var root in scene.GetRootGameObjects())report.Add("ROOT remaining: "+root.name+" | "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root));
        report.Add($"SUCCESS walls swapped={changed}; roots parented={parented}; backup={backup}");
        File.WriteAllLines("Documentation/SolarOrganizationRevision.txt",report);Debug.Log(report.Last());
    }
}
