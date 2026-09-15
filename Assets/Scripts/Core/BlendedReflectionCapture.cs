using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace JuegoCriminal.Core
{
    // Capture and display are separate: incomplete cubemap faces are never shown.
    internal sealed class BlendedReflectionCapture : IDisposable
    {
        private static BlendedReflectionCapture captureOwner;
        private readonly ReflectionProbe capture;
        private readonly ReflectionProbe display;
        private readonly RenderTexture previous;
        private readonly RenderTexture next;
        public RenderTexture Output { get; }
        public bool Ready { get; private set; }
        private int renderId = -1;
        private float nextCapture;
        private float blendElapsed;
        private bool blending;
        private bool failed;

        public BlendedReflectionCapture(Transform parent, string name, Vector3 position,
            int resolution, int mask, float farClip, ReflectionZone zone, Texture seed)
        {
            previous = NewCube(resolution, name + " Previous");
            next = NewCube(resolution, name + " Next");
            Output = NewCube(resolution, name + " Blended");
            var go = new GameObject(name + " Capture (runtime)");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            capture = go.AddComponent<ReflectionProbe>();
            capture.mode = ReflectionProbeMode.Realtime;
            capture.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            capture.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            capture.resolution = resolution;
            capture.hdr = true;
            capture.cullingMask = mask;
            capture.nearClipPlane = 0.1f;
            capture.farClipPlane = farClip;
            capture.clearFlags = ReflectionProbeClearFlags.Skybox;
            capture.size = Vector3.one;
            // Move only the influence volume away; retain the real capture origin.
            capture.center = new Vector3(0f, -100000f, 0f);
            go.SetActive(true);

            if (zone != null)
            {
                var visual = new GameObject(name + " Reflection (runtime)");
                visual.SetActive(false);
                visual.transform.SetParent(parent, false);
                visual.transform.SetPositionAndRotation(position, Quaternion.identity);
                display = visual.AddComponent<ReflectionProbe>();
                display.mode = ReflectionProbeMode.Custom;
                display.size = zone.Size;
                display.center = zone.transform.position - position;
                display.blendDistance = zone.BlendDistance;
                display.boxProjection = zone.BoxProjection;
                display.importance = zone.Importance;
                display.customBakedTexture = Output;
                visual.SetActive(true);
                display.enabled = false;
            }
            if (seed != null && seed.dimension == TextureDimension.Cube && seed.width == resolution)
            {
                Ready = ReflectionProbe.BlendCubemap(seed, seed, 0f, Output);
                if (display != null) display.enabled = Ready;
            }
        }

        public void Tick(float interval, float duration)
        {
            if (failed) return;
            if (renderId >= 0)
            {
                if (!capture.IsFinishedRendering(renderId)) return;
                renderId = -1;
                captureOwner = null;
                if (!Ready)
                {
                    Graphics.CopyTexture(next, Output);
                    Ready = true;
                    if (display != null) display.enabled = true;
                }
                else
                {
                    Graphics.CopyTexture(Output, previous);
                    blending = true;
                    blendElapsed = 0f;
                }
                nextCapture = Time.time + interval;
            }
            if (blending)
            {
                blendElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(blendElapsed / Mathf.Max(0.2f, duration));
                if (!ReflectionProbe.BlendCubemap(previous, next, Mathf.SmoothStep(0f, 1f, t), Output))
                {
                    failed = true;
                    Debug.LogWarning("Reflection crossfade unsupported; keeping the last capture.", capture);
                    return;
                }
                blending = t < 1f;
                Output.IncrementUpdateCount();
            }
            if (!blending && captureOwner == null && Time.time >= nextCapture)
            {
                captureOwner = this;
                renderId = capture.RenderProbe(next);
                if (renderId < 0)
                {
                    captureOwner = null;
                    nextCapture = Time.time + interval;
                }
            }
        }

        private static RenderTexture NewCube(int resolution, string name)
        {
            var texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBHalf)
            {
                name = name, dimension = TextureDimension.Cube,
                useMipMap = true, autoGenerateMips = false, hideFlags = HideFlags.DontSave
            };
            texture.Create();
            return texture;
        }

        public void Dispose()
        {
            if (captureOwner == this) captureOwner = null;
            if (display != null) { display.enabled = false; UnityEngine.Object.Destroy(display.gameObject); }
            if (capture != null) { capture.enabled = false; UnityEngine.Object.Destroy(capture.gameObject); }
            // Destroy at frame end; do not synchronously release active render targets.
            UnityEngine.Object.Destroy(previous);
            UnityEngine.Object.Destroy(next);
            UnityEngine.Object.Destroy(Output);
        }
    }
}
