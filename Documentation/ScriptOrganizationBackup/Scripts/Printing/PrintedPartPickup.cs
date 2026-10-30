using System;
using JuegoCriminal.Interaction;
using JuegoCriminal.UI;
using JuegoCriminal.Inventory;
using UnityEngine;

namespace JuegoCriminal.Printing
{
    /// <summary>Interacción temporal hasta que exista el inventario real.</summary>
    public sealed class PrintedPartPickup : MonoBehaviour
    {
        private Action _onPickedUp;
        private InventoryItemDefinition _item;
        private bool _pickedUp;

        public void Initialize(
            InventoryItemDefinition item,
            WorldPromptUI promptPrefab,
            string promptText,
            float promptExtraHeight,
            Action onPickedUp)
        {
            _item = item;
            _onPickedUp = onPickedUp;
            EnsureCollider();

            InteractableObject interactable = GetComponent<InteractableObject>();
            if (interactable == null)
                interactable = gameObject.AddComponent<InteractableObject>();

            interactable.ConfigureRuntime(promptText, promptPrefab, PickUp);
            interactable.UseOwnFallbackPromptAnchor(promptExtraHeight);
        }

        private void PickUp()
        {
            if (_pickedUp) return;

            PlayerInventory inventory = FindAnyObjectByType<PlayerInventory>();
            if (inventory == null || !inventory.TryAdd(_item))
            {
                Debug.LogWarning("[Inventory] No hay espacio para recoger el objeto.", this);
                return;
            }

            _pickedUp = true;
            _onPickedUp?.Invoke();
            Destroy(gameObject);
        }

        private void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>(true) != null) return;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(transform.position, Vector3.one * 0.25f);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (!hasBounds) { bounds = renderers[i].bounds; hasBounds = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }

            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.center = transform.InverseTransformPoint(bounds.center);
            Vector3 scale = transform.lossyScale;
            collider.size = new Vector3(
                bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }
    }
}
