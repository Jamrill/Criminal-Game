using UnityEngine;

namespace JuegoCriminal.Interaction
{
    public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string openBoolName = "IsOpen";

        [Header("Prompt")]
        [SerializeField] private string openText = "Open";
        [SerializeField] private string closeText = "Close";

        private bool _isOpen;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        public bool CanInteract()
        {
            return animator != null;
        }

        public void Interact()
        {
            if (animator == null) return;

            _isOpen = !_isOpen;

            animator.SetBool(openBoolName, _isOpen);
        }

        public string GetInteractionText()
        {
            return _isOpen ? closeText : openText;
        }
    }
}