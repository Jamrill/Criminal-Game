using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    public sealed class WorldPromptUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image iconImage;          // tu Image "E"
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);

        private Transform _follow;
        private Camera _cam;

        private void Awake()
        {
            if (iconImage == null)
                iconImage = GetComponentInChildren<Image>(true);
        }

        public void Attach(Transform follow, Camera cam)
        {
            _follow = follow;
            _cam = cam;
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage != null) iconImage.sprite = sprite;
        }

        private void LateUpdate()
        {
            if (_follow == null) return;

            transform.position = _follow.position + worldOffset;

            // Billboard (mirar a cámara)
            if (_cam != null)
                transform.forward = (transform.position - _cam.transform.position).normalized;
        }
    }
}