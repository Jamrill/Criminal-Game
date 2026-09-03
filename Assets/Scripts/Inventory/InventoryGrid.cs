using System;
using System.Collections.Generic;

namespace JuegoCriminal.Inventory
{
    [Serializable]
    public sealed class InventoryPlacement
    {
        public string instanceId;
        public string itemId;
        public int x;
        public int y;
        public bool rotated;
    }

    public sealed class InventoryGrid
    {
        public const int Width = 10;
        public const int MaxRows = 8;
        public const int MaxCapacity = Width * MaxRows;

        private readonly List<InventoryPlacement> _placements;
        private readonly Func<string, InventoryItemDefinition> _resolve;
        public int Capacity { get; private set; }
        public IReadOnlyList<InventoryPlacement> Placements => _placements;

        public InventoryGrid(int capacity, List<InventoryPlacement> placements, Func<string, InventoryItemDefinition> resolve)
        {
            Capacity = Math.Clamp(capacity, 0, MaxCapacity);
            _placements = placements ?? new List<InventoryPlacement>();
            _resolve = resolve;
        }

        public void SetCapacity(int value) => Capacity = Math.Clamp(value, 0, MaxCapacity);

        public bool FitsWithinCapacity(int proposedCapacity)
        {
            proposedCapacity = Math.Clamp(proposedCapacity, 0, MaxCapacity);
            for (int i = 0; i < _placements.Count; i++)
            {
                InventoryPlacement p = _placements[i];
                InventoryItemDefinition item = _resolve(p.itemId);
                if (item == null) continue;
                for (int y = 0; y < item.Height(p.rotated); y++)
                for (int x = 0; x < item.Width(p.rotated); x++)
                    if (item.Occupies(x, y, p.rotated)
                        && (p.y + y) * Width + p.x + x >= proposedCapacity) return false;
            }
            return true;
        }

        public bool TryAdd(InventoryItemDefinition item, out InventoryPlacement placement)
        {
            placement = null;
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return false;

            for (int rotation = 0; rotation < (item.CanRotate ? 2 : 1); rotation++)
            {
                bool rotated = rotation == 1;
                for (int y = 0; y < MaxRows; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (!CanPlace(item, x, y, rotated, null)) continue;
                    placement = new InventoryPlacement
                    {
                        instanceId = Guid.NewGuid().ToString("N"), itemId = item.Id,
                        x = x, y = y, rotated = rotated
                    };
                    _placements.Add(placement);
                    return true;
                }
            }
            return false;
        }

        public bool TryMove(string instanceId, int x, int y, bool rotated)
        {
            InventoryPlacement placement = _placements.Find(p => p.instanceId == instanceId);
            InventoryItemDefinition item = placement != null ? _resolve(placement.itemId) : null;
            if (item == null || (rotated && !item.CanRotate) || !CanPlace(item, x, y, rotated, instanceId)) return false;
            placement.x = x; placement.y = y; placement.rotated = rotated;
            return true;
        }

        public bool Remove(string instanceId) => _placements.RemoveAll(p => p.instanceId == instanceId) > 0;

        public bool CanPlace(InventoryItemDefinition item, int x, int y, bool rotated, string ignoredInstanceId)
        {
            if (item == null) return false;
            for (int localY = 0; localY < item.Height(rotated); localY++)
            for (int localX = 0; localX < item.Width(rotated); localX++)
            {
                if (!item.Occupies(localX, localY, rotated)) continue;
                int cellX = x + localX, cellY = y + localY;
                int index = cellY * Width + cellX;
                if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= MaxRows || index >= Capacity) return false;
                if (IsOccupied(cellX, cellY, ignoredInstanceId)) return false;
            }
            return true;
        }

        private bool IsOccupied(int x, int y, string ignoredInstanceId)
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                InventoryPlacement p = _placements[i];
                if (p.instanceId == ignoredInstanceId) continue;
                InventoryItemDefinition item = _resolve(p.itemId);
                if (item == null) continue;
                int lx = x - p.x, ly = y - p.y;
                if (lx >= 0 && ly >= 0 && lx < item.Width(p.rotated) && ly < item.Height(p.rotated)
                    && item.Occupies(lx, ly, p.rotated)) return true;
            }
            return false;
        }
    }
}
