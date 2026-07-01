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
            go.AddComponent<Bootstrapper>();
            go.AddComponent<SceneLoader>();
            go.AddComponent<SaveService>();
            go.AddComponent<GameStateMachine>();
            go.AddComponent<EconomyService>();
            go.AddComponent<PropertyService>();

            var worldMode = go.AddComponent<JuegoCriminal.States.WorldModeController>();
            worldMode.enabled = false;

            Debug.Log("[AutoBootstrap] @App created");
        }
    }

    public sealed class SceneLoader : MonoBehaviour
    {
        [Header("Loading Screen")]
        [SerializeField] private string loadingText = "Loading...";
        [SerializeField] private float minimumLoadingTime = 1.0f;
        [SerializeField] private int framesToWaitAfterLoad = 2;

        public bool IsLoading { get; private set; }
        public event Action<string> OnSceneLoaded;

        public void LoadScene(string sceneName)
        {
            if (IsLoading) return;

            Debug.Log("[SceneLoader] Loading: " + sceneName);
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;

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

            // Esperamos algunos frames para que GameStateMachine, spawner y demás terminen de preparar la escena.
            for (int i = 0; i < framesToWaitAfterLoad; i++)
                yield return null;

            loadingScreen = GetLoadingScreen();

            if (loadingScreen != null)
                loadingScreen.Hide();

            IsLoading = false;
        }

        private LoadingScreenUI GetLoadingScreen()
        {
            if (LoadingScreenUI.Instance != null)
                return LoadingScreenUI.Instance;

            return FindAnyObjectByType<LoadingScreenUI>(FindObjectsInactive.Include);
        }
    }
}