using UnityEngine;
using JuegoCriminal.Services;

namespace JuegoCriminal.Core
{
    public enum GameState
    {
        None,
        Boot,
        Loading,
        World
    }

    public sealed class GameStateMachine : MonoBehaviour
    {
        [Header("Default scenes")]
        [SerializeField] private string fallbackWorldScene = "10_World_City";

        private SceneLoader _sceneLoader;
        private SaveService _save;

        public GameState CurrentState { get; private set; } = GameState.None;

        public SceneContext CurrentSceneContext { get; private set; }
        [SerializeField] private MonoBehaviour worldModeController;

        private void Awake()
        {
            _sceneLoader = GetComponent<SceneLoader>();
            _save = GetComponent<SaveService>();

            if (_sceneLoader != null)
                _sceneLoader.OnSceneLoaded += OnSceneLoaded;
            else
                Debug.LogError("[GSM] SceneLoader missing on @App");
        }

        private void Start()
        {
            if (worldModeController == null)
                worldModeController = GetComponent<JuegoCriminal.States.WorldModeController>();

            //Debug.Log("[GSM] (Start) worldModeController is " + (worldModeController == null ? "NULL" : worldModeController.GetType().Name));
        }

        public void Boot()
        {
            SetState(GameState.Boot);

            bool loaded = _save.Load();
            if (!loaded)
                _save.NewGame();

            string targetScene = _save.Current?.lastScene;
            if (string.IsNullOrWhiteSpace(targetScene))
                targetScene = fallbackWorldScene;

            LoadWorld(targetScene);
        }

        private void LoadWorld(string sceneName)
        {
            SetState(GameState.Loading);
            _sceneLoader.LoadScene(sceneName);

            _save.SetLastScene(sceneName);
            _save.Save();
        }

        private void OnSceneLoaded(string sceneName)
        {
            Debug.Log("[GSM] Scene loaded callback: " + sceneName);

            // Capturar SceneContext (si existe)
            CurrentSceneContext = FindAnyObjectByType<SceneContext>();
            if (CurrentSceneContext == null)
                Debug.LogWarning("[GSM] No SceneContext found in scene: " + sceneName);
            else
                Debug.Log("[GSM] SceneContext registered.");

            // Spawnear player si existe PlayerSpawner en la escena
            var spawner = FindAnyObjectByType<JuegoCriminal.Scenes.PlayerSpawner>();
            if (spawner != null && CurrentSceneContext != null)
                spawner.Spawn(CurrentSceneContext);
            else
                Debug.LogWarning("[GSM] PlayerSpawner not found or SceneContext missing.");

            SetState(GameState.World);
        }

        private void SetState(GameState state)
        {
            CurrentState = state;
            Debug.Log($"[GSM] State -> {state}");

            if (worldModeController != null)
                worldModeController.enabled = (state == GameState.World);
        }
    }
}