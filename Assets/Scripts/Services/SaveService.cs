using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace JuegoCriminal.Services
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int money = 1000;
        public string lastScene = "10_World_City";

        public int[] ownedProperties = new int[0];

        // Compat (puedes quitarlo más adelante)
        public float playerX;
        public float playerY;
        public float playerZ;
        public bool hasPlayerPos;

        public const int MaxPlayers = 4;

        public int playerCount = 0;
        public float[] px = new float[MaxPlayers];
        public float[] py = new float[MaxPlayers];
        public float[] pz = new float[MaxPlayers];
        public bool[] hasPos = new bool[MaxPlayers];

        // Slots
        public int slotId = -1;
        public string displayName = "";
        public string gameMode = "Single"; // "Single" o "Coop"
        public string lastPlayedUtc = "";  // DateTime.UtcNow.ToString("o")
    }

    [Serializable]
    public sealed class MetaData
    {
        public int lastSlotId = -1;
    }

    public sealed class SaveService : MonoBehaviour
    {
        public const int MaxSlots = 10;

        private const string MetaFileName = "meta.json";
        private const string SavesFolderName = "saves";

        // Por ahora, slot fijo por defecto
        public const int DefaultSlotId = 1;

        public SaveData Current { get; private set; }

        private string BasePath => Application.persistentDataPath;
        private string SavesFolderPath => Path.Combine(BasePath, SavesFolderName);
        private string MetaPath => Path.Combine(BasePath, MetaFileName);
        private string SlotPath(int slotId) => Path.Combine(SavesFolderPath, $"slot_{slotId}.json");

        // ---------- Helpers públicos ----------
        public bool SlotExists(int slotId) => File.Exists(SlotPath(slotId));

        public bool HasAnySlots()
        {
            EnsureFolders();
            for (int i = 1; i <= MaxSlots; i++)
                if (File.Exists(SlotPath(i))) return true;
            return false;
        }

        public void SetCurrent(SaveData data)
        {
            Current = data;
        }

        public int GetLastSlotId() => LoadMeta().lastSlotId;

        public void SetLastSlotId(int slotId)
        {
            var meta = LoadMeta();
            meta.lastSlotId = slotId;
            SaveMeta(meta);
        }

        public List<SaveData> ListExistingSlots()
        {
            EnsureFolders();
            var list = new List<SaveData>();

            for (int i = 1; i <= MaxSlots; i++)
            {
                var path = SlotPath(i);
                if (!File.Exists(path)) continue;

                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<SaveData>(json);
                    if (data != null) list.Add(data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveService] Failed reading slot {i}: {e.Message}");
                }
            }

            list.Sort((a, b) => string.Compare(b.lastPlayedUtc, a.lastPlayedUtc, StringComparison.Ordinal));
            return list;
        }

        // ---------- Flujo actual del juego ----------
        public void InitEmptyInMemory()
        {
            Current = new SaveData();
        }

        // Por ahora: NewGame crea/reescribe el SLOT 1
        public void NewGame(string displayName = "Slot 1", string mode = "Single", int slotId = DefaultSlotId)
        {
            Current = new SaveData();
            Current.slotId = slotId;
            Current.displayName = displayName ?? "";
            Current.gameMode = string.IsNullOrWhiteSpace(mode) ? "Single" : mode;

            SaveSlot(slotId);
        }

        // Por ahora: Load carga SLOT 1 (o el que pases)
        public bool Load(int slotId = DefaultSlotId)
        {
            return LoadSlot(slotId);
        }

        // Guarda el slot actual (por defecto slot 1)
        public void Save(int slotId = DefaultSlotId)
        {
            SaveSlot(slotId);
        }

        public SaveData CreateNewData(int slotId, string displayName, string mode)
        {
            var d = new SaveData();
            d.slotId = slotId;
            d.displayName = displayName ?? "";
            d.gameMode = string.IsNullOrWhiteSpace(mode) ? "Single" : mode;
            d.lastScene = "10_World_City";
            d.lastPlayedUtc = DateTime.UtcNow.ToString("o");
            return d;
        }

        // ---------- Slots reales ----------
        public void SaveSlot(int slotId)
        {
            if (Current == null)
            {
                Debug.LogWarning("[SaveService] SaveSlot called but Current is null");
                return;
            }

            EnsureFolders();

            Current.slotId = slotId;
            Current.lastPlayedUtc = DateTime.UtcNow.ToString("o");

            try
            {
                var json = JsonUtility.ToJson(Current, prettyPrint: true);
                File.WriteAllText(SlotPath(slotId), json);

                SetLastSlotId(slotId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] SaveSlot failed: {e.Message}");
            }
        }

        public bool LoadSlot(int slotId)
        {
            EnsureFolders();

            var path = SlotPath(slotId);
            if (!File.Exists(path))
                return false;

            try
            {
                var json = File.ReadAllText(path);
                Current = JsonUtility.FromJson<SaveData>(json);
                if (Current == null) return false;

                SetLastSlotId(slotId);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] LoadSlot failed: {e.Message}");
                return false;
            }
        }

        public void DeleteSlot(int slotId)
        {
            EnsureFolders();
            var path = SlotPath(slotId);

            if (File.Exists(path))
                File.Delete(path);

            var meta = LoadMeta();
            if (meta.lastSlotId == slotId)
            {
                meta.lastSlotId = -1;
                SaveMeta(meta);
            }
        }

        public void SetLastScene(string sceneName)
        {
            if (Current == null) return;
            Current.lastScene = sceneName;
        }

        // ---------- Internos ----------
        private void EnsureFolders()
        {
            if (!Directory.Exists(SavesFolderPath))
                Directory.CreateDirectory(SavesFolderPath);
        }

        private MetaData LoadMeta()
        {
            try
            {
                if (!File.Exists(MetaPath))
                    return new MetaData();

                var json = File.ReadAllText(MetaPath);
                var meta = JsonUtility.FromJson<MetaData>(json);
                return meta ?? new MetaData();
            }
            catch
            {
                return new MetaData();
            }
        }

        private void SaveMeta(MetaData meta)
        {
            try
            {
                var json = JsonUtility.ToJson(meta, prettyPrint: true);
                File.WriteAllText(MetaPath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] SaveMeta failed: {e.Message}");
            }
        }
    }
}