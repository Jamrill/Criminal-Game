using UnityEngine;
using JuegoCriminal.Core;

namespace JuegoCriminal.Scenes
{
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;

        private GameObject _currentPlayer;

        public void Spawn(SceneContext ctx)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] Player Prefab is not assigned.");
                return;
            }

            if (ctx == null || ctx.playerSpawn == null)
            {
                Debug.LogError("[PlayerSpawner] SceneContext or playerSpawn is missing.");
                return;
            }

            if (_currentPlayer != null)
                Destroy(_currentPlayer);

            _currentPlayer = Instantiate(playerPrefab, ctx.playerSpawn.position, ctx.playerSpawn.rotation);
            _currentPlayer.name = "Player";

            Debug.Log("[PlayerSpawner] Player spawned.");
        }
    }
}