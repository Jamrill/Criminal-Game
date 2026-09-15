using System.Collections.Generic;
using JuegoCriminal.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JuegoCriminal.Core
{
    // Coordinates fixed scene zones and the global sky. No player-following probe.
    public sealed class ReflectionQualityController : MonoBehaviour
    {
        private readonly Dictionary<ReflectionZone, BlendedReflectionCapture> zones = new();
        private readonly List<ReflectionZone> removed = new();
        private BlendedReflectionCapture sky;
        private Texture previousReflection;
        private DefaultReflectionMode previousMode;
        private int ownerScene;
        private int quality = -1;
        private float nextScan;

        public static int ResolutionFor(int quality) => quality <= 0 ? 0 : quality == 1 ? 128 : quality == 2 ? 256 : 512;
        public static float IntervalFor(int quality) => quality <= 0 ? float.PositiveInfinity : quality == 1 ? 5f : quality == 2 ? 2f : 0.5f;

        private void OnEnable() => SceneManager.activeSceneChanged += ActiveSceneChanged;
        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            Release();
        }
        private void ActiveSceneChanged(Scene previous, Scene current) => Release();

        private void LateUpdate()
        {
            if (quality != VideoSettings.Reflections)
            {
                Release();
                quality = VideoSettings.Reflections;
            }
            if (quality <= 0 || Time.timeScale <= 0f) return;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 1f;
                if (sky == null && FindFirstObjectByType<DayNightCycle>() != null)
                {
                    previousReflection = RenderSettings.customReflectionTexture;
                    previousMode = RenderSettings.defaultReflectionMode;
                    ownerScene = SceneManager.GetActiveScene().handle;
                    sky = new BlendedReflectionCapture(transform, "Global Sky", Vector3.zero,
                        128, 0, 10f, null, previousReflection);
                }
                foreach (ReflectionZone zone in FindObjectsByType<ReflectionZone>(FindObjectsSortMode.None))
                {
                    if (!zone.isActiveAndEnabled || zones.ContainsKey(zone)) continue;
                    zones.Add(zone, new BlendedReflectionCapture(zone.transform, zone.name,
                        zone.CapturePosition, Mathf.Min(ResolutionFor(quality), zone.MaxResolution),
                        zone.ReflectedLayers, zone.CaptureDistance, zone,
                        sky != null && sky.Ready ? sky.Output : RenderSettings.customReflectionTexture));
                }
            }
            if (sky != null)
            {
                sky.Tick(Mathf.Min(2f, IntervalFor(quality)), 2f);
                if (sky.Ready)
                {
                    RenderSettings.customReflectionTexture = sky.Output;
                    RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                }
            }
            removed.Clear();
            foreach (var entry in zones)
            {
                if (entry.Key == null || !entry.Key.isActiveAndEnabled)
                {
                    entry.Value.Dispose();
                    removed.Add(entry.Key);
                    continue;
                }
                entry.Value.Tick(Mathf.Max(IntervalFor(quality), entry.Key.RefreshInterval), entry.Key.TransitionSeconds);
            }
            foreach (ReflectionZone zone in removed) zones.Remove(zone);
        }

        private void Release()
        {
            if (sky != null && RenderSettings.customReflectionTexture == sky.Output)
            {
                bool sameScene = ownerScene == SceneManager.GetActiveScene().handle;
                RenderSettings.customReflectionTexture = sameScene ? previousReflection : null;
                RenderSettings.defaultReflectionMode = sameScene ? previousMode : DefaultReflectionMode.Skybox;
            }
            sky?.Dispose();
            sky = null;
            foreach (var capture in zones.Values) capture.Dispose();
            zones.Clear();
            nextScan = 0f;
        }
    }
}
