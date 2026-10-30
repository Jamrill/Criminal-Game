using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Construction;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ConstructionPropertyValidation
{
    public static void Run()
    {
        ConstructionValidation.Run();
        var report=new List<string>();
        void Check(bool condition,string text) { if(!condition) throw new Exception("FAIL: "+text); report.Add("PASS: "+text); }
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        var controller=prefab.GetComponent<ConstructionController>();
        Check(controller.embeddedUI && controller.embeddedUI.transform.IsChildOf(prefab.transform),"UI hija del prefab Player");
        Check(prefab.GetComponentsInChildren<BuildMenuView>(true).Length==1,"Una sola UI incorporada");
        Check(prefab.GetComponent<ConstructionPersistence>().catalog==catalog,"Player inicia persistencia");
        Check(!UnityEngine.Object.FindAnyObjectByType<ConstructionPersistence>(),"Sin host de persistencia sobrante en la escena");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        // Opening a scene unloads prefab assets not referenced by scene objects.
        catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var root=new GameObject("ValidationServices");
        GameObject uiRoot=null, plotRoot=null, loaderRoot=null;
        void Awake(object target) => target.GetType().GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
        try
        {
            var save=root.AddComponent<SaveService>(); save.SetCurrent(new SaveData{slotId=1,money=3});
            var economy=root.AddComponent<EconomyService>(); Awake(economy);
            var properties=root.AddComponent<PropertyService>(); Awake(properties);
            Check(!properties.TryBuy(-1,1) && economy.Money==3,"ID invalido no cobra");
            Check(!properties.TryBuy(10,-1) && economy.Money==3,"Precio negativo no cobra");
            Check(!properties.TryBuy(10,4) && !properties.IsOwned(10),"Sin saldo no compra");
            int notifications=0; properties.OnOwnershipChanged+=_=>notifications++;
            plotRoot=new GameObject("Solar de prueba"); var plot=plotRoot.AddComponent<BuildPlot>();
            plot.propertyId=10; plot.price=1; plot.buildable=true; BuildPlot.Active.Add(plot);
            uiRoot=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ConstructionUI.prefab"));
            var ui=uiRoot.GetComponent<BuildMenuView>(); ui.propertiesPanel.Refresh();
            var row=ui.propertiesPanel.content.GetComponentInChildren<Button>(true);
            Check(row && row.interactable,"Solar disponible listado para compra");
            row.onClick.Invoke();
            Check(economy.Money==2 && properties.IsOwned(10) && notifications==1,"Boton compra por 1 y registra propiedad");
            Check(!row.interactable && plot.CanBuild(properties),"Propiedad comprada desactiva compra y habilita construccion");
            Check(!properties.TryBuy(10,1) && economy.Money==2,"Compra duplicada no cobra de nuevo");
            var roundtrip=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Current));
            Check(roundtrip.ownedProperties.Contains(10) && roundtrip.money==2,"Propiedad y saldo persisten en SaveData");
            var record=new BuildRecord{id="property-validation",definitionId=catalog.entries[0].id,sceneName=scene.name,propertyId=10,position=new Vector3(20,0,20),scale=catalog.entries[0].prefab.transform.localScale};
            save.Current.buildings=new[]{record};
            loaderRoot=new GameObject("FirstPlayerLoader"); var loader=loaderRoot.AddComponent<ConstructionPersistence>(); loader.catalog=catalog;
            loader.RestoreScene(save.Current,scene);
            var placed=UnityEngine.Object.FindObjectsByType<PlacedBuildObject>(FindObjectsSortMode.None).Single();
            placed.transform.position+=Vector3.right; var movedPosition=placed.transform.position;
            loader.RestoreScene(save.Current,scene);
            Check(placed.transform.position==movedPosition && UnityEngine.Object.FindObjectsByType<PlacedBuildObject>(FindObjectsSortMode.None).Length==1,"Restauracion repetida no duplica ni reinicia objetos");
            UnityEngine.Object.DestroyImmediate(loaderRoot); loaderRoot=new GameObject("RespawnedPlayerLoader");
            loader=loaderRoot.AddComponent<ConstructionPersistence>(); loader.catalog=catalog; loader.RestoreScene(save.Current,scene);
            Check(placed && placed.transform.position==movedPosition && !placed.transform.parent,"Respawn conserva construccion independiente del Player");
            UnityEngine.Object.DestroyImmediate(placed.gameObject);
        }
        finally
        {
            if(plotRoot) { BuildPlot.Active.Remove(plotRoot.GetComponent<BuildPlot>()); UnityEngine.Object.DestroyImmediate(plotRoot); }
            if(uiRoot) UnityEngine.Object.DestroyImmediate(uiRoot);
            if(loaderRoot) UnityEngine.Object.DestroyImmediate(loaderRoot);
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        }
        File.WriteAllLines("Documentation/ConstructionPropertiesValidation.txt",report);
        Debug.Log("CONSTRUCTION_PROPERTIES_VALIDATION_OK tests="+report.Count);
    }
}
