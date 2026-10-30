#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Player;
using JuegoCriminal.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AimPoseRefinement
{
    const string Player = "Assets/Prefabs/Player.prefab";
    const string TestKey = "CriminalGame.AimPoseRefinementTest";
    static AimPoseRefinement()
    {
        EditorApplication.playModeStateChanged += StateChanged;
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(TestKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // Let domain-reload editor callbacks finish before the isolated test
            // exits Play; otherwise deferred scene utilities see a half-exited editor.
            EditorApplication.delayCall += RunPlayValidation;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(TestKey, false);
            EditorApplication.Exit(SessionState.GetInt(TestKey + ".result", 1));
        }
    }
    static void RunPlayValidation()
    {
        try { Validate(); SessionState.SetInt(TestKey + ".result", 0); }
        catch (Exception exception) { Debug.LogException(exception); SessionState.SetInt(TestKey + ".result", 1); }
        EditorApplication.delayCall += EditorApplication.ExitPlaymode;
    }
    public static void StartValidation()
    {
        PlayerAnimationExpansion.Validate();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Isolated aiming validation").AddComponent<StandalonePreviewScene>();
        SessionState.SetBool(TestKey, true);
        EditorApplication.EnterPlaymode();
    }
    public static void Apply()
    {
        string backup = "Documentation/AimPoseBackup/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup); File.Copy(Player, backup + "/Player.prefab");
        var root = PrefabUtility.LoadPrefabContents(Player);
        try
        {
            var driver = root.GetComponent<PlayerLocomotionAnimation>();
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("aimBlendDuration").floatValue = .4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var animator = root.GetComponentsInChildren<Animator>(true).Single(a => a.runtimeAnimatorController);
            if (!animator.GetComponent<PlayerAimHands>()) animator.gameObject.AddComponent<PlayerAimHands>();
            PrefabUtility.SaveAsPrefabAsset(root, Player);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("AIM_POSE_REFINEMENT_APPLIED backup=" + backup);
        StartValidation();
    }

    public static void Validate()
    {
        var report = new List<string>();
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); report.Add("PASS " + label); }
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
        try
        {
            var animator = root.GetComponentsInChildren<Animator>(true).Single(a => a.runtimeAnimatorController);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var hands = animator.GetComponent<PlayerAimHands>();
            Check(hands && hands.enabled, "Hand correction saved on the animated model");
            // Edit-mode instantiation does not invoke Awake.
            typeof(PlayerAimHands).GetField("animator", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hands, animator);
            var driver = root.GetComponent<PlayerLocomotionAnimation>(); driver.Configure(animator);
            driver.enabled = false;
            root.GetComponent<ThirdPersonController>().enabled = false;
            typeof(PlayerLocomotionAnimation).GetField("movement", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(driver, root.GetComponent<ThirdPersonController>());
            animator.Rebind(); driver.UpdateLocomotion(Vector3.zero); animator.Update(.5f);
            Check(PlayerAimHands.AimWeight(animator, 0) == 0, "No hand correction in normal idle");
            driver.AdvanceInput(Vector2.zero, false, true, 0); animator.Update(.15f);
            Check(animator.IsInTransition(0), "Entry still blends after 150 ms");
            float early = PlayerAimHands.AimWeight(animator, 0);
            Check(early > 0 && early < 1, "Hand correction blends with the incoming pose");
            animator.Update(.5f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Pistol Idle") && PlayerAimHands.AimWeight(animator, 0) == 1, "Final pistol pose gets full correction");
            var correction = typeof(PlayerAimHands).GetField("correctionWeight", BindingFlags.Instance | BindingFlags.NonPublic);
            correction.SetValue(hands, 0f);
            animator.Play("Base Layer.Pistol Idle", 0, .3f); animator.Update(0);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            float originalGap = Vector3.Dot(right.position - left.position, animator.transform.right);
            correction.SetValue(hands, 1f);
            animator.Play("Base Layer.Pistol Idle", 0, .3f); animator.Update(0);
            float correctedGap = Vector3.Dot(right.position - left.position, animator.transform.right);
            Check(correctedGap > originalGap + .02f && correctedGap < originalGap + .12f,
                "Hand separation increases modestly: " + originalGap.ToString("F3") + " -> " + correctedGap.ToString("F3") + " m");
            driver.AdvanceInput(Vector2.zero, false, true, 0); animator.Update(.15f);
            Check(animator.IsInTransition(0) && PlayerAimHands.AimWeight(animator, 0) > 0, "Exit blends pose and hand correction out together");
            animator.Update(.5f);
            Check(PlayerAimHands.AimWeight(animator, 0) == 0, "No correction remains after exit");
        }
        finally { Object.DestroyImmediate(root); }
        File.WriteAllLines("Documentation/AimPoseRefinementValidation.txt", report);
        Debug.Log("AIM_POSE_REFINEMENT_VALIDATED checks=" + report.Count);
    }
}
#endif
