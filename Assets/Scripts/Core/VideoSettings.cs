using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JuegoCriminal.Core
{
    /// <summary>Device preferences, independent of saved games.</summary>
    public static class VideoSettings
    {
        public enum QualityPreset { Low, Medium, High, Ultra, Custom }
        public static QualityPreset Preset { get; private set; } = QualityPreset.High;
        public static int Antialiasing { get; private set; } = 2;
        public static int MsaaSamples { get; private set; } = 4;
        public static int SmaaQuality { get; private set; } = 1;
        public static int Reflections { get; private set; } = 2;
        public static string ReflectionsLabel => new[] { "Bajo", "Medio", "Alto", "Ultra" }[Reflections];
        public static event Action Changed;
        public static string AntialiasingLabel => new[] { "Off", "FXAA", "SMAA", "TAA" }[Antialiasing];
        public static string PresetLabel => new[] { "Bajo", "Medio", "Alto", "Ultra", "Personalizado" }[(int)Preset];
        public static string SmaaQualityLabel => new[] { "Baja", "Media", "Alta" }[SmaaQuality];
        private static RenderPipelineAsset originalQualityPipeline;
        private static UniversalRenderPipelineAsset runtimePipeline;
        private static ReflectionQualityController reflectionController;
        private static bool originalRealtimeReflections;
        private static bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Cleanup();
            Changed = null;
            bool legacyPreference = !PlayerPrefs.HasKey("Video.Preset") && PlayerPrefs.HasKey("Video.Antialiasing");
            Preset = legacyPreference ? QualityPreset.Custom :
                (QualityPreset)Mathf.Clamp(PlayerPrefs.GetInt("Video.Preset", 2), 0, 4);
            if (Preset == QualityPreset.Custom)
            {
                Antialiasing = Mathf.Clamp(PlayerPrefs.GetInt("Video.Antialiasing", 2), 0, 3);
                int samples = PlayerPrefs.GetInt("Video.MSAA", legacyPreference ? 1 : 4);
                MsaaSamples = samples == 2 || samples == 4 || samples == 8 ? samples : 1;
                SmaaQuality = Mathf.Clamp(PlayerPrefs.GetInt("Video.SMAAQuality", legacyPreference ? 2 : 1), 0, 2);
                Reflections = Mathf.Clamp(PlayerPrefs.GetInt("Video.Reflections", 2), 0, 3);
                if (Antialiasing == 3) MsaaSamples = 1;
            }
            else SetPresetValues(Preset);

            originalRealtimeReflections = QualitySettings.realtimeReflectionProbes;
            initialized = true;
            QualitySettings.realtimeReflectionProbes = Reflections > 0;
            var reflectionObject = new GameObject("@RuntimeReflections");
            UnityEngine.Object.DontDestroyOnLoad(reflectionObject);
            reflectionController = reflectionObject.AddComponent<ReflectionQualityController>();

            // Use a runtime copy so Play mode cannot modify the source URP asset.
            originalQualityPipeline = QualitySettings.renderPipeline;
            var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (source != null)
            {
                runtimePipeline = UnityEngine.Object.Instantiate(source);
                runtimePipeline.name = source.name + " (Video Settings)";
                runtimePipeline.hideFlags = HideFlags.DontSave;
                runtimePipeline.msaaSampleCount = MsaaSamples;
                QualitySettings.renderPipeline = runtimePipeline;
            }
            RenderPipelineManager.beginCameraRendering += ConfigureCamera;
            Application.quitting += Cleanup;
        }

        private static void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= ConfigureCamera;
            Application.quitting -= Cleanup;
            if (reflectionController != null)
            {
                UnityEngine.Object.Destroy(reflectionController.gameObject);
                reflectionController = null;
            }
            if (initialized) QualitySettings.realtimeReflectionProbes = originalRealtimeReflections;
            initialized = false;
            if (runtimePipeline == null) return;
            if (QualitySettings.renderPipeline == runtimePipeline)
                QualitySettings.renderPipeline = originalQualityPipeline;
            UnityEngine.Object.Destroy(runtimePipeline);
            runtimePipeline = null;
        }

        private static void SetPresetValues(QualityPreset preset)
        {
            Antialiasing = (int)preset;
            MsaaSamples = preset == QualityPreset.Medium ? 2 : preset == QualityPreset.High ? 4 : 1;
            SmaaQuality = 1;
            Reflections = Mathf.Clamp((int)preset, 0, 3);
        }

        public static void CyclePreset()
        {
            SetPreset(((int)Preset + 1) % 5);
        }

        public static void SetPreset(int value)
        {
            Preset = (QualityPreset)Mathf.Clamp(value, 0, 4);
            if (Preset != QualityPreset.Custom) SetPresetValues(Preset);
            SaveAndApply();
        }

        public static void CycleAntialiasing()
        {
            SetAntialiasing((Antialiasing + 1) % 4);
        }

        public static void SetAntialiasing(int value)
        {
            Antialiasing = Mathf.Clamp(value, 0, 3);
            if (Antialiasing == 3) MsaaSamples = 1;
            Preset = QualityPreset.Custom;
            SaveAndApply();
        }

        public static void CycleMsaa()
        {
            SetMsaa(MsaaSamples == 8 ? 1 : MsaaSamples * 2);
        }

        public static void SetMsaa(int value)
        {
            if (Antialiasing == 3) return;
            MsaaSamples = value == 2 || value == 4 || value == 8 ? value : 1;
            Preset = QualityPreset.Custom;
            SaveAndApply();
        }

        public static void CycleSmaaQuality()
        {
            SetSmaaQuality((SmaaQuality + 1) % 3);
        }

        public static void SetSmaaQuality(int value)
        {
            if (Antialiasing != 2) return;
            SmaaQuality = Mathf.Clamp(value, 0, 2);
            Preset = QualityPreset.Custom;
            SaveAndApply();
        }

        public static void SetReflections(int value)
        {
            Reflections = Mathf.Clamp(value, 0, 3);
            Preset = QualityPreset.Custom;
            SaveAndApply();
        }

        private static void SaveAndApply()
        {
            QualitySettings.realtimeReflectionProbes = Reflections > 0;
            if (runtimePipeline != null) runtimePipeline.msaaSampleCount = MsaaSamples;
            PlayerPrefs.SetInt("Video.Preset", (int)Preset);
            PlayerPrefs.SetInt("Video.Antialiasing", Antialiasing);
            PlayerPrefs.SetInt("Video.MSAA", MsaaSamples);
            PlayerPrefs.SetInt("Video.SMAAQuality", SmaaQuality);
            PlayerPrefs.SetInt("Video.Reflections", Reflections);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void ConfigureCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Application.isPlaying || camera.cameraType != CameraType.Game || camera.targetTexture != null)
                return;
            var data = camera.GetUniversalAdditionalCameraData();
            data.antialiasing = Antialiasing == 0 ? AntialiasingMode.None :
                Antialiasing == 1 ? AntialiasingMode.FastApproximateAntialiasing :
                Antialiasing == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing :
                AntialiasingMode.TemporalAntiAliasing;
            data.antialiasingQuality = (AntialiasingQuality)SmaaQuality;
            camera.allowMSAA = MsaaSamples > 1;
            if (Antialiasing == 3) camera.allowDynamicResolution = false;
            if (Antialiasing != 0) data.renderPostProcessing = true;
        }
    }
}
