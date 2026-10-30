#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using JuegoCriminal.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class MovementSteeringValidation
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        var report=new List<string>();
        void Check(bool ok,string name)
        {
            report.Add((ok?"PASS ":"FAIL ")+name);File.WriteAllLines("Documentation/MovementSteeringValidation.txt",report);
            if(!ok)throw new Exception(name);
        }
        try
        {
            var move=player.GetComponent<ThirdPersonController>();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var step=typeof(ThirdPersonController).GetMethod("StepHorizontalVelocity",flags);
            Vector3 Step(Vector3 v,Vector3 t,float dt)=>(Vector3)step.Invoke(move,new object[]{v,t,dt});
            float speed=move.MovementSpeedFor(Vector2.up,true,false);
            foreach(int fps in new[]{30,60,144})foreach(float angle in new[]{45f,-45f,90f,-90f,135f,180f})
            {
                var target=Quaternion.Euler(0,angle,0)*Vector3.forward*speed;
                var velocity=Vector3.forward*speed;float elapsed=0;
                while(elapsed<.1f-.000001f)
                {
                    float dt=Mathf.Min(1f/fps,.1f-elapsed);velocity=Step(velocity,target,dt);elapsed+=dt;
                    if(Mathf.Abs(velocity.magnitude-speed)>.001f || Mathf.Abs(velocity.y)>.00001f)throw new Exception("Turning changed sprint speed or vertical velocity");
                }
                Check(Vector3.Angle(velocity,target)<10.2f,"Steering at "+fps+" FPS, "+angle+" degrees: less than 10.2 degrees residual after 100ms, full speed");
            }
            var started=Step(Vector3.zero,Vector3.forward*speed,1f/60);
            Check(started.magnitude>0 && started.magnitude<speed*.1f,"Starting still accelerates gradually");
            var slowed=Step(Vector3.forward*speed,Vector3.forward*move.MovementSpeedFor(Vector2.up,false,false),1f/60);
            Check(slowed.magnitude<speed && slowed.magnitude>move.MovementSpeedFor(Vector2.up,false,false),"Run-to-walk speed does not snap");
            var zero=Step(Vector3.zero,Vector3.zero,1f/60);
            Check(zero==Vector3.zero,"Zero input/velocity is stable");
            typeof(ThirdPersonController).GetField("_horizontalVelocity",flags).SetValue(move,Vector3.forward*speed);
            typeof(ThirdPersonController).GetMethod("RotateMovementFrame",flags).Invoke(move,new object[]{180f});
            var rotated=(Vector3)typeof(ThirdPersonController).GetField("_horizontalVelocity",flags).GetValue(move);
            Check(Vector3.Distance(rotated,Vector3.back*speed)<.001f,"Fast mouse yaw rotates movement immediately without speed loss");
            typeof(ThirdPersonController).GetField("directionChangeTime",flags).SetValue(move,0f);
            Check(Vector3.Distance(Step(Vector3.forward*speed,Vector3.back*speed,1f/60),Vector3.back*speed)<.001f,"Inspector allows instant steering at zero response time");
            move.StopHorizontalMotion();
            Check((Vector3)typeof(ThirdPersonController).GetField("_horizontalVelocity",flags).GetValue(move)==Vector3.zero,"Explicit stop still clears movement");
            Check(Mathf.Approximately((float)typeof(ThirdPersonController).GetField("runTransitionDuration",flags).GetValue(move),.4f),"Existing sprint transition duration remains unchanged");
            Debug.Log("MOVEMENT_STEERING_VALIDATION_OK checks="+report.Count+" sprintSpeed="+speed);
        }
        finally{Object.DestroyImmediate(player);}
        MovementControlsRevisionValidation.Run();
        StationaryJumpValidation.Run();
    }
}
#endif
