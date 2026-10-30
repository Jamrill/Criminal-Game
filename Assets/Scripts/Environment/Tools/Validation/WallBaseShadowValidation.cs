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

public static class WallBaseShadowValidation
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Construction/Incompletes/Wall_incomplete.prefab");
        var floor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Floor.prefab");
        var wall=Object.Instantiate(prefab,new Vector3(482,101,505),Quaternion.identity);
        Object.Instantiate(floor,new Vector3(482,101,501),Quaternion.identity);
        Object.Instantiate(floor,new Vector3(482,101,505),Quaternion.identity);
        var sun=new GameObject("Base diagnostic sun").AddComponent<Light>();sun.type=LightType.Directional;
        sun.intensity=1;sun.shadows=LightShadows.Hard;sun.shadowBias=0;sun.shadowNormalBias=0;sun.transform.rotation=Quaternion.Euler(35,0,0);
        sun.gameObject.AddComponent<UniversalAdditionalLightData>().usePipelineSettings=false;
        RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray*.1f;RenderSettings.reflectionIntensity=0;
        var camera=new GameObject("Base diagnostic camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=60;camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;
        var source=File.ReadAllText("Documentation/WorldWall_before_base_fade.shader.txt").Replace("Shader \"CriminalGame/World Wall\"","Shader \"Hidden/CriminalGame/UnboundedWallShadowTest\"");
        var oldShader=ShaderUtil.CreateShaderAsset(source,true);
        var oldMat=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wall_Building.mat"));oldMat.shader=oldShader;oldMat.SetFloat("_ShadowReceiverInset",.05f);
        var current=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wall_Building.mat");
        var renderer=wall.GetComponent<JuegoCriminal.Construction.WallTopology>().wall.GetComponent<MeshRenderer>();
        var previous=RenderTexture.active;bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        Directory.CreateDirectory("Documentation/WallBaseShadows");var lines=new List<string>();
        try
        {
            for(int mode=0;mode<2;mode++)
            {
                renderer.sharedMaterial=mode==0?oldMat:current;
                for(int view=0;view<2;view++)
                {
                    camera.transform.position=new Vector3(484,102.2f,view==0?501:495);camera.transform.LookAt(new Vector3(484,101.5f,504.75f));
                    camera.Render();RenderTexture.active=rt;
                    var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                    foreach(float h in new[]{.02f,.03f,.04f,.06f,.1f,.2f,.3f})
                    {
                        var pixel=camera.WorldToScreenPoint(new Vector3(484,101+h,504.75f));
                        lines.Add($"mode={mode} camera={view} height={h} color={texture.GetPixel(Mathf.RoundToInt(pixel.x),Mathf.RoundToInt(pixel.y))}");
                    }
                    File.WriteAllBytes($"Documentation/WallBaseShadows/mode{mode}_camera{view}.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
                }
            }
            File.WriteAllLines("Documentation/WallBaseShadows/Measurements.txt",lines);
            Debug.Log("WALL_BASE_SHADOW_COMPARISON_OK");
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation=async;RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Object.DestroyImmediate(oldMat);Object.DestroyImmediate(oldShader);
        }
        ClosedRoomShadowValidation.FinalRun();
    }
}
#endif
