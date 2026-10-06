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
    public static readonly Dictionary<string, (float takeoff, float landing)> JumpContacts = new Dictionary<string, (float, float)>();
    const string ControllerPath = "Assets/Animations/PlayerLocomotion.controller";
    static readonly string[] Sources = { "Standing Idle", "Female Start Walking", "Female Walk", "Female Stop Walking", "Left Strafe Walk", "Right Strafe Walk",
        "Pistol Aim", "Pistol Idle", "Crouched To Standing", "Crouched Walking",
        "Jump_on_site", "Jump_in_movement", "Fast Run", "Running Right Turn" };

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
            bool loop = name != "Pistol Aim" && name != "Crouched To Standing" && name != "Female Start Walking" && name != "Female Stop Walking" &&
                name != "Jump_on_site" && name != "Jump_in_movement" && name != "Running Right Turn";
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
        foreach (string name in new[] { "Female Start Walking", "Female Walk", "Female Stop Walking" })
            clips[name] = InPlace(clips[name], clips["Standing Idle"]);
        foreach (string name in new[] { "Jump_on_site", "Jump_in_movement", "Fast Run", "Running Right Turn" })
        {
            var source = clips[name];
            clips[name] = InPlace(source, clips["Standing Idle"]);
            if (name.StartsWith("Jump_"))
            {
                JumpContacts[name] = JumpMotionProcessing.Process(source, clips[name]);
                continue;
            }
            // Capsule owns jump height and facing. Do not accumulate translation
            // or the turn's root rotation inside the visual child.
            if (name == "Fast Run") continue;
            foreach (var binding in AnimationUtility.GetCurveBindings(clips[name]))
            {
                if (binding.type != typeof(Animator)) continue;
                if (!binding.propertyName.StartsWith("RootQ.")) continue;
                var origin = AnimationUtility.GetEditorCurve(clips["Standing Idle"], binding);
                if (origin != null) AnimationUtility.SetEditorCurve(clips[name], binding,
                    AnimationCurve.Constant(0, clips[name].length, origin.Evaluate(0)));
            }
            EditorUtility.SetDirty(clips[name]);
        }
        clips["Walking"] = clips["Female Walk"];
        clips["Crouched Walking"] = InPlace(clips["Crouched Walking"]);
        clips["Walking Reverse"] = Reverse(clips["Walking"], "Walking Reverse");
        clips["Backward Start"] = Reverse(clips["Female Stop Walking"], "Backward Start");
        clips["Backward Stop"] = Reverse(clips["Female Start Walking"], "Backward Stop");
        clips["Crouched Walking Reverse"] = Reverse(clips["Crouched Walking"], "Crouched Walking Reverse");
        clips["Pistol Aim Reverse"] = Reverse(clips["Pistol Aim"], "Pistol Aim Reverse");
        clips["Crouched To Standing Reverse"] = Reverse(clips["Crouched To Standing"], "Crouched To Standing Reverse");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        var layers = controller.layers; layers[0].iKPass = true; controller.layers = layers;
        // This is the project's dedicated locomotion controller; GUID stays unchanged.
        foreach (var t in machine.anyStateTransitions) machine.RemoveAnyStateTransition(t);
        foreach (var s in machine.states) machine.RemoveState(s.state);
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("JumpSpeed", AnimatorControllerParameterType.Float);
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
        Add("Walk Start", "Female Start Walking"); Add("Walk Stop", "Female Stop Walking");
        Add("Backward Start", "Backward Start"); Add("Backward Stop", "Backward Stop");
        Add("Left Strafe Walk", "Left Strafe Walk"); Add("Right Strafe Walk", "Right Strafe Walk");
        Add("Pistol Draw", "Pistol Aim Reverse", PlayerLocomotionAnimation.DrawSpeed); Add("Pistol Idle", "Pistol Idle"); Add("Pistol Holster", "Pistol Aim", PlayerLocomotionAnimation.HolsterSpeed);
        Add("Crouch Down", "Crouched To Standing Reverse"); Add("Stand Up", "Crouched To Standing");
        Add("Crouched Idle", "Crouched To Standing", 0);
        Add("Crouched Walking", "Crouched Walking"); Add("Crouched Backward", "Crouched Walking Reverse");
        Add("Fast Run", "Fast Run");
        Add("Run Right Turn", "Running Right Turn");
        Add("Run Left Turn", "Running Right Turn").mirror = true;
        foreach (var pair in new[] { ("Jump On Site", "Jump_on_site"), ("Jump Moving", "Jump_in_movement") })
        {
            var state = Add(pair.Item1, pair.Item2);
            state.speedParameter = "JumpSpeed"; state.speedParameterActive = true;
        }
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimationClip InPlace(AnimationClip source, AnimationClip origin = null)
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
            if (origin)
            {
                // CharacterController owns translation. All three walking clips must
                // share one horizontal origin, including their reversed endpoints.
                var reference = AnimationUtility.GetEditorCurve(origin, binding);
                float value = reference != null ? reference.Evaluate(0) : 0;
                AnimationUtility.SetEditorCurve(result, binding, AnimationCurve.Constant(0, source.length, value));
                continue;
            }
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
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
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
            Check(controller.layers[0].stateMachine.states.Length == 22, "22 animation states assigned");
            foreach (string name in Sources)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Animations/" + name + ".fbx");
                var av = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<Avatar>().FirstOrDefault();
                Check(importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel && !importer.sourceAvatar && av && av.isHuman && av.isValid, name + " uses its own valid humanoid avatar");
            }
            foreach (var pair in new[] { (Vector3.zero,"Standing Idle"), (Vector3.forward,"Walking"),
                (Vector3.back,"Walking Backward"), (Vector3.left,"Left Strafe Walk"), (Vector3.right,"Right Strafe Walk") })
            {
                driver.UpdateLocomotion(pair.Item1); animator.Update(.10f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName(pair.Item2), "Locomotion " + pair.Item2);
            }
            foreach (var direction in new[] {Vector3.forward, Vector3.back})
            {
                driver.UpdateLocomotion(direction); animator.Update(10);
                driver.UpdateLocomotion(direction); animator.Update(.20f);
                driver.UpdateLocomotion(Vector3.zero); animator.Update(.10f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Standing Idle"), "Release stops feet immediately " + direction.z);
                animator.Update(10); driver.UpdateLocomotion(Vector3.zero); animator.Update(.20f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Standing Idle"), "Stop completes " + direction.z);
            }
            foreach (var input in new[] {new Vector2(-1,1), new Vector2(1,1), new Vector2(-1,-1), new Vector2(1,-1)})
            {
                for (int frame = 0; frame < 12; frame++)
                {
                    float jitter = frame % 2 == 0 ? .02f : -.02f;
                    driver.UpdateLocomotion(new Vector3(input.x * (.7f + jitter), 0, input.y * (.7f - jitter)), input);
                    animator.Update(.1f);
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName(input.y > 0 ? "Walking" : "Walking Backward"),
                        "Diagonal keeps longitudinal animation " + input + " frame " + frame);
                }
            }
            driver.UpdateLocomotion(Vector3.zero); animator.Update(.2f);
            driver.UpdateLocomotion(Vector3.forward * 6, Vector2.up, true); animator.Update(.4f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Fast Run"), "Sprint uses Fast Run");
            foreach (int direction in new[] {1,-1})
            {
                var input = new Vector2(direction, 1);
                driver.UpdateLocomotion(new Vector3(direction,0,1), input, true); animator.Update(.4f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName(direction > 0 ? "Run Right Turn" : "Run Left Turn"), "Running turn " + direction);
                animator.Update(10); driver.UpdateLocomotion(new Vector3(direction,0,1), input, true); animator.Update(.4f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Fast Run"), "Holding diagonal does not loop turn " + direction);
            }
            Check(controller.layers[0].stateMachine.states.First(s => s.state.name == "Run Left Turn").state.mirror, "Left turn mirrors right turn");
            foreach (bool moving in new[] {false,true})
            {
                driver.BeginJump(moving, .7f); animator.Update(.1f);
                Check(driver.IsJumping && driver.BlocksJump && !driver.BlocksMovement, "Jump blocks second jump but permits translation");
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName(moving ? "Jump Moving" : "Jump On Site"), "Jump clip selected " + moving);
                Check(!driver.ConsumeJumpTakeoff(), "Preparation precedes physical takeoff");
                driver.AdvanceInput(Vector2.zero, false, false, 10);
                Check(driver.ConsumeJumpTakeoff(), "Jump launches after preparation");
                driver.AdvanceInput(Vector2.zero, true, true, .1f);
                driver.UpdateLocomotion(Vector3.forward, Vector2.up, true); animator.Update(.1f);
                Check(driver.IsJumping && driver.Phase == PlayerLocomotionAnimation.ActionPhase.Locomotion, "Airborne actions do not interrupt jump");
                driver.UpdateJumpContact(true, 1);
                Check(driver.IsJumping, "Ground contact on ascending launch does not cancel jump");
                driver.UpdateJumpContact(false, -1);
                Check(driver.IsJumping, "Descending in air keeps jump");
                driver.UpdateJumpContact(true, -1);
                driver.AdvanceInput(Vector2.zero, false, false, 10);
                driver.UpdateLocomotion(Vector3.zero); animator.Update(.2f);
                Check(!driver.IsJumping && animator.GetCurrentAnimatorStateInfo(0).IsName("Standing Idle"), "Landing resumes locomotion");
            }
            float pistol = controller.animationClips.First(c => c.name == "Pistol Aim").length;
            float crouch = controller.animationClips.First(c => c.name == "Crouched To Standing").length;
            driver.AdvanceInput(Vector2.zero, false, true, 0); animator.Update(.05f);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Drawing, "Right click draws");
            Check(controller.layers[0].stateMachine.states.First(s => s.state.name == "Pistol Draw").state.speed == PlayerLocomotionAnimation.DrawSpeed, "Draw state speed matches action timer");
            Check(controller.layers[0].stateMachine.states.First(s => s.state.name == "Pistol Holster").state.speed == PlayerLocomotionAnimation.HolsterSpeed, "Holster state speed matches action timer");
            driver.AdvanceInput(Vector2.zero, false, false, pistol / PlayerLocomotionAnimation.DrawSpeed - .01f);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Drawing, "Draw stays active until adjusted clip duration");
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
                var source = controller.animationClips.First(c => c.name == (sourceName == "Walking" ? "Female Walk" : sourceName) +
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
            var idleOrigin = SampleHips("Standing Idle", 0);
            foreach (string jumpName in new[] { "Jump_on_site", "Jump_in_movement" })
            {
                var original = AssetDatabase.LoadAllAssetsAtPath("Assets/Animations/" + jumpName + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
                var generated = controller.animationClips.First(c => c.name == jumpName + " In Place");
                foreach (var binding in AnimationUtility.GetCurveBindings(original))
                {
                    if (binding.propertyName.StartsWith("RootT.")) continue;
                    var a = AnimationUtility.GetEditorCurve(original, binding);
                    var b = AnimationUtility.GetEditorCurve(generated, binding);
                    for (int i = 0; i <= 40; i++)
                        Check(b != null && Mathf.Abs(a.Evaluate(original.length * i / 40) - b.Evaluate(original.length * i / 40)) < .001f,
                            jumpName + " preserves source pose " + binding.propertyName + " sample " + i);
                }
                foreach (var binding in AnimationUtility.GetCurveBindings(generated).Where(b => b.propertyName == "RootT.x" || b.propertyName == "RootT.z"))
                {
                    var curve = AnimationUtility.GetEditorCurve(generated, binding);
                    Check(curve.keys.All(k => Mathf.Abs(k.value - curve.Evaluate(0)) < .001f), jumpName + " has no horizontal root travel " + binding.propertyName);
                }
            }
            foreach (string state in new[] { "Walk Start", "Walk Stop", "Backward Start", "Backward Stop", "Walking", "Walking Backward",
                "Fast Run", "Run Right Turn", "Run Left Turn" })
            {
                for (int i = 0; i <= 20; i++)
                {
                    var hips = SampleHips(state, i / 20f);
                    Check(Vector2.Distance(new Vector2(idleOrigin.x, idleOrigin.z), new Vector2(hips.x, hips.z)) < .20f,
                        state + " stays near idle/capsule origin at " + i);
                }
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
            driver.AdvanceInput(Vector2.zero, false, false, pistol * .5f / PlayerLocomotionAnimation.DrawSpeed);
            driver.AdvanceInput(Vector2.up, true, false, 0);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Holstering, "Interrupted draw holsters");
            driver.AdvanceInput(Vector2.zero, false, false, pistol * .49f / PlayerLocomotionAnimation.HolsterSpeed);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Holstering, "Partial holster accounts for both playback speeds");
            driver.AdvanceInput(Vector2.zero, false, false, pistol * .02f / PlayerLocomotionAnimation.HolsterSpeed);
            Check(driver.Phase == PlayerLocomotionAnimation.ActionPhase.Crouching, "Queued crouch follows partial holster");
            File.WriteAllLines("Documentation/MixamoAnimationValidation.txt", report);
            Debug.Log("MIXAMO_VALIDATION_PASSED " + report.Count);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            if (!Application.isBatchMode)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (activeScene.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
            }
        }
    }
}
