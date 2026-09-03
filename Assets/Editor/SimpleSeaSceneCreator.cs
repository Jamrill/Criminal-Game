using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace JuegoCriminal.EditorTools
{
    public static class SimpleSeaSceneCreator
    {
        private const string Folder = "Assets/Materials/SimpleSea";
        private const string ShaderPath = Folder + "/SimpleSea.shader";
        private const string MaterialPath = Folder + "/M_SimpleSea.mat";
        private const string MeshPath = Folder + "/M_SimpleSeaGrid.asset";
        private const string ScenePath = "Assets/Scenes/20_SimpleSea.unity";

        [InitializeOnLoadMethod]
        private static void GenerateAfterReload()
        {
            EditorApplication.delayCall += EnsureSceneExists;
        }

        [MenuItem("Tools/Juego Criminal/Create Simple Sea Scene")]
        public static void CreateOrRebuildScene()
        {
            EnsureFolder();
            Material material = EnsureMaterial();
            Mesh mesh = EnsureGridMesh();
            if (material == null || mesh == null) return;

            Scene previousScene = SceneManager.GetActiveScene();
            Scene seaScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(seaScene);

            CreatePreviewMarker();
            CreateCamera();
            Light sun = CreateSun();
            Light moon = CreateMoon();
            TMP_Text clock = CreateClock();
            CreateDayNightCycle(sun, moon, clock);
            CreateSea(mesh, material);
            ConfigureEnvironment();

            EditorSceneManager.SaveScene(seaScene, ScenePath);
            if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(seaScene, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SimpleSea] Scene created at {ScenePath}");
        }

        private static void EnsureSceneExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                return;

            EnsureFolder();
            EnsureMaterial();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EnsureDayNightInExistingScene();
                return;
            }

            CreateOrRebuildScene();
        }

        private static void EnsureDayNightInExistingScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene previousScene = SceneManager.GetActiveScene();
            Scene seaScene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = seaScene.IsValid() && seaScene.isLoaded;
            if (!wasLoaded)
                seaScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            if (!seaScene.IsValid() || !seaScene.isLoaded)
                return;

            SceneManager.SetActiveScene(seaScene);
            if (FindInScene<JuegoCriminal.Environment.DayNightCycle>(seaScene) == null)
            {
                Light sun = FindLight(seaScene, "Sun") ?? CreateSun();
                Light moon = FindLight(seaScene, "Moon") ?? CreateMoon();
                TMP_Text clock = CreateClock();
                CreateDayNightCycle(sun, moon, clock);
                EditorSceneManager.SaveScene(seaScene);
                Debug.Log("[SimpleSea] Day/night cycle added to 20_SimpleSea.");
            }

            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);
            if (!wasLoaded)
                EditorSceneManager.CloseScene(seaScene, true);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }

        private static Light FindLight(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform current in transforms)
                {
                    if (current.name == objectName && current.TryGetComponent(out Light light))
                        return light;
                }
            }
            return null;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Materials", "SimpleSea");
        }

        private static Material EnsureMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError("[SimpleSea] SimpleSea.shader has not imported correctly.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_SimpleSea" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_DeepColor", new Color(0.015f, 0.10f, 0.18f, 1f));
            material.SetColor("_ShallowColor", new Color(0.04f, 0.48f, 0.52f, 1f));
            material.SetFloat("_WaveHeight", 0.35f);
            material.SetFloat("_WaveFrequency", 0.55f);
            material.SetFloat("_WaveSpeed", 1.1f);
            material.SetFloat("_WaveVariation", 1f);
            material.SetFloat("_VariationSpeed", 0.45f);
            material.SetFloat("_RippleStrength", 0.22f);
            material.SetFloat("_RippleScale", 2.4f);
            material.SetFloat("_RippleSpeed", 1.3f);
            material.SetFloat("_Smoothness", 0.72f);
            material.SetFloat("_Alpha", 0.92f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh EnsureGridMesh()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null) return existing;

            const int resolution = 100;
            const float size = 120f;
            int stride = resolution + 1;
            var vertices = new Vector3[stride * stride];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[resolution * resolution * 6];

            for (int z = 0; z <= resolution; z++)
            for (int x = 0; x <= resolution; x++)
            {
                int index = z * stride + x;
                float tx = x / (float)resolution;
                float tz = z / (float)resolution;
                vertices[index] = new Vector3((tx - 0.5f) * size, 0f, (tz - 0.5f) * size);
                uv[index] = new Vector2(tx, tz);
            }

            int triangle = 0;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                int i = z * stride + x;
                triangles[triangle++] = i;
                triangles[triangle++] = i + stride;
                triangles[triangle++] = i + 1;
                triangles[triangle++] = i + 1;
                triangles[triangle++] = i + stride;
                triangles[triangle++] = i + stride + 1;
            }

            var mesh = new Mesh { name = "M_SimpleSeaGrid" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        private static void CreateSea(Mesh mesh, Material material)
        {
            var sea = new GameObject("Simple Sea", typeof(MeshFilter), typeof(MeshRenderer));
            sea.GetComponent<MeshFilter>().sharedMesh = mesh;
            sea.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreatePreviewMarker()
        {
            var marker = new GameObject("Standalone Preview Scene");
            marker.AddComponent<JuegoCriminal.Core.StandalonePreviewScene>();
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 18f, -28f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 15f));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            camera.backgroundColor = new Color(0.42f, 0.68f, 0.82f);
        }

        private static Light CreateSun()
        {
            var sunObject = new GameObject("Sun", typeof(Light));
            sunObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light sun = sunObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.78f);
            sun.intensity = 1.1f;
            RenderSettings.sun = sun;
            return sun;
        }

        private static Light CreateMoon()
        {
            var moonObject = new GameObject("Moon", typeof(Light));
            moonObject.transform.rotation = Quaternion.Euler(225f, -35f, 0f);
            Light moon = moonObject.GetComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.38f, 0.48f, 0.78f);
            moon.intensity = 0.16f;
            return moon;
        }

        private static TMP_Text CreateClock()
        {
            var canvasObject = new GameObject("Day Night UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var textObject = new GameObject("Clock", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(35f, -30f);
            rect.sizeDelta = new Vector2(260f, 90f);

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = "05:30";
            label.fontSize = 48f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            return label;
        }

        private static void CreateDayNightCycle(Light sun, Light moon, TMP_Text clock)
        {
            var cycleObject = new GameObject("Day Night Cycle");
            var cycle = cycleObject.AddComponent<JuegoCriminal.Environment.DayNightCycle>();
            cycle.Configure(sun, moon, clock);
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.60f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.34f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.08f, 0.10f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.42f, 0.68f, 0.82f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 150f;
        }
    }
}
