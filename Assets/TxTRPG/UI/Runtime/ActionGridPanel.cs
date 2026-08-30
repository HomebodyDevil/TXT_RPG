using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public sealed class ActionGridPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ActionGridCell cellPrefab;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private ActionContextMenu contextMenu;

        [Header("Layout")]
        [SerializeField] private ActionGridLayoutMode layoutMode = ActionGridLayoutMode.FixedColumns;
        [SerializeField, Min(1)] private int fixedColumns = 5;
        [SerializeField] private Vector2 minimumCellSize = new(72f, 72f);
        [SerializeField] private Vector2 maximumCellSize = new(128f, 128f);
        [SerializeField] private Vector2 spacing = new(8f, 8f);
        [SerializeField] private RectOffset padding;

        [Header("Behavior")]
        [SerializeField] private GridActivationBehavior activationBehavior = GridActivationBehavior.OpenContextMenu;
        [SerializeField] private ActionGridPopulationMode populationMode = ActionGridPopulationMode.EntriesOnly;
        [SerializeField, Min(0)] private int capacity;

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

        public event Action<ActionGridEntry> SelectionChanged;
        public event Action<ActionCommandResult> CommandCompleted;

        public int SelectedIndex => selectedIndex;
        public int CurrentColumns => currentColumns;

        private void OnEnable()
        {
            padding ??= new RectOffset(8, 8, 8, 8);
            ApplyLayout();
        }

        private void Awake()
        {
            if (content == null)
            {
                return;
            }

            var existingCells = content.GetComponentsInChildren<ActionGridCell>(true);
            foreach (var cell in existingCells)
            {
                if (cell != null && !cellPool.Contains(cell))
                {
                    cellPool.Add(cell);
                }
            }
        }

        private void OnDestroy()
        {
            CancelPendingCommand();
            ReleaseAssets();
        }

        public void SetAssetProvider(IAssetProvider provider)
        {
            ReleaseAssets();
            assetProvider = provider;
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout();
            ConfigureNavigation();
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

            var displayedCount = populationMode == ActionGridPopulationMode.FillCapacityWithEmptySlots
                ? Mathf.Max(entries.Count, capacity)
                : entries.Count;
            EnsurePool(displayedCount);
            for (var i = 0; i < cellPool.Count; i++)
            {
                var active = i < displayedCount;
                cellPool[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                if (i < entries.Count)
                {
                    cellPool[i].Bind(i, entries[i], ActivateCell);
                }
                else
                {
                    cellPool[i].BindEmpty(i, ActivateCell);
                }
            }

            emptyState?.SetActive(entries.Count == 0 && displayedCount == 0);
            ApplyLayout();
            ConfigureNavigation();
            selectedIndex = FindEntryIndex(selectedId);
            if (selectedIndex < 0 && entries.Count > 0)
            {
                selectedIndex = 0;
            }

            RefreshSelection(false);
            if (Application.isPlaying)
            {
                LoadIconsAsync();
            }
        }

        public void Select(int index, bool moveFocus = true)
        {
            if (index < 0 || index >= entries.Count)
            {
                return;
            }

            selectedIndex = index;
            RefreshSelection(moveFocus);
            SelectionChanged?.Invoke(entries[index]);
        }

        public void CloseContextMenu()
        {
            contextMenu?.Hide();
        }

        private void ActivateCell(int index)
        {
            if (index < 0 || index >= entries.Count)
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
            if (viewport == null || gridLayout == null)
            {
                return;
            }

            var availableWidth = Mathf.Max(1f, viewport.rect.width - padding.horizontal);
            currentColumns = CalculateColumnCount(
                layoutMode,
                fixedColumns,
                availableWidth,
                minimumCellSize.x,
                spacing.x);
            var width = (availableWidth - spacing.x * (currentColumns - 1)) / currentColumns;
            width = Mathf.Clamp(width, minimumCellSize.x, maximumCellSize.x);
            var aspect = minimumCellSize.x > 0f ? minimumCellSize.y / minimumCellSize.x : 1f;

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = currentColumns;
            gridLayout.cellSize = new Vector2(
                width,
                Mathf.Clamp(width * aspect, minimumCellSize.y, maximumCellSize.y));
            gridLayout.spacing = spacing;
            gridLayout.padding = padding;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
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

            return mode == ActionGridLayoutMode.FixedColumns
                ? Mathf.Min(Mathf.Max(1, maximumColumns), columnsThatFit)
                : columnsThatFit;
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

        private void CancelPendingCommand()
        {
            commandCancellation?.Cancel();
            commandCancellation?.Dispose();
            commandCancellation = null;
        }

        private async void LoadIconsAsync()
        {
            ReleaseAssets();
            var cancellation = new CancellationTokenSource();
            assetCancellation = cancellation;
            var scope = new AssetScope(assetProvider);
            assetScope = scope;
            try
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (string.IsNullOrWhiteSpace(entry.IconAssetId))
                    {
                        continue;
                    }

                    var lease = await scope.LoadAsync<Sprite>(entry.IconAssetId, cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (i >= entries.Count || !string.Equals(entries[i].Id, entry.Id, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    entries[i] = entry.WithIcon(lease.Asset);
                    if (i < cellPool.Count && cellPool[i].gameObject.activeSelf)
                    {
                        cellPool[i].Bind(i, entries[i], ActivateCell);
                        cellPool[i].SetSelected(i == selectedIndex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Action grid icons could not be loaded: {exception.Message}", this);
            }
        }

        private void ReleaseAssets()
        {
            assetCancellation?.Cancel();
            assetCancellation?.Dispose();
            assetCancellation = null;
            assetScope?.Dispose();
            assetScope = null;
        }
    }
}
