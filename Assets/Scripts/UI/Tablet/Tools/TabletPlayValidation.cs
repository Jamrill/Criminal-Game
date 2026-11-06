#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using JuegoCriminal.Construction;
using JuegoCriminal.Core;
using JuegoCriminal.Environment;
using JuegoCriminal.Localization;
using JuegoCriminal.Services;
using JuegoCriminal.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TabletPlayValidation
{
    const string Key="TabletPlayValidation";
    static int phase;static double next,deadline;
    static ConstructionController controller;static BuildMenuView view;static BuildPlot plot;static EconomyService money;static PropertyService properties;
    static readonly List<string> report=new();
    static TabletPlayValidation(){if(SessionState.GetBool(Key,false))Hook();}
    static void Hook(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
    public static void Start()
    {
        SessionState.SetBool(Key+".hadLocale",PlayerPrefs.HasKey(GameLanguage.PreferenceKey));SessionState.SetInt(Key+".locale",PlayerPrefs.GetInt(GameLanguage.PreferenceKey));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Isolated test; no boot or disk saves").AddComponent<StandalonePreviewScene>();
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);Hook();EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;next=EditorApplication.timeSinceStartup+1;deadline=next+60;report.Clear();}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            if(SessionState.GetBool(Key+".hadLocale",false))PlayerPrefs.SetInt(GameLanguage.PreferenceKey,SessionState.GetInt(Key+".locale",0));else PlayerPrefs.DeleteKey(GameLanguage.PreferenceKey);PlayerPrefs.Save();
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
        }
    }
    static void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/TabletPlayValidation.txt",report);if(!ok)throw new Exception(label);}
    static void Tick()
    {
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Tablet Play validation timeout");
            next=EditorApplication.timeSinceStartup+.75;
            switch(phase++)
            {
                case 0:
                    var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.SetPositionAndRotation(new Vector3(0,4,-8),Quaternion.Euler(20,0,0));
                    new GameObject("Game clock").AddComponent<DayNightCycle>();
                    // AutoBootstrap creates services even in isolated preview scenes.
                    // Use those instances; duplicate services make FindAnyObjectByType
                    // resolve different balances in the controller and the tablet.
                    var save=Bootstrapper.Instance.SaveService;money=Bootstrapper.Instance.EconomyService;properties=Bootstrapper.Instance.PropertyService;
                    save.SetCurrent(new SaveData{slotId=1,money=5000,ownedProperties=Array.Empty<int>()});
                    plot=new GameObject("Property 973").AddComponent<BuildPlot>();plot.propertyId=973;plot.price=1250;plot.buildable=true;plot.Volume.size=Vector3.one*50;
                    controller=new GameObject("Player UI test").AddComponent<ConstructionController>();controller.catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);controller.uiPrefab=AssetDatabase.LoadAssetAtPath<BuildMenuView>(TabletSetup.UIPath);
                    // Do not inherit the author's free-construction debug switch:
                    // this fixture must exercise a real price without editing the asset.
                    controller.catalog=UnityEngine.Object.Instantiate(controller.catalog);
                    controller.catalog.developmentFreeConstruction=false;
                    plot.catalog=controller.catalog;GameLanguage.Set(GameLocale.Spanish);break;
                case 1:
                    view=controller.GetComponentInChildren<BuildMenuView>(true);
                    Check(view&&view.tablet&&view.menu.activeInHierarchy&&!view.tablet.IsOpen,"Controller Start keeps docked tablet alive");
                    Check(view.contextPrompt.gameObject.activeSelf&&view.contextPrompt.text.Contains("Comprar"),"Live unowned property shows B Comprar");
                    Check(!view.tablet.timeText.text.Contains("00:00"),"Readout follows the game clock");
                    controller.ContextAction();Check(view.tablet.IsOpen,"Context handler requests opening immediately");next=EditorApplication.timeSinceStartup+2;break;
                case 2:
                    Check(view.tablet.IsOpen&&view.tablet.body.anchoredPosition.sqrMagnitude<.01f&&view.tablet.chrome.alpha>.99f,"Animated tablet reaches screen centre: open="+view.tablet.IsOpen+" position="+view.tablet.body.anchoredPosition+" alpha="+view.tablet.chrome.alpha);
                    Check(view.tablet.SelectedProperty==plot&&view.tablet.SelectedSection==TabletSection.Properties&&money.Money==5000,"Context action opens matching property without charging");
                    view.tablet.purchaseButton.onClick.Invoke();
                    Check(properties.IsOwned(973)&&money.Money==3750,"Runtime confirmation buys once");
                    controller.ContextAction();next=EditorApplication.timeSinceStartup+2;break;
                case 3:
                    Check(!view.tablet.IsOpen&&view.hud.activeInHierarchy&&controller.CapturesInput,"Purchased property enters construction through context handler");
                    Check(view.tablet.chrome.alpha<.01f&&!view.tablet.chrome.blocksRaycasts,"Closed tablet cannot intercept construction clicks");
                    controller.ContextAction();break;
                case 4:
                    Check(view.contextPrompt.gameObject.activeSelf&&view.contextPrompt.text.Contains("Construir"),"Owned property shows B Construir after exit");
                    GameLanguage.Set(GameLocale.English);break;
                case 5:
                    Check(view.contextPrompt.text.Contains("Build")&&view.tablet.bankText.text.Contains("Bank balance"),"Context prompt and docked data update language live");
                    controller.transform.position=Vector3.one*100;break;
                case 6:
                    Check(!view.contextPrompt.gameObject.activeSelf,"Outside a property there is no buy/build prompt");
                    Check(!view.tablet.IsOpen&&view.tablet.dataPanel.gameObject.activeInHierarchy,"Three docked data fields remain visible outside properties");
                    Debug.Log("TABLET_PLAY_VALIDATION_OK checks="+report.Count);SessionState.SetInt(Key+".result",0);EditorApplication.ExitPlaymode();break;
            }
        }
        catch(Exception e){Debug.LogException(e);report.Add(e.ToString());File.WriteAllLines("Documentation/TabletPlayValidation.txt",report);SessionState.SetInt(Key+".result",1);EditorApplication.ExitPlaymode();}
    }
}
#endif
