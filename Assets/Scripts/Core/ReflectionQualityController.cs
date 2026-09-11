using JuegoCriminal.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JuegoCriminal.Core
{
    /// <summary>
    /// One budgeted local environment capture. Authored room probes take priority.
    /// Created by VideoSettings; never modifies scene assets or material settings.
    /// </summary>
    public sealed class ReflectionQualityController : MonoBehaviour
    {
        [SerializeField, Min(8f)] private float influenceSize = 64f;
        [SerializeField, Min(1f)] private float captureDistance = 96f;
        [SerializeField] private Vector3 captureOffset = new Vector3(0f, 1.8f, 0f);
        [SerializeField] private LayerMask reflectedLayers = ~(1 << 5); // Exclude UI.

        private LocalPlayerMarker player;
        private ReflectionProbe probe;
        private int renderId = -1;
        private int appliedQuality = -1;
        private float nextCapture;
        private float nextPlayerSearch;

        public static int ResolutionFor(int quality) => quality <= 0 ? 0 : quality == 1 ? 128 : quality == 2 ? 256 : 512;
        public static float IntervalFor(int quality) => quality <= 0 ? float.PositiveInfinity : quality == 1 ? 5f : quality == 2 ? 2f : 0.5f;

        private void OnEnable()
        {
            VideoSettings.Changed += SettingsChanged;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void OnDisable()
        {
            VideoSettings.Changed -= SettingsChanged;
            SceneManager.sceneLoaded -= SceneLoaded;
            ReleaseProbe();
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ReleaseProbe();
            player = null;
            nextPlayerSearch = 0f;
        }

        private void SettingsChanged()
        {
            if (VideoSettings.Reflections == 0) ReleaseProbe();
            nextCapture = 0f;
        }

        private void Update()
        {
            if (VideoSettings.Reflections == 0 || !SystemInfo.supportsRenderToCubemap) return;
            if (player == null || !player.isActiveAndEnabled)
            {
                ReleaseProbe();
                if (Time.unscaledTime < nextPlayerSearch) return;
                nextPlayerSearch = Time.unscaledTime + 1f;
                player = FindFirstObjectByType<LocalPlayerMarker>();
                if (player == null || !player.isActiveAndEnabled) return;
            }

            // Do not recapture a paused game. Wait for the whole cubemap before
            // moving the origin, changing resolution or submitting more GPU work.
            if (Time.timeScale <= 0f) return;
            if (probe != null && renderId >= 0)
            {
                if (!probe.IsFinishedRendering(renderId)) return;
                renderId = -1;
            }
            int quality = VideoSettings.Reflections;
            if (probe == null)
            {
                var go = new GameObject("LocalEnvironmentProbe");
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.size = Vector3.one * influenceSize;
                probe.blendDistance = Mathf.Min(8f, influenceSize * 0.25f);
                probe.importance = -100; // Prefer deliberately placed room probes.
                probe.boxProjection = false; // Moving volume is not a room boundary.
                probe.nearClipPlane = 0.3f;
                probe.farClipPlane = captureDistance;
                probe.cullingMask = reflectedLayers;
                probe.clearFlags = ReflectionProbeClearFlags.Skybox;
                probe.hdr = true;
                probe.resolution = ResolutionFor(quality);
                go.SetActive(true);
            }
            if (appliedQuality != quality)
            {
                probe.resolution = ResolutionFor(quality);
                appliedQuality = quality;
                nextCapture = 0f;
            }
            if (Time.unscaledTime < nextCapture) return;
            probe.transform.SetPositionAndRotation(player.transform.position + captureOffset, Quaternion.identity);
            renderId = probe.RenderProbe();
            nextCapture = Time.unscaledTime + IntervalFor(quality);
        }

        private void ReleaseProbe()
        {
            if (probe != null)
            {
                probe.enabled = false;
                Destroy(probe.gameObject);
                probe = null;
            }
            renderId = -1;
            appliedQuality = -1;
            nextCapture = 0f;
        }
    }
}
