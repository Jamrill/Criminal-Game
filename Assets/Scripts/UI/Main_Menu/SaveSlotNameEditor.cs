using JuegoCriminal.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    /// <summary>Añade edición en línea al texto de nombre de una fila de guardado.</summary>
    public sealed class SaveSlotNameEditor : MonoBehaviour
    {
        private SaveSlotRowUI _row;
        private SaveService _save;
        private TMP_Text _text;
        private TMP_InputField _input;
        private string _savedName;

        public void Configure(SaveSlotRowUI row, SaveService save, TMP_Text text)
        {
            _row = row;
            _save = save;
            _text = text;
            _savedName = text != null ? text.text : string.Empty;
            if (_text == null) return;

            _input = _text.GetComponent<TMP_InputField>();
            if (_input == null) _input = _text.gameObject.AddComponent<TMP_InputField>();
            _input.textViewport = _text.rectTransform;
            _input.textComponent = _text;
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _input.characterLimit = 40;
            _input.transition = Selectable.Transition.None;
            _input.onSelect.RemoveListener(OnSelected);
            _input.onSelect.AddListener(OnSelected);
            _input.onEndEdit.RemoveListener(OnEndEdit);
            _input.onEndEdit.AddListener(OnEndEdit);
        }

        public void SetInteractable(bool value)
        {
            if (_input != null) _input.interactable = value;
        }

        private void OnSelected(string value)
        {
            _savedName = _text != null ? _text.text : string.Empty;
            if (_row != null)
                _row.Clicked?.Invoke(_row);
        }

        private void OnEndEdit(string value)
        {
            if (_row == null || !_row.SlotExists || _save == null) return;
            string normalized = (value ?? string.Empty).Trim();
            if (!_save.RenameSlot(_row.SlotId, normalized))
            {
                _text.text = _savedName;
                return;
            }

            _savedName = normalized;
            _text.text = normalized;
        }
    }
}
