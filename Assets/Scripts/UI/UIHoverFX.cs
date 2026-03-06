using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    public sealed class UIHoverFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform target; // normalmente el propio SlotRow
        [SerializeField] private Image image;          // la Image del sprite
        [SerializeField] private float hoverScale = 0.98f;
        [SerializeField] private float pressedScale = 0.96f;
        [SerializeField] private float speed = 14f;
        [SerializeField] private float brighten = 1.12f;

        private Vector3 _baseScale;
        private Vector3 _wantedScale;

        private Color _baseColor;
        private Color _wantedColor;

        private void Awake()
        {
            if (target == null) target = (RectTransform)transform;
            if (image == null) image = GetComponent<Image>();

            _baseScale = target.localScale;
            _wantedScale = _baseScale;

            if (image != null)
            {
                _baseColor = image.color;
                _wantedColor = _baseColor;
            }
        }

        private void Update()
        {
            if (target != null)
                target.localScale = Vector3.Lerp(target.localScale, _wantedScale, speed * Time.unscaledDeltaTime);

            if (image != null)
                image.color = Color.Lerp(image.color, _wantedColor, speed * Time.unscaledDeltaTime);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _wantedScale = _baseScale * hoverScale;

            if (image != null)
                _wantedColor = new Color(
                    Mathf.Clamp01(_baseColor.r * brighten),
                    Mathf.Clamp01(_baseColor.g * brighten),
                    Mathf.Clamp01(_baseColor.b * brighten),
                    _baseColor.a
                );
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _wantedScale = _baseScale;
            if (image != null) _wantedColor = _baseColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _wantedScale = _baseScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // vuelve a hover si sigue encima, si no, volverá por exit
            _wantedScale = _baseScale * hoverScale;
        }
    }
}