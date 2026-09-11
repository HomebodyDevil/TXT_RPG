using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Players;
using TxTRPG.Content.Items;
using TxTRPG.Gameplay.Players;
using TxTRPG.UI.Windows;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.Application.Items
{
    public sealed class InventoryGameWindowPage : GameWindowPage
    {
        [SerializeField] private PlayerSessionHost sessionHost;
        [SerializeField] private ItemCatalog catalog;
        [SerializeField] private TMP_Text selectionText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button previousItemButton;
        [SerializeField] private Button nextItemButton;
        [SerializeField] private Button previousSlotButton;
        [SerializeField] private Button nextSlotButton;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button unregisterButton;
        [SerializeField] private Button useButton;
        private readonly List<ItemDefinition> ownedItems = new();
        private PlayerState player;
        private QuickItemService service;
        private int itemIndex;
        private int slotIndex;

        private void Awake()
        {
            previousItemButton?.onClick.AddListener(PreviousItem); nextItemButton?.onClick.AddListener(NextItem);
            previousSlotButton?.onClick.AddListener(PreviousSlot); nextSlotButton?.onClick.AddListener(NextSlot);
            registerButton?.onClick.AddListener(Register); unregisterButton?.onClick.AddListener(Unregister); useButton?.onClick.AddListener(Use);
        }
        private void OnDestroy()
        {
            previousItemButton?.onClick.RemoveListener(PreviousItem); nextItemButton?.onClick.RemoveListener(NextItem);
            previousSlotButton?.onClick.RemoveListener(PreviousSlot); nextSlotButton?.onClick.RemoveListener(NextSlot);
            registerButton?.onClick.RemoveListener(Register); unregisterButton?.onClick.RemoveListener(Unregister); useButton?.onClick.RemoveListener(Use);
        }
        public override async Task PrepareAsync(CancellationToken cancellationToken)
        {
            var host = sessionHost != null ? sessionHost : PlayerSessionHost.Instance;
            if (host == null || catalog == null) throw new InvalidOperationException("Inventory page references are incomplete.");
            await host.EnsureInitializedAsync(cancellationToken);
            player = host.Session.CurrentPlayer; service = new QuickItemService(player, catalog);
            RebuildOwnedItems(); Refresh();
        }
        private void RebuildOwnedItems()
        {
            ownedItems.Clear();
            foreach (var definition in catalog.Definitions)
                if (definition != null && player.Inventory.GetQuantity(definition.DefinitionId) > 0) ownedItems.Add(definition);
            itemIndex = Mathf.Clamp(itemIndex, 0, Mathf.Max(0, ownedItems.Count - 1));
        }
        private void PreviousItem() { if (ownedItems.Count > 0) itemIndex = (itemIndex - 1 + ownedItems.Count) % ownedItems.Count; Refresh(); }
        private void NextItem() { if (ownedItems.Count > 0) itemIndex = (itemIndex + 1) % ownedItems.Count; Refresh(); }
        private void PreviousSlot() { slotIndex = (slotIndex - 1 + player.QuickItems.Capacity) % player.QuickItems.Capacity; Refresh(); }
        private void NextSlot() { slotIndex = (slotIndex + 1) % player.QuickItems.Capacity; Refresh(); }
        private void Register() { SetResult(ownedItems.Count > 0 && service.TryRegister(slotIndex, ownedItems[itemIndex].DefinitionId) ? "Registered." : "Could not register this item."); Refresh(); }
        private void Unregister() { SetResult(service.TryUnregister(slotIndex) ? "Removed." : "Could not remove the slot."); Refresh(); }
        private async void Use() { var result = await service.UseAsync(slotIndex); SetResult(result.Succeeded ? $"Recovered {result.AppliedAmount} health." : result.Failure.ToString()); RebuildOwnedItems(); Refresh(); }
        private void SetResult(string value) { if (resultText != null) resultText.text = value; }
        private void Refresh()
        {
            if (selectionText == null || player == null) return;
            var item = ownedItems.Count > 0 ? ownedItems[itemIndex] : null;
            var assigned = player.QuickItems.GetItemDefinitionId(slotIndex);
            selectionText.text = $"Slot {slotIndex + 1}: {(assigned.Length > 0 ? assigned : "Empty")}\nInventory: {(item != null ? item.DisplayNameLocalizationKey + " x" + player.Inventory.GetQuantity(item.DefinitionId) : "No usable items")}";
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(PlayerSessionHost host, ItemCatalog itemCatalog, TMP_Text selection, TMP_Text result,
            Button previousItem, Button nextItem, Button previousSlot, Button nextSlot, Button register, Button unregister, Button use)
        { sessionHost = host; catalog = itemCatalog; selectionText = selection; resultText = result; previousItemButton = previousItem; nextItemButton = nextItem; previousSlotButton = previousSlot; nextSlotButton = nextSlot; registerButton = register; unregisterButton = unregister; useButton = use; }
#endif
    }

    public sealed class StatusGameWindowPage : GameWindowPage
    {
        [SerializeField] private PlayerSessionHost sessionHost;
        [SerializeField] private TMP_Text statusText;
        public override async Task PrepareAsync(CancellationToken cancellationToken)
        {
            var host = sessionHost != null ? sessionHost : PlayerSessionHost.Instance;
            if (host == null) throw new InvalidOperationException("Status page requires a PlayerSessionHost.");
            await host.EnsureInitializedAsync(cancellationToken);
            var character = host.Session.CurrentPlayer.ActiveCharacter;
            statusText.text = $"HP {character.Health.Current} / {character.Health.Maximum}\nAttack {character.Stats.AttackPower}";
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(PlayerSessionHost host, TMP_Text text) { sessionHost = host; statusText = text; }
#endif
    }
}
