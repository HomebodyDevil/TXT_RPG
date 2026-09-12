using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public sealed class ActionGridPanel : MonoBehaviour, IPanelInitialLayoutParticipant
    {
        private const float LayoutEpsilon = 0.5f;

        [Header("References")]
        [SerializeField] private ActionGridCell cellPrefab;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private ConfigurableScrollbarController scrollbarController;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private ActionContextMenu contextMenu;

        [Header("Layout")]
        [FormerlySerializedAs("slotAlignment")]
        [SerializeField, Tooltip("Aligns the complete grid block within the viewport.")]
        private ActionGridHorizontalAlignment gridAlignment =
            ActionGridHorizontalAlignment.Center;
        [SerializeField, Tooltip("Aligns only the final row when it contains fewer cells than the current column count.")]
        private ActionGridHorizontalAlignment incompleteRowAlignment =
            ActionGridHorizontalAlignment.Left;
        [SerializeField, Tooltip("Keeps fitting content at the top or centers it vertically. Overflowing content always starts at the top.")]
        private ActionGridVerticalPlacement verticalPlacement =
            ActionGridVerticalPlacement.Top;
        [SerializeField] private ActionGridLayoutMode layoutMode = ActionGridLayoutMode.FixedColumns;
        [SerializeField, Min(1)] private int fixedColumns = 5;
        [SerializeField] private Vector2 minimumCellSize = new(72f, 72f);
        [SerializeField] private Vector2 maximumCellSize = new(128f, 128f);
        [SerializeField] private Vector2 spacing = new(8f, 8f);
        [SerializeField] private RectOffset padding;

        [Header("Behavior")]
        [SerializeField] private GridActivationBehavior activationBehavior = GridActivationBehavior.OpenContextMenu;
        [SerializeField] private ActionGridPopulationMode populationMode =
            ActionGridPopulationMode.FillCapacityWithEmptySlots;
        [SerializeField] private ActionGridPackingMode packingMode = ActionGridPackingMode.CompactForward;
        [SerializeField, Min(0)] private int initialCapacity = 20;
        [SerializeField, Min(0)] private int capacity = 20;
        [SerializeField, Range(1, 8)] private int maxConcurrentIconLoads = 4;

        private readonly List<ActionGridEntry> entries = new();
        private readonly List<ActionGridCell> cellPool = new();
        private IActionMenuProvider menuProvider;
        private IActionCommandExecutor commandExecutor;
        private CancellationTokenSource commandCancellation;
        private CancellationTokenSource assetCancellation;
        private AssetScope assetScope;
        private IAssetProvider assetProvider;
        private int selectedIndex = -1;
        private int currentColumns = 1;
        private Task currentIconLoadTask = Task.CompletedTask;
        private bool initialScrollPending = true;
        private bool initialContentSetupHeld;
        private bool applyingInitialScroll;
        public Task WhenAssetsReady => currentIconLoadTask;

        public event Action<ActionGridEntry> SelectionChanged;
        public event Action<ActionCommandResult> CommandCompleted;
        public event Action<int> CapacityChanged;
        public event Action<ActionGridEntry> EntryAdded;
        public event Action<ActionGridEntry> EntryRemoved;
        public event Action<IReadOnlyList<ActionGridEntry>> Overflowed;

        public int SelectedIndex => selectedIndex;
        public int CurrentColumns => currentColumns;
        public int Capacity => capacity;
        public int EntryCount => CountOccupiedEntries();
        public int VisibleCellCount => GetDisplayedCount();
        public ActionGridPopulationMode PopulationMode => populationMode;
        public ActionGridPackingMode PackingMode => packingMode;
        public ActionGridHorizontalAlignment GridAlignment => gridAlignment;
        public ActionGridHorizontalAlignment IncompleteRowAlignment => incompleteRowAlignment;
        public ActionGridVerticalPlacement VerticalPlacement => verticalPlacement;
        public bool IsInitialScrollPending => initialScrollPending;
        public bool HasAppliedInitialScroll => !initialScrollPending;
        public ScrollRect ScrollRect => scrollRect;
        public ActionGridLayoutMode LayoutMode => layoutMode;
        public int ConfiguredColumns => fixedColumns;
        public Vector2 MinimumCellSize => minimumCellSize;
        public Vector2 Spacing => spacing;
        public RectOffset Padding => padding;

        [Obsolete("Use GridAlignment and IncompleteRowAlignment.")]
        public ActionGridHorizontalAlignment SlotAlignment => gridAlignment;

        private void OnEnable()
        {
            padding ??= new RectOffset(8, 8, 8, 8);
            DisableConflictingContentSizeFitter();
            ResolveScrollbarController();
            if (scrollbarController != null)
            {
                scrollbarController.ViewportLayoutChanged -= OnViewportLayoutChanged;
                scrollbarController.ViewportLayoutChanged += OnViewportLayoutChanged;
            }
            ApplyLayout();
            RefreshScrollbarLayout();
            ScheduleInitialScrollResolution();
        }

        private void OnDisable()
        {
            if (scrollbarController != null)
            {
                scrollbarController.ViewportLayoutChanged -= OnViewportLayoutChanged;
            }
            Canvas.willRenderCanvases -= ResolveInitialScrollBeforeRender;
        }

        private void Awake()
        {
            if (content == null)
            {
                return;
            }

            DisableConflictingContentSizeFitter();

            var existingCells = content.GetComponentsInChildren<ActionGridCell>(true);
            foreach (var cell in existingCells)
            {
                if (cell != null && !cellPool.Contains(cell))
                {
                    cellPool.Add(cell);
                }
            }

            capacity = Mathf.Max(capacity, initialCapacity);
            EnsurePool(capacity);
            RefreshVisibleCells(string.Empty, 0, false);
        }

        private void OnDestroy()
        {
            Canvas.willRenderCanvases -= ResolveInitialScrollBeforeRender;
            CancelPendingCommand();
            ReleaseAssets();
        }

        public void SetAssetProvider(IAssetProvider provider)
        {
            ReleaseAssets();
            assetProvider = provider;
        }

        public void BeginInitialContentSetup()
        {
            if (!initialScrollPending) return;
            initialContentSetupHeld = true;
            Canvas.willRenderCanvases -= ResolveInitialScrollBeforeRender;
        }

        public bool CompleteInitialContentSetup()
        {
            if (!initialScrollPending) return true;
            initialContentSetupHeld = false;
            Canvas.ForceUpdateCanvases();
            ApplyLayout();
            RefreshScrollbarLayout();
            if (TryResolveInitialScroll()) return true;
            ScheduleInitialScrollResolution();
            return false;
        }

        public void CancelInitialContentSetup()
        {
            if (!initialScrollPending) return;
            initialContentSetupHeld = false;
            ScheduleInitialScrollResolution();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout();
            RefreshScrollbarLayout();
            ConfigureNavigation();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            padding ??= new RectOffset(8, 8, 8, 8);
            ApplyLayout();
        }
#endif

        public void SetGridAlignment(ActionGridHorizontalAlignment alignment)
        {
            gridAlignment = alignment;
            RebuildGridLayout();
        }

        public void SetIncompleteRowAlignment(ActionGridHorizontalAlignment alignment)
        {
            incompleteRowAlignment = alignment;
            RebuildGridLayout();
        }

        public void SetVerticalPlacement(ActionGridVerticalPlacement placement)
        {
            if (verticalPlacement == placement)
            {
                return;
            }

            verticalPlacement = placement;
            RebuildGridLayout();
            RefreshScrollbarLayout();
        }

        public void ConfigureLayout(
            ActionGridLayoutMode mode,
            int columns,
            Vector2 cellSize,
            Vector2 layoutSpacing,
            RectOffset layoutPadding,
            ActionGridHorizontalAlignment horizontalAlignment,
            ActionGridHorizontalAlignment trailingRowAlignment,
            ActionGridVerticalPlacement placement)
        {
            layoutMode = mode;
            fixedColumns = Mathf.Max(1, columns);
            minimumCellSize = new Vector2(Mathf.Max(1f, cellSize.x), Mathf.Max(1f, cellSize.y));
            maximumCellSize = mode == ActionGridLayoutMode.ExactColumns
                ? minimumCellSize
                : new Vector2(Mathf.Max(minimumCellSize.x, maximumCellSize.x), Mathf.Max(minimumCellSize.y, maximumCellSize.y));
            spacing = new Vector2(Mathf.Max(0f, layoutSpacing.x), Mathf.Max(0f, layoutSpacing.y));
            padding = layoutPadding == null
                ? new RectOffset()
                : new RectOffset(
                    Mathf.Max(0, layoutPadding.left), Mathf.Max(0, layoutPadding.right),
                    Mathf.Max(0, layoutPadding.top), Mathf.Max(0, layoutPadding.bottom));
            gridAlignment = horizontalAlignment;
            incompleteRowAlignment = trailingRowAlignment;
            verticalPlacement = placement;
            RebuildGridLayout();
            RefreshScrollbarLayout();
            ConfigureNavigation();
        }

        [Obsolete("Use SetGridAlignment and SetIncompleteRowAlignment.")]
        public void SetSlotAlignment(ActionGridHorizontalAlignment alignment)
        {
            gridAlignment = alignment;
            incompleteRowAlignment = alignment;
            RebuildGridLayout();
        }

        private void RebuildGridLayout()
        {
            ApplyLayout();
            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
        }

        public void ConfigureBehavior(
            ActionGridPopulationMode population,
            ActionGridPackingMode packing,
            GridActivationBehavior activation)
        {
            populationMode = population;
            packingMode = packing;
            activationBehavior = activation;
            InitializeVisibleSlots();
        }
        public void SetServices(IActionMenuProvider options, IActionCommandExecutor executor)
        {
            menuProvider = options;
            commandExecutor = executor;
        }

        public void SetEntries(IReadOnlyList<ActionGridEntry> newEntries, int slotCapacity = -1)
        {
            var selectedId = selectedIndex >= 0 && selectedIndex < entries.Count
                ? entries[selectedIndex].Id
                : string.Empty;
            entries.Clear();
            if (newEntries != null)
            {
                for (var i = 0; i < newEntries.Count; i++)
                {
                    entries.Add(newEntries[i]);
                }
            }

            if (slotCapacity >= 0)
            {
                capacity = slotCapacity;
            }
            capacity = Mathf.Max(capacity, entries.Count);
            capacity = Mathf.Max(capacity, entries.Count);

            if (packingMode == ActionGridPackingMode.CompactForward)
            {
                entries.RemoveAll(entry => string.IsNullOrEmpty(entry.EntryInstanceId));
            }

            RefreshVisibleCells(selectedId, 0, true);
        }

        public void InitializeVisibleSlots()
        {
            capacity = Mathf.Max(capacity, entries.Count);
            RefreshVisibleCells(
                IsOccupied(selectedIndex) ? entries[selectedIndex].EntryInstanceId : string.Empty,
                Mathf.Max(0, selectedIndex),
                false);
        }

        private void RefreshVisibleCells(
            string selectedId,
            int preferredIndex,
            bool loadIcons)
        {
            var displayedCount = GetDisplayedCount();
            EnsurePool(displayedCount);
            for (var i = 0; i < cellPool.Count; i++)
            {
                var active = i < displayedCount;
                cellPool[i].gameObject.SetActive(active);
                if (!active)
                {
                    cellPool[i].Unbind();
                    continue;
                }

                if (i < entries.Count && !string.IsNullOrEmpty(entries[i].EntryInstanceId))
                {
                    cellPool[i].Bind(i, entries[i], ActivateCell);
                }
                else
                {
                    cellPool[i].BindEmpty(i, ActivateCell);
                }
            }

            emptyState?.SetActive(CountOccupiedEntries() == 0 && displayedCount == 0);
            ApplyLayout();
            RefreshScrollbarLayout();
            ConfigureNavigation();
            selectedIndex = FindEntryIndex(selectedId);
            if (selectedIndex < 0)
            {
                selectedIndex = FindNearestOccupiedIndex(preferredIndex);
            }

            RefreshSelection(false);
            if (loadIcons && Application.isPlaying)
            {
                currentIconLoadTask = ObserveIconLoadsAsync(LoadIconsAsync());
            }
        }

        private int GetDisplayedCount()
        {
            return populationMode == ActionGridPopulationMode.FillCapacityWithEmptySlots
                ? Mathf.Max(entries.Count, capacity)
                : entries.Count;
        }

        public void Select(int index, bool moveFocus = true)
        {
            if (!IsOccupied(index))
            {
                return;
            }

            selectedIndex = index;
            RefreshSelection(moveFocus);
            SelectionChanged?.Invoke(entries[index]);
        }

        public CapacityChangeResult SetCapacity(
            int newCapacity,
            CapacityReductionPolicy reductionPolicy = CapacityReductionPolicy.RejectIfOccupied)
        {
            newCapacity = Mathf.Max(0, newCapacity);
            var previous = capacity;
            var overflow = new List<ActionGridEntry>();
            for (var i = newCapacity; i < entries.Count; i++)
            {
                if (!string.IsNullOrEmpty(entries[i].EntryInstanceId)) overflow.Add(entries[i]);
            }
            if (overflow.Count > 0 && reductionPolicy == CapacityReductionPolicy.RejectIfOccupied)
            {
                return new CapacityChangeResult(false, previous, previous, overflow.Count);
            }

            var selectedId = IsOccupied(selectedIndex) ? entries[selectedIndex].EntryInstanceId : string.Empty;
            if (entries.Count > newCapacity) entries.RemoveRange(newCapacity, entries.Count - newCapacity);
            capacity = newCapacity;
            EnsurePool(capacity);
            contextMenu?.Hide(false);
            Rebind(selectedId);
            if (overflow.Count > 0 && reductionPolicy == CapacityReductionPolicy.MoveOverflow) Overflowed?.Invoke(overflow);
            CapacityChanged?.Invoke(capacity);
            return new CapacityChangeResult(true, previous, capacity, overflow.Count);
        }

        public bool TryAddEntry(in ActionGridEntry entry)
        {
            if (string.IsNullOrEmpty(entry.EntryInstanceId) || FindEntryIndex(entry.EntryInstanceId) >= 0) return false;
            if (packingMode == ActionGridPackingMode.PreserveSlots)
            {
                var empty = entries.FindIndex(value => string.IsNullOrEmpty(value.EntryInstanceId));
                if (empty >= 0) entries[empty] = entry;
                else if (entries.Count < capacity) entries.Add(entry);
                else return false;
            }
            else
            {
                if (entries.Count >= capacity) return false;
                entries.Add(entry);
            }
            Rebind(entry.EntryInstanceId);
            EntryAdded?.Invoke(entry);
            return true;
        }

        public bool TryInsertEntry(int index, in ActionGridEntry entry)
        {
            if (index < 0 || index >= capacity || string.IsNullOrEmpty(entry.EntryInstanceId) ||
                FindEntryIndex(entry.EntryInstanceId) >= 0) return false;
            if (packingMode == ActionGridPackingMode.PreserveSlots)
            {
                while (entries.Count <= index) entries.Add(default);
                if (IsOccupied(index)) return false;
                entries[index] = entry;
            }
            else
            {
                if (entries.Count >= capacity) return false;
                entries.Insert(Mathf.Min(index, entries.Count), entry);
            }
            Rebind(entry.EntryInstanceId);
            EntryAdded?.Invoke(entry);
            return true;
        }

        public bool RemoveEntry(string entryInstanceId)
        {
            var index = FindEntryIndex(entryInstanceId);
            if (index < 0) return false;
            var removed = entries[index];
            if (packingMode == ActionGridPackingMode.CompactForward) entries.RemoveAt(index);
            else entries[index] = default;
            contextMenu?.Hide(false);
            Rebind(string.Empty, index);
            EntryRemoved?.Invoke(removed);
            return true;
        }

        public bool UpdateEntry(in ActionGridEntry replacement)
        {
            var index = FindEntryIndex(replacement.EntryInstanceId);
            if (index < 0) return false;
            entries[index] = replacement;
            Rebind(replacement.EntryInstanceId);
            return true;
        }

        public bool ReplaceEntry(string entryInstanceId, in ActionGridEntry replacement)
        {
            var index = FindEntryIndex(entryInstanceId);
            if (index < 0 || string.IsNullOrEmpty(replacement.EntryInstanceId)) return false;
            var duplicate = FindEntryIndex(replacement.EntryInstanceId);
            if (duplicate >= 0 && duplicate != index) return false;
            entries[index] = replacement;
            Rebind(replacement.EntryInstanceId);
            return true;
        }

        public void ClearEntries()
        {
            entries.Clear();
            contextMenu?.Hide(false);
            Rebind(string.Empty);
        }

        public void CloseContextMenu()
        {
            contextMenu?.Hide();
        }

        public void ScrollToTop()
        {
            ApplyLayout();
            RefreshScrollbarLayout();
            ResetScrollToTop();
        }

        private void ActivateCell(int index)
        {
            if (!IsOccupied(index))
            {
                return;
            }

            Select(index, false);
            switch (activationBehavior)
            {
                case GridActivationBehavior.OpenContextMenu:
                    OpenContextMenu();
                    break;
                case GridActivationBehavior.ExecuteDefaultAction:
                    ExecuteDefaultAction();
                    break;
            }
        }

        private void OpenContextMenu()
        {
            if (selectedIndex < 0 || menuProvider == null || contextMenu == null)
            {
                return;
            }

            var entry = entries[selectedIndex];
            var options = menuProvider.GetOptions(entry.Id);
            var cell = cellPool[selectedIndex];
            contextMenu.Show(options, ExecuteOption, cell.transform as RectTransform, cell.gameObject);
        }

        private void ExecuteDefaultAction()
        {
            if (selectedIndex < 0 || menuProvider == null)
            {
                return;
            }

            var options = menuProvider.GetOptions(entries[selectedIndex].Id);
            if (options == null)
            {
                return;
            }

            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].IsEnabled)
                {
                    ExecuteOption(options[i]);
                    return;
                }
            }
        }

        private async void ExecuteOption(ActionMenuOption option)
        {
            if (selectedIndex < 0 || commandExecutor == null || !option.IsEnabled)
            {
                return;
            }

            contextMenu?.Hide(false);
            CancelPendingCommand();
            var entryId = entries[selectedIndex].Id;
            var cancellation = new CancellationTokenSource();
            commandCancellation = cancellation;
            try
            {
                var result = await commandExecutor.ExecuteAsync(
                    entryId,
                    option.CommandId,
                    cancellation.Token);
                CommandCompleted?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                cancellation.Dispose();
                if (ReferenceEquals(commandCancellation, cancellation))
                {
                    commandCancellation = null;
                }

                RefreshSelection(true);
            }
        }

        private void EnsurePool(int count)
        {
            while (cellPool.Count < count)
            {
                var cell = Instantiate(cellPrefab, content);
                cell.gameObject.SetActive(false);
                cellPool.Add(cell);
            }
        }

        private void ApplyLayout()
        {
            if (viewport == null || content == null || gridLayout == null)
            {
                return;
            }

            padding ??= new RectOffset();
            var availableWidth = Mathf.Max(1f, viewport.rect.width - padding.horizontal);
            currentColumns = CalculateColumnCount(
                layoutMode,
                fixedColumns,
                availableWidth,
                minimumCellSize.x,
                spacing.x);
            var width = layoutMode == ActionGridLayoutMode.ExactColumns
                ? minimumCellSize.x
                : (availableWidth - spacing.x * (currentColumns - 1)) / currentColumns;
            width = Mathf.Clamp(width, minimumCellSize.x, maximumCellSize.x);
            var aspect = minimumCellSize.x > 0f ? minimumCellSize.y / minimumCellSize.x : 1f;

            var cellHeight = Mathf.Clamp(
                width * aspect,
                minimumCellSize.y,
                maximumCellSize.y);
            var viewportHeight = Mathf.Max(0f, viewport.rect.height);
            var requiredGridHeight = CalculateRequiredGridHeight(
                GetDisplayedCount(),
                currentColumns,
                cellHeight,
                spacing.y,
                padding);
            var contentFits = requiredGridHeight <= viewportHeight + LayoutEpsilon;
            var wasOverflowing = content.rect.height > viewportHeight + LayoutEpsilon;
            var targetContentHeight = contentFits ? viewportHeight : requiredGridHeight;

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = currentColumns;
            gridLayout.cellSize = new Vector2(width, cellHeight);
            gridLayout.spacing = spacing;
            gridLayout.padding = padding;
            gridLayout.childAlignment = ResolveChildAlignment(
                gridAlignment,
                verticalPlacement == ActionGridVerticalPlacement.CenterWhenContentFits && contentFits);
            if (gridLayout is ActionGridLayoutGroup alignedGrid)
            {
                alignedGrid.IncompleteRowAlignment = incompleteRowAlignment;
            }

            if (!Mathf.Approximately(content.rect.height, targetContentHeight))
            {
                content.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    targetContentHeight);
            }

            if (wasOverflowing && contentFits)
            {
                ResetScrollToTop();
            }
        }

        private static TextAnchor ResolveChildAlignment(
            ActionGridHorizontalAlignment horizontalAlignment,
            bool centerVertically)
        {
            if (centerVertically)
            {
                return horizontalAlignment switch
                {
                    ActionGridHorizontalAlignment.Left => TextAnchor.MiddleLeft,
                    ActionGridHorizontalAlignment.Right => TextAnchor.MiddleRight,
                    _ => TextAnchor.MiddleCenter
                };
            }

            return horizontalAlignment switch
            {
                ActionGridHorizontalAlignment.Left => TextAnchor.UpperLeft,
                ActionGridHorizontalAlignment.Right => TextAnchor.UpperRight,
                _ => TextAnchor.UpperCenter
            };
        }

        private void ResetScrollToTop()
        {
            if (scrollRect != null)
            {
                scrollRect.StopMovement();
                scrollRect.verticalNormalizedPosition = 1f;
            }

            var anchoredPosition = content.anchoredPosition;
            if (Mathf.Abs(anchoredPosition.y) <= LayoutEpsilon)
            {
                return;
            }

            anchoredPosition.y = 0f;
            content.anchoredPosition = anchoredPosition;
        }

        private void ScheduleInitialScrollResolution()
        {
            if (!isActiveAndEnabled || !initialScrollPending || initialContentSetupHeld) return;
            Canvas.willRenderCanvases -= ResolveInitialScrollBeforeRender;
            Canvas.willRenderCanvases += ResolveInitialScrollBeforeRender;
        }

        private void ResolveInitialScrollBeforeRender()
        {
            if (TryResolveInitialScroll())
                Canvas.willRenderCanvases -= ResolveInitialScrollBeforeRender;
        }

        private bool TryResolveInitialScroll()
        {
            if (!initialScrollPending || initialContentSetupHeld || applyingInitialScroll) return !initialScrollPending;
            if (viewport == null || content == null || viewport.rect.width <= LayoutEpsilon || viewport.rect.height <= LayoutEpsilon) return false;
            applyingInitialScroll = true;
            try
            {
                ApplyLayout();
                if (content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                scrollbarController?.Refresh();
                var overflowing = content.rect.height > viewport.rect.height + LayoutEpsilon;
                if (overflowing) ResetScrollToTop();
                initialScrollPending = false;
                return true;
            }
            finally { applyingInitialScroll = false; }
        }

        private void DisableConflictingContentSizeFitter()
        {
            if (content != null && content.TryGetComponent<ContentSizeFitter>(out var fitter))
            {
                fitter.enabled = false;
            }
        }

        private void ResolveScrollbarController()
        {
            if (scrollbarController == null && scrollRect != null)
            {
                scrollbarController = scrollRect.GetComponent<ConfigurableScrollbarController>();
            }
        }

        private void RefreshScrollbarLayout()
        {
            ResolveScrollbarController();
            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
            scrollbarController?.Refresh();
        }

        private void OnViewportLayoutChanged()
        {
            ApplyLayout();
            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
        }

        public static int CalculateColumnCount(
            ActionGridLayoutMode mode,
            int maximumColumns,
            float availableWidth,
            float minimumCellWidth,
            float horizontalSpacing)
        {
            var safeMinimumWidth = Mathf.Max(1f, minimumCellWidth);
            var safeSpacing = Mathf.Max(0f, horizontalSpacing);
            var columnsThatFit = Mathf.Max(1, Mathf.FloorToInt(
                (Mathf.Max(1f, availableWidth) + safeSpacing) /
                (safeMinimumWidth + safeSpacing)));

            return mode switch
            {
                ActionGridLayoutMode.ExactColumns => Mathf.Max(1, maximumColumns),
                ActionGridLayoutMode.FixedColumns => Mathf.Min(Mathf.Max(1, maximumColumns), columnsThatFit),
                _ => columnsThatFit
            };
        }

        public static float CalculateRequiredGridHeight(
            int visibleCellCount,
            int columns,
            float cellHeight,
            float verticalSpacing,
            RectOffset layoutPadding)
        {
            columns = Mathf.Max(1, columns);
            visibleCellCount = Mathf.Max(0, visibleCellCount);
            var rows = Mathf.CeilToInt(visibleCellCount / (float)columns);
            var paddingHeight = layoutPadding?.vertical ?? 0;
            return paddingHeight +
                   rows * Mathf.Max(0f, cellHeight) +
                   Mathf.Max(0, rows - 1) * Mathf.Max(0f, verticalSpacing);
        }

        private void ConfigureNavigation()
        {
            var activeCells = 0;
            while (activeCells < cellPool.Count && cellPool[activeCells].gameObject.activeSelf)
            {
                activeCells++;
            }

            for (var i = 0; i < activeCells; i++)
            {
                var navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnLeft = i % currentColumns > 0 ? cellPool[i - 1].Button : null;
                navigation.selectOnRight = i % currentColumns < currentColumns - 1 && i + 1 < activeCells
                    ? cellPool[i + 1].Button
                    : null;
                navigation.selectOnUp = i - currentColumns >= 0 ? cellPool[i - currentColumns].Button : null;
                navigation.selectOnDown = i + currentColumns < activeCells ? cellPool[i + currentColumns].Button : null;
                cellPool[i].Button.navigation = navigation;
            }
        }

        private void RefreshSelection(bool moveFocus)
        {
            for (var i = 0; i < cellPool.Count; i++)
            {
                cellPool[i].SetSelected(i == selectedIndex);
            }

            if (moveFocus && selectedIndex >= 0 && selectedIndex < cellPool.Count && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(cellPool[selectedIndex].gameObject);
            }
        }

        private int FindEntryIndex(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }

            return entries.FindIndex(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        }

        private bool IsOccupied(int index)
        {
            return index >= 0 && index < entries.Count && !string.IsNullOrEmpty(entries[index].EntryInstanceId);
        }

        private int CountOccupiedEntries()
        {
            var count = 0;
            for (var i = 0; i < entries.Count; i++) if (!string.IsNullOrEmpty(entries[i].EntryInstanceId)) count++;
            return count;
        }

        private int FindNearestOccupiedIndex(int preferred)
        {
            if (CountOccupiedEntries() == 0) return -1;
            for (var i = Mathf.Clamp(preferred, 0, entries.Count - 1); i < entries.Count; i++) if (IsOccupied(i)) return i;
            for (var i = Mathf.Min(preferred - 1, entries.Count - 1); i >= 0; i--) if (IsOccupied(i)) return i;
            return -1;
        }

        private void Rebind(string selectedId, int preferredIndex = 0)
        {
            SetEntries(entries.ToArray(), capacity);
            var restored = FindEntryIndex(selectedId);
            selectedIndex = restored >= 0 ? restored : FindNearestOccupiedIndex(preferredIndex);
            RefreshSelection(false);
        }

        private void CancelPendingCommand()
        {
            commandCancellation?.Cancel();
            commandCancellation?.Dispose();
            commandCancellation = null;
        }

        public async Task LoadIconsAsync(CancellationToken cancellationToken = default)
        {
            ReleaseAssets();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            assetCancellation = cancellation;
            var scope = new AssetScope(assetProvider);
            assetScope = scope;
            try
            {
                var uniqueAssetIds = new List<string>();
                var seenAssetIds = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (string.IsNullOrEmpty(entry.EntryInstanceId) || string.IsNullOrWhiteSpace(entry.IconAssetId))
                    {
                        continue;
                    }
                    if (seenAssetIds.Add(entry.IconAssetId))
                    {
                        uniqueAssetIds.Add(entry.IconAssetId);
                    }
                }

                using var concurrency = new SemaphoreSlim(Mathf.Clamp(maxConcurrentIconLoads, 1, 8));
                var loads = new List<Task>(uniqueAssetIds.Count);
                for (var i = 0; i < uniqueAssetIds.Count; i++)
                {
                    loads.Add(LoadAndApplyIconAsync(
                        uniqueAssetIds[i],
                        scope,
                        concurrency,
                        cancellation.Token));
                }

                await Task.WhenAll(loads);
            }
            finally
            {
                if (ReferenceEquals(assetCancellation, cancellation))
                {
                    assetCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private async Task LoadAndApplyIconAsync(
            string assetId,
            AssetScope scope,
            SemaphoreSlim concurrency,
            CancellationToken cancellationToken)
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                AssetLease<Sprite> lease;
                try
                {
                    lease = await scope.LoadAsync<Sprite>(assetId, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Action grid icon '{assetId}' could not be loaded: {exception.Message}", this);
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                for (var i = 0; i < entries.Count; i++)
                {
                    if (!string.Equals(entries[i].IconAssetId, assetId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    entries[i] = entries[i].WithIcon(lease.Asset);
                    if (i < cellPool.Count && cellPool[i].gameObject.activeSelf)
                    {
                        cellPool[i].Bind(i, entries[i], ActivateCell);
                        cellPool[i].SetSelected(i == selectedIndex);
                    }
                }
            }
            finally
            {
                concurrency.Release();
            }
        }

        private async Task ObserveIconLoadsAsync(Task loadTask)
        {
            try
            {
                await loadTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Action grid icon loading stopped unexpectedly: {exception.Message}", this);
            }
        }

        private void ReleaseAssets()
        {
            assetCancellation?.Cancel();
            assetCancellation?.Dispose();
            assetCancellation = null;
            for (var i = 0; i < cellPool.Count; i++)
            {
                if (cellPool[i] != null && cellPool[i].HasEntry)
                {
                    cellPool[i].ClearIcon();
                }
            }
            for (var i = 0; i < entries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(entries[i].IconAssetId))
                {
                    entries[i] = entries[i].WithIcon(null);
                }
            }
            assetScope?.Dispose();
            assetScope = null;
        }
    }
}
