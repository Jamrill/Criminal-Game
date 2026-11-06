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
        private Button _pencil;
        private bool _canEdit;
        private bool _editing;
        private FontStyles _savedFontStyle;

        public void Configure(SaveSlotRowUI row, SaveService save, TMP_Text text)
        {
            if (_pencil != null) _pencil.onClick.RemoveListener(BeginEditing);
            _row = row;
            _save = save;
            _text = text;
            _savedName = text != null ? text.text.TrimEnd('\u200B') : string.Empty;
            if (_text == null) return;

            if (_input == null) CreateInputContainer();
            _input.textViewport = (RectTransform)_input.transform;
            _input.textComponent = _text;
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _input.characterLimit = SaveService.MaxSaveNameLength;
            _input.SetTextWithoutNotify(_savedName);
            _input.transition = Selectable.Transition.None;
            _input.onFocusSelectAll = true;
            ApplyEditingStyle();
            _input.navigation = new Navigation { mode = Navigation.Mode.None };
            _input.interactable = false;
            _text.raycastTarget = false;
            _input.onEndEdit.RemoveListener(OnEndEdit);
            _input.onEndEdit.AddListener(OnEndEdit);
            // TMP creates the caret renderer in OnEnable, after text/viewport
            // references must be assigned (AddComponent enables it too early).
            ShowDisplayName(_savedName);
            _pencil = row.RenameButton;
            if (_pencil != null) _pencil.onClick.AddListener(BeginEditing);
            SetInteractable(true);
        }

        public void SetInteractable(bool value)
        {
            _canEdit = value;
            if (!value && _editing) FinishEditing(_savedName);
            if (_pencil != null) _pencil.interactable = value && _row != null && _row.SlotExists;
        }

        public void SetDisplayName(string name)
        {
            _savedName = name ?? string.Empty;
            if (_editing) FinishEditing(_savedName);
            else if (_input != null) ShowDisplayName(_savedName);
            else if (_text != null) _text.text = _savedName;
        }

        public void BeginEditing()
        {
            if (!_canEdit || _editing || _input == null || _save == null || _row == null || !_row.SlotExists) return;
            // TMP appends an invisible caret marker to its rendered label.
            // That marker is not part of the save name.
            _savedName = _text != null ? _text.text.TrimEnd('\u200B') : string.Empty;
            _row.Clicked?.Invoke(_row);
            _editing = true;
            _savedFontStyle = _text.fontStyle;
            if (_row.UnderlineNameWhileEditing) _text.fontStyle |= FontStyles.Underline;
            ApplyEditingStyle();
            _text.overflowMode = TextOverflowModes.Overflow;
            _input.SetTextWithoutNotify(_savedName);
            _input.enabled = true;
            _input.interactable = true;
            _text.raycastTarget = true;
            _input.Select();
            _input.ActivateInputField();
        }

        private void CreateInputContainer()
        {
            // TMP draws selection/caret beside its text, inside the input. Putting
            // both components on the label itself prevents that normal hierarchy.
            // Preserve the authored label rectangle; no layout groups are added.
            var label = _text.rectTransform;
            var field = new GameObject("NameInput", typeof(RectTransform)).GetComponent<RectTransform>();
            field.SetParent(label.parent, false);
            field.SetSiblingIndex(label.GetSiblingIndex());
            field.anchorMin = label.anchorMin;
            field.anchorMax = label.anchorMax;
            field.pivot = label.pivot;
            field.sizeDelta = label.sizeDelta;
            field.anchoredPosition3D = label.anchoredPosition3D;
            field.localRotation = label.localRotation;
            field.localScale = label.localScale;
            label.SetParent(field, false);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            label.localRotation = Quaternion.identity;
            label.localScale = Vector3.one;
            // The editable label may scroll horizontally, but never over the
            // row icon, pencil or date. Negative authored margins escaped it.
            field.gameObject.AddComponent<RectMask2D>();
            var margin = _text.margin;
            _text.margin = new Vector4(Mathf.Max(0, margin.x), Mathf.Max(0, margin.y), Mathf.Max(0, margin.z), Mathf.Max(0, margin.w));
            _text.richText = false;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _input = field.gameObject.AddComponent<TMP_InputField>();
            _input.enabled = false;
        }

        private void ApplyEditingStyle()
        {
            _input.selectionColor = _row.NameSelectionColor;
            _input.customCaretColor = true;
            _input.caretColor = _row.NameCaretColor;
            _input.caretWidth = _row.NameCaretWidth;
        }

        private void OnEndEdit(string value)
        {
            if (!_editing) return;
            string normalized = SaveService.NormalizeSaveName(value);
            bool saved = !_input.wasCanceled && _row != null && _row.SlotExists && _save != null &&
                _save.RenameSlot(_row.SlotId, normalized);
            FinishEditing(saved ? normalized : _savedName);
        }

        private void FinishEditing(string name)
        {
            _editing = false; // Deactivation may raise onEndEdit again.
            _savedName = name;
            _text.fontStyle = _savedFontStyle;
            _input.DeactivateInputField(true);
            ShowDisplayName(name);
        }

        private void ShowDisplayName(string name)
        {
            // TMP moves its text RectTransform to keep the caret visible. Just
            // restoring text leaves that scroll offset behind after Escape.
            // Disable the editor while showing the static label, so deferred
            // caret/mesh rebuilds cannot move it again after cancellation.
            _input.enabled = false;
            _input.interactable = false;
            _input.SetTextWithoutNotify(name);
            var label = _text.rectTransform;
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            _text.overflowMode = TextOverflowModes.Ellipsis;
            _text.text = name;
            _text.raycastTarget = false;
        }

        private void OnDisable()
        {
            if (_editing && _input != null) FinishEditing(_savedName);
        }

        private void OnDestroy()
        {
            if (_pencil != null) _pencil.onClick.RemoveListener(BeginEditing);
            if (_input != null) _input.onEndEdit.RemoveListener(OnEndEdit);
        }
    }
}
