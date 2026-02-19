using UnityEngine;

namespace JuegoCriminal.Core
{
    public sealed class SceneContext : MonoBehaviour
    {
        [Header("Optional references (can be empty for now)")]
        public Transform playerSpawn;
        public Transform[] enemySpawns;

        private void Awake()
        {
            Debug.Log($"[SceneContext] Awake in scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        }
    }
}