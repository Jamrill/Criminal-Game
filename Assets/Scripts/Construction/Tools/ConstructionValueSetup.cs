#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using JuegoCriminal.Construction;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ConstructionValueSetup
{
    const string ScenePath="Assets/Scenes/10_World_City.unity";
    const string FreeMenu="Tools/Criminal Game/Construccion/Costes cero (desarrollo)";
    [MenuItem(FreeMenu)]
    public static void ToggleFree()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        if(!catalog)return;
        SetFreeConstruction(catalog,!catalog.developmentFreeConstruction);
    }
    public static void SetFreeConstruction(BuildCatalog catalog,bool free)
    {
        if(!catalog)return;
        Undo.RecordObject(catalog,"Cambiar costes de desarrollo");
        catalog.developmentFreeConstruction=free;
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        if(EditorApplication.isPlaying)
            foreach(var view in Object.FindObjectsByType<BuildMenuView>(FindObjectsInactive.Include,FindObjectsSortMode.None))view.RefreshCatalogPrices();
        Debug.Log("Costes cero: "+catalog.developmentFreeConstruction+". Los precios reales no se han modificado.");
    }
    [MenuItem("Tools/Criminal Game/Construccion/Restaurar precios reales")]
    public static void RestoreRealPrices()=>SetFreeConstruction(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath),false);
    [MenuItem(FreeMenu,true)] static bool CheckFree()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        Menu.SetChecked(FreeMenu,catalog && catalog.developmentFreeConstruction);
        return catalog;
    }
    public static void Inspect()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var lines=new List<string>();
        foreach(var plot in Object.FindObjectsByType<BuildPlot>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            lines.Add("PLOT "+plot.name+" position="+plot.transform.position.ToString("F3")+" scale="+plot.transform.lossyScale+" bounds="+plot.Volume.bounds+" grid="+plot.Grid.position.ToString("F3"));
            foreach(var root in scene.GetRootGameObjects())
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    {
                        string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                        if(t.IsChildOf(plot.transform) || Path.GetFileNameWithoutExtension(path)=="Floor")
                            lines.Add(path+" | "+t.name+" | "+t.position.ToString("F3")+" | parent="+(t.parent?t.parent.name:"ROOT")+" | rotation="+t.eulerAngles+" | scale="+t.lossyScale);
                    }
        }
        File.WriteAllLines("Documentation/ConstructionValueInspection.txt",lines);
        Debug.Log("CONSTRUCTION_VALUE_INSPECTION_OK");
    }
    static int Price(string name)
    {
        name=name.ToLowerInvariant();
        if(name.Contains("wall_door_hole") || name.Contains("glass_wall_door"))return 200;
        if(name.Contains("window_wall"))return 100;
        if(name.StartsWith("+_wall"))return 150;
        if(name.StartsWith("t_wall"))return 120;
        if(name.Contains("corner_wall"))return 70;
        if(name.Contains("diagonal_wall"))return 50;
        if(name.Contains("glass_wall"))return 90;
        if(name.StartsWith("wall"))return 50;
        switch(name)
        {
            case "floor": return 20;
            case "stairs": return 80;
            case "door": return 60;
            case "new_cheap_3d_printer": return 500;
            case "bed": return 180;
            case "sofa": return 220;
            case "chair": return 35;
            case "table": return 90;
            case "toilet": return 100;
            case "kitchen cabinet": return 160;
            case "counter": return 140;
            case "display_shelving_unit": return 110;
            case "central_display_unit": return 130;
            case "floor_lamp": return 45;
            case "light_point": case "light_point_cold": return 30;
            default: return 50;
        }
    }
    public static void Apply()
    {
        string backup="Documentation/ConstructionValueBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath,backup+"/10_World_City.unity");
        File.Copy(ConstructionSetup.CatalogPath,backup+"/ConstructionCatalog.asset");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var entries=catalog.entries.ToList();
        foreach(string path in new[]{"Assets/Prefabs/Floor.prefab","Assets/Prefabs/Stairs.prefab","Assets/Prefabs/Construction/Door.prefab","Assets/Prefabs/Printers/New_Cheap_3D_Printer.prefab"})
        {
            string id=AssetDatabase.AssetPathToGUID(path);
            if(entries.Any(e=>e.id==id))continue;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!prefab)throw new Exception("Missing prefab "+path);
            var bounds=BuildGeometry.Bounds(prefab);
            entries.Add(new BuildEntry{id=id,title=prefab.name,prefab=prefab,bounds=bounds,category=BuildCategory.Walls,
                hiddenInMenu=path.Contains("/Printers/")});
        }
        catalog.entries=entries.ToArray();
        foreach(var entry in entries) { if(!entry.prefab)throw new Exception("Missing catalog prefab "+entry.id);entry.title=entry.prefab.name;entry.price=Price(entry.prefab.name); }
        // Start with real prices. The explicit Tools toggle enables free testing.
        catalog.developmentFreeConstruction=false;
        EditorUtility.SetDirty(catalog);
        int registered=0, floorsAdded=0;
        foreach(var plot in Object.FindObjectsByType<BuildPlot>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            plot.catalog=catalog;
            floorsAdded+=FillFloor(plot,catalog);
            // Only instance roots: a knob/door embedded in another catalog object
            // must not be counted or sold a second time.
            foreach(Transform child in plot.transform.Cast<Transform>().ToArray())
            {
                var path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                var entry=entries.FirstOrDefault(e=>AssetDatabase.GetAssetPath(e.prefab)==path);
                if(entry==null)continue;
                var marker=child.GetComponent<PlacedBuildObject>();
                if(!marker)
                {
                    marker=child.gameObject.AddComponent<PlacedBuildObject>();
                    marker.persistentId="scene-"+GUID.Generate();marker.finish=-1;registered++;
                }
                marker.definitionId=entry.id;marker.propertyId=plot.propertyId;
                marker.cellCenter=child.position;EditorUtility.SetDirty(marker);
            }
            EditorUtility.SetDirty(plot);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var report=new List<string>{"Backup="+backup,"Registered="+registered,"Base floor tiles added="+floorsAdded};
        report.AddRange(entries.Select(e=>e.title+" = $"+e.price));
        report.AddRange(Object.FindObjectsByType<BuildPlot>(FindObjectsSortMode.None).Select(p=>p.DisplayName+" total=$"+p.TotalValue));
        File.WriteAllLines("Documentation/ConstructionValueSetup.txt",report);
        Debug.Log("CONSTRUCTION_VALUE_SETUP_OK floors="+floorsAdded+" registered="+registered);
    }
    static int FillFloor(BuildPlot plot,BuildCatalog catalog)
    {
        var tiles=plot.GetComponentsInChildren<Transform>(true).Where(t=>
            PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)=="Assets/Prefabs/Floor.prefab" &&
            Mathf.Abs(t.position.y-plot.Grid.position.y)<.05f).ToList();
        if(tiles.Count==0)throw new Exception("No reference floor on base level: "+plot.name);
        var reference=tiles[0];
        if(Quaternion.Angle(reference.rotation,Quaternion.identity)>.01f || Vector3.Distance(reference.lossyScale,Vector3.one)>.001f)
            throw new Exception("Inspect rotated/scaled floor before extending its grid");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Floor.prefab");
        var mesh=prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
        float stepX=mesh.size.x,stepZ=mesh.size.z;
        var volume=plot.Volume.bounds;
        Vector3 origin=reference.position;
        int added=0;
        int minX=Mathf.CeilToInt((volume.min.x-origin.x-mesh.min.x)/stepX-.001f);
        int maxX=Mathf.FloorToInt((volume.max.x-origin.x-mesh.max.x)/stepX+.001f);
        int minZ=Mathf.CeilToInt((volume.min.z-origin.z-mesh.min.z)/stepZ-.001f);
        int maxZ=Mathf.FloorToInt((volume.max.z-origin.z-mesh.max.z)/stepZ+.001f);
        for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
        {
            Vector3 pos=origin+new Vector3(x*stepX,0,z*stepZ);
            if(tiles.Any(t=>Vector3.Distance(t.position,pos)<.01f))continue;
            // Respect the actual oriented volume, not just its world AABB.
            bool inside=true;
            foreach(float dx in new[]{mesh.min.x,mesh.max.x})foreach(float dz in new[]{mesh.min.z,mesh.max.z})
                inside &= plot.Contains(pos+new Vector3(dx,.01f,dz));
            if(!inside)continue;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,plot.gameObject.scene);
            go.name="Floor_Base_"+added;
            go.transform.SetPositionAndRotation(pos,reference.rotation);
            go.transform.SetParent(plot.transform,true);
            tiles.Add(go.transform);added++;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        int expected=(maxX-minX+1)*(maxZ-minZ+1);
        if(tiles.Count!=expected)throw new Exception("Base floor coverage mismatch "+tiles.Count+" / "+expected);
        return added;
    }
    public static void ApplyAndValidate() { Apply();ConstructionValueValidation.Run(); }
}
// Editor-only balance tools, grouped with the construction pricing tools.
public static class DevelopmentEconomyTools
{
    const string BalanceMenu="Tools/Criminal Game/Desarrollo/Saldo a 5.000.000 (Play)";
    public const int TestBalance=5000000;
    public static bool SetBalance(EconomyService economy,int target)
    {
        var save=economy?economy.GetComponent<SaveService>():null;
        if(!save || !save.HasCurrentGame)return false;
        target=Mathf.Max(0,target);
        if(economy.Money<target)economy.AddMoney(target-economy.Money);
        else if(economy.Money>target && !economy.TrySpend(economy.Money-target))return false;
        return economy.Money==target;
    }
    [MenuItem(BalanceMenu)]
    public static void GrantTestBalance()
    {
        if(!EditorApplication.isPlaying)return;
        if(SetBalance(Object.FindAnyObjectByType<EconomyService>(),TestBalance))
            Debug.Log("Development balance set to $5,000,000. Save the game normally only if you want to keep this balance.");
        else Debug.LogWarning("Start or load a game before setting the test balance.");
    }
    [MenuItem(BalanceMenu,true)]
    static bool CanGrant()
    {
        if(!EditorApplication.isPlaying)return false;
        var economy=Object.FindAnyObjectByType<EconomyService>();var save=economy?economy.GetComponent<SaveService>():null;
        return save && save.HasCurrentGame;
    }
}
#endif
