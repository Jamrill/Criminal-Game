#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Localization;
using JuegoCriminal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class OptionsMenuRepair
{
    const string Options="Assets/Prefabs/UI/OptionsPanel.prefab",Main="Assets/Scenes/01_MainMenu.unity";
    public static void Apply()
    {
        string backup="Documentation/OptionsMenuRepairBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        File.Copy(Options,backup+"/OptionsPanel.prefab");File.Copy(Main,backup+"/01_MainMenu.unity");
        var report=new List<string>{"Backup "+backup};
        var root=PrefabUtility.LoadPrefabContents(Options);
        try
        {
            foreach(var control in root.GetComponentsInChildren<ControlRebindButtonUI>(true))control.RefreshLayout();
            var video=root.GetComponentInChildren<VideoMenuUI>(true);
            var content=video.transform.Find("VideoScrollView/Viewport/Content");
            foreach(Transform row in content)
            {
                var button=row.GetComponentInChildren<Button>(true);
                var caption=row.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>!t.GetComponentInParent<Button>());
                if(button && caption)SettingsRowLayout.Configure(row,caption,button);
            }
            var back=root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Back");
            report.Add("Prefab Back original callbacks="+back.onClick.GetPersistentEventCount());
            var nav=root.GetComponent<OptionsPanelNavigation>()??root.AddComponent<OptionsPanelNavigation>();
            var s=new SerializedObject(nav);s.FindProperty("backButton").objectReferenceValue=back;s.ApplyModifiedPropertiesWithoutUndo();
            // Returning to the owner is now wired once at runtime by explicit reference.
            back.onClick=new Button.ButtonClickedEvent();PrefabUtility.RecordPrefabInstancePropertyModifications(back);
            var accessibility=root.GetComponentsInChildren<Button>(true).Single(b=>b.name.Equals("Accesibility",StringComparison.OrdinalIgnoreCase) || b.name.Equals("Accessibility",StringComparison.OrdinalIgnoreCase));
            var label=accessibility.GetComponentInChildren<TMP_Text>(true);
            var translation=label.GetComponent<LocalizedText>()??label.gameObject.AddComponent<LocalizedText>();
            translation.english="Accessibility";translation.spanish="Accesibilidad";translation.Refresh();
            PrefabUtility.SaveAsPrefabAsset(root,Options);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var scene=EditorSceneManager.OpenScene(Main);
        var main=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<JuegoCriminal.Scenes.MainMenuUI>(true)).Single();
        var panel=(GameObject)new SerializedObject(main).FindProperty("optionsPanel").objectReferenceValue;
        var navigation=panel.GetComponent<OptionsPanelNavigation>();
        report.Add("Main scene Back original callbacks="+navigation.BackButton.onClick.GetPersistentEventCount());
        navigation.BackButton.onClick=new Button.ButtonClickedEvent();PrefabUtility.RecordPrefabInstancePropertyModifications(navigation.BackButton);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllLines("Documentation/OptionsMenuRepair.txt",report);
        Validate();Debug.Log("OPTIONS_MENU_REPAIR_OK");
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Validate()
    {
        var report=new List<string>();
        var canvas=new GameObject("Options validation canvas",typeof(RectTransform),typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Options),canvas.transform,false);
        var original=GameLanguage.Current;
        try
        {
            root.SetActive(true);
            var controls=root.GetComponentInChildren<ControlsMenuUI>(true);controls.OrganizeSections();
            var video=root.GetComponentInChildren<VideoMenuUI>(true);
            typeof(VideoMenuUI).GetMethod("EnsureReflectionsRow",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(video,null);
            foreach(var menu in new[]{controls.gameObject,video.gameObject})
            {
                menu.SetActive(true);Canvas.ForceUpdateCanvases();
                foreach(var scroll in menu.GetComponentsInChildren<ScrollRect>(true))LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Canvas.ForceUpdateCanvases();
                float width=-1;
                foreach(var row in menu.GetComponentsInChildren<SettingsRowLayout>(true))
                {
                    row.Apply();var label=row.label.rectTransform;var button=(RectTransform)row.button.transform;
                    Require(label.rect.width>=100,"Label has insufficient width: "+row.name+" width="+label.rect.width);
                    Require(Mathf.Abs(button.rect.height-45)<.01f,"Button height: "+row.name);
                    Require(width<0 || Mathf.Abs(button.rect.width-width)<.01f,"Inconsistent button width: "+row.name);width=button.rect.width;
                    var labelCorners=new Vector3[4];var buttonCorners=new Vector3[4];label.GetWorldCorners(labelCorners);button.GetWorldCorners(buttonCorners);
                    Require(labelCorners[2].x<buttonCorners[0].x,"Columns overlap: "+row.name);
                    Require(row.label.color==Color.white && row.label.text.Length>0,"Unreadable/missing action label: "+row.name);
                    report.Add("PASS "+row.name+" label="+label.rect.width+" button="+button.rect.size);
                }
                menu.SetActive(false);
            }
            foreach(var language in new[]{GameLocale.English,GameLocale.Spanish})
            {
                GameLanguage.Set(language);
                var text=root.GetComponentsInChildren<LocalizedText>(true).Single(t=>t.english=="Accessibility");text.Refresh();
                Require(text.GetComponent<TMP_Text>().text==(language==GameLocale.Spanish?"Accesibilidad":"Accessibility"),"Accessibility translation");
                report.Add("PASS accessibility "+language);
            }
            var nav=root.GetComponent<OptionsPanelNavigation>();int closed=0;nav.Bind(()=>closed++);
            controls.Open();nav.BackButton.onClick.Invoke();
            Require(closed==1 && !controls.gameObject.activeSelf,"Options Back did not close controls/call owner exactly once");
            video.Open();nav.BackButton.onClick.Invoke();
            Require(closed==2 && !video.gameObject.activeSelf,"Options Back did not close video/call owner exactly once");
            report.Add("PASS explicit Back navigation with controls/video open");
            File.WriteAllLines("Documentation/OptionsMenuValidation.txt",report);
            Debug.Log("OPTIONS_MENU_VALIDATION_OK rows="+(report.Count-3));
        }
        finally{GameLanguage.Set(original);Object.DestroyImmediate(canvas);}
    }
    public static void Regressions(){Validate();ButtonPrefabMigration.Validate();Debug.Log("OPTIONS_MENU_REGRESSIONS_OK");}
}
#endif
