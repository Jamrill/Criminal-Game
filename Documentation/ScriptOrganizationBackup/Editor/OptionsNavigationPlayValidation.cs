using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using JuegoCriminal.Core;
using JuegoCriminal.Scenes;
using JuegoCriminal.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class OptionsNavigationPlayValidation
{
    const string Key="CriminalGame.OptionsNavigationPlayValidation";
    static int phase;static double deadline,next;
    static MainMenuUI main;static GameObject options;static PauseMenuUI pause;
    static readonly List<string> report=new();
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static OptionsNavigationPlayValidation(){if(SessionState.GetBool(Key,false))Hook();}
    static void Hook(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
    public static void Start()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/01_MainMenu.unity");
        new GameObject("Isolated UI test - no boot or save writes").AddComponent<StandalonePreviewScene>();
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);SessionState.SetFloat(Key+".start",(float)EditorApplication.timeSinceStartup);
        Hook();EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;deadline=EditorApplication.timeSinceStartup+90;next=EditorApplication.timeSinceStartup+3;report.Clear();}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            int result=SessionState.GetInt(Key+".result",1);SessionState.SetBool(Key,false);
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;
            EditorApplication.Exit(result);
        }
    }
    static object Field(object owner,string name)=>owner.GetType().GetField(name,Private).GetValue(owner);
    static void Call(object owner,string name)=>owner.GetType().GetMethod(name,Private).Invoke(owner,null);
    static void Check(bool condition,string message)
    {report.Add((condition?"PASS ":"FAIL ")+message);File.WriteAllLines("Documentation/OptionsNavigationPlayValidation.txt",report);if(!condition)throw new Exception(message);}
    static void ClickBack()
    {
        var button=options.GetComponent<OptionsPanelNavigation>().BackButton;
        Check(button.isActiveAndEnabled && button.IsInteractable(),"Back is interactable");
        Canvas.ForceUpdateCanvases();
        var canvas=button.GetComponentInParent<Canvas>().rootCanvas;
        var position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
        var data=new PointerEventData(EventSystem.current){position=position,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"Back is not covered by another UI raycast target: point="+position+" screen="+Screen.width+"x"+Screen.height+" hits="+string.Join(";",hits.Take(8).Select(h=>AnimationUtility.CalculateTransformPath(h.gameObject.transform,null))));
        button.OnPointerClick(data);
    }
    static void Layout(GameObject menu)
    {
        Canvas.ForceUpdateCanvases();
        var rows=menu.GetComponentsInChildren<SettingsRowLayout>(false);
        Check(rows.Length>0 && rows.All(r=>r.label.rectTransform.rect.width>=100 && r.button.GetComponent<RectTransform>().rect.width>=150),"Runtime action labels and button columns have usable width");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying)return;
        if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Options Play validation timeout at phase "+phase);
            switch(phase)
            {
                case 0:
                    main=UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();Check(main,"Main menu exists");
                    Call(main,"OpenOptions");options=(GameObject)Field(main,"optionsPanel");phase++;next=EditorApplication.timeSinceStartup+3;break;
                case 1:
                    options.GetComponentInChildren<ControlsMenuUI>(true).Open();phase++;next=EditorApplication.timeSinceStartup+.3;break;
                case 2:
                    Layout(options.GetComponentInChildren<ControlsMenuUI>(true).gameObject);ClickBack();phase++;next=EditorApplication.timeSinceStartup+3;break;
                case 3:
                    Check(!options.activeSelf && ((GameObject)Field(main,"mainButtonsPanel")).activeInHierarchy,"Controls Back returns to MainMenu");
                    Call(main,"OpenOptions");phase++;next=EditorApplication.timeSinceStartup+3;break;
                case 4:
                    options.GetComponentInChildren<VideoMenuUI>(true).Open();phase++;next=EditorApplication.timeSinceStartup+.3;break;
                case 5:
                    Layout(options.GetComponentInChildren<VideoMenuUI>(true).gameObject);ClickBack();phase++;next=EditorApplication.timeSinceStartup+3;break;
                case 6:
                    Check(!options.activeSelf && ((GameObject)Field(main,"mainButtonsPanel")).activeInHierarchy,"Video Back returns to MainMenu");
                    SceneManager.LoadScene("10_World_City");phase++;next=EditorApplication.timeSinceStartup+4;break;
                case 7:
                    pause=UnityEngine.Object.FindFirstObjectByType<PauseMenuUI>();Check(pause,"World pause menu exists");
                    Call(pause,"Toggle");Call(pause,"OpenOptions");options=(GameObject)Field(pause,"_optionsPanelInstance");phase++;next=EditorApplication.timeSinceStartup+3;break;
                case 8:
                    Check(Time.timeScale==0,"Pause UI works with time stopped");options.GetComponentInChildren<ControlsMenuUI>(true).Open();phase++;next=EditorApplication.timeSinceStartup+.3;break;
                case 9:
                    Layout(options.GetComponentInChildren<ControlsMenuUI>(true).gameObject);ClickBack();phase++;next=EditorApplication.timeSinceStartup+3;break;
                case 10:
                    Check(!options.activeSelf && ((GameObject)Field(pause,"panel")).activeInHierarchy,"Controls Back returns to PauseMenu");
                    Call(pause,"OpenOptions");phase++;next=EditorApplication.timeSinceStartup+3;break;
                case 11:
                    options.GetComponentInChildren<VideoMenuUI>(true).Open();phase++;next=EditorApplication.timeSinceStartup+.3;break;
                case 12:
                    Layout(options.GetComponentInChildren<VideoMenuUI>(true).gameObject);ClickBack();phase++;next=EditorApplication.timeSinceStartup+3;break;
                case 13:
                    Check(!options.activeSelf && ((GameObject)Field(pause,"panel")).activeInHierarchy,"Video Back returns to PauseMenu");
                    Debug.Log("OPTIONS_NAVIGATION_PLAY_VALIDATION_OK checks="+report.Count);SessionState.SetInt(Key+".result",0);Time.timeScale=1;EditorApplication.ExitPlaymode();break;
            }
        }
        catch(Exception exception)
        {
            Debug.LogException(exception);report.Add(exception.ToString());File.WriteAllLines("Documentation/OptionsNavigationPlayValidation.txt",report);
            SessionState.SetInt(Key+".result",1);Time.timeScale=1;EditorApplication.ExitPlaymode();
        }
    }
}
