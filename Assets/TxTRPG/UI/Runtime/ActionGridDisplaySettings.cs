using System;
using UnityEngine;

namespace TxTRPG.UI
{
    public enum ActionGridDisplayMode { Manual = 0, Balanced = 1, DistributedSpacing = 2, LargeSlots = 3 }

    [Serializable]
    public sealed class ActionGridDisplayPreset
    {
        [Range(1, 32)] public int maximumColumns = 6;
        public Vector2 minimumCellSize = new(56, 56);
        public Vector2 maximumCellSize = new(72, 72);
        public Vector2 spacing = new(16, 16);
        public RectOffset padding;
        public ActionGridHorizontalAlignment alignment = ActionGridHorizontalAlignment.Center;
        public ActionGridHorizontalAlignment incompleteRowAlignment = ActionGridHorizontalAlignment.Left;
        public ActionGridVerticalPlacement verticalPlacement = ActionGridVerticalPlacement.Top;
        [Tooltip("Cell size in Canvas UI units; used by Distributed Spacing.")]
        public Vector2 targetCellSize = new(64, 64);
        public float minimumHorizontalGap = 12;
        public float maximumHorizontalGap = 32;

        public ActionGridDisplayPreset Copy() => new()
        {
            maximumColumns = maximumColumns, minimumCellSize = minimumCellSize, maximumCellSize = maximumCellSize,
            spacing = spacing, padding = padding == null ? null : new RectOffset(padding.left, padding.right, padding.top, padding.bottom),
            alignment = alignment, incompleteRowAlignment = incompleteRowAlignment, verticalPlacement = verticalPlacement,
            targetCellSize = targetCellSize, minimumHorizontalGap = minimumHorizontalGap, maximumHorizontalGap = maximumHorizontalGap
        };
        public void Normalize()
        {
            maximumColumns = Mathf.Clamp(maximumColumns, 1, 32);
            minimumCellSize = SafeSize(minimumCellSize, Vector2.one * 56);
            maximumCellSize = Vector2.Max(minimumCellSize, SafeSize(maximumCellSize, Vector2.one * 72));
            targetCellSize = Vector2.Min(maximumCellSize, Vector2.Max(minimumCellSize, SafeSize(targetCellSize, Vector2.one * 64)));
            spacing = new Vector2(Finite(spacing.x, 16, 0, 256), Finite(spacing.y, 16, 0, 256));
            minimumHorizontalGap = Finite(minimumHorizontalGap, 12, 0, 256);
            maximumHorizontalGap = Finite(maximumHorizontalGap, 32, minimumHorizontalGap, 256);
            padding ??= new RectOffset(14, 14, 14, 14);
            padding.left = Mathf.Clamp(padding.left, 0, 512); padding.right = Mathf.Clamp(padding.right, 0, 512);
            padding.top = Mathf.Clamp(padding.top, 0, 512); padding.bottom = Mathf.Clamp(padding.bottom, 0, 512);
            if ((int)alignment < 0 || (int)alignment > 2) alignment = ActionGridHorizontalAlignment.Center;
            if ((int)incompleteRowAlignment < 0 || (int)incompleteRowAlignment > 2) incompleteRowAlignment = ActionGridHorizontalAlignment.Left;
            if ((int)verticalPlacement < 0 || (int)verticalPlacement > 1) verticalPlacement = ActionGridVerticalPlacement.Top;
        }
        internal static Vector2 SafeSize(Vector2 v, Vector2 fallback) => new(Finite(v.x, fallback.x, 1, 1024), Finite(v.y, fallback.y, 1, 1024));
        internal static float Finite(float v, float fallback, float min, float max) => float.IsNaN(v) || float.IsInfinity(v) ? Mathf.Clamp(fallback, min, max) : Mathf.Clamp(v, min, max);
    }

    [Serializable]
    public sealed class ActionGridDisplaySettings
    {
        [Tooltip("Manual preserves legacy layout fields. Other modes retain independent settings. All dimensions are Canvas UI units.")]
        public ActionGridDisplayMode mode;
        public ActionGridFlow flow;
        public ActionGridSingleRowSettings singleRow = new();
        public ActionGridDisplayPreset balanced = new();
        public ActionGridDisplayPreset distributedSpacing = new();
        public ActionGridDisplayPreset largeSlots = new() { maximumColumns = 3, minimumCellSize = new Vector2(72, 72), maximumCellSize = new Vector2(96, 96), targetCellSize = new Vector2(80, 80) };
        public ActionGridDisplayPreset ActivePreset => mode switch
        {
            ActionGridDisplayMode.Balanced => balanced,
            ActionGridDisplayMode.DistributedSpacing => distributedSpacing,
            ActionGridDisplayMode.LargeSlots => largeSlots,
            _ => null
        };
        public ActionGridDisplaySettings Copy() => new() { mode = mode, flow = flow, singleRow = singleRow?.Copy(), balanced = balanced?.Copy(), distributedSpacing = distributedSpacing?.Copy(), largeSlots = largeSlots?.Copy() };
        public void Normalize()
        {
            if ((int)flow < 0 || (int)flow > 1) flow = ActionGridFlow.Grid;
            singleRow ??= new(); singleRow.Normalize();
            if ((int)mode < 0 || (int)mode > 3) mode = ActionGridDisplayMode.Manual;
            balanced ??= new(); distributedSpacing ??= new();
            largeSlots ??= new() { maximumColumns = 3, minimumCellSize = new Vector2(72,72), maximumCellSize = new Vector2(96,96) };
            balanced.Normalize(); distributedSpacing.Normalize(); largeSlots.Normalize();
        }
    }

    public readonly struct ActionGridLayoutResult
    {
        public readonly int Columns;
        public readonly Vector2 CellSize, Spacing;
        public readonly float RequiredWidth, RequiredHeight;
        public readonly bool IsReady, HasInsufficientSpace;
        internal ActionGridLayoutResult(int columns, Vector2 cell, Vector2 gap, float width, float height, Vector2 viewport)
        {
            Columns = columns; CellSize = cell; Spacing = gap; RequiredWidth = width; RequiredHeight = height;
            IsReady = viewport.x > .5f && viewport.y > .5f;
            HasInsufficientSpace = !IsReady || width > viewport.x + .5f || cell.y > viewport.y;
        }
    }

    public static class ActionGridLayoutCalculator
    {
        internal static void ApplyPadding(UnityEngine.UI.GridLayoutGroup grid, RectOffset value)
        {
            var current = grid.padding;
            if (!ReferenceEquals(current, value) && current != null && current.left == value.left && current.right == value.right && current.top == value.top && current.bottom == value.bottom) return;
            // LayoutGroup serialization must never mutate the presentation settings through a shared RectOffset.
            grid.padding = new RectOffset(value.left, value.right, value.top, value.bottom);
        }

        // Settings are normalized at their ownership boundary, not once per cell or frame.
        public static ActionGridLayoutResult Calculate(Vector2 viewport, int count, ActionGridLayoutMode mode,
            int columns, Vector2 minimum, Vector2 maximum, Vector2 gap, RectOffset padding,
            bool distribute = false, Vector2 target = default, float minimumGap = 0, float maximumGap = 0)
        {
            viewport = new Vector2(ActionGridDisplayPreset.Finite(viewport.x, 0, 0, 1000000), ActionGridDisplayPreset.Finite(viewport.y, 0, 0, 1000000));
            minimum = new Vector2(ActionGridDisplayPreset.Finite(minimum.x,1,1,1000000),ActionGridDisplayPreset.Finite(minimum.y,1,1,1000000));
            maximum = new Vector2(ActionGridDisplayPreset.Finite(maximum.x,minimum.x,minimum.x,1000000),ActionGridDisplayPreset.Finite(maximum.y,minimum.y,minimum.y,1000000));
            gap = new Vector2(ActionGridDisplayPreset.Finite(gap.x, 0, 0, 1000000), ActionGridDisplayPreset.Finite(gap.y, 0, 0, 1000000));
            var paddingWidth = Math.Max(0, padding?.left ?? 0) + (float)Math.Max(0, padding?.right ?? 0);
            var paddingHeight = Math.Max(0, padding?.top ?? 0) + (float)Math.Max(0, padding?.bottom ?? 0);
            var available = Mathf.Max(1, viewport.x - paddingWidth);
            target = Vector2.Min(maximum, Vector2.Max(minimum, ActionGridDisplayPreset.SafeSize(target, minimum)));
            minimumGap = ActionGridDisplayPreset.Finite(minimumGap, 0, 0, 1000000);
            maximumGap = ActionGridDisplayPreset.Finite(maximumGap, minimumGap, minimumGap, 1000000);
            var actualColumns = ActionGridPanel.CalculateColumnCount(mode, columns, available, distribute ? target.x : minimum.x, distribute ? minimumGap : gap.x);
            var width = distribute ? target.x : mode == ActionGridLayoutMode.ExactColumns ? minimum.x : Mathf.Clamp((available - gap.x * (actualColumns - 1)) / actualColumns, minimum.x, maximum.x);
            var height = distribute ? target.y : Mathf.Clamp(width * minimum.y / minimum.x, minimum.y, maximum.y);
            if (distribute) gap.x = actualColumns > 1 ? Mathf.Clamp((available - actualColumns * width) / (actualColumns - 1), minimumGap, maximumGap) : minimumGap;
            var rows = Mathf.CeilToInt(Mathf.Max(0, count) / (float)actualColumns);
            return new ActionGridLayoutResult(actualColumns, new Vector2(width,height), gap,
                paddingWidth + actualColumns * width + (actualColumns-1) * gap.x,
                paddingHeight + rows * height + Mathf.Max(0, rows-1) * gap.y, viewport);
        }
    }
}
