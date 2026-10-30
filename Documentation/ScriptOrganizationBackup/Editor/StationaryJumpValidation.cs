using System;
using System.Reflection;
using JuegoCriminal.Player;
using UnityEditor;
using UnityEngine;
public static class StationaryJumpValidation
{
    public static void Run()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        var go=UnityEngine.Object.Instantiate(prefab);
        try
        {
            var driver=go.GetComponent<PlayerLocomotionAnimation>();
            var animator=go.GetComponentInChildren<Animator>(true);driver.Configure(animator);
            // Awake is not invoked for ordinary MonoBehaviours in an editor-only test.
            typeof(PlayerLocomotionAnimation).GetField("movement",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(driver,go.GetComponent<ThirdPersonController>());
            string State()=>(string)typeof(PlayerLocomotionAnimation).GetField("currentState",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(driver);
            void Check(bool value,string label){if(!value)throw new Exception(label);Debug.Log("MOVEMENT_PASS "+label);}
            driver.UpdateLocomotion(Vector3.left,Vector2.left,false);Check(State()=="Walking","A uses Walking");
            driver.UpdateLocomotion(Vector3.right,Vector2.right,true);Check(State()=="Fast Run","Sprint D uses Fast Run");
            driver.ConfigureJumpTimings(1,1);driver.ConfigureJumpContacts(.1f,.8f,.1f,.8f);
            driver.BeginJump(false,.5f);Check(driver.IsJumping && driver.BlocksMovement && State()=="Jump On Site","Stationary jump locks movement");
            driver.AdvanceInput(Vector2.up,false,false,.1f);Check(driver.ConsumeJumpTakeoff(),"Takeoff occurs");
            driver.UpdateJumpContact(true,-1);
            driver.AdvanceInput(Vector2.up,false,false,.05f);Check(driver.IsJumping,"W cannot cancel landing recovery");
            driver.AdvanceInput(Vector2.up,false,false,.2f);Check(!driver.IsJumping,"Movement unlocks after recovery");
            driver.BeginJump(true,.5f);Check(driver.IsJumping && driver.IsMovingJump && !driver.BlocksMovement && State()=="Jump Moving","Moving jump selects moving clip and allows translation");
            Debug.Log("STATIONARY_JUMP_VALIDATION_OK");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
}
