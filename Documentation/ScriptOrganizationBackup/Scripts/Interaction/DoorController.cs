using System.Collections;
using UnityEngine;

namespace JuegoCriminal.Interaction
{
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField] private InteractableObject interactableObject;
        [Header("Code-driven hinges (local axes)")]
        [SerializeField] private Transform doorPivot;
        [SerializeField] private Vector3 doorAxis = Vector3.up;
        [SerializeField] private float openAngle = 95f;
        [SerializeField] private Transform knobPivot;
        [SerializeField] private Vector3 knobAxis = Vector3.forward;
        [SerializeField] private float knobAngle = -25f;
        [SerializeField, Min(0f)] private float knobDuration = 0.3f;
        [Header("Prompt Text")]
        [SerializeField] private string openText = "Open";
        [SerializeField] private string closeText = "Close";
        [SerializeField] private string movingText = "Moving...";
        [Header("Behavior")]
        [SerializeField] private bool canClose = true;
        [SerializeField, Min(0.05f)] private float movementLockTime = 0.85f;
        [SerializeField] private bool debugLogs;

        private Quaternion closedDoor;
        private Quaternion closedKnob;
        private bool isOpen;
        private bool isMoving;
        private bool initialized;
        private Coroutine movement;
        private float openingDirection = 1f;

        private void Awake()
        {
            if (interactableObject == null) interactableObject = GetComponent<InteractableObject>();
            if (doorPivot != null) closedDoor = doorPivot.localRotation;
            if (knobPivot != null) closedKnob = knobPivot.localRotation;
            initialized = true;
            RefreshPrompt();
        }

        private Quaternion TargetRotation => closedDoor * Quaternion.AngleAxis(
            isOpen ? openAngle * openingDirection : 0f, doorAxis.sqrMagnitude > 0.001f ? doorAxis.normalized : Vector3.up);

        public void ToggleDoor() => SetOpen(!isOpen);
        public void OpenDoor() => SetOpen(true);
        public void CloseDoor() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (!isActiveAndEnabled || doorPivot == null || isMoving || open == isOpen || (!open && !canClose)) return;
            if (open) openingDirection = ChooseOpeningDirection();
            isOpen = open;
            isMoving = true;
            RefreshPrompt();
            movement = StartCoroutine(Animate());
            if (debugLogs) Debug.Log($"DoorController: {name} -> open={isOpen}", this);
        }

        private float ChooseOpeningDirection()
        {
            Transform actor = interactableObject != null ? interactableObject.CurrentInteractor : null;
            if (actor == null) return 1f;

            // The positive-angle tangent at the leaf centre tells us which side
            // the leaf would move towards, independently of the building rotation.
            BoxCollider leaf = doorPivot.GetComponent<BoxCollider>();
            Vector3 centre = leaf != null ? doorPivot.TransformPoint(leaf.center)
                : knobPivot != null ? knobPivot.position : doorPivot.position + doorPivot.right;
            Vector3 axis = doorPivot.TransformDirection(
                doorAxis.sqrMagnitude > 0.001f ? doorAxis.normalized : Vector3.up);
            Vector3 tangent = Vector3.Cross(axis, centre - doorPivot.position) * Mathf.Sign(openAngle);
            return Vector3.Dot(tangent, actor.position - centre) > 0f ? -1f : 1f;
        }

        private IEnumerator Animate()
        {
            Quaternion start = doorPivot.localRotation;
            Quaternion target = TargetRotation;
            float duration = Mathf.Max(0.05f, movementLockTime);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                doorPivot.localRotation = Quaternion.Slerp(start, target, t * t * (3f - 2f * t));
                if (knobPivot != null)
                {
                    float k = knobDuration > 0f ? Mathf.Clamp01(elapsed / knobDuration) : 1f;
                    Vector3 axis = knobAxis.sqrMagnitude > 0.001f ? knobAxis.normalized : Vector3.forward;
                    knobPivot.localRotation = closedKnob * Quaternion.AngleAxis(Mathf.Sin(k * Mathf.PI) * knobAngle, axis);
                }
                yield return null;
            }
            FinishMovement();
        }

        private void OnDisable()
        {
            if (!initialized) return;
            if (movement != null) StopCoroutine(movement);
            FinishMovement();
        }

        private void FinishMovement()
        {
            if (doorPivot != null) doorPivot.localRotation = TargetRotation;
            if (knobPivot != null) knobPivot.localRotation = closedKnob;
            movement = null;
            isMoving = false;
            RefreshPrompt();
        }

        private void RefreshPrompt()
        {
            if (interactableObject == null) return;
            interactableObject.SetCanInteract(doorPivot != null && !isMoving && (canClose || !isOpen));
            interactableObject.SetCanShowPrompt(canClose || !isOpen || isMoving);
            interactableObject.SetInteractionText(isMoving ? movingText : isOpen ? closeText : openText);
        }
    }
}
