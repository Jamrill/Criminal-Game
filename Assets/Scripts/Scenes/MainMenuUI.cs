using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Core;
using JuegoCriminal.Services;
using JuegoCriminal.UI;

namespace JuegoCriminal.Scenes
{
    public sealed class MainMenuUI : MonoBehaviour
    {
        [Header("Main buttons")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button coopButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private SlotsPanelUI slotsPanel;     // tu controlador de slots
        [SerializeField] private GameObject optionsPanel;     // opcional (puede ser null)

        private SaveService _save;
        private SceneLoader _loader;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            if (_save == null) Debug.LogError("[MainMenuUI] SaveService not found (@App missing?)");
            if (_loader == null) Debug.LogError("[MainMenuUI] SceneLoader not found (@App missing?)");
            if (slotsPanel == null) Debug.LogError("[MainMenuUI] SlotsPanelUI not assigned");

            // Continue y LoadGame habilitados solo si hay slots existentes
            RefreshButtons();

            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (newGameButton != null) newGameButton.onClick.AddListener(OpenNewGame);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(OpenLoadGame);
            if (coopButton != null) coopButton.onClick.AddListener(OpenCoop);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);

            if (optionsPanel != null)
                optionsPanel.SetActive(false);
        }

        private void RefreshButtons()
        {
            if (_save == null) return;

            bool anySlots = _save.HasAnySlots();

            if (continueButton != null)
            {
                int last = _save.GetLastSlotId();
                bool canContinue = (last > 0 && _save.SlotExists(last)) || _save.SlotExists(SaveService.DefaultSlotId);
                continueButton.interactable = canContinue;
            }

            if (loadGameButton != null)
                loadGameButton.interactable = anySlots;
        }

        private void Continue()
        {
            if (_save == null || _loader == null) return;

            int slotId = _save.GetLastSlotId();
            if (slotId <= 0 || !_save.SlotExists(slotId))
                slotId = SaveService.DefaultSlotId;

            if (!_save.LoadSlot(slotId))
            {
                Debug.LogWarning("[MainMenuUI] Continue failed: slot not found.");
                RefreshButtons();
                return;
            }

            string target = _save.Current?.lastScene;
            if (string.IsNullOrWhiteSpace(target))
                target = "10_World_City";

            _loader.LoadScene(target);
        }

        private void OpenNewGame()
        {
            if (slotsPanel == null) return;
            slotsPanel.Open(SlotPanelMode.NewSingle);
        }

        private void OpenCoop()
        {
            if (slotsPanel == null) return;
            slotsPanel.Open(SlotPanelMode.NewCoop);
        }

        private void OpenLoadGame()
        {
            if (slotsPanel == null) return;
            slotsPanel.Open(SlotPanelMode.LoadOnly);
        }

        private void OpenOptions()
        {
            if (optionsPanel == null)
            {
                Debug.Log("[MainMenuUI] Options panel not assigned (ok for now).");
                return;
            }

            optionsPanel.SetActive(true);
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