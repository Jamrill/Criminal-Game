using System;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using JuegoCriminal.Player;
using JuegoCriminal.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

public static class MovementControlsRevisionValidation
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var report=new List<string>();
        void Check(bool ok,string message){if(!ok)throw new Exception(message);report.Add("PASS "+message);}
        var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        try
        {
            var move=player.GetComponent<ThirdPersonController>();
            Check(Mathf.Abs(move.MovementSpeedFor(Vector2.down,false,false)/move.MovementSpeedFor(Vector2.up,false,false)-.65f)<.001f,"Backward walk is 65 percent");
            Check(Mathf.Approximately(move.MovementSpeedFor(Vector2.down,true,false),move.MovementSpeedFor(Vector2.up,true,false)),"Sprint translation speed unchanged in reverse");
            var animation=player.GetComponent<PlayerLocomotionAnimation>();animation.Configure(player.GetComponentInChildren<Animator>(true));
            animation.UpdateLocomotion(Vector3.back,Vector2.down,true);
            string state=(string)typeof(PlayerLocomotionAnimation).GetField("currentState",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(animation);
            float yaw=(float)typeof(PlayerLocomotionAnimation).GetField("targetBodyYaw",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(animation);
            Check(animation.IsBackwardRunning && state=="Fast Run" && Mathf.Abs(yaw)==180,"Reverse sprint turns body and uses forward running clip");
            animation.UpdateLocomotion(Vector3.back,Vector2.down,false);
            Check(!animation.IsBackwardRunning,"Head/body reverse-run mode clears on walk");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/PlayerLocomotion.controller");
            Check(Mathf.Approximately(controller.layers[0].stateMachine.states.Single(s=>s.state.name=="Fast Run").state.speed,.9f),"Running clip speed 0.9");
        }
        finally{Object.DestroyImmediate(player);}
        var panel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/OptionsPanel.prefab"));
        try
        {
            var controls=panel.GetComponentInChildren<ControlsMenuUI>(true);controls.OrganizeSections();controls.OrganizeSections();
            var content=(RectTransform)typeof(ControlsMenuUI).GetField("contentRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controls);
            var header=content.Find("ConstructionControlsHeader");
            var rows=content.Cast<Transform>().Where(t=>t.gameObject.activeSelf && t.GetSiblingIndex()>header.GetSiblingIndex() && t.GetComponent<ControlRebindButtonUI>()).ToArray();
            Check(rows.Length==4,"Construction section has exactly four bindings after repeated organization");
            Check(content.Find("Binding_Tablet").GetSiblingIndex()<header.GetSiblingIndex(),"Tablet appears in normal controls");
            Check(rows.Any(t=>t.name=="Binding_BuildPreviewDistance"),"Wheel preview distance exposed");
        }
        finally{Object.DestroyImmediate(panel);}
        var input=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        Check(input.FindAction("Player/BuildDistance").bindings.Any(b=>b.path=="<Mouse>/scroll"),"Wheel action correctly bound");
        File.WriteAllLines("Documentation/MovementControlsRevisionValidation.txt",report);Debug.Log("MOVEMENT_CONTROLS_REVISION_OK checks="+report.Count);
    }
    public static void ApplyAndValidate()
    {
        InteriorLightingCorrection.Run();Run();
        InteriorLightContactReview.Run();
        ClosedRoomShadowValidation.FinalRun();
    }
}
