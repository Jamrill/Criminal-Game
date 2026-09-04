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

        private float _currentHour;
        private Camera _mainCamera;

        public float CurrentHour => _currentHour;

        private void Awake()
        {
            _currentHour = startHour;
            _mainCamera = Camera.main;
            ApplyLighting();
        }

        private void Update()
        {
            if (cycleEnabled)
            {
                float hoursPerSecond = 24f / (fullDayDurationMinutes * 60f);
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

            Color nightSky = new Color(0.012f, 0.025f, 0.075f);
            Color daySky = new Color(0.30f, 0.62f, 0.86f);
            Color dawnSky = new Color(0.95f, 0.31f, 0.10f);
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
    }
}
