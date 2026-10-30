#if UNITY_EDITOR
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

public static class PlayerFootIKValidation
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        var report=new List<string>();
        void Check(bool ok,string description)
        {report.Add((ok?"PASS ":"FAIL ")+description);File.WriteAllLines("Documentation/PlayerFootIKValidation.txt",report);if(!ok)throw new Exception(description);}
        BoxCollider Surface(string name,Vector3 center,Vector3 size)
        {var go=new GameObject(name);go.transform.position=center;var box=go.AddComponent<BoxCollider>();box.size=size;return box;}
        try
        {
            var animator=player.GetComponentsInChildren<Animator>().First(a=>a.isHuman);
            var ik=animator.GetComponent<PlayerFootGrounding>();
            foreach(var script in player.GetComponentsInChildren<MonoBehaviour>())if(script!=ik)script.enabled=false;
            // Enable callbacks only on this temporary instance, not on the prefab.
            ik.runInEditMode=true;
            Check(ik && ik.leftFootSensor && ik.rightFootSensor,"Prefab has a grounding solver and two assigned foot boxes");
            Check(ik.leftFootSensor.isTrigger && ik.rightFootSensor.isTrigger && ik.leftFootSensor.gameObject.layer==2 && ik.rightFootSensor.gameObject.layer==2,"Foot boxes are non-blocking sensors excluded from interaction rays");
            Check(ik.leftFootSensor.transform.IsChildOf(animator.GetBoneTransform(HumanBodyBones.LeftFoot)) && ik.rightFootSensor.transform.IsChildOf(animator.GetBoneTransform(HumanBodyBones.RightFoot)),"Sensors follow the correct humanoid bones");
            animator.Rebind();animator.Play("Standing Idle",0,0);animator.Update(0);
            Check(ik.leftFootSensor.bounds.size.z<1 && ik.leftFootSensor.bounds.size.z>.2f && ik.rightFootSensor.bounds.size.x<.6f,"Sensor scale compensates the FBX bones' 100x scale");
            float y=ik.restSoleHeight;
            var ground=Surface("Ground",new Vector3(0,y-.05f,0),new Vector3(10,.1f,10));Physics.SyncTransforms();
            Check(ik.Probe(new Vector3(-.38f,y,.18f),out var flat) && flat.collider==ground && Mathf.Abs(flat.point.y-y)<.002f,"Feet detect the supporting ground");
            var curb=Surface("Left curb",new Vector3(-.38f,y+.12f,.18f),new Vector3(.48f,.24f,1));Physics.SyncTransforms();
            Check(ik.Probe(new Vector3(-.38f,y,.18f),out var left) && left.collider==curb && Mathf.Abs(left.point.y-y-.24f)<.002f,"Left foot detects a 24cm curb");
            Check(ik.Probe(new Vector3(.38f,y,.18f),out var right) && right.collider==ground,"Right foot independently remains on lower ground");
            var own=Surface("Own body collider",new Vector3(2,y+.15f,0),new Vector3(.5f,.3f,.5f));own.transform.SetParent(player.transform,true);Physics.SyncTransforms();
            Check(ik.Probe(new Vector3(2,y,0),out var self) && self.collider==ground,"Ground query ignores all player colliders");
            var trigger=Surface("Trigger volume",new Vector3(2,y+.15f,2),new Vector3(.5f,.3f,.5f));trigger.isTrigger=true;Physics.SyncTransforms();
            Check(ik.Probe(new Vector3(2,y,2),out var tr) && tr.collider==ground,"Ground query ignores triggers");
            var dropped=Surface("Dropped rigidbody item",new Vector3(0,y+.09f,-2),new Vector3(.5f,.18f,.5f));var rb=dropped.gameObject.AddComponent<Rigidbody>();rb.isKinematic=true;Physics.SyncTransforms();
            Check(ik.Probe(new Vector3(0,y,-2),out var item) && item.collider==dropped,"Feet recognize dropped objects with their own colliders");
            var slope=Surface("Slope",new Vector3(-2,y+.08f,-2),new Vector3(1,.15f,1));slope.transform.rotation=Quaternion.Euler(0,0,15);Physics.SyncTransforms();
            Check(ik.Probe(new Vector3(-2,y,-2),out var inclined) && inclined.collider==slope && Vector3.Angle(inclined.normal,Vector3.up)>14,"Surface normals retain gentle slope information");
            var layers=ik.groundLayers;ik.groundLayers=0;
            Check(!ik.Probe(new Vector3(0,y,0),out _),"Inspector ground layer mask is honored");ik.groundLayers=layers;
            ground.gameObject.SetActive(false);curb.gameObject.SetActive(false);Physics.SyncTransforms();
            Check(!ik.Probe(new Vector3(0,y+4,0),out _),"No foot snapping to distant ground while airborne");
            ground.gameObject.SetActive(true);curb.gameObject.SetActive(true);Physics.SyncTransforms();
            // The actual Animator IK pass is verified separately in Play mode;
            // manual editor Animator.Update does not deliver this callback.
            Check(animator.GetComponent<PlayerHeadLook>() && player.GetComponent<CharacterController>(),"Head-look solver and body CharacterController are preserved");
            Debug.Log("PLAYER_FOOT_IK_VALIDATION_OK checks="+report.Count);
        }
        finally{Object.DestroyImmediate(player);}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
}
#endif
