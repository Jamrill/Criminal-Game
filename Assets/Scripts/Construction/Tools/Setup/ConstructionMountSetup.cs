#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ConstructionMountSetup
{
    public static void Inspect()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var lines=new List<string>();
        foreach(var e in catalog.entries)
            lines.Add(e.title+" root="+e.prefab.transform.position.ToString("F4")+" OLD="+e.bounds+" LOCAL="+BuildGeometry.Bounds(e.prefab));
        foreach(var root in scene.GetRootGameObjects())foreach(var marker in root.GetComponentsInChildren<PlacedBuildObject>(true))
        {
            var entry=catalog.Find(marker.definitionId);
            if(entry==null || !(entry.prefab.name=="Door" || entry.prefab.name.StartsWith("Light_point")))continue;
            lines.Add("SCENE "+marker.name+" position="+marker.transform.position.ToString("F4")+" rotation="+marker.transform.eulerAngles);
            foreach(var r in marker.GetComponentsInChildren<MeshRenderer>(true)) lines.Add("  "+r.name+" meshBounds="+r.bounds);
            foreach(var light in marker.GetComponentsInChildren<Light>(true)) lines.Add("  LIGHT "+light.name+" pos="+light.transform.position+" direction="+light.transform.forward);
        }
        File.WriteAllLines("Documentation/ConstructionMountInspection.txt",lines);
        Debug.Log("CONSTRUCTION_MOUNT_INSPECTION_OK");
    }
    static Bounds BoxBounds(BoxCollider box,Transform root)
    {
        var matrix=Matrix4x4.Rotate(Quaternion.Inverse(root.rotation))*Matrix4x4.Translate(-root.position)*box.transform.localToWorldMatrix;
        var b=new Bounds(matrix.MultiplyPoint3x4(box.center),Vector3.zero);
        foreach(float x in new[]{-.5f,.5f})foreach(float y in new[]{-.5f,.5f})foreach(float z in new[]{-.5f,.5f})
            b.Encapsulate(matrix.MultiplyPoint3x4(box.center+Vector3.Scale(box.size,new Vector3(x,y,z))));
        return b;
    }
    public static void Apply()
    {
        string backup="Documentation/ConstructionMountBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);File.Copy(ConstructionSetup.CatalogPath,backup+"/ConstructionCatalog.asset");
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var report=new List<string>{"Backup="+backup};
        foreach(var entry in catalog.entries)
        {
            entry.bounds=BuildGeometry.Bounds(entry.prefab);
            string name=entry.prefab.name;
            entry.mount=name=="Door" ? BuildMount.Doorway : name.StartsWith("Light_point",StringComparison.OrdinalIgnoreCase) ? BuildMount.Ceiling : BuildMount.Ground;
            if(name.StartsWith("Wall_door_hole",StringComparison.OrdinalIgnoreCase))
            {
                var boxes=entry.prefab.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.enabled&&!c.isTrigger)
                    .Select(c=>BoxBounds(c,entry.prefab.transform)).OrderBy(b=>b.center.x).ToArray();
                if(boxes.Length!=3)throw new Exception("Inspect doorway colliders: "+name);
                var left=boxes[0];var right=boxes[2];var top=boxes[1];
                var min=new Vector3(left.max.x,Mathf.Max(left.min.y,right.min.y),Mathf.Max(left.min.z,right.min.z));
                var max=new Vector3(right.min.x,top.min.y,Mathf.Min(left.max.z,right.max.z));
                if(max.x<=min.x || max.y<=min.y || max.z<=min.z)throw new Exception("Invalid opening "+name);
                entry.doorway=new Bounds((min+max)*.5f,max-min);
            }
            report.Add(name+" | "+entry.mount+" | bounds="+entry.bounds+" | doorway="+entry.doorway);
        }
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        File.WriteAllLines("Documentation/ConstructionMountSetup.txt",report);
        Debug.Log("CONSTRUCTION_MOUNT_SETUP_OK");
    }
}
#endif
