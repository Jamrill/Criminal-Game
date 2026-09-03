using JuegoCriminal.Services;
using JuegoCriminal.States;
using JuegoCriminal.UI;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Script encargado de crear el elemento empty en escena que contiene los scripts importantes de la partida,
// incluido este, y además se encarga de cargar la escena.

namespace JuegoCriminal.Core
{
    public static class AppAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureApp()
        {
            if (UnityEngine.Object.FindAnyObjectByType<Bootstrapper>() != null) return;

            var go = new GameObject("@App");
            go.AddComponent<SceneLoader>();
            go.AddComponent<SaveService>();
            go.AddComponent<EconomyService>();
            go.AddComponent<PropertyService>();
            go.AddComponent<WorldSaveService>();

            var worldMode = go.AddComponent<JuegoCriminal.States.WorldModeController>();
            worldMode.enabled = false;

            // Bootstrapper se añade al final porque AddComponent ejecuta Awake inmediatamente.
            // Así puede cachear una composición completa y GameStateMachine ve todos los servicios.
            go.AddComponent<GameStateMachine>();
            go.AddComponent<Bootstrapper>();

            Debug.Log("[AutoBootstrap] @App created");
        }
    }

    public sealed class SceneLoader : MonoBehaviour
    {
        [Header("Loading Screen")]
        [SerializeField] private string loadingText = "Loading...";
        [SerializeField] private float minimumLoadingTime = 1.0f;
        [Min(1f)]
        [SerializeField] private float sceneSetupTimeout = 30f;
        [SerializeField] private int framesToWaitAfterLoad = 2;

        private string _readySceneName;

        public bool IsLoading { get; private set; }
        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoaded;

        public void ReportSceneReady(string sceneName)
        {
            if (!IsLoading || string.IsNullOrWhiteSpace(sceneName))
                return;

            _readySceneName = sceneName;
            Debug.Log("[SceneLoader] Scene ready: " + sceneName);
        }

        public void LoadScene(string sceneName)
        {
            if (IsLoading) return;

            Debug.Log("[SceneLoader] Loading: " + sceneName);
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            _readySceneName = null;
            OnSceneLoadStarted?.Invoke(sceneName);

            Time.timeScale = 1f;

            LoadingScreenUI loadingScreen = GetLoadingScreen();

            if (loadingScreen != null)
                loadingScreen.Show(loadingText);

            // Esperamos un frame para que Unity llegue a pintar el panel antes de empezar la carga.
            yield return null;

            float startTime = Time.unscaledTime;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (op == null)
            {
                Debug.LogError("[SceneLoader] Could not load scene: " + sceneName);
                IsLoading = false;

                if (loadingScreen != null)
                    loadingScreen.Hide();

                yield break;
            }

            op.allowSceneActivation = false;

            // Unity suele llegar hasta 0.9 y espera allowSceneActivation.
            while (op.progress < 0.9f)
                yield return null;

            float elapsed = Time.unscaledTime - startTime;
            float remaining = minimumLoadingTime - elapsed;

            if (remaining > 0f)
                yield return new WaitForSecondsRealtime(remaining);

            op.allowSceneActivation = true;

            while (!op.isDone)
                yield return null;

            Debug.Log("[SceneLoader] Loaded: " + sceneName);

            OnSceneLoaded?.Invoke(sceneName);

            // La activación de Unity no implica que el mundo ya esté preparado. Esperamos a que
            // GameStateMachine confirme que el contexto, el jugador y la cámara están disponibles.
            float setupStartTime = Time.unscaledTime;

            while (_readySceneName != sceneName)
            {
                if (Time.unscaledTime - setupStartTime >= sceneSetupTimeout)
                {
                    Debug.LogError($"[SceneLoader] Scene setup timed out after {sceneSetupTimeout:0.#}s: {sceneName}");
                    break;
                }

                yield return null;
            }

            // Margen configurable para que otros Start/LateUpdate reaccionen a la escena preparada.
            for (int i = 0; i < framesToWaitAfterLoad; i++)
                yield return null;

            loadingScreen = GetLoadingScreen();

            if (loadingScreen != null)
                loadingScreen.Hide();

            IsLoading = false;
            _readySceneName = null;
        }

        private LoadingScreenUI GetLoadingScreen()
        {
            if (LoadingScreenUI.Instance != null)
                return LoadingScreenUI.Instance;

            return FindAnyObjectByType<LoadingScreenUI>(FindObjectsInactive.Include);
        }
    }
}
