using JuegoCriminal.UI;
using UnityEngine;
using UnityEngine.Events;

namespace JuegoCriminal.Interaction
{
    public sealed class InteractableObject : MonoBehaviour
    {
        private const string InteractableLayerName = "Interactable";
        private const string PromptAnchorPrefix = "PromptAnchor";

        [Header("Layer")]
        [SerializeField] private bool forceInteractableLayer = true;
        [SerializeField] private bool applyLayerToChildren = true;

        [Header("Input")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Header("Prompt")]
        [SerializeField] private string interactionText = "Interact";
        [SerializeField] private bool canShowPrompt = true;

        [Tooltip("Prefab visual del prompt para este objeto. El prefab ya debe traer sus iconos configurados.")]
        [SerializeField] private WorldPromptUI promptPrefab;

        [Tooltip("Anchors manuales. Si está vacío, se buscan hijos cuyo nombre empiece por PromptAnchor.")]
        [SerializeField] private Transform[] promptAnchors;

        [Header("Fallback Prompt Anchor")]
        [Tooltip("Si no hay PromptAnchor, crea uno automáticamente encima del objeto.")]
        [SerializeField] private bool createFallbackAnchorAboveObject = true;

        [Tooltip("Altura extra sobre el punto superior del objeto.")]
        [SerializeField] private float fallbackExtraHeight = 0.10f;

        [Header("Interaction")]
        [SerializeField] private bool canInteract = true;
        [SerializeField] private UnityEvent onInteract;

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        private Transform _fallbackPromptAnchor;

        public KeyCode GetInteractionKey()
        {
            return interactionKey;
        }

        private void Reset()
        {
            EnsureInteractableLayer();
            AutoFindPromptAnchors();
        }

        private void Awake()
        {
            EnsureInteractableLayer();

            if (promptAnchors == null || promptAnchors.Length == 0)
                AutoFindPromptAnchors();

            if ((promptAnchors == null || promptAnchors.Length == 0) && createFallbackAnchorAboveObject)
                CreateOrUpdateFallbackAnchor();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                EnsureInteractableLayer();
        }
#endif

        public bool CanShowPrompt()
        {
            return canShowPrompt && promptPrefab != null;
        }

        public bool CanInteract()
        {
            return canInteract;
        }

        public string GetInteractionText()
        {
            return interactionText;
        }

        public WorldPromptUI GetPromptPrefab()
        {
            return promptPrefab;
        }

        public void Interact()
        {
            if (!CanInteract())
                return;

            if (debugLogs)
                Debug.Log($"InteractableObject: {name} interacted.", this);

            onInteract?.Invoke();
        }

        public Transform GetClosestPromptAnchor(Transform interactorTransform)
        {
            if (promptAnchors == null || promptAnchors.Length == 0)
            {
                if (createFallbackAnchorAboveObject)
                    return CreateOrUpdateFallbackAnchor();

                return transform;
            }

            Transform bestAnchor = null;
            float bestDistance = float.MaxValue;

            Vector3 interactorPosition = interactorTransform != null
                ? interactorTransform.position
                : transform.position;

            for (int i = 0; i < promptAnchors.Length; i++)
            {
                Transform anchor = promptAnchors[i];

                if (anchor == null)
                    continue;

                float distance = Vector3.Distance(interactorPosition, anchor.position);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestAnchor = anchor;
                }
            }

            if (bestAnchor != null)
                return bestAnchor;

            if (createFallbackAnchorAboveObject)
                return CreateOrUpdateFallbackAnchor();

            return transform;
        }

        public bool HasPromptAnchor()
        {
            return promptAnchors != null && promptAnchors.Length > 0;
        }

        public void SetInteractionText(string newText)
        {
            interactionText = newText;
        }

        public void SetCanInteract(bool value)
        {
            canInteract = value;
        }

        public void SetCanShowPrompt(bool value)
        {
            canShowPrompt = value;
        }

        private Transform CreateOrUpdateFallbackAnchor()
        {
            if (_fallbackPromptAnchor == null)
            {
                GameObject anchorObject = new GameObject("PromptAnchor_Auto");
                anchorObject.transform.SetParent(transform, true);
                _fallbackPromptAnchor = anchorObject.transform;
            }

            Bounds bounds = CalculateWorldBounds();

            Vector3 anchorPosition = bounds.center;
            anchorPosition.y = bounds.max.y + fallbackExtraHeight;

            _fallbackPromptAnchor.position = anchorPosition;
            _fallbackPromptAnchor.rotation = transform.rotation;

            return _fallbackPromptAnchor;
        }

        private Bounds CalculateWorldBounds()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            bool hasBounds = false;
            Bounds bounds = new Bounds(transform.position, Vector3.zero);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];

                if (col == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            if (hasBounds)
                return bounds;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];

                if (rend == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = rend.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rend.bounds);
                }
            }

            if (hasBounds)
                return bounds;

            return new Bounds(transform.position, Vector3.one);
        }

        private void EnsureInteractableLayer()
        {
            if (!forceInteractableLayer)
                return;

            int interactableLayer = LayerMask.NameToLayer(InteractableLayerName);

            if (interactableLayer == -1)
            {
                Debug.LogWarning(
                    $"InteractableObject: no existe la layer '{InteractableLayerName}'. Créala en Project Settings > Tags and Layers.",
                    this
                );

                return;
            }

            if (applyLayerToChildren)
                SetLayerRecursively(transform, interactableLayer);
            else
                gameObject.layer = interactableLayer;
        }

        private void SetLayerRecursively(Transform target, int layer)
        {
            target.gameObject.layer = layer;

            for (int i = 0; i < target.childCount; i++)
                SetLayerRecursively(target.GetChild(i), layer);
        }

        private void AutoFindPromptAnchors()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);

            int count = 0;

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];

                if (child == transform)
                    continue;

                if (child.name.StartsWith(PromptAnchorPrefix))
                    count++;
            }

            if (count == 0)
            {
                promptAnchors = null;
                return;
            }

            promptAnchors = new Transform[count];

            int index = 0;

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];

                if (child == transform)
                    continue;

                if (!child.name.StartsWith(PromptAnchorPrefix))
                    continue;

                promptAnchors[index] = child;
                index++;
            }
        }
    }
}