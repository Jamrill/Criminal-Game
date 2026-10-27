using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using JuegoCriminal.Services;
using System.Reflection;

public static class ConstructionEconomyGridRevision
{
    public static void Apply()
    {
        const string path="Assets/Scenes/10_World_City.unity";
        string backup="Documentation/ConstructionEconomyGridBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/10_World_City.unity");
        var scene=EditorSceneManager.OpenScene(path);var report=new List<string>{"Backup="+backup};
        foreach(var plot in Object.FindObjectsByType<BuildPlot>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var grid=plot.Grid;var gridPosition=grid.position;var gridRotation=grid.rotation;
            var items=plot.GetComponentsInChildren<PlacedBuildObject>(true).ToDictionary(i=>i,i=>(i.transform.position,i.transform.rotation,i.transform.localScale));
            plot.fitFurnitureGridToPlot=true;
            if(!plot.TryFurnitureGridLayout(out var offset,out var cells))throw new Exception("Plot is too small or is not aligned to its grid: "+plot.name);
            var world=grid.TransformPoint(new Vector3(offset.x,0,offset.y));
            if(grid.position!=gridPosition || Quaternion.Angle(grid.rotation,gridRotation)>.001f || items.Any(p=>p.Key.transform.position!=p.Value.position || Quaternion.Angle(p.Key.transform.rotation,p.Value.rotation)>.001f || p.Key.transform.localScale!=p.Value.localScale))throw new Exception("Grid fitting must not move existing transforms");
            report.Add(plot.name+": authored Grid Origin="+gridPosition.ToString("F3")+"; fitted furniture/visual grid="+world.ToString("F3")+"; cells="+cells+"; existing objects unchanged="+items.Count);
            EditorUtility.SetDirty(plot);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllLines("Documentation/ConstructionEconomyGridSetup.txt",report);Run();
    }
    public static void Run()
    {
        var report=new List<string>();
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/ConstructionEconomyGridValidation.txt",report);if(!ok)throw new Exception(label);}
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);bool oldFree=catalog.developmentFreeConstruction;
        var clone=Object.Instantiate(catalog);var prices=clone.entries.Select(e=>e.price).ToArray();
        var services=new GameObject("Economy development tools validation");var save=services.AddComponent<SaveService>();var money=services.AddComponent<EconomyService>();
        typeof(EconomyService).GetField("_save",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(money,save);
        try
        {
            Check(!DevelopmentEconomyTools.SetBalance(money,5000000),"Money tool does not create a save or modify a game that is not loaded");
            save.SetCurrent(new SaveData{slotId=1,money=250});int notifications=0,reported=-1;money.OnMoneyChanged+=value=>{notifications++;reported=value;};
            Check(DevelopmentEconomyTools.SetBalance(money,5000000) && money.Money==5000000 && reported==5000000 && notifications==1,"Money tool sets exactly five million and refreshes the HUD through EconomyService");
            money.AddMoney(6000000);Check(DevelopmentEconomyTools.SetBalance(money,5000000) && money.Money==5000000,"Money tool also works if the balance was higher than five million");
            int before=money.Money;ConstructionValueSetup.SetFreeConstruction(clone,true);
            Check(clone.FreeConstruction && clone.Cost(clone.entries.First(e=>e.price>0))==0,"Development free-cost toggle remains available in the Editor");
            ConstructionValueSetup.SetFreeConstruction(clone,false);var entry=clone.entries.First(e=>e.price>0);
            Check(!clone.FreeConstruction && clone.Cost(entry)==entry.price && prices.SequenceEqual(clone.entries.Select(e=>e.price)),"Restore real prices re-enables catalogue prices without losing or rewriting them");
            Check(money.TrySpend(clone.Cost(entry)) && money.Money==before-entry.price && reported==money.Money,"A real purchase deducts its real cost after restoring prices");
            Check(catalog.developmentFreeConstruction==oldFree,"Validation preserves the project's current free-cost selection");
        }
        finally{Object.DestroyImmediate(services);Object.DestroyImmediate(clone);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Grid fitting validation");
        try
        {
            var plot=root.AddComponent<BuildPlot>();plot.Volume.size=new Vector3(44.5f,28,44.5f);plot.Volume.center=new Vector3(0,6,0);root.transform.position=new Vector3(474,109,507);
            var grid=new GameObject("Grid Origin");grid.transform.SetParent(root.transform,false);grid.transform.position=new Vector3(474,101,507);plot.gridOrigin=grid.transform;
            var originalGrid=grid.transform.position;var chair=catalog.entries.First(e=>e.prefab.name=="Chair");
            plot.fitFurnitureGridToPlot=false;
            var oldAnchor=BuildFootprint.SnapAnchor(chair,plot,new Vector3(451.76f,101,484.76f),Quaternion.identity);
            Check(!BuildFootprint.IsAvailable(chair,plot,BuildPlacement.Position(chair,oldAnchor,Quaternion.identity),Quaternion.identity),"Reproduced: authored centre-origin grid rejects the parcel's near corner");
            plot.fitFurnitureGridToPlot=true;
            Check(plot.TryFurnitureGridLayout(out var offset,out var cells) && cells==new Vector2Int(11,11) && Vector2.Distance(offset,new Vector2(-22,-22))<.001f,"44.5x44.5 plot fits 11x11 full cells instead of only 10x10");
            var newOrigin=grid.transform.TransformPoint(new Vector3(offset.x,0,offset.y));
            Check(Vector3.Distance(newOrigin,new Vector3(452,101,485))<.001f && grid.transform.position==originalGrid,"Fitted origin matches floor tile phase while preserving Grid Origin transform and height");
            foreach(float x in new[]{451.76f,496.24f})foreach(float z in new[]{484.76f,529.24f})
            {
                var anchor=BuildFootprint.SnapAnchor(chair,plot,new Vector3(x,101,z),Quaternion.identity);
                Check(BuildFootprint.IsAvailable(chair,plot,BuildPlacement.Position(chair,anchor,Quaternion.identity),Quaternion.identity),"Aiming at plot corner "+x+","+z+" snaps to its closest complete usable cell");
                var cell=plot.FurnitureCell(anchor);Check(cell.x>=0 && cell.x<11 && cell.y>=0 && cell.y<11,"Visual grid indexes use the same fitted origin as snapping");
            }
            var bed=catalog.entries.First(e=>e.prefab.name=="Bed");var turn=Quaternion.Euler(0,90,0);
            var end=BuildFootprint.SnapAnchor(bed,plot,new Vector3(496.24f,108,529.24f),turn);
            Check(BuildFootprint.IsAvailable(bed,plot,BuildPlacement.Position(bed,end,turn),turn) && Mathf.Abs(end.y-108)<.001f,"Rotated two-cell furniture fits near the far corner without changing storey height");
            var wall=catalog.entries.First(e=>e.prefab.name=="Wall_complete");var point=new Vector3(451.76f,101,484.76f);
            Check(Vector3.Distance(BuildFootprint.SnapAnchor(wall,plot,point,Quaternion.identity),plot.SnapOrigin(point))<.001f,"Wall-origin snapping is unchanged");
            root.transform.rotation=Quaternion.Euler(0,90,0);
            Check(plot.TryFurnitureGridLayout(out offset,out cells) && cells==new Vector2Int(11,11),"Automatic fitting supports rotated rectangular parcels");
        }
        finally{Object.DestroyImmediate(root);}
        Debug.Log("CONSTRUCTION_ECONOMY_GRID_VALIDATION_OK checks="+report.Count);
    }
    public static void Regressions(){Run();ConstructionGuidesValidation.RunAll();Debug.Log("CONSTRUCTION_ECONOMY_GRID_REGRESSIONS_OK");}
    public static void Inspect()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");var report=new List<string>();
        foreach(var plot in Object.FindObjectsByType<BuildPlot>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            report.Add("PLOT "+plot.name+" position="+plot.transform.position.ToString("F4")+" rotation="+plot.transform.eulerAngles+" scale="+plot.transform.lossyScale);
            report.Add("VOLUME center="+plot.Volume.center.ToString("F4")+" size="+plot.Volume.size.ToString("F4")+" world="+plot.Volume.bounds);
            report.Add("GRID "+plot.Grid.name+" position="+plot.Grid.position.ToString("F4")+" rotation="+plot.Grid.eulerAngles+" scale="+plot.Grid.lossyScale);
            foreach(var floor in plot.GetComponentsInChildren<BuildFloorCoverage>(true).Take(4))report.Add("FLOOR "+floor.name+" position="+floor.transform.position.ToString("F4"));
        }
        File.WriteAllLines("Documentation/ConstructionEconomyGridInspection.txt",report);Debug.Log("CONSTRUCTION_ECONOMY_GRID_INSPECTION_OK");
    }
}
