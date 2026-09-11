using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JuegoCriminal.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>One explicitly requested migration. Uses Unity's importer IDs, never guessed YAML IDs.</summary>
[InitializeOnLoad]
public static class FixedFbxMigration
{
    private const string Request = "Documentation/FixedFbxMigration.request";
    private const string Report = "Documentation/FixedFbxMigration.report.txt";
    private const string Done = "Documentation/FixedFbxMigration.done";
    private const string Backup = "Documentation/FixedFbxBackup";
    private const string Originals = "Assets/Original_assets/";
    private static readonly string[] Models = {
        "Door.fbx", "Knob.fbx", "Walls/Wall_Complete.fbx", "Walls/Wall_door_hole_complete.fbx",
        "Walls/Corner_Wall_Complete.fbx", "Walls/Glass_wall_complete.fbx", "Walls/Diagonal_Wall_Complete.fbx"
    };
    private static readonly Dictionary<string, string> Walls = new Dictionary<string, string> {
        { "Assets/Prefabs/Construction/Incompletes/Wall.prefab", "Wall_Complete.fbx" },
        { "Assets/Prefabs/Construction/Incompletes/Wall_door_hole.prefab", "Wall_door_hole_complete.fbx" },
        { "Assets/Prefabs/Construction/Incompletes/Corner_Wall.prefab", "Corner_Wall_Complete.fbx" },
        { "Assets/Prefabs/Construction/Incompletes/Glass_wall_Incomplete.prefab", "Glass_wall_complete.fbx" },
        { "Assets/Prefabs/Construction/Glass_Wall.prefab", "Glass_wall_complete.fbx" },
        { "Assets/Prefabs/Construction/Diagonal_Wall.prefab", "Diagonal_Wall_Complete.fbx" }
    };
    private static readonly List<string> log = new List<string>();

    static FixedFbxMigration()
    {
        if (File.Exists(Request) && !File.Exists(Done) && !File.Exists(Report))
            EditorApplication.delayCall += TryRun;
    }

    private static string Fixed(string relative)
    {
        string directory = Path.GetDirectoryName(relative)?.Replace('\\', '/');
        return Originals + (string.IsNullOrEmpty(directory) ? "" : directory + "/") + "Fixed/" + Path.GetFileName(relative);
    }

