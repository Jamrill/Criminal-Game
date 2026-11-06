#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using JuegoCriminal.Core;
using JuegoCriminal.Player;
using JuegoCriminal.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MenuCursorPlayValidation
{
    const string Key="MenuCursorPlayValidation",Report="Documentation/MenuCursorPlayValidation.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int phase;static double next,deadline;
    static MainMenuUI menu;static ThirdPersonController player;
    static MenuCursorPlayValidation(){if(SessionState.GetBool(Key,false))Hook();}
    static void Hook(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
    public static void Start()
    {
        File.WriteAllText(Report,"");SessionState.SetBool(Key,true);SessionState.SetInt(Key+".cycle",1);SessionState.SetInt(Key+".checks",0);SessionState.SetInt(Key+".result",1);
        Hook();Open();
    }
    static void Open(){EditorSceneManager.OpenScene("Assets/Scenes/01_MainMenu.unity");EditorApplication.EnterPlaymode();}
    static void State(PlayModeStateChange value)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(value==PlayModeStateChange.EnteredPlayMode){phase=0;next=EditorApplication.timeSinceStartup+1;deadline=next+90;}
        if(value!=PlayModeStateChange.EnteredEditMode)return;
        if(SessionState.GetInt(Key+".result",1)==0 && SessionState.GetInt(Key+".cycle",1)==1)
        {
            SessionState.SetInt(Key+".cycle",2);SessionState.SetInt(Key+".result",1);EditorApplication.delayCall+=Open;return;
        }
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;
        if(SessionState.GetInt(Key+".result",1)==0)Debug.Log("MENU_CURSOR_PLAY_VALIDATION_OK cycles=2 checks="+SessionState.GetInt(Key+".checks",0));
        EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
    }
    static void Check(bool ok,string message)
    {
        File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+"cycle="+SessionState.GetInt(Key+".cycle",1)+" "+message+"\n");
        if(!ok)throw new Exception(message);SessionState.SetInt(Key+".checks",SessionState.GetInt(Key+".checks",0)+1);
    }
    static void SetState(GameState state)=>typeof(GameStateMachine).GetMethod("SetState",Private).Invoke(Bootstrapper.Instance.GameStateMachine,new object[]{state});
    static void Free(string label)=>Check(Cursor.lockState==CursorLockMode.None&&Cursor.visible,label);
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Cursor validation timeout");
            next=EditorApplication.timeSinceStartup+.25;
            var app=Bootstrapper.Instance;
            switch(phase)
            {
                case 0:
                    if(!app || app.GameStateMachine.CurrentState!=GameState.Menu || app.SceneLoader.IsLoading)return;
                    menu=UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();Check(menu,"Real boot reaches MainMenu");
                    Free("Initial menu cursor is visible and unlocked");
                    Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;phase++;break;
                case 1:
                    Free("Menu repairs a stale hidden/locked cursor on next frame");
                    Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
                    typeof(MainMenuUI).GetMethod("OnApplicationFocus",Private).Invoke(menu,new object[]{true});
                    Free("Regaining menu focus restores cursor");
                    player=new GameObject("Late-starting player fixture").AddComponent<ThirdPersonController>();phase++;break;
                case 2:
                    Free("Player Start does not capture cursor from menu");
                    foreach(var state in new[]{GameState.Boot,GameState.Loading,GameState.Menu})
                    {Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;SetState(state);Free(state+" explicitly releases cursor");}
                    SetState(GameState.World);
                    // Batch mode has no focused Game View: the OS/editor can
                    // reject a lock request even though the policy requests it.
                    if(Application.isBatchMode && Cursor.lockState!=CursorLockMode.Locked)
                        File.AppendAllText(Report,"NOTE Headless editor does not grant a physical cursor lock; world lock requires interactive verification.\n");
                    var worldLock=Cursor.lockState;
                    Check(!Cursor.visible && (Application.isBatchMode || worldLock==CursorLockMode.Locked),"World-ready transition hides cursor (lock="+worldLock+")");
                    typeof(MainMenuUI).GetMethod("LateUpdate",Private).Invoke(menu,null);
                    Check(Cursor.lockState==worldLock&&!Cursor.visible,"Menu code does not override world cursor");
                    // Simulate an open gameplay overlay and a player starting late.
                    Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                    typeof(ThirdPersonController).GetMethod("Start",Private).Invoke(player,null);
                    Free("Late player Start preserves overlay cursor in world");
                    SetState(GameState.Loading);SetState(GameState.Menu);phase++;break;
                case 3:
                    Free("Returning to menu leaves cursor visible and unlocked");
                    Check(!app.SaveService.HasCurrentGame,"No save slot loaded or created");
                    UnityEngine.Object.Destroy(player.gameObject);SessionState.SetInt(Key+".result",0);phase=99;EditorApplication.ExitPlaymode();break;
            }
        }
        catch(Exception error){Debug.LogException(error);File.AppendAllText(Report,error+"\n");phase=99;SessionState.SetInt(Key+".result",1);EditorApplication.ExitPlaymode();}
    }
}
#endif
