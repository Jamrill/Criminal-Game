using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class ConstructionSetup
{
    public const string CatalogPath = "Assets/ConstructionCatalog.asset";
    const string ScenePath = "Assets/Scenes/10_World_City.unity";
    static IEnumerable<string> WallPaths() => AssetDatabase.FindAssets("t:Prefab", new[]{"Assets/Prefabs/Construction"})
        .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains("wall"));
    public static void Inspect()
    {
        var lines = new List<string>();
        foreach(var path in WallPaths())
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))
                lines.Add(path+" | "+r.name+" | "+string.Join(", ",r.sharedMaterials.Select(m=>m ? m.name : "NULL")));
        }
        File.WriteAllLines("Documentation/ConstructionInspection.txt",lines);
        Debug.Log("CONSTRUCTION_INSPECTION_OK");
    }
    [MenuItem("Tools/Criminal Game/Construccion/Preparar sistema")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var shader=Shader.Find("CriminalGame/World Wall");
        if(!shader) throw new InvalidOperationException("World Wall shader missing");
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Wall_Building_texture.png");
        if(!texture) throw new InvalidOperationException("Wall texture missing");
        var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        importer.wrapMode=TextureWrapMode.Repeat; importer.SaveAndReimport();
        const string materialPath="Assets/Materials/Wall_Building.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,materialPath); }
        material.SetTexture("_BaseMap",texture); material.SetFloat("_MetersPerRepeat",4); material.SetFloat("_Smoothness",.15f);
        EditorUtility.SetDirty(material);
        var report=new List<string>();
        foreach(var path in WallPaths())
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var slots=new List<BuildSurface.Slot>();
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials=r.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)
                    {
                        string name=materials[i] ? materials[i].name.ToLowerInvariant() : "";
                        // Only plaster/paint slots. Glass, frames, skirting and hardware stay intact.
                        if(name=="white_paint" || name=="black_paint" || name=="wall_building" ||
                            name=="int" || name=="ext" || name.StartsWith("int_") || name.StartsWith("ext_") ||
                            name.Contains("wall_interior") || name.Contains("wall_exterior"))
                        { materials[i]=material; slots.Add(new BuildSurface.Slot{renderer=r,index=i}); }
                    }
                    r.sharedMaterials=materials;
                }
                if(slots.Count>0)
                {
                    var surface=root.GetComponent<BuildSurface>() ?? root.AddComponent<BuildSurface>(); surface.slots=slots.ToArray();
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                report.Add(path+": painted slots="+slots.Count);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(CatalogPath);
        if(!catalog) { catalog=ScriptableObject.CreateInstance<BuildCatalog>(); AssetDatabase.CreateAsset(catalog,CatalogPath); }
        var entries=new List<BuildEntry>();
        foreach(var path in WallPaths().Concat(AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Furniture"}).Select(AssetDatabase.GUIDToAssetPath)))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool any=false; var bounds=new Bounds();
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh) continue;
                var b=filter.sharedMesh.bounds;
                foreach(float x in new[]{b.min.x,b.max.x}) foreach(float y in new[]{b.min.y,b.max.y}) foreach(float z in new[]{b.min.z,b.max.z})
                {
                    var p=Quaternion.Inverse(prefab.transform.rotation)*(filter.transform.TransformPoint(new Vector3(x,y,z))-prefab.transform.position);
                    if(!any) { bounds=new Bounds(p,Vector3.zero); any=true; } else bounds.Encapsulate(p);
                }
            }
            if(!any) continue;
            entries.Add(new BuildEntry{id=AssetDatabase.AssetPathToGUID(path),title=prefab.name,prefab=prefab,bounds=bounds,
                category=path.Contains("/Furniture/") ? BuildCategory.Furniture : BuildCategory.Walls,
                hiddenInMenu=!path.Contains("/Furniture/") && !path.Contains("/Completes/")});
            report.Add(prefab.name+" bounds="+bounds);
        }
        catalog.entries=entries.ToArray();
        catalog.finishes=new[]{material,AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Paints/White_Paint.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Paints/Black_Paint.mat")};
        EditorUtility.SetDirty(catalog);
        const string uiPath="Assets/Prefabs/UI/ConstructionUI.prefab";
        var ui=AssetDatabase.LoadAssetAtPath<GameObject>(uiPath);
        if(!ui)
        {
            var view=BuildMenuView.Create(); ui=PrefabUtility.SaveAsPrefabAsset(view.gameObject,uiPath); UnityEngine.Object.DestroyImmediate(view.gameObject);
        }
        const string playerPath="Assets/Prefabs/Player.prefab";
        var player=PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            var controller=player.GetComponent<ConstructionController>() ?? player.AddComponent<ConstructionController>();
            controller.catalog=catalog; controller.uiPrefab=ui.GetComponent<BuildMenuView>();
            PrefabUtility.SaveAsPrefabAsset(player,playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        ConstructionPropertySetup.ConfigurePlayer(catalog);
        var actions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        var map=actions.FindActionMap("Player",true);
        if(map.FindAction("Construction")==null) map.AddAction("Construction",InputActionType.Button,"<Keyboard>/tab",groups:"Keyboard&Mouse");
        if(map.FindAction("BuildRotate")==null) map.AddAction("BuildRotate",InputActionType.Button,"<Keyboard>/r",groups:"Keyboard&Mouse");
        File.WriteAllText(AssetDatabase.GetAssetPath(actions),actions.ToJson());
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(actions));
        var scene=EditorSceneManager.OpenScene(ScenePath);
        ConstructionPropertySetup.RemoveSceneHost();
        foreach(var surface in UnityEngine.Object.FindObjectsByType<BuildSurface>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            surface.Apply(material);
            foreach(var slot in surface.slots) if(slot.renderer) PrefabUtility.RecordPrefabInstancePropertyModifications(slot.renderer);
        }
        foreach(var plot in UnityEngine.Object.FindObjectsByType<BuildPlot>(FindObjectsSortMode.None))
            report.Add("PLOT "+plot.name+" propertyId="+plot.propertyId+" buildable="+plot.buildable+" origin="+(plot.gridOrigin ? plot.gridOrigin.position.ToString() : "MISSING"));
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        foreach(var message in ShaderUtil.GetShaderMessages(shader)) report.Add("SHADER "+message.severity+": "+message.message);
        File.WriteAllLines("Documentation/ConstructionSetupReport.txt",report);
        Debug.Log("CONSTRUCTION_SETUP_OK entries="+entries.Count);
    }
}
