using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StairsRampSetup
{
    const string Path = "Assets/Prefabs/Stairs.prefab";

    [MenuItem("Tools/Criminal Game/Ajustar rampas de Stairs")]
    public static void ApplyAndValidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Sal de Play y de Prefab Mode antes de ajustar Stairs.");
        var root = PrefabUtility.LoadPrefabContents(Path);
        var report = new List<string>();
        try
        {
            var flights = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh &&
                AssetDatabase.GetAssetPath(f.sharedMesh) == "Assets/Original_assets/Stairs.fbx").ToArray();
            if (flights.Length != 2) throw new InvalidOperationException("Expected exactly two stair meshes, found " + flights.Length);
            // Fit both ramps before touching the prefab, so unexpected geometry aborts safely.
            var fits = flights.Select(f => Fit(f.transform, f)).ToArray();
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            for (int i = 0; i < fits.Length; i++)
            {
                string name = "CollisionRamp";
                var child = flights[i].transform.Find(name);
                if (!child) { child = new GameObject(name).transform; child.SetParent(flights[i].transform, false); }
                var fit = fits[i];
                child.localPosition = fit.center; child.localRotation = fit.rotation; child.localScale = Vector3.one;
                child.gameObject.layer = flights[i].gameObject.layer;
                var box = child.gameObject.AddComponent<BoxCollider>(); box.size = fit.size;
                report.Add($"PASS {flights[i].name}/{name} center={fit.center:F4} size={fit.size:F4} rotation={fit.rotation.eulerAngles:F3}");
            }
            if (root.GetComponentsInChildren<Collider>(true).Length != 2) throw new Exception("Expected only two ramp colliders.");
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        PlayerAnimationExpansion.Validate();
        report.Add("PASS locomotion validation");
        System.IO.File.WriteAllLines("Documentation/StairsRampResult.txt", report);
    }

    static (Vector3 center, Quaternion rotation, Vector3 size) Fit(Transform root, MeshFilter filter)
    {
        var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        var vertices = filter.sharedMesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
        var triangles = filter.sharedMesh.triangles;
        var centers = new List<Vector3>(); var surface = new List<Vector3>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.magnitude < .02f || Vector3.Dot(normal.normalized, Vector3.up) < .999f) continue;
            centers.Add((a + b + c) / 3); surface.AddRange(new[] { a, b, c });
        }
        if (centers.Count < 12) throw new Exception("Insufficient horizontal treads in " + filter.name);
        // Work with each whole tread's midpoint rather than triangulation-biased centers.
        var groups = surface.GroupBy(v => Mathf.RoundToInt(v.y * 1000)).ToArray();
        // The narrow central stringer also has upward faces. Keep only faces
        // spanning the full stair width, otherwise they bias the ramp slope.
        bool widthIsX = vertices.Max(v => v.x) - vertices.Min(v => v.x) < vertices.Max(v => v.z) - vertices.Min(v => v.z);
        Func<Vector3, float> acrossStair = v => widthIsX ? v.x : v.z;
        float fullWidth = vertices.Max(acrossStair) - vertices.Min(acrossStair);
        surface = groups.Where(g => g.Max(acrossStair) - g.Min(acrossStair) > fullWidth * .85f).SelectMany(g => g).ToList();
        var treads = surface.GroupBy(v => Mathf.RoundToInt(v.y * 1000)).Select(g => new Vector3(
            (g.Min(v => v.x) + g.Max(v => v.x)) / 2, g.Average(v => v.y),
            (g.Min(v => v.z) + g.Max(v => v.z)) / 2)).ToArray();
        float xSpan = treads.Max(v => v.x) - treads.Min(v => v.x);
        float zSpan = treads.Max(v => v.z) - treads.Min(v => v.z);
        bool alongX = xSpan > zSpan;
        Func<Vector3, float> run = v => alongX ? v.x : v.z;
        Func<Vector3, float> width = v => alongX ? v.z : v.x;
        float mean = treads.Average(run), height = treads.Average(v => v.y);
        float variance = treads.Sum(v => Mathf.Pow(run(v) - mean, 2));
        if (variance < .1f) throw new Exception("Degenerate stair run.");
        float slope = treads.Sum(v => (run(v) - mean) * (v.y - height)) / variance;
        float start = surface.Min(run), end = surface.Max(run);
        float mid = (start + end) / 2, across = (surface.Min(width) + surface.Max(width)) / 2;
        var forward = (alongX ? new Vector3(1, slope, 0) : new Vector3(0, slope, 1)).normalized;
        var rotation = Quaternion.LookRotation(forward, Vector3.up);
        const float thickness = .18f;
        var top = alongX ? new Vector3(mid, height + slope * (mid - mean), across) : new Vector3(across, height + slope * (mid - mean), mid);
        float error = treads.Max(v => Mathf.Abs(v.y - (height + slope * (run(v) - mean))));
        if (error > .15f || Mathf.Abs(slope) < .05f) throw new Exception("Unexpected stair surface: fit error=" + error);
        return (top - rotation * Vector3.up * thickness / 2, rotation,
            new Vector3(surface.Max(width) - surface.Min(width), thickness, (end - start) * Mathf.Sqrt(1 + slope * slope)));
    }
}
