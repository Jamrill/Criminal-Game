using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class JumpMotionProcessing
{
    public static (float takeoff, float landing) Process(AnimationClip source, AnimationClip result)
    {
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(source)));
        var animator = model.GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var graph = PlayableGraph.Create("Measure jump contact");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var playable = AnimationClipPlayable.Create(graph, source);
        playable.SetApplyFootIK(false);
        AnimationPlayableOutput.Create(graph, "Jump", animator).SetSourcePlayable(playable);
        graph.Play();
        int count = Mathf.CeilToInt(source.length * 60);
        var heights = new float[count + 1];
        try
        {
            for (int i = 0; i <= count; i++)
            {
                playable.SetTime(source.length * i / count); graph.Evaluate(0);
                heights[i] = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,
                    animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y) / animator.humanScale;
            }
        }
        finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(model); }
        float ground = Mathf.Min(heights[0], heights[count]);
        int bestStart = 0, bestEnd = 0, start = -1;
        for (int i = 0; i <= count + 1; i++)
        {
            if (i <= count && heights[i] > ground + .045f) { if (start < 0) start = i; }
            else if (start >= 0)
            {
                if (i - start > bestEnd - bestStart) { bestStart = start; bestEnd = i - 1; }
                start = -1;
            }
        }
        if (bestEnd - bestStart < 5) throw new Exception("Cannot identify airborne interval: " + source.name);
        float takeoff = source.length * Mathf.Max(0, bestStart - 1) / count;
        float landing = source.length * Mathf.Min(count, bestEnd + 1) / count;
        var binding = AnimationUtility.GetCurveBindings(source).First(b => b.type == typeof(Animator) && b.propertyName == "RootT.y");
        var original = AnimationUtility.GetEditorCurve(source, binding);
        float y0 = original.Evaluate(takeoff), y1 = original.Evaluate(landing);
        float numerator = 0, denominator = 0;
        for (int i = 1; i < 60; i++)
        {
            float u = i / 60f, parabola = 4 * u * (1 - u);
            numerator += (original.Evaluate(Mathf.Lerp(takeoff, landing, u)) - Mathf.Lerp(y0, y1, u)) * parabola;
            denominator += parabola * parabola;
        }
        float rise = Mathf.Max(0, numerator / denominator);
        // Remove only the airborne trajectory; preserve anticipation, landing
        // compression, all muscles and the source body rotation.
        var keys = new Keyframe[count + 1];
        for (int i = 0; i <= count; i++)
        {
            float time = source.length * i / count;
            float u = Mathf.Clamp01((time - takeoff) / (landing - takeoff));
            keys[i] = new Keyframe(time, original.Evaluate(time) - rise * 4 * u * (1 - u));
        }
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0);
        AnimationUtility.SetEditorCurve(result, binding, curve);
        EditorUtility.SetDirty(result);
        Debug.Log($"JUMP_CONTACT {source.name} takeoff={takeoff:F3} landing={landing:F3} length={source.length:F3} removedArc={rise:F3}");
        return (takeoff, landing);
    }
}
