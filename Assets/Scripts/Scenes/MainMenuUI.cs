using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Core;
using JuegoCriminal.Services;
using JuegoCriminal.UI;

namespace JuegoCriminal.Scenes
{
    public sealed class MainMenuUI : MonoBehaviour
    {
        [Header("Main buttons panel")]
        [SerializeField] private GameObject mainButtonsPanel;

        [SerializeField] private MenuTransitionController transitions;


        [Header("Main buttons")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button coopButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private SlotsPanelUI slotsPanel;
        [SerializeField] private GameObject optionsPanel; // opcional
        [SerializeField] private ControlsMenuUI controlsMenu;
        [SerializeField] private NewGameDialogUI newGamePanel;

        private SaveService _save;
        private SceneLoader _loader;

        private void Awake()
        {
            Bootstrapper app = Bootstrapper.Instance;
            _save = app != null ? app.SaveService : null;
            _loader = app != null ? app.SceneLoader : null;

            if (_save == null) _save = FindAnyObjectByType<SaveService>();
            if (_loader == null) _loader = FindAnyObjectByType<SceneLoader>();

            if (_save == null) Debug.LogError("[MainMenuUI] SaveService not found (@App missing?)");
            if (_loader == null) Debug.LogError("[MainMenuUI] SceneLoader not found (@App missing?)");
            if (slotsPanel == null) Debug.LogError("[MainMenuUI] SlotsPanelUI not assigned");
            if (newGamePanel == null) Debug.LogError("[MainMenuUI] NewGameDialogUI not assigned");
            if (mainButtonsPanel == null) Debug.LogError("[MainMenuUI] MainButtonsPanel not assigned");

            if (controlsMenu == null)
                controlsMenu = FindAnyObjectByType<ControlsMenuUI>(FindObjectsInactive.Include);

            // Listeners
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (newGameButton != null) newGameButton.onClick.AddListener(OpenNewGame);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(OpenLoadGame);
            if (coopButton != null) coopButton.onClick.AddListener(OpenCoop);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void OnEnable()
        {
            // Suscripci�n segura
            if (slotsPanel != null)
            {
                slotsPanel.OnClosed -= OnSlotsClosed;
                slotsPanel.OnClosed += OnSlotsClosed;
            }
        }

        private void OnDisable()
        {
            if (slotsPanel != null)
                slotsPanel.OnClosed -= OnSlotsClosed;
        }

        private void Start()
        {
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (_save == null) return;

            bool anySlots = _save.HasAnySlots();

            if (continueButton != null)
            {
                int continueSlotId = _save.GetContinueSlotId();
                continueButton.interactable = continueSlotId > 0;
            }

            if (loadGameButton != null)
                loadGameButton.interactable = anySlots;
        }

        private void OnSlotsClosed()
        {
            if (mainButtonsPanel != null)
                mainButtonsPanel.SetActive(true);

            RefreshButtons();
        }

        private void Continue()
        {
            if (_save == null || _loader == null) return;

            int slotId = _save.GetContinueSlotId();

            if (slotId <= 0 || !_save.LoadSlot(slotId))
            {
                Debug.LogWarning("[MainMenuUI] Continue failed: slot not found.");
                RefreshButtons();
                return;
            }

            string target = _save.CurrentSceneName;
            if (string.IsNullOrWhiteSpace(target))
                target = "10_World_City";

            _loader.LoadScene(target);
        }

        private void OpenNewGame()
        {
            if (newGamePanel == null) return;

            if (optionsPanel != null)
                optionsPanel.SetActive(false);

            if (transitions != null)
                transitions.TransitionToNewGame(coop: false);
            else
            {
                if (mainButtonsPanel != null)
                    mainButtonsPanel.SetActive(false);

                newGamePanel.Open(coop: false);
            }
        }

        private void OpenCoop()
        {
            if (newGamePanel == null) return;

            if (optionsPanel != null)
                optionsPanel.SetActive(false);

            if (transitions != null)
                transitions.TransitionToNewGame(coop: true);
            else
            {
                if (mainButtonsPanel != null)
                    mainButtonsPanel.SetActive(false);

                newGamePanel.Open(coop: true);
            }
        }

        private void OpenLoadGame()
        {
            if (transitions != null)
                transitions.TransitionToLoadGame();
            else
                slotsPanel.Open(SlotPanelMode.LoadOnly); // fallback
        }

        private void OpenOptions()
        {
            if (optionsPanel == null)
            {
                Debug.Log("[MainMenuUI] Options panel not assigned (ok for now).");
                return;
            }

            if (controlsMenu != null)
                controlsMenu.Close();

            if (transitions != null)
                transitions.TransitionToOptions(optionsPanel);
            else
            {
                if (mainButtonsPanel != null)
                    mainButtonsPanel.SetActive(false);

                optionsPanel.SetActive(true);
            }
        }

        public void CloseOptions()
        {
            if (controlsMenu != null)
                controlsMenu.Close();

            if (transitions != null)
                transitions.TransitionBackFromOptions(optionsPanel);
            else
            {
                if (optionsPanel != null)
                    optionsPanel.SetActive(false);

                ShowMainButtons();
            }
        }

        public void ShowMainButtons()
        {
            if (mainButtonsPanel != null)
                mainButtonsPanel.SetActive(true);

            RefreshButtons();
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
