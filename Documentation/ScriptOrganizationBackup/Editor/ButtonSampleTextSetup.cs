using System;
using System.IO;
using JuegoCriminal.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ButtonSampleTextSetup
{
    const string Path="Assets/Prefabs/UI/Button_sample.prefab";
    public static void Run()
    {
        string backup="Documentation/ButtonSampleBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);File.Copy(Path,backup+"/Button_sample.prefab");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var button=root.GetComponent<Button>();
            var text=root.GetComponentInChildren<TMP_Text>(true);
            var colors=root.GetComponent<ButtonTextStateColors>();
            if(!colors)
            {
                colors=root.AddComponent<ButtonTextStateColors>();
                var serialized=new SerializedObject(colors);
                serialized.FindProperty("normal").colorValue=text.color;
                serialized.FindProperty("highlighted").colorValue=Color.white;
                serialized.FindProperty("labels").arraySize=1;
                serialized.FindProperty("labels").GetArrayElementAtIndex(0).objectReferenceValue=text;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if(button.transition!=Selectable.Transition.SpriteSwap)throw new Exception("Sample no longer uses Sprite Swap");
            PrefabUtility.SaveAsPrefabAsset(root,Path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Validate();
    }
    public static void Validate()
    {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
        try
        {
            var button=root.GetComponent<Button>();var colors=root.GetComponent<ButtonTextStateColors>();
            var text=root.GetComponentInChildren<TMP_Text>(true);var s=new SerializedObject(colors);
            void Check(string field)
            {
                colors.RefreshVisuals(true);
                if(text.color!=s.FindProperty(field).colorValue)throw new Exception("Text state: "+field);
                Debug.Log("BUTTON_TEXT_PASS "+field);
            }
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            Check("normal");colors.OnPointerEnter(pointer);Check("highlighted");
            colors.OnPointerDown(pointer);Check("pressed");colors.OnPointerUp(pointer);
            colors.OnSelect(null);Check("selected");
            button.interactable=false;Check("disabled");
            button.interactable=true;colors.OnDeselect(null);colors.OnPointerExit(pointer);Check("normal");
            var group=root.GetComponent<CanvasGroup>()??root.AddComponent<CanvasGroup>();
            group.interactable=false;Check("disabled");group.interactable=true;Check("normal");
            colors.enabled=false;colors.enabled=true;Check("normal");
            if(button.transition!=Selectable.Transition.SpriteSwap)throw new Exception("Sprite Swap changed");
            Debug.Log("BUTTON_SAMPLE_TEXT_VALIDATION_OK");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
