using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class WallJoinRevision
{
    static Bounds FilterBounds(MeshFilter filter,Transform root)
    {
        var matrix=Matrix4x4.Rotate(Quaternion.Inverse(root.rotation))*Matrix4x4.Translate(-root.position)*filter.transform.localToWorldMatrix;
        var b=filter.sharedMesh.bounds;var result=new Bounds();bool first=true;
        foreach(float x in new[]{b.min.x,b.max.x})foreach(float y in new[]{b.min.y,b.max.y})foreach(float z in new[]{b.min.z,b.max.z})
        {var p=matrix.MultiplyPoint3x4(new Vector3(x,y,z));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}
        return result;
    }
    public static void ApplyFrameFix()
    {
        const string catalogPath="Assets/ConstructionCatalog.asset",meshPath="Assets/Meshes/Construction/GlassDoorFrame_WallDepth.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(catalogPath);
        string backup="Documentation/WallJoinBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);File.Copy(catalogPath,backup+"/ConstructionCatalog.asset");
        if(!AssetDatabase.IsValidFolder("Assets/Meshes"))AssetDatabase.CreateFolder("Assets","Meshes");
        if(!AssetDatabase.IsValidFolder("Assets/Meshes/Construction"))AssetDatabase.CreateFolder("Assets/Meshes","Construction");
        var report=new List<string>{"Backup="+backup};
        foreach(var entry in catalog.entries.Where(e=>e.prefab && e.prefab.name.StartsWith("Glass_Wall_door",StringComparison.OrdinalIgnoreCase)))
        {
            string path=AssetDatabase.GetAssetPath(entry.prefab);File.Copy(path,backup+"/"+Path.GetFileName(path));
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var transforms=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
                var filter=root.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name.Equals("Glass_door",StringComparison.OrdinalIgnoreCase));
                float depth=FilterBounds(filter,root.transform).size.z;
                if(depth>.501f)
                {
                    float factor=.5f/depth;int axis=0;float best=0;
                    for(int i=0;i<3;i++){var direction=Vector3.zero;direction[i]=1;float dot=Mathf.Abs(Vector3.Dot(filter.transform.TransformDirection(direction),root.transform.forward));if(dot>best){best=dot;axis=i;}}
                    if(best<.999f)throw new Exception("Door frame depth is not axis-aligned");
                    var source=filter.sharedMesh;var mesh=Object.Instantiate(source);mesh.name="GlassDoorFrame_WallDepth";
                    float centre=mesh.bounds.center[axis];var vertices=mesh.vertices;
                    for(int i=0;i<vertices.Length;i++){var p=vertices[i];p[axis]=centre+(p[axis]-centre)*factor;vertices[i]=p;}mesh.vertices=vertices;
                    var normals=mesh.normals;for(int i=0;i<normals.Length;i++){var n=normals[i];n[axis]/=factor;normals[i]=n.normalized;}if(normals.Length>0)mesh.normals=normals;
                    var tangents=mesh.tangents;for(int i=0;i<tangents.Length;i++){var t=tangents[i];var xyz=new Vector3(t.x,t.y,t.z);xyz[axis]*=factor;xyz.Normalize();tangents[i]=new Vector4(xyz.x,xyz.y,xyz.z,t.w);}if(tangents.Length>0)mesh.tangents=tangents;
                    mesh.RecalculateBounds();var stored=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(!stored){AssetDatabase.CreateAsset(mesh,meshPath);stored=mesh;}else{EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(stored);}
                    filter.sharedMesh=stored;
                    foreach(var box in filter.GetComponents<BoxCollider>())
                    {
                        var c=box.center;var s=box.size;c[axis]=centre+(c[axis]-centre)*factor;s[axis]*=factor;box.center=c;box.size=s;
                    }
                }
                foreach(var pair in transforms)
                    if(pair.Key.localPosition!=pair.Value.localPosition || Quaternion.Angle(pair.Key.localRotation,pair.Value.localRotation)>.001f || pair.Key.localScale!=pair.Value.localScale)throw new Exception("Unexpected door or hinge transform change");
                PrefabUtility.SaveAsPrefabAsset(root,path);
                entry.bounds=BuildGeometry.Bounds(root);
                report.Add(entry.prefab.name+": frame depth "+depth+" -> "+FilterBounds(filter,root.transform).size.z+"; total wall depth="+entry.bounds.size.z+"; transforms/leaf/hinge unchanged");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();File.WriteAllLines("Documentation/WallJoinSetup.txt",report);Debug.Log("WALL_FRAME_FIX_OK");
    }
    public static void Inspect()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);var report=new List<string>();
        foreach(var entry in catalog.entries.Where(e=>e.prefab && (e.prefab.name=="Wall_complete" || e.prefab.name=="Corner_Wall" || e.prefab.name=="T_wall_complete" || e.prefab.name=="+_wall_complete" || e.prefab.name.StartsWith("Glass_Wall_door",StringComparison.OrdinalIgnoreCase))))
        {
            report.Add(entry.prefab.name+" catalog bounds="+entry.bounds);
            var root=Object.Instantiate(entry.prefab);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);Physics.SyncTransforms();
            try
            {
                var wall=root.GetComponent<WallTopology>();
                foreach(var span in WallGeometry.World(wall))report.Add(" SPAN "+span.a.ToString("F4")+" -> "+span.b.ToString("F4"));
                foreach(var box in root.GetComponentsInChildren<BoxCollider>(true))report.Add(" BOX "+AnimationUtility.CalculateTransformPath(box.transform,root.transform)+" bounds="+box.bounds+" rotation="+box.transform.eulerAngles+" local center="+box.center+" local size="+box.size);
                foreach(var mesh in root.GetComponentsInChildren<MeshRenderer>(true))report.Add(" MESH "+AnimationUtility.CalculateTransformPath(mesh.transform,root.transform)+" bounds="+mesh.bounds);
            }
            finally{Object.DestroyImmediate(root);}
        }
        File.WriteAllLines("Documentation/WallJoinInspection.txt",report);Debug.Log("WALL_JOIN_INSPECTION_OK");
    }
}
