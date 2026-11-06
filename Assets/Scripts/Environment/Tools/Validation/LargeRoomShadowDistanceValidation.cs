#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JuegoCriminal.Construction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

// Isolated rendered comparison; never saves the user's scene or pipeline asset.
public static class LargeRoomShadowDistanceValidation
{
    public static void Run()
        =>RunChecks(false);
    public static void CornerSeams()=>RunChecks(true);
    public static void CornerSeamsFinal(){RunChecks(true,true);WallBaseShadowValidation.Run();}
    static void RunChecks(bool seams,bool verify=false)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var wall=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Construction/Incompletes/Wall_incomplete.prefab");
        var floor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Floor.prefab");
        var original=QualitySettings.renderPipeline;
        var pipeline=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset"));
        QualitySettings.renderPipeline=pipeline;
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var camera=new GameObject("Corner camera").AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.1f;camera.farClipPlane=150;camera.fieldOfView=65;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var rt=new RenderTexture(640,360,24);camera.targetTexture=rt;
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1;sun.shadows=LightShadows.Hard;
        sun.shadowBias=.01f;sun.shadowNormalBias=.02f;sun.shadowNearPlane=.01f;
        sun.gameObject.AddComponent<UniversalAdditionalLightData>().usePipelineSettings=false;
        RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.04f,.04f,.04f);RenderSettings.reflectionIntensity=0;RenderSettings.fog=false;RenderSettings.skybox=null;
        var report=new List<string>();string dir=seams?"Documentation/CornerSeamDiagnosis":"Documentation/LargeRoomShadowDistance";Directory.CreateDirectory(dir);
        Texture2D Capture(string name)
        {
            camera.Render();camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;
            var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();RenderTexture.active=previous;
            File.WriteAllBytes(dir+"/"+name+".png",texture.EncodeToPNG());return texture;
        }
        try
        {
            for(int i=0;i<11;i++)
            {
                Object.Instantiate(wall,new Vector3(472+4*i,101,485),Quaternion.identity);
                Object.Instantiate(wall,new Vector3(516,101,485+4*i),Quaternion.Euler(0,-90,0));
                Object.Instantiate(wall,new Vector3(516-4*i,101,529),Quaternion.Euler(0,180,0));
                Object.Instantiate(wall,new Vector3(472,101,529-4*i),Quaternion.Euler(0,90,0));
                for(int j=0;j<11;j++)
                {
                    Object.Instantiate(floor,new Vector3(472+4*i,101,485+4*j),Quaternion.identity);
                    var roof=Object.Instantiate(floor,new Vector3(472+4*i,108,485+4*j),Quaternion.identity);
                    var materials=roof.GetComponentsInChildren<MeshRenderer>().First(r=>r.shadowCastingMode!=ShadowCastingMode.ShadowsOnly).sharedMaterials;
                    var patch=roof.AddComponent<RoomFloorPatch>();patch.materials=materials;
                    patch.triangles=new[]{Vector3.zero,new Vector3(4,0,0),new Vector3(4,0,4),Vector3.zero,new Vector3(4,0,4),new Vector3(0,0,4)};patch.Rebuild();
                }
            }
            var corners=new[]{new Vector3(474,104,487),new Vector3(514,104,487),new Vector3(514,104,527),new Vector3(474,104,527)};
            float oldTotal=0,newTotal=0;
            if(seams)
            {
                sun.shadowBias=0;sun.shadowNormalBias=0;sun.shadowNearPlane=.1f;
                var splits=pipeline.cascade4Split;
                var wallMaterials=new List<Material>();
                foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if(renderer.GetComponentInParent<WallTopology>())
                    {
                        var copies=renderer.sharedMaterials.Select(m=>Object.Instantiate(m)).ToArray();
                        wallMaterials.AddRange(copies);renderer.sharedMaterials=copies;
                    }
                float before=0,after=0;
                for(int corner=0;corner<4;corner++)foreach(float elevation in new[]{10f,35f,60f})foreach(float yaw in verify?new[]{-25f,0f,25f}:new[]{0f})
                {
                    camera.transform.position=corners[corner];camera.transform.LookAt(corners[(corner+2)%4]+Vector3.up*2);
                    camera.transform.Rotate(0,yaw,0,Space.World);
                    sun.transform.rotation=Quaternion.Euler(elevation,corner*90,0);
                    sun.enabled=false;var baseline=Capture($"c{corner}_e{elevation}_yaw{yaw}_off");sun.enabled=true;
                    foreach(int mode in verify?new[]{0,4}:new[]{0,1,2,3,4,5,6})
                    {
                        foreach(var material in wallMaterials)if(material.HasProperty("_ShadowReceiverInset"))material.SetFloat("_ShadowReceiverInset",mode==4?AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wall_Building.mat").GetFloat("_ShadowReceiverInset"):mode==5?.2f:mode==6?.3f:.05f);
                        foreach(var material in wallMaterials)if(material.HasProperty("_ShadowContactTexels"))material.SetFloat("_ShadowContactTexels",mode==4?AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wall_Building.mat").GetFloat("_ShadowContactTexels"):0);
                        pipeline.shadowDistance=mode==1?150:100;
                        pipeline.cascade4Split=mode==2?new Vector3(.1f,.3f,.6f):splits;
                        sun.shadowNearPlane=mode==3?5:.1f;
                        var image=Capture($"c{corner}_e{elevation}_yaw{yaw}_mode{mode}");float excess=0;int bright=0;
                        for(int y=15;y<345;y++)for(int x=15;x<625;x++)
                        {float delta=Mathf.Max(0,image.GetPixel(x,y).grayscale-baseline.GetPixel(x,y).grayscale);excess+=delta;if(delta>.08f)bright++;}
                        report.Add($"corner={corner} elevation={elevation} yaw={yaw} mode={mode} excess={excess:F4} bright={bright}");Object.DestroyImmediate(image);
                        if(mode==0)before+=excess;if(mode==4)after+=excess;
                    }
                    Object.DestroyImmediate(baseline);
                }
                foreach(var material in wallMaterials)Object.DestroyImmediate(material);
                File.WriteAllLines(dir+(verify?"/FinalMeasurements.txt":"/Measurements.txt"),report);
                if(verify && (before<1 || after>before*.1f))throw new Exception($"Contact correction regression: before={before} after={after}");
                Debug.Log($"CORNER_SEAMS_OK before={before} after={after}");return;
            }
            for(int corner=0;corner<4;corner++)
            {
                camera.transform.position=corners[corner];camera.transform.LookAt(corners[(corner+2)%4]);
                sun.transform.rotation=Quaternion.Euler(35,corner*90,0);
                sun.enabled=false;var baseline=Capture("corner"+corner+"_sun_off");sun.enabled=true;
                foreach(float distance in new[]{50f,100f})
                {
                    pipeline.shadowDistance=distance;
                    var image=Capture("corner"+corner+"_distance"+distance);float excess=0;int pixels=0;
                    for(int y=30;y<330;y++)for(int x=30;x<610;x++)
                    {excess+=Mathf.Max(0,image.GetPixel(x,y).grayscale-baseline.GetPixel(x,y).grayscale);pixels++;}
                    excess/=pixels;if(distance==50)oldTotal+=excess;else newTotal+=excess;
                    report.Add($"corner={corner} distance={distance} meanDirectSunLeak={excess:F6}");Object.DestroyImmediate(image);
                }
                Object.DestroyImmediate(baseline);
            }
            File.WriteAllLines(dir+"/Measurements.txt",report);
            if(oldTotal<.005f)throw new Exception("Original 50-unit leak was not reproduced");
            if(newTotal>oldTotal*.2f || newTotal>.01f)throw new Exception("100-unit shadows do not sufficiently eliminate the leak: "+newTotal);
            Debug.Log($"LARGE_ROOM_SHADOW_DISTANCE_OK old={oldTotal:F6} fixed={newTotal:F6}");
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation=async;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);
            QualitySettings.renderPipeline=original;Object.DestroyImmediate(pipeline);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
#endif
