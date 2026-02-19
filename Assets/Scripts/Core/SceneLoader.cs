using JuegoCriminal.Services;
using JuegoCriminal.States;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            var worldMode = go.AddComponent<JuegoCriminal.States.WorldModeController>();
            worldMode.enabled = false;

            Debug.Log("[AutoBootstrap] @App created");
        }
    }

    public sealed class SceneLoader : MonoBehaviour
    {
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
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            while (!op.isDone)
                yield return null;

            IsLoading = false;
            Debug.Log("[SceneLoader] Loaded: " + sceneName);
            OnSceneLoaded?.Invoke(sceneName);
        }
    }
}