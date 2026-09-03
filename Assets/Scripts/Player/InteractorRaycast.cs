using System.Collections.Generic;
using JuegoCriminal.CameraSystem;
using JuegoCriminal.Interaction;
using JuegoCriminal.UI;
using JuegoCriminal.Core;
using UnityEngine;

namespace JuegoCriminal.Player
{
    public sealed class InteractorRaycast : MonoBehaviour
    {
        [Header("First Person Raycast")]
        [SerializeField] private float firstPersonDistance = 5f;

        [Header("Third Person Radius")]
        [SerializeField] private float thirdPersonRadius = 3f;
        [SerializeField] private LayerMask interactMask;

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        private CameraBoomCollision _cameraBoom;
        private Camera _cam;

        private InteractableObject _current;

        private WorldPromptUI _prompt;
        private WorldPromptUI _currentPromptPrefab;

        private readonly List<InteractableObject> _targetsInRange = new();
        private readonly RaycastHit[] _firstPersonHits = new RaycastHit[16];
        private int _targetIndex;

        private void Awake()
        {
            _cameraBoom = FindAnyObjectByType<CameraBoomCollision>();
            _cam = Camera.main;
        }

        private void Update()
        {
            if (Time.timeScale == 0f || Cursor.lockState != CursorLockMode.Locked)
            {
                HidePrompt();
                return;
            }

            RefreshReferences();

            if (_cam == null)
            {
                HidePrompt();
                return;
            }

            bool isFirstPerson = _cameraBoom != null && _cameraBoom.IsFirstPerson;

            if (isFirstPerson)
                UpdateFirstPersonInteraction();
            else
                UpdateThirdPersonInteraction();

            if (_current == null || !_current.CanShowPrompt())
            {
                HidePrompt();
                return;
            }

            ShowPromptForCurrent();

            if (GameInput.InteractPressed && _current.CanInteract())
            {
                _current.Interact();
                RefreshPromptVisuals();
            }
        }

        private void RefreshReferences()
        {
            if (_cameraBoom == null)
                _cameraBoom = FindAnyObjectByType<CameraBoomCollision>();

            if (_cam == null)
                _cam = Camera.main;
        }

        private void UpdateFirstPersonInteraction()
        {
            _targetsInRange.Clear();
            _targetIndex = 0;

            _current = GetLookAtInteractable();
        }

        private InteractableObject GetLookAtInteractable()
        {
            Ray ray = _cam.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f)
            );

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _firstPersonHits,
                firstPersonDistance,
                interactMask,
                QueryTriggerInteraction.Ignore);

            InteractableObject closestAvailable = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _firstPersonHits[i];
                InteractableObject interactable =
                    hit.collider.GetComponentInParent<InteractableObject>();

                if (interactable == null || !interactable.CanShowPrompt())
                    continue;

                if (hit.distance >= closestDistance)
                    continue;

                closestAvailable = interactable;
                closestDistance = hit.distance;
            }

            return closestAvailable;
        }

        private void UpdateThirdPersonInteraction()
        {
            InteractableObject previousTarget = _current;

            RefreshTargetsInRadius();

            if (_targetsInRange.Count == 0)
            {
                _current = null;
                _targetIndex = 0;
                return;
            }

            if (previousTarget != null)
            {
                int previousIndex = _targetsInRange.IndexOf(previousTarget);

                if (previousIndex >= 0)
                    _targetIndex = previousIndex;
            }

            if (_targetIndex >= _targetsInRange.Count)
                _targetIndex = 0;

            if (GameInput.SwitchTargetPressed)
            {
                _targetIndex++;

                if (_targetIndex >= _targetsInRange.Count)
                    _targetIndex = 0;
            }

            _current = _targetsInRange[_targetIndex];
        }

        private void RefreshTargetsInRadius()
        {
            _targetsInRange.Clear();

            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                thirdPersonRadius,
                interactMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hits.Length; i++)
            {
                InteractableObject interactable = hits[i].GetComponentInParent<InteractableObject>();

                if (interactable == null)
                    continue;

                if (!interactable.CanShowPrompt())
                    continue;

                if (_targetsInRange.Contains(interactable))
                    continue;

                _targetsInRange.Add(interactable);
            }

            _targetsInRange.Sort(CompareTargets);
        }

        private int CompareTargets(InteractableObject a, InteractableObject b)
        {
            float scoreA = GetTargetScore(a != null ? a.transform : null);
            float scoreB = GetTargetScore(b != null ? b.transform : null);

            return scoreA.CompareTo(scoreB);
        }

        private float GetTargetScore(Transform target)
        {
            if (target == null)
                return float.MaxValue;

            Vector3 toTarget = target.position - transform.position;
            float distance = toTarget.magnitude;

            if (distance <= 0.001f)
                return 0f;

            Vector3 direction = toTarget.normalized;
            float forwardDot = Vector3.Dot(transform.forward, direction);

            float behindPenalty = forwardDot < 0f ? 2.5f : 0f;

            return distance + behindPenalty;
        }

        private void ShowPromptForCurrent()
        {
            if (_current == null)
                return;

            WorldPromptUI promptPrefab = _current.GetPromptPrefab();

            if (promptPrefab == null)
            {
                HidePrompt();

                if (debugLogs)
                    Debug.LogWarning($"'{_current.name}' no tiene Prompt Prefab asignado.", _current);

                return;
            }

            EnsurePromptInstance(promptPrefab);

            if (_prompt == null)
                return;

            Transform anchor = _current.GetClosestPromptAnchor(transform);
            bool hasManualAnchor = _current.HasPromptAnchor();

            _prompt.gameObject.SetActive(true);
            _prompt.Attach(anchor, _cam, hasManualAnchor);

            RefreshPromptVisuals();
        }

        private void RefreshPromptVisuals()
        {
            if (_prompt == null || _current == null)
                return;

            _prompt.SetText(_current.GetInteractionText());
            _prompt.SetInteractableVisual(_current.CanInteract());
        }

        private void EnsurePromptInstance(WorldPromptUI promptPrefab)
        {
            if (_prompt != null && _currentPromptPrefab == promptPrefab)
                return;

            if (_prompt != null)
                Destroy(_prompt.gameObject);

            _currentPromptPrefab = promptPrefab;
            _prompt = Instantiate(promptPrefab);
        }

        private void HidePrompt()
        {
            if (_prompt != null)
                _prompt.gameObject.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, thirdPersonRadius);
        }
    }
}
