using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using JuegoCriminal.Services;
using JuegoCriminal.Core;
using JuegoCriminal.Localization;

namespace JuegoCriminal.UI
{
    public enum SlotPanelMode { NewSingle, NewCoop, LoadOnly }

    public sealed class SaveSlotRowUI : MonoBehaviour, IPointerDownHandler
    {
        [Header("UI")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private Button rowButton;
        [SerializeField] private Button renameButton;
        public TMP_Text NameText => nameText;
        public Button RenameButton => renameButton;

        [Header("Save name editing")]
        [SerializeField] private Color nameSelectionColor = new Color(1f, .85f, .35f, .45f);
        [SerializeField] private Color nameCaretColor = Color.white;
        [SerializeField, Range(1, 5)] private int nameCaretWidth = 2;
        [SerializeField] private bool underlineNameWhileEditing = true;
        public Color NameSelectionColor => nameSelectionColor;
        public Color NameCaretColor => nameCaretColor;
        public int NameCaretWidth => nameCaretWidth;
        public bool UnderlineNameWhileEditing => underlineNameWhileEditing;

        [Header("Visual")]
        [SerializeField] private bool isCoopVisual; // Normal=false, Coop=true
        public bool IsCoopVisual => isCoopVisual;

        private int _slotId;

        public int SlotId => _slotId;
        public bool SlotExists { get; private set; }
        public Action<SaveSlotRowUI> Clicked;

        public void Init(int slotId, SlotPanelMode mode, SaveService save, SceneLoader loader)
        {
            _slotId = slotId;

            // Asegurar que el botón es clicable
            if (rowButton != null) rowButton.interactable = true;
        }

        public void SetMode(SlotPanelMode mode)
        {
            // Actualmente solo lo usamos en LoadOnly, así que no necesitamos lógica extra aquí.
        }

        public void Refresh(SaveData dataOrNull)
        {
            SlotExists = (dataOrNull != null);

            if (!SlotExists)
            {
                SetName("");
                if (dateText != null) dateText.text = "";
                if (rowButton != null) rowButton.interactable = false;
                return;
            }

            SetName(dataOrNull.displayName);

            /*if (dateText != null)
            {
                dateText.text = string.IsNullOrWhiteSpace(dataOrNull.lastPlayedUtc)
                    ? ""
                    : dataOrNull.lastPlayedUtc.Replace("T", " ").Replace("Z", " UTC");
            }*/

            if (dateText != null)
            {
                // Más adelante: cultureCode vendrá de Settings (por ejemplo "es-ES" o "en-US")
                string cultureCode = null; // TODO: leerlo de SettingsService
                dateText.text = DateFormatUtil.FormatLastPlayedWithTime(dataOrNull.lastPlayedUtc, cultureCode);
            }

            if (rowButton != null) rowButton.interactable = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        private void SetName(string name)
        {
            if (nameText == null) return;
            var editor = nameText.GetComponent<SaveSlotNameEditor>();
            if (editor != null) editor.SetDisplayName(name);
            else nameText.text = name;
        }
    }
}
