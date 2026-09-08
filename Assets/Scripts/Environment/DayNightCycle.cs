using TMPro;
using UnityEngine;

namespace JuegoCriminal.Environment
{
    public sealed class DayNightCycle : MonoBehaviour
    {
        [Header("Clock")]
        [SerializeField, Range(0f, 24f)] private float startHour = 5.5f;
        [SerializeField, Min(0.25f)] private float fullDayDurationMinutes = 4f;
        [SerializeField] private bool cycleEnabled = true;

        [Header("Scene references")]
        [SerializeField] private Light sun;
        [SerializeField] private Light moon;
        [SerializeField] private TMP_Text clockText;

        [Header("Lighting")]
        [SerializeField, Min(0f)] private float sunIntensity = 1.15f;
        [SerializeField, Min(0f)] private float moonIntensity = 0.16f;

        [Header("Flat sky (optional)")]
        [SerializeField] private Shader flatSkyShader;
        [SerializeField] private Color daySkyColor = new Color(0.30f, 0.62f, 0.86f);
        [SerializeField] private Color twilightSkyColor = new Color(0.95f, 0.31f, 0.10f);
        [SerializeField] private Color nightSkyColor = new Color(0.012f, 0.025f, 0.075f);
        [Header("Procedural clouds")]
        [SerializeField, Range(0f, 1f)] private float cloudCoverage = 0.45f;
        [SerializeField, Range(0f, 1f)] private float cloudOpacity = 0.85f;
        [SerializeField, Range(0.5f, 10f)] private float cloudScale = 3f;
        [Tooltip("Grados por segundo real, sin pausa. 0.1 = 1 vuelta/hora; 1 = 6 minutos; 10 = 36 segundos. Negativo invierte el sentido. Solo se anima en Play.")]
        [SerializeField] private float cloudRotationSpeed = 0.1f;
        [SerializeField] private Color dayCloudColor = new Color(0.92f, 0.95f, 1f);
        [SerializeField] private Color nightCloudColor = new Color(0.055f, 0.07f, 0.11f);
        [Header("Slow cloud variation")]
        [Tooltip("When enabled, the ranges below replace fixed coverage and rotation speed during Play.")]
        [SerializeField] private bool dynamicClouds;
        [SerializeField] private Vector2 cloudCoverageRange = new Vector2(0.25f, 0.65f);
        [Tooltip("Real seconds for a complete coverage oscillation, excluding pauses.")]
        [SerializeField, Min(1f)] private float cloudCoveragePeriodSeconds = 600f;
        [Tooltip("Minimum and maximum rotation speed, in degrees per second.")]
        [SerializeField] private Vector2 cloudSpeedRange = new Vector2(0.1f, 1f);
        [SerializeField, Min(1f)] private float cloudSpeedPeriodSeconds = 240f;
        private float _coveragePhase;
        private float _speedPhase;
        private float _currentCloudCoverage;
        private float _currentCloudSpeed;
        private float _cloudAngle;
        private Material _skyMaterial;
        private Material _previousSky;

        private float _currentHour;
        private Camera _mainCamera;

        public float CurrentHour => _currentHour;
        public float CurrentCloudAngleDegrees => _cloudAngle * Mathf.Rad2Deg;
        public float CurrentCloudCoverage => _currentCloudCoverage;
        public float CurrentCloudSpeed => _currentCloudSpeed;

        private void Awake()
        {
            _currentHour = Mathf.Repeat(startHour, 24f);
            _coveragePhase = InitialCloudPhase(cloudCoverage, CoverageLimits);
            _speedPhase = InitialCloudPhase(cloudRotationSpeed, SpeedLimits);
            UpdateCloudVariation(0f);
            if (flatSkyShader != null)
            {
                _previousSky = RenderSettings.skybox;
                _skyMaterial = new Material(flatSkyShader) { name = "Day Night Sky (runtime)" };
                RenderSettings.skybox = _skyMaterial;
                UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += PrepareCamera;
            }
            _mainCamera = Camera.main;
            ApplyLighting();
        }

        private void Update()
        {
            UpdateCloudVariation(Time.deltaTime);
            _cloudAngle = Mathf.Repeat(_cloudAngle + _currentCloudSpeed * Mathf.Deg2Rad * Time.deltaTime, Mathf.PI * 2f);
            if (cycleEnabled)
            {
                float hoursPerSecond = 24f / (Mathf.Max(0.25f, fullDayDurationMinutes) * 60f);
                _currentHour = Mathf.Repeat(_currentHour + Time.deltaTime * hoursPerSecond, 24f);
            }

            ApplyLighting();
        }

        public void Configure(Light sunLight, Light moonLight, TMP_Text timeLabel)
        {
            sun = sunLight;
            moon = moonLight;
            clockText = timeLabel;
        }

        private Vector2 CoverageLimits => new Vector2(
            Mathf.Clamp01(Mathf.Min(cloudCoverageRange.x, cloudCoverageRange.y)),
            Mathf.Clamp01(Mathf.Max(cloudCoverageRange.x, cloudCoverageRange.y)));

        private Vector2 SpeedLimits => new Vector2(
            Mathf.Min(cloudSpeedRange.x, cloudSpeedRange.y),
            Mathf.Max(cloudSpeedRange.x, cloudSpeedRange.y));

        private static float InitialCloudPhase(float value, Vector2 limits)
        {
            return Mathf.Asin(Mathf.InverseLerp(limits.x, limits.y, value) * 2f - 1f);
        }

        private void UpdateCloudVariation(float deltaTime)
        {
            if (!dynamicClouds)
            {
                _currentCloudCoverage = Mathf.Clamp01(cloudCoverage);
                _currentCloudSpeed = cloudRotationSpeed;
                return;
            }

            // Independent bounded phases keep long sessions precise. Integrating
            // speed into _cloudAngle avoids jumps when the wind changes speed.
            _coveragePhase = Mathf.Repeat(_coveragePhase + deltaTime * Mathf.PI * 2f /
                Mathf.Max(1f, cloudCoveragePeriodSeconds), Mathf.PI * 2f);
            _speedPhase = Mathf.Repeat(_speedPhase + deltaTime * Mathf.PI * 2f /
                Mathf.Max(1f, cloudSpeedPeriodSeconds), Mathf.PI * 2f);
            Vector2 coverage = CoverageLimits;
            Vector2 speed = SpeedLimits;
            _currentCloudCoverage = Mathf.Lerp(coverage.x, coverage.y, (Mathf.Sin(_coveragePhase) + 1f) * 0.5f);
            _currentCloudSpeed = Mathf.Lerp(speed.x, speed.y, (Mathf.Sin(_speedPhase) + 1f) * 0.5f);
        }

        private void ApplyLighting()
        {
            float orbitAngle = _currentHour / 24f * 360f - 90f;
            if (sun != null)
                sun.transform.rotation = Quaternion.Euler(orbitAngle, -35f, 0f);
            if (moon != null)
                moon.transform.rotation = Quaternion.Euler(orbitAngle + 180f, -35f, 0f);

            float sunHeight = Mathf.Sin((_currentHour - 6f) / 12f * Mathf.PI);
            float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.22f, sunHeight));
            float night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.25f, 0.05f, sunHeight));
            float horizon = Mathf.Clamp01(1f - Mathf.Abs(sunHeight) * 4f) * (1f - night * 0.65f);

            Color nightSky = nightSkyColor;
            Color daySky = daySkyColor;
            Color dawnSky = twilightSkyColor;
            Color skyColor = Color.Lerp(nightSky, daySky, daylight);
            skyColor = Color.Lerp(skyColor, dawnSky, horizon * 0.72f);

            if (sun != null)
            {
                sun.intensity = Mathf.Max(0f, sunHeight) * sunIntensity;
                sun.color = Color.Lerp(new Color(1f, 0.36f, 0.12f), new Color(1f, 0.95f, 0.82f), daylight);
                sun.enabled = sun.intensity > 0.001f;
            }

            if (moon != null)
            {
                moon.intensity = night * moonIntensity;
                moon.enabled = moon.intensity > 0.001f;
            }

            if (_skyMaterial != null)
            {
                _skyMaterial.SetColor("_SkyColor", skyColor);
                if (sun != null) _skyMaterial.SetVector("_SunDirection", -sun.transform.forward);
                if (moon != null) _skyMaterial.SetVector("_MoonDirection", -moon.transform.forward);
                _skyMaterial.SetFloat("_SunVisibility", Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.08f, sunHeight)));
                _skyMaterial.SetFloat("_MoonVisibility", night);
                _skyMaterial.SetFloat("_CloudAngle", _cloudAngle);
                _skyMaterial.SetFloat("_CloudCoverage", _currentCloudCoverage);
                _skyMaterial.SetFloat("_CloudOpacity", cloudOpacity);
                _skyMaterial.SetFloat("_CloudScale", cloudScale);
                Color cloudColor = Color.Lerp(nightCloudColor, dayCloudColor, daylight);
                cloudColor = Color.Lerp(cloudColor, twilightSkyColor * 0.8f, horizon * 0.4f);
                _skyMaterial.SetColor("_CloudColor", cloudColor);
                RenderSettings.sun = sun != null && sun.enabled ? sun : moon;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.018f, 0.025f, 0.08f), skyColor * 0.72f, daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.012f, 0.018f, 0.045f), skyColor * 0.42f, daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.006f, 0.009f, 0.018f), new Color(0.045f, 0.075f, 0.085f), daylight);
            RenderSettings.fogColor = Color.Lerp(nightSky, skyColor, daylight * 0.85f + horizon * 0.15f);

            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _mainCamera.backgroundColor = skyColor;
                PositionCelestialVisual(sun, "Sun Visual");
                PositionCelestialVisual(moon, "Moon Visual");
            }

            if (clockText != null)
            {
                int hours = Mathf.FloorToInt(_currentHour);
                int minutes = Mathf.FloorToInt((_currentHour - hours) * 60f);
                clockText.SetText("{0:00}:{1:00}", hours, minutes);
            }
        }

        private void PositionCelestialVisual(Light source, string childName)
        {
            if (source == null || _mainCamera == null)
                return;

            Transform visual = source.transform.Find(childName);
            if (visual != null)
                visual.position = _mainCamera.transform.position - source.transform.forward * 180f;
        }

        private void PrepareCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            // Also covers player/printer cameras created after this scene starts.
            if (isActiveAndEnabled && _skyMaterial != null && camera.cameraType == CameraType.Game && camera.targetTexture == null)
            {
                // Scene activation can restore serialized RenderSettings after Awake.
                // Bind the animated material at render time, not just once at startup.
                if (RenderSettings.skybox != _skyMaterial)
                    RenderSettings.skybox = _skyMaterial;
                _skyMaterial.SetFloat("_CloudAngle", _cloudAngle);
                camera.clearFlags = CameraClearFlags.Skybox;
            }
        }

        private void OnDestroy()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= PrepareCamera;
            if (_skyMaterial != null)
            {
                if (RenderSettings.skybox == _skyMaterial) RenderSettings.skybox = _previousSky;
                Destroy(_skyMaterial);
            }
        }
    }
}
