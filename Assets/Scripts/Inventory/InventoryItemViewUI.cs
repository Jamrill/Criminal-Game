using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuegoCriminal.Inventory
{
    public sealed class InventoryItemViewUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        private InventoryMenuUI _menu;
        private InventoryPlacement _placement;
        private RectTransform _rect;
        private Canvas _canvas;
        private bool _isDragging;
        private bool _dragRotated;

        public void Initialize(InventoryMenuUI menu, InventoryPlacement placement)
        {
            _menu = menu;
            _placement = placement;
            _rect = transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
            _dragRotated = placement.rotated;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_menu == null || _menu.IsInteractionBlocked)
                return;
            _isDragging = true;
            _dragRotated = _placement.rotated;
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_menu != null && !_menu.IsInteractionBlocked && _rect != null)
                _rect.anchoredPosition += eventData.delta / Mathf.Max(0.01f, _canvas.scaleFactor);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_menu == null)
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

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_menu == null || _menu.IsInteractionBlocked ||
                eventData.button != PointerEventData.InputButton.Right || !_menu.CanRotate(_placement))
                return;

            if (_isDragging)
            {
                _dragRotated = !_dragRotated;
                _menu.UpdateDraggedItemVisual(_placement, _rect, _dragRotated);
            }
            else
            {
                _menu.TryRotate(_placement);
            }
        }
    }
}
