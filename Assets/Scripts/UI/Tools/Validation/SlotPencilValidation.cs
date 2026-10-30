#if UNITY_EDITOR
using System;
using JuegoCriminal.Services;
using JuegoCriminal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

public static class SlotPencilValidation
{
    public static void Run()
    {
        int checks=0;
        void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("PENCIL_PASS "+message);}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("EventSystem",typeof(EventSystem));
        var canvas=new GameObject("Canvas",typeof(Canvas));
        var save=new GameObject("Test save service").AddComponent<SaveService>();
        foreach(string name in new[]{"SlotRow","SlotRowCoop"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<SaveSlotRowUI>("Assets/Prefabs/UI/"+name+".prefab");
            var row=Object.Instantiate(prefab,canvas.transform);
            Check(row.RenameButton&&row.RenameButton.name=="Pencil",name+": serialized Pencil reference");
            row.Init(-1,SlotPanelMode.LoadOnly,save,null); // Invalid slot: never writes any real save.
            row.Refresh(new SaveData{displayName="Original name",slotId=-1});
            var editor=row.NameText.gameObject.AddComponent<SaveSlotNameEditor>();
            editor.Configure(row,save,row.NameText);
            var input=row.NameText.GetComponentInParent<TMP_InputField>();
            Check(input.gameObject!=row.NameText.gameObject&&row.NameText.transform.parent==input.transform,name+": label is inside the input so caret and selection render correctly");
            Check(input.onFocusSelectAll&&input.customCaretColor&&input.caretWidth==row.NameCaretWidth&&input.caretColor==row.NameCaretColor&&input.selectionColor==row.NameSelectionColor,name+": inspector editing palette and select-all configured");
            Check(!input.interactable&&!row.NameText.raycastTarget,name+": name is not editable by clicking text");
            Check(input.characterLimit==20,name+": retains twenty-character limit");
            row.OnPointerDown(new PointerEventData(EventSystem.current){clickCount=2});
            Check(!input.interactable,name+": double click does not start editing");
            int selections=0;row.Clicked+=_=>selections++;
            row.RenameButton.onClick.Invoke();
            Check((row.NameText.fontStyle&FontStyles.Underline)!=0,name+": name underlined while editing");
            Check(input.interactable&&selections==1&&input.text=="Original name",name+": pencil selects row and starts editing current name (enabled="+input.interactable+", selections="+selections+", text="+input.text+")");
            input.onEndEdit.Invoke("");
            Check((row.NameText.fontStyle&FontStyles.Underline)==0,name+": underline removed after editing");
            Check(!input.interactable&&row.NameText.text.TrimEnd('\u200B')=="Original name",name+": invalid name restores previous value and locks input (enabled="+input.interactable+", text="+row.NameText.text+")");
            editor.Configure(row,save,row.NameText);editor.Configure(row,save,row.NameText);
            selections=0;row.RenameButton.onClick.Invoke();
            Check(selections==1,name+": reconfiguration never duplicates pencil listener");
            input.SetTextWithoutNotify("Unconfirmed edit");editor.SetInteractable(false);
            Check(!input.interactable&&!row.RenameButton.interactable&&row.NameText.text.TrimEnd('\u200B')=="Original name",name+": modal lock cancels pending edit");
            row.RenameButton.onClick.Invoke();
            Check(!input.interactable,name+": modal cannot be bypassed by pencil event");
            editor.SetInteractable(true);row.Refresh(new SaveData{displayName="Refreshed name"});
            row.RenameButton.onClick.Invoke();
            Check(input.text=="Refreshed name",name+": starts with refreshed save name, not stale input text");
            row.gameObject.SetActive(false);
            // Ordinary MonoBehaviours do not receive lifecycle callbacks in Edit Mode.
            typeof(SaveSlotNameEditor).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(editor,null);
            Check(!input.interactable,name+": closing row leaves editing disabled");
            Object.DestroyImmediate(row.gameObject);
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Debug.Log("SLOT_PENCIL_VALIDATION_OK checks="+checks);
    }
}

// Focus/selection are deferred by TMP until Play/LateUpdate: verify those in
// an isolated running scene as well as the synchronous checks above.
[InitializeOnLoad]
public static class SlotPencilPlayValidation
{
    const string Key="CriminalGame.SlotPencilPlayValidation";
    static SaveSlotRowUI row;
    static TMP_InputField input;
    static SaveService save;
    static Transform canvas;
    static int index,phase,frame,checks;
    static double deadline;
    static SlotPencilPlayValidation(){if(SessionState.GetBool(Key,false))Hook();}
    static void Hook()
    {
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
    }
    public static void Start()
    {
        SlotPencilValidation.Run();
        new GameObject("Isolated UI test").AddComponent<JuegoCriminal.Core.StandalonePreviewScene>();
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);Hook();EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;index=checks=0;deadline=EditorApplication.timeSinceStartup+45;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;
            EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
        }
    }
    static void Check(bool ok,string message)
    {if(!ok)throw new Exception(message);checks++;Debug.Log("PENCIL_PLAY_PASS "+index+": "+message);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Pencil Play validation timeout");
            if(phase==0)
            {
                new GameObject("EventSystem",typeof(EventSystem));
                var view=new GameObject("Canvas",typeof(Canvas)).GetComponent<Canvas>();view.renderMode=RenderMode.ScreenSpaceOverlay;canvas=view.transform;
                save=new GameObject("Test save service").AddComponent<SaveService>();phase=1;
            }
            if(phase==1)
            {
                string name=index==0?"SlotRow":"SlotRowCoop";
                row=Object.Instantiate(AssetDatabase.LoadAssetAtPath<SaveSlotRowUI>("Assets/Prefabs/UI/"+name+".prefab"),canvas);
                row.GetComponent<RectTransform>().sizeDelta=new Vector2(800,50);
                row.Init(-1,SlotPanelMode.LoadOnly,save,null);row.Refresh(new SaveData{displayName="Test name",slotId=-1});
                var corners=new Vector3[4];row.NameText.rectTransform.GetWorldCorners(corners);
                var editor=row.NameText.gameObject.AddComponent<SaveSlotNameEditor>();editor.Configure(row,save,row.NameText);
                var after=new Vector3[4];row.NameText.rectTransform.GetWorldCorners(after);
                for(int i=0;i<4;i++)Check(Vector3.Distance(corners[i],after[i])<.01f,"Authored name rectangle preserved, corner "+i);
                input=row.NameText.GetComponentInParent<TMP_InputField>();
                row.RenameButton.onClick.Invoke();frame=Time.frameCount;phase=2;
            }
            else if(phase==2&&Time.frameCount>frame+2)
            {
                Check(input.isFocused,"Pencil focuses the input");
                Check(Mathf.Abs(input.selectionStringAnchorPosition-input.selectionStringFocusPosition)==input.text.Length,"Whole name selected after deferred TMP activation");
                Check((row.NameText.fontStyle&FontStyles.Underline)!=0,"Underline remains visible while editing");
                Check(input.GetComponentsInChildren<TMP_SelectionCaret>(true).Length==1,"TMP creates a selection/caret renderer inside the field");
                Canvas.ForceUpdateCanvases();
                var selectionMesh=input.GetComponentInChildren<TMP_SelectionCaret>().canvasRenderer.GetMesh();
                Check(selectionMesh&&selectionMesh.vertexCount>0,"Selection has actual drawable geometry, not just focus state");
                input.SetTextWithoutNotify("Changed");input.caretPosition=input.text.Length;
                Check(input.selectionStringAnchorPosition==input.selectionStringFocusPosition,"Typing can collapse selection to a caret");
                row.gameObject.SetActive(false);
                Check(!input.interactable,"Closing panel exits edit mode in Play");
                Object.Destroy(row.gameObject);index++;
                if(index<2)phase=1;
                else
                {
                    Debug.Log("SLOT_PENCIL_PLAY_VALIDATION_OK checks="+checks);SessionState.SetInt(Key+".result",0);phase=3;
                    EditorApplication.delayCall+=EditorApplication.ExitPlaymode;
                }
            }
        }
        catch(Exception error){Debug.LogException(error);phase=3;EditorApplication.delayCall+=EditorApplication.ExitPlaymode;}
    }
}
#endif
