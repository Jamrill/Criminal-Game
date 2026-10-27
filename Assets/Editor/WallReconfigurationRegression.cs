using System;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WallReconfigurationRegression
{
    public static void Full(){WallReconfigurationValidation.Run();Run();}
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var report=new System.Collections.Generic.List<string>();
        foreach(var root in scene.GetRootGameObjects())foreach(var item in root.GetComponentsInChildren<PlacedBuildObject>(true))
        {
            var entry=catalog.Find(item.definitionId);var wall=item.GetComponent<WallTopology>();
            if(entry==null || !wall)continue;
            var template=entry.prefab.GetComponent<WallTopology>();
            var actual=WallGeometry.Local(wall);var expected=WallGeometry.Local(template);
            if(actual.Count!=expected.Count || !actual.All(s=>expected.Any(t=>t.Same(s))))throw new Exception("Scene wall geometry mismatch "+item.name);
            report.Add(item.name+" | "+entry.prefab.name+" | spans="+actual.Count);
        }
        File.WriteAllLines("Documentation/WallGraphSceneMetadata.txt",report);
        ConstructionMountValidation.Run();
        ConstructionValueValidation.Run();
        ConstructionPreviewValidation.Run();
        Debug.Log("WALL_GRAPH_REGRESSIONS_OK sceneWalls="+report.Count);
    }
}
