using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FurnitureColliderRefinement
{
    static readonly string[] Names = { "Sofa", "Floor_lamp", "Toilet", "Table", "Chair", "Bed", "Kitchen cabinet" };
    [MenuItem("Tools/Criminal Game/Ajustar colision detallada de muebles")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Sal de Play y Prefab Mode.");
        var report = new List<string>();
        foreach (string name in Names)
        {
            string path = "Assets/Prefabs/Furniture/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var c in root.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger)) UnityEngine.Object.DestroyImmediate(c);
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh))
                {
                    void Box(float x0, float y0, float z0, float x1, float y1, float z1)
                    {
                        var box = f.gameObject.AddComponent<BoxCollider>();
                        var min = new Vector3(x0,y0,z0); var max = new Vector3(x1,y1,z1);
                        box.center = (min + max)/2; box.size = max-min;
                    }
                    void BoundsBox(Bounds b) => Box(b.min.x,b.min.y,b.min.z,b.max.x,b.max.y,b.max.z);
                    var islands = Islands(f.sharedMesh);
                    if (name == "Kitchen cabinet" && f.transform == root.transform)
                    {
                        // Measured outer/inner faces. No box across the storage volume.
                        Box(.3f,0,.3f,.3142f,1.9439f,2.3f);
                        Box(1.9858f,0,.3f,2,1.9439f,2.3f);
                        Box(.3142f,0,.3f,1.9858f,1.9439f,.342f);
                        Box(.3142f,0,.342f,1.9858f,.1999f,2.0597f);
                        Box(.3142f,.1844f,2.0597f,1.9858f,.1999f,2.3f);
                        Box(.3142f,1.8419f,.342f,1.9858f,1.9439f,2.3595f);
                    }
                    else if (name == "Sofa" && f.transform == root.transform)
                    {
                        Box(.3f,.6f,.9f,7.7f,1,2.9f); // seat support
                        Box(.3f,1,.9f,.7933f,1.935f,2.9f);
                        Box(7.2067f,1,.9f,7.7f,1.935f,2.9f);
                        foreach (var b in islands.Skip(1)) BoundsBox(b); // back and individual feet
                    }
                    else if (name == "Bed" && f.transform == root.transform)
                    {
                        Box(.3f,.9f,.3f,7.7f,1.3f,3.7f);
                        Box(.3f,1.3f,.3f,.433f,2.4155f,3.7f);
                        Box(7.567f,1.3f,.3f,7.7f,1.8328f,3.7f);
                        foreach (var b in islands.Skip(1)) BoundsBox(b);
                    }
                    else if (name == "Chair")
                    {
                        Box(.9494f,1.1924f,1.0893f,3.0506f,1.2924f,3.0735f);
                        foreach (float x in new[] {1f,2.7667f})
                        {
                            Box(x,0,2.80f,x+.2333f,1.1924f,3.04f);
                            Box(x,0,1.0343f,x+.2333f,3.0091f,1.39f);
                        }
                        Box(1.2333f,1.4377f,1.137f,2.7667f,1.6662f,1.38f);
                        Box(1.2333f,2.8091f,1.0343f,2.7667f,3.0091f,1.28f);
                        Box(1,.8924f,1.15f,3,1.1924f,1.25f);
                        Box(1,.8924f,2.936f,3,1.1924f,3.036f);
                        Box(1,.8924f,1.25f,1.1f,1.1924f,2.936f);
                        Box(2.9f,.8924f,1.25f,3,1.1924f,2.936f);
                    }
                    else if (name == "Floor_lamp")
                    {
                        Box(-1,-1,-1,1,1,1);
                        Box(-.0771f,1,-.0771f,.0771f,39.5662f,.0771f);
                        // Thin shade walls, not a solid column from floor to shade.
                        for (int i = 0; i < 8; i++)
                        {
                            string childName = "ShadeCollision_" + i;
                            var child = f.transform.Find(childName);
                            if (!child) { child = new GameObject(childName).transform; child.SetParent(f.transform,false); }
                            float angle = i * 45;
                            child.localRotation = Quaternion.Euler(0,angle,0);
                            child.localPosition = child.localRotation * new Vector3(0,42.274f,.9f);
                            child.localScale = Vector3.one; child.gameObject.layer = f.gameObject.layer;
                            var box = child.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(.75f,12,.2f);
                        }
                    }
                    else if (name == "Toilet" && f.transform == root.transform)
                    {
                        Box(1.2214f,0,1.1691f,2.7786f,.25f,2.5201f);
                        Box(1.2096f,.25f,1.1591f,2.7904f,2.6351f,1.475f);
                        Box(1.4f,.25f,1.475f,2.6f,.80f,2.55f);
                        Box(1.2096f,.25f,1.475f,1.4f,1.1482f,2.55f);
                        Box(2.6f,.25f,1.475f,2.7904f,1.1482f,2.55f);
                        Box(1.384f,.25f,2.55f,2.616f,1.1482f,2.7387f);
                    }
                    else foreach (var b in islands) BoundsBox(b);
                }
                var solid = root.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
                if (solid.Any(c => !(c is BoxCollider))) throw new Exception("Non-box collider: " + name);
                foreach (BoxCollider box in solid)
                    if (box.size.x <= 0 || box.size.y <= 0 || box.size.z <= 0) throw new Exception("Invalid box: " + name);
                // Check representative empty regions against all solid boxes,
                // including children (local tests avoid global physics scene state).
                void Empty(Vector3 point)
                {
                    var world = root.transform.TransformPoint(point);
                    foreach (BoxCollider box in solid)
                        if (new Bounds(box.center,box.size).Contains(box.transform.InverseTransformPoint(world)))
                            throw new Exception(name + " blocks empty space at " + point);
                }
                if (name == "Kitchen cabinet")
                    foreach (float x in new[]{.4f,1.15f,1.9f})
                    foreach (float y in new[]{.3f,1f,1.7f})
                    foreach (float z in new[]{.45f,1.2f,2.15f}) Empty(new Vector3(x,y,z));
                if (name == "Table") Empty(new Vector3(4,.8f,2));
                if (name == "Chair") { Empty(new Vector3(2,.5f,2)); Empty(new Vector3(2,2.2f,1.2f)); }
                if (name == "Bed") Empty(new Vector3(4,.4f,2));
                if (name == "Sofa") Empty(new Vector3(4,.3f,1.8f));
                if (name == "Floor_lamp") Empty(new Vector3(.7f,20,0));
                PrefabUtility.SaveAsPrefabAsset(root,path);
                report.Add("PASS " + name + ": " + solid.Length + " BoxColliders; empty-space checks passed.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines("Documentation/FurnitureColliderRefinement.txt",report);
        Debug.Log("FURNITURE_REFINEMENT_PASSED");
    }
    public static void Inspect()
    {
        var lines = new List<string>();
        foreach (var name in Names)
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Furniture/" + name + ".prefab");
            try
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.sharedMesh) continue;
                    lines.Add($"{name}/{filter.name} scale={filter.transform.lossyScale:F4} overall={filter.sharedMesh.bounds}");
                    int index = 0;
                    foreach (var bounds in Islands(filter.sharedMesh)) lines.Add($"  {index++} center={bounds.center:F4} size={bounds.size:F4}");
                    if (name == "Table" || filter.name == "Almohada" || filter.name == "colchon") continue;
                    var mesh = filter.sharedMesh; var vs = mesh.vertices; var ts = mesh.triangles;
                    var planes = new Dictionary<string, Bounds>();
                    for (int t = 0; t < ts.Length; t += 3)
                    {
                        var a = vs[ts[t]]; var b = vs[ts[t+1]]; var c = vs[ts[t+2]];
                        var n = Vector3.Cross(b-a,c-a).normalized;
                        for (int axis = 0; axis < 3; axis++)
                        {
                            if (Mathf.Abs(n[axis]) < .9999f) continue;
                            string key = axis + ":" + Mathf.Sign(n[axis]) + ":" + Mathf.Round(a[axis]*10000)/10000;
                            if (!planes.TryGetValue(key, out var bounds)) bounds = new Bounds(a,Vector3.zero);
                            bounds.Encapsulate(a); bounds.Encapsulate(b); bounds.Encapsulate(c); planes[key] = bounds;
                        }
                    }
                    foreach (var plane in planes.OrderBy(p => p.Key))
                        if (plane.Value.size.magnitude > .1f) lines.Add($"  PLANE {plane.Key} center={plane.Value.center:F4} size={plane.Value.size:F4}");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        File.WriteAllLines("Documentation/FurnitureColliderGeometry.txt", lines);
    }
    static List<Bounds> Islands(Mesh mesh)
    {
        var vertices = mesh.vertices; var triangles = mesh.triangles;
        var parents = Enumerable.Range(0, vertices.Length).ToArray();
        int Find(int n) { while (parents[n] != n) { parents[n] = parents[parents[n]]; n = parents[n]; } return n; }
        void Join(int a, int b) { parents[Find(a)] = Find(b); }
        float precision = Mathf.Max(mesh.bounds.size.magnitude * .000001f, .000001f);
        var welded = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i] / precision;
            var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            if (welded.TryGetValue(key, out int other)) Join(i, other); else welded[key] = i;
        }
        for (int i = 0; i < triangles.Length; i += 3) { Join(triangles[i], triangles[i + 1]); Join(triangles[i], triangles[i + 2]); }
        var groups = new Dictionary<int, Bounds>();
        foreach (int i in triangles.Distinct())
        {
            int group = Find(i);
            if (!groups.TryGetValue(group, out var bounds)) bounds = new Bounds(vertices[i], Vector3.zero);
            bounds.Encapsulate(vertices[i]); groups[group] = bounds;
        }
        return groups.Values.OrderByDescending(b => b.size.x * b.size.y * b.size.z).ToList();
    }
}
