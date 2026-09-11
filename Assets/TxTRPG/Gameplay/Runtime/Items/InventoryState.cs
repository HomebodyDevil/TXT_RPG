using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.Items
{
    public sealed class InventoryState
    {
        private readonly Dictionary<string, int> quantities = new(StringComparer.Ordinal);
        public event Action<string, int> QuantityChanged;
        public InventoryState(IEnumerable<KeyValuePair<string, int>> initialQuantities = null)
        {
            if (initialQuantities == null) return;
            foreach (var pair in initialQuantities)
            {
                var id = NormalizeId(pair.Key);
                if (id.Length == 0) throw new ArgumentException("Item IDs cannot be empty.");
                if (pair.Value < 0) throw new ArgumentOutOfRangeException(nameof(initialQuantities), "Item quantities cannot be negative.");
                if (!quantities.TryAdd(id, pair.Value)) throw new ArgumentException($"Duplicate item ID '{id}'.");
            }
        }
        public IEnumerable<KeyValuePair<string, int>> Quantities => quantities;
        public int GetQuantity(string itemDefinitionId) => quantities.TryGetValue(NormalizeId(itemDefinitionId), out var value) ? value : 0;
        public void Add(string itemDefinitionId, int amount)
        {
            var id = RequireId(itemDefinitionId);
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var next = checked(GetQuantity(id) + amount);
            quantities[id] = next;
            QuantityChanged?.Invoke(id, next);
        }
        public bool TryConsume(string itemDefinitionId, int amount = 1)
        {
            var id = RequireId(itemDefinitionId);
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var current = GetQuantity(id);
            if (current < amount) return false;
            var next = current - amount;
            quantities[id] = next;
            QuantityChanged?.Invoke(id, next);
            return true;
        }
        private static string RequireId(string id)
        {
            var normalized = NormalizeId(id);
            if (normalized.Length == 0) throw new ArgumentException("An item definition ID is required.", nameof(id));
            return normalized;
        }
        private static string NormalizeId(string id) => id?.Trim() ?? string.Empty;
    }
}
