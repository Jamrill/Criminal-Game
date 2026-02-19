using System;
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
    }

    public sealed class SaveService : MonoBehaviour
    {
        private const string FileName = "save.json";
        public SaveData Current { get; private set; }

        private string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public void NewGame()
        {
            Current = new SaveData();
            Save();
        }

        public bool Load()
        {
            if (!File.Exists(SavePath))
                return false;

            try
            {
                var json = File.ReadAllText(SavePath);
                Current = JsonUtility.FromJson<SaveData>(json);
                return Current != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save load failed: {e.Message}");
                return false;
            }
        }

        public void Save()
        {
            if (Current == null) return;

            try
            {
                var json = JsonUtility.ToJson(Current, prettyPrint: true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save write failed: {e.Message}");
            }
        }

        public void SetLastScene(string sceneName)
        {
            if (Current == null) return;
            Current.lastScene = sceneName;
        }
    }
}