using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.Items
{
    public sealed class QuickItemLoadout
    {
        public const int DefaultCapacity = 5;
        private readonly string[] itemDefinitionIds;
        public QuickItemLoadout(int capacity = DefaultCapacity, IEnumerable<KeyValuePair<int, string>> initialSlots = null)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            itemDefinitionIds = new string[capacity];
            if (initialSlots == null) return;
            var assigned = new HashSet<int>();
            foreach (var pair in initialSlots)
            {
                if (!IsValidSlot(pair.Key)) throw new ArgumentOutOfRangeException(nameof(initialSlots), $"Quick-item slot {pair.Key} is outside capacity {capacity}.");
                if (!assigned.Add(pair.Key)) throw new ArgumentException($"Duplicate quick-item slot {pair.Key}.");
                itemDefinitionIds[pair.Key] = NormalizeId(pair.Value);
            }
        }
        public event Action<int, string> SlotChanged;
        public int Capacity => itemDefinitionIds.Length;
        public string GetItemDefinitionId(int slotIndex) => IsValidSlot(slotIndex) ? itemDefinitionIds[slotIndex] ?? string.Empty : string.Empty;
        public bool TryAssign(int slotIndex, string itemDefinitionId)
        {
            if (!IsValidSlot(slotIndex)) return false;
            var id = NormalizeId(itemDefinitionId);
            if (id.Length == 0) return false;
            if (itemDefinitionIds[slotIndex] == id) return true;
            itemDefinitionIds[slotIndex] = id;
            SlotChanged?.Invoke(slotIndex, id);
            return true;
        }
        public bool TryClear(int slotIndex)
        {
            if (!IsValidSlot(slotIndex)) return false;
            if (string.IsNullOrEmpty(itemDefinitionIds[slotIndex])) return true;
            itemDefinitionIds[slotIndex] = string.Empty;
            SlotChanged?.Invoke(slotIndex, string.Empty);
            return true;
        }
        private bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < itemDefinitionIds.Length;
        private static string NormalizeId(string id) => id?.Trim() ?? string.Empty;
    }
}
