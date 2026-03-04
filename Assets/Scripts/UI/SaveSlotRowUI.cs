using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Services;
using JuegoCriminal.Core;

namespace JuegoCriminal.UI
{
    public enum SlotPanelMode { NewSingle, NewCoop, LoadOnly }

    public sealed class SaveSlotRowUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text slotNumberText;
        [SerializeField] private Button rowButton; // botón del slot (raíz)

        [Header("Visual")]
        [SerializeField] private bool isCoopVisual; // Normal prefab = false, Coop prefab = true
        public bool IsCoopVisual => isCoopVisual;

        private int _slotId;
        private SlotPanelMode _mode;
        private SaveService _save;
        private SceneLoader _loader;

        private bool _wired;

        public int SlotId => _slotId;
        public bool SlotExists { get; private set; }
        public System.Action<SaveSlotRowUI> Clicked;

        public void Init(int slotId, SlotPanelMode mode, SaveService save, SceneLoader loader)
        {
            _slotId = slotId;
            _save = save;
            _loader = loader;

            if (slotNumberText != null)
                slotNumberText.text = _slotId.ToString();

            WireOnce();
            SetMode(mode);
        }

        public void SetMode(SlotPanelMode mode)
        {
            _mode = mode;

            bool loadOnly = (_mode == SlotPanelMode.LoadOnly);

            if (nameInput != null)
            {
                // En LoadOnly: que no sea editable ni “seleccionable”
                nameInput.readOnly = loadOnly;
                nameInput.interactable = !loadOnly;
                nameInput.enabled = true; // se ve igual

                // En LoadOnly, que NINGÚN hijo del input bloquee clicks (incluye Caret)
                SetAllInputGraphicsRaycast(!loadOnly);
            }
        }

        public void Refresh(SaveData dataOrNull)
        {
            SlotExists = (dataOrNull != null);

            if (!SlotExists)
            {
                if (nameInput != null)
                {
                    nameInput.text = "";
                    if (nameInput.placeholder != null)
                    {
                        var ph = nameInput.placeholder.GetComponent<TMP_Text>();
                        if (ph != null) ph.text = "Partida nueva";
                    }
                }

                if (dateText != null) dateText.text = "";

                // En LoadOnly y slot vacío: deshabilitar click del row
                if (_mode == SlotPanelMode.LoadOnly)
                {
                    if (rowButton != null) rowButton.interactable = false;
                    SetAllInputGraphicsRaycast(false);
                }
                else
                {
                    if (rowButton != null) rowButton.interactable = true;
                }

                // Ocultar número de slot en "Partida nueva"
                if (slotNumberText != null)
                    slotNumberText.gameObject.SetActive(false);

                return;
            }

            // Slot existente
            if (nameInput != null) nameInput.text = dataOrNull.displayName;
            if (rowButton != null) rowButton.interactable = true;

            if (dateText != null)
            {
                dateText.text = string.IsNullOrWhiteSpace(dataOrNull.lastPlayedUtc)
                    ? ""
                    : dataOrNull.lastPlayedUtc.Replace("T", " ").Replace("Z", " UTC");
            }

            if (slotNumberText != null)
                slotNumberText.gameObject.SetActive(true);

            // En LoadOnly queremos click fácil: raycast del input off
            if (_mode == SlotPanelMode.LoadOnly)
                SetAllInputGraphicsRaycast(false);
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            if (nameInput != null)
            {
                nameInput.onSubmit.RemoveAllListeners();
                nameInput.onEndEdit.RemoveAllListeners();
                nameInput.onSubmit.AddListener(_ => OnSubmit());
                nameInput.onEndEdit.AddListener(_ => OnEndEditFallback());
            }

            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                rowButton.onClick.AddListener(OnRowClicked);
            }
        }

        private void SetAllInputGraphicsRaycast(bool enabled)
        {
            if (nameInput == null) return;

            var graphics = nameInput.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = enabled;
        }

        private void OnRowClicked()
        {
            // Avisar al panel: "me han clickado"
            Clicked?.Invoke(this);

            // En New/Coop: click enfoca el input
            if (_mode != SlotPanelMode.LoadOnly)
                nameInput?.ActivateInputField();
        }

        private void OnSubmit() => HandleAction();

        private void OnEndEditFallback()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                HandleAction();
        }

        public void PerformDefaultAction() => HandleAction();

        private void HandleAction()
        {
            if (_save == null || _loader == null) return;

            bool exists = _save.SlotExists(_slotId);

            if (_mode == SlotPanelMode.LoadOnly)
            {
                if (!exists) return;

                if (_save.LoadSlot(_slotId))
                {
                    var target = _save.Current?.lastScene;
                    if (string.IsNullOrWhiteSpace(target)) target = "10_World_City";
                    _loader.LoadScene(target);
                }
                return;
            }

            // NewSingle / NewCoop: crear o sobrescribir y entrar
            string enteredName = nameInput != null ? nameInput.text?.Trim() : null;
            if (string.IsNullOrWhiteSpace(enteredName))
                enteredName = $"Slot {_slotId}";

            var modeStr = (_mode == SlotPanelMode.NewCoop) ? "Coop" : "Single";
            var data = _save.CreateNewData(_slotId, enteredName, modeStr);

            _save.SetCurrent(data);
            _save.SaveSlot(_slotId);

            _loader.LoadScene("10_World_City");
        }
    }
}