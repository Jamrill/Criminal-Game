#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using JuegoCriminal.Player;
using UnityEditor.Animations;

public static class PlayerFootIKSetup
{
    sealed class SensorData {public Vector3 position,scale,size;public Quaternion rotation;}
    static SensorData Measure(GameObject player,Animator animator,bool left)
    {
        var foot=animator.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
        var toe=animator.GetBoneTransform(left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
        var forward=toe?Vector3.ProjectOnPlane(toe.position-foot.position,Vector3.up).normalized:player.transform.forward;
        var rotation=Quaternion.LookRotation(forward,Vector3.up);
        bool any=false;var bounds=new Bounds();
        foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if(!skin.sharedMesh)continue;
            var baked=new Mesh();
            try
            {
                skin.BakeMesh(baked);var positions=baked.vertices;var weights=skin.sharedMesh.boneWeights;
                for(int i=0;i<positions.Length&&i<weights.Length;i++)
                {
                    var w=weights[i];
                    bool Match(int index,float weight)=>weight>.25f && index<skin.bones.Length && skin.bones[index] && skin.bones[index].IsChildOf(foot);
                    if(!Match(w.boneIndex0,w.weight0)&&!Match(w.boneIndex1,w.weight1)&&!Match(w.boneIndex2,w.weight2)&&!Match(w.boneIndex3,w.weight3))continue;
                    var point=Quaternion.Inverse(rotation)*(skin.transform.TransformPoint(positions[i])-foot.position);
                    if(!any){bounds=new Bounds(point,Vector3.zero);any=true;}else bounds.Encapsulate(point);
                }
            }
            finally{Object.DestroyImmediate(baked);}
        }
        if(!any)throw new Exception("Cannot measure actual foot mesh "+foot.name);
        var go=new GameObject("MeasuredSensor");go.transform.SetParent(foot,false);
        go.transform.SetPositionAndRotation(foot.position+rotation*bounds.center,rotation);
        var s=foot.lossyScale;go.transform.localScale=new Vector3(1/Mathf.Abs(s.x),1/Mathf.Abs(s.y),1/Mathf.Abs(s.z));
        var result=new SensorData{position=go.transform.localPosition,rotation=go.transform.localRotation,scale=go.transform.localScale,size=bounds.size};
        Object.DestroyImmediate(go);return result;
    }
    public static void ApplyAndValidate()
    {
        const string path="Assets/Prefabs/Player.prefab";
        string backup="Documentation/PlayerFootIKBackup/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/Player.prefab");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var sample=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        SensorData left,right;float baseline;
        try
        {
            foreach(var prior in sample.GetComponentsInChildren<PlayerFootGrounding>())prior.enabled=false;
            var animator=sample.GetComponentsInChildren<Animator>().First(a=>a.isHuman);
            animator.Rebind();animator.Play("Standing Idle",0,0);animator.Update(0);
            left=Measure(sample,animator,true);right=Measure(sample,animator,false);
            baseline=Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y-animator.leftFeetBottomHeight,
                animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y-animator.rightFeetBottomHeight)-sample.transform.position.y;
        }
        finally{Object.DestroyImmediate(sample);}
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var animator=root.GetComponentsInChildren<Animator>().First(a=>a.isHuman);
            // Do not evaluate animation on prefab contents: that would save an
            // animation pose as hundreds of bone-transform overrides.
            var bones=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            var solver=animator.GetComponent<PlayerFootGrounding>();if(!solver)solver=animator.gameObject.AddComponent<PlayerFootGrounding>();
            BoxCollider Add(bool isLeft,SensorData data)
            {
                var foot=animator.GetBoneTransform(isLeft?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                string name=isLeft?"FootContact_Left":"FootContact_Right";
                var child=foot.Find(name);if(!child){child=new GameObject(name).transform;child.SetParent(foot,false);}
                child.localPosition=data.position;child.localRotation=data.rotation;child.localScale=data.scale;child.gameObject.layer=2;
                var box=child.GetComponent<BoxCollider>();if(!box)box=child.gameObject.AddComponent<BoxCollider>();box.center=Vector3.zero;box.size=data.size;box.isTrigger=true;
                return box;
            }
            solver.leftFootSensor=Add(true,left);solver.rightFootSensor=Add(false,right);solver.restSoleHeight=baseline;
            var controller=animator.runtimeAnimatorController as AnimatorController;
            if(!controller || !controller.layers[0].iKPass)throw new Exception("Animator IK pass must already be enabled");
            foreach(var pair in bones)
                if(pair.Key.localPosition!=pair.Value.localPosition || Quaternion.Angle(pair.Key.localRotation,pair.Value.localRotation)>.001f || pair.Key.localScale!=pair.Value.localScale)
                    throw new Exception("Unexpected modification to original transform "+pair.Key.name);
            PrefabUtility.SaveAsPrefabAsset(root,path);
            File.WriteAllText("Documentation/PlayerFootIKSetup.txt","Backup="+backup+"\nLeft box="+left.size+"\nRight box="+right.size+"\nRest sole height="+baseline+"\nOriginal bone transforms unchanged\n");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();PlayerFootIKValidation.Run();
    }
    public static void Inspect()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        try
        {
            var animator=player.GetComponentsInChildren<Animator>(true).First(a=>a.isHuman && a.gameObject.activeInHierarchy);
            animator.Rebind();animator.Play("Standing Idle",0,0);animator.Update(0);
            var lines=new List<string>{"root="+player.transform.position+" animator="+animator.name+" scale="+animator.transform.lossyScale+" humanScale="+animator.humanScale,
                "bottoms L="+animator.leftFeetBottomHeight+" R="+animator.rightFeetBottomHeight};
            foreach(var bone in new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightFoot,HumanBodyBones.RightToes})
            {
                var t=animator.GetBoneTransform(bone);lines.Add(bone+"="+(t?t.name+" pos="+t.position.ToString("F4")+" rotation="+t.eulerAngles+" scale="+t.lossyScale:"MISSING"));
            }
            foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                try
                {
                    if(!skin.sharedMesh || skin.sharedMesh.vertexCount==0)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh);
                    var weights=skin.sharedMesh.boneWeights;var vertices=mesh.vertices;
                    foreach(var bone in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                    {
                        var foot=animator.GetBoneTransform(bone);bool any=false;var bounds=new Bounds();
                        for(int i=0;i<vertices.Length&&i<weights.Length;i++)
                        {
                            var w=weights[i];
                            bool Matches(int index,float weight)=>weight>.25f && index<skin.bones.Length && skin.bones[index] && skin.bones[index].IsChildOf(foot);
                            if(!Matches(w.boneIndex0,w.weight0)&&!Matches(w.boneIndex1,w.weight1)&&!Matches(w.boneIndex2,w.weight2)&&!Matches(w.boneIndex3,w.weight3))continue;
                            var point=skin.transform.TransformPoint(vertices[i]);
                            if(!any){bounds=new Bounds(point,Vector3.zero);any=true;}else bounds.Encapsulate(point);
                        }
                        if(any)lines.Add("MESH "+skin.name+" "+bone+" "+bounds);
                    }
                    Object.DestroyImmediate(mesh);
                }
                catch(Exception e){lines.Add("MESH "+skin.name+" inspection="+e.Message);}
            }
            File.WriteAllLines("Documentation/PlayerFootInspection.txt",lines);
            Debug.Log("PLAYER_FOOT_INSPECTION_OK");
        }
        finally{Object.DestroyImmediate(player);}
    }
}
#endif
