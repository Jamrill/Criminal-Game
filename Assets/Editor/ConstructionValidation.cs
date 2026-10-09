using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class ConstructionValidation
{
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string description) { if(!ok) throw new Exception("FAIL: "+description); report.Add("PASS: "+description); }
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        Check(catalog && catalog.entries.Length>0,"Catalogo disponible");
        Check(catalog.entries.Select(e=>e.id).Distinct().Count()==catalog.entries.Length,"GUID de catalogo unicos");
        Check(catalog.entries.All(e=>e.prefab && e.bounds.size.sqrMagnitude>0),"Prefabs y volumenes validos");
        var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        var controller=player.GetComponent<ConstructionController>();
        Check(controller && controller.catalog==catalog && controller.uiPrefab,"Player enlazado a catalogo y Canvas");
        var ui=controller.uiPrefab;
        Check(ui.menu && ui.hud && ui.content && ui.buildButton && ui.status && ui.buildLabel,"Referencias de UI completas");
        var actions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        Check(actions.FindAction("Player/Construction",true).bindings.Any(b=>b.path=="<Keyboard>/tab"),"Tab asignado");
        Check(actions.FindAction("Player/BuildRotate",true).bindings.Any(b=>b.path=="<Keyboard>/r"),"R asignada");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        Check(player.GetComponent<ConstructionPersistence>().catalog==catalog,"Persistencia en prefab Player");
        var realPlot=UnityEngine.Object.FindObjectsByType<BuildPlot>(FindObjectsSortMode.None).First(p=>p.name=="Solar");
        Check(realPlot.gridOrigin && realPlot.Volume.isTrigger,"Solar conserva origen y trigger");
        report.Add("INFO: solar Property Id y Buildable: "+realPlot.propertyId+", "+realPlot.buildable);
        var testScene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Validation");
        try
        {
            var plot=root.AddComponent<BuildPlot>(); plot.Volume.isTrigger=true; plot.Volume.size=new Vector3(40,30,40); plot.Volume.center=new Vector3(0,15,0);
            plot.propertyId=12; plot.buildable=true;
            var save=root.AddComponent<SaveService>(); save.SetCurrent(new SaveData{slotId=1});
            var properties=root.AddComponent<PropertyService>();
            typeof(PropertyService).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(properties,null);
            Check(!plot.CanBuild(properties),"Solar ajeno impide construir");
            save.TryAddOwnedProperty(12); Check(plot.CanBuild(properties),"Solar propio habilita construir");
            plot.buildable=false; Check(!plot.CanBuild(properties),"Buildable desactivado bloquea construccion"); plot.buildable=true;
            Check(plot.Cell(new Vector3(4.1f,0,8.1f))==new Vector2Int(1,2),"Rejilla 4x4");
            Check(plot.Cell(new Vector3(-.1f,0,-.1f))==new Vector2Int(-1,-1),"Rejilla funciona en coordenadas negativas");
            var occupant=new GameObject("OccupantTest");
            var capsule=occupant.AddComponent<CharacterController>(); capsule.height=3.5f; capsule.center=new Vector3(0,1.9f,0);
            occupant.transform.position=new Vector3(0,-.1f,0);
            Check(!plot.Contains(occupant.transform.position) && plot.ContainsPlayer(occupant.transform,capsule),"Pivote bajo el suelo no excluye una capsula interior");
            occupant.transform.position=new Vector3(30,-.1f,0);
            Check(!plot.ContainsPlayer(occupant.transform,capsule),"Capsula fuera del solar sigue excluida");
            occupant.transform.position=new Vector3(0,-10,0);
            Check(!plot.ContainsPlayer(occupant.transform,capsule),"Capsula completamente bajo el solar sigue excluida");
            UnityEngine.Object.DestroyImmediate(occupant);
            var entry=new BuildEntry{prefab=player,bounds=new Bounds(new Vector3(0,1,0),new Vector3(2,2,2))};
            var cell=plot.Center(Vector2Int.zero); var position=BuildPlacement.Position(entry,cell,Quaternion.identity);
            Physics.SyncTransforms();
            Check(BuildPlacement.IsValid(entry,plot,position,Quaternion.identity,cell),"Zona vacia valida");
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.transform.position=cell+Vector3.up;
            Physics.SyncTransforms(); Check(!BuildPlacement.IsValid(entry,plot,position,Quaternion.identity,cell),"Solapamiento rechaza colocacion");
            UnityEngine.Object.DestroyImmediate(obstacle); Physics.SyncTransforms();
            Check(!BuildPlacement.IsValid(entry,plot,new Vector3(40,0,40),Quaternion.identity,new Vector3(40,0,40)),"Fuera de solar rechazado");
            var occupied=new GameObject("Occupied").AddComponent<PlacedBuildObject>(); occupied.cellCenter=cell;
            Check(!BuildPlacement.IsValid(entry,plot,position,Quaternion.identity,cell),"Celda ocupada bloqueada aun sin collider");
            UnityEngine.Object.DestroyImmediate(occupied.gameObject);
            var record=new BuildRecord{id="validation",definitionId=catalog.entries[0].id,sceneName=testScene.name,propertyId=12,position=cell,cellCenter=cell,rotation=Quaternion.Euler(0,90,0),scale=catalog.entries[0].prefab.transform.localScale,finish=1};
            save.UpdateBuildingStates(testScene.name,new[]{record});
            var roundtrip=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save.Current));
            Check(roundtrip.buildings.Length==1 && roundtrip.buildings[0].finish==1 && roundtrip.buildings[0].rotation==record.rotation,"SaveData conserva construccion y acabado");
            var restored=ConstructionPersistence.Spawn(catalog,roundtrip.buildings[0],true);
            Check(restored && restored.persistentId==record.id && restored.transform.position==record.position,"Restauracion de prefab colocado");
            foreach(var surface in restored.GetComponentsInChildren<BuildSurface>())
                Check(surface.slots.All(s=>s.renderer.sharedMaterials[s.index]==catalog.finishes[1]),"Acabado restaurado en slots pintables");
            UnityEngine.Object.DestroyImmediate(restored.gameObject);
            var shader=Shader.Find("CriminalGame/World Wall");
            Check(shader && !ShaderUtil.ShaderHasError(shader),"Shader sin errores importados");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.GetComponent<Renderer>().sharedMaterial=catalog.finishes[0];
            var camera=new GameObject("ValidationCamera").AddComponent<Camera>(); camera.transform.position=new Vector3(0,0,-5);
            var target=new RenderTexture(64,64,16); camera.targetTexture=target; camera.Render(); camera.targetTexture=null;
            Check(!ShaderUtil.ShaderHasError(shader),"Shader tras render de prueba");
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(wall);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        File.WriteAllLines("Documentation/ConstructionValidation.txt",report);
        Debug.Log("CONSTRUCTION_VALIDATION_OK tests="+report.Count);
    }
}
