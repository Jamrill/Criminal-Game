#if UNITY_EDITOR
using System;
using System.Reflection;
using JuegoCriminal.Audio;
using JuegoCriminal.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AmbientMusicPlayValidation
{
    const string Key="AmbientMusicPlayValidation";
    static int checks;static double deadline;static bool ran;
    static AmbientMusicPlayValidation(){if(SessionState.GetBool(Key,false))Hook();}
    static void Hook(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
    public static void Start()
    {
        // Real boot into MainMenu, but no slot is opened, created or saved.
        EditorSceneManager.OpenScene("Assets/Scenes/01_MainMenu.unity");
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);Hook();EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){checks=0;ran=false;deadline=EditorApplication.timeSinceStartup+90;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.Exit(SessionState.GetInt(Key+".result",1));}
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Debug.Log("MUSIC_PLAY_PASS "+message);}
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||ran)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Music boot timeout");
            var app=Bootstrapper.Instance;if(!app || app.GameStateMachine.CurrentState!=GameState.Menu || app.SceneLoader.IsLoading)return;
            ran=true;
            var director=app.GetComponent<AmbientMusicDirector>();
            Check(director,"Actual Bootstrapper installs music director on @App");
            Check(UnityEngine.Object.FindObjectsByType<AmbientMusicDirector>(FindObjectsSortMode.None).Length==1,"Only one music director after boot reloads MainMenu");
            Check(director.settings&&director.settings.tracks.Length>0&&director.settings.tracks[0].length>50,"Original 53-second composition is configured");
            var original=director.settings;var settings=UnityEngine.Object.Instantiate(original);director.enabled=false;director.settings=settings;
            var random=UnityEngine.Random.state;
            try
            {
                settings.menuFirstDelay=new Vector2(.2f,.2f);settings.worldFirstDelay=new Vector2(.2f,.2f);
                settings.menuSilence=new Vector2(.4f,.8f);settings.worldSilence=new Vector2(1,2);
                settings.calmSecondsBeforeMusic=.3f;settings.fadeSeconds=.1f;settings.volume=.02f;
                var method=typeof(AmbientMusicDirector).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic);
                void Advance(float dt,AmbientMusicDirector.Context context)=>method.Invoke(director,new object[]{dt,context});
                var source=director.GetComponentInChildren<AudioSource>();
                Advance(.1f,AmbientMusicDirector.Context.Menu);Check(!director.IsPlaying,"No immediate track before first menu delay");
                Advance(.11f,AmbientMusicDirector.Context.Menu);Check(director.IsPlaying&&!source.loop&&source.spatialBlend==0,"Menu starts one non-looping 2D music cue");
                Advance(.1f,AmbientMusicDirector.Context.Menu);Check(source.volume>0,"Track fades in");
                source.Stop();Advance(.01f,AmbientMusicDirector.Context.Menu);
                Check(!director.IsPlaying&&director.SecondsUntilNextCue>=.4f&&director.SecondsUntilNextCue<=.8f,"Finished cue schedules random menu silence");
                Advance(.1f,AmbientMusicDirector.Context.Menu);Check(!director.IsPlaying,"Silence does not immediately repeat the track");
                Advance(.1f,AmbientMusicDirector.Context.CalmWorld);Check(!director.IsPlaying,"World requires sustained calm");
                Advance(.21f,AmbientMusicDirector.Context.CalmWorld);Check(director.IsPlaying,"Calm world starts a cue after grace and delay");
                Advance(.1f,AmbientMusicDirector.Context.CalmWorld);Advance(.2f,AmbientMusicDirector.Context.Silent);
                Check(!director.IsPlaying,"Activity/load context fades and stops music");
                settings.musicEnabled=false;Advance(10,AmbientMusicDirector.Context.Menu);Check(!director.IsPlaying,"Global music disable blocks new tracks");
                Check(!app.SaveService.HasCurrentGame,"Boot test did not open or create a save slot");
            }
            finally{UnityEngine.Random.state=random;director.settings=original;UnityEngine.Object.Destroy(settings);}
            Debug.Log("AMBIENT_MUSIC_PLAY_VALIDATION_OK checks="+checks);SessionState.SetInt(Key+".result",0);EditorApplication.ExitPlaymode();
        }
        catch(Exception error){ran=true;Debug.LogException(error);EditorApplication.ExitPlaymode();}
    }
}
#endif
