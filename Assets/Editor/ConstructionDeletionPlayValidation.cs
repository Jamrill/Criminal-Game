using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Construction;
using JuegoCriminal.Core;
using JuegoCriminal.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ConstructionDeletionPlayValidation
{
    const string Key="CriminalGame.DeletionPlayValidation";
    static readonly List<string> report=new();
    static int phase,frame;static double deadline;static BuildPlot plot;static BuildCatalog catalog;static BuildEntry wall;static GameObject services;
    static PlacedBuildObject sold,authored,legacy,replacement;static EconomyService money;static ConstructionPersistence persistence;
    static ConstructionDeletionPlayValidation(){if(SessionState.GetBool(Key,false))Hook();}
    static void Hook(){EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    public static void Start()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Isolated construction deletion test").AddComponent<StandalonePreviewScene>();
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);Hook();EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;deadline=EditorApplication.timeSinceStartup+45;report.Clear();}
        if(state==PlayModeStateChange.EnteredEditMode)
        {SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.Exit(SessionState.GetInt(Key+".result",1));}
    }
    static void Check(bool condition,string text)
    {report.Add((condition?"PASS ":"FAIL ")+text);File.WriteAllLines("Documentation/ConstructionDeletionPlayValidation.txt",report);if(!condition)throw new Exception(text);}
    static BuildRecord Record(string id)=>new(){id=id,definitionId=wall.id,propertyId=881,sceneName=plot.gameObject.scene.name,position=Vector3.zero,rotation=Quaternion.identity,scale=Vector3.one,finish=-1};
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Deletion Play validation timed out");
            if(phase==0)
            {
                catalog=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath));catalog.developmentFreeConstruction=false;wall=catalog.entries.First(e=>e.prefab.name=="Wall_complete");
                plot=new GameObject("Deletion Play plot").AddComponent<BuildPlot>();plot.propertyId=881;plot.catalog=catalog;plot.buildable=true;plot.Volume.isTrigger=true;plot.Volume.size=Vector3.one*100;plot.GetComponent<RoomConstruction>().completeAutomatically=false;
                services=new GameObject("Deletion Play services");var save=services.AddComponent<SaveService>();money=services.AddComponent<EconomyService>();services.AddComponent<PropertyService>();
                typeof(SaveService).GetProperty("Current").GetSetMethod(true).Invoke(save,new object[]{new SaveData{slotId=1,money=100000,ownedProperties=new[]{881}}});
                sold=ConstructionEconomy.Build(catalog,Record("sold"),plot,money);Check(sold && ConstructionEconomy.Sell(catalog,sold,money),"Runtime sale succeeds");
                Check(sold.deleted && !sold.gameObject.activeSelf && BuildDeletionLedger.Deleted(plot).Any(r=>r.id=="sold"),"Sale suppresses physics immediately and records deletion before deferred Destroy");
                authored=ConstructionPersistence.Spawn(catalog,Record("sold"),false,plot.gameObject.scene);
                persistence=services.AddComponent<ConstructionPersistence>();persistence.catalog=catalog;
                var tombstone=Record("sold");tombstone.deleted=true;
                persistence.RestoreScene(new SaveData{buildings=new[]{tombstone}},plot.gameObject.scene);
                frame=Time.frameCount;phase=1;
            }
            else if(phase==1 && Time.frameCount>frame)
            {
                Check(!sold && !authored && plot.GetComponentsInChildren<PlacedBuildObject>(true).Length==0,"After a frame, neither sold nor suppressed authored model remains in the hierarchy");
                Check(BuildDeletionLedger.CaptureScene(plot.gameObject.scene).Count(r=>r.id=="sold" && r.deleted)==1,"Save capture retains only one data record for the deletion");
                legacy=ConstructionPersistence.Spawn(catalog,Record("legacy-live"),false,plot.gameObject.scene);legacy.deleted=true;legacy.gameObject.SetActive(false);
                persistence.RestoreScene(new SaveData{buildings=new[]{Record("legacy-live")}},plot.gameObject.scene);
                replacement=plot.GetComponentsInChildren<PlacedBuildObject>().Single(p=>!p.deleted && p.persistentId=="legacy-live");
                Check(replacement!=legacy,"Loading a live identity replaces a legacy model already scheduled for destruction");frame=Time.frameCount;phase=2;
            }
            else if(phase==2 && Time.frameCount>frame)
            {
                Check(!legacy && replacement && replacement.gameObject.activeInHierarchy,"Live restored replacement survives the frame while the legacy model is destroyed");
                Check(!BuildDeletionLedger.Deleted(plot).Any(r=>r.id=="legacy-live"),"Restoring a live identity removes its obsolete deletion record");
                SessionState.SetInt(Key+".result",0);Debug.Log("CONSTRUCTION_DELETION_PLAY_OK checks="+report.Count);phase=3;EditorApplication.delayCall+=EditorApplication.ExitPlaymode;
            }
        }
        catch(Exception exception){Debug.LogException(exception);phase=3;EditorApplication.delayCall+=EditorApplication.ExitPlaymode;}
    }
}
