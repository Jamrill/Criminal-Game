using JuegoCriminal.Core;
using JuegoCriminal.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    public enum SlotPanelMode { NewSingle, NewCoop, LoadOnly }

    public sealed class SaveSlotRowUI : MonoBehaviour, IPointerDownHandler
    {
        [Header("UI")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private Button rowButton;

        [Header("Visual")]
        [SerializeField] private bool isCoopVisual; // Normal=false, Coop=true
        public bool IsCoopVisual => isCoopVisual;

        private int _slotId;
        private SlotPanelMode _mode;

        public int SlotId => _slotId;
        public bool SlotExists { get; private set; }
        public System.Action<SaveSlotRowUI> Clicked;

        public void Init(int slotId, SlotPanelMode mode, SaveService save, SceneLoader loader)
        {
            _slotId = slotId;
            SetMode(mode);
        }

        public void SetMode(SlotPanelMode mode)
        {
            _mode = mode;

            // Ahora el row se usa básicamente para LoadOnly
            bool loadOnly = (_mode == SlotPanelMode.LoadOnly);

            if (nameInput != null)
            {
                nameInput.readOnly = true;
                nameInput.interactable = false;
                nameInput.enabled = true;

                // En LoadOnly: que nada del input bloquee clicks
                SetAllInputGraphicsRaycast(!loadOnly ? true : false);
            }

            if (rowButton != null)
                rowButton.interactable = true;
        }

        public void Refresh(SaveData dataOrNull)
        {
            SlotExists = (dataOrNull != null);

            if (!SlotExists)
            {
                // En Load panel, normalmente estos rows están ocultos por SlotsPanelUI,
                // pero por si acaso:
                if (nameInput != null) nameInput.text = "";
                if (dateText != null) dateText.text = "";
                if (rowButton != null) rowButton.interactable = false;
                return;
            }

            if (nameInput != null)
                nameInput.text = dataOrNull.displayName;

            if (dateText != null)
            {
                dateText.text = string.IsNullOrWhiteSpace(dataOrNull.lastPlayedUtc)
                    ? ""
                    : dataOrNull.lastPlayedUtc.Replace("T", " ").Replace("Z", " UTC");
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Cualquier click dentro del row lo selecciona
            Clicked?.Invoke(this);
        }

        private void SetAllInputGraphicsRaycast(bool enabled)
        {
            if (nameInput == null) return;

            var graphics = nameInput.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = enabled;
        }
    }
}