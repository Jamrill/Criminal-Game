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
        public int rotation;
        // Old saves stored only a boolean (0 or 90 degrees).
        public int Rotation => rotation == 0 && rotated ? 1 : ((rotation % 4) + 4) % 4;
    }

    public sealed class InventoryGrid
    {
        public const int Width = 10;
        public const int MaxRows = 8;
        public const int MaxCapacity = Width * MaxRows;
        public int Columns { get; private set; }
        public int Rows => (MaxCapacity + Columns - 1) / Columns;

        private readonly List<InventoryPlacement> _placements;
        private readonly Func<string, InventoryItemDefinition> _resolve;
        public int Capacity { get; private set; }
        public IReadOnlyList<InventoryPlacement> Placements => _placements;

        public InventoryGrid(int capacity, List<InventoryPlacement> placements, Func<string, InventoryItemDefinition> resolve, int columns = Width)
        {
            Columns = Math.Clamp(columns, 1, MaxCapacity);
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
                for (int y = 0; y < item.Height(p.Rotation); y++)
                for (int x = 0; x < item.Width(p.Rotation); x++)
                    if (item.Occupies(x, y, p.Rotation)
                        && (p.y + y) * Columns + p.x + x >= proposedCapacity) return false;
            }
            return true;
        }

        public bool TryAdd(InventoryItemDefinition item, out InventoryPlacement placement)
        {
            placement = null;
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return false;

            for (int rotation = 0; rotation < (item.CanRotate ? 4 : 1); rotation++)
            {
                int rotated = rotation;
                for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                {
                    if (!CanPlace(item, x, y, rotated, null)) continue;
                    placement = new InventoryPlacement
                    {
                        instanceId = Guid.NewGuid().ToString("N"), itemId = item.Id,
                        x = x, y = y, rotation = rotated, rotated = (rotated & 1) != 0
                    };
                    _placements.Add(placement);
                    return true;
                }
            }
            return false;
        }

        public bool TryMove(string instanceId, int x, int y, int rotated)
        {
            InventoryPlacement placement = _placements.Find(p => p.instanceId == instanceId);
            InventoryItemDefinition item = placement != null ? _resolve(placement.itemId) : null;
            if (item == null || (rotated != 0 && !item.CanRotate) || !CanPlace(item, x, y, rotated, instanceId)) return false;
            placement.x = x; placement.y = y; placement.rotation = ((rotated % 4) + 4) % 4; placement.rotated = (placement.rotation & 1) != 0;
            return true;
        }

        public bool Remove(string instanceId) => _placements.RemoveAll(p => p.instanceId == instanceId) > 0;

        public bool CanPlace(InventoryItemDefinition item, int x, int y, int rotated, string ignoredInstanceId)
        {
            if (item == null) return false;
            for (int localY = 0; localY < item.Height(rotated); localY++)
            for (int localX = 0; localX < item.Width(rotated); localX++)
            {
                if (!item.Occupies(localX, localY, rotated)) continue;
                int cellX = x + localX, cellY = y + localY;
                int index = cellY * Columns + cellX;
                if (cellX < 0 || cellX >= Columns || cellY < 0 || cellY >= Rows || index >= Capacity) return false;
                if (IsOccupied(cellX, cellY, ignoredInstanceId)) return false;
            }
            return true;
        }

        public bool TrySetColumns(int columns)
        {
            columns = Math.Clamp(columns, 1, MaxCapacity);
            if (columns == Columns) return true;
            var test = new InventoryGrid(Capacity, _placements, _resolve, columns);
            bool fits = true;
            foreach (var p in _placements)
                if (!test.CanPlace(_resolve(p.itemId), p.x, p.y, p.Rotation, p.instanceId)) { fits = false; break; }
            if (fits) { Columns = columns; return true; }

            // Repack on a separate grid. Failure must never delete or move items.
            var packed = new List<InventoryPlacement>();
            test = new InventoryGrid(Capacity, packed, _resolve, columns);
            foreach (var p in _placements)
            {
                if (!test.TryAdd(_resolve(p.itemId), out var placed)) return false;
                placed.instanceId = p.instanceId;
            }
            for (int i = 0; i < _placements.Count; i++)
            {
                _placements[i].x = packed[i].x;
                _placements[i].y = packed[i].y;
                _placements[i].rotation = packed[i].rotation;
                _placements[i].rotated = packed[i].rotated;
            }
            Columns = columns;
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
                if (lx >= 0 && ly >= 0 && lx < item.Width(p.Rotation) && ly < item.Height(p.Rotation)
                    && item.Occupies(lx, ly, p.Rotation)) return true;
            }
            return false;
        }
    }
}
