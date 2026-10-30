#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using JuegoCriminal.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Explicit, read-only checks against disposable instances. Never runs on startup.
public static class SlotsAndAnimationValidation
{
    public static void Run()
    {
        var report=new List<string>();
        var canvas=new GameObject("Slots validation",typeof(RectTransform),typeof(Canvas));
        try
        {
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SlotsPanel.prefab"),canvas.transform,false);
            foreach(var t in panel.GetComponentsInChildren<Transform>(true))t.gameObject.SetActive(true);
            var serialized=new SerializedObject(panel.GetComponentInChildren<SlotsPanelUI>(true));
            var content=(RectTransform)serialized.FindProperty("content").objectReferenceValue;
            var rows=new List<RectTransform>();
            for(int i=0;i<12;i++)
            {
                string path="Assets/Prefabs/UI/"+(i%2==0?"SlotRow":"SlotRowCoop")+".prefab";
                var row=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),content,false);
                rows.Add(row.GetComponent<RectTransform>());
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            foreach(var row in rows)
            {
                if(Mathf.Abs(row.rect.height-50)>.01f)throw new Exception(row.name+" actual height "+row.rect.height);
                report.Add("PASS "+row.name+" layout height=50");
            }
            float expected=12*50+11*content.GetComponent<VerticalLayoutGroup>().spacing;
            if(content.rect.height<expected)throw new Exception("Content does not grow for scrolling: "+content.rect.height);
            report.Add("PASS content grows for all rows: "+content.rect.height);
        }
        finally{UnityEngine.Object.DestroyImmediate(canvas);}
        string controller="Assets/Animations/PlayerLocomotion.controller";
        string before=File.ReadAllText(controller);
        PlayerAnimationExpansion.Validate();
        if(before!=File.ReadAllText(controller))throw new Exception("Validation changed the Animator asset");
        report.Add("PASS animation validation without regenerating controller");
        File.WriteAllLines("Documentation/SlotsAndAnimationValidation.txt",report);
        Debug.Log("SLOTS_AND_ANIMATION_VALIDATION_OK\n"+string.Join("\n",report));
    }
}
#endif
