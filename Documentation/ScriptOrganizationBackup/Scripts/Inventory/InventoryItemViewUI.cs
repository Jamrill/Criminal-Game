using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using JuegoCriminal.Core;

namespace JuegoCriminal.Inventory
{
    public sealed class InventoryItemViewUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private InventoryMenuUI _menu;
        private InventoryPlacement _placement;
        private RectTransform _rect;
        private Canvas _canvas;
        private bool _isDragging;
        private int _dragRotated;

        public void Initialize(InventoryMenuUI menu, InventoryPlacement placement)
        {
            _menu = menu;
            _placement = placement;
            _rect = transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
            _dragRotated = placement.Rotation;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left ||
                _menu == null || _menu.IsInteractionBlocked)
                return;
            _isDragging = true;
            _dragRotated = _placement.Rotation;
            transform.SetAsLastSibling();
        }

        private void Update()
        {
            if (_isDragging && _menu != null && !_menu.IsInteractionBlocked &&
                GameInput.RotateInventoryPressed && _menu.CanRotate(_placement))
            {
                _dragRotated = (_dragRotated + 1) % 4;
                _menu.UpdateDraggedItemVisual(_placement, _rect, _dragRotated);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isDragging && eventData.button == PointerEventData.InputButton.Left &&
                _menu != null && !_menu.IsInteractionBlocked && _rect != null)
                _rect.anchoredPosition += eventData.delta / Mathf.Max(0.01f, _canvas.scaleFactor);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging || eventData.button != PointerEventData.InputButton.Left || _menu == null)
                return;
            if (_menu.IsInteractionBlocked)
            {
                _isDragging = false;
                _menu.Refresh();
                return;
            }
            _isDragging = false;
            _menu.TryDropAtLocalPosition(_placement, _rect.anchoredPosition, _dragRotated);
        }

    }
}
