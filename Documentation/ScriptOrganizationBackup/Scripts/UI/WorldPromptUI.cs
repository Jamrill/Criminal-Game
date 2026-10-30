using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    public sealed class WorldPromptUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image secondIconImage;
        [SerializeField] private TMP_Text promptText;

        [Header("Follow")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color disabledColor = Color.gray;

        private Transform _follow;
        private Camera _cam;
        private bool _useAnchorTransform;
        private bool _applyWorldOffset = true;

        private void Awake()
        {
            if (iconImage == null)
                //iconImage = GetComponentInChildren<Image>(true);
                iconImage = transform.Find("Bounds Letter").GetComponent<Image>();

            if (secondIconImage == null)
                secondIconImage = transform.Find("Bounds Letter/Letter").GetComponent<Image>();

            if (promptText == null)
                promptText = GetComponentInChildren<TMP_Text>(true);
        }

        public void Attach(Transform follow, Camera cam)
        {
            Attach(follow, cam, false);
        }

        public void Attach(Transform follow, Camera cam, bool useAnchorTransform)
        {
            _follow = follow;
            _cam = cam;
            _useAnchorTransform = useAnchorTransform;
            _applyWorldOffset = !useAnchorTransform;
        }

        public void AttachDirectBillboard(Transform follow, Camera cam)
        {
            _follow = follow;
            _cam = cam;
            _useAnchorTransform = false;
            _applyWorldOffset = false;
        }

        public void SetIcon(Sprite primarysprite, Sprite SecondarySprite)
        {
            if (iconImage == null)
                return;
            if (secondIconImage == null)
                return ;

            iconImage.sprite = primarysprite;
            iconImage.gameObject.SetActive(primarysprite != null);

            secondIconImage.sprite = SecondarySprite;
            secondIconImage.gameObject.SetActive(SecondarySprite != null);
        }

        public void SetText(string text)
        {
            if (promptText == null)
                return;

            promptText.text = text;
            promptText.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        public void SetInteractableVisual(bool canInteract)
        {
            Color targetColor = canInteract ? normalColor : disabledColor;

            if (iconImage != null)
                iconImage.color = targetColor;

            if (secondIconImage != null)
                secondIconImage.color = targetColor;

            if (promptText != null)
                promptText.color = targetColor;
        }

        private void LateUpdate()
        {
            if (_follow == null)
                return;

            if (_useAnchorTransform)
            {
                transform.position = _follow.position;
                transform.rotation = _follow.rotation;
                return;
            }

            transform.position = _follow.position + (_applyWorldOffset ? worldOffset : Vector3.zero);

            if (_cam != null)
                transform.forward = (transform.position - _cam.transform.position).normalized;
        }
    }
}
