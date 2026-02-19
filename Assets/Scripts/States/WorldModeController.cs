using UnityEngine;

namespace JuegoCriminal.States
{
    public sealed class WorldModeController : MonoBehaviour
    {
        private void OnEnable()
        {
            Debug.Log("[WorldMode] Enabled");
        }

        private void OnDisable()
        {
            Debug.Log("[WorldMode] Disabled");
        }
    }
}