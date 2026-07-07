using System.Collections;
using UnityEngine;

namespace JuegoCriminal.Interaction
{
    public sealed class DoorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InteractableObject interactableObject;

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

        private void Reset()
        {
            interactableObject = GetComponent<InteractableObject>();
            doorAnimator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (interactableObject == null)
                interactableObject = GetComponent<InteractableObject>();

            if (doorAnimator == null)
                doorAnimator = GetComponent<Animator>();

            RefreshPromptText();
        }

        public void ToggleDoor()
        {
            if (!CanUseDoor())
                return;

            if (canClose)
                _isOpen = !_isOpen;
            else
                _isOpen = true;

            if (doorAnimator != null)
                doorAnimator.SetBool(openBoolName, _isOpen);

            PlayExtraAnimators();

            if (_movementRoutine != null)
                StopCoroutine(_movementRoutine);

            _movementRoutine = StartCoroutine(MovementCooldown());

            if (debugLogs)
                Debug.Log($"DoorController: {name} -> {openBoolName} = {_isOpen}", this);
        }

        public void OpenDoor()
        {
            if (!CanUseDoor())
                return;

            _isOpen = true;

            if (doorAnimator != null)
                doorAnimator.SetBool(openBoolName, true);

            PlayExtraAnimators();

            if (_movementRoutine != null)
                StopCoroutine(_movementRoutine);

            _movementRoutine = StartCoroutine(MovementCooldown());

            if (debugLogs)
                Debug.Log($"DoorController: {name} opened.", this);
        }

        public void CloseDoor()
        {
            if (!CanUseDoor())
                return;

            if (!canClose)
                return;

            _isOpen = false;

            if (doorAnimator != null)
                doorAnimator.SetBool(openBoolName, false);

            PlayExtraAnimators();

            if (_movementRoutine != null)
                StopCoroutine(_movementRoutine);

            _movementRoutine = StartCoroutine(MovementCooldown());

            if (debugLogs)
                Debug.Log($"DoorController: {name} closed.", this);
        }

        private bool CanUseDoor()
        {
            if (doorAnimator == null)
                return false;

            if (_isMoving)
                return false;

            if (!canClose && _isOpen)
                return false;

            return true;
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

            if (interactableObject != null)
            {
                interactableObject.SetCanInteract(false);
                interactableObject.SetInteractionText(movingText);
            }

            yield return new WaitForSeconds(movementLockTime);

            _isMoving = false;
            _movementRoutine = null;

            if (interactableObject != null)
            {
                interactableObject.SetCanInteract(CanUseDoor());
                RefreshPromptText();
            }
        }

        private void RefreshPromptText()
        {
            if (interactableObject == null)
                return;

            if (_isMoving)
            {
                interactableObject.SetInteractionText(movingText);
                return;
            }

            if (canClose)
            {
                interactableObject.SetInteractionText(_isOpen ? closeText : openText);
            }
            else
            {
                interactableObject.SetInteractionText(_isOpen ? string.Empty : openText);
                interactableObject.SetCanShowPrompt(!_isOpen);
            }
        }
    }
}