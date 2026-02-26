using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Core;
using JuegoCriminal.Services;

namespace JuegoCriminal.Scenes
{
    public sealed class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button quitButton;

        private SaveService _save;
        private SceneLoader _loader;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            if (_save == null) Debug.LogError("[MainMenuUI] SaveService not found (@App missing?)");
            if (continueButton != null && _save != null)
                continueButton.interactable = _save.HasSaveFile;
            if (_loader == null) Debug.LogError("[MainMenuUI] SceneLoader not found (@App missing?)");

            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void Continue()
        {
            if (_save == null || _loader == null) return;
            if (!_save.HasSaveFile) return;

            var target = _save.Current?.lastScene;
            if (string.IsNullOrWhiteSpace(target))
                target = "10_World_City";

            _loader.LoadScene(target);
        }

        private void NewGame()
        {
            if (_save == null || _loader == null) return;

            _save.NewGame();              // resetea save
            _loader.LoadScene("10_World_City");
        }

        private void Quit()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}