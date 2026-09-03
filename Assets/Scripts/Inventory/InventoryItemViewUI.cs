using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuegoCriminal.Inventory
{
    public sealed class InventoryItemViewUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private InventoryMenuUI _menu;
        private InventoryPlacement _placement;
        private RectTransform _rect;
        private Canvas _canvas;

        public void Initialize(InventoryMenuUI menu, InventoryPlacement placement)
        {
            _menu = menu;
            _placement = placement;
            _rect = transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData) => transform.SetAsLastSibling();

        public void OnDrag(PointerEventData eventData)
        {
            if (_rect != null)
                _rect.anchoredPosition += eventData.delta / Mathf.Max(0.01f, _canvas.scaleFactor);
        }

        public void OnEndDrag(PointerEventData eventData) =>
            _menu.TryDropAtLocalPosition(_placement, _rect.anchoredPosition, _placement.rotated);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                _menu.TryRotate(_placement);
        }
    }
}
