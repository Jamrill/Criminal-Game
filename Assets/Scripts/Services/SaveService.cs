using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using JuegoCriminal.Inventory;

namespace JuegoCriminal.Services
{
    public readonly struct PlayerSaveState
    {
        public Vector3 Position { get; }
        public float Yaw { get; }
        public float Pitch { get; }
        public bool HasLookRotation { get; }

        public PlayerSaveState(Vector3 position, float yaw, float pitch, bool hasLookRotation)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
            HasLookRotation = hasLookRotation;
        }
    }

    public readonly struct PlayerLoadState
    {
        public int Index { get; }
        public Vector3 Position { get; }
        public float Yaw { get; }
        public float Pitch { get; }
        public bool HasLookRotation { get; }

        public PlayerLoadState(
            int index,
            Vector3 position,
            float yaw,
            float pitch,
            bool hasLookRotation)
        {
            Index = index;
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
            HasLookRotation = hasLookRotation;
        }
    }

    [Serializable]
    public sealed class PrinterSaveState
    {
        public string persistenceId;
        public string sceneName;
        public int status;
        public string itemId;
        public float elapsedSeconds;
        public int selectedPartIndex;
    }

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 5;

        public int version = CurrentVersion;
        public int money = 1000;
        public string lastScene = "10_World_City";

        public int[] ownedProperties = new int[0];
        public InventoryPlacement[] inventoryItems = Array.Empty<InventoryPlacement>();
        public string[] equippedItemIds = Array.Empty<string>();
        public PrinterSaveState[] printers = Array.Empty<PrinterSaveState>();

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
        public float[] playerYaw = new float[MaxPlayers];
        public float[] cameraPitch = new float[MaxPlayers];
        public bool[] hasLookRotation = new bool[MaxPlayers];

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
        public const int MaxPlayers = SaveData.MaxPlayers;

        private const string MetaFileName = "meta.json";
        private const string SavesFolderName = "saves";

        // Por ahora, slot fijo por defecto
        public const int DefaultSlotId = 1;

        public SaveData Current { get; private set; }
        public bool HasCurrentGame => Current != null && IsValidSlotId(Current.slotId);
        public int CurrentSlotId => HasCurrentGame ? Current.slotId : -1;
        public int CurrentMoney => Current != null ? Current.money : 0;
        public string CurrentSceneName => Current != null ? Current.lastScene : string.Empty;

        private string BasePath => Application.persistentDataPath;
        private string SavesFolderPath => Path.Combine(BasePath, SavesFolderName);
        private string MetaPath => Path.Combine(BasePath, MetaFileName);
        private string MetaBackupPath => Path.Combine(BasePath, "meta.backup.json");
        private string SlotPath(int slotId) => Path.Combine(SavesFolderPath, $"slot_{slotId}.json");
        private string SlotBackupPath(int slotId) => Path.Combine(SavesFolderPath, $"slot_{slotId}.backup.json");

        // ---------- Helpers públicos ----------
        public bool SlotExists(int slotId)
        {
            return IsValidSlotId(slotId) &&
                (File.Exists(SlotPath(slotId)) || File.Exists(SlotBackupPath(slotId)));
        }

        public bool HasAnySlots()
        {
            EnsureFolders();
            return GetExistingSlotIds().Count > 0;
        }

        public int GetFirstFreeSlotId()
        {
            HashSet<int> used = GetExistingSlotIds();
            int candidate = 1;
            while (used.Contains(candidate) && candidate < int.MaxValue)
                candidate++;
            return candidate;
        }

        public void SetCurrent(SaveData data)
        {
            if (TryNormalizeData(data, data != null ? data.slotId : -1, out SaveData normalized))
                Current = normalized;
        }

        public int GetLastSlotId()
        {
            int slotId = LoadMeta().lastSlotId;
            return IsValidSlotId(slotId) ? slotId : -1;
        }

        public int GetContinueSlotId()
        {
            int lastUsedSlotId = GetLastSlotId();

            if (SlotExists(lastUsedSlotId))
                return lastUsedSlotId;

            // Si falta o está dañado meta.json, usamos la partida con fecha más reciente.
            List<SaveData> existingSlots = ListExistingSlots();
            return existingSlots.Count > 0 ? existingSlots[0].slotId : -1;
        }

        public void SetLastSlotId(int slotId)
        {
            if (!IsValidSlotId(slotId))
                return;

            var meta = LoadMeta();
            meta.lastSlotId = slotId;
            SaveMeta(meta);
        }

        public List<SaveData> ListExistingSlots()
        {
            EnsureFolders();
            var list = new List<SaveData>();

            foreach (int i in GetExistingSlotIds())
            {
                try
                {
                    if (TryReadSlotData(i, out SaveData normalized))
                        list.Add(normalized);
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
            TryNormalizeData(Current, -1, out _);
        }

        // Por ahora: NewGame crea/reescribe el SLOT 1
        public void NewGame(string displayName = "Slot 1", string mode = "Single", int slotId = DefaultSlotId)
        {
            if (!IsValidSlotId(slotId))
                slotId = DefaultSlotId;

            Current = new SaveData();
            Current.slotId = slotId;
            Current.displayName = displayName ?? "";
            Current.gameMode = string.IsNullOrWhiteSpace(mode) ? "Single" : mode;

            TryNormalizeData(Current, slotId, out _);

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
            if (!IsValidSlotId(slotId))
                slotId = DefaultSlotId;

            var d = new SaveData();
            d.slotId = slotId;
            d.displayName = displayName ?? "";
            d.gameMode = string.IsNullOrWhiteSpace(mode) ? "Single" : mode;
            d.lastScene = "10_World_City";
            d.lastPlayedUtc = DateTime.UtcNow.ToString("o");
            TryNormalizeData(d, slotId, out _);
            return d;
        }

        // ---------- Slots reales ----------
        public bool SaveSlot(int slotId)
        {
            if (Current == null)
            {
                Debug.LogWarning("[SaveService] SaveSlot called but Current is null");
                return false;
            }

            if (!IsValidSlotId(slotId))
            {
                Debug.LogWarning($"[SaveService] Invalid slot id: {slotId}");
                return false;
            }

            if (!TryNormalizeData(Current, slotId, out SaveData normalized))
                return false;

            Current = normalized;

            EnsureFolders();

            Current.slotId = slotId;
            Current.lastPlayedUtc = DateTime.UtcNow.ToString("o");

            try
            {
                var json = JsonUtility.ToJson(Current, prettyPrint: true);
                WriteTextSafely(SlotPath(slotId), SlotBackupPath(slotId), json);

                SetLastSlotId(slotId);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] SaveSlot failed: {e.Message}");
                return false;
            }
        }

        public bool LoadSlot(int slotId)
        {
            if (!IsValidSlotId(slotId))
                return false;

            EnsureFolders();

            if (!SlotExists(slotId))
                return false;

            try
            {
                if (!TryReadSlotData(slotId, out SaveData normalized))
                    return false;

                Current = normalized;

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
            if (!IsValidSlotId(slotId))
                return;

            EnsureFolders();
            var path = SlotPath(slotId);
            var backupPath = SlotBackupPath(slotId);
            var temporaryPath = GetTemporaryPath(path);

            if (File.Exists(path))
                File.Delete(path);

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

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

        public bool UpdatePlayerStates(IReadOnlyList<PlayerSaveState> playerStates)
        {
            if (!HasCurrentGame)
            {
                Debug.LogWarning("[SaveService] Cannot update players without an active game.");
                return false;
            }

            if (!TryNormalizeData(Current, Current.slotId, out SaveData normalized))
                return false;

            Current = normalized;

            for (int i = 0; i < SaveData.MaxPlayers; i++)
            {
                Current.px[i] = 0f;
                Current.py[i] = 0f;
                Current.pz[i] = 0f;
                Current.hasPos[i] = false;
                Current.playerYaw[i] = 0f;
                Current.cameraPitch[i] = 0f;
                Current.hasLookRotation[i] = false;
            }

            int playerCount = playerStates != null
                ? Math.Min(playerStates.Count, SaveData.MaxPlayers)
                : 0;

            Current.playerCount = playerCount;

            for (int i = 0; i < playerCount; i++)
            {
                PlayerSaveState state = playerStates[i];

                if (!IsFinite(state.Position.x) ||
                    !IsFinite(state.Position.y) ||
                    !IsFinite(state.Position.z))
                {
                    Debug.LogWarning($"[SaveService] Ignoring invalid position for player {i}.");
                    continue;
                }

                Current.px[i] = state.Position.x;
                Current.py[i] = state.Position.y;
                Current.pz[i] = state.Position.z;
                Current.hasPos[i] = true;

                if (state.HasLookRotation && IsFinite(state.Yaw) && IsFinite(state.Pitch))
                {
                    Current.playerYaw[i] = state.Yaw;
                    Current.cameraPitch[i] = state.Pitch;
                    Current.hasLookRotation[i] = true;
                }
            }

            UpdateLegacyPlayerPosition();
            return true;
        }

        public bool SaveCurrentSlot()
        {
            if (!HasCurrentGame)
            {
                Debug.LogWarning("[SaveService] Cannot save without an active slot.");
                return false;
            }

            return SaveSlot(Current.slotId);
        }

        public IReadOnlyList<InventoryPlacement> GetInventoryPlacements()
        {
            return Current?.inventoryItems ?? Array.Empty<InventoryPlacement>();
        }

        public bool UpdateInventoryPlacements(IReadOnlyList<InventoryPlacement> placements)
        {
            if (Current == null) return false;
            int count = placements?.Count ?? 0;
            Current.inventoryItems = new InventoryPlacement[count];
            for (int i = 0; i < count; i++)
            {
                InventoryPlacement source = placements[i];
                Current.inventoryItems[i] = new InventoryPlacement
                {
                    instanceId = source.instanceId,
                    itemId = source.itemId,
                    x = source.x,
                    y = source.y,
                    rotated = source.rotated
                };
            }
            return true;
        }

        public IReadOnlyList<string> GetEquippedItemIds() => Current?.equippedItemIds ?? Array.Empty<string>();

        public bool UpdateEquippedItemIds(IReadOnlyList<string> itemIds)
        {
            if (Current == null) return false;
            int count = itemIds?.Count ?? 0;
            Current.equippedItemIds = new string[count];
            for (int i = 0; i < count; i++) Current.equippedItemIds[i] = itemIds[i];
            return true;
        }

        public PrinterSaveState GetPrinterState(string persistenceId, string sceneName)
        {
            if (Current?.printers == null || string.IsNullOrWhiteSpace(persistenceId)) return null;
            for (int i = 0; i < Current.printers.Length; i++)
            {
                PrinterSaveState state = Current.printers[i];
                if (state != null && state.persistenceId == persistenceId && state.sceneName == sceneName)
                    return state;
            }
            return null;
        }

        public bool UpdatePrinterStates(string sceneName, IReadOnlyList<PrinterSaveState> states)
        {
            if (Current == null || string.IsNullOrWhiteSpace(sceneName)) return false;

            var merged = new List<PrinterSaveState>();
            if (Current.printers != null)
                for (int i = 0; i < Current.printers.Length; i++)
                    if (Current.printers[i] != null && Current.printers[i].sceneName != sceneName)
                        merged.Add(Current.printers[i]);

            if (states != null)
                for (int i = 0; i < states.Count; i++)
                    if (states[i] != null && !string.IsNullOrWhiteSpace(states[i].persistenceId))
                        merged.Add(states[i]);

            Current.printers = merged.ToArray();
            return true;
        }

        public IReadOnlyList<PlayerLoadState> GetCurrentPlayerStates()
        {
            var result = new List<PlayerLoadState>();

            if (Current == null ||
                !TryNormalizeData(Current, Current.slotId, out SaveData normalized))
            {
                return result;
            }

            Current = normalized;
            int count = Math.Min(Current.playerCount, SaveData.MaxPlayers);

            for (int i = 0; i < count; i++)
            {
                if (!Current.hasPos[i])
                    continue;

                result.Add(new PlayerLoadState(
                    i,
                    new Vector3(Current.px[i], Current.py[i], Current.pz[i]),
                    Current.playerYaw[i],
                    Current.cameraPitch[i],
                    Current.hasLookRotation[i]
                ));
            }

            return result;
        }

        public bool TrySpendMoney(int amount, out int remainingMoney)
        {
            remainingMoney = CurrentMoney;

            if (Current == null)
                return false;

            int normalizedAmount = NormalizePositiveAmount(amount);
            if (Current.money < normalizedAmount)
                return false;

            Current.money -= normalizedAmount;
            remainingMoney = Current.money;
            return true;
        }

        public int AddMoney(int amount)
        {
            if (Current == null)
                return 0;

            int normalizedAmount = NormalizePositiveAmount(amount);
            long result = (long)Current.money + normalizedAmount;
            Current.money = (int)Math.Min(result, int.MaxValue);
            return Current.money;
        }

        public bool IsPropertyOwned(int propertyId)
        {
            if (Current == null || propertyId < 0)
                return false;

            Current.ownedProperties = NormalizeOwnedProperties(Current.ownedProperties);
            return Array.IndexOf(Current.ownedProperties, propertyId) >= 0;
        }

        public bool TryAddOwnedProperty(int propertyId)
        {
            if (Current == null || propertyId < 0 || IsPropertyOwned(propertyId))
                return false;

            int[] properties = Current.ownedProperties;
            int oldLength = properties.Length;
            Array.Resize(ref properties, oldLength + 1);
            properties[oldLength] = propertyId;
            Current.ownedProperties = properties;
            return true;
        }

        // ---------- Internos ----------
        private void EnsureFolders()
        {
            if (!Directory.Exists(SavesFolderPath))
                Directory.CreateDirectory(SavesFolderPath);
        }

        private bool IsValidSlotId(int slotId)
        {
            return slotId >= 1;
        }

        private HashSet<int> GetExistingSlotIds()
        {
            EnsureFolders();
            var ids = new HashSet<int>();
            string[] files = Directory.GetFiles(SavesFolderPath, "slot_*.json");

            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                const string prefix = "slot_";
                const string backupSuffix = ".backup.json";
                const string normalSuffix = ".json";
                string number;

                if (fileName.EndsWith(backupSuffix, StringComparison.OrdinalIgnoreCase))
                    number = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - backupSuffix.Length);
                else if (fileName.EndsWith(normalSuffix, StringComparison.OrdinalIgnoreCase))
                    number = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - normalSuffix.Length);
                else
                    continue;

                if (int.TryParse(number, out int slotId) && IsValidSlotId(slotId))
                    ids.Add(slotId);
            }

            return ids;
        }

        private int NormalizePositiveAmount(int amount)
        {
            if (amount == int.MinValue)
                return int.MaxValue;

            return Math.Abs(amount);
        }

        private void UpdateLegacyPlayerPosition()
        {
            if (Current.playerCount > 0 && Current.hasPos[0])
            {
                Current.playerX = Current.px[0];
                Current.playerY = Current.py[0];
                Current.playerZ = Current.pz[0];
                Current.hasPlayerPos = true;
                return;
            }

            Current.playerX = 0f;
            Current.playerY = 0f;
            Current.playerZ = 0f;
            Current.hasPlayerPos = false;
        }

        private bool TryReadSlotData(int slotId, out SaveData normalized)
        {
            normalized = null;

            if (TryReadSaveFile(SlotPath(slotId), slotId, out normalized))
                return true;

            string backupPath = SlotBackupPath(slotId);
            if (!TryReadSaveFile(backupPath, slotId, out normalized))
                return false;

            Debug.LogWarning($"[SaveService] Slot {slotId} recovered from backup.");
            return true;
        }

        private bool TryReadSaveFile(string path, int slotId, out SaveData normalized)
        {
            normalized = null;

            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path);
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                return TryNormalizeData(loaded, slotId, out normalized);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Failed reading '{Path.GetFileName(path)}': {e.Message}");
                return false;
            }
        }

        public bool RenameSlot(int slotId, string newDisplayName)
        {
            if (!IsValidSlotId(slotId) || !SlotExists(slotId)) return false;

            string normalizedName = (newDisplayName ?? string.Empty).Trim();
            if (normalizedName.Length == 0) return false;
            if (normalizedName.Length > 40) normalizedName = normalizedName.Substring(0, 40);

            try
            {
                if (!TryReadSlotData(slotId, out SaveData data)) return false;
                data.displayName = normalizedName;
                WriteTextSafely(
                    SlotPath(slotId),
                    SlotBackupPath(slotId),
                    JsonUtility.ToJson(data, prettyPrint: true));

                if (Current != null && Current.slotId == slotId)
                    Current.displayName = normalizedName;

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] RenameSlot failed: {e.Message}");
                return false;
            }
        }

        private void WriteTextSafely(string destinationPath, string backupPath, string content)
        {
            string temporaryPath = GetTemporaryPath(destinationPath);

            try
            {
                File.WriteAllText(temporaryPath, content);

                if (File.Exists(destinationPath))
                {
                    try
                    {
                        File.Replace(temporaryPath, destinationPath, backupPath);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceWithPortableFallback(temporaryPath, destinationPath, backupPath);
                    }
                }
                else
                {
                    File.Move(temporaryPath, destinationPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private void ReplaceWithPortableFallback(
            string temporaryPath,
            string destinationPath,
            string backupPath)
        {
            File.Copy(destinationPath, backupPath, true);
            File.Copy(temporaryPath, destinationPath, true);
            File.Delete(temporaryPath);
        }

        private string GetTemporaryPath(string destinationPath)
        {
            return destinationPath + ".tmp";
        }

        private bool TryNormalizeData(SaveData data, int expectedSlotId, out SaveData normalized)
        {
            normalized = data;

            if (data == null)
                return false;

            if (data.version > SaveData.CurrentVersion)
            {
                Debug.LogWarning(
                    $"[SaveService] Save version {data.version} is newer than supported version {SaveData.CurrentVersion}."
                );
                return false;
            }

            MigrateData(data);

            data.ownedProperties = NormalizeOwnedProperties(data.ownedProperties);
            data.inventoryItems ??= Array.Empty<InventoryPlacement>();
            data.equippedItemIds ??= Array.Empty<string>();
            data.printers ??= Array.Empty<PrinterSaveState>();
            data.px = ResizeArray(data.px, SaveData.MaxPlayers);
            data.py = ResizeArray(data.py, SaveData.MaxPlayers);
            data.pz = ResizeArray(data.pz, SaveData.MaxPlayers);
            data.hasPos = ResizeArray(data.hasPos, SaveData.MaxPlayers);
            data.playerYaw = ResizeArray(data.playerYaw, SaveData.MaxPlayers);
            data.cameraPitch = ResizeArray(data.cameraPitch, SaveData.MaxPlayers);
            data.hasLookRotation = ResizeArray(data.hasLookRotation, SaveData.MaxPlayers);

            data.playerCount = Mathf.Clamp(data.playerCount, 0, SaveData.MaxPlayers);

            for (int i = 0; i < SaveData.MaxPlayers; i++)
            {
                if (IsFinite(data.px[i]) && IsFinite(data.py[i]) && IsFinite(data.pz[i]))
                    continue;

                data.px[i] = 0f;
                data.py[i] = 0f;
                data.pz[i] = 0f;
                data.hasPos[i] = false;
            }

            for (int i = 0; i < SaveData.MaxPlayers; i++)
            {
                if (IsFinite(data.playerYaw[i]) && IsFinite(data.cameraPitch[i]))
                    continue;

                data.playerYaw[i] = 0f;
                data.cameraPitch[i] = 0f;
                data.hasLookRotation[i] = false;
            }

            if (string.IsNullOrWhiteSpace(data.lastScene))
                data.lastScene = "10_World_City";

            data.gameMode = string.Equals(data.gameMode, "Coop", StringComparison.OrdinalIgnoreCase)
                ? "Coop"
                : "Single";

            if (IsValidSlotId(expectedSlotId))
                data.slotId = expectedSlotId;
            else if (!IsValidSlotId(data.slotId))
                data.slotId = -1;

            if (string.IsNullOrWhiteSpace(data.displayName) && IsValidSlotId(data.slotId))
                data.displayName = $"Slot {data.slotId}";

            data.version = SaveData.CurrentVersion;
            return true;
        }

        private void MigrateData(SaveData data)
        {
            if (data.version >= 2)
                return;

            // Versión 1: una única posición legacy. Se conserva como jugador local.
            if (data.hasPlayerPos && data.playerCount <= 0)
            {
                data.px = ResizeArray(data.px, SaveData.MaxPlayers);
                data.py = ResizeArray(data.py, SaveData.MaxPlayers);
                data.pz = ResizeArray(data.pz, SaveData.MaxPlayers);
                data.hasPos = ResizeArray(data.hasPos, SaveData.MaxPlayers);

                data.px[0] = data.playerX;
                data.py[0] = data.playerY;
                data.pz[0] = data.playerZ;
                data.hasPos[0] = true;
                data.playerCount = 1;
            }
        }

        private int[] NormalizeOwnedProperties(int[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<int>();

            var unique = new HashSet<int>();
            var result = new List<int>(source.Length);

            for (int i = 0; i < source.Length; i++)
            {
                int propertyId = source[i];

                if (propertyId < 0 || !unique.Add(propertyId))
                    continue;

                result.Add(propertyId);
            }

            return result.ToArray();
        }

        private T[] ResizeArray<T>(T[] source, int targetLength)
        {
            var result = new T[targetLength];

            if (source != null)
                Array.Copy(source, result, Math.Min(source.Length, targetLength));

            return result;
        }

        private bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private MetaData LoadMeta()
        {
            if (TryReadMetaFile(MetaPath, out MetaData meta))
                return meta;

            if (TryReadMetaFile(MetaBackupPath, out meta))
            {
                Debug.LogWarning("[SaveService] Save metadata recovered from backup.");
                return meta;
            }

            return new MetaData();
        }

        private void SaveMeta(MetaData meta)
        {
            try
            {
                var json = JsonUtility.ToJson(meta, prettyPrint: true);
                WriteTextSafely(MetaPath, MetaBackupPath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] SaveMeta failed: {e.Message}");
            }
        }

        private bool TryReadMetaFile(string path, out MetaData meta)
        {
            meta = null;

            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path);
                meta = JsonUtility.FromJson<MetaData>(json);
                return meta != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Failed reading '{Path.GetFileName(path)}': {e.Message}");
                return false;
            }
        }
    }
}
