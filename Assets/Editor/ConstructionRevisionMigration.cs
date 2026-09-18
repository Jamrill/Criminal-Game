using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ConstructionRevisionMigration
{
    const string Root = "Assets/Prefabs/Construction/";
    const string Models = "Assets/Original_assets/Walls/";
    const string BackupRoot = "Documentation/ConstructionRevisionBackup/";
    const string Done = "Documentation/ConstructionRevision.done";
    static readonly string[] Names = { "Corner_Wall", "Diagonal_Wall", "Glass_wall", "Wall", "Wall_door_hole", "Window_Wall" };
    static readonly string[] Complete = { "Corner_Wall_complete", "Diagonal_Wall_complete", "Glass_Wall_complete", "Wall_Complete", "Wall_door_hole_complete", "Window_wall_complete" };
    static readonly string[] Incomplete = { "Corner_Wall_incomplete", "Diagonal_Wall_incomplete", "Glass_Wall_incomplete", "Wall_incomplete", "Wall_door_hole_incomplete", "Window_wall_incomplete" };
    static readonly List<string> Log = new();

    [MenuItem("Tools/Criminal Game/Migrar Construction Complete e Incomplete")]
    public static void Run()
    {
        if (File.Exists(Done)) { Debug.Log("Construction: migracion ya aplicada."); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)
            throw new InvalidOperationException("Salir de Play y Prefab Mode primero.");
        for (int i=0;i<SceneManager.sceneCount;i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Guardar las escenas antes de migrar.");
        Log.Clear();
        try
        {
            for (int i=0;i<Names.Length;i++)
            {
                foreach (string path in new[]{Models+"Complete/"+Complete[i]+".fbx",Models+"Incomplete/"+Incomplete[i]+".fbx"})
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) throw new Exception("FBX no importado: "+path);
                if (i<5 && !File.Exists(Root+"Completes/"+Names[i]+".prefab")) throw new Exception("Prefab inexistente: "+Names[i]);
                if (File.Exists(Root+"Incompletes/"+Names[i]+".prefab")) throw new Exception("Destino ya existe; revisar antes de sobrescribir: "+Names[i]);
            }
            foreach (string file in Directory.GetFiles(Root,"*.prefab",SearchOption.AllDirectories)) Backup(file.Replace('\\','/'));
            foreach (string scene in Directory.GetFiles("Assets","*.unity",SearchOption.AllDirectories)) Backup(scene.Replace('\\','/'));
            var guidMap=new Dictionary<string,string>();
            for (int i=0;i<Names.Length;i++)
            {
                string complete=Root+"Completes/"+Names[i]+".prefab";
                if (i==5)
                {
                    File.Copy(Root+"Completes/Wall.prefab",complete);
                    AssetDatabase.ImportAsset(complete,ImportAssetOptions.ForceSynchronousImport);
                }
                ReplaceVisual(complete,Models+"Complete/"+Complete[i]+".fbx",Names[i]);
                string incomplete=Root+"Incompletes/"+Names[i]+".prefab";
                // Copy serialized IDs, not just objects: root scene overrides and
                // external references remain valid when swapping the prefab GUID.
                File.Copy(complete,incomplete);
                AssetDatabase.ImportAsset(incomplete,ImportAssetOptions.ForceSynchronousImport);
                ReplaceVisual(incomplete,Models+"Incomplete/"+Incomplete[i]+".fbx",Names[i]);
                guidMap.Add(AssetDatabase.AssetPathToGUID(complete),AssetDatabase.AssetPathToGUID(incomplete));
                CheckRootIds(complete,incomplete);
            }
            AssetDatabase.SaveAssets();
            foreach (string raw in Directory.GetFiles("Assets","*.unity",SearchOption.AllDirectories))
            {
                string path=raw.Replace('\\','/');string before=File.ReadAllText(path),after=before;
                int instances=0;
                foreach(var pair in guidMap)
                {
                    instances+=Regex.Matches(before,@"m_SourcePrefab: \{fileID: 100100000, guid: "+pair.Key).Count;
                    after=after.Replace("guid: "+pair.Key,"guid: "+pair.Value);
                }
                if (after==before) continue;
                File.WriteAllText(path,after);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                Log.Add(path+": "+instances+" instancias ahora Incompletes; IDs y overrides conservados.");
            }
            Validate();
            File.WriteAllText(Done,DateTime.Now.ToString("O"));
            Log.Add("SUCCESS. Old conservado. Seis completos y seis incompletos.");
        }
        catch(Exception ex) { Log.Add("ERROR: "+ex); throw; }
        finally { File.WriteAllLines("Documentation/ConstructionRevision.report.txt",Log); }
    }

    static void Backup(string path)
    {
        string destination=BackupRoot+path;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (!File.Exists(destination)) File.Copy(path,destination);
        if(File.Exists(path+".meta") && !File.Exists(destination+".meta")) File.Copy(path+".meta",destination+".meta");
    }

    static Vector3[] Points(GameObject root,MeshFilter filter) => filter.sharedMesh.vertices
        .Select(v=>root.transform.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
    static Bounds BoundsOf(IEnumerable<Vector3> points)
    {
        var all=points.ToArray();if(all.Length==0) throw new Exception("Malla vacia");
        var b=new Bounds(all[0],Vector3.zero);foreach(var v in all)b.Encapsulate(v);return b;
    }
    static Bounds VisualBounds(GameObject root) => BoundsOf(root.GetComponentsInChildren<MeshFilter>(true).SelectMany(f=>Points(root,f)));

    static void ReplaceVisual(string path,string model,string name)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            Bounds old=VisualBounds(root);
            foreach(var child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach(var collider in root.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            root.name=name;
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model),root.scene);
            visual.name="FixedVisual";
            visual.transform.SetParent(root.transform,false);
            Bounds fresh=VisualBounds(root);
            visual.transform.localPosition+=old.min-fresh.min;
            foreach(var c in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach(var a in visual.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            Bounds b=VisualBounds(root);
            if(Mathf.Abs(b.size.y-7f)>.02f) throw new Exception(path+" altura inesperada: "+b.size);
            var colliderRoot=new GameObject("Collision");colliderRoot.transform.SetParent(root.transform,false);
            if(name=="Corner_Wall") Corner(root,colliderRoot,b);
            else if(name=="Diagonal_Wall") Diagonal(root,colliderRoot);
            else if(name=="Wall_door_hole" || name=="Window_Wall")
            {
                foreach(var f in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(name=="Wall_door_hole" || f.sharedMesh.name.ToLowerInvariant().Contains("wall")) Opening(root,colliderRoot,f);
                    else AddBox(colliderRoot,BoundsOf(Points(root,f))); // Window frame + glass.
                }
            }
            else AddBox(colliderRoot,b);
            PrefabUtility.SaveAsPrefabAsset(root,path);
            Log.Add(path+" -> "+model+"; bounds="+b+"; BoxColliders="+root.GetComponentsInChildren<BoxCollider>(true).Length);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void AddBox(GameObject target,Bounds b)
    {
        var c=target.AddComponent<BoxCollider>();c.center=b.center;c.size=b.size;
        if(c.size.x<.001f || c.size.y<.001f || c.size.z<.001f) throw new Exception("Collider degenerado");
    }
    static float[] Coordinates(Vector3[] points,int axis) => points.Select(p=>(float)Math.Round(p[axis],4)).Distinct().OrderBy(v=>v).ToArray();
    static void Corner(GameObject root,GameObject target,Bounds b)
    {
        var points=root.GetComponentsInChildren<MeshFilter>().SelectMany(f=>Points(root,f)).ToArray();
        var x=Coordinates(points,0);var z=Coordinates(points,2);
        bool highX=x.Count(v=>v>b.max.x-.6f)>x.Count(v=>v<b.min.x+.6f);
        bool highZ=z.Count(v=>v>b.max.z-.6f)>z.Count(v=>v<b.min.z+.6f);
        float thickX=highX?b.max.x-x.First(v=>v>b.max.x-.6f):x.Last(v=>v<b.min.x+.6f)-b.min.x;
        float thickZ=highZ?b.max.z-z.First(v=>v>b.max.z-.6f):z.Last(v=>v<b.min.z+.6f)-b.min.z;
        AddBox(target,new Bounds(new Vector3(highX?b.max.x-thickX/2:b.min.x+thickX/2,b.center.y,b.center.z),new Vector3(thickX,b.size.y,b.size.z)));
        AddBox(target,new Bounds(new Vector3(b.center.x,b.center.y,highZ?b.max.z-thickZ/2:b.min.z+thickZ/2),new Vector3(b.size.x,b.size.y,thickZ)));
    }
    static void Diagonal(GameObject root,GameObject target)
    {
        var points=root.GetComponentsInChildren<MeshFilter>().SelectMany(f=>Points(root,f)).ToArray();
        float area=float.PositiveInfinity;Bounds best=default;Quaternion rotation=Quaternion.identity;
        foreach(var a in points) foreach(var b in points)
        {
            Vector3 d=b-a;if(d.x*d.x+d.z*d.z<.01f) continue;
            var q=Quaternion.Euler(0,Mathf.Atan2(-d.z,d.x)*Mathf.Rad2Deg,0);
            Bounds bounds=BoundsOf(points.Select(v=>Quaternion.Inverse(q)*v));
            if(bounds.size.x*bounds.size.z>=area)continue;
            area=bounds.size.x*bounds.size.z;best=bounds;rotation=q;
        }
        target.transform.localRotation=rotation;AddBox(target,best);
    }
    static bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
    {
        float Cross(Vector2 u,Vector2 v)=>u.x*v.y-u.y*v.x;
        float area=Cross(b-a,c-a);if(Mathf.Abs(area)<.00001f)return false;
        float u=Cross(b-p,c-p)/area,v=Cross(c-p,a-p)/area,w=1-u-v;
        return u>=-.00001f && v>=-.00001f && w>=-.00001f;
    }
    static void Opening(GameObject root,GameObject target,MeshFilter mesh)
    {
        var p=Points(root,mesh);var xs=Coordinates(p,0);var ys=Coordinates(p,1);
        var triangles=mesh.sharedMesh.triangles;var b=BoundsOf(p);
        for(int y=0;y<ys.Length-1;y++)
        {
            int start=-1;
            for(int x=0;x<xs.Length;x++)
            {
                bool solid=false;
                if(x<xs.Length-1)
                {
                    var sample=new Vector2((xs[x]+xs[x+1])/2,(ys[y]+ys[y+1])/2);
                    for(int t=0;t<triangles.Length;t+=3)
                    {
                        var a=p[triangles[t]];var c=p[triangles[t+1]];var d=p[triangles[t+2]];
                        if(Inside(sample,new Vector2(a.x,a.y),new Vector2(c.x,c.y),new Vector2(d.x,d.y))) { solid=true;break; }
                    }
                }
                if(solid && start<0)start=x;
                if(!solid && start>=0)
                {
                    AddBox(target,new Bounds(new Vector3((xs[start]+xs[x])/2,(ys[y]+ys[y+1])/2,b.center.z),new Vector3(xs[x]-xs[start],ys[y+1]-ys[y],b.size.z)));
                    start=-1;
                }
            }
        }
    }

    static void CheckRootIds(string first,string second)
    {
        var a=AssetDatabase.LoadAssetAtPath<GameObject>(first);
        var b=AssetDatabase.LoadAssetAtPath<GameObject>(second);
        foreach(var pair in new[]{new Object[]{a,b},new Object[]{a.transform,b.transform}})
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair[0],out string guidA,out long idA);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair[1],out string guidB,out long idB);
            if(idA!=idB)
                throw new Exception("Root ID cambiado: "+second);
        }
    }

    public static void RepairOpeningColliders()
    {
        Log.Clear();
        foreach(string folder in new[]{"Completes","Incompletes"})
        foreach(string name in new[]{"Wall_door_hole","Window_Wall"})
        {
            string path=Root+folder+"/"+name+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var collision=root.transform.Find("Collision");
                foreach(var collider in collision.GetComponents<Collider>())Object.DestroyImmediate(collider);
                foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(name=="Wall_door_hole" || f.sharedMesh.name.ToLowerInvariant().Contains("wall"))Opening(root,collision.gameObject,f);
                    else AddBox(collision.gameObject,BoundsOf(Points(root,f)));
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Log.Add(path+": BoxColliders="+root.GetComponentsInChildren<BoxCollider>(true).Length);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        Validate();
        File.WriteAllLines("Documentation/ConstructionRevision.collider-validation.txt",Log);
    }

    public static void Validate()
    {
        foreach(string folder in new[]{"Completes","Incompletes"})
        {
            if(Directory.GetFiles(Root+folder,"*.prefab").Length!=6)throw new Exception("Cantidad de prefabs incorrecta: "+folder);
            foreach(string name in Names)
            {
                string path=Root+folder+"/"+name+".prefab";
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var deps=AssetDatabase.GetDependencies(path,true);
                if(deps.Any(d=>d.StartsWith(Models+"Old/")))throw new Exception("Dependencia Old: "+path);
                string expected=Models+(folder=="Completes"?"Complete/":"Incomplete/");
                if(!deps.Any(d=>d.StartsWith(expected)&&d.EndsWith(".fbx")))throw new Exception("Modelo incorrecto: "+path);
                if(asset.GetComponentsInChildren<MeshCollider>(true).Length!=0)throw new Exception("MeshCollider restante: "+path);
                if(asset.GetComponentsInChildren<BoxCollider>(true).Length==0)throw new Exception("Sin BoxCollider: "+path);
                if(name=="Wall_door_hole")
                {
                    var bounds=VisualBounds(asset);
                    var sample=new Vector3(bounds.center.x,bounds.min.y+2f,bounds.center.z);
                    foreach(var box in asset.GetComponentsInChildren<BoxCollider>(true))
                    {
                        Vector3 local=box.transform.InverseTransformPoint(asset.transform.TransformPoint(sample));
                        if(new Bounds(box.center,box.size).Contains(local))throw new Exception("Collider obstruye la puerta: "+path);
                    }
                    if(asset.GetComponentsInChildren<BoxCollider>(true).Length!=3)throw new Exception("La puerta debe tener dos jambas y dintel: "+path);
                }
                foreach(var f in asset.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh==null)throw new Exception("Mesh perdido: "+path);
                foreach(var t in asset.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Script perdido: "+path);
            }
        }
        foreach(string path in Directory.GetFiles("Assets","*.unity",SearchOption.AllDirectories))
        {
            string text=File.ReadAllText(path);
            foreach(string name in Names)
                if(text.Contains("guid: "+AssetDatabase.AssetPathToGUID(Root+"Completes/"+name+".prefab")))throw new Exception("Complete sigue en escena: "+path);
        }
        foreach(string path in Directory.GetFiles("Assets/Scenes","*.unity"))
        {
            var scene=EditorSceneManager.OpenPreviewScene(path);
            try
            {
                int count=0;
                foreach(var root in scene.GetRootGameObjects())
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if(PrefabUtility.IsPrefabAssetMissing(t.gameObject)) throw new Exception("Prefab perdido en "+path+": "+t.name);
                    string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                    if(source.StartsWith(Root+"Incompletes/") && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))count++;
                }
                Log.Add("Scene load PASS "+path+": "+count+" raices Incompletes.");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        string[] oldGuids=Directory.GetFiles(Models+"Old","*.fbx").Select(p=>AssetDatabase.AssetPathToGUID(p.Replace('\\','/'))).Where(g=>!string.IsNullOrEmpty(g)).ToArray();
        foreach(string path in Directory.GetFiles("Assets","*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".prefab")||p.EndsWith(".unity")||p.EndsWith(".asset")))
            if(oldGuids.Any(g=>File.ReadAllText(path).Contains("guid: "+g)))throw new Exception("Referencia Old pendiente: "+path);
        Log.Add("VALIDATION PASS: modelos correctos, colliders presentes, sin Old ni Complete en referencias directas de escena.");
    }
}
