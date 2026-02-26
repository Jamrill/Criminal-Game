using System;
using System.IO;
using UnityEngine;

//Script enfocado a guardar y cargar la partida, además identifica la escena cargada en el momento.

namespace JuegoCriminal.Services
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int money = 1000;
        public string lastScene = "10_World_City";

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

    }

    public sealed class SaveService : MonoBehaviour
    {
        private const string FileName = "save.json";
        public SaveData Current { get; private set; }

        private string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public bool HasSaveFile => File.Exists(SavePath);

        public void InitEmptyInMemory()
        {
            Current = new SaveData();
            // No guardamos aquí
        }

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