using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using JuegoCriminal.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerFootIKPlayValidation
{
    const string Key="CriminalGame.FootIKValidation";
    static Animator animator;
    static PlayerFootGrounding ik;
    static PlayerLocomotionAnimation locomotion;
    static GameObject root,curb;
    static int phase,frame;
    static float startTime;
    static readonly List<string> report=new();
    static PlayerFootIKPlayValidation(){if(SessionState.GetBool(Key,false))Hook();}
    static void Hook()
    {
        EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    public static void Start()
    {
        PlayerFootIKValidation.Run();MovementSteeringValidation.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);SessionState.SetFloat(Key+".start",(float)EditorApplication.timeSinceStartup);
        Hook();EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            try{Setup();}catch(Exception e){Fail(e);}
        }
        else if(state==PlayModeStateChange.EnteredEditMode)
        {
            int result=SessionState.GetInt(Key+".result",1);SessionState.SetBool(Key,false);
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;EditorApplication.Exit(result);
        }
    }
    static void Setup()
    {
        report.Clear();phase=0;frame=0;startTime=Time.time;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        var source=prefab.GetComponentsInChildren<Animator>().First(a=>a.isHuman);
        float baseline=source.GetComponent<PlayerFootGrounding>().restSoleHeight;
        root=new GameObject("FootIK_PlayTest");var cc=root.AddComponent<CharacterController>();cc.height=3.5f;cc.center=new Vector3(0,1.9f,0);
        var movement=root.AddComponent<ThirdPersonController>();movement.enabled=false;
        locomotion=root.AddComponent<PlayerLocomotionAnimation>();locomotion.enabled=false;
        var model=Object.Instantiate(source.gameObject,root.transform,false);
        model.transform.localPosition=source.transform.localPosition;model.transform.localRotation=source.transform.localRotation;model.transform.localScale=source.transform.localScale;
        animator=model.GetComponent<Animator>();ik=model.GetComponent<PlayerFootGrounding>();
        foreach(var script in model.GetComponentsInChildren<MonoBehaviour>())if(script!=ik)script.enabled=false;
        locomotion.Configure(animator);animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Play("Standing Idle",0,0);
        void Surface(string name,Vector3 pos,Vector3 size)
        {var go=new GameObject(name);go.transform.position=pos;var box=go.AddComponent<BoxCollider>();box.size=size;}
        Surface("Ground",new Vector3(0,baseline-.05f,0),new Vector3(10,.1f,10));
        Surface("Curb left",new Vector3(-.38f,baseline+.12f,.18f),new Vector3(.48f,.24f,1));
        curb=GameObject.Find("Curb left");
        Physics.SyncTransforms();
    }
    static void Check(bool ok,string description)
    {
        report.Add((ok?"PASS ":"FAIL ")+description);File.WriteAllLines("Documentation/PlayerFootIKPlayValidation.txt",report);
        if(!ok)throw new Exception(description);
    }
    static void CompareAnimation()
    {
        var reference=Object.Instantiate(animator.gameObject,root.transform,false);
        foreach(var script in reference.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
        var original=reference.GetComponent<Animator>();
        original.applyRootMotion=false;original.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var lines=new List<string>();
        var platform=new GameObject("Animation comparison support");var support=platform.AddComponent<BoxCollider>();
        support.size=new Vector3(8,.1f,8);
        float response=ik.footResponse,weightResponse=ik.weightResponse;
        ik.footResponse=100000;ik.weightResponse=100000;
        try
        {
            foreach(float elevation in new[]{0f,.15f})
            {
            platform.SetActive(elevation>0);
            platform.transform.position=new Vector3(0,ik.restSoleHeight+elevation-.05f,0);Physics.SyncTransforms();
            float maximumHorizontalError=0,maximumRotationError=0,maximumVerticalError=0;
            foreach(string state in new[]{"Standing Idle","Walking","Walking Backward","Fast Run"})
            foreach(float time in new[]{0f,.2f,.4f,.6f,.8f})
            {
                animator.Play(state,0,time);original.Play(state,0,time);
                original.Update(0);animator.Update(0);
                foreach(var bone in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                {
                    var expected=original.GetBoneTransform(bone).position;
                    var actual=animator.GetBoneTransform(bone).position;
                    var delta=Vector3.ProjectOnPlane(actual-expected,Vector3.up);
                    maximumHorizontalError=Mathf.Max(maximumHorizontalError,delta.magnitude);
                    maximumRotationError=Mathf.Max(maximumRotationError,Quaternion.Angle(original.GetBoneTransform(bone).rotation,animator.GetBoneTransform(bone).rotation));
                    float weight=bone==HumanBodyBones.LeftFoot?ik.LeftWeight:ik.RightWeight;
                    maximumVerticalError=Mathf.Max(maximumVerticalError,Mathf.Abs((actual.y-expected.y)-(elevation>0?(elevation+ik.soleClearance)*weight:0)));
                    lines.Add("support="+elevation+" "+state+" t="+time+" "+bone+" reference="+expected.ToString("F4")+" IK="+actual.ToString("F4")+" horizontalError="+delta.magnitude);
                }
            }
            File.WriteAllLines("Documentation/PlayerFootIKAnimationComparison.txt",lines);
            Check(maximumHorizontalError<.025f,"IK preserves animated foot spacing and stride in idle/walk/backward/run at elevation="+elevation+"; maximum horizontal error="+maximumHorizontalError);
            if(elevation==0)Check(maximumRotationError<.2f,"Flat-ground IK preserves the original foot rotations; maximum error="+maximumRotationError);
            else Check(maximumVerticalError<.04f,"Elevated support retains the animated vertical swing arc; maximum error="+maximumVerticalError);
            }
            platform.SetActive(false);Physics.SyncTransforms();
            Time.timeScale=0;animator.Play("Walking",0,.35f);original.Play("Walking",0,.35f);original.Update(0);animator.Update(0);
            Time.timeScale=1;original.Update(0);animator.Update(0);
            foreach(var bone in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                Check(Vector3.ProjectOnPlane(animator.GetBoneTransform(bone).position-original.GetBoneTransform(bone).position,Vector3.up).magnitude<.025f,"Pause/resume preserves "+bone+" spacing");
        }
        finally
        {
            Time.timeScale=1;ik.footResponse=response;ik.weightResponse=weightResponse;
            Object.Destroy(reference);Object.Destroy(platform);animator.Play("Standing Idle",0,0);animator.Update(0);
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".start",0)>100){Fail(new Exception("Foot IK Play test timeout"));return;}
        if(!EditorApplication.isPlaying || !animator)return;
        try
        {
            frame++;
            if(phase==0 && frame>30 && Time.time-startTime>.3f)
            {
                var left=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var right=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                string Detail(string side)
                {
                    var state=typeof(PlayerFootGrounding).GetField(side,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ik);
                    return string.Join("; ",state.GetType().GetFields().Select(f=>f.Name+"="+f.GetValue(state)));
                }
                File.WriteAllText("Documentation/PlayerFootIKPose.txt","Left="+left.position+" Right="+right.position+" L box="+ik.leftFootSensor.bounds+" R box="+ik.rightFootSensor.bounds+" passes="+ik.SolverPasses+" weights="+ik.LeftWeight+","+ik.RightWeight+"\nLEFT "+Detail("left")+"\nRIGHT "+Detail("right"));
                Check(ik.SolverPasses>0,"Real Animator OnAnimatorIK callback executes in Play");
                Check(left.position.y-right.position.y>.15f,"Real humanoid solver raises left foot onto 24cm curb independently of right foot");
                Check(ik.LeftWeight>.5f && ik.RightWeight>.5f,"Both planted feet have IK weight");
                curb.SetActive(false);Physics.SyncTransforms();phase=1;frame=0;startTime=Time.time;
            }
            else if(phase==1 && frame>30 && Time.time-startTime>.5f)
            {
                var l=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var r=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Check(Mathf.Abs(l.position.y-r.position.y)<.04f,"Feet smoothly return to matching height after removing the curb");
                Check(ik.LeftWeight>.5f && ik.RightWeight>.5f,"Flat-ground support stays active after stepping down");
                CompareAnimation();
                locomotion.BeginJump(false,.5f);phase=2;frame=0;
            }
            else if(phase==2 && frame>4)
            {
                Check(ik.LeftWeight==0 && ik.RightWeight==0,"Jump animation immediately releases both feet");
                typeof(PlayerLocomotionAnimation).GetField("<IsJumping>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(locomotion,false);
                root.transform.position=Vector3.up*3;Physics.SyncTransforms();phase=3;frame=0;
            }
            else if(phase==3 && frame>4)
            {
                Check(ik.LeftWeight==0 && ik.RightWeight==0,"Airborne character does not snap to distant ground");
                Debug.Log("PLAYER_FOOT_IK_PLAY_VALIDATION_OK checks="+report.Count);SessionState.SetInt(Key+".result",0);EditorApplication.isPlaying=false;
            }
        }
        catch(Exception e){Fail(e);}
    }
    static void Fail(Exception e)
    {
        Debug.LogException(e);SessionState.SetInt(Key+".result",1);
        if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
        else{SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
}
