using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JuegoCriminal.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Manual, Play-only A/B tests. Never saves scenes, prefabs or pipeline assets.
public sealed class LightLeakDiagnostics : EditorWindow
{
    readonly List<Action> restoreTest = new();
    readonly List<Action> restoreSession = new();
    Light sun;
    Camera cameraUnderTest;
    bool running;
    string test = "Baseline";
    string folder;
    Vector2 scroll;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    int captureFrame;
    double captureDeadline;
    string capturePrefix;
    bool captureRequested;

    [MenuItem("Tools/Criminal Game/Diagnostico Light Leaks")]
    static void Open() => GetWindow<LightLeakDiagnostics>("Light Leaks A-B");

    void OnEnable()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.playModeStateChanged += PlayState;
        RenderPipelineManager.beginCameraRendering += HoldCamera;
        EditorApplication.update += CaptureWhenReady;
    }

    void OnDisable()
    {
        Stop();
        AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        EditorApplication.playModeStateChanged -= PlayState;
        RenderPipelineManager.beginCameraRendering -= HoldCamera;
        EditorApplication.update -= CaptureWhenReady;
    }

    void PlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Stop();
    }

    void HoldCamera(ScriptableRenderContext context, Camera cam)
    {
        if (running && cam == cameraUnderTest)
            cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("En Play, situate donde se vea el defecto. Espera a que los reflejos se estabilicen. Cada prueba vuelve primero a la misma base. No guardes assets durante la sesion. Cierra esta ventana para restaurar todo.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || running))
            if (GUILayout.Button("1. Congelar y registrar estado real")) StartSession();
        if (capturePrefix != null) EditorGUILayout.HelpBox("Guardando prueba automaticamente. Mantener Game View visible; no pausar el Editor.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!running || capturePrefix != null))
        {
            GUILayout.Label("Prueba: " + test);
            if (GUILayout.Button("Base original")) Test("Baseline", () => { });
            if (GUILayout.Button("A - Solo sol sin intensidad")) Test("A_SunOff", () =>
            {
                float intensity = sun.intensity;
                restoreTest.Add(() => { if (sun) sun.intensity = intensity; });
                sun.intensity = 0;
            });
            if (GUILayout.Button("B - Solo ambiente negro")) Test("B_AmbientOff", BlackAmbient);
            if (GUILayout.Button("C1 - Solo reflejos desactivados")) Test("C1_ReflectionsOff", ReflectionsOff);
            if (GUILayout.Button("G - Todas las luces + ambiente + reflejos apagados")) Test("G_AllLightingOff", () =>
            {
                BlackAmbient();
                ReflectionsOff();
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    bool enabled = light.enabled;
                    restoreTest.Add(() => { if (light) light.enabled = enabled; });
                    light.enabled = false;
                }
                foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    var probes = r.lightProbeUsage;
                    int baked = r.lightmapIndex, realtime = r.realtimeLightmapIndex;
                    var bakedST = r.lightmapScaleOffset;
                    var realtimeST = r.realtimeLightmapScaleOffset;
                    restoreTest.Add(() =>
                    {
                        if (!r) return;
                        r.lightProbeUsage = probes;
                        r.lightmapIndex = baked;
                        r.realtimeLightmapIndex = realtime;
                        r.lightmapScaleOffset = bakedST;
                        r.realtimeLightmapScaleOffset = realtimeST;
                    });
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.lightmapIndex = -1;
                    r.realtimeLightmapIndex = -1;
                }
                bool fog = RenderSettings.fog;
                restoreTest.Add(() => RenderSettings.fog = fog);
                RenderSettings.fog = false;
                var data = cameraUnderTest.GetComponent<UniversalAdditionalCameraData>();
                if (data)
                {
                    bool post = data.renderPostProcessing;
                    restoreTest.Add(() => { if (data) data.renderPostProcessing = post; });
                    data.renderPostProcessing = false;
                }
                // Keep emissive/unlit surfaces and the sky visible to detect them through gaps.
            });
            if (GUILayout.Button("C2 - Ambiente, lightmaps y probes difusos desactivados")) Test("C2_DiffuseIndirectOff", () =>
            {
                BlackAmbient();
                foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    var usage = r.lightProbeUsage;
                    int bakedIndex = r.lightmapIndex, realtimeIndex = r.realtimeLightmapIndex;
                    var bakedST = r.lightmapScaleOffset;
                    var realtimeST = r.realtimeLightmapScaleOffset;
                    restoreTest.Add(() =>
                    {
                        if (!r) return;
                        r.lightProbeUsage = usage;
                        r.lightmapIndex = bakedIndex;
                        r.realtimeLightmapIndex = realtimeIndex;
                        r.lightmapScaleOffset = bakedST;
                        r.realtimeLightmapScaleOffset = realtimeST;
                    });
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.lightmapIndex = -1;
                    r.realtimeLightmapIndex = -1;
                }
            });
            if (GUILayout.Button("D1 - Solo postprocesado desactivado")) Test("D1_PostOff", () =>
            {
                var data = cameraUnderTest.GetComponent<UniversalAdditionalCameraData>();
                if (!data) throw new InvalidOperationException("La camara no tiene datos URP.");
                bool post = data.renderPostProcessing;
                restoreTest.Add(() => { if (data) data.renderPostProcessing = post; });
                data.renderPostProcessing = false;
            });
            if (GUILayout.Button("D2 - Solo SSAO desactivado")) Test("D2_SSAOOff", () =>
            {
                var pipeline = GraphicsSettings.currentRenderPipeline;
                if (!pipeline) throw new InvalidOperationException("Sin pipeline activo.");
                var list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
                int count = 0;
                if (list != null)
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        var data = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                        if (!data) continue;
                        foreach (var feature in data.rendererFeatures)
                        {
                            if (!feature || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                            bool active = feature.isActive;
                            restoreTest.Add(() => { if (feature) feature.SetActive(active); });
                            feature.SetActive(false); // In-memory only, restored on test change/exit/reload.
                            count++;
                        }
                    }
                if (count == 0) throw new InvalidOperationException("No se encontro SSAO en el pipeline activo.");
            });
            if (GUILayout.Button("E - TwoSided en meshes de la escena")) Test("E_TwoSided", () =>
            {
                foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r.gameObject.scene != SceneManager.GetActiveScene()) continue;
                    var mode = r.shadowCastingMode;
                    restoreTest.Add(() => { if (r) r.shadowCastingMode = mode; });
                    if (mode != ShadowCastingMode.Off && mode != ShadowCastingMode.ShadowsOnly)
                        r.shadowCastingMode = ShadowCastingMode.TwoSided;
                }
            });
            EditorGUILayout.HelpBox("Para probar el bloque, selecciona una pared/techo en Hierarchy. Se crea un cubo grueso temporal hacia el sol; ajusta su posicion en Scene para cubrir solo la union afectada. No es una correccion.", MessageType.None);
            if (GUILayout.Button("F - Bloque opaco temporal junto a seleccion")) Test("F_Blocker", () =>
            {
                var target = Selection.activeGameObject;
                var renderer = target ? target.GetComponentInChildren<MeshRenderer>() : null;
                if (!renderer) throw new InvalidOperationException("Selecciona una pieza con MeshRenderer.");
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "DIAGNOSTICO - bloque temporal";
                cube.hideFlags = HideFlags.DontSave;
                restoreTest.Add(() => { if (cube) Object.DestroyImmediate(cube); });
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.hideFlags = HideFlags.HideAndDontSave;
                restoreTest.Add(() => { if (material) Object.DestroyImmediate(material); });
                cube.GetComponent<Renderer>().sharedMaterial = material;
                cube.transform.position = renderer.bounds.center - sun.transform.forward * (renderer.bounds.extents.magnitude + 1);
                cube.transform.localScale = Vector3.one * 3;
                Selection.activeGameObject = cube;
            });
            if (GUILayout.Button("Guardar captura del Game View y estado actual"))
                ScheduleCapture();
            if (GUILayout.Button("Restaurar y terminar")) Stop();
        }
        EditorGUILayout.EndScrollView();
    }

    void StartSession()
    {
        try
        {
            var cycles = Object.FindObjectsByType<DayNightCycle>(FindObjectsSortMode.None);
            if (cycles.Length != 1) throw new InvalidOperationException("Se requiere exactamente un DayNightCycle activo para esta prueba.");
            var cycle = cycles[0];
            sun = new SerializedObject(cycle).FindProperty("sun").objectReferenceValue as Light;
            cameraUnderTest = Camera.main;
            if (!sun || !sun.isActiveAndEnabled || sun.intensity <= 0 || !cameraUnderTest)
                throw new InvalidOperationException("Se necesita sol encendido y Camera.main; iniciar en una hora diurna.");
            folder = Path.GetFullPath("Documentation/LightLeakDiagnostics/" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff"));
            Directory.CreateDirectory(folder);
            test = "Baseline";
            File.WriteAllText(Path.Combine(folder, "initial.txt"), Describe());
            bool enabled = cycle.enabled;
            restoreSession.Add(() => { if (cycle) cycle.enabled = enabled; });
            cycle.enabled = false;
            float scale = Time.timeScale;
            restoreSession.Add(() => Time.timeScale = scale);
            Time.timeScale = 0; // Also freezes ReflectionQualityController without destroying its cubemaps.
            cameraPosition = cameraUnderTest.transform.position;
            cameraRotation = cameraUnderTest.transform.rotation;
            running = true;
            test = "Baseline";
            ScheduleCapture();
        }
        catch (Exception ex) { Stop(); Debug.LogException(ex); }
    }

    void BlackAmbient()
    {
        var mode = RenderSettings.ambientMode;
        Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor;
        Color ground = RenderSettings.ambientGroundColor, flat = RenderSettings.ambientLight;
        float intensity = RenderSettings.ambientIntensity;
        var probe = RenderSettings.ambientProbe;
        restoreTest.Add(() =>
        {
            RenderSettings.ambientMode = mode;
            RenderSettings.ambientLight = flat;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientIntensity = intensity;
            RenderSettings.ambientProbe = probe;
        });
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0;
        RenderSettings.ambientProbe = new SphericalHarmonicsL2();
    }

    void ReflectionsOff()
    {
        float intensity = RenderSettings.reflectionIntensity;
        restoreTest.Add(() => RenderSettings.reflectionIntensity = intensity);
        RenderSettings.reflectionIntensity = 0;
        foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
        {
            bool enabled = probe.enabled;
            restoreTest.Add(() => { if (probe) probe.enabled = enabled; });
            probe.enabled = false;
        }
    }

    void Test(string label, Action apply)
    {
        Restore(restoreTest);
        try { apply(); test = label; ScheduleCapture(); }
        catch (Exception ex) { Restore(restoreTest); test = "Baseline"; Debug.LogException(ex); }
        SceneView.RepaintAll();
    }

    static void Restore(List<Action> actions)
    {
        for (int i = actions.Count - 1; i >= 0; i--)
            try { actions[i](); } catch (Exception ex) { Debug.LogException(ex); }
        actions.Clear();
    }

    void Stop()
    {
        capturePrefix = null;
        Restore(restoreTest);
        Restore(restoreSession);
        running = false;
    }

    void ScheduleCapture()
    {
        capturePrefix = Path.Combine(folder, DateTime.Now.ToString("HHmmssfff") + "_" + test);
        captureFrame = Time.frameCount + 5;
        captureDeadline = EditorApplication.timeSinceStartup + 20;
        captureRequested = false;
        File.AppendAllText(Path.Combine(folder, "events.txt"), $"{DateTime.Now:O} APPLY {test}\n");
    }

    void CaptureWhenReady()
    {
        if (!running || capturePrefix == null) return;
        if (EditorApplication.timeSinceStartup > captureDeadline)
        {
            Debug.LogWarning("No se completo la captura: mantener Game View visible y Editor sin Pause. Prueba: " + test);
            File.AppendAllText(Path.Combine(folder, "events.txt"), $"{DateTime.Now:O} CAPTURE_TIMEOUT {test}\n");
            capturePrefix = null;
            Repaint();
            return;
        }
        if (!captureRequested && Time.frameCount >= captureFrame)
        {
            File.WriteAllText(capturePrefix + ".txt", Describe());
            ScreenCapture.CaptureScreenshot(capturePrefix + ".png");
            captureRequested = true;
        }
        if (captureRequested && File.Exists(capturePrefix + ".png") && new FileInfo(capturePrefix + ".png").Length > 0)
        {
            File.AppendAllText(Path.Combine(folder, "events.txt"), $"{DateTime.Now:O} CAPTURE_SAVED {test}\n");
            Debug.Log("Prueba guardada: " + capturePrefix);
            capturePrefix = null;
            Repaint();
        }
    }

    string Describe()
    {
        var b = new StringBuilder();
        b.AppendLine($"Test={test}; frame={Time.frameCount}; scene={SceneManager.GetActiveScene().path}");
        b.AppendLine($"Camera={cameraUnderTest.name}; position={cameraUnderTest.transform.position}; rotation={cameraUnderTest.transform.eulerAngles}");
        b.AppendLine($"RenderSettings.sun={RenderSettings.sun}; ambient={RenderSettings.ambientMode}; intensity={RenderSettings.ambientIntensity}; sky={RenderSettings.ambientSkyColor}; equator={RenderSettings.ambientEquatorColor}; ground={RenderSettings.ambientGroundColor}; skybox={RenderSettings.skybox}; reflectionIntensity={RenderSettings.reflectionIntensity}; lightmaps={LightmapSettings.lightmaps.Length}");
        var pipeline = GraphicsSettings.currentRenderPipeline;
        b.AppendLine($"Pipeline={pipeline}; path={AssetDatabase.GetAssetPath(pipeline)}");
        if (pipeline) b.AppendLine(EditorJsonUtility.ToJson(pipeline, true));
        foreach (var cycle in Object.FindObjectsByType<DayNightCycle>(FindObjectsSortMode.None))
            b.AppendLine($"DayNight={cycle.name}; hour={cycle.CurrentHour}; enabled={cycle.enabled}");
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            var data = light.GetComponent<UniversalAdditionalLightData>();
            b.AppendLine($"Light={light.name}; id={light.GetInstanceID()}; selectedSun={light == sun}; type={light.type}; enabled={light.isActiveAndEnabled}; intensity={light.intensity}; shadows={light.shadows}; strength={light.shadowStrength}; bias={light.shadowBias}; normalBias={light.shadowNormalBias}; near={light.shadowNearPlane}; pipelineBias={(data ? data.usePipelineSettings.ToString() : "default")}");
        }
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (Vector3.Distance(r.bounds.ClosestPoint(cameraUnderTest.transform.position), cameraUnderTest.transform.position) > 30) continue;
            b.AppendLine($"Renderer={Hierarchy(r.transform)}; boundsMin={r.bounds.min}; boundsMax={r.bounds.max}; scale={r.transform.lossyScale}; determinant={r.localToWorldMatrix.determinant}; cast={r.shadowCastingMode}; receive={r.receiveShadows}; lightmap={r.lightmapIndex}; probes={r.lightProbeUsage}");
        }
        return b.ToString();
    }

    static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent) + "/" + t.name : t.name;
}
