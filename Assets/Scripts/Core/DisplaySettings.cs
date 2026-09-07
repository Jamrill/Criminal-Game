using System.Collections.Generic;
using UnityEngine;

namespace JuegoCriminal.Core
{
    public static class DisplaySettings
    {
        private static readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        public static IReadOnlyList<Vector2Int> Resolutions => resolutions;
        public static Vector2Int Resolution { get; private set; }
        public static int Mode { get; private set; }
        public static readonly string[] ModeLabels = { "Pantalla completa", "Ventana (con bordes)", "Ventana sin bordes" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            resolutions.Clear();
            var desktop = Screen.currentResolution;
            float aspect = desktop.height > 0 ? (float)desktop.width / desktop.height : 16f / 9f;
            foreach (var mode in Screen.resolutions)
            {
                var size = new Vector2Int(mode.width, mode.height);
                if (mode.width > 3840 || mode.height > 2160 || mode.height <= 0 ||
                    Mathf.Abs((float)mode.width / mode.height - aspect) > 0.01f) continue;
                if (!resolutions.Contains(size)) resolutions.Add(size);
            }
            // Some platforms/editors do not enumerate display modes.
            if (resolutions.Count == 0)
            {
                foreach (int height in new[] { 2160, 1440, 1080, 900, 720 })
                {
                    int width = Mathf.RoundToInt(height * aspect / 2f) * 2;
                    if (width <= 3840 && (desktop.width <= 0 || width <= desktop.width))
                        resolutions.Add(new Vector2Int(width, height));
                }
                if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height)));
            }
            resolutions.Sort((a, b) => b.x.CompareTo(a.x));
            Resolution = new Vector2Int(PlayerPrefs.GetInt("Video.Width", Screen.width), PlayerPrefs.GetInt("Video.Height", Screen.height));
            if (!resolutions.Contains(Resolution)) Resolution = resolutions[0];
            int currentMode = Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen ? 0 : Screen.fullScreenMode == FullScreenMode.Windowed ? 1 : 2;
            Mode = Mathf.Clamp(PlayerPrefs.GetInt("Video.WindowMode", currentMode), 0, 2);
            if (PlayerPrefs.HasKey("Video.Width")) Apply();
        }

        public static void SetResolution(int index)
        {
            if (index < 0 || index >= resolutions.Count) return;
            Resolution = resolutions[index];
            Save();
        }

        public static void SetMode(int index)
        {
            Mode = Mathf.Clamp(index, 0, 2);
            Save();
        }

        private static void Save()
        {
            PlayerPrefs.SetInt("Video.Width", Resolution.x);
            PlayerPrefs.SetInt("Video.Height", Resolution.y);
            PlayerPrefs.SetInt("Video.WindowMode", Mode);
            PlayerPrefs.Save();
            Apply();
        }

        private static void Apply()
        {
            // Window modes must be verified in a standalone build, not the editor window.
            if (Application.isEditor) return;
            var mode = Mode == 0 ? FullScreenMode.ExclusiveFullScreen : Mode == 1 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            Screen.SetResolution(Resolution.x, Resolution.y, mode);
        }
    }
}
