using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Services;
using JuegoCriminal.Core;

namespace JuegoCriminal.UI
{
    public sealed class SlotsPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject panelRoot;                  // el panel entero (SlotsPanel)
        [SerializeField] private Transform content;                     // ScrollView/Viewport/Content
        [SerializeField] private SaveSlotRowUI slotRowPrefab;           // prefab SlotRow (single)
        [SerializeField] private SaveSlotRowUI slotRowCoopPrefab;       // prefab SlotRow (coop)
        [SerializeField] private Button backButton;                     // Botón para volver atrás
        [SerializeField] private Button deleteButton;                   // Botón para borrar partida

        [Header("Confirm Delete Panel")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        [Header("Load Button (only for LoadOnly)")]
        [SerializeField] private Button loadButton;

        // Estado interno: slot seleccionado y borrado armado
        private int _selectedSlotId = -1;
        private bool _selectedSlotExists = false;
        private bool _deleteArmed = false;

        public event Action OnClosed;

        private SaveService _save;
        private SceneLoader _loader;

        private readonly List<SaveSlotRowUI> _rows = new();
        private readonly Dictionary<int, SaveSlotRowUI> _rowBySlot = new();

        private SlotPanelMode _mode = SlotPanelMode.NewSingle;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            if (panelRoot != null) panelRoot.SetActive(false);

            // Back
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(Close);
            }

            // Delete
            if (deleteButton != null)
            {
                deleteButton.interactable = false;
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(OnDeletePressed);
            }

            // Load
            if (loadButton != null)
            {
                loadButton.interactable = false;
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(OnLoadPressed);
                loadButton.gameObject.SetActive(false);
            }

            // Confirm panel
            if (confirmPanel != null) confirmPanel.SetActive(false);

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

        public void Open(SlotPanelMode mode)
        {
            _mode = mode;

            // Mostrar el botón Load solo en LoadOnly
            if (loadButton != null)
                loadButton.gameObject.SetActive(_mode == SlotPanelMode.LoadOnly);

            // Reset estado al abrir
            _selectedSlotId = -1;
            _selectedSlotExists = false;
            _deleteArmed = false;

            if (loadButton != null) loadButton.interactable = false;
            if (deleteButton != null) deleteButton.interactable = false;

            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(true);

            BuildIfNeeded();
            RefreshAll();
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            OnClosed?.Invoke();
        }

        private void BuildIfNeeded()
        {
            if (_rows.Count > 0) return;
            if (content == null || slotRowPrefab == null) return;

            for (int slotId = 1; slotId <= SaveService.MaxSlots; slotId++)
            {
                var row = Instantiate(slotRowPrefab, content);
                row.Init(slotId, _mode, _save, _loader);
                row.Clicked += OnRowClicked;

                _rows.Add(row);
                _rowBySlot[slotId] = row;
            }
        }

        // Asegura que el slotId está representado con el prefab correcto (single/coop)
        private SaveSlotRowUI EnsureRowPrefab(int slotId, bool wantCoopPrefab)
        {
            if (!_rowBySlot.TryGetValue(slotId, out var current) || current == null)
                return null;

            // Si ya es del tipo correcto, no hacemos nada
            if (current.IsCoopVisual == wantCoopPrefab)
                return current;

            // Si queremos coop pero no hay prefab coop, no podemos cambiar
            if (wantCoopPrefab && slotRowCoopPrefab == null)
                return current;

            // Guardar posición en la jerarquía para mantener el orden
            int siblingIndex = current.transform.GetSiblingIndex();

            current.Clicked -= OnRowClicked;
            Destroy(current.gameObject);

            var prefab = wantCoopPrefab ? slotRowCoopPrefab : slotRowPrefab;
            var newRow = Instantiate(prefab, content);
            newRow.transform.SetSiblingIndex(siblingIndex);

            newRow.Init(slotId, _mode, _save, _loader);
            newRow.Clicked += OnRowClicked;

            _rowBySlot[slotId] = newRow;
            if (slotId - 1 >= 0 && slotId - 1 < _rows.Count)
                _rows[slotId - 1] = newRow;

            return newRow;
        }

