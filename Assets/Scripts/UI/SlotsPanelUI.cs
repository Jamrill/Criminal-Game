using System.Collections.Generic;
using UnityEngine;
using JuegoCriminal.Services;
using JuegoCriminal.Core;

namespace JuegoCriminal.UI
{
    public sealed class SlotsPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject panelRoot;          // el panel entero (SlotsPanel)
        [SerializeField] private Transform content;             // ScrollView/Viewport/Content
        [SerializeField] private SaveSlotRowUI slotRowPrefab;   // tu prefab SlotRow con SaveSlotRowUI

        private SaveService _save;
        private SceneLoader _loader;

        private readonly List<SaveSlotRowUI> _rows = new();

        private SlotPanelMode _mode = SlotPanelMode.NewSingle;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
            _loader = FindAnyObjectByType<SceneLoader>();

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Open(SlotPanelMode mode)
        {
            _mode = mode;

            if (panelRoot != null) panelRoot.SetActive(true);

            BuildIfNeeded();
            RefreshAll();
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void BuildIfNeeded()
        {
            if (_rows.Count > 0) return;
            if (slotRowPrefab == null || content == null) return;

            for (int slotId = 1; slotId <= SaveService.MaxSlots; slotId++)
            {
                var row = Instantiate(slotRowPrefab, content);
                row.Init(slotId, _mode, _save, _loader);
                _rows.Add(row);
            }
        }

        private void RefreshAll()
        {
            if (_save == null) return;

            var existing = _save.ListExistingSlots();
            var map = new Dictionary<int, SaveData>();
            foreach (var s in existing)
                map[s.slotId] = s;

            // Calcula el primer slot libre (1..MaxSlots)
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
                        ? (data != null) // solo existentes
                        : (data != null || slotId == firstFree); // existentes + 1 "nuevo"

                _rows[i].gameObject.SetActive(show);

                if (!show) continue;

                _rows[i].SetMode(_mode);
                _rows[i].Refresh(data);
            }
        }
    }
}