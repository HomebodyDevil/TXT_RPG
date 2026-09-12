using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.Content.Items;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEngine;

namespace TxTRPG.Application.Items
{
    public enum InventoryDisplayMode { VerticalScroll, Paged }
    public enum InventoryColumnPolicy { Exact, AdaptiveUpToConfigured }

    [Serializable]
    public sealed class GridContentLayoutSettings : IModalContentConfiguration
    {
        public const int MaximumSlots = 512;
        public const int MaximumColumns = 32;
        public const float MaximumCellDimension = 1024f;
        public const float MaximumSpacing = 256f;
        public const int MaximumPadding = 512;
        public InventoryDisplayMode displayMode = InventoryDisplayMode.Paged;
        [Min(1)] public int slotsPerPage = 12;
        [Min(0)] public int minimumScrollSlots = 12;
        public bool fillPageWithEmptySlots;
        [Min(1)] public int columns = 4;
        public InventoryColumnPolicy columnPolicy = InventoryColumnPolicy.AdaptiveUpToConfigured;
        public Vector2 cellSize = new(72f, 72f);
        public Vector2 spacing = new(8f, 8f);
        public RectOffset padding;
        public ActionGridHorizontalAlignment alignment = ActionGridHorizontalAlignment.Center;
        public ActionGridHorizontalAlignment incompleteRowAlignment = ActionGridHorizontalAlignment.Left;
        public ActionGridVerticalPlacement verticalPlacement = ActionGridVerticalPlacement.Top;

        public void Normalize()
        {
            displayMode = Enum.IsDefined(typeof(InventoryDisplayMode), displayMode) ? displayMode : InventoryDisplayMode.Paged;
            columnPolicy = Enum.IsDefined(typeof(InventoryColumnPolicy), columnPolicy) ? columnPolicy : InventoryColumnPolicy.AdaptiveUpToConfigured;
            slotsPerPage = Math.Min(MaximumSlots, Math.Max(1, slotsPerPage));
            minimumScrollSlots = Math.Min(MaximumSlots, Math.Max(0, minimumScrollSlots));
            columns = Math.Min(MaximumColumns, Math.Max(1, columns));
            cellSize = new Vector2(SafeFinite(cellSize.x, 72f, 1f, MaximumCellDimension), SafeFinite(cellSize.y, 72f, 1f, MaximumCellDimension));
            spacing = new Vector2(SafeFinite(spacing.x, 8f, 0f, MaximumSpacing), SafeFinite(spacing.y, 8f, 0f, MaximumSpacing));
            padding ??= new RectOffset();
            padding.left = Math.Min(MaximumPadding, Math.Max(0, padding.left));
            padding.right = Math.Min(MaximumPadding, Math.Max(0, padding.right));
            padding.top = Math.Min(MaximumPadding, Math.Max(0, padding.top));
            padding.bottom = Math.Min(MaximumPadding, Math.Max(0, padding.bottom));
            alignment = Enum.IsDefined(typeof(ActionGridHorizontalAlignment), alignment) ? alignment : ActionGridHorizontalAlignment.Center;
            incompleteRowAlignment = Enum.IsDefined(typeof(ActionGridHorizontalAlignment), incompleteRowAlignment) ? incompleteRowAlignment : ActionGridHorizontalAlignment.Left;
            verticalPlacement = Enum.IsDefined(typeof(ActionGridVerticalPlacement), verticalPlacement) ? verticalPlacement : ActionGridVerticalPlacement.Top;
        }

