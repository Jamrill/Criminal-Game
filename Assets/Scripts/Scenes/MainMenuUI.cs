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
        [SerializeField] private NewGameDialogUI newGameDialog;

        private SaveService _save;
        private SceneLoader _loader;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            if (_save == null) Debug.LogError("[MainMenuUI] SaveService not found (@App missing?)");
            if (_loader == null) Debug.LogError("[MainMenuUI] SceneLoader not found (@App missing?)");
            if (slotsPanel == null) Debug.LogError("[MainMenuUI] SlotsPanelUI not assigned");
            if (newGameDialog == null) Debug.LogError("[MainMenuUI] NewGameDialogUI not assigned");
            if (mainButtonsPanel == null) Debug.LogError("[MainMenuUI] MainButtonsPanel not assigned");

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
            // Suscripción segura
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
                int last = _save.GetLastSlotId();
                bool canContinue =
                    (last > 0 && _save.SlotExists(last)) ||
                    _save.SlotExists(SaveService.DefaultSlotId);

                continueButton.interactable = canContinue;
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
            if (newGameDialog == null) return;

            // Ocultar menú principal y otros paneles
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);

            newGameDialog.Open(coop: false);
        }

        private void OpenCoop()
        {
            if (newGameDialog == null) return;

            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);

            newGameDialog.Open(coop: true);
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

            if (mainButtonsPanel != null)
                mainButtonsPanel.SetActive(false);

            optionsPanel.SetActive(true);
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