using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Services;
using JuegoCriminal.Core;

namespace JuegoCriminal.UI
{
    public sealed class NewGameDialogUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button backButton;
        [SerializeField] private Button acceptButton;

        [Header("Defaults")]
        [SerializeField] private string worldSceneName = "10_World_City";
        [SerializeField] private string singleTitle = "New Game";
        [SerializeField] private string coopTitle = "New Co-op Game";

        // Services (viven en @App)
        private SaveService _save;
        private SceneLoader _loader;

        // Session mode for the next save we create: "Single" or "Coop"
        private string _mode = "Single";

        // When true, we are closing the dialog because we are starting the game,
        // so we should NOT re-enable the main menu buttons (avoids the "flash").
        private bool _isStartingGame;

        private void Awake()
        {
            // Find services once (they exist in @App)
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            // Start hidden by default
            if (panelRoot != null)
                panelRoot.SetActive(false);

            // Wire UI buttons (remove listeners first to avoid duplicates)
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackPressed);
            }

            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(OnAcceptPressed);
            }

            // Pressing Enter on the input field acts like Accept
            if (nameInput != null)
            {
                nameInput.onSubmit.RemoveAllListeners();
                nameInput.onSubmit.AddListener(_ => OnAcceptPressed());
            }
        }

        /// <summary>
        /// Opens the dialog and sets which type of game we are creating.
        /// </summary>
        public void Open(bool coop)
        {
            _isStartingGame = false;
            _mode = coop ? "Coop" : "Single";

            // Update title
            if (titleText != null)
                titleText.text = coop ? coopTitle : singleTitle;

            // Clear and focus input
            if (nameInput != null)
            {
                nameInput.text = "";
                nameInput.ActivateInputField();
            }

            // Show dialog
            if (panelRoot != null)
                panelRoot.SetActive(true);
        }

        /// <summary>
        /// Closes the dialog.
        /// If we are NOT starting the game, we return to the main menu buttons.
        /// </summary>
        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            // If we are starting a new game, do NOT show the menu again (avoids a visual flash)
            if (_isStartingGame)
                return;

            ShowMainMenuButtons();
        }

        // -------------------------
        // UI Button Handlers
        // -------------------------

        private void OnBackPressed()
        {
            // Back simply closes the dialog and returns to main menu buttons
            Close();
        }

        private void OnAcceptPressed()
        {
            if (_save == null || _loader == null)
                return;

            _isStartingGame = true;

            // 1) Build a save display name
            string displayName = GetEnteredNameOrDefault();

            // 2) Choose first free slot (1..MaxSlots)
            int slotId = FindFirstFreeSlotOrDefault();

            // 3) Create/Save the new game (writes slot + meta)
            _save.NewGame(displayName: displayName, mode: _mode, slotId: slotId);

            // 4) Close WITHOUT showing main menu again (no flash), then load world
            if (panelRoot != null)
                panelRoot.SetActive(false);

            _loader.LoadScene(worldSceneName);
        }

        // -------------------------
        // Helper Methods
        // -------------------------

        /// <summary>
        /// Returns the entered name, or a default if empty.
        /// </summary>
        private string GetEnteredNameOrDefault()
        {
            string displayName = nameInput != null ? nameInput.text.Trim() : "";

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = _mode == "Coop" ? "New Co-op Save" : "New Save";

            return displayName;
        }

        /// <summary>
        /// Finds the first available slot. If none exist, falls back to DefaultSlotId.
        /// (Later we can show a "No free slots" warning or a "Overwrite" flow.)
        /// </summary>
        private int FindFirstFreeSlotOrDefault()
        {
            int slotId = -1;

            for (int i = 1; i <= SaveService.MaxSlots; i++)
            {
                if (!_save.SlotExists(i))
                {
                    slotId = i;
                    break;
                }
            }

            if (slotId < 0)
                slotId = SaveService.DefaultSlotId;

            return slotId;
        }

        /// <summary>
        /// Re-enables the main menu buttons panel in a safe way.
        /// </summary>
        private void ShowMainMenuButtons()
        {
            // Preferred: use MainMenuUI helper if available
            var menu = FindAnyObjectByType<JuegoCriminal.Scenes.MainMenuUI>();
            if (menu != null)
                menu.ShowMainButtons();

            // Fallback: enable a panel by name if you still use it
            /*var mainButtons = GameObject.Find("MainPanel");
            if (mainButtons != null)
                mainButtons.SetActive(true);*/
        }
    }
}