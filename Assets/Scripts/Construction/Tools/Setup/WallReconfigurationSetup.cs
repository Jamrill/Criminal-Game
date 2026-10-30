#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEngine;

public static class WallReconfigurationSetup
{
    public static void ApplyAndValidate()
    {
        string backup="Documentation/WallReconfigurationBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var report=new List<string>{"Backup="+backup};
        foreach(var entry in catalog.entries.Where(e=>e.prefab && e.prefab.GetComponent<WallTopology>()))
        {
            string path=AssetDatabase.GetAssetPath(entry.prefab);
            string copy=backup+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(copy));File.Copy(path,copy);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var wall=root.GetComponent<WallTopology>();
                var spans=WallGeometry.Local(wall);
                wall.segmentPoints=spans.SelectMany(s=>new[]{s.a,s.b}).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,path);
                report.Add(path+" spans="+spans.Count+" | "+string.Join("; ",spans.Select(s=>s.a+" -> "+s.b)));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();File.WriteAllLines("Documentation/WallReconfigurationSetup.txt",report);
        WallReconfigurationValidation.Run();
    }
}
#endif