    private static void TryRun()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }
        Run();
    }

    [MenuItem("Tools/Criminal Game/Aplicar FBX Fixed y puerta por codigo")]
    public static void Run()
    {
        if (File.Exists(Done)) { Debug.Log("Fixed FBX: migracion ya aplicada. Consultar " + Report); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogWarning("Fixed FBX: salir de Play y cerrar Prefab Mode antes de aplicar.");
            return;
        }
        log.Clear();
        try
        {
            // Validate all inputs before saving any prefab.
            foreach (string relative in Models)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Fixed(relative)) == null)
                    throw new InvalidOperationException("FBX no importado: " + Fixed(relative));
            foreach (string relative in Models) ConfigureImporter(relative);
            foreach (var entry in Walls) ReplaceWall(entry.Key, entry.Value);
            ReplaceDoor();
            AssetDatabase.SaveAssets();
            string[] retired = Models.Select(m => Originals + m).Concat(new[] {
                Originals + "Walls/Incompletes/Wall_Incomplete.fbx",
                Originals + "Walls/Incompletes/Wall_door_hole_incomplete.fbx",
                Originals + "Walls/Incompletes/Corner_Wall_Incomplete.fbx",
                Originals + "Walls/Incompletes/Glass_wall_Incomplete.fbx"
            }).ToArray();
            foreach (string prefab in Walls.Keys.Concat(new[] { "Assets/Prefabs/Door.prefab" }))
            {
                var remaining = AssetDatabase.GetDependencies(prefab, true).Intersect(retired).ToArray();
                if (remaining.Length != 0) throw new InvalidOperationException(prefab + " mantiene dependencias antiguas: " + string.Join(", ", remaining));
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                if (asset.GetComponentsInChildren<Animator>(true).Length != 0) throw new InvalidOperationException("Animator restante: " + prefab);
            }
            File.WriteAllText(Done, "Completed " + DateTime.Now.ToString("O"));
            log.Add("SUCCESS. Originales conservados. Prefabs verificados sin dependencias de los FBX sustituidos ni Animator.");
            Debug.Log("Fixed FBX: sustitucion completada. " + Report);
        }
        catch (Exception error)
        {
            log.Add("ERROR: " + error);
            log.Add("No borrar originales. Copias previas disponibles en " + Backup + ". No se reintentara automaticamente.");
            Debug.LogException(error);
        }
        finally { File.WriteAllLines(Report, log); }
    }

    private static void CopyBackup(string assetPath)
    {
        if (!File.Exists(assetPath)) return;
        string dest = Backup + "/" + assetPath;
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        if (!File.Exists(dest)) File.Copy(assetPath, dest);
        if (File.Exists(assetPath + ".meta") && !File.Exists(dest + ".meta")) File.Copy(assetPath + ".meta", dest + ".meta");
    }

    private static void ConfigureImporter(string relative)
    {
        string path = Fixed(relative);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        CopyBackup(path + ".meta");
        var old = (ModelImporter)AssetImporter.GetAtPath(Originals + relative);
        if (old != null)
            foreach (var remap in old.GetExternalObjectMap())
                if (remap.Value is Material) importer.AddRemap(remap.Key, remap.Value);
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.addCollider = false;
        importer.SaveAndReimport();
    }

    private static Bounds LocalBounds(GameObject root, IEnumerable<MeshFilter> filters)
    {
        bool found = false; Bounds bounds = default;
        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 matrix = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds b = mf.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x,y,z)));
                        if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                        else bounds.Encapsulate(p);
                    }
        }
        if (!found) throw new InvalidOperationException("No hay mallas en " + root.name);
        return bounds;
    }

    private static void Unpack(GameObject root)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(root))
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        // Added nested instances (the separate knob) can survive unpacking a variant.
        GameObject nested;
        while ((nested = root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)
            .FirstOrDefault(go => go != root && PrefabUtility.IsAnyPrefabInstanceRoot(go))) != null)
            PrefabUtility.UnpackPrefabInstance(nested, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    private static void ReplaceWall(string prefabPath, string filename)
    {
        bool existed = File.Exists(prefabPath);
        if (existed && AssetDatabase.GetDependencies(prefabPath, true).Contains(Fixed("Walls/" + filename)))
        {
            log.Add(prefabPath + ": ya usa Fixed; se conserva sin reconstruir.");
            return;
        }
        CopyBackup(prefabPath);
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject("Diagonal_Wall");
        try
        {
            Vector3 position = root.transform.localPosition, scale = root.transform.localScale;
            Quaternion rotation = root.transform.localRotation;
            var oldFilters = root.GetComponentsInChildren<MeshFilter>(true);
            Bounds oldBounds = existed ? LocalBounds(root, oldFilters) : default;
            var oldMaterials = root.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToArray();
            var material = root.GetComponentsInChildren<Collider>(true).Select(c => c.sharedMaterial).FirstOrDefault(m => m != null);
            Unpack(root);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var mf in oldFilters)
            {
                var renderer = mf.GetComponent<MeshRenderer>();
                if (renderer != null) Object.DestroyImmediate(renderer);
                Object.DestroyImmediate(mf);
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fixed("Walls/" + filename));
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "FixedVisual";
            var filters = visual.GetComponentsInChildren<MeshFilter>(true);
            Bounds newBounds = LocalBounds(root, filters);
            if (existed) visual.transform.localPosition += oldBounds.min - newBounds.min;
            else if (filename == "Diagonal_Wall_Complete.fbx") visual.transform.localPosition += new Vector3(0f, 0f, 0.25f);
            foreach (var mf in filters)
            {
                mf.gameObject.layer = root.layer;
                PreserveMaterials(mf.GetComponent<MeshRenderer>(), oldMaterials);
                var collider = mf.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mf.sharedMesh;
                collider.convex = false;
                collider.sharedMaterial = material;
            }
            if (root.transform.localPosition != position || root.transform.localRotation != rotation || root.transform.localScale != scale)
                throw new InvalidOperationException("Cambio de raiz: " + prefabPath);
            Bounds finalBounds = LocalBounds(root, filters);
            if (existed && Vector3.Distance(finalBounds.min, oldBounds.min) > 0.001f)
                throw new InvalidOperationException("Cambio de anclaje: " + prefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException("No se pudo guardar " + prefabPath);
            log.Add(prefabPath + " -> " + Fixed("Walls/" + filename) + "; bounds=" + finalBounds + "; root preservada=" + existed);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static void PreserveMaterials(MeshRenderer renderer, Material[] old)
    {
        if (renderer == null) return;
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            string name = materials[i] != null ? materials[i].name : "";
            // Never retain an embedded material that would keep a retired FBX alive.
            Material match = old.FirstOrDefault(m => m.name == name && AssetDatabase.GetAssetPath(m).EndsWith(".mat", StringComparison.OrdinalIgnoreCase));
            if (match != null) materials[i] = match;
        }
        renderer.sharedMaterials = materials;
    }

    private static void ReplaceDoor()
    {
        const string path = "Assets/Prefabs/Door.prefab";
        CopyBackup(path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Vector3 pos = root.transform.localPosition, scale = root.transform.localScale;
            Quaternion rot = root.transform.localRotation;
            Unpack(root);
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            Transform leaf = null;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                string oldPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (oldPath != Originals + "Door.fbx" && oldPath != Originals + "Knob.fbx" && oldPath != Fixed("Door.fbx") && oldPath != Fixed("Knob.fbx")) continue;
                string file = Path.GetFileName(oldPath);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fixed(file));
                var candidates = model.GetComponentsInChildren<MeshFilter>(true);
                MeshFilter source = candidates.FirstOrDefault(c => c.sharedMesh.name == mf.sharedMesh.name);
                if (source == null && candidates.Length == 1) source = candidates[0];
                if (source == null) throw new InvalidOperationException("No se encuentra malla nueva para " + mf.name + ": " + mf.sharedMesh.name);
                var renderer = mf.GetComponent<MeshRenderer>();
                Material[] materials = renderer != null ? renderer.sharedMaterials : Array.Empty<Material>();
                mf.sharedMesh = source.sharedMesh;
                if (renderer != null)
                {
                    renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                    PreserveMaterials(renderer, materials);
                }
                foreach (var c in mf.GetComponents<Collider>()) Object.DestroyImmediate(c);
                if (file == "Door.fbx" && (mf.name.IndexOf("Sheet", StringComparison.OrdinalIgnoreCase) >= 0 || mf.sharedMesh.name.IndexOf("Sheet", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    leaf = mf.transform;
                    var collider = mf.gameObject.AddComponent<BoxCollider>();
                    collider.center = mf.sharedMesh.bounds.center;
                    collider.size = mf.sharedMesh.bounds.size;
                }
                else if (file == "Door.fbx")
                {
                    var collider = mf.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mf.sharedMesh;
                }
            }
            if (leaf == null) throw new InvalidOperationException("No se encuentra Sheet / bisagra de Door");
            Transform knob = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "KnobPoint");
            if (knob == null || !knob.IsChildOf(leaf)) throw new InvalidOperationException("KnobPoint debe ser hijo de Sheet");
            var controller = root.GetComponent<DoorController>();
            if (controller == null) throw new InvalidOperationException("Falta DoorController (comprobar compilacion)");
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("doorPivot").objectReferenceValue = leaf;
            serialized.FindProperty("knobPivot").objectReferenceValue = knob;
            serialized.FindProperty("doorAxis").vector3Value = leaf.InverseTransformDirection(root.transform.up).normalized;
            serialized.FindProperty("knobAxis").vector3Value = knob.InverseTransformDirection(root.transform.forward).normalized;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (root.transform.localPosition != pos || root.transform.localRotation != rot || root.transform.localScale != scale)
                throw new InvalidOperationException("Cambio de raiz en Door");
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            if (!saved) throw new InvalidOperationException("No se pudo guardar Door");
            log.Add(path + " -> Fixed/Door + Fixed/Knob; sin Animator; bisagra=" + leaf.name + "; pomo=" + knob.name);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void ValidateFinal()
    {
        var results = new List<string>();
        foreach (string path in Walls.Keys.Concat(new[] { "Assets/Prefabs/Door.prefab" }))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponentsInChildren<Animator>(true).Length != 0) throw new Exception("Animator: " + path);
                if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c == null)) throw new Exception("Missing script: " + path);
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null && !AssetDatabase.GetAssetPath(filter.sharedMesh).Contains("/Fixed/"))
                        throw new Exception("Malla antigua: " + path + "/" + filter.name);
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                    if (collider.sharedMesh == null) throw new Exception("Collider sin malla: " + path);
                if (path.EndsWith("Door.prefab"))
                {
                    var controller = root.GetComponent<DoorController>();
                    var so = new SerializedObject(controller);
                    Transform leaf = (Transform)so.FindProperty("doorPivot").objectReferenceValue;
                    Transform knob = (Transform)so.FindProperty("knobPivot").objectReferenceValue;
                    if (leaf == null || knob == null || !knob.IsChildOf(leaf)) throw new Exception("Bisagras sin conectar");
                    var box = leaf.GetComponent<BoxCollider>();
                    var mesh = leaf.GetComponent<MeshFilter>().sharedMesh;
                    if (box == null || Vector3.Distance(box.size, mesh.bounds.size) > .0001f) throw new Exception("Collider de hoja desajustado");
                    const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    typeof(DoorController).GetMethod("Awake", flags).Invoke(controller, null);
                    Quaternion closed = leaf.localRotation;
                    Vector3 rootPosition = root.transform.position;
                    Vector3 knobPosition = knob.position;
                    typeof(DoorController).GetField("isOpen", flags).SetValue(controller, true);
                    typeof(DoorController).GetMethod("FinishMovement", flags).Invoke(controller, null);
                    if (Mathf.Abs(Quaternion.Angle(closed, leaf.localRotation) - 95f) > .01f) throw new Exception("Giro de puerta incorrecto");
                    if (Vector3.Distance(knobPosition, knob.position) < .01f) throw new Exception("El pomo no acompana la hoja");
                    if (root.transform.position != rootPosition) throw new Exception("Se mueve la raiz de Door");
                    typeof(DoorController).GetField("isOpen", flags).SetValue(controller, false);
                    typeof(DoorController).GetMethod("FinishMovement", flags).Invoke(controller, null);
                    if (Quaternion.Angle(closed, leaf.localRotation) > .01f) throw new Exception("No vuelve a cerrado");
                    results.Add("Door: abrir/cerrar 95 grados; raiz fija; pomo acompana; collider ajustado; sin Animator.");
                }
                else
                {
                    Bounds b = LocalBounds(root, root.GetComponentsInChildren<MeshFilter>(true));
                    if (Mathf.Abs(b.size.y - 8f) > .001f) throw new Exception("Altura distinta de 8: " + path);
                    results.Add(path + ": altura 8; bounds " + b + "; mallas y colliders Fixed.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        File.WriteAllLines("Documentation/FixedFbxMigration.validation.txt", results);
        Debug.Log("Fixed FBX: VALIDATION PASSED");
    }
}
