using UnityEngine;
using JuegoCriminal.Services;
using JuegoCriminal.States;

//Se encarga de que el objeto que tiene este script no se destruya cuando se cambia de escena y que continúe, además revisa el estado del script GameStateMachine.

namespace JuegoCriminal.Core
{
    public sealed class Bootstrapper : MonoBehaviour
    {
        public static Bootstrapper Instance { get; private set; }

        public SceneLoader SceneLoader { get; private set; }
        public SaveService SaveService { get; private set; }
        public EconomyService EconomyService { get; private set; }
        public PropertyService PropertyService { get; private set; }
        public WorldSaveService WorldSaveService { get; private set; }
        public GameStateMachine GameStateMachine { get; private set; }
        public WorldModeController WorldModeController { get; private set; }

        private bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Bootstrapper] Duplicate @App detected. Destroying the newer instance.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CacheServices();

            if (_initialized) return;
            _initialized = true;

            DontDestroyOnLoad(gameObject);

            // Aquí solo aseguramos que los componentes críticos existen.
            // El flujo real lo gestiona la GameStateMachine.
        }

        private void Start()
        {
            if (Instance != this)
                return;

            if (FindAnyObjectByType<StandalonePreviewScene>() != null)
            {
                Debug.Log("[Bootstrapper] Standalone preview scene detected; normal game boot skipped.");
                return;
            }

            // Arranca la máquina de estados.
            if (GameStateMachine != null)
                GameStateMachine.Boot();
        }

        private void CacheServices()
        {
            SceneLoader = GetComponent<SceneLoader>();
            SaveService = GetComponent<SaveService>();
            EconomyService = GetComponent<EconomyService>();
            PropertyService = GetComponent<PropertyService>();
            WorldSaveService = GetComponent<WorldSaveService>();
            if (WorldSaveService == null)
                WorldSaveService = gameObject.AddComponent<WorldSaveService>();
            GameStateMachine = GetComponent<GameStateMachine>();
            WorldModeController = GetComponent<WorldModeController>();

            if (SceneLoader == null || SaveService == null || GameStateMachine == null)
                Debug.LogError("[Bootstrapper] @App is missing one or more critical services.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