        private void RefreshAll()
        {
            if (_save == null) return;

            var existing = _save.ListExistingSlots();
            var map = new Dictionary<int, SaveData>();
            foreach (var s in existing)
                map[s.slotId] = s;

            // Calcula el primer slot libre (1..MaxSlots) para mostrar "Partida nueva"
            int firstFree = -1;
            for (int id = 1; id <= SaveService.MaxSlots; id++)
            {
                if (!map.ContainsKey(id))
                {
                    firstFree = id;
                    break;
                }
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                int slotId = i + 1;
                map.TryGetValue(slotId, out var data);

                // Decidir si se muestra esta fila
                bool show =
                    (_mode == SlotPanelMode.LoadOnly)
                        ? (data != null)                          // solo existentes
                        : (data != null || slotId == firstFree);  // existentes + 1 "nuevo"

                // Determinar qué visual queremos para este slot
                bool wantCoop =
                    (data != null)
                        ? string.Equals(data.gameMode, "Coop", StringComparison.OrdinalIgnoreCase)
                        : (_mode == SlotPanelMode.NewCoop); // "Partida nueva" en modo coop

                var row = EnsureRowPrefab(slotId, wantCoop);
                if (row == null) continue;

                row.gameObject.SetActive(show);
                if (!show) continue;

                row.SetMode(_mode);
                row.Refresh(data);
            }
        }

        // Cuando el usuario clickea un slot row
        private void OnRowClicked(SaveSlotRowUI row)
        {
            if (row == null) return;

            // Guardar selección
            _selectedSlotId = row.SlotId;
            _selectedSlotExists = row.SlotExists;

            // Activar botones según selección
            if (deleteButton != null)
                deleteButton.interactable = _selectedSlotExists;

            if (loadButton != null)
                loadButton.interactable = (_mode == SlotPanelMode.LoadOnly && _selectedSlotExists);

            // Si el borrado está armado, este click abre confirmación
            if (_deleteArmed)
            {
                _deleteArmed = false;
                OpenConfirmDelete();
                return;
            }

            // En LoadOnly NO cargamos al click (ahora se carga con Load button)
        }

        private void OnLoadPressed()
        {
            if (_mode != SlotPanelMode.LoadOnly) return;
            if (_selectedSlotId <= 0 || !_selectedSlotExists) return;
            if (_save == null || _loader == null) return;

            if (_save.LoadSlot(_selectedSlotId))
            {
                var target = _save.Current?.lastScene;
                if (string.IsNullOrWhiteSpace(target)) target = "10_World_City";
                _loader.LoadScene(target);
            }
        }

        // Al pulsar el botón Delete
        private void OnDeletePressed()
        {
            if (_selectedSlotId > 0 && _selectedSlotExists)
            {
                OpenConfirmDelete();
                return;
            }

            // En LoadOnly, si no hay selección, armamos borrado para que el siguiente click elija slot
            if (_mode == SlotPanelMode.LoadOnly)
                _deleteArmed = true;
        }

        private void OpenConfirmDelete()
        {
            if (!_selectedSlotExists || _selectedSlotId <= 0) return;
            if (confirmPanel != null) confirmPanel.SetActive(true);
        }

        private void ConfirmDeleteNo()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            _deleteArmed = false;
        }

        private void ConfirmDeleteYes()
        {
            if (_save == null) return;

            if (_selectedSlotId > 0)
                _save.DeleteSlot(_selectedSlotId);

            if (confirmPanel != null) confirmPanel.SetActive(false);

            // Reset selección y refrescar lista
            _selectedSlotId = -1;
            _selectedSlotExists = false;
            _deleteArmed = false;

            if (deleteButton != null) deleteButton.interactable = false;
            if (loadButton != null) loadButton.interactable = false;

            RefreshAll();
        }
    }
}