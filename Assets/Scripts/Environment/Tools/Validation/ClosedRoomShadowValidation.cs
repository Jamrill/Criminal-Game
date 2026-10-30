#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class ClosedRoomShadowValidation
{
    public static void Run()
        => RunChecks(false);
    public static void FinalRun()
        => RunChecks(true);
    static void RunChecks(bool finalOnly)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var wall=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Construction/Incompletes/Wall_incomplete.prefab");
        var floor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Floor.prefab");
        if(finalOnly && Mathf.Abs(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wall_Building.mat").GetFloat("_ShadowReceiverInset")-.05f)>.0001f)throw new Exception("Contact correction not persisted");
        var materials=new List<Material>();
        var white=new Material(Shader.Find("Universal Render Pipeline/Lit"));white.SetFloat("_Smoothness",0);materials.Add(white);
        for(int i=0;i<6;i++)
        {
            Object.Instantiate(wall,new Vector3(472+4*i,101,485),Quaternion.identity);
            Object.Instantiate(wall,new Vector3(496,101,485+4*i),Quaternion.Euler(0,-90,0));
            Object.Instantiate(wall,new Vector3(496-4*i,101,509),Quaternion.Euler(0,180,0));
            Object.Instantiate(wall,new Vector3(472,101,509-4*i),Quaternion.Euler(0,90,0));
            for(int j=0;j<6;j++)
            {
                Object.Instantiate(floor,new Vector3(472+4*i,108,485+4*j),Quaternion.identity);
                Object.Instantiate(floor,new Vector3(472+4*i,101,485+4*j),Quaternion.identity);
            }
        }
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.04f,.04f,.04f);RenderSettings.fog=false;
        RenderSettings.reflectionIntensity=0;
        var light=new GameObject("Diagnostic sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;
        light.shadows=LightShadows.Hard;light.shadowBias=0;light.shadowNormalBias=0;light.shadowNearPlane=.01f;
        light.gameObject.AddComponent<UniversalAdditionalLightData>().usePipelineSettings=false;
        RenderSettings.sun=light;
        var camera=new GameObject("Diagnostic camera").AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.fieldOfView=60;
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;
        var originalPipeline=QualitySettings.renderPipeline;
        UniversalRenderPipelineAsset diagnosticPipeline=null;
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        Directory.CreateDirectory("Documentation/ClosedRoomShadows");
        var measurements=new List<string>();
        try
        {
            for(int mode=0;mode<(finalOnly?1:10);mode++)
            {
                if(mode==1)
                    foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))if(r.name=="Floor_ShadowBlocker")r.enabled=false;
                if(mode==2)
                    foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                        if(r.GetComponentInParent<JuegoCriminal.Construction.WallTopology>())
                            r.sharedMaterials=Array.ConvertAll(r.sharedMaterials,_=>white);
                if(mode==3)
                    foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                        if(r.name=="Floor_ShadowBlocker") { r.enabled=true;r.transform.localScale=new Vector3(4.6f,.4f,4.6f);r.transform.localPosition=new Vector3(2,-.2f,2); }
                if(mode==4)
                    foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                        if(r.GetComponentInParent<JuegoCriminal.Construction.WallTopology>())r.shadowCastingMode=ShadowCastingMode.TwoSided;
                if(mode==5)light.shadowNearPlane=5;
                if(mode==6)
                {
                    light.shadowNearPlane=.01f;
                    var pipeline=Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);diagnosticPipeline=pipeline;
                    pipeline.shadowCascadeCount=1;pipeline.shadowDistance=35;
                    QualitySettings.renderPipeline=pipeline;
                }
                if(mode==7)
                {
                    diagnosticPipeline.msaaSampleCount=1;
                    diagnosticPipeline.shadowCascadeCount=4;diagnosticPipeline.shadowDistance=50;
                }
                if(mode==8 || mode==9)
                {
                    var m=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wall_Building.mat"));
                    materials.Add(m);m.SetFloat("_ShadowReceiverInset",mode==8?.02f:.05f);
                    foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                        if(r.GetComponentInParent<JuegoCriminal.Construction.WallTopology>())r.sharedMaterials=Array.ConvertAll(r.sharedMaterials,_=>m);
                }
                for(int angle=0;angle<(finalOnly?4:2);angle++)
                {
                    light.transform.rotation=Quaternion.Euler(finalOnly?new[]{2f,5f,15f,35f}[angle]:angle==0?5:35,0,0);
                    for(int distance=0;distance<(finalOnly?3:2);distance++)
                    {
                        camera.transform.position=new Vector3(484,104,finalOnly?new[]{505f,499f,490f}[distance]:distance==0?505:490);
                        camera.transform.LookAt(new Vector3(484,107.8f,509));
                        camera.Render();
                        var old=RenderTexture.active;RenderTexture.active=rt;
                        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
                        texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                        int top=Mathf.FloorToInt(camera.WorldToScreenPoint(new Vector3(484,107.99f,508.75f)).y);
                        int bright=0;for(int y=top-22;y<=top;y++)for(int x=450;x<830;x++)if(texture.GetPixel(x,y).grayscale>.2f)bright++;
                        measurements.Add($"mode={mode} angle={angle} distance={distance} brightContactPixels={bright}");
                        if(finalOnly && bright>0)throw new Exception($"Contact leak regression: sun={angle}, camera={distance}, pixels={bright}");
                        if(!finalOnly && mode==0 && angle==0 && distance==1)
                        {
                            var lines=new List<string>();
                            foreach(var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))collider.enabled=false;
                            foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                                if(renderer.shadowCastingMode!=ShadowCastingMode.ShadowsOnly && renderer.GetComponent<MeshFilter>())renderer.gameObject.AddComponent<MeshCollider>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                            Physics.SyncTransforms();
                            for(int y=363;y<374;y++)
                            {
                                var ray=camera.ScreenPointToRay(new Vector3(640.5f,y+.5f));
                                if(Physics.Raycast(ray,out var hit,100))lines.Add($"pixelY={y} color={texture.GetPixel(640,y)} mesh={hit.collider.name} point={hit.point.ToString("F6")} normal={hit.normal} triangle={hit.triangleIndex}");
                            }
                            File.WriteAllLines("Documentation/ClosedRoomShadows/EdgePixelHits.txt",lines);
                        }
                        File.WriteAllBytes($"Documentation/ClosedRoomShadows/{(finalOnly?"final":"mode"+mode)}_angle{angle}_distance{distance}.png",texture.EncodeToPNG());
                        Object.DestroyImmediate(texture);RenderTexture.active=old;
                    }
                    if(angle==0)
                    {
                        camera.transform.position=new Vector3(484,104,480);camera.transform.LookAt(new Vector3(484,104,485));camera.Render();
                        var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1280,720,TextureFormat.RGB24,false);
                        t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();
                        measurements.Add($"mode={mode} sunlitExteriorCenter={t.GetPixel(640,360)}");
                        File.WriteAllBytes($"Documentation/ClosedRoomShadows/mode{mode}_exterior.png",t.EncodeToPNG());Object.DestroyImmediate(t);RenderTexture.active=old;
                    }
                }
            }
            File.WriteAllLines("Documentation/ClosedRoomShadows/"+(finalOnly?"FinalMeasurements":"Measurements")+".txt",measurements);
            if(finalOnly)
            {
                light.enabled=false;
                var spot=new GameObject("Diagnostic spotlight").AddComponent<Light>();spot.type=LightType.Spot;
                spot.range=15;spot.spotAngle=110;spot.innerSpotAngle=80;spot.intensity=30;spot.shadows=LightShadows.Hard;
                spot.transform.position=new Vector3(484,104,506);spot.transform.LookAt(new Vector3(484,101,508.75f));
                var additional=spot.gameObject.AddComponent<UniversalAdditionalLightData>();
                var report=new List<string>();
                for(int test=0;test<2;test++)
                {
                    additional.usePipelineSettings=test==0;spot.shadowBias=.01f;spot.shadowNormalBias=.02f;
                    camera.transform.position=new Vector3(484,103,499);camera.transform.LookAt(new Vector3(484,102,508.75f));camera.Render();
                    var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();
                    foreach(var height in new[]{.03f,.1f,.25f,.5f,1f})
                    {
                        var p=camera.WorldToScreenPoint(new Vector3(484,101+height,508.75f));
                        report.Add($"pipelineBias={additional.usePipelineSettings} height={height} color={t.GetPixel(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y))}");
                    }
                    File.WriteAllBytes($"Documentation/ClosedRoomShadows/spot_bias{test}.png",t.EncodeToPNG());
                    Object.DestroyImmediate(t);RenderTexture.active=old;
                }
                File.WriteAllLines("Documentation/ClosedRoomShadows/SpotMeasurements.txt",report);
            }
            Debug.Log("CLOSED_ROOM_SHADOW_CAPTURES_OK");
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation=async;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);
            QualitySettings.renderPipeline=originalPipeline;if(diagnosticPipeline)Object.DestroyImmediate(diagnosticPipeline);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(var m in materials)Object.DestroyImmediate(m);
        }
    }
}
#endif
