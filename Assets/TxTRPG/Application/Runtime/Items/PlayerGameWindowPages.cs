using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Players;
using TxTRPG.Content.Items;
using TxTRPG.Gameplay.Players;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.Application.Items
{
    public sealed class InventoryGameWindowPage : GameWindowPage, IActionMenuProvider, IActionCommandExecutor, ISerializationCallbackReceiver
    {
        private sealed class ConfigurationException : InvalidOperationException, IGameWindowDisplayException
        {
            public ConfigurationException(string errorCode, string diagnostic) : base(diagnostic) { ErrorCode = errorCode; }
            public string ErrorCode { get; }
            public string UserMessage => "가방을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }
        [SerializeField] private PlayerSessionHost sessionHost;
        [SerializeField] private ItemCatalog catalog;
        [SerializeField] private ActionGridPanel itemGrid;
        [SerializeField] private RectTransform categoryTabs;
        [SerializeField] private Button categoryButtonPrefab;
        [SerializeField] private TMP_Text emptyState;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private RectTransform pageNumbersRoot;
        [SerializeField] private Button pageNumberButtonPrefab;
        [SerializeField, Min(1)] private int maximumVisiblePageButtons = 7;
        [SerializeField] private GridContentLayoutSettings gridSettings = new();
        [SerializeField, HideInInspector] private InventoryDisplayMode displayMode = InventoryDisplayMode.VerticalScroll;
        [SerializeField, HideInInspector] private int itemsPerPage = 12;
        [SerializeField, HideInInspector] private bool fillPageWithEmptySlots;
        [SerializeField, HideInInspector] private int gridSettingsVersion;
        [SerializeField] private List<InventoryCategoryDefinition> categories = new()
        {
            new InventoryCategoryDefinition { id = InventoryProjectionBuilder.AllCategoryId, displayName = "All", order = 0 },
            new InventoryCategoryDefinition { id = "consumable", displayName = "Consumables", order = 10 },
            new InventoryCategoryDefinition { id = InventoryProjectionBuilder.MiscCategoryId, displayName = "Misc", order = 20 }
        };
        private readonly List<Button> tabButtons = new();
        private readonly List<Button> pageNumberButtons = new();
        private PlayerState player;
        private QuickItemService service;
        private string categoryId = InventoryProjectionBuilder.AllCategoryId;
        private string selectedItemId = string.Empty;
        private int pageIndex;
        private InventoryModalDataProvider requestProvider;
        public bool IsUsingFallbackGridSettings { get; private set; }
        public string GridSettingsSource => IsUsingFallbackGridSettings ? "SafeFallback" : "Prefab";
        public InventoryDisplayMode DisplayMode => gridSettings.displayMode;
        public int CurrentPageNumber => pageIndex + 1;
        public int VisiblePageNumberButtonCount => pageNumberButtons.Count;
        public bool IsVerticalScrollEnabled => itemGrid != null && itemGrid.ScrollRect != null && itemGrid.ScrollRect.vertical;

        public override ModalOpenRequest CreateDefaultRequest(GameObject returnFocus = null) =>
            new(PageId, ModalContentKind.ItemGrid, DisplayTitle,
                GridContentLayoutSettings.CreateSafeCopy(gridSettings, out _),
                new InventoryModalDataProvider(sessionHost, catalog), InventoryProjectionBuilder.AllCategoryId, 1, returnFocus);

        public override void ApplyRequest(ModalOpenRequest request)
        {
            if (request == null || !string.Equals(request.WindowId, PageId, StringComparison.Ordinal))
                throw new ModalRequestException("inventory.request-page-mismatch", $"Inventory page received request '{request?.WindowId ?? "<null>"}'.");
            if (request.ContentKind != ModalContentKind.ItemGrid)
                throw new ModalRequestException("inventory.request-kind-mismatch", $"Inventory requires ItemGrid, received '{request.ContentKind}'.");
            requestProvider = request.DataProvider as InventoryModalDataProvider
                ?? throw new ModalRequestException("inventory.provider-missing", "Inventory request has no compatible data provider.", "가방 데이터를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.");
            gridSettings = GridContentLayoutSettings.CreateSafeCopy(request.Configuration as GridContentLayoutSettings, out var usedFallback);
            IsUsingFallbackGridSettings = usedFallback;
            categoryId = string.IsNullOrEmpty(request.InitialCategoryId) ? InventoryProjectionBuilder.AllCategoryId : request.InitialCategoryId;
            pageIndex = Math.Max(0, request.InitialPage - 1);
        }

        private void Awake()
        {
            previousPageButton?.onClick.AddListener(PreviousPage);
            nextPageButton?.onClick.AddListener(NextPage);
            if (itemGrid != null) itemGrid.SelectionChanged += OnSelectionChanged;
            BuildCategoryTabs();
        }
        private void OnValidate()
        {
            gridSettings = GridContentLayoutSettings.CreateSafeCopy(gridSettings, out _);
            maximumVisiblePageButtons = Mathf.Max(1, maximumVisiblePageButtons);
        }
        private void OnDestroy()
        {
            previousPageButton?.onClick.RemoveListener(PreviousPage);
            nextPageButton?.onClick.RemoveListener(NextPage);
            if (itemGrid != null) itemGrid.SelectionChanged -= OnSelectionChanged;
            UnsubscribeInventory();
        }
        public override async Task PrepareAsync(CancellationToken cancellationToken)
        {
            var host = requestProvider?.ResolveHost() ?? (sessionHost != null ? sessionHost : PlayerSessionHost.Instance);
            var activeCatalog = requestProvider?.Catalog ?? catalog;
            if (host == null) throw new ConfigurationException("inventory.session-host-missing", $"Inventory page '{GetHierarchyPath()}' has no PlayerSessionHost in the AppScene lifetime.");
            if (activeCatalog == null) throw new ConfigurationException("inventory.catalog-missing", $"Inventory page '{GetHierarchyPath()}' has no ItemCatalog reference.");
            if (itemGrid == null) throw new ConfigurationException("inventory.item-grid-missing", $"Inventory page '{GetHierarchyPath()}' has no ActionGridPanel reference.");
            gridSettings = GridContentLayoutSettings.CreateSafeCopy(gridSettings, out var usedFallback);
            IsUsingFallbackGridSettings |= usedFallback;
            if (usedFallback) Debug.LogWarning($"Inventory page '{GetHierarchyPath()}' is using safe fallback display settings.", this);
            await host.EnsureInitializedAsync(cancellationToken);
            UnsubscribeInventory();
            player = host.Session.CurrentPlayer;
            catalog = activeCatalog;
            service = new QuickItemService(player, catalog);
            player.Inventory.QuantityChanged += OnQuantityChanged;
            itemGrid.SetServices(this, this);
            itemGrid.ConfigureBehavior(ActionGridPopulationMode.EntriesOnly, ActionGridPackingMode.CompactForward, GridActivationBehavior.OpenContextMenu);
            Refresh(true);
        }

        public override void Hide()
        {
            itemGrid?.CloseContextMenu();
            base.Hide();
        }

        public override void DisposePage() => UnsubscribeInventory();

        public void SetDisplayMode(InventoryDisplayMode mode)
        {
            if (gridSettings.displayMode == mode) return;
            itemGrid?.CloseContextMenu();
            gridSettings.displayMode = mode;
            Refresh(true, true);
        }

        public void ConfigureGrid(GridContentLayoutSettings settings)
        {
            gridSettings = GridContentLayoutSettings.CreateSafeCopy(settings, out var usedFallback);
            IsUsingFallbackGridSettings = usedFallback;
            itemGrid?.CloseContextMenu();
            Refresh(false, true);
        }

        public void SelectCategory(string id)
        {
            itemGrid?.CloseContextMenu(); categoryId = string.IsNullOrWhiteSpace(id) ? InventoryProjectionBuilder.AllCategoryId : id.Trim();
            pageIndex = 0; selectedItemId = string.Empty; Refresh(true);
        }

        public void GoToPage(int oneBasedPage)
        { itemGrid?.CloseContextMenu(); pageIndex = Mathf.Max(0, oneBasedPage - 1); Refresh(true); }

        private void PreviousPage() => GoToPage(pageIndex);
        private void NextPage() => GoToPage(pageIndex + 2);
        private void OnSelectionChanged(ActionGridEntry entry) => selectedItemId = entry.Id;
        private void OnQuantityChanged(string _, int __) => Refresh(false);
        private void UnsubscribeInventory() { if (player != null) player.Inventory.QuantityChanged -= OnQuantityChanged; }

        private void Refresh(bool resetTop, bool preserveSelectionPage = false)
        {
            if (player == null || catalog == null || itemGrid == null) return;
            gridSettings = GridContentLayoutSettings.CreateSafeCopy(gridSettings, out var usedFallback);
            IsUsingFallbackGridSettings |= usedFallback;
            var knownCategories = categories.Select(value => value.id).Append(InventoryProjectionBuilder.MiscCategoryId);
            var projection = InventoryProjectionBuilder.Build(catalog.Definitions, player.Inventory.GetQuantity,
                categoryId, InventoryDisplayMode.VerticalScroll, 0, gridSettings.slotsPerPage, knownCategories);
            var allEntries = new List<ActionGridEntry>();
            foreach (var item in projection.Items)
                allEntries.Add(new ActionGridEntry(item.DefinitionId, ActionGridEntryKind.Item, null,
                    item.DisplayNameLocalizationKey, item.CategoryId, player.Inventory.GetQuantity(item.DefinitionId),
                    true, iconAssetId: item.IconAssetId));
            if (categoryId == InventoryProjectionBuilder.AllCategoryId || categoryId == InventoryProjectionBuilder.MiscCategoryId)
                foreach (var pair in player.Inventory.Quantities)
                    if (pair.Value > 0 && !catalog.TryGet(pair.Key, out _))
                        allEntries.Add(new ActionGridEntry(pair.Key, ActionGridEntryKind.Item, null,
                            $"Unknown item ({pair.Key})", "Definition is missing.", pair.Value, false));
            var safeItemsPerPage = gridSettings.slotsPerPage;
            if (preserveSelectionPage && gridSettings.displayMode == InventoryDisplayMode.Paged && !string.IsNullOrEmpty(selectedItemId))
            {
                var selectedIndex = allEntries.FindIndex(entry => entry.Id == selectedItemId);
                if (selectedIndex >= 0) pageIndex = selectedIndex / safeItemsPerPage;
            }
            var pageCount = gridSettings.displayMode == InventoryDisplayMode.Paged
                ? Mathf.Max(1, Mathf.CeilToInt(allEntries.Count / (float)safeItemsPerPage)) : 1;
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            var entries = gridSettings.displayMode == InventoryDisplayMode.Paged
                ? allEntries.Skip(pageIndex * safeItemsPerPage).Take(safeItemsPerPage).ToList()
                : allEntries;
            var capacity = gridSettings.CalculateDisplayCapacity(entries.Count);
            itemGrid.ConfigureLayout(
                gridSettings.columnPolicy == InventoryColumnPolicy.Exact ? ActionGridLayoutMode.ExactColumns : ActionGridLayoutMode.FixedColumns,
                gridSettings.columns, gridSettings.cellSize, gridSettings.spacing, gridSettings.padding,
                gridSettings.alignment, gridSettings.incompleteRowAlignment, gridSettings.verticalPlacement);
            itemGrid.ConfigureBehavior(capacity > entries.Count
                ? ActionGridPopulationMode.FillCapacityWithEmptySlots : ActionGridPopulationMode.EntriesOnly,
                ActionGridPackingMode.CompactForward, GridActivationBehavior.OpenContextMenu);
            itemGrid.SetEntries(entries, capacity);
            if (resetTop) itemGrid.ScrollToTop();
            if (!string.IsNullOrEmpty(selectedItemId))
            {
                var index = entries.FindIndex(entry => entry.Id == selectedItemId);
                if (index >= 0) itemGrid.Select(index, false);
            }
            var paged = gridSettings.displayMode == InventoryDisplayMode.Paged;
            if (previousPageButton != null) { previousPageButton.gameObject.SetActive(paged); previousPageButton.interactable = pageIndex > 0; }
            if (nextPageButton != null) { nextPageButton.gameObject.SetActive(paged); nextPageButton.interactable = pageIndex + 1 < pageCount; }
            if (pageText != null) { pageText.gameObject.SetActive(paged); pageText.text = $"{pageIndex + 1} / {pageCount}"; }
            RebuildPageNumberButtons(paged ? pageCount : 0);
            if (itemGrid.ScrollRect != null) itemGrid.ScrollRect.vertical = !paged;
            if (emptyState != null) emptyState.gameObject.SetActive(entries.Count == 0);
        }

        private void RebuildPageNumberButtons(int pageCount)
        {
            foreach (var button in pageNumberButtons) if (button != null) Destroy(button.gameObject);
            pageNumberButtons.Clear();
            if (pageNumbersRoot == null || pageNumberButtonPrefab == null || pageCount <= 0) return;
            var visibleCount = Mathf.Min(pageCount, maximumVisiblePageButtons);
            var first = Mathf.Clamp(pageIndex - visibleCount / 2, 0, Mathf.Max(0, pageCount - visibleCount));
            for (var i = 0; i < visibleCount; i++)
            {
                var targetPage = first + i;
                var button = Instantiate(pageNumberButtonPrefab, pageNumbersRoot);
                button.gameObject.SetActive(true);
                button.interactable = targetPage != pageIndex;
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = (targetPage + 1).ToString();
                button.onClick.AddListener(() => GoToPage(targetPage + 1));
                pageNumberButtons.Add(button);
            }
        }

        public IReadOnlyList<ActionMenuOption> GetOptions(string entryId)
        {
            var options = new List<ActionMenuOption>();
            ItemDefinition definition = null;
            var known = catalog != null && catalog.TryGet(entryId, out definition);
            var quantity = player?.Inventory.GetQuantity(entryId) ?? 0;
            var usable = known && definition.EffectKind == ItemEffectKind.Healing && definition.EffectAmount > 0 && quantity > 0;
            options.Add(new ActionMenuOption("use", "Use", usable, usable ? "" : "Unavailable"));
            if (player != null)
                for (var i = 0; i < player.QuickItems.Capacity; i++)
                {
                    var assigned = player.QuickItems.GetItemDefinitionId(i);
                    var available = known && usable && (assigned.Length == 0 || assigned == entryId);
                    options.Add(new ActionMenuOption($"register:{i}", $"Register to slot {i + 1}", available,
                        available ? "" : assigned.Length > 0 ? "Slot occupied" : "Unavailable"));
                }
            options.Add(new ActionMenuOption("discard", "Discard", false, "Not implemented"));
            return options;
        }

        public async Task<ActionCommandResult> ExecuteAsync(string entryId, string commandId, CancellationToken cancellationToken)
        {
            if (player == null || service == null) return new ActionCommandResult(false, "Inventory is not ready.");
            if (commandId == "use")
            {
                var result = await service.UseItemAsync(entryId, cancellationToken);
                var message = result.Succeeded ? $"Recovered {result.AppliedAmount} health." : result.Failure.ToString();
                SetResult(message); Refresh(false); return new ActionCommandResult(result.Succeeded, message);
            }
            if (commandId.StartsWith("register:", StringComparison.Ordinal) && int.TryParse(commandId.Substring(9), out var slot))
            {
                if (slot < 0 || slot >= player.QuickItems.Capacity)
                    return new ActionCommandResult(false, "Invalid quick slot.");
                var assigned = player.QuickItems.GetItemDefinitionId(slot);
                if (assigned.Length > 0 && !string.Equals(assigned, entryId, StringComparison.Ordinal))
                    return new ActionCommandResult(false, "The quick slot is occupied and replacement was not confirmed.");
                var succeeded = service.TryRegister(slot, entryId);
                var message = succeeded ? $"Registered to slot {slot + 1}." : "Could not register this item.";
                SetResult(message); Refresh(false); return new ActionCommandResult(succeeded, message);
            }
            return new ActionCommandResult(false, "This command is not implemented.");
        }

        private void SetResult(string value) { if (resultText != null) resultText.text = value ?? string.Empty; }

        private void BuildCategoryTabs()
        {
            if (categoryTabs == null || categoryButtonPrefab == null) return;
            foreach (var button in tabButtons) if (button != null) Destroy(button.gameObject);
            tabButtons.Clear();
            foreach (var category in categories.OrderBy(value => value.order).ThenBy(value => value.id, StringComparer.Ordinal))
            {
                var definition = category;
                var button = Instantiate(categoryButtonPrefab, categoryTabs);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>(true); if (label != null) label.text = definition.displayName;
                button.onClick.AddListener(() => SelectCategory(definition.id)); tabButtons.Add(button);
            }
        }

        public bool ValidateRequiredReferences(out string errorCode)
        {
            if (catalog == null) { errorCode = "inventory.catalog-missing"; return false; }
            if (itemGrid == null) { errorCode = "inventory.item-grid-missing"; return false; }
            errorCode = string.Empty;
            return true;
        }

        private string GetHierarchyPath()
        {
            var current = transform;
            var path = current.name;
            while (current.parent != null) { current = current.parent; path = current.name + "/" + path; }
            return path;
        }
#if UNITY_EDITOR
        public void ConfigureInventoryForEditor(PlayerSessionHost host, ItemCatalog itemCatalog, ActionGridPanel grid,
            RectTransform tabs, Button tabPrefab, TMP_Text empty, TMP_Text result, Button previousPage,
            Button nextPage, TMP_Text pageLabel, RectTransform numbersRoot = null, Button numberTemplate = null)
        { sessionHost = host; catalog = itemCatalog; itemGrid = grid; categoryTabs = tabs; categoryButtonPrefab = tabPrefab;
          emptyState = empty; resultText = result; previousPageButton = previousPage; nextPageButton = nextPage; pageText = pageLabel;
          pageNumbersRoot = numbersRoot; pageNumberButtonPrefab = numberTemplate; }
#endif

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize()
        {
            if (gridSettingsVersion > 0 && gridSettings != null) return;
            gridSettings ??= new GridContentLayoutSettings();
            gridSettings.displayMode = displayMode;
            gridSettings.slotsPerPage = Math.Max(1, itemsPerPage);
            gridSettings.fillPageWithEmptySlots = fillPageWithEmptySlots;
            gridSettingsVersion = 1;
        }
    }

}
