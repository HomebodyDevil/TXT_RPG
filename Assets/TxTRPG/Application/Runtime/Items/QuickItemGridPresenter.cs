using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Application.Players;
using TxTRPG.Content.Items;
using TxTRPG.Gameplay.Players;
using TxTRPG.UI;
using UnityEngine;

namespace TxTRPG.Application.Items
{
    [DisallowMultipleComponent]
    public sealed class QuickItemGridPresenter : MonoBehaviour,
        IActionGridEntryProvider, IActionMenuProvider, IActionCommandExecutor
    {
        public const string UseCommandId = "use";
        public const string RemoveCommandId = "remove";

        [SerializeField] private ActionGridPanel panel;
        [SerializeField] private ItemCatalog itemCatalog;
        [SerializeField] private PlayerSessionHost sessionHost;
        [SerializeField] private bool configureGridPolicy = true;

        private PlayerState player;
        private QuickItemService service;
        private readonly List<ActionGridEntry> entries = new();
        private CancellationTokenSource lifetime;

        public void Configure(ActionGridPanel target, ItemCatalog catalog, PlayerSessionHost host = null)
        {
            panel = target;
            itemCatalog = catalog;
            sessionHost = host;
        }

        private async void OnEnable()
        {
            panel?.BeginInitialContentSetup();
            lifetime = new CancellationTokenSource();
            try
            {
                var host = sessionHost != null ? sessionHost : PlayerSessionHost.Instance;
                if (host == null) throw new InvalidOperationException("QuickItemGridPresenter requires a PlayerSessionHost.");
                await host.EnsureInitializedAsync(lifetime.Token);
                Bind(host.Session.CurrentPlayer);
                panel?.CompleteInitialContentSetup();
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { panel?.CancelInitialContentSetup(); }
            catch (Exception exception) { panel?.CompleteInitialContentSetup(); Debug.LogError($"Quick-item UI initialization failed: {exception.Message}", this); }
        }

        private void OnDisable()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            Unbind();
        }

        public IReadOnlyList<ActionGridEntry> GetEntries() => entries;

        public IReadOnlyList<ActionMenuOption> GetOptions(string entryId)
        {
            var slot = ParseSlot(entryId);
            if (slot < 0) return Array.Empty<ActionMenuOption>();
            var itemId = player.QuickItems.GetItemDefinitionId(slot);
            var quantity = player.Inventory.GetQuantity(itemId);
            return new[]
            {
                new ActionMenuOption(UseCommandId, "Use", quantity > 0, quantity > 0 ? string.Empty : "Out of stock"),
                new ActionMenuOption(RemoveCommandId, "Remove")
            };
        }

        public async Task<ActionCommandResult> ExecuteAsync(string entryId, string commandId, CancellationToken cancellationToken)
        {
            var slot = ParseSlot(entryId);
            if (slot < 0) return new ActionCommandResult(false, "Invalid quick-item slot.");
            if (commandId == RemoveCommandId)
                return new ActionCommandResult(service.TryUnregister(slot), "Quick item removed.");
            if (commandId != UseCommandId) return new ActionCommandResult(false, "Unknown command.");
            var result = await service.UseAsync(slot, cancellationToken);
            return new ActionCommandResult(result.Succeeded, result.Succeeded ? $"Recovered {result.AppliedAmount} health." : result.Failure.ToString());
        }

        private void Bind(PlayerState state)
        {
            Unbind();
            if (panel == null || itemCatalog == null) throw new InvalidOperationException("Quick-item presenter references are incomplete.");
            player = state;
            service = new QuickItemService(player, itemCatalog);
            player.Inventory.QuantityChanged += OnInventoryChanged;
            player.QuickItems.SlotChanged += OnSlotChanged;
            if (configureGridPolicy) panel.ConfigureBehavior(ActionGridPopulationMode.FillCapacityWithEmptySlots, ActionGridPackingMode.PreserveSlots, GridActivationBehavior.ExecuteDefaultAction);
            panel.SetServices(this, this);
            Refresh();
        }

        private void Unbind()
        {
            if (player != null)
            {
                player.Inventory.QuantityChanged -= OnInventoryChanged;
                player.QuickItems.SlotChanged -= OnSlotChanged;
            }
            player = null;
            service = null;
        }

        private void OnInventoryChanged(string _, int __) => Refresh();
        private void OnSlotChanged(int _, string __) => Refresh();
        private void Refresh()
        {
            entries.Clear();
            for (var slot = 0; slot < player.QuickItems.Capacity; slot++)
            {
                var itemId = player.QuickItems.GetItemDefinitionId(slot);
                if (itemId.Length == 0 || !itemCatalog.TryGet(itemId, out var definition)) { entries.Add(default); continue; }
                var quantity = player.Inventory.GetQuantity(itemId);
                entries.Add(new ActionGridEntry($"quick-item:{slot}", ActionGridEntryKind.Item, null,
                    definition.DisplayNameLocalizationKey, quantity: quantity, isEnabled: quantity > 0,
                    shortcutLabel: (slot + 1).ToString(), iconAssetId: definition.IconAssetId));
            }
            panel.SetEntries(entries, player.QuickItems.Capacity);
        }

        private static int ParseSlot(string entryId)
        {
            const string prefix = "quick-item:";
            return entryId != null && entryId.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(entryId.Substring(prefix.Length), out var slot) ? slot : -1;
        }
    }
}