        public static GridContentLayoutSettings CreateSafeCopy(GridContentLayoutSettings source, out bool usedFallback)
        {
            usedFallback = source == null;
            source ??= new GridContentLayoutSettings();
            var copy = new GridContentLayoutSettings
            {
                displayMode = source.displayMode,
                slotsPerPage = source.slotsPerPage,
                minimumScrollSlots = source.minimumScrollSlots,
                fillPageWithEmptySlots = source.fillPageWithEmptySlots,
                columns = source.columns,
                columnPolicy = source.columnPolicy,
                cellSize = source.cellSize,
                spacing = source.spacing,
                padding = source.padding == null ? null : new RectOffset(source.padding.left, source.padding.right, source.padding.top, source.padding.bottom),
                alignment = source.alignment,
                incompleteRowAlignment = source.incompleteRowAlignment,
                verticalPlacement = source.verticalPlacement
            };
            var before = copy.Snapshot();
            copy.Normalize();
            usedFallback |= before != copy.Snapshot();
            return copy;
        }

        public IModalContentConfiguration CloneForRequest() => CreateSafeCopy(this, out _);

        private string Snapshot() => $"{(int)displayMode}|{slotsPerPage}|{minimumScrollSlots}|{columns}|{(int)columnPolicy}|{cellSize}|{spacing}|{padding?.left},{padding?.right},{padding?.top},{padding?.bottom}|{(int)alignment}|{(int)incompleteRowAlignment}|{(int)verticalPlacement}";
        private static float SafeFinite(float value, float fallback, float minimum, float maximum) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Min(maximum, Math.Max(minimum, value));

        public int CalculateDisplayCapacity(int itemCount)
        {
            Normalize();
            if (displayMode == InventoryDisplayMode.VerticalScroll)
                return Math.Max(Math.Max(0, itemCount), minimumScrollSlots);
            return fillPageWithEmptySlots ? slotsPerPage : Math.Max(0, itemCount);
        }
    }

    [Serializable]
    public sealed class InventoryCategoryDefinition
    {
        public string id = "misc";
        public string displayName = "Misc";
        public int order;
    }

    public readonly struct InventoryProjection
    {
        public InventoryProjection(IReadOnlyList<ItemDefinition> items, int pageIndex, int pageCount)
        { Items = items; PageIndex = pageIndex; PageCount = pageCount; }
        public IReadOnlyList<ItemDefinition> Items { get; }
        public int PageIndex { get; }
        public int PageCount { get; }
    }

    public static class InventoryProjectionBuilder
    {
        public const string AllCategoryId = "all";
        public const string MiscCategoryId = "misc";

        public static InventoryProjection Build(IEnumerable<ItemDefinition> definitions,
            Func<string, int> quantity, string categoryId, InventoryDisplayMode mode,
            int pageIndex, int itemsPerPage, IEnumerable<string> knownCategoryIds = null)
        {
            if (quantity == null) throw new ArgumentNullException(nameof(quantity));
            if (mode == InventoryDisplayMode.Paged && itemsPerPage < 1)
                throw new ArgumentOutOfRangeException(nameof(itemsPerPage));
            var category = string.IsNullOrWhiteSpace(categoryId) ? AllCategoryId : categoryId.Trim();
            var knownCategories = knownCategoryIds == null ? null : new HashSet<string>(knownCategoryIds, StringComparer.Ordinal);
            var filtered = (definitions ?? Array.Empty<ItemDefinition>())
                .Where(item => item != null && quantity(item.DefinitionId) > 0)
                .Where(item => category == AllCategoryId || string.Equals(
                    knownCategories != null && !knownCategories.Contains(item.CategoryId) ? MiscCategoryId : item.CategoryId,
                    category, StringComparison.Ordinal))
                .OrderBy(item => item.DisplayNameLocalizationKey, StringComparer.Ordinal)
                .ThenBy(item => item.DefinitionId, StringComparer.Ordinal)
                .ToArray();
            if (mode == InventoryDisplayMode.VerticalScroll)
                return new InventoryProjection(filtered, 0, 1);
            var pageCount = Math.Max(1, (filtered.Length + itemsPerPage - 1) / itemsPerPage);
            var validPage = Math.Max(0, Math.Min(pageIndex, pageCount - 1));
            return new InventoryProjection(filtered.Skip(validPage * itemsPerPage).Take(itemsPerPage).ToArray(), validPage, pageCount);
        }
    }
}
