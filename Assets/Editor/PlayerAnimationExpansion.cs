using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using JuegoCriminal.Player;

public static class PlayerAnimationExpansion
{
    const string ControllerPath = "Assets/Animations/PlayerLocomotion.controller";
    static readonly string[] Sources = { "Standing Idle", "Walking", "Left Strafe Walk", "Right Strafe Walk",
        "Pistol Aim", "Pistol Idle", "Crouched To Standing", "Crouched Walking" };

    public static AnimatorController BuildController()
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (string name in Sources)
        {
            string path = "Assets/Animations/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer) throw new InvalidOperationException("Missing animation: " + path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            var definitions = importer.defaultClipAnimations;
            if (definitions.Length != 1) throw new InvalidOperationException("Expected one take: " + path);
            var clip = definitions[0]; clip.name = name;
            bool loop = name != "Pistol Aim" && name != "Crouched To Standing";
            clip.loopTime = loop; clip.loopPose = loop;
            clip.lockRootRotation = true; clip.lockRootPositionXZ = true; clip.lockRootHeightY = true;
            clip.keepOriginalOrientation = true; clip.keepOriginalPositionXZ = true;
            clip.keepOriginalPositionY = true; clip.heightFromFeet = false;
            importer.clipAnimations = definitions;
            importer.SaveAndReimport();
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var avatar = assets.OfType<Avatar>().FirstOrDefault();
            var motion = assets.OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
            if (!avatar || !avatar.isValid || !avatar.isHuman || !motion || !motion.humanMotion)
                throw new InvalidOperationException("Humanoid import failed: " + path);
            clips.Add(name, motion);
            Debug.Log($"MIXAMO_VALID {name} length={motion.length:F3} humanoid={motion.humanMotion}");
        }
        clips["Walking"] = InPlace(clips["Walking"]);
        clips["Crouched Walking"] = InPlace(clips["Crouched Walking"]);
        clips["Walking Reverse"] = Reverse(clips["Walking"], "Walking Reverse");
        clips["Crouched Walking Reverse"] = Reverse(clips["Crouched Walking"], "Crouched Walking Reverse");
        clips["Pistol Aim Reverse"] = Reverse(clips["Pistol Aim"], "Pistol Aim Reverse");
        clips["Crouched To Standing Reverse"] = Reverse(clips["Crouched To Standing"], "Crouched To Standing Reverse");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        // This is the project's dedicated locomotion controller; GUID stays unchanged.
        foreach (var t in machine.anyStateTransitions) machine.RemoveAnyStateTransition(t);
        foreach (var s in machine.states) machine.RemoveState(s.state);
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        int index = 0;
        AnimatorState Add(string stateName, string clipName, float speed = 1)
        {
            var state = machine.AddState(stateName, new Vector3(240 + index % 3 * 260, index / 3 * 100)); index++;
            state.motion = clips[clipName]; state.speed = speed; state.writeDefaultValues = true;
            // Runtime crossfades own sequencing; reversed clips already contain reversed curves.
            state.iKOnFeet = false;
            return state;
        }
        machine.defaultState = Add("Standing Idle", "Standing Idle");
        Add("Walking", "Walking"); Add("Walking Backward", "Walking Reverse");
        Add("Left Strafe Walk", "Left Strafe Walk"); Add("Right Strafe Walk", "Right Strafe Walk");
        Add("Pistol Draw", "Pistol Aim Reverse", PlayerLocomotionAnimation.DrawSpeed); Add("Pistol Idle", "Pistol Idle"); Add("Pistol Holster", "Pistol Aim");
        Add("Crouch Down", "Crouched To Standing Reverse"); Add("Stand Up", "Crouched To Standing");
        Add("Crouched Idle", "Crouched To Standing", 0);
        Add("Crouched Walking", "Crouched Walking"); Add("Crouched Backward", "Crouched Walking Reverse");
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimationClip InPlace(AnimationClip source)
    {
        const string folder = "Assets/Animations/Generated";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Animations", "Generated");
        string name = source.name + " In Place";
        string path = folder + "/" + name + ".anim";
        var result = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!result) { result = new AnimationClip(); AssetDatabase.CreateAsset(result, path); }
        EditorUtility.CopySerialized(source, result); result.name = name;
        // Remove travel before reversing: the last frame must share the first
        // frame's origin. Preserve vertical motion and the gait's horizontal sway.
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            if (binding.type != typeof(Animator) ||
                (binding.propertyName != "RootT.x" && binding.propertyName != "RootT.z")) continue;
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            float slope = (curve.Evaluate(source.length) - curve.Evaluate(0)) / source.length;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value -= slope * keys[i].time;
                keys[i].inTangent -= slope; keys[i].outTangent -= slope;
            }
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(result, binding, curve);
        }
        EditorUtility.SetDirty(result);
        return result;
    }

    static AnimationClip Reverse(AnimationClip source, string name)
    {
        const string folder = "Assets/Animations/Generated";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Animations", "Generated");
        string path = folder + "/" + name + ".anim";
        var result = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!result) { result = new AnimationClip(); AssetDatabase.CreateAsset(result, path); }
        EditorUtility.CopySerialized(source, result); result.name = name;
        // Reverse the actual Humanoid curves, including weighted tangents, rather than
        // relying on negative Animator state speeds for non-looping clips.
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            var keys = curve.keys.Reverse().Select(k => new Keyframe(source.length - k.time, k.value,
                -k.outTangent, -k.inTangent, k.outWeight, k.inWeight) {
                    weightedMode = (WeightedMode)(((int)k.weightedMode & 1) * 2 + ((int)k.weightedMode & 2) / 2)
                }).ToArray();
            var reversed = new AnimationCurve(keys) { preWrapMode = curve.postWrapMode, postWrapMode = curve.preWrapMode };
            AnimationUtility.SetEditorCurve(result, binding, reversed);
        }
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
            foreach (var i in Enumerable.Range(0, keys.Length)) keys[i].time = source.length - keys[i].time;
            AnimationUtility.SetObjectReferenceCurve(result, binding, keys.OrderBy(k => k.time).ToArray());
        }
        var events = AnimationUtility.GetAnimationEvents(source);
        foreach (var e in events) e.time = source.length - e.time;
        AnimationUtility.SetAnimationEvents(result, events.OrderBy(e => e.time).ToArray());
        EditorUtility.SetDirty(result);
        return result;
    }

    public static void ConfigureAndValidate()
    {
        string playerMetaPath = "Assets/Original_assets/Player_Humanoid.fbx.meta";
        string before = File.ReadAllText(playerMetaPath);
        PlayerLocomotionSetup.Configure();
        if (before != File.ReadAllText(playerMetaPath)) throw new Exception("Character rig was unexpectedly modified.");
        Validate();
    }

    public static void Validate()
    {
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var animator = root.GetComponentsInChildren<Animator>(true).First(a => a.runtimeAnimatorController);
        var driver = root.GetComponent<PlayerLocomotionAnimation>();
        var report = new List<string>();
        void Check(bool value, string message) { if (!value) throw new Exception(message); report.Add("PASS " + message); }
        try
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0);
            Check(animator.avatar.isValid && animator.avatar.isHuman, "Character avatar valid");
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            Check(controller.layers[0].stateMachine.states.Length == 13, "13 animation states assigned");
            foreach (string name in Sources)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Animations/" + name + ".fbx");
                var av = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<Avatar>().FirstOrDefault();
                Check(importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel && !importer.sourceAvatar && av && av.isHuman && av.isValid, name + " uses its own valid humanoid avatar");
            }
            foreach (var pair in new[] { (Vector3.zero,"Standing Idle"), (Vector3.forward,"Walking"),
                (Vector3.back,"Walking Backward"), (Vector3.left,"Left Strafe Walk"), (Vector3.right,"Right Strafe Walk") })
            {
                driver.UpdateLocomotion(pair.Item1); animator.Update(.20f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName(pair.Item2), "Locomotion " + pair.Item2);
            }
            float pistol = controller.animationClips.First(c => c.name == "Pistol Aim").length;
            float crouch = controller.animationClips.First(c => c.name == "Crouched To Standing").length;
            driver.AdvanceInput(Vector2.zero, false, true, 0); animator.Update(.05f);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Drawing, "Right click draws");
            Check(controller.layers[0].stateMachine.states.First(s => s.state.name == "Pistol Draw").state.speed == 2, "Draw state runs at double speed");
            driver.AdvanceInput(Vector2.zero, false, false, pistol / 2 - .01f);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Drawing, "Draw stays active until half clip duration");
            driver.AdvanceInput(Vector2.zero, false, false, .02f); animator.Update(.2f);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Aiming, "Draw ends in pistol idle");
            driver.AdvanceInput(Vector2.right, false, false, .01f);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Holstering && driver.BlocksMovement, "Movement holsters before translation");
            driver.AdvanceInput(Vector2.right, false, false, pistol + .01f);
            Check(!driver.BlocksMovement, "Movement released after holster");
            driver.AdvanceInput(Vector2.zero, true, false, 0);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Crouching, "Control starts crouch");
            driver.AdvanceInput(Vector2.zero, false, false, crouch + .01f); driver.UpdateLocomotion(Vector3.zero); animator.Update(.2f);
            Check(driver.IsCrouched && animator.GetCurrentAnimatorStateInfo(0).IsName("Crouched Idle"), "Crouch remains after releasing Control");
            driver.UpdateLocomotion(Vector3.back); animator.Update(.2f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Crouched Backward"), "Crouched S reverses walk");
            driver.AdvanceInput(Vector2.zero, true, false, 0);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Standing, "Second Control starts stand up");
            driver.AdvanceInput(Vector2.zero, false, false, crouch + .01f);
            Check(!driver.IsCrouched && !driver.BlocksMovement, "Stand up completes");
            // Check the reversed curves against the source at multiple points over the clip.
            foreach (var sourceName in new[] { "Walking", "Crouched Walking", "Pistol Aim", "Crouched To Standing" })
            {
                var source = controller.animationClips.First(c => c.name == sourceName +
                    (sourceName == "Walking" || sourceName == "Crouched Walking" ? " In Place" : ""));
                var reverse = controller.animationClips.First(c => c.name == sourceName + " Reverse");
                Check(reverse.humanMotion, sourceName + " reversed clip remains Humanoid");
                foreach (var binding in AnimationUtility.GetCurveBindings(source))
                {
                    var a = AnimationUtility.GetEditorCurve(source,binding);
                    var b = AnimationUtility.GetEditorCurve(reverse,binding);
                    foreach (float t in new[] {0f,.17f,.43f,.79f,1f})
                        if (Mathf.Abs(a.Evaluate(source.length*(1-t))-b.Evaluate(source.length*t)) > .003f)
                            throw new Exception("Reverse curve mismatch: " + sourceName + " / " + binding.propertyName);
                }
                Check(true, sourceName + " reversed curves match source");
            }
            // Sample the actual retargeted character, not just the imported curves.
            Vector3 SampleHips(string state, float time)
            {
                animator.Rebind(); animator.Play("Base Layer." + state, 0, time); animator.Update(0);
                return animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
            }
            foreach (var pair in new[] { ("Walking", "Walking Backward"), ("Crouched Walking", "Crouched Backward") })
            {
                var start = SampleHips(pair.Item1, 0);
                var backwardStart = SampleHips(pair.Item2, 0);
                Check(Vector2.Distance(new Vector2(start.x, start.z), new Vector2(backwardStart.x, backwardStart.z)) < .08f,
                    pair.Item2 + " starts at forward origin without teleporting");
                foreach (string state in new[] { pair.Item1, pair.Item2 })
                {
                    for (int i = 0; i <= 20; i++)
                    {
                        var pose = SampleHips(state, i / 20f);
                        Check(Vector2.Distance(new Vector2(start.x, start.z), new Vector2(pose.x, pose.z)) < .20f,
                            state + " stays in place at " + i);
                    }
                }
            }
            var low = SampleHips("Stand Up", 0);
            var high = SampleHips("Stand Up", 1);
            Check(high.y > low.y + .05f, "Crouch clip lowers the character hips");
            var reverseHigh = SampleHips("Crouch Down",0); var reverseLow = SampleHips("Crouch Down",1);
            Debug.Log($"CROUCH_POSES low={low:F5} high={high:F5} reverseStart={reverseHigh:F5} reverseEnd={reverseLow:F5}");
            Check(Vector3.Distance(reverseHigh, high) < .01f &&
                  Vector3.Distance(reverseLow, low) < .01f, "Reversed crouch endpoints retarget correctly");
            // Interrupting the draw must return from the same point, then allow crouch.
            driver.AdvanceInput(Vector2.zero, false, true, 0);
            driver.AdvanceInput(Vector2.zero, false, false, pistol * .25f);
            driver.AdvanceInput(Vector2.up, true, false, 0);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Holstering, "Interrupted draw holsters");
            driver.AdvanceInput(Vector2.zero, false, false, pistol * .49f);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Holstering, "Partial holster accounts for double draw speed");
            driver.AdvanceInput(Vector2.zero, false, false, pistol * .02f);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Crouching, "Queued crouch follows partial holster");
            File.WriteAllLines("Documentation/MixamoAnimationValidation.txt", report);
            Debug.Log("MIXAMO_VALIDATION_PASSED " + report.Count);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.CloseScene(scene, true);
            if (activeScene.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
        }
    }
}
