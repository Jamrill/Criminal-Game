using System.Collections.Generic;
using UnityEngine;
using JuegoCriminal.Core;

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

        public void SpawnOne(SceneContext ctx, Vector3 pos, Quaternion rot, int index)
        {
            var go = Instantiate(playerPrefab, pos, rot);
            go.name = $"Player_{index}";
            _players.Add(go);

            if (index == 0)
                go.AddComponent<JuegoCriminal.Player.LocalPlayerMarker>();

        }

        public void SpawnFromSave(SceneContext ctx, JuegoCriminal.Services.SaveData save)
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
            if (save != null && save.playerCount > 0)
            {
                int count = Mathf.Min(save.playerCount, JuegoCriminal.Services.SaveData.MaxPlayers);

                for (int i = 0; i < count; i++)
                {
                    if (!save.hasPos[i]) continue;

                    var pos = new Vector3(save.px[i], save.py[i], save.pz[i]);
                    SpawnOne(ctx, pos, ctx.playerSpawn.rotation, i);
                }

                Debug.Log("[PlayerSpawner] Spawned from save. Count: " + count);
                return;
            }

            // Fallback: spawnear 1 en playerSpawn
            SpawnOne(ctx, ctx.playerSpawn.position, ctx.playerSpawn.rotation, 0);
            Debug.Log("[PlayerSpawner] Spawned default player at playerSpawn.");
        }
    }
}