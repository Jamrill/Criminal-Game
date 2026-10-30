using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class WalkingDiagnostics
{
    public static void Run()
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Animations/Walking.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (binding.propertyName.StartsWith("RootT") || binding.propertyName.Contains("Left Leg") || binding.propertyName.Contains("Left Upper Leg"))
                Debug.Log($"CURVE {binding.propertyName} start={curve.Evaluate(0):F5} end={curve.Evaluate(clip.length):F5} firstStep={curve.Evaluate(.01f)-curve.Evaluate(0):F5} lastStep={curve.Evaluate(clip.length)-curve.Evaluate(clip.length-.01f):F5}");
        }
        foreach (string path in new[] { "Assets/Animations/Walking.fbx", "Assets/Prefabs/Player.prefab" })
        foreach (bool ik in new[] { false, true })
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var animator = root.GetComponentsInChildren<Animator>().First();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            var graph = PlayableGraph.Create("WalkingDiagnostic");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(ik);
            AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
            graph.Play();
            try
            {
                foreach (double phase in new[] {0d,.1,.2,.3,.4,.5,.6,.7,.8,.9,.99,1.0,1.01})
                {
                    playable.SetTime(phase * clip.length); graph.Evaluate(0);
                    string result = $"WALK {path} IK={ik} phase={phase:F2}";
                    foreach (var side in new[] {HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg})
                    {
                        bool left = side == HumanBodyBones.LeftUpperLeg;
                        var hip = animator.GetBoneTransform(side).position;
                        var knee = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg).position;
                        var foot = animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot).position;
                        var line = foot-hip;
                        var bend = knee - (hip + Vector3.Project(knee-hip,line));
                        result += $" {(left ? "L" : "R")}K={root.transform.InverseTransformPoint(knee)/animator.humanScale:F4} F={root.transform.InverseTransformPoint(foot)/animator.humanScale:F4} bend={root.transform.InverseTransformDirection(bend)/animator.humanScale:F4}";
                    }
                    Debug.Log(result);
                }
            }
            finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
