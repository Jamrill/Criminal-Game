using UnityEngine;

//Se encarga de que el objeto que tiene este script no se destruya cuando se cambia de escena y que continúe, además revisa el estado del script GameStateMachine.

namespace JuegoCriminal.Core
{
    public sealed class Bootstrapper : MonoBehaviour
    {
        private bool _initialized;

        private void Awake()
        {
            if (_initialized) return;
            _initialized = true;

            DontDestroyOnLoad(gameObject);

            // Aquí solo aseguramos que los componentes críticos existen.
            // El flujo real lo gestiona la GameStateMachine.
        }

        private void Start()
        {
            // Arranca la máquina de estados.
            var gsm = GetComponent<GameStateMachine>();
            if (gsm != null)
                gsm.Boot();
        }
    }
}