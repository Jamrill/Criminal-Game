#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JuegoCriminal.Vehicles.EditorTools
{
    /// <summary>Builds only the Velaro asset and prefab. Never opens or saves a user scene.</summary>
    [InitializeOnLoad]
    public static class VelaroAssetSetup
    {
        public const string Folder = "Assets/Vehicles/VelaroGT";
        public const string ModelPath = Folder + "/velaro_gt.fbx";
        public const string PrefabPath = Folder + "/Velaro_GT.prefab";
        private const string MaterialsFolder = Folder + "/Materials";
        private static bool scheduled;
        private static bool building;

        static VelaroAssetSetup() => ScheduleFirstBuild();

        internal static void ScheduleFirstBuild()
        {
            if (scheduled || building) return;
            scheduled = true;
            EditorApplication.delayCall += TryFirstBuild;
        }

        private static void TryFirstBuild()
        {
            scheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ScheduleFirstBuild();
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return;
            BuildPrefab();
        }

        [MenuItem("Tools/Velaro GT/Build prefab")]
        public static void BuildPrefab()
        {
            if (building) return;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (model == null || lit == null)
            {
                Debug.LogError("[Velaro GT] FBX or URP/Lit shader unavailable. Expected: " + ModelPath);
                return;
            }

            building = true;
            Scene preview = default;
            try
            {
                EnsureFolder(MaterialsFolder);
                Dictionary<string, Material> materials = BuildMaterials(lit);
                // A preview scene keeps hierarchy callbacks and undo history out of the user's scenes.
                preview = EditorSceneManager.NewPreviewScene();
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                instance.name = "Velaro_GT";
                // Preserve any FBX axis/unit conversion authored on the imported root.
                instance.transform.localPosition = Vector3.zero;
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                Transform doorL = FindRequired(instance.transform, "Door_L");
                Transform doorR = FindRequired(instance.transform, "Door_R");
                Transform hood = FindRequired(instance.transform, "Hood");
                Transform trunk = FindRequired(instance.transform, "Trunk");

                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] == null) continue;
                        string name = slots[i].name.Replace(" (Instance)", string.Empty);
                        if (materials.TryGetValue(name, out Material replacement)) slots[i] = replacement;
                        else Debug.LogWarning("[Velaro GT] Material name has no URP mapping: " + name);
                    }
                    renderer.sharedMaterials = slots;
                }

                VelaroOpenableParts parts = instance.AddComponent<VelaroOpenableParts>();
                parts.Configure(doorL, doorR, hood, trunk);
                // Defaults correspond to the configured FBX conversion. If export axes change,
                // inspect the hinge's local axes and adjust Open Euler in the prefab inspector.

                AddChassisCollider(instance);
                AddPanelCollider(doorL);
                AddPanelCollider(doorR);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Unity did not save the prefab.");
                AssetDatabase.SaveAssets();
                Debug.Log("[Velaro GT] Prefab ready: " + PrefabPath +
                    ". Four hinges and URP materials configured; no scene changed.");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                building = false;
            }
        }

        private static Transform FindRequired(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            throw new InvalidOperationException("Missing required hinge: " + name);
        }

        private static Dictionary<string, Material> BuildMaterials(Shader shader)
        {
            return new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                ["Paint_Metallic"] = Material(shader, "Paint_Metallic", new Color(0.035f, 0.22f, 0.37f), 0.82f, 0.68f),
                ["Leather_Tan"] = Material(shader, "Leather_Tan", new Color(0.48f, 0.25f, 0.12f), 0f, 0.27f),
                ["Trim_Dark"] = Material(shader, "Trim_Dark", new Color(0.028f, 0.033f, 0.04f), 0.08f, 0.32f),
                ["Alloy_Metal"] = Material(shader, "Alloy_Metal", new Color(0.55f, 0.61f, 0.66f), 0.9f, 0.72f),
                ["Glass_Tinted"] = Material(shader, "Glass_Tinted", new Color(0.14f, 0.22f, 0.27f, 0.24f), 0.05f, 0.94f, transparent: true),
                ["LED_White"] = Material(shader, "LED_White", new Color(0.8f, 0.94f, 1f), 0.05f, 0.6f, new Color(0.8f, 0.94f, 1f) * 2f),
                ["LED_Red"] = Material(shader, "LED_Red", new Color(0.55f, 0.008f, 0.015f), 0.05f, 0.65f, new Color(1f, 0.006f, 0.012f)),
                ["Tire_Rubber"] = Material(shader, "Tire_Rubber", new Color(0.015f, 0.018f, 0.021f), 0f, 0.2f),
                ["Display"] = Material(shader, "Display", new Color(0.013f, 0.05f, 0.07f), 0.08f, 0.7f, new Color(0.01f, 0.18f, 0.24f))
            };
        }

        private static Material Material(Shader shader, string name, Color color, float metallic,
            float smoothness, Color emission = default, bool transparent = false)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            // Preserve any material edits when the user explicitly rebuilds the prefab.
            if (material != null) return material;
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void AddChassisCollider(GameObject root)
        {
            if (!TryMeshBounds(root.transform, out Bounds bounds)) return;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            // Only the floor/underbody, so this broad collider does not obstruct the cabin or trunk.
            collider.center = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.2f, bounds.center.z);
            collider.size = new Vector3(bounds.size.x * 0.88f, bounds.size.y * 0.2f, bounds.size.z * 0.9f);
        }

        private static void AddPanelCollider(Transform pivot)
        {
            if (!TryMeshBounds(pivot, out Bounds bounds)) return;
            BoxCollider collider = pivot.gameObject.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = Vector3.Max(bounds.size, Vector3.one * 0.025f);
        }

        private static bool TryMeshBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds meshBounds = filter.sharedMesh.bounds;
                Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    Vector3 point = matrix.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents, sign));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return found;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }

    public sealed class VelaroModelPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!string.Equals(assetPath, VelaroAssetSetup.ModelPath, StringComparison.Ordinal)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.meshCompression = ModelImporterMeshCompression.Off;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!string.Equals(path, VelaroAssetSetup.ModelPath, StringComparison.Ordinal)) continue;
                VelaroAssetSetup.ScheduleFirstBuild();
                break;
            }
        }
    }
}
#endif
