#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using JuegoCriminal.Localization;
using JuegoCriminal.UI;

public static class ConstructionUIRevision
{
    const string UIPath="Assets/Prefabs/UI/ConstructionUI.prefab",PlayerPath="Assets/Prefabs/Player.prefab",OptionsPath="Assets/Prefabs/UI/OptionsPanel.prefab";
    sealed class RectState
    {
        public Vector2 min,max,pivot,size,pos;
        public Vector3 scale;
        public Quaternion rotation;
        public RectState(RectTransform rect){min=rect.anchorMin;max=rect.anchorMax;pivot=rect.pivot;size=rect.sizeDelta;pos=rect.anchoredPosition;scale=rect.localScale;rotation=rect.localRotation;}
        public void Restore(RectTransform rect){rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=pos;rect.localScale=scale;rect.localRotation=rotation;}
    }
    static Dictionary<string,RectState> Capture(Transform root)=>root.GetComponentsInChildren<RectTransform>(true).ToDictionary(r=>AnimationUtility.CalculateTransformPath(r,root),r=>new RectState(r));
    static void Restore(Transform root,Dictionary<string,RectState> state)
    {foreach(var pair in state){var t=pair.Key.Length==0?root:root.Find(pair.Key);if(t)pair.Value.Restore((RectTransform)t);}}
    static void Configure(BuildMenuView view)
    {
        var state=Capture(view.transform);
        foreach(var target in new[]{view.menu,view.hud,view.propertiesPanel.gameObject})
            foreach(var layout in target.GetComponents<LayoutGroup>())Object.DestroyImmediate(layout,true);
        Restore(view.transform,state);
        var content=(RectTransform)view.content;
        foreach(var layout in content.GetComponents<LayoutGroup>())if(!(layout is GridLayoutGroup))Object.DestroyImmediate(layout,true);
        content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
        content.sizeDelta=new Vector2(0,content.sizeDelta.y);content.anchoredPosition=Vector2.zero;
        var grid=content.GetComponent<GridLayoutGroup>();if(!grid)grid=content.gameObject.AddComponent<GridLayoutGroup>();
        grid.spacing=new Vector2(8,8);grid.padding=new RectOffset(4,4,4,4);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;
        var responsive=content.GetComponent<BuildCatalogGrid>();if(!responsive)responsive=content.gameObject.AddComponent<BuildCatalogGrid>();responsive.Refresh();
        var fitter=content.GetComponent<ContentSizeFitter>();if(!fitter)fitter=content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        void Label(Button button,string value){if(button)button.GetComponentInChildren<TMP_Text>(true).text=value;}
        Label(view.buildButton,"Build");Label(view.wallsButton,"Walls");Label(view.furnitureButton,"Furniture");
        Label(view.finishButton,"Finish / paint");Label(view.propertiesButton,"Properties");Label(view.hudPropertiesButton,"Properties");
        var title=view.menu.transform.Find("Label");if(title)title.GetComponent<TMP_Text>().text="Tablet · Tab to close";
        view.status.text="R: rotate · Wheel: distance\nEsc: exit";
        view.propertiesPanel.message.text="Properties";
    }
    public static void Apply()
    {
        string backup="Documentation/ConstructionUIBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
        foreach(string path in new[]{UIPath,PlayerPath,OptionsPath})File.Copy(path,Path.Combine(backup,Path.GetFileName(path)));
        Dictionary<string,RectState> embedded;
        var player=PrefabUtility.LoadPrefabContents(PlayerPath);
        try{embedded=Capture(player.GetComponentInChildren<BuildMenuView>(true).transform);}
        finally{PrefabUtility.UnloadPrefabContents(player);}
        var ui=PrefabUtility.LoadPrefabContents(UIPath);
        try{Configure(ui.GetComponent<BuildMenuView>());PrefabUtility.SaveAsPrefabAsset(ui,UIPath);}
        finally{PrefabUtility.UnloadPrefabContents(ui);}
        player=PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var view=player.GetComponentInChildren<BuildMenuView>(true);Restore(view.transform,embedded);Configure(view);
            PrefabUtility.SaveAsPrefabAsset(player,PlayerPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(player);}
        var options=PrefabUtility.LoadPrefabContents(OptionsPath);
        try
        {
            if(!options.GetComponentInChildren<LanguageSettingsUI>(true))
            {
                var row=new GameObject("LanguageSettings",typeof(RectTransform),typeof(LanguageSettingsUI));row.transform.SetParent(options.transform,false);
                var rect=(RectTransform)row.transform;rect.anchorMin=rect.anchorMax=Vector2.one;rect.pivot=Vector2.one;rect.sizeDelta=new Vector2(440,44);rect.anchoredPosition=new Vector2(-40,-30);
                var settings=row.GetComponent<LanguageSettingsUI>();
                settings.label=BuildMenuView.Text("Language",row.transform);settings.label.alignment=TextAlignmentOptions.MidlineLeft;
                var label=settings.label.rectTransform;label.anchorMin=Vector2.zero;label.anchorMax=new Vector2(.4f,1);label.offsetMin=label.offsetMax=Vector2.zero;
                var dropdown=TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());dropdown.name="LanguageDropdown";dropdown.transform.SetParent(row.transform,false);
                var dr=(RectTransform)dropdown.transform;dr.anchorMin=new Vector2(.4f,0);dr.anchorMax=Vector2.one;dr.offsetMin=dr.offsetMax=Vector2.zero;
                settings.dropdown=dropdown.GetComponent<TMP_Dropdown>();settings.dropdown.ClearOptions();settings.dropdown.AddOptions(new List<string>{"English","Español"});settings.dropdown.value=0;
                foreach(var text in dropdown.GetComponentsInChildren<TMP_Text>(true)){text.font=TMP_Settings.defaultFontAsset;text.fontSize=20;}
            }
            PrefabUtility.SaveAsPrefabAsset(options,OptionsPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(options);}
        AssetDatabase.SaveAssets();File.WriteAllText("Documentation/ConstructionUIRevision.txt","Backup="+backup+"\nPreserved authored tablet/HUD sizes and embedded Player RectTransform overrides.\nRemoved outer layout groups; catalogue only uses a two-column grid.\nLanguage dropdown added to shared OptionsPanel.\n");
        Validate();
    }
    public static void Validate()
    {
        var report=new List<string>();
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines("Documentation/ConstructionUIValidation.txt",report);if(!ok)throw new Exception(label);}
        bool hadPreference=PlayerPrefs.HasKey(GameLanguage.PreferenceKey);int preference=PlayerPrefs.GetInt(GameLanguage.PreferenceKey,0);
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        var source=asset.GetComponentInChildren<BuildMenuView>(true);
        var root=Object.Instantiate(source.gameObject);var view=root.GetComponent<BuildMenuView>();
        try
        {
            var menu=(RectTransform)view.menu.transform;var hud=(RectTransform)view.hud.transform;
            var menuSize=menu.sizeDelta;var hudSize=hud.sizeDelta;
            view.EnsurePropertiesUI();view.SetPropertiesVisible(true);view.SetPropertiesVisible(false);view.SetPropertiesVisible(true);
            Check(menu.sizeDelta==menuSize && hud.sizeDelta==hudSize,"Opening Properties preserves authored tablet and HUD dimensions: "+menuSize+" / "+hudSize);
            Check(!view.menu.GetComponent<LayoutGroup>() && !view.hud.GetComponent<LayoutGroup>() && !view.propertiesPanel.GetComponent<LayoutGroup>(),"Tablet, HUD and property panel have no automatic layout groups");
            Check(asset.GetComponent<ConstructionController>().embeddedUI==source,"ConstructionController uses the UI nested inside Player, not a second instance");
            var catalog=AssetDatabase.LoadAssetAtPath<BuildCatalog>("Assets/ConstructionCatalog.asset");
            GameLanguage.Set(GameLocale.English);
            foreach(var category in new[]{BuildCategory.Walls,BuildCategory.Furniture})
            {
                view.Populate(catalog,category,_=>{});view.content.GetComponent<BuildCatalogGrid>().Refresh();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)view.content);
                var grid=view.content.GetComponent<GridLayoutGroup>();
                Check(grid.constraintCount==2 && grid.constraint==GridLayoutGroup.Constraint.FixedColumnCount && grid.cellSize.x>50,category+" catalogue uses two responsive columns");
                Check(view.content.childCount>2 && view.content.GetChild(0).localPosition.x!=view.content.GetChild(1).localPosition.x,category+" cards occupy separate columns");
            }
            Check(ConstructionText.Title(catalog.entries.First(e=>e.prefab && e.prefab.name=="Chair"))=="Chair","Catalogue displays readable English names");
            // In Edit mode OnEnable may not run, so invoke the same event handler explicitly.
            GameLanguage.Set(GameLocale.Spanish);typeof(BuildMenuView).GetMethod("RefreshLanguage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(view,null);
            Check(view.wallsButton.GetComponentInChildren<TMP_Text>().text=="Paredes","Language change updates construction navigation");
            Check(ConstructionText.Title(catalog.entries.First(e=>e.prefab && e.prefab.name=="Chair"))=="Silla","Language change updates catalogue names without modifying definitions");
            Check(GameLanguage.Current==GameLocale.Spanish && PlayerPrefs.GetInt(GameLanguage.PreferenceKey)==1,"Language selection is persisted");
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(OptionsPath).GetComponentInChildren<LanguageSettingsUI>(true).dropdown.options.Count==2,"Shared Options prefab includes English/Spanish dropdown");
        }
        finally
        {
            Object.DestroyImmediate(root);GameLanguage.Set((GameLocale)Mathf.Clamp(preference,0,1));
            if(hadPreference)PlayerPrefs.SetInt(GameLanguage.PreferenceKey,preference);else PlayerPrefs.DeleteKey(GameLanguage.PreferenceKey);PlayerPrefs.Save();
        }
        Debug.Log("CONSTRUCTION_UI_VALIDATION_OK checks="+report.Count);
    }
    public static void Finish()
    {
        var options=PrefabUtility.LoadPrefabContents(OptionsPath);
        try
        {
            var captions=new (string source,string english,string spanish)[]{
                ("Resolucion","Resolution","Resolución"),("Modo de pantalla","Display mode","Modo de pantalla"),
                ("Perfil","Quality preset","Perfil de calidad"),("Calidad SMAA","SMAA quality","Calidad SMAA"),
                ("Reflejos","Reflections","Reflejos"),("Controls","Controls","Controles"),("Video","Video","Vídeo"),
                ("Audio","Audio","Audio"),("Game","Game","Juego"),("Back","Back","Volver"),
                ("Reset Bindings","Reset bindings","Restablecer controles"),("Reset","Reset","Restablecer")
            };
            foreach(var text in options.GetComponentsInChildren<TMP_Text>(true))
            foreach(var pair in captions)
                if(text.text==pair.source || text.text==pair.english)
                {
                    var local=text.GetComponent<LocalizedText>();if(!local)local=text.gameObject.AddComponent<LocalizedText>();
                    local.english=pair.english;local.spanish=pair.spanish;text.text=pair.english;break;
                }
            PrefabUtility.SaveAsPrefabAsset(options,OptionsPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(options);}
        AssetDatabase.SaveAssets();Validate();
        WallFamilyValidation.Full();ConstructionValueValidation.Run();
        Debug.Log("CONSTRUCTION_UI_REVISION_COMPLETE");
    }
    public static void Inspect()
    {
        var report=new List<string>();
        foreach(var path in new[]{"Assets/Prefabs/UI/ConstructionUI.prefab","Assets/Prefabs/Player.prefab","Assets/Prefabs/UI/OptionsPanel.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                report.Add(path);
                foreach(var rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if(path.Contains("Player") && !rect.GetComponentInParent<BuildMenuView>())continue;
                    string hierarchy=AnimationUtility.CalculateTransformPath(rect,root.transform);
                    var text=rect.GetComponent<TMP_Text>();
                    report.Add(hierarchy+" size="+rect.sizeDelta+" pos="+rect.anchoredPosition+" anchors="+rect.anchorMin+"/"+rect.anchorMax+" pivot="+rect.pivot+" components="+string.Join(",",rect.GetComponents<Component>().Select(c=>c?c.GetType().Name:"missing"))+(text?" text="+text.text:""));
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        File.WriteAllLines("Documentation/ConstructionUIInspection.txt",report);
        Debug.Log("CONSTRUCTION_UI_INSPECTION_OK");
    }
}
#endif
