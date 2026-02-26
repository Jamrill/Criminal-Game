using UnityEngine;
using JuegoCriminal.Core;

namespace JuegoCriminal.Scenes
{
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;

        private GameObject _currentPlayer;

        public void Spawn(SceneContext ctx, Vector3? overridePos = null)
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

            Vector3 pos = overridePos ?? ctx.playerSpawn.position;
            Quaternion rot = ctx.playerSpawn.rotation;

            _currentPlayer = Instantiate(playerPrefab, pos, rot);
            _currentPlayer.name = "Player";

            Debug.Log("[PlayerSpawner] Player spawned.");
        }
    }
}