using JuegoCriminal.Core;
using JuegoCriminal.Services;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    public sealed class SlotsPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform content;
        [SerializeField] private SaveSlotRowUI slotRowPrefab;     // single (rojo)
        [SerializeField] private SaveSlotRowUI slotRowCoopPrefab; // coop (azul)
        [SerializeField] private Button backButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button backgroundButton;
        [SerializeField] private MenuTransitionController transitions;

        [Header("Confirm Delete Panel")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        public event Action OnClosed;

        private SaveService _save;
        private SceneLoader _loader;

        private readonly List<SaveSlotRowUI> _rows = new();
        private readonly Dictionary<int, SaveSlotRowUI> _rowBySlot = new();

        private int _selectedSlotId = -1;
        private bool _selectedSlotExists = false;

        private bool _confirmDeleteOpen;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackPressed);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(OpenConfirmDelete);
                deleteButton.interactable = false;
            }

            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(OnLoadPressed);
                loadButton.interactable = false;
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(ClearSelection);
            }

            if (confirmPanel != null)
                confirmPanel.SetActive(false);

            if (confirmYesButton != null)
            {
                confirmYesButton.onClick.RemoveAllListeners();
                confirmYesButton.onClick.AddListener(ConfirmDeleteYes);
            }

            if (confirmNoButton != null)
            {
                confirmNoButton.onClick.RemoveAllListeners();
                confirmNoButton.onClick.AddListener(ConfirmDeleteNo);
            }
        }

        // Abre el panel de slots en modo carga.
        // Este panel actualmente solo se usa para Load Game.
        public void Open(SlotPanelMode mode)
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);

            HideConfirmDeletePanel();

            BuildIfNeeded();
            RefreshAll();
            ClearSelection();

            if (loadButton != null)
                loadButton.gameObject.SetActive(true);
        }

        public void Close()
        {
            HideConfirmDeletePanel();

            if (panelRoot != null)
                panelRoot.SetActive(false);

            OnClosed?.Invoke();
        }

        // Construye las filas de slots una sola vez.
        // Después solo se refrescan los datos para evitar reinstanciar UI continuamente.
        private void BuildIfNeeded()
        {
            if (_rows.Count > 0) return;
            if (content == null || slotRowPrefab == null) return;

            for (int slotId = 1; slotId <= SaveService.MaxSlots; slotId++)
            {
                var row = Instantiate(slotRowPrefab, content);
                row.Init(slotId, SlotPanelMode.LoadOnly, _save, _loader);
                row.Clicked += OnRowClicked;

                _rows.Add(row);
                _rowBySlot[slotId] = row;
            }
        }

        private void OnBackPressed()
        {
            if (_confirmDeleteOpen)
                return;

            if (transitions != null)
                transitions.TransitionBackToMainMenu();
            else
                Close();
        }

        // Cambia el prefab visual de una fila si la partida es Coop.
        // Las partidas Single usan un prefab y las Coop otro.
        private SaveSlotRowUI EnsureRowPrefab(int slotId, bool wantCoop)
        {
            if (!_rowBySlot.TryGetValue(slotId, out var current) || current == null)
                return null;

            if (current.IsCoopVisual == wantCoop)
                return current;

            if (wantCoop && slotRowCoopPrefab == null)
                return current;

            int siblingIndex = current.transform.GetSiblingIndex();
            current.Clicked -= OnRowClicked;
            Destroy(current.gameObject);

            var prefab = wantCoop ? slotRowCoopPrefab : slotRowPrefab;
            var newRow = Instantiate(prefab, content);
            newRow.transform.SetSiblingIndex(siblingIndex);

            newRow.Init(slotId, SlotPanelMode.LoadOnly, _save, _loader);
            newRow.Clicked += OnRowClicked;

            _rowBySlot[slotId] = newRow;
            _rows[slotId - 1] = newRow;

            return newRow;
        }

        //Refresca todas las filas visibles según los saves existentes.
        private void RefreshAll()
        {
            if (_save == null)
                return;

            // 1) Obtenemos todos los saves existentes.
            var existing = _save.ListExistingSlots();

            // 2) Los ordenamos por fecha de última partida, de más reciente a más antigua.
            existing.Sort((a, b) =>
            {
                DateTime dateA = GetLastPlayedDate(a);
                DateTime dateB = GetLastPlayedDate(b);

                return dateB.CompareTo(dateA);
            });

            // 3) Creamos un mapa por slotId para refrescar cada fila correctamente.
            var map = new Dictionary<int, SaveData>();

            foreach (var saveData in existing)
                map[saveData.slotId] = saveData;

            // 4) Refrescamos todas las filas según el slot que representan.
            for (int i = 0; i < _rows.Count; i++)
            {
                int slotId = i + 1;
                map.TryGetValue(slotId, out var data);

                bool show = data != null;

                bool wantCoop =
                    data != null &&
                    string.Equals(data.gameMode, "Coop", StringComparison.OrdinalIgnoreCase);

                var row = EnsureRowPrefab(slotId, wantCoop);
                if (row == null)
                    continue;

                row.gameObject.SetActive(show);

                if (!show)
                    continue;

                row.SetMode(SlotPanelMode.LoadOnly);
                row.Refresh(data);
            }

            // 5) Reordenamos visualmente las filas activas en el Content.
            // No cambiamos el slot real, solo el orden en pantalla.
            for (int i = 0; i < existing.Count; i++)
            {
                int slotId = existing[i].slotId;

                if (_rowBySlot.TryGetValue(slotId, out var row) && row != null)
                    row.transform.SetSiblingIndex(i);
            }

            // 6) Si está abierto el ConfirmPanel, mantenemos las filas bloqueadas.
            SetRowsInteractable(!_confirmDeleteOpen);
        }

        private DateTime GetLastPlayedDate(SaveData data)
        {
            if (data == null)
                return DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(data.lastPlayedUtc))
                return DateTime.MinValue;

            if (DateTime.TryParse(
                    data.lastPlayedUtc,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out DateTime parsedDate))
            {
                return parsedDate;
            }

            return DateTime.MinValue;
        }

        // Se ejecuta al seleccionar una fila de save.
        // Activa Load/Delete solo si el slot existe.
        private void OnRowClicked(SaveSlotRowUI row)
        {
            if (_confirmDeleteOpen)
                return;

            if (row == null)
                return;

            _selectedSlotId = row.SlotId;
            _selectedSlotExists = row.SlotExists;

            RefreshActionButtons();
        }

        private void OnLoadPressed()
        {
            if (_confirmDeleteOpen)
                return;

            if (_save == null || _loader == null) return;
            if (_selectedSlotId <= 0 || !_selectedSlotExists) return;

            if (_save.LoadSlot(_selectedSlotId))
            {
                var target = _save.Current?.lastScene;

                if (string.IsNullOrWhiteSpace(target))
                    target = "10_World_City";

                _loader.LoadScene(target);
            }
        }

        private void OpenConfirmDelete()
        {
            if (_selectedSlotId <= 0 || !_selectedSlotExists)
                return;

            ShowConfirmDeletePanel();
        }

        private void ConfirmDeleteNo()
        {
            HideConfirmDeletePanel();
            RefreshActionButtons();
        }

        private void ConfirmDeleteYes()
        {
            if (_save == null)
            {
                HideConfirmDeletePanel();
                return;
            }

            if (_selectedSlotId <= 0 || !_selectedSlotExists)
            {
                HideConfirmDeletePanel();
                return;
            }

            _save.DeleteSlot(_selectedSlotId);

            HideConfirmDeletePanel();

            RefreshAll();
            ClearSelection();
        }

        private void ClearSelection()
        {
            if (_confirmDeleteOpen)
                return;

            _selectedSlotId = -1;
            _selectedSlotExists = false;

            RefreshActionButtons();
        }

        // Abre la ventana de confirmación de borrado.
        // Mientras está abierta, bloqueamos el resto del panel.
        private void ShowConfirmDeletePanel()
        {
            _confirmDeleteOpen = true;

            if (confirmPanel != null)
                confirmPanel.SetActive(true);

            SetSlotPanelControlsInteractable(false);

            if (confirmYesButton != null)
                confirmYesButton.interactable = true;

            if (confirmNoButton != null)
                confirmNoButton.interactable = true;
        }

        // Cierra la ventana de confirmación y devuelve el control al SlotsPanel.
        private void HideConfirmDeletePanel()
        {
            _confirmDeleteOpen = false;

            if (confirmPanel != null)
                confirmPanel.SetActive(false);

            SetSlotPanelControlsInteractable(true);
            RefreshActionButtons();
        }

        // Bloquea o desbloquea los controles del SlotsPanel.
        // Importante: los botones Yes/No del ConfirmPanel NO se bloquean aquí.
        private void SetSlotPanelControlsInteractable(bool interactable)
        {
            if (backButton != null)
                backButton.interactable = interactable;

            if (backgroundButton != null)
                backgroundButton.interactable = interactable;

            SetRowsInteractable(interactable);

            if (!interactable)
            {
                if (deleteButton != null)
                    deleteButton.interactable = false;

                if (loadButton != null)
                    loadButton.interactable = false;

                return;
            }

            RefreshActionButtons();
        }

        // Actualiza Load/Delete según haya o no una partida seleccionada.
        private void RefreshActionButtons()
        {
            if (_confirmDeleteOpen)
            {
                if (deleteButton != null)
                    deleteButton.interactable = false;

                if (loadButton != null)
                    loadButton.interactable = false;

                return;
            }

            if (deleteButton != null)
                deleteButton.interactable = _selectedSlotExists;

            if (loadButton != null)
                loadButton.interactable = _selectedSlotExists;
        }

        // Bloquea también las filas de saves para impedir selección/clicks detrás del ConfirmPanel.
        private void SetRowsInteractable(bool interactable)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                SaveSlotRowUI row = _rows[i];

                if (row == null)
                    continue;

                Button[] buttons = row.GetComponentsInChildren<Button>(true);

                for (int j = 0; j < buttons.Length; j++)
                    buttons[j].interactable = interactable;

                CanvasGroup canvasGroup = row.GetComponent<CanvasGroup>();

                if (canvasGroup != null)
                {
                    canvasGroup.interactable = interactable;
                    canvasGroup.blocksRaycasts = interactable;
                }
            }
        }
    }
}