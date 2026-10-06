using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using JuegoCriminal.Player;

[InitializeOnLoad]
public static class PlayerLocomotionSetup
{
    const string PlayerModel = "Assets/Original_assets/Player_Humanoid.fbx";
    const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    const string ControllerPath = "Assets/Animations/PlayerLocomotion.controller";
    const string SessionKey = "CriminalGame.PlayerLocomotionSetup.v1";

    static PlayerLocomotionSetup() { EditorApplication.delayCall += TryAutomaticSetup; }

    static void TryAutomaticSetup()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            PrefabStageUtility.GetCurrentPrefabStage() != null || SessionState.GetBool(SessionKey, false)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        if (prefab && prefab.GetComponent<PlayerLocomotionAnimation>()) return;
        SessionState.SetBool(SessionKey, true);
        Configure();
    }

    [MenuItem("Tools/Criminal Game/Configurar animaciones del jugador")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogWarning("Sal de Play y cierra Prefab Mode antes de configurar las animaciones.");
            return;
        }
        try
        {
            // Preserve the valid character Avatar and its user-adjusted T-pose.
            var avatar = ValidAvatar(PlayerModel);
            var controller = PlayerAnimationExpansion.BuildController();
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var model = root.GetComponentsInChildren<Transform>(true)
                    .OrderByDescending(t => AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)) == PlayerModel)
                    .FirstOrDefault(t =>
                    t != root.transform && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) is GameObject source &&
                    (AssetDatabase.GetAssetPath(source) == PlayerModel || AssetDatabase.GetAssetPath(source) == "Assets/Original_assets/Player.fbx") && t.parent == root.transform);
                if (!model) throw new InvalidOperationException("No se encuentra el modelo Player.fbx como hijo del prefab.");
                if (AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject)) != PlayerModel)
                {
                    // Keep the old model recoverable in the prefab until the replacement is validated.
                    var replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModel), root.scene);
                    replacement.transform.SetParent(root.transform, false);
                    replacement.transform.localPosition = model.localPosition;
                    replacement.transform.localRotation = model.localRotation;
                    replacement.transform.localScale = model.localScale;
                    foreach (var renderer in replacement.GetComponentsInChildren<Renderer>(true))
                    {
                        var old = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == renderer.name);
                        if (old) renderer.sharedMaterials = old.sharedMaterials;
                    }
                    model.gameObject.SetActive(false);
                    model.name = "Player_PreHumanoid_Backup";
                    model = replacement.transform;
                }
                var animator = model.GetComponent<Animator>();
                if (!animator) animator = model.gameObject.AddComponent<Animator>();
                if (animator.runtimeAnimatorController && animator.runtimeAnimatorController != controller)
                    throw new InvalidOperationException("El modelo ya tiene otro Animator Controller; no se sobrescribe.");
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                if (!model.GetComponent<PlayerHeadLook>()) model.gameObject.AddComponent<PlayerHeadLook>();
                var driver = root.GetComponent<PlayerLocomotionAnimation>();
                if (!driver) driver = root.AddComponent<PlayerLocomotionAnimation>();
                driver.Configure(animator);
                driver.ConfigureTimings(controller.animationClips.First(c => c.name == "Pistol Aim").length,
                    controller.animationClips.First(c => c.name == "Crouched To Standing").length);
                driver.ConfigureJumpTimings(controller.animationClips.First(c => c.name == "Jump_on_site In Place").length,
                    controller.animationClips.First(c => c.name == "Jump_in_movement In Place").length);
                var stationaryContact = PlayerAnimationExpansion.JumpContacts["Jump_on_site"];
                var movingContact = PlayerAnimationExpansion.JumpContacts["Jump_in_movement"];
                driver.ConfigureJumpContacts(stationaryContact.takeoff, stationaryContact.landing, movingContact.takeoff, movingContact.landing);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Jugador configurado: caminar, strafe, pistola y agachado con transiciones. Avatar del personaje conservado.");
        }
        catch (Exception error) { Debug.LogError("No se ha completado la configuración de animaciones: " + error); }
    }

    static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode condition)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = .18f;
        transition.AddCondition(condition, 0, "IsMoving");
    }

    [Serializable] public class PoseMesh { public string name; public Vector3[] vertices; public int[] triangles; }
    [Serializable] public class PoseSnapshot { public PoseMesh[] meshes; public float floor; }

    public static void RepairAndValidate()
    {
        Configure();
        AlignFeetAndValidate();
    }

    public static void FinalizeGroundAlignment()
    {
        AlignFeetAndValidate();
        ValidateGroundContact();
    }

    public static void ValidateGroundContact()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Ground validation is batch-only.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            var ground = new GameObject("Ground", typeof(BoxCollider));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
            ground.GetComponent<BoxCollider>().size = new Vector3(100, 1, 100);
            ground.transform.position = new Vector3(0, -.5f, 0);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), scene);
            root.transform.position = new Vector3(0, 5, 0);
            var controller = root.GetComponent<CharacterController>();
            Physics.SyncTransforms();
            for (int i = 0; i < 100; i++) controller.Move(Vector3.down * .1f);
            var animator = root.GetComponentsInChildren<Animator>().First(a => a.runtimeAnimatorController);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.SetFloat("PlaybackSpeed", 1);
            foreach (var state in new[] { "Standing Idle", "Walking" })
            {
                animator.SetBool("IsMoving", state == "Walking");
                animator.Play(state, 0, 0); animator.Update(0);
                for (int i = 0; i < 120; i++) animator.Update(1f / 60f);
                var body = animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == "Retopo_Personaje_femenino");
                var mesh = new Mesh(); body.BakeMesh(mesh);
                float bottom = mesh.vertices.Min(v => body.transform.TransformPoint(v).y);
                UnityEngine.Object.DestroyImmediate(mesh);
                Debug.Log($"GROUND_CONTACT {state}: grounded={controller.isGrounded}, rootY={root.transform.position.y:F5}, feetY={bottom:F5}, groundY=0");
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    public static void AlignFeetAndValidate()
    {
        float correction;
        var sample = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var animator = sample.GetComponentsInChildren<Animator>(true).First(a => a.gameObject.activeInHierarchy && a.runtimeAnimatorController);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.SetFloat("PlaybackSpeed", 1);
            var body = animator.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == "Retopo_Personaje_femenino");
            var mesh = new Mesh();
            float minimum = float.PositiveInfinity;
            try
            {
                foreach (string state in new[] { "Standing Idle" })
                {
                    animator.SetBool("IsMoving", state == "Walking");
                    animator.Play(state, 0, 0); animator.Update(0);
                    for (int i = 0; i < 40; i++)
                    {
                        animator.Update(1f / 20f);
                        if (i != 39) continue;
                        body.BakeMesh(mesh);
                        foreach (var vertex in mesh.vertices)
                            minimum = Mathf.Min(minimum, sample.transform.InverseTransformPoint(body.transform.TransformPoint(vertex)).y);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            var cc = sample.GetComponent<CharacterController>();
            // CharacterController rests skinWidth above the geometric bottom.
            // Align the idle contact pose, not the deepest transient vertex of a walk.
            correction = cc.center.y - cc.height * .5f - cc.skinWidth - minimum;
            Debug.Log($"FOOT_ALIGNMENT correction={correction:F6} sampledMinimum={minimum:F6}");
            if (float.IsNaN(correction) || Mathf.Abs(correction) > .5f) throw new Exception("Unexpected foot offset; prefab not saved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(sample); }
        // Reload so sampled poses never become prefab overrides.
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var animator = root.GetComponentsInChildren<Animator>(true).First(a => a.gameObject.activeInHierarchy && a.runtimeAnimatorController);
            animator.transform.localPosition += Vector3.up * correction;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        ValidateInstalled();
    }

    public static void ValidateInstalled()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var animator = root.GetComponentsInChildren<Animator>(true).First(a => a.gameObject.activeInHierarchy && a.runtimeAnimatorController);
            if (!animator.avatar.isValid || !animator.avatar.isHuman) throw new Exception("Invalid installed avatar");
            var old = root.transform.Find("Player_PreHumanoid_Backup");
            foreach (var r in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var prior = old.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x => x.name == r.name);
                if (!prior) continue;
                var a = new Mesh(); var b = new Mesh();
                r.BakeMesh(a); prior.BakeMesh(b);
                var ac = r.transform.TransformPoint(a.bounds.center);
                var bc = prior.transform.TransformPoint(b.bounds.center);
                Debug.Log($"PLACEMENT {r.name}: old={bc:F5}, new={ac:F5}, delta={Vector3.Distance(ac, bc):F6}, size={a.bounds.size:F5}");
                UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b);
            }
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.SetFloat("PlaybackSpeed", 1);
            foreach (string state in new[] { "Standing Idle", "Walking" })
            {
                animator.SetBool("IsMoving", state == "Walking");
                animator.Play(state, 0, 0); animator.Update(0);
                var leg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                Quaternion first = leg.rotation; Quaternion firstHead = head.rotation;
                for (int i = 0; i < 120; i++) animator.Update(1f / 60f);
                var meshes = new List<PoseMesh>();
                float minimum = float.PositiveInfinity;
                foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var baked = new Mesh();
                    renderer.BakeMesh(baked);
                    var vertices = baked.vertices.Select(v => root.transform.InverseTransformPoint(renderer.transform.TransformPoint(v))).ToArray();
                    minimum = Mathf.Min(minimum, vertices.Min(v => v.y));
                    meshes.Add(new PoseMesh { name = renderer.name, vertices = vertices, triangles = baked.triangles });
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                var cc = root.GetComponent<CharacterController>();
                float floor = cc.center.y - cc.height * .5f;
                System.IO.File.WriteAllText("Documentation/PlayerPose_" + state.Replace(" ", "_") + ".json",
                    JsonUtility.ToJson(new PoseSnapshot { meshes = meshes.ToArray(), floor = floor }));
                Debug.Log($"FEET {state}: meshBottom={minimum:F5}, capsuleBottom={floor:F5}, difference={minimum-floor:F5}");
                Debug.Log($"ANIMATION {state}: legDelta={Quaternion.Angle(first, leg.rotation):F5}, headDelta={Quaternion.Angle(firstHead, head.rotation):F5}, state={animator.GetCurrentAnimatorStateInfo(0).shortNameHash}, human={animator.isHuman}");
            }
            Debug.Log("PLAYER_VALIDATION_COMPLETE");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Avatar ValidAvatar(string path)
    {
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(a => a.isValid && a.isHuman);
        if (!avatar) throw new InvalidOperationException("Avatar Humanoid no válido en " + path + ". Revisa Rig > Configure; no se conectará un avatar inválido.");
        return avatar;
    }

    static AnimationClip ConfigureClip(string path, string name)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        var clips = importer.clipAnimations;
        if (clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips.Length != 1) throw new InvalidOperationException("Se esperaba una sola animación en " + path);
        clips[0].name = name;
        clips[0].loopTime = true;
        clips[0].loopPose = true;
        clips[0].lockRootRotation = true;
        clips[0].lockRootHeightY = true;
        clips[0].lockRootPositionXZ = true;
        clips[0].keepOriginalOrientation = true;
        clips[0].keepOriginalPositionY = false;
        clips[0].heightFromFeet = true;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        ValidAvatar(path);
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
        if (!clip || !clip.humanMotion) throw new InvalidOperationException("Clip Humanoid no válido: " + path);
        return clip;
    }

    static void ConfigurePlayerAvatar()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(PlayerModel);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModel);
        var transforms = model.GetComponentsInChildren<Transform>(true);
        var mapping = new Dictionary<string, string> {
            {"Hips", "DEF-spine"}, {"Spine", "DEF-spine.001"}, {"Chest", "DEF-spine.003"},
            {"Neck", "DEF-spine.005"}, {"Head", "DEF-spine.006"} };
        foreach (var side in new[] { "Left", "Right" })
        {
            string suffix = side == "Left" ? ".L" : ".R";
            mapping.Add(side + "UpperLeg", "DEF-thigh" + suffix);
            mapping.Add(side + "LowerLeg", "DEF-shin" + suffix);
            mapping.Add(side + "Foot", "DEF-foot" + suffix);
            mapping.Add(side + "Toes", "DEF-toe" + suffix);
            mapping.Add(side + "UpperArm", "DEF-upper_arm" + suffix);
            mapping.Add(side + "LowerArm", "DEF-forearm" + suffix);
            mapping.Add(side + "Hand", "DEF-hand" + suffix);
        }
        foreach (string bone in mapping.Values)
            if (!transforms.Any(t => t.name == bone)) throw new InvalidOperationException("Falta el hueso " + bone);
        var description = importer.humanDescription;
        description.human = mapping.Select(pair => new HumanBone {
            humanName = pair.Key, boneName = pair.Value, limit = new HumanLimit { useDefaultValues = true }
        }).ToArray();
        // Use Unity's own Enforce T-Pose, on a disposable instance, not the bind mesh.
        var poseInstance = UnityEngine.Object.Instantiate(model);
        poseInstance.name = model.name;
        try
        {
            var tool = typeof(Editor).Assembly.GetType("UnityEditor.AvatarSetupTool", true);
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var bones = poseInstance.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => true);
            var getBones = tool.GetMethod("GetHumanBones", flags, null,
                new[] { typeof(Dictionary<string, string>), typeof(Dictionary<Transform, bool>) }, null);
            if (getBones == null) throw new InvalidOperationException("Unity AvatarSetupTool API changed; no pose was applied.");
            var wrappers = getBones.Invoke(null, new object[] { mapping, bones });
            var enforce = tool.GetMethod("MakePoseValid", flags);
            enforce.Invoke(null, new[] { wrappers });
            enforce.Invoke(null, new[] { wrappers });
            Debug.Log("HUMANOID_TPOSE_ERROR " + tool.GetMethod("GetPoseError", flags).Invoke(null, new[] { wrappers }));
            description.skeleton = poseInstance.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone {
                name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
            }).ToArray();
        }
        finally { UnityEngine.Object.DestroyImmediate(poseInstance); }
        string previousMeta = System.IO.File.ReadAllText(PlayerModel + ".meta");
        try
        {
            importer.humanDescription = description;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            ValidAvatar(PlayerModel);
        }
        catch
        {
            // An invalid Rigify mapping must not leave the user's model broken.
            System.IO.File.WriteAllText(PlayerModel + ".meta", previousMeta);
            AssetDatabase.ImportAsset(PlayerModel, ImportAssetOptions.ForceUpdate);
            throw;
        }
    }
}
