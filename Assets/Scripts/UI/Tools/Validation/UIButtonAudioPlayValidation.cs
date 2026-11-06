#if UNITY_EDITOR
using System;
using System.Reflection;
using JuegoCriminal.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class UIButtonAudioPlayValidation
{
    const string Key="UIButtonAudioPlayValidation";
    static int phase,checks;static double next,deadline;
    static Button button;static UIButtonAudio feedback;static bool oldEnabled;static float oldVolume;
    static CanvasGroup fadeGroup;static float disappearStamp;
    static UIButtonAudioPlayValidation(){if(SessionState.GetBool(Key,false))Hook();}
    static void Hook(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
    public static void Start()
    {
        UIButtonAudioSetup.Validate();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Isolated sound validation").AddComponent<JuegoCriminal.Core.StandalonePreviewScene>();
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);Hook();EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=checks=0;next=EditorApplication.timeSinceStartup+1;deadline=next+40;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.Exit(SessionState.GetInt(Key+".result",1));}
    }
    static object Field(string name)=>typeof(UIButtonSoundPlayer).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Debug.Log("UI_AUDIO_PLAY_PASS "+message);}
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("UI audio validation timeout");
            next=EditorApplication.timeSinceStartup+.4;
            switch(phase++)
            {
                case 0:
                    var s=UIButtonSoundPlayer.Settings;Check(s && s.appear && s.hover && s.click,"Runtime loads shared settings and all three WAVs");
                    oldEnabled=s.soundEnabled;oldVolume=s.masterVolume;s.soundEnabled=true;s.masterVolume=.5f;
                    var canvas=new GameObject("Canvas",typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                    new GameObject("Test audio listener").AddComponent<AudioListener>();
                    new GameObject("EventSystem",typeof(EventSystem));
                    button=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Button>("Assets/Prefabs/UI/Button_sample.prefab"),canvas.transform);
                    ((RectTransform)button.transform).anchoredPosition=Vector2.zero;feedback=button.GetComponent<UIButtonAudio>();
                    Check(feedback,"Shared button prefab carries feedback");break;
                case 1:
                    var source=(AudioSource)Field("source");
                    Check(source && (float)Field("lastAppear")>=0,"Visible button triggers an appearance cue");
                    Check(source.spatialBlend==0 && source.ignoreListenerPause && !source.playOnAwake,"UI audio is 2D and works in paused menus");
                    UIButtonSoundPlayer.Play(UIButtonSoundPlayer.Cue.Appear);float stamp=(float)Field("lastAppear");
                    UIButtonSoundPlayer.Play(UIButtonSoundPlayer.Cue.Appear);Check((float)Field("lastAppear")==stamp,"Simultaneous appearances coalesce");
                    button.interactable=false;stamp=(float)Field("lastHover");feedback.OnPointerEnter(new PointerEventData(EventSystem.current));
                    Check((float)Field("lastHover")==stamp,"Disabled button does not play hover");
                    button.interactable=true;feedback.OnPointerEnter(new PointerEventData(EventSystem.current));
                    Check((float)Field("lastHover")>=0,"Pointer hover dispatches metal cue");
                    UIButtonSoundPlayer.Settings.soundEnabled=false;stamp=(float)Field("lastHover");feedback.OnPointerEnter(new PointerEventData(EventSystem.current));
                    Check((float)Field("lastHover")==stamp,"Global disable silences cues");UIButtonSoundPlayer.Settings.soundEnabled=true;
                    Time.timeScale=0;source.Stop();button.onClick.Invoke();
                    Check(source.isPlaying,"Button click actually starts playback while time is paused");
                    UnityEngine.Object.Destroy(button.gameObject);break;
                case 2:
                    Check((AudioSource)Field("source"),"Click source survives closing/destroying the button");
                    Check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==1,"Buttons share one source");
                    Check((float)Field("lastDisappear")>=0,"Disabling/destroying a visible button plays exit whoosh");
                    disappearStamp=(float)Field("lastDisappear");
                    var root=UnityEngine.Object.FindAnyObjectByType<Canvas>();
                    var groupObject=new GameObject("Fade group",typeof(RectTransform),typeof(CanvasGroup));groupObject.transform.SetParent(root.transform,false);
                    fadeGroup=groupObject.GetComponent<CanvasGroup>();
                    button=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Button>("Assets/Prefabs/UI/Button_sample.prefab"),groupObject.transform);
                    ((RectTransform)button.transform).anchoredPosition=Vector2.zero;break;
                case 3:
                    fadeGroup.alpha=0;break;
                case 4:
                    Check((float)Field("lastDisappear")>disappearStamp,"CanvasGroup fade-out also triggers exit without disabling the object");
                    Debug.Log("UI_BUTTON_AUDIO_PLAY_VALIDATION_OK checks="+checks);SessionState.SetInt(Key+".result",0);Finish();break;
            }
        }
        catch(Exception error){Debug.LogException(error);Finish();}
    }
    static void Finish()
    {
        Time.timeScale=1;var s=UIButtonSoundPlayer.Settings;if(s){s.soundEnabled=oldEnabled;s.masterVolume=oldVolume;}
        phase=99;EditorApplication.ExitPlaymode();
    }
}
#endif
