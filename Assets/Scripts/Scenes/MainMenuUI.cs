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

        private SaveService _save;
        private SceneLoader _loader;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            if (_save == null) Debug.LogError("[MainMenuUI] SaveService not found (@App missing?)");
            if (_loader == null) Debug.LogError("[MainMenuUI] SceneLoader not found (@App missing?)");
            if (slotsPanel == null) Debug.LogError("[MainMenuUI] SlotsPanelUI not assigned");
            if (mainButtonsPanel == null) Debug.LogError("[MainMenuUI] MainButtonsPanel not assigned");

            // Listeners de botones (solo se registran una vez aquí)
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (newGameButton != null) newGameButton.onClick.AddListener(OpenNewGame);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(OpenLoadGame);
            if (coopButton != null) coopButton.onClick.AddListener(OpenCoop);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void OnEnable()
        {
            // Suscripción "a prueba de duplicados" al evento de cierre del SlotsPanel
            if (slotsPanel != null)
            {
                slotsPanel.OnClosed -= OnSlotsClosed;
                slotsPanel.OnClosed += OnSlotsClosed;
            }
        }

        private void OnDisable()
        {
            // Buen hábito: desuscribirse al desactivar
            if (slotsPanel != null)
                slotsPanel.OnClosed -= OnSlotsClosed;
        }

        private void Start()
        {
            // Estado inicial del menú (importante si el panel viene desactivado desde la escena)
            if (optionsPanel != null) optionsPanel.SetActive(false);

            // Por seguridad, al entrar al menú mostramos siempre los botones principales
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);

            // Refresca interactables de Continue/Load según slots existentes
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

        private void ShowSlotsPanel(SlotPanelMode mode)
        {
            if (slotsPanel == null) return;

            // Al abrir el panel de slots ocultamos el panel principal del menú
            if (mainButtonsPanel != null)
                mainButtonsPanel.SetActive(false);

            // Y cerramos opciones si estaban abiertas
            if (optionsPanel != null)
                optionsPanel.SetActive(false);

            slotsPanel.Open(mode);
        }

        private void OnSlotsClosed()
        {
            // Al cerrar el panel de slots volvemos a mostrar el panel principal
            if (mainButtonsPanel != null)
                mainButtonsPanel.SetActive(true);

            // Por si se creó o borró una partida: actualizar botones
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

        private void OpenNewGame() => ShowSlotsPanel(SlotPanelMode.NewSingle);
        private void OpenCoop() => ShowSlotsPanel(SlotPanelMode.NewCoop);
        private void OpenLoadGame() => ShowSlotsPanel(SlotPanelMode.LoadOnly);

        private void OpenOptions()
        {
            if (optionsPanel == null)
            {
                Debug.Log("[MainMenuUI] Options panel not assigned (ok for now).");
                return;
            }

            // Ocultamos el panel principal y mostramos opciones
            if (mainButtonsPanel != null)
                mainButtonsPanel.SetActive(false);

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