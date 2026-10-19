using System;
using UnityEngine;

namespace TxTRPG.UI.Runes
{
    // Rune grid sizing in Canvas units. Values are clamped when read, so an invalid Inspector value never breaks the layout.
    [Serializable]
    public sealed class RuneGridLayoutSettings
    {
        public const float DefaultMinimumCardSize = 64f;
        public const float DefaultMaximumCardSize = 120f;
        public const float DefaultSpacing = 8f;
        public const int DefaultPadding = 8;
        public const float DefaultBadgeSizeRatio = .33f;
        public const float DefaultBadgeOverhangRatio = .13f;

        [SerializeField, Tooltip("Smallest card side. Cards only get smaller when the grid is narrower than one cell.")]
        private float minimumCardSize = DefaultMinimumCardSize;
        [SerializeField, Tooltip("Largest card side. Extra width on a row stays empty on the right.")]
        private float maximumCardSize = DefaultMaximumCardSize;
        [SerializeField, Tooltip("Gap between cells. Keep it above 0 so outline or glow effects have room.")]
        private float spacing = DefaultSpacing;
        [SerializeField] private RuneGridPadding padding = new(DefaultPadding);
        [SerializeField, Range(0f, 1f), Tooltip("Badge side as a fraction of the card side.")]
        private float badgeSizeRatio = DefaultBadgeSizeRatio;
        [SerializeField, Range(0f, 1f), Tooltip("How far the badge sticks out past the card's left and top edges, as a fraction of the card side.")]
        private float badgeOverhangRatio = DefaultBadgeOverhangRatio;

        public RuneGridLayoutSettings() { }

        public RuneGridLayoutSettings(float minimumCard, float maximumCard, float gap, RuneGridPadding inset, float badgeRatio, float overhangRatio)
        {
            minimumCardSize = minimumCard;
            maximumCardSize = maximumCard;
            spacing = gap;
            padding = inset;
            badgeSizeRatio = badgeRatio;
            badgeOverhangRatio = overhangRatio;
        }

        public float MinimumCardSize => Mathf.Max(1f, Finite(minimumCardSize, DefaultMinimumCardSize));
        public float MaximumCardSize => Mathf.Max(MinimumCardSize, Finite(maximumCardSize, DefaultMaximumCardSize));
        public float Spacing => Mathf.Max(0f, Finite(spacing, DefaultSpacing));
        public int PaddingLeft => Mathf.Max(0, padding.Left);
        public int PaddingRight => Mathf.Max(0, padding.Right);
        public int PaddingTop => Mathf.Max(0, padding.Top);
        public int PaddingBottom => Mathf.Max(0, padding.Bottom);
        public float BadgeSizeRatio => Mathf.Clamp01(Finite(badgeSizeRatio, DefaultBadgeSizeRatio));
        public float BadgeOverhangRatio => Mathf.Clamp01(Finite(badgeOverhangRatio, DefaultBadgeOverhangRatio));

        private static float Finite(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }

    // Grid inset in Canvas units. A plain struct instead of RectOffset, whose setters Unity forbids in field initializers.
    [Serializable]
    public struct RuneGridPadding
    {
        [SerializeField] private int left;
        [SerializeField] private int right;
        [SerializeField] private int top;
        [SerializeField] private int bottom;

        public RuneGridPadding(int all) : this(all, all, all, all) { }
        public RuneGridPadding(int leftInset, int rightInset, int topInset, int bottomInset)
        {
            left = leftInset;
            right = rightInset;
            top = topInset;
            bottom = bottomInset;
        }

        public int Left => left;
        public int Right => right;
        public int Top => top;
        public int Bottom => bottom;
    }

    public readonly struct RuneGridLayoutResult
    {
        public RuneGridLayoutResult(int columns, int rows, float cellSize, float cardSize, float badgeSize, float badgeOverhang, float spacing, float contentHeight)
        {
            Columns = columns;
            Rows = rows;
            CellSize = cellSize;
            CardSize = cardSize;
            BadgeSize = badgeSize;
            BadgeOverhang = badgeOverhang;
            Spacing = spacing;
            ContentHeight = contentHeight;
        }

        public int Columns { get; }
        public int Rows { get; }
        // Cell = card + badge overhang; the card sits at the cell's bottom right and the badge at its top left.
        public float CellSize { get; }
        public float CardSize { get; }
        public float BadgeSize { get; }
        public float BadgeOverhang { get; }
        public float Spacing { get; }
        public float ContentHeight { get; }
        public float UsedWidth => Columns * CellSize + Mathf.Max(0, Columns - 1) * Spacing;
    }

    // Pure column and size calculation (no Unity objects). Guarantees UsedWidth <= availableWidth for any width, so the
    // grid never needs horizontal scrolling; extra rows scroll vertically.
    public static class RuneGridLayoutMath
    {
        public static RuneGridLayoutResult Calculate(float availableWidth, int count, RuneGridLayoutSettings settings)
        {
            settings ??= new RuneGridLayoutSettings();
            count = Mathf.Max(0, count);
            var gap = settings.Spacing;
            var overhang = settings.BadgeOverhangRatio;
            int columns;
            float cell;
            if (float.IsNaN(availableWidth) || availableWidth <= 0f)
            {
                columns = 1;
                cell = 0f;
            }
            else
            {
                var minimumCell = settings.MinimumCardSize * (1f + overhang);
                var maximumCell = settings.MaximumCardSize * (1f + overhang);
                if (float.IsPositiveInfinity(availableWidth)) availableWidth = maximumCell;
                if (availableWidth < minimumCell)
                {
                    columns = 1;
                    cell = availableWidth;
                }
                else
                {
                    var fit = Mathf.Max(1, Mathf.FloorToInt((availableWidth + gap) / (minimumCell + gap)));
                    var cap = Mathf.Max(1, Mathf.CeilToInt((availableWidth + gap) / (maximumCell + gap)));
                    columns = Mathf.Min(cap, fit);
                    cell = Mathf.Min(maximumCell, (availableWidth - gap * (columns - 1)) / columns);
                }
            }
            var card = cell / (1f + overhang);
            var rows = count == 0 ? 0 : (count + columns - 1) / columns;
            var height = settings.PaddingTop + settings.PaddingBottom + rows * cell + Mathf.Max(0, rows - 1) * gap;
            return new RuneGridLayoutResult(columns, rows, cell, card, card * settings.BadgeSizeRatio, card * overhang, gap, height);
        }
    }
}
