#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class InteriorLightContactReview
{
    public static void Run()
        => RunCore(false,"mode");
    public static void SunBefore() => RunCore(true,"sun_before");
    public static void SunAfter() => RunCore(true,"sun_after");
    static void RunCore(bool sunOnly,string prefix)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        var lines=new List<string>();
        foreach(var l in lights)
        {
            var additional=l.GetComponent<UniversalAdditionalLightData>();
            lines.Add($"{l.name} type={l.type} pos={l.transform.position} angle={l.spotAngle} range={l.range} intensity={l.intensity} shadow={l.shadows} bias={l.shadowBias} normal={l.shadowNormalBias} pipeline={additional?.usePipelineSettings}");
            if(l.type==LightType.Directional)l.enabled=false;
        }
        var enabledLights=lights.Where(l=>l.enabled && l.type!=LightType.Directional).ToArray();
        if(sunOnly)
        {
            foreach(var l in enabledLights)l.enabled=false;
            var sun=lights.First(l=>l.type==LightType.Directional && l.name!="Moon");
            sun.enabled=true;sun.intensity=1;sun.transform.rotation=Quaternion.Euler(8,-60,0);
        }
        var oldShadows=enabledLights.Select(l=>l.shadows).ToArray();
        var camera=new GameObject("Contact diagnostic camera").AddComponent<Camera>();camera.enabled=false;
        camera.fieldOfView=60;camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;var previous=RenderTexture.active;
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        Directory.CreateDirectory("Documentation/InteriorLightContacts");
        var temporary=new List<Material>();
        try
        {
            for(int mode=0;mode<(sunOnly?1:4);mode++)
            {
                if(mode==1)foreach(var l in enabledLights)l.shadows=LightShadows.None;
                if(mode==2)
                    for(int i=0;i<enabledLights.Length;i++)
                    {
                        var l=enabledLights[i];l.shadows=oldShadows[i];l.shadowBias=.01f;l.shadowNormalBias=.02f;
                        var data=l.GetComponent<UniversalAdditionalLightData>();if(data)data.usePipelineSettings=false;
                    }
                if(mode==3)
                    foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    {
                        var mats=r.sharedMaterials;
                        for(int i=0;i<mats.Length;i++)if(mats[i] && mats[i].shader.name=="CriminalGame/World Wall")
                        {
                            var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));temporary.Add(m);
                            m.SetColor("_BaseColor",mats[i].GetColor("_BaseColor"));m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.15f);mats[i]=m;
                        }
                        r.sharedMaterials=mats;
                    }
                for(int view=0;view<2;view++)
                {
                    camera.transform.position=new Vector3(482,103,view==0?505:497);
                    camera.transform.LookAt(new Vector3(482,102.5f,508.75f));camera.Render();RenderTexture.active=rt;
                    var t=new Texture2D(1280,720,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();
                    File.WriteAllBytes($"Documentation/InteriorLightContacts/{prefix}{(sunOnly?"":mode.ToString())}_view{view}.png",t.EncodeToPNG());
                    foreach(float y in new[]{101.02f,101.05f,101.1f,101.25f,101.5f,102f})
                    {
                        var p=camera.WorldToScreenPoint(new Vector3(482,y,508.75f));
                        lines.Add($"mode={mode} view={view} wallY={y} screen={p} color={t.GetPixel(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y))}");
                    }
                    if(sunOnly && prefix=="sun_after")
                    {
                        var before=new Texture2D(2,2);before.LoadImage(File.ReadAllBytes($"Documentation/InteriorLightContacts/sun_before_view{view}.png"));
                        foreach(float x in new[]{486f,487f})foreach(float y in new[]{101.02f,101.04f,101.08f,101.15f,101.3f})
                        {
                            var p=camera.WorldToScreenPoint(new Vector3(x,y,508.75f));int px=Mathf.RoundToInt(p.x),py=Mathf.RoundToInt(p.y);
                            lines.Add($"BASE_COMPARE view={view} world=({x},{y}) before={before.GetPixel(px,py)} after={t.GetPixel(px,py)}");
                        }
                        Object.DestroyImmediate(before);
                    }
                    Object.DestroyImmediate(t);
                }
            }
            File.WriteAllLines("Documentation/InteriorLightContacts/"+(sunOnly?prefix:"Report")+".txt",lines);
            Debug.Log("INTERIOR_LIGHT_CONTACT_COMPARISON_OK");
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation=async;RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);foreach(var m in temporary)Object.DestroyImmediate(m);
        }
    }
}
#endif
