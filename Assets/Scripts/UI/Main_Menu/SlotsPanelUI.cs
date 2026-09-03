using JuegoCriminal.Core;
using JuegoCriminal.Services;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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
        public event Action CloseRequested;
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private SaveService _save;
        private SceneLoader _loader;

        private readonly List<SaveSlotRowUI> _rows = new();
        private readonly Dictionary<int, SaveSlotRowUI> _rowBySlot = new();

        private int _selectedSlotId = -1;
        private bool _selectedSlotExists = false;

        private bool _confirmDeleteOpen;

        private void Awake()
        {
            Bootstrapper app = Bootstrapper.Instance;
            _save = app != null ? app.SaveService : null;
            _loader = app != null ? app.SceneLoader : null;

            if (_save == null) _save = FindAnyObjectByType<SaveService>();
            if (_loader == null) _loader = FindAnyObjectByType<SceneLoader>();

            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBackPressed);
                backButton.onClick.AddListener(OnBackPressed);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveListener(OpenConfirmDelete);
                deleteButton.onClick.AddListener(OpenConfirmDelete);
                deleteButton.interactable = false;
            }

            if (loadButton != null)
            {
                loadButton.onClick.RemoveListener(OnLoadPressed);
                loadButton.onClick.AddListener(OnLoadPressed);
                loadButton.interactable = false;
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveListener(ClearSelection);
                backgroundButton.onClick.AddListener(ClearSelection);
            }

            if (confirmPanel != null)
                confirmPanel.SetActive(false);

            if (confirmYesButton != null)
            {
                confirmYesButton.onClick.RemoveListener(ConfirmDeleteYes);
                confirmYesButton.onClick.AddListener(ConfirmDeleteYes);
            }

            if (confirmNoButton != null)
            {
                confirmNoButton.onClick.RemoveListener(ConfirmDeleteNo);
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

        private void OnBackPressed()
        {
            if (_confirmDeleteOpen)
                return;

            if (transitions != null)
                transitions.TransitionBackToMainMenu();
            else if (CloseRequested != null)
                CloseRequested.Invoke();
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
            int listIndex = _rows.IndexOf(current);
            current.Clicked -= OnRowClicked;
            Destroy(current.gameObject);

            var prefab = wantCoop ? slotRowCoopPrefab : slotRowPrefab;
            var newRow = Instantiate(prefab, content);
            newRow.transform.SetSiblingIndex(siblingIndex);

            newRow.Init(slotId, SlotPanelMode.LoadOnly, _save, _loader);
            newRow.Clicked += OnRowClicked;
            ConfigureNameEditing(newRow);

            _rowBySlot[slotId] = newRow;
            if (listIndex >= 0) _rows[listIndex] = newRow;

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

            var existingIds = new HashSet<int>();
            for (int i = 0; i < existing.Count; i++) existingIds.Add(existing[i].slotId);

            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                SaveSlotRowUI stale = _rows[i];
                if (stale != null && existingIds.Contains(stale.SlotId)) continue;
                if (stale != null)
                {
                    stale.Clicked -= OnRowClicked;
                    _rowBySlot.Remove(stale.SlotId);
                    Destroy(stale.gameObject);
                }
                _rows.RemoveAt(i);
            }

            for (int i = 0; i < existing.Count; i++)
            {
                SaveData data = existing[i];
                bool wantCoop = string.Equals(data.gameMode, "Coop", StringComparison.OrdinalIgnoreCase);
                SaveSlotRowUI row;

                if (!_rowBySlot.TryGetValue(data.slotId, out row) || row == null)
                {
                    SaveSlotRowUI prefab = wantCoop && slotRowCoopPrefab != null
                        ? slotRowCoopPrefab : slotRowPrefab;
                    if (prefab == null) continue;
                    row = Instantiate(prefab, content);
                    row.Init(data.slotId, SlotPanelMode.LoadOnly, _save, _loader);
                    row.Clicked += OnRowClicked;
                    ConfigureNameEditing(row);
                    _rows.Add(row);
                    _rowBySlot[data.slotId] = row;
                }

                row = EnsureRowPrefab(data.slotId, wantCoop);
                if (row == null) continue;
                row.gameObject.SetActive(true);
                row.SetMode(SlotPanelMode.LoadOnly);
                row.Refresh(data);
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
                var target = _save.CurrentSceneName;

                if (string.IsNullOrWhiteSpace(target))
                    target = "10_World_City";

                // También se usa desde pausa, donde el juego está detenido.
                Time.timeScale = 1f;
                _loader.LoadScene(target);
            }
        }

        private void OpenConfirmDelete()
        {
            if (_selectedSlotId <= 0 || !_selectedSlotExists)
                return;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

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

                SaveSlotNameEditor nameEditor = row.GetComponentInChildren<SaveSlotNameEditor>(true);
                if (nameEditor != null)
                    nameEditor.SetInteractable(interactable);

                CanvasGroup canvasGroup = row.GetComponent<CanvasGroup>();

                if (canvasGroup != null)
                {
                    canvasGroup.interactable = interactable;
                    canvasGroup.blocksRaycasts = interactable;
                }
            }
        }

        private void ConfigureNameEditing(SaveSlotRowUI row)
        {
            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text nameText = null;
            for (int i = 0; i < texts.Length; i++)
                if (texts[i].name != "DateText")
                {
                    nameText = texts[i];
                    break;
                }

            if (nameText == null) return;
            SaveSlotNameEditor editor = nameText.GetComponent<SaveSlotNameEditor>();
            if (editor == null) editor = nameText.gameObject.AddComponent<SaveSlotNameEditor>();
            editor.Configure(row, _save, nameText);
        }
    }
}
