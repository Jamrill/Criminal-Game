using System.Collections.Generic;
using UnityEngine;
using JuegoCriminal.Core;
using JuegoCriminal.Services;

namespace JuegoCriminal.Scenes
{
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;

        private readonly List<GameObject> _players = new();

        public void DespawnAll()
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i] != null) Destroy(_players[i]);

            _players.Clear();
        }

        public void SpawnOne(
            SceneContext ctx,
            Vector3 pos,
            Quaternion rot,
            int index,
            bool restoreLookRotation = false,
            float playerYaw = 0f,
            float cameraPitch = 0f)
        {
            var go = Instantiate(playerPrefab, pos, rot);
            go.name = $"Player_{index}";
            _players.Add(go);

            if (index == 0)
                go.AddComponent<JuegoCriminal.Player.LocalPlayerMarker>();

            if (restoreLookRotation)
            {
                var controller = go.GetComponent<JuegoCriminal.Player.ThirdPersonController>();
                if (controller != null)
                    controller.SetLookRotation(playerYaw, cameraPitch);
            }

        }

        public void SpawnFromSave(SceneContext ctx, IReadOnlyList<PlayerLoadState> savedPlayers)
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

            DespawnAll();

            // Si hay datos multi-player guardados, usarlos
            if (savedPlayers != null && savedPlayers.Count > 0)
            {
                int spawnedCount = 0;

                for (int i = 0; i < savedPlayers.Count; i++)
                {
                    PlayerLoadState state = savedPlayers[i];
                    Quaternion rotation = state.HasLookRotation
                        ? Quaternion.Euler(0f, state.Yaw, 0f)
                        : ctx.playerSpawn.rotation;

                    SpawnOne(
                        ctx,
                        state.Position,
                        rotation,
                        state.Index,
                        state.HasLookRotation,
                        state.Yaw,
                        state.Pitch
                    );
                    spawnedCount++;
                }

                if (spawnedCount > 0)
                {
                    EnsureLocalPlayerMarker();
                    Debug.Log("[PlayerSpawner] Spawned from save. Count: " + spawnedCount);
                    return;
                }

                Debug.LogWarning("[PlayerSpawner] Save had no valid player positions. Using scene spawn.");
            }

            // Fallback: spawnear 1 en playerSpawn
            SpawnOne(ctx, ctx.playerSpawn.position, ctx.playerSpawn.rotation, 0);
            Debug.Log("[PlayerSpawner] Spawned default player at playerSpawn.");
        }

        private void EnsureLocalPlayerMarker()
        {
            if (_players.Count == 0 || _players[0] == null)
                return;

            if (_players[0].GetComponent<JuegoCriminal.Player.LocalPlayerMarker>() == null)
                _players[0].AddComponent<JuegoCriminal.Player.LocalPlayerMarker>();
        }
    }
}
