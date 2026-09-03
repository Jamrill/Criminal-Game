using System;
using System.Collections.Generic;
using JuegoCriminal.Core;
using JuegoCriminal.Services;
using UnityEngine;

namespace JuegoCriminal.Inventory
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private InventoryItemCatalog catalog;
        [Tooltip("Capacidad temporal/base. La ropa equipada se suma encima.")]
        [SerializeField, Range(0, InventoryGrid.MaxCapacity)] private int baseCapacity = 46;
        [SerializeField] private InventoryItemDefinition[] equippedItems;

        private readonly List<InventoryPlacement> _placements = new();
        private readonly Dictionary<string, InventoryItemDefinition> _sessionDefinitions = new();
        private SaveService _save;

        public InventoryGrid Grid { get; private set; }
        public int Capacity => CalculateCapacity();
        public event Action Changed;

        private void Awake()
        {
            _save = Bootstrapper.Instance != null ? Bootstrapper.Instance.SaveService : FindAnyObjectByType<SaveService>();
            LoadFromSave();
        }

        public InventoryItemDefinition Resolve(string itemId)
        {
            if (_sessionDefinitions.TryGetValue(itemId, out var item)) return item;
            return catalog != null ? catalog.Find(itemId) : null;
        }

        public bool TryAdd(InventoryItemDefinition item)
        {
            if (item == null) return false;
            _sessionDefinitions[item.Id] = item;
            if (!Grid.TryAdd(item, out _)) return false;
            CommitChange();
            return true;
        }

        public bool TryMove(string instanceId, int x, int y, bool rotated)
        {
            if (!Grid.TryMove(instanceId, x, y, rotated)) return false;
            CommitChange();
            return true;
        }

        public bool Remove(string instanceId)
        {
            if (!Grid.Remove(instanceId)) return false;
            CommitChange();
            return true;
        }

        public bool TryEquip(InventoryItemDefinition item)
        {
            if (item == null || item.EquipmentSlot == EquipmentSlot.None) return false;
            var equipped = new List<InventoryItemDefinition>(equippedItems ?? Array.Empty<InventoryItemDefinition>());
            int existingIndex = equipped.FindIndex(e => e != null && e.EquipmentSlot == item.EquipmentSlot);
            if (existingIndex >= 0) equipped[existingIndex] = item; else equipped.Add(item);
            int proposedCapacity = CalculateCapacity(equipped);
            if (!Grid.FitsWithinCapacity(proposedCapacity)) return false;
            equippedItems = equipped.ToArray();
            Grid.SetCapacity(proposedCapacity);
            CommitChange();
            return true;
        }

        public bool TryUnequip(EquipmentSlot slot)
        {
            var equipped = new List<InventoryItemDefinition>(equippedItems ?? Array.Empty<InventoryItemDefinition>());
            if (equipped.RemoveAll(e => e != null && e.EquipmentSlot == slot) == 0) return false;
            int proposedCapacity = CalculateCapacity(equipped);
            if (!Grid.FitsWithinCapacity(proposedCapacity)) return false;
            equippedItems = equipped.ToArray();
            Grid.SetCapacity(proposedCapacity);
            CommitChange();
            return true;
        }

        public void RecalculateCapacity()
        {
            Grid.SetCapacity(Capacity);
            Changed?.Invoke();
        }

        private int CalculateCapacity()
        {
            return CalculateCapacity(equippedItems);
        }

        private int CalculateCapacity(IReadOnlyList<InventoryItemDefinition> equipment)
        {
            int result = baseCapacity;
            if (equipment != null)
                for (int i = 0; i < equipment.Count; i++)
                    if (equipment[i] != null) result += equipment[i].InventoryCapacityBonus;
            return Mathf.Clamp(result, 0, InventoryGrid.MaxCapacity);
        }

        private void LoadFromSave()
        {
            IReadOnlyList<string> equippedIds = _save?.GetEquippedItemIds();
            if (equippedIds != null && equippedIds.Count > 0)
            {
                var loadedEquipment = new List<InventoryItemDefinition>();
                for (int i = 0; i < equippedIds.Count; i++)
                {
                    InventoryItemDefinition item = Resolve(equippedIds[i]);
                    if (item != null && item.EquipmentSlot != EquipmentSlot.None) loadedEquipment.Add(item);
                }
                equippedItems = loadedEquipment.ToArray();
            }

            _placements.Clear();
            IReadOnlyList<InventoryPlacement> saved = _save?.GetInventoryPlacements();
            if (saved != null)
                for (int i = 0; i < saved.Count; i++)
                {
                    InventoryPlacement p = saved[i];
                    if (p != null && Resolve(p.itemId) != null) _placements.Add(new InventoryPlacement
                    {
                        instanceId = string.IsNullOrWhiteSpace(p.instanceId) ? Guid.NewGuid().ToString("N") : p.instanceId,
                        itemId = p.itemId, x = p.x, y = p.y, rotated = p.rotated
                    });
                }
            Grid = new InventoryGrid(Capacity, _placements, Resolve);
        }

        private void CommitChange()
        {
            _save?.UpdateInventoryPlacements(Grid.Placements);
            var equippedIds = new List<string>();
            if (equippedItems != null)
                for (int i = 0; i < equippedItems.Length; i++)
                    if (equippedItems[i] != null) equippedIds.Add(equippedItems[i].Id);
            _save?.UpdateEquippedItemIds(equippedIds);
            Changed?.Invoke();
        }
    }
}
