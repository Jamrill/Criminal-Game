#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JuegoCriminal.UI;
using Object=UnityEngine.Object;

public static class ButtonPrefabMigration
{
    static readonly string[] Scenes={"Assets/Scenes/01_MainMenu.unity","Assets/Scenes/10_World_City.unity"};
    const string Large="Assets/Prefabs/UI/Button_sample.prefab",Small="Assets/Prefabs/UI/Button_sample_small.prefab";
    static string[] AssetsInScope()=>AssetDatabase.GetDependencies(Scenes,true).Where(p=>p.EndsWith(".prefab") && p!=Large && p!=Small).ToArray();
    static string Describe(Button b)
    {
        var r=(RectTransform)b.transform;
        return AnimationUtility.CalculateTransformPath(b.transform,b.transform.root)+" | size="+r.sizeDelta+" rect="+r.rect.size+" | source="+AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(b))+" | text="+string.Join(",",b.GetComponentsInChildren<TMP_Text>(true).Select(t=>t.name));
    }
    public static void Inspect()
    {
        var report=new List<string>();
        foreach(string path in AssetsInScope())
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var b in root.GetComponentsInChildren<Button>(true))report.Add(path+" | "+Describe(b));}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(path);
            foreach(var b in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Button>(true)))report.Add(path+" | "+Describe(b));
        }
        File.WriteAllLines("Documentation/ButtonPrefabInspection.txt",report);
        Debug.Log("BUTTON_PREFAB_INSPECTION_OK count="+report.Count);
    }
    static bool IsSmall(Button b)
    {var size=((RectTransform)b.transform).sizeDelta;return Mathf.Abs(size.x-150)<.01f && Mathf.Abs(size.y-45)<.01f;}
    static bool IsSample(Button b)
    {
        var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(b);
        string path=AssetDatabase.GetAssetPath(source);
        return path==Large || path==Small;
    }
    static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
    sealed class Reference
    {public Component owner;public string path;public Object value;}
    sealed class State
    {
        public Transform transform;public string name;public bool active;
        public Vector3 position,scale;public Quaternion rotation;
        public Vector2 min,max,pivot,size,anchorPosition;
        public State(Transform t)
        {
            transform=t;name=t.name;active=t.gameObject.activeSelf;position=t.localPosition;scale=t.localScale;rotation=t.localRotation;
            if(t is RectTransform r){min=r.anchorMin;max=r.anchorMax;pivot=r.pivot;size=r.sizeDelta;anchorPosition=r.anchoredPosition;}
        }
        public void Validate()
        {
            Require(transform && transform.name==name && transform.gameObject.activeSelf==active,"Lost or renamed object: "+name);
            Require(Vector3.Distance(transform.localScale,scale)<.0001f && Quaternion.Angle(transform.localRotation,rotation)<.001f,"Changed transform: "+name);
            if(transform is RectTransform r)
                Require(Vector2.Distance(r.anchorMin,min)<.001f && Vector2.Distance(r.anchorMax,max)<.001f && Vector2.Distance(r.pivot,pivot)<.001f && Vector2.Distance(r.sizeDelta,size)<.001f && Vector2.Distance(r.anchoredPosition,anchorPosition)<.001f,"Changed UI layout: "+name);
            else Require(Vector3.Distance(transform.localPosition,position)<.001f,"Changed position: "+name);
        }
    }
    static List<Reference> CaptureReferences(GameObject root)
    {
        var result=new List<Reference>();
        foreach(var component in root.GetComponentsInChildren<Component>(true))
        {
            if(!component)continue;
            var iterator=new SerializedObject(component).GetIterator();
            while(iterator.Next(true))
            {
                if(iterator.propertyType!=SerializedPropertyType.ObjectReference || iterator.propertyPath.StartsWith("m_Prefab") || iterator.propertyPath=="m_CorrespondingSourceObject")continue;
                if(component is Button && iterator.propertyPath.StartsWith("m_SpriteState"))continue;
                if(component is Image image && image.GetComponent<Button>() && iterator.propertyPath=="m_Sprite")continue;
                result.Add(new Reference{owner=component,path=iterator.propertyPath,value=iterator.objectReferenceValue});
            }
        }
        return result;
    }
    static void ValidateReferences(List<Reference> values)
    {
        foreach(var value in values)
        {
            Require(value.owner,"Lost component: "+value.path);
            var property=new SerializedObject(value.owner).FindProperty(value.path);
            Require(property!=null && property.objectReferenceValue==value.value,"Changed reference: "+value.owner.name+" / "+value.path);
        }
    }
    static void Inherit(Object target,params string[] paths)
    {
        foreach(string path in paths)
        {
            var serialized=new SerializedObject(target);var property=serialized.FindProperty(path);
            if(property!=null && property.prefabOverride)PrefabUtility.RevertPropertyOverride(property,InteractionMode.AutomatedAction);
        }
    }
    static void Convert(Button button,List<string> report,string owner)
    {
        if(IsSample(button))return;
        // Inherited controls are converted once, inside their defining prefab.
        if(PrefabUtility.GetCorrespondingObjectFromSource(button))return;
        var go=button.gameObject;
        string path=IsSmall(button)?Small:Large;
        var originalObjects=go.GetComponentsInChildren<Transform>(true).Select(t=>t.gameObject).ToHashSet();
        var labels=go.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.GetComponentInParent<Button>()==button).ToArray();
        string name=go.name;
        bool keepSlotSprites=go.GetComponent<SaveSlotRowUI>();
        if(keepSlotSprites)
        {
            // Unity forbids replacing a prefab-contents root with an instance:
            // it breaks references/overrides in its consumers. Keep the specialized
            // row prefab and its sprite states, but share the sample's text palette.
            var rowColors=go.GetComponent<ButtonTextStateColors>()??go.AddComponent<ButtonTextStateColors>();
            rowColors.UseStyle(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ButtonTextStateColors>());
            report.Add(owner+" / "+name+" -> shared palette "+path+" (specialized slot prefab preserved)");
            return;
        }
        var settings=new ConvertToPrefabInstanceSettings
        {
            objectMatchMode=ObjectMatchMode.ByHierarchy,changeRootNameToAssetName=false,
            componentsNotMatchedBecomesOverride=true,gameObjectsNotMatchedBecomesOverride=true,recordPropertyOverridesOfMatches=true
        };
        PrefabUtility.ConvertToPrefabInstance(go,AssetDatabase.LoadAssetAtPath<GameObject>(path),settings,InteractionMode.AutomatedAction);
        Require(button && button.gameObject==go,"Conversion replaced button identity: "+name);
        // Keep authored labels, icons and layouts. Sample-only children are not inserted.
        foreach(var t in go.GetComponentsInChildren<Transform>(true).Reverse())
            if(t && !originalObjects.Contains(t.gameObject))Object.DestroyImmediate(t.gameObject);
        if(!keepSlotSprites)
        {
            Inherit(button,"m_Transition","m_Colors","m_SpriteState","m_AnimationTriggers");
            var image=go.GetComponent<Image>();if(image)Inherit(image,"m_Sprite","m_Color","m_Type","m_PreserveAspect");
        }
        var colors=go.GetComponent<ButtonTextStateColors>();
        Require(colors,"Missing sample text colors: "+name);
        Inherit(colors,"normal","highlighted","pressed","selected","disabled","fadeDuration");
        var serializedColors=new SerializedObject(colors);var array=serializedColors.FindProperty("labels");
        array.arraySize=labels.Length;
        for(int i=0;i<labels.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=labels[i];
        serializedColors.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(colors);
        report.Add(owner+" / "+name+" -> "+path+(keepSlotSprites?" (slot sprites preserved)":""));
    }
    static bool Process(GameObject root,string owner,List<string> report)
    {
        var buttons=root.GetComponentsInChildren<Button>(true);
        if(!buttons.Any(b=>!IsSample(b) && !PrefabUtility.GetCorrespondingObjectFromSource(b)))return false;
        var states=root.GetComponentsInChildren<Transform>(true).Select(t=>new State(t)).ToArray();
        var references=CaptureReferences(root);
        var texts=root.GetComponentsInChildren<TMP_Text>(true).Select(t=>(text:t,value:t.text)).ToArray();
        foreach(var b in buttons)Convert(b,report,owner);
        foreach(var state in states)state.Validate();
        ValidateReferences(references);
        foreach(var text in texts)Require(text.text && text.text.text==text.value,"Changed text: "+text.value);
        return true;
    }
    public static void Apply()
    {
        var assets=AssetsInScope();
        string backup="Documentation/ButtonPrefabBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        foreach(string path in assets.Concat(Scenes))
        {string destination=backup+"/"+path;Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));File.Copy(path,destination);}
        var report=new List<string>{"Backup: "+backup};
        // Dependencies before containers: Player inherits the already converted ConstructionUI.
        foreach(string path in assets.OrderBy(p=>AssetDatabase.GetDependencies(p,true).Length))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try{if(Process(root,path,report))PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(path);
            bool changed=false;
            foreach(var root in scene.GetRootGameObjects())changed|=Process(root,path,report);
            if(changed)EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines("Documentation/ButtonPrefabMigration.txt",report);
        Debug.Log("BUTTON_PREFAB_MIGRATION_OK count="+(report.Count-1));
        Inspect();
    }
    public static void Finish()
    {
        foreach(string path in new[]{"Assets/Prefabs/UI/OptionsPanel.prefab","Assets/Prefabs/UI/ConstructionUI.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if(component is ControlsMenuUI || component is VideoMenuUI || component is JuegoCriminal.Construction.BuildMenuView)
                    {
                        var serialized=new SerializedObject(component);
                        serialized.FindProperty("buttonTemplate").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Large).GetComponent<Button>();
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Validate();
    }
    static void ValidateButton(Button button,List<string> report)
    {
        string expected=IsSmall(button)?Small:Large;
        var colors=button.GetComponent<ButtonTextStateColors>();
        Require(colors,"Missing text-state component: "+button.name);
        if(button.GetComponent<SaveSlotRowUI>())
        {
            var style=new SerializedObject(colors).FindProperty("styleSource").objectReferenceValue;
            Require(style==AssetDatabase.LoadAssetAtPath<GameObject>(Large).GetComponent<ButtonTextStateColors>(),"Slot palette source");
            return;
        }
        Require(IsSample(button),"Button not connected to sample: "+button.name);
        var original=PrefabUtility.GetCorrespondingObjectFromOriginalSource(button);
        Require(AssetDatabase.GetAssetPath(original)==expected,"Wrong button template: "+button.name);
        var serialized=new SerializedObject(colors);
        foreach(string field in new[]{"normal","highlighted","pressed","selected","disabled","fadeDuration"})
            Require(!serialized.FindProperty(field).prefabOverride,"Text palette overridden: "+button.name+" / "+field);
        Require(button.transition==Selectable.Transition.SpriteSwap,"Sprite Swap not inherited: "+button.name);
        report.Add("PASS "+button.name+" -> "+expected);
    }
    public static void Validate()
    {
        var report=new List<string>();
        foreach(string path in AssetsInScope())
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var b in root.GetComponentsInChildren<Button>(true))ValidateButton(b,report);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(path);
            foreach(var button in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Button>(true)))ValidateButton(button,report);
        }
        var construction=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ConstructionUI.prefab"));
        var canvas=new GameObject("Runtime button checks",typeof(RectTransform),typeof(Canvas));
        try
        {
            var view=construction.GetComponent<JuegoCriminal.Construction.BuildMenuView>();
            var built=JuegoCriminal.Construction.BuildMenuView.Button("TEST",view.content);
            Require(built.GetComponent<ButtonTextStateColors>() && built.transition==Selectable.Transition.SpriteSwap,"Dynamic construction button lost style");
            var options=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/OptionsPanel.prefab"),canvas.transform,false);
            var controls=options.GetComponentInChildren<ControlsMenuUI>(true);
            controls.OrganizeSections();
            foreach(var button in controls.GetComponentsInChildren<Button>(true))
                Require(button.GetComponent<ButtonTextStateColors>() && button.transition==Selectable.Transition.SpriteSwap,"Dynamic control not styled: "+button.name);
            var video=options.GetComponentInChildren<VideoMenuUI>(true);
            var data=new SerializedObject(video);
            var source=(Button)data.FindProperty("antialiasingButton").objectReferenceValue;
            var method=typeof(VideoMenuUI).GetMethod("ShowChoices",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            method.Invoke(video,new object[]{source,new List<string>{"One","Two","Three"},0,(Action<int>)(_=>{})});
            var popup=canvas.transform.Find("VideoDropdown");
            Require(popup,"No video dropdown");
            var choices=popup.GetComponentsInChildren<Button>(true).Where(b=>b.name.StartsWith("Option_")).ToArray();
            Require(choices.Length==3 && choices.All(b=>b.GetComponent<ButtonTextStateColors>() && b.transition==Selectable.Transition.SpriteSwap),"Dynamic dropdown options not styled");
            report.Add("PASS dynamic construction, controls and video dropdowns use sample");
        }
        finally{Object.DestroyImmediate(construction);Object.DestroyImmediate(canvas);}
        ButtonSampleTextSetup.Validate();
        SlotsAndAnimationValidation.Run();
        File.WriteAllLines("Documentation/ButtonPrefabValidation.txt",report);
        Debug.Log("BUTTON_PREFAB_VALIDATION_OK checks="+report.Count);
    }
}
#endif
