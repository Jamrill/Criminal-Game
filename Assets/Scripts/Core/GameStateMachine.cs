using System.Collections;
using UnityEngine;
using JuegoCriminal.Services;
using JuegoCriminal.Player;

namespace JuegoCriminal.Core
{
    public enum GameState
    {
        None,
        Boot,
        Loading,
        Menu,
        World
    }

    public sealed class GameStateMachine : MonoBehaviour
    {
        [Header("Default scenes")]
        [SerializeField] private string fallbackMenuScene = "01_MainMenu";


        private SceneLoader _sceneLoader;
        private SaveService _save;
        private EconomyService _economy;

        public GameState CurrentState { get; private set; } = GameState.None;

        public SceneContext CurrentSceneContext { get; private set; }
        [SerializeField] private MonoBehaviour worldModeController;
        private Coroutine _sceneSetupRoutine;

        private void Awake()
        {
            _sceneLoader = GetComponent<SceneLoader>();
            _save = GetComponent<SaveService>();
            _economy = GetComponent<EconomyService>();

            if (_sceneLoader != null)
            {
                _sceneLoader.OnSceneLoadStarted += OnSceneLoadStarted;
                _sceneLoader.OnSceneLoaded += OnSceneLoaded;
            }
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

            // El arranque no debe cargar ningún slot: hacerlo modificaría el metadato de
            // "última partida" antes de que el jugador pulse Continue o elija una partida.
            _save.InitEmptyInMemory();

            _sceneLoader.LoadScene(fallbackMenuScene);
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

            // Las escenas sin contexto de mundo (por ejemplo, el menú) no intentan crear jugadores.
            var spawner = FindAnyObjectByType<JuegoCriminal.Scenes.PlayerSpawner>();
            if (spawner != null && CurrentSceneContext != null)
            {
                spawner.SpawnFromSave(CurrentSceneContext, _save.GetCurrentPlayerStates());
            }
            else if (CurrentSceneContext != null)
            {
                Debug.LogWarning("[GSM] SceneContext exists but PlayerSpawner is missing.");
            }

            if (_economy != null)
                _economy.SyncFromSave();

            if (CurrentSceneContext == null)
            {
                SetState(GameState.Menu);
                _sceneLoader.ReportSceneReady(sceneName);
                return;
            }

            _sceneSetupRoutine = StartCoroutine(WaitForWorldReady(sceneName));
        }

        private IEnumerator WaitForWorldReady(string sceneName)
        {
            // Instantiate ejecuta Awake/OnEnable inmediatamente, pero Start y los seguidores de
            // cámara necesitan al menos un frame completo para quedar inicializados.
            while (FindAnyObjectByType<LocalPlayerMarker>() == null || Camera.main == null)
                yield return null;

            yield return null;
            yield return new WaitForEndOfFrame();

            SetState(GameState.World);
            _sceneLoader.ReportSceneReady(sceneName);
            _sceneSetupRoutine = null;
        }

        private void OnSceneLoadStarted(string sceneName)
        {
            if (_sceneSetupRoutine != null)
            {
                StopCoroutine(_sceneSetupRoutine);
                _sceneSetupRoutine = null;
            }

            SetState(GameState.Loading);
        }

        private void OnDestroy()
        {
            if (_sceneLoader == null)
                return;

            _sceneLoader.OnSceneLoadStarted -= OnSceneLoadStarted;
            _sceneLoader.OnSceneLoaded -= OnSceneLoaded;
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
