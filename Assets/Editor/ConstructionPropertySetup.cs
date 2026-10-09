using System;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using JuegoCriminal.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConstructionPropertySetup
{
    public static void ConfigurePlayer(BuildCatalog catalog)
    {
        const string uiPath="Assets/Prefabs/UI/ConstructionUI.prefab";
        var uiRoot=PrefabUtility.LoadPrefabContents(uiPath);
        try
        {
            uiRoot.GetComponent<BuildMenuView>().EnsurePropertiesUI();
            PrefabUtility.SaveAsPrefabAsset(uiRoot,uiPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(uiRoot); }
        const string playerPath="Assets/Prefabs/Player.prefab";
        var player=PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            var controller=player.GetComponent<ConstructionController>() ?? player.AddComponent<ConstructionController>();
            controller.catalog=catalog;
            controller.uiPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(uiPath).GetComponent<BuildMenuView>();
            var embedded=player.GetComponentInChildren<BuildMenuView>(true);
            if(!embedded)
                embedded=((GameObject)PrefabUtility.InstantiatePrefab(controller.uiPrefab.gameObject,player.transform)).GetComponent<BuildMenuView>();
            controller.embeddedUI=embedded;
            var persistence=player.GetComponent<ConstructionPersistence>() ?? player.AddComponent<ConstructionPersistence>();
            persistence.catalog=catalog;
            PrefabUtility.SaveAsPrefabAsset(player,playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
    }

    public static void RemoveSceneHost()
    {
        foreach(var host in UnityEngine.Object.FindObjectsByType<ConstructionPersistence>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(host.GetComponent<ConstructionController>()) continue;
            var go=host.gameObject;
            // Only remove the old, otherwise empty host. Preserve any user-added contents.
            if(go.name=="ConstructionPersistence" && go.transform.childCount==0 && go.GetComponents<Component>().Length==2)
                UnityEngine.Object.DestroyImmediate(go);
            else UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [MenuItem("Tools/Criminal Game/Construccion/Integrar propiedades y UI del Player")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        if(!catalog) throw new InvalidOperationException("Falta el catalogo de construccion.");
        ConfigurePlayer(catalog);
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        RemoveSceneHost();
        var plots=UnityEngine.Object.FindObjectsByType<BuildPlot>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var used=plots.Select(p=>p.propertyId).Concat(UnityEngine.Object.FindObjectsByType<PropertyMarker>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(m=>m.propertyId)).ToHashSet();
        foreach(var plot in plots)
        {
            if(plot.propertyId<0) { int id=1; while(used.Contains(id)) id++; plot.propertyId=id; used.Add(id); }
            plot.price=1;
            if(plot.name=="Solar") plot.buildable=true;
            EditorUtility.SetDirty(plot);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        File.WriteAllLines("Documentation/ConstructionPropertiesSetup.txt",plots.Select(p=>$"{p.name}: ID={p.propertyId}, price={p.price}, buildable={p.buildable}, origin={p.Grid.position}"));
        Debug.Log("CONSTRUCTION_PROPERTIES_SETUP_OK");
    }
}
