using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Services;
using JuegoCriminal.Core;

namespace JuegoCriminal.UI
{
    public sealed class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button quitButton;

        private SaveService _save;
        private SceneLoader _loader;

        private Transform _player;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            // Encuentra al player (simple por ahora)
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) _player = playerGo.transform;

            if (panel != null) panel.SetActive(false);

            if (saveButton != null) saveButton.onClick.AddListener(SaveGame);
            if (loadButton != null) loadButton.onClick.AddListener(LoadGame);
            if (quitButton != null) quitButton.onClick.AddListener(QuitToMenu);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Toggle();
        }

        private void Toggle()
        {
            if (panel == null) return;

            bool show = !panel.activeSelf;
            panel.SetActive(show);

            Time.timeScale = show ? 0f : 1f; // pausa simple
        }

        private void SaveGame()
        {
            if (_save == null || _save.Current == null)
            {
                Debug.LogWarning("[PauseMenu] SaveService or SaveData missing");
                return;
            }

            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players == null || players.Length == 0)
            {
                Debug.LogWarning("[PauseMenu] No players with tag 'Player' found.");
                return;
            }

            int count = Mathf.Min(players.Length, SaveData.MaxPlayers);
            _save.Current.playerCount = count;

            // Guardamos posiciones
            for (int i = 0; i < SaveData.MaxPlayers; i++)
                _save.Current.hasPos[i] = false;

            for (int i = 0; i < count; i++)
            {
                Vector3 p = players[i].transform.position;
                _save.Current.px[i] = p.x;
                _save.Current.py[i] = p.y;
                _save.Current.pz[i] = p.z;
                _save.Current.hasPos[i] = true;
            }

            // (Opcional) mantener el sistema antiguo apuntando al player 0
            Vector3 p0 = players[0].transform.position;
            _save.Current.playerX = p0.x;
            _save.Current.playerY = p0.y;
            _save.Current.playerZ = p0.z;
            _save.Current.hasPlayerPos = true;

            _save.SetLastScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            int slotId = (_save.Current != null && _save.Current.slotId > 0) ? _save.Current.slotId : SaveService.DefaultSlotId;
            _save.SaveSlot(slotId);

            Debug.Log($"[PauseMenu] Game Saved. Players saved: {count}");
        }

        private void LoadGame()
        {
            if (_save == null) return;

            bool ok = _save.Load();
            Debug.Log("[PauseMenu] Load -> " + ok);

            // recargar escena actual para que el spawn use posición guardada
            if (_loader != null)
                _loader.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            Time.timeScale = 1f;
            if (panel != null) panel.SetActive(false);
        }

        private void QuitToMenu()
        {
            Time.timeScale = 1f;
            if (_loader != null)
                _loader.LoadScene("01_MainMenu");
        }
    }
}