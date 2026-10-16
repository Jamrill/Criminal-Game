using System;
using System.IO;
using System.Reflection;
using JuegoCriminal.Construction;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ConstructionPreviewValidation
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var go=new GameObject("PreviewTest");
        var controller=go.AddComponent<ConstructionController>();
        var factory=typeof(ConstructionController).GetMethod("CreatePreviewMaterial",BindingFlags.NonPublic|BindingFlags.Instance);
        var material=(Material)factory.Invoke(controller,new object[]{Color.green});
        try
        {
            if(Mathf.Abs(material.GetColor("_BaseColor").a-.55f)>.001f)throw new Exception("Unexpected preview opacity");
            if(material.renderQueue!=(int)RenderQueue.Transparent || !material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))throw new Exception("Transparent surface not configured");
            if(material.GetFloat("_SrcBlend")!=(float)BlendMode.SrcAlpha || material.GetFloat("_DstBlend")!=(float)BlendMode.OneMinusSrcAlpha || material.GetFloat("_ZWrite")!=0)throw new Exception("Incorrect transparent blending");
            controller.previewOpacity=.3f;
            typeof(ConstructionController).GetMethod("UpdatePreviewOpacity",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(controller,new object[]{material});
            if(Mathf.Abs(material.GetColor("_BaseColor").a-.3f)>.001f)throw new Exception("Inspector opacity update failed");
            File.WriteAllText("Documentation/ConstructionPreviewValidation.txt","PASS default opacity 55%\nPASS URP transparent surface and queue\nPASS alpha blending, depth writing disabled\nPASS live Inspector opacity update\n");
            Debug.Log("CONSTRUCTION_PREVIEW_VALIDATION_OK");
        }
        finally { UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(go); }
    }
}
