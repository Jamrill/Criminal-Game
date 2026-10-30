using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class WallRevisionInspection
{
    public static void Run()
    {
        var log=new List<string>();
        foreach(var path in AssetDatabase.FindAssets("t:Model",new[]{"Assets/Original_assets/Walls"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.Contains("/Old/")))
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            log.Add(path+" scale="+root.transform.localScale);
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var m=f.sharedMesh;
                var pts=m.vertices.Select(v=>root.transform.InverseTransformPoint(f.transform.TransformPoint(v))).ToArray();
                var b=new Bounds(pts[0],Vector3.zero);foreach(var p in pts)b.Encapsulate(p);
                log.Add("  "+f.name+" mesh="+m.name+" bounds="+b+" materials="+string.Join(",",f.GetComponent<Renderer>().sharedMaterials.Select(x=>x ? x.name : "NULL")));
                log.Add("  X="+string.Join(",",pts.Select(v=>System.Math.Round(v.x,3)).Distinct().OrderBy(x=>x))+" Y="+string.Join(",",pts.Select(v=>System.Math.Round(v.y,3)).Distinct().OrderBy(x=>x))+" Z="+string.Join(",",pts.Select(v=>System.Math.Round(v.z,3)).Distinct().OrderBy(x=>x)));
            }
        }
        File.WriteAllLines("Documentation/WallRevisionInspection.txt",log);
    }
}
