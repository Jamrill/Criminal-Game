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

        // Selección
        private int _selectedSlotId = -1;
        private bool _selectedSlotExists = false;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            if (panelRoot != null) panelRoot.SetActive(false);

            // Back
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackPressed);
            }

            // Delete
            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(OpenConfirmDelete);
                deleteButton.interactable = false;
            }

            // Load
            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(OnLoadPressed);
                loadButton.interactable = false;
            }

            // Background (deselect)
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(ClearSelection);
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
            // Este panel ya solo se usa para LoadOnly
            // (si lo llamas con otro modo, lo tratamos igual)
            if (panelRoot != null) panelRoot.SetActive(true);
            if (confirmPanel != null) confirmPanel.SetActive(false);

            BuildIfNeeded();
            RefreshAll();
            ClearSelection();

            // Mostrar/ocultar el botón Load (solo tiene sentido en Load)
            if (loadButton != null)
                loadButton.gameObject.SetActive(true);
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
                row.Init(slotId, SlotPanelMode.LoadOnly, _save, _loader);
                row.Clicked += OnRowClicked;

                _rows.Add(row);
                _rowBySlot[slotId] = row;
            }
        }
        private void OnBackPressed()
        {
            if (transitions != null)
                transitions.TransitionBackToMainMenu();
            else
                Close(); // fallback
        }

        // Asegura que el slot usa el prefab correcto (single/coop) según el SaveData
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

        private void RefreshAll()
        {
            if (_save == null) return;

            var existing = _save.ListExistingSlots();
            var map = new Dictionary<int, SaveData>();
            foreach (var s in existing)
                map[s.slotId] = s;

            for (int i = 0; i < _rows.Count; i++)
            {
                int slotId = i + 1;
                map.TryGetValue(slotId, out var data);

                bool show = (data != null); // solo existentes

                bool wantCoop = (data != null) &&
                                string.Equals(data.gameMode, "Coop", StringComparison.OrdinalIgnoreCase);

                var row = EnsureRowPrefab(slotId, wantCoop);
                if (row == null) continue;

                row.gameObject.SetActive(show);
                if (!show) continue;

                row.SetMode(SlotPanelMode.LoadOnly);
                row.Refresh(data);
            }
        }

        private void OnRowClicked(SaveSlotRowUI row)
        {
            if (row == null) return;

            _selectedSlotId = row.SlotId;
            _selectedSlotExists = row.SlotExists;

            if (deleteButton != null) deleteButton.interactable = _selectedSlotExists;
            if (loadButton != null) loadButton.interactable = _selectedSlotExists;
        }

        private void OnLoadPressed()
        {
            if (_save == null || _loader == null) return;
            if (_selectedSlotId <= 0 || !_selectedSlotExists) return;

            if (_save.LoadSlot(_selectedSlotId))
            {
                var target = _save.Current?.lastScene;
                if (string.IsNullOrWhiteSpace(target)) target = "10_World_City";
                _loader.LoadScene(target);
            }
        }

        private void OpenConfirmDelete()
        {
            if (_selectedSlotId <= 0 || !_selectedSlotExists) return;
            if (confirmPanel != null) confirmPanel.SetActive(true);
        }

        private void ConfirmDeleteNo()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        private void ConfirmDeleteYes()
        {
            if (_save == null) return;
            if (_selectedSlotId <= 0 || !_selectedSlotExists) return;

            _save.DeleteSlot(_selectedSlotId);

            if (confirmPanel != null) confirmPanel.SetActive(false);

            RefreshAll();
            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectedSlotId = -1;
            _selectedSlotExists = false;

            if (deleteButton != null) deleteButton.interactable = false;
            if (loadButton != null) loadButton.interactable = false;

            if (confirmPanel != null) confirmPanel.SetActive(false);
        }
    }
}