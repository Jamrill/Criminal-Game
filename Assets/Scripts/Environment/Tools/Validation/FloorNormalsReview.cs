#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class FloorNormalsReview
{
    public static void Run()
    {
        var report=new List<string>();
        void Inspect(GameObject root,string label)
        {
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!f.sharedMesh)continue;var mesh=f.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;
                var matrix=root.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;
                var normalMatrix=matrix.inverse.transpose;
                report.Add(label+"/"+f.name+" vertices="+vertices.Length+" normals="+normals.Length+" bounds="+mesh.bounds);
                for(int s=0;s<mesh.subMeshCount;s++)
                {
                    var triangles=mesh.GetTriangles(s);float min=1,sum=0;int count=0;var geometric=Vector3.zero;var shading=Vector3.zero;
                    for(int t=0;t<triangles.Length;t+=3)
                    {
                        int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                        var g=Vector3.Cross(matrix.MultiplyPoint3x4(vertices[b])-matrix.MultiplyPoint3x4(vertices[a]),matrix.MultiplyPoint3x4(vertices[c])-matrix.MultiplyPoint3x4(vertices[a])).normalized;
                        geometric+=g;
                        foreach(int index in new[]{a,b,c})if(normals.Length>index)
                        {
                            var n=normalMatrix.MultiplyVector(normals[index]).normalized;float dot=Vector3.Dot(g,n);
                            min=Mathf.Min(min,dot);sum+=dot;count++;shading+=n;
                        }
                    }
                    var r=f.GetComponent<Renderer>();var mats=r?r.sharedMaterials:Array.Empty<Material>();
                    report.Add($"submesh={s} material={(s<mats.Length?mats[s].name:"missing")} minDot={min} avgDot={(count>0?sum/count:0)} geometryNormal={geometric.normalized} shadingNormal={shading.normalized}");
                }
            }
        }
        Inspect(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Floor.prefab"),"Floor");
        Inspect(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Construction/Incompletes/Wall_incomplete.prefab"),"Wall");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/10_World_City.unity");
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))if(r.name.StartsWith("Floor") && r.GetComponent<MeshFilter>())
            report.Add($"scene={r.name} layer={r.gameObject.layer} renderingLayer={r.renderingLayerMask} scale={r.transform.lossyScale} material={string.Join(",",Array.ConvertAll(r.sharedMaterials,m=>m?m.name:"null"))}");
        foreach(var root in scene.GetRootGameObjects())foreach(var ui in root.GetComponentsInChildren<RectTransform>(true))
            if(ui.name=="OptionsPanel" || ui.name=="SlotsPanel")report.Add($"UI {ui.name} source={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ui.gameObject)}");
        var menu=EditorSceneManager.OpenScene("Assets/Scenes/01_MainMenu.unity");
        foreach(var root in menu.GetRootGameObjects())foreach(var ui in root.GetComponentsInChildren<RectTransform>(true))
            if(ui.name=="OptionsPanel" || ui.name=="SlotsPanel")report.Add($"MENU_UI {ui.name} source={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ui.gameObject)} overrides={PrefabUtility.HasPrefabInstanceAnyOverrides(ui.gameObject,false)}");
        File.WriteAllLines("Documentation/FloorNormalsReview.txt",report);Debug.Log("FLOOR_NORMALS_REVIEW_OK");
    }
}
#endif
