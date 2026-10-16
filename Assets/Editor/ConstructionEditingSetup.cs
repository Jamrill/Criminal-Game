using System;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using JuegoCriminal.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ConstructionEditingSetup
{
    public static void Run()
    {
        const string path="Assets/Scenes/10_World_City.unity";
        string backup="Documentation/ConstructionEditingBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);File.Copy(path,backup+"/10_World_City.unity");
        var scene=EditorSceneManager.OpenScene(path);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        int tagged=0;
        foreach(var root in scene.GetRootGameObjects())foreach(var plot in root.GetComponentsInChildren<BuildPlot>(true))
        {
            foreach(Transform child in plot.transform)
            {
                var prefabPath=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                var entry=catalog.entries.FirstOrDefault(e=>e.prefab && AssetDatabase.GetAssetPath(e.prefab)==prefabPath);
                if(entry==null)continue;
                var marker=child.GetComponent<PlacedBuildObject>();if(marker)continue;
                marker=child.gameObject.AddComponent<PlacedBuildObject>();
                marker.persistentId="scene-"+GUID.Generate();marker.definitionId=entry.id;
                marker.propertyId=plot.propertyId;marker.finish=-1;marker.runtimePlaced=false;
                var wall=child.GetComponent<WallTopology>();marker.cellCenter=wall?wall.Bottom:child.position;
                EditorUtility.SetDirty(marker);tagged++;
            }
        }
        var zone=Object.FindObjectsByType<ReflectionZone>(FindObjectsSortMode.None).FirstOrDefault(z=>z.name=="Reflections_Interior_Small_Property");
        if(zone)
        {
            // Move the blending shell slightly outside the old wall/ceiling boundaries.
            // Keep the probe centre/capture point, priority and refresh budget unchanged.
            var serialized=new SerializedObject(zone);
            var size=serialized.FindProperty("volumeSize");
            if(Vector3.Distance(size.vector3Value,new Vector3(24.5f,7.2f,24.5f))<.01f)
                size.vector3Value=new Vector3(24.9f,7.6f,24.9f);
            serialized.FindProperty("blendDistance").floatValue=.15f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Documentation/ConstructionEditingSetup.txt","Scene markers added="+tagged+"\nBackup="+backup+"\nReflection adjustment requires visual day-time comparison.\n");
        Validate();
        Debug.Log("CONSTRUCTION_EDITING_OK tagged="+tagged);
    }
    public static void Validate()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        void Check(bool condition,string message){if(!condition)throw new Exception(message);Debug.Log("EDITING_TEST_PASS "+message);}
        var straight=catalog.entries.First(e=>e.prefab && (e.prefab.name=="Wall" || e.prefab.name=="Wall_complete"));
        var opening=catalog.entries.First(e=>e.IsOpening && !e.hiddenInMenu);
        var marker=ConstructionPersistence.Spawn(catalog,new BuildRecord{id="test",definitionId=straight.id,scale=Vector3.one,rotation=Quaternion.identity,propertyId=1,finish=-1},true);
        marker.Demolish();var record=marker.Capture();
        Check(record.deleted && !marker.gameObject.activeSelf,"Demolition creates inactive tombstone");
        Check(Object.FindObjectsByType<PlacedBuildObject>(FindObjectsInactive.Include,FindObjectsSortMode.None).Contains(marker),"Save includes tombstone");
        var restored=ConstructionPersistence.Spawn(catalog,record,true);
        Check(restored.deleted && !restored.gameObject.activeSelf,"Deleted object stays deleted after restore");
        record.deleted=false;record.definitionId=opening.id;
        var converted=ConstructionPersistence.Spawn(catalog,record,true);
        Check(converted.definitionId==opening.id && converted.persistentId=="test","Opening keeps identity");
        var plot=new GameObject("Plot").AddComponent<BuildPlot>();plot.Volume.size=new Vector3(100,100,100);
        var pos=plot.SnapOrigin(new Vector3(1.2f,7,2.8f));
        Check(Vector3.Distance(pos,new Vector3(1,7,3))<.001f,"One-unit grid preserves storey height");
        var input=AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        Check(input.FindAction("Player/BuildDemolish").bindings.Any(b=>b.path=="<Keyboard>/x"),"Demolition X binding exists");
        Check(!ShaderUtil.ShaderHasError(Shader.Find("CriminalGame/World Wall")),"Wall shader has no reported errors");
        Object.DestroyImmediate(marker.gameObject);Object.DestroyImmediate(restored.gameObject);Object.DestroyImmediate(converted.gameObject);
        marker=ConstructionPersistence.Spawn(catalog,new BuildRecord{id="aim",definitionId=straight.id,scale=Vector3.one,rotation=Quaternion.identity,propertyId=1,finish=-1},true);
        plot.propertyId=1;Physics.SyncTransforms();
        var tool=new ConstructionEditing();var red=new Material(Shader.Find("Universal Render Pipeline/Unlit"));var green=new Material(red);
        var ray=new Ray(new Vector3(2,6,-3),Vector3.forward);
        var text=tool.Tick(ray,plot,catalog,opening,red,green,false);
        Check(text.StartsWith("Clic: sustituir"),"Opening conversion validates an aimed wall");tool.Clear();
        plot.propertyId=2;tool.Tick(ray,plot,catalog,null,red,green,true);
        Check(!marker.deleted,"Cannot demolish another property's wall");
        plot.propertyId=1;tool.Tick(ray,plot,catalog,null,red,green,true);
        Check(marker.deleted,"Aimed own wall is demolished on click");tool.Clear();
        Object.DestroyImmediate(red);Object.DestroyImmediate(green);
        Debug.Log("CONSTRUCTION_EDITING_VALIDATION_OK");
    }
}
