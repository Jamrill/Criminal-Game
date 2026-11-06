#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using JuegoCriminal.Core;
using JuegoCriminal.Scenes;
using JuegoCriminal.UI;
using JuegoCriminal.Localization;
using JuegoCriminal.Construction;
using TMPro;
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
        SessionState.SetBool(Key+".hadLocale",PlayerPrefs.HasKey(GameLanguage.PreferenceKey));
        SessionState.SetInt(Key+".locale",PlayerPrefs.GetInt(GameLanguage.PreferenceKey,0));
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
            if(SessionState.GetBool(Key+".hadLocale",false))PlayerPrefs.SetInt(GameLanguage.PreferenceKey,SessionState.GetInt(Key+".locale",0));
            else PlayerPrefs.DeleteKey(GameLanguage.PreferenceKey);
            PlayerPrefs.Save();
            int result=SessionState.GetInt(Key+".result",1);SessionState.SetBool(Key,false);
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;
            EditorApplication.Exit(result);
        }
    }
    static object Field(object owner,string name)=>owner.GetType().GetField(name,Private).GetValue(owner);
    static void Call(object owner,string name)=>owner.GetType().GetMethod(name,Private).Invoke(owner,null);
    static void Check(bool condition,string message)
    {report.Add((condition?"PASS ":"FAIL ")+message);File.WriteAllLines("Documentation/OptionsNavigationPlayValidation.txt",report);if(!condition)throw new Exception(message);}
    static void Caption(object owner,string field,string expected)
    {
        var button=(Button)Field(owner,field);
        var label=button.GetComponentInChildren<TMP_Text>(true);
        Check(label.text==expected,owner.GetType().Name+" "+field+" = "+expected+" (actual: "+label.text+")");
    }
    static void LocalizeMain()
    {
        foreach(var locale in new[]{GameLocale.English,GameLocale.Spanish,GameLocale.English})
        {
            GameLanguage.Set(locale);bool es=locale==GameLocale.Spanish;
            Caption(main,"newGameButton",es?"Nueva partida":"New Game");Caption(main,"continueButton",es?"Continuar":"Continue");
            Caption(main,"loadGameButton",es?"Cargar partida":"Load Game");Caption(main,"coopButton",es?"Cooperativo":"Coop");
            Caption(main,"quitButton",es?"Salir":"Quit");
        }
        var dialog=(NewGameDialogUI)Field(main,"newGamePanel");dialog.gameObject.SetActive(true);dialog.Open(true);
        GameLanguage.Set(GameLocale.Spanish);
        Check(((TMP_Text)Field(dialog,"titleText")).text=="Nueva partida cooperativa","Open new-game title updates on language change");
        Caption(dialog,"acceptButton","Aceptar");Caption(dialog,"backButton","Volver");dialog.HidePanelOnly();
        var slots=(SlotsPanelUI)Field(main,"slotsPanel");slots.gameObject.SetActive(true);slots.Open(SlotPanelMode.LoadOnly);
        Caption(slots,"deleteButton","Eliminar");Caption(slots,"loadButton","Cargar");Caption(slots,"backButton","Volver");
        slots.Close();main.ShowMainButtons();GameLanguage.Set(GameLocale.English);
    }
    static void LocalizeControls()
    {
        var menu=options.GetComponentInChildren<ControlsMenuUI>(true);
        GameLanguage.Set(GameLocale.Spanish);
        var content=(RectTransform)Field(menu,"contentRoot");
        Check(content.Find("DrivingControlsHeader").GetComponent<TMP_Text>().text=="CONDUCCIÓN","Separate translated driving section");
        var cancel=content.Find("Binding_CancelPlacement");
        Check(cancel && cancel.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Cancelar colocación"),"Construction cancel is listed in Spanish");
        Check(cancel.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Clic derecho"),"Cancel uses the actual right-click binding");
        Check(cancel.GetSiblingIndex()>content.Find("ConstructionControlsHeader").GetSiblingIndex() && cancel.GetSiblingIndex()<content.Find("DrivingControlsHeader").GetSiblingIndex(),"Cancel belongs to construction, before driving");
        var build=content.Find("Binding_BuildorBuyProperty");
        Check(build && build.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Construir / Comprar propiedad"),"Contextual build/buy action translated in Controls");
        Check(build.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==GameInput.GetBindingDisplayString(GameInputAction.BuildOrBuy,0)),"Build/buy row shows current binding");
        GameLanguage.Set(GameLocale.English);
    }
    static void LocalizeWorld()
    {
        GameLanguage.Set(GameLocale.Spanish);
        Caption(pause,"saveButton","Guardar partida");Caption(pause,"loadButton","Cargar partida");Caption(pause,"quitButton","Salir");
        var tablet=UnityEngine.Object.FindFirstObjectByType<BuildMenuView>(FindObjectsInactive.Include);
        // The isolated UI fixture deliberately does not spawn a saved player.
        // Exercise its real nested UI prefab without loading or writing a save.
        if(!tablet)tablet=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BuildMenuView>("Assets/Prefabs/UI/ConstructionUI.prefab"));
        Check(tablet,"Authored tablet exists");
        Check(tablet.wallsButton.GetComponentInChildren<TMP_Text>(true).text=="Paredes" && tablet.furnitureButton.GetComponentInChildren<TMP_Text>(true).text=="Muebles","Hidden tablet captions follow Spanish");
        var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>(ConstructionSetup.CatalogPath);
        tablet.Populate(catalog,BuildCategory.Walls,_=>{});
        Check(tablet.content.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.StartsWith("Pared\n")),"Tablet catalogue uses Spanish names");
        GameLanguage.Set(GameLocale.English);
        tablet.Populate(catalog,BuildCategory.Walls,_=>{});
        Check(tablet.content.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.StartsWith("Wall\n")),"Tablet catalogue switches back to English");
        var row=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SaveSlotRowUI>("Assets/Prefabs/UI/SlotRow.prefab"));
        row.Refresh(new JuegoCriminal.Services.SaveData{displayName="Options"});LocalizedText.BindMenu(row.gameObject);
        GameLanguage.Set(GameLocale.Spanish);Check(row.NameText.text=="Options","User save names are never translated");
        UnityEngine.Object.Destroy(row.gameObject);GameLanguage.Set(GameLocale.English);
    }
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
                    LocalizeMain();
                    Call(main,"OpenOptions");options=(GameObject)Field(main,"optionsPanel");phase++;next=EditorApplication.timeSinceStartup+3;break;
                case 1:
                    options.GetComponentInChildren<ControlsMenuUI>(true).Open();phase++;next=EditorApplication.timeSinceStartup+.3;break;
                case 2:
                    LocalizeControls();Layout(options.GetComponentInChildren<ControlsMenuUI>(true).gameObject);ClickBack();phase++;next=EditorApplication.timeSinceStartup+3;break;
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
                    Call(pause,"Toggle");LocalizeWorld();
                    Call(pause,"OpenOptions");options=(GameObject)Field(pause,"_optionsPanelInstance");phase++;next=EditorApplication.timeSinceStartup+3;break;
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
#endif
