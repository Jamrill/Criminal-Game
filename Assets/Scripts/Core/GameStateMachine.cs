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

        private void Awake()
        {
            _sceneLoader = GetComponent<SceneLoader>();
            _save = GetComponent<SaveService>();

            if (_sceneLoader != null)
                _sceneLoader.OnSceneLoaded += _ => SetState(GameState.World);
        }

        public void Boot()
        {
            SetState(GameState.Boot);

            // Intentar cargar save; si no existe, crear nueva partida
            bool loaded = _save.Load();
            if (!loaded)
                _save.NewGame();

            // Elegir escena destino
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

        private void SetState(GameState state)
        {
            CurrentState = state;
            // Más adelante: emitir evento OnStateChanged
            Debug.Log($"[GSM] State -> {state}");
        }
    }
}