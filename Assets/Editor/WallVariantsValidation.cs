using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JuegoCriminal.Construction;
using JuegoCriminal.Environment;
using JuegoCriminal.UI;
using JuegoCriminal.Core;
using Object=UnityEngine.Object;

public static class WallVariantsValidation
{
    public static void SetupAndRun() { WallVariantsSetup.Run(); Run(); }
    public static void Run()
    {
        ConstructionPropertyValidation.Run();
        var report=new List<string>();
        void Check(bool ok,string message) { if(!ok) throw new Exception("FAIL: "+message); report.Add("PASS: "+message); }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        var walls=catalog.entries.Where(e=>e.category==BuildCategory.Walls).ToArray();
        Check(walls.Length==27,"27 prefabs de pared");
        Check(walls.Count(e=>!e.hiddenInMenu)==9,"Solo 9 completas en el menu");
        foreach(var entry in walls)
        {
            var topology=entry.prefab.GetComponent<WallTopology>();
            Check(topology && topology.variants.Length==3 && topology.variants.All(m=>m),entry.title+" tiene 3 mallas");
            Check(entry.bounds.size.y>6.9f && entry.bounds.size.y<7.1f,entry.title+" altura coherente");
            Check(entry.prefab.GetComponentsInChildren<BoxCollider>(true).Length>0,entry.title+" conserva BoxColliders");
        }
        var straight=walls.Single(e=>e.prefab.name=="Wall");
        var left=Object.Instantiate(straight.prefab,new Vector3(-4,0,0),Quaternion.identity);
        var middle=Object.Instantiate(straight.prefab,Vector3.zero,Quaternion.identity);
        var right=Object.Instantiate(straight.prefab,new Vector3(4,0,0),Quaternion.identity);
        WallTopology.RefreshScene(scene);
        var mid=middle.GetComponent<WallTopology>();
        Check(mid.currentVariant==1,"Dos vecinos cambian la central a semincompleta");
        Check(left.GetComponent<WallTopology>().currentVariant==0 && right.GetComponent<WallTopology>().currentVariant==0,"Extremos permanecen completos");
        middle.GetComponent<BuildSurface>().Apply(catalog.finishes[1]);
        var upper=Object.Instantiate(straight.prefab,new Vector3(0,7,0),Quaternion.identity);
        WallTopology.RefreshScene(scene);
        Check(mid.currentVariant==2,"Pared superior cambia central a incompleta");
        Check(mid.wall.GetComponent<Renderer>().sharedMaterials.All(m=>m==catalog.finishes[1]),"Acabado conservado al cambiar variante");
        Object.DestroyImmediate(upper); WallTopology.RefreshScene(scene); Check(mid.currentVariant==1,"Quitar pared superior recupera semincompleta");
        Object.DestroyImmediate(right); WallTopology.RefreshScene(scene); Check(mid.currentVariant==0,"Quitar vecino recupera completa");
        var cornerPrefab=walls.Single(e=>e.prefab.name=="Corner_Wall").prefab;
        var corner=Object.Instantiate(cornerPrefab,new Vector3(40,0,0),Quaternion.identity);
        Object.Instantiate(straight.prefab,new Vector3(36,0,0),Quaternion.identity);
        Object.Instantiate(straight.prefab,new Vector3(44,0,8),Quaternion.Euler(0,90,0));
        WallTopology.RefreshScene(scene); Check(corner.GetComponent<WallTopology>().currentVariant==1,"Esquina reconoce vecino girado");
        var tee=Object.Instantiate(walls.Single(e=>e.prefab.name=="T_wall_complete").prefab,new Vector3(60,0,0),Quaternion.identity);
        Object.Instantiate(straight.prefab,new Vector3(56,0,0),Quaternion.identity);
        Object.Instantiate(straight.prefab,new Vector3(64,0,8),Quaternion.Euler(0,90,0));
        WallTopology.RefreshScene(scene); Check(tee.GetComponent<WallTopology>().currentVariant==0,"T conserva caras con un extremo abierto");
        Object.Instantiate(straight.prefab,new Vector3(64,0,-8),Quaternion.Euler(0,-90,0));
        WallTopology.RefreshScene(scene); Check(tee.GetComponent<WallTopology>().currentVariant==1,"T cambia al conectar sus tres extremos");
        var plot=new GameObject("TestPlot").AddComponent<BuildPlot>();plot.Volume.isTrigger=true;plot.Volume.size=new Vector3(100,30,100);plot.Volume.center=new Vector3(0,15,0);
        Physics.SyncTransforms();
        Check(BuildPlacement.IsValid(straight,plot,new Vector3(4,0,0),Quaternion.identity,new Vector3(6,0,0)),"Permite unir pared por extremo");
        Check(!BuildPlacement.IsValid(straight,plot,Vector3.zero,Quaternion.identity,new Vector3(2,0,0)),"Rechaza superponer una pared existente");
        var cycle=new GameObject("Cycle").AddComponent<DayNightCycle>();
        var sun=new GameObject("Sun").AddComponent<Light>(); var moon=new GameObject("Moon").AddComponent<Light>(); cycle.Configure(sun,moon,null);
        typeof(DayNightCycle).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cycle,null);
        float hour=cycle.CurrentHour; float intensity=sun.intensity; bool enabled=sun.enabled;
        var routine=cycle.PrepareLightingVariants();int frames=0;
        while(routine.MoveNext()) { frames++; Check(cycle.CurrentHour==hour,"Preparacion no avanza el reloj"); }
        Check(frames>=8 && Mathf.Approximately(intensity,sun.intensity) && enabled==sun.enabled,"Preparacion restaura iluminacion inicial");
        var controls=new GameObject("Controls",typeof(RectTransform)).AddComponent<ControlsMenuUI>();
        var content=new GameObject("Content",typeof(RectTransform));content.transform.SetParent(controls.transform,false);
        typeof(ControlsMenuUI).GetField("contentRoot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controls,content.GetComponent<RectTransform>());
        controls.OrganizeSections(); int count=content.transform.childCount;controls.OrganizeSections();
        Check(count==content.transform.childCount && content.transform.GetChild(0).name=="NormalControlsHeader","Encabezados de controles sin duplicados");
        Check(content.transform.Find("ConstructionControlsHeader") && content.transform.Find("Binding_RotateBuilding"),"Seccion de construccion con rotacion");
        File.WriteAllLines("Documentation/WallVariantsValidation.txt",report);
        EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        Debug.Log("WALL_VARIANTS_VALIDATION_OK checks="+report.Count);
    }
}
