using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using TxTRPG.Content.Items;
using TxTRPG.Gameplay.Players;

namespace TxTRPG.Application.Items
{
    public enum ItemUseFailure { None, Busy, InvalidSlot, EmptySlot, UnknownItem, UnsupportedEffect, OutOfStock, TargetAtFullHealth, ActiveCharacterChanged }
    public readonly struct ItemUseResult
    {
        public ItemUseResult(bool succeeded, ItemUseFailure failure, int appliedAmount = 0) { Succeeded = succeeded; Failure = failure; AppliedAmount = appliedAmount; }
        public bool Succeeded { get; }
        public ItemUseFailure Failure { get; }
        public int AppliedAmount { get; }
    }

    public sealed class QuickItemService
    {
        private readonly PlayerState player;
        private readonly ItemCatalog catalog;
        private sealed class SharedUseGate { public int InProgress; }
        private static readonly ConditionalWeakTable<PlayerState, SharedUseGate> UseGates = new();
        private readonly SharedUseGate useGate;

        public QuickItemService(PlayerState player, ItemCatalog catalog)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            useGate = UseGates.GetOrCreateValue(player);
        }

        public bool TryRegister(int slotIndex, string itemDefinitionId)
        {
            if (!catalog.TryGet(itemDefinitionId, out var definition) || definition.EffectKind == ItemEffectKind.Unsupported) return false;
            if (player.Inventory.GetQuantity(definition.DefinitionId) <= 0) return false;
            return player.QuickItems.TryAssign(slotIndex, definition.DefinitionId);
        }

        public bool TryUnregister(int slotIndex) => player.QuickItems.TryClear(slotIndex);

        public Task<ItemUseResult> UseAsync(int slotIndex, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (slotIndex < 0 || slotIndex >= player.QuickItems.Capacity)
                return Task.FromResult(new ItemUseResult(false, ItemUseFailure.InvalidSlot));
            if (Interlocked.CompareExchange(ref useGate.InProgress, 1, 0) != 0)
                return Task.FromResult(new ItemUseResult(false, ItemUseFailure.Busy));
            try { return Task.FromResult(UseCore(player.QuickItems.GetItemDefinitionId(slotIndex), slotIndex)); }
            finally { Volatile.Write(ref useGate.InProgress, 0); }
        }

        public Task<ItemUseResult> UseItemAsync(string itemDefinitionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.CompareExchange(ref useGate.InProgress, 1, 0) != 0)
                return Task.FromResult(new ItemUseResult(false, ItemUseFailure.Busy));
            try { return Task.FromResult(UseCore(itemDefinitionId, -1)); }
            finally { Volatile.Write(ref useGate.InProgress, 0); }
        }

        private ItemUseResult UseCore(string itemId, int slotIndex)
        {
            if (slotIndex >= player.QuickItems.Capacity) return new ItemUseResult(false, ItemUseFailure.InvalidSlot);
            itemId = itemId?.Trim() ?? string.Empty;
            if (itemId.Length == 0) return new ItemUseResult(false, ItemUseFailure.EmptySlot);
            if (!catalog.TryGet(itemId, out var definition)) return new ItemUseResult(false, ItemUseFailure.UnknownItem);
            if (definition.EffectKind != ItemEffectKind.Healing || definition.EffectAmount <= 0) return new ItemUseResult(false, ItemUseFailure.UnsupportedEffect);
            if (player.Inventory.GetQuantity(itemId) <= 0) return new ItemUseResult(false, ItemUseFailure.OutOfStock);

            var characterId = player.ActiveCharacterInstanceId;
            var target = player.ActiveCharacter;
            if (target.Health.Current >= target.Health.Maximum) return new ItemUseResult(false, ItemUseFailure.TargetAtFullHealth);
            if (player.ActiveCharacterInstanceId != characterId) return new ItemUseResult(false, ItemUseFailure.ActiveCharacterChanged);
            if (!player.Inventory.TryConsume(itemId)) return new ItemUseResult(false, ItemUseFailure.OutOfStock);
            var result = target.Health.Heal(definition.EffectAmount);
            if (!result.Changed)
            {
                player.Inventory.Add(itemId, 1);
                return new ItemUseResult(false, ItemUseFailure.TargetAtFullHealth);
            }
            return new ItemUseResult(true, ItemUseFailure.None, result.AppliedAmount);
        }
    }
}
