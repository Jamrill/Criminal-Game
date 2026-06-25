using System.Collections.Generic;
using UnityEngine;
using JuegoCriminal.UI;
using JuegoCriminal.CameraSystem;
using JuegoCriminal.Interaction;

namespace JuegoCriminal.Player
{
    public sealed class InteractorRaycast : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private KeyCode switchTargetKey = KeyCode.Tab;

        [Header("First Person Raycast")]
        [SerializeField] private float firstPersonDistance = 5f;

        [Header("Third Person Radius")]
        [SerializeField] private float thirdPersonRadius = 3f;
        [SerializeField] private LayerMask interactMask;

        [Header("World Prompt Prefab")]
        [SerializeField] private WorldPromptUI promptPrefab;

        private CameraBoomCollision _cameraBoom;
        private Camera _cam;

        private IInteractable _current;
        private Transform _currentTransform;

        private WorldPromptUI _prompt;

        private readonly List<IInteractable> _targetsInRange = new();
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

            ShowPromptOver(_currentTransform);

            if (_prompt != null)
            {
                _prompt.SetText(_current.GetInteractionText());
                _prompt.SetInteractableVisual(_current.CanInteract());
            }

            if (Input.GetKeyDown(interactKey) && _current.CanInteract())
            {
                _current.Interact();

                if (_current != null && _current.CanShowPrompt() && _prompt != null)
                {
                    _prompt.SetText(_current.GetInteractionText());
                    _prompt.SetInteractableVisual(_current.CanInteract());
                }
                else
                {
                    HidePrompt();
                }
            }

            ShowPromptOver(_currentTransform);

            if (_prompt != null)
                _prompt.SetText(_current.GetInteractionText());

            if (Input.GetKeyDown(interactKey))
                _current.Interact();
        }

        private void RefreshReferences()
        {
            if (_cameraBoom == null)
                _cameraBoom = FindAnyObjectByType<CameraBoomCollision>();

            if (_cam == null)
                _cam = Camera.main;
        }

        // -------------------------
        // First person
        // -------------------------

        private void UpdateFirstPersonInteraction()
        {
            _targetsInRange.Clear();
            _targetIndex = 0;

            _current = GetLookAtInteractable(out _currentTransform);
        }

        private IInteractable GetLookAtInteractable(out Transform interactableTransform)
        {
            interactableTransform = null;

            if (_cam == null) return null;

            Ray ray = _cam.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f)
            );

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    firstPersonDistance,
                    interactMask,
                    QueryTriggerInteraction.Ignore))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

                if (interactable != null && interactable.CanShowPrompt())
                {
                    interactableTransform = GetTransformFromInteractable(interactable);
                    return interactable;
                }
            }

            return null;
        }

        // -------------------------
        // Third person
        // -------------------------

        private void UpdateThirdPersonInteraction()
        {
            IInteractable previousTarget = _current;

            RefreshTargetsInRadius();

            if (_targetsInRange.Count == 0)
            {
                _current = null;
                _currentTransform = null;
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

            if (Input.GetKeyDown(switchTargetKey))
            {
                _targetIndex++;

                if (_targetIndex >= _targetsInRange.Count)
                    _targetIndex = 0;
            }

            _current = _targetsInRange[_targetIndex];
            _currentTransform = GetTransformFromInteractable(_current);
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
                IInteractable interactable = hits[i].GetComponentInParent<IInteractable>();

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

        private int CompareTargets(IInteractable a, IInteractable b)
        {
            Transform ta = GetTransformFromInteractable(a);
            Transform tb = GetTransformFromInteractable(b);

            float scoreA = GetTargetScore(ta);
            float scoreB = GetTargetScore(tb);

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

        // -------------------------
        // Prompt
        // -------------------------

        private void ShowPromptOver(Transform target)
        {
            if (target == null) return;
            if (promptPrefab == null) return;

            if (_prompt == null)
                _prompt = Instantiate(promptPrefab);

            Transform anchor = GetPromptAnchor(target, out bool useAnchorTransform);

            _prompt.gameObject.SetActive(true);
            _prompt.Attach(anchor, _cam, useAnchorTransform);
        }

        private Transform GetPromptAnchor(Transform target, out bool useAnchorTransform)
        {
            useAnchorTransform = false;

            if (target == null)
                return transform;

            Transform bestAnchor = null;
            float bestDistance = float.MaxValue;

            // Busca todos los hijos cuyo nombre empiece por "PromptAnchor"
            // Ejemplos válidos:
            // PromptAnchor
            // PromptAnchor_2
            // PromptAnchor_Left
            // PromptAnchor_Right
            Transform[] children = target.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];

                if (!child.name.StartsWith("PromptAnchor"))
                    continue;

                float distance = Vector3.Distance(transform.position, child.position);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestAnchor = child;
                }
            }

            if (bestAnchor != null)
            {
                useAnchorTransform = true;
                return bestAnchor;
            }

            // Si no hay ningún PromptAnchor, comportamiento por defecto.
            useAnchorTransform = false;
            return target;
        }

        private void HidePrompt()
        {
            if (_prompt != null)
                _prompt.gameObject.SetActive(false);
        }

        private Transform GetTransformFromInteractable(IInteractable interactable)
        {
            if (interactable is Component component)
                return component.transform;

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, thirdPersonRadius);
        }
    }
}