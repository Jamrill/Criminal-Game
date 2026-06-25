using System.Collections;
using UnityEngine;

namespace JuegoCriminal.Interaction
{
    public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        [Header("Door Animation")]
        [SerializeField] private Animator doorAnimator;
        [SerializeField] private string openBoolName = "IsOpen";

        [Header("Extra Animators")]
        [SerializeField] private Animator[] extraAnimators;
        [SerializeField] private string useTriggerName = "UseKnob";

        [Header("Prompt Text")]
        [SerializeField] private string openText = "Open";
        [SerializeField] private string closeText = "Close";
        [SerializeField] private string movingText = "Moving...";

        [Header("Behavior")]
        [SerializeField] private bool canClose = true;
        [SerializeField] private float movementLockTime = 0.85f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private bool _isOpen;
        private bool _isMoving;
        private Coroutine _movementRoutine;

        private void Awake()
        {
            if (doorAnimator == null)
                doorAnimator = GetComponent<Animator>();
        }

        public bool CanShowPrompt()
        {
            return doorAnimator != null;
        }

        public bool CanInteract()
        {
            if (doorAnimator == null)
                return false;

            if (_isMoving)
                return false;

            if (!canClose && _isOpen)
                return false;

            return true;
        }

        public void Interact()
        {
            if (!CanInteract())
                return;

            if (canClose)
                _isOpen = !_isOpen;
            else
                _isOpen = true;

            doorAnimator.SetBool(openBoolName, _isOpen);

            PlayExtraAnimators();

            if (_movementRoutine != null)
                StopCoroutine(_movementRoutine);

            _movementRoutine = StartCoroutine(MovementCooldown());

            if (debugLogs)
                Debug.Log($"DoorInteractable: {name} -> {openBoolName} = {_isOpen}", this);
        }

        public string GetInteractionText()
        {
            if (_isMoving)
                return movingText;

            if (canClose)
                return _isOpen ? closeText : openText;

            return openText;
        }

        private void PlayExtraAnimators()
        {
            if (extraAnimators == null)
                return;

            for (int i = 0; i < extraAnimators.Length; i++)
            {
                Animator extraAnimator = extraAnimators[i];

                if (extraAnimator == null)
                    continue;

                extraAnimator.ResetTrigger(useTriggerName);
                extraAnimator.SetTrigger(useTriggerName);
            }
        }

        private IEnumerator MovementCooldown()
        {
            _isMoving = true;

            yield return new WaitForSeconds(movementLockTime);

            _isMoving = false;
            _movementRoutine = null;
        }
    }
}