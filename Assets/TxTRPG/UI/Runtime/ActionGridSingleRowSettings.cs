using System;
using UnityEngine;

namespace TxTRPG.UI
{
    public enum ActionGridFlow { Grid = 0, SingleRow = 1 }
    public enum ActionGridRowAlignment { ConditionalCenterOrEnds = 0, FixedGapGroup = 1, AlwaysEnds = 2 }
    public enum ActionGridRowVerticalAlignment { Top = 0, Center = 1, Bottom = 2 }

    [Serializable]
    public sealed class ActionGridSingleRowSettings
    {
        public ActionGridRowAlignment alignment;
        [Min(0)] public int centerThreshold = 4;
        public ActionGridHorizontalAlignment groupAlignment = ActionGridHorizontalAlignment.Center;
        public ActionGridRowVerticalAlignment verticalAlignment = ActionGridRowVerticalAlignment.Center;
        [Tooltip("Manual mode target. Presets use their own target cell size, spacing and padding.")]
        public Vector2 targetCellSize = new(64, 64);
        public ScrollbarVisibilityMode scrollbarVisibility = ScrollbarVisibilityMode.Hidden;
        public float scrollbarHeight = 12;
        public float scrollbarGap = 4;
        public ActionGridSingleRowSettings Copy() => (ActionGridSingleRowSettings)MemberwiseClone();
        public void Normalize()
        {
            centerThreshold = Mathf.Max(0, centerThreshold);
            targetCellSize = ActionGridDisplayPreset.SafeSize(targetCellSize, Vector2.one * 64);
            scrollbarHeight = ActionGridDisplayPreset.Finite(scrollbarHeight, 12, 1, 128);
            scrollbarGap = ActionGridDisplayPreset.Finite(scrollbarGap, 4, 0, 128);
            if ((int)alignment < 0 || (int)alignment > 2) alignment = ActionGridRowAlignment.ConditionalCenterOrEnds;
            if ((int)groupAlignment < 0 || (int)groupAlignment > 2) groupAlignment = ActionGridHorizontalAlignment.Center;
            if ((int)verticalAlignment < 0 || (int)verticalAlignment > 2) verticalAlignment = ActionGridRowVerticalAlignment.Center;
            if (!Enum.IsDefined(typeof(ScrollbarVisibilityMode), scrollbarVisibility)) scrollbarVisibility = ScrollbarVisibilityMode.Hidden;
        }
    }

    public readonly struct ActionGridSingleRowResult
    {
        public readonly ActionGridLayoutResult Layout;
        public readonly ActionGridHorizontalAlignment Alignment;
        public readonly bool Overflow;
        public ActionGridSingleRowResult(ActionGridLayoutResult layout, ActionGridHorizontalAlignment alignment, bool overflow)
        { Layout = layout; Alignment = alignment; Overflow = overflow; }
    }

    public static class ActionGridSingleRowCalculator
    {
        public static ActionGridSingleRowResult Calculate(Vector2 viewport, int count, Vector2 cell, float gap,
            RectOffset padding, ActionGridSingleRowSettings settings)
        {
            count = Mathf.Max(0, count);
            viewport = new Vector2(ActionGridDisplayPreset.Finite(viewport.x, 0, 0, 1000000), ActionGridDisplayPreset.Finite(viewport.y, 0, 0, 1000000));
            cell = ActionGridDisplayPreset.SafeSize(cell, Vector2.one * 64);
            gap = ActionGridDisplayPreset.Finite(gap, 8, 0, 1000000);
            var horizontalPadding = Mathf.Max(0, padding?.left ?? 0) + (float)Mathf.Max(0, padding?.right ?? 0);
            var verticalPadding = Mathf.Max(0, padding?.top ?? 0) + (float)Mathf.Max(0, padding?.bottom ?? 0);
            var required = horizontalPadding + count * (double)cell.x + Math.Max(0, count - 1) * (double)gap;
            var overflow = required > viewport.x + .001;
            var alignment = ActionGridHorizontalAlignment.Left;
            if (!overflow)
            {
                var ends = settings.alignment == ActionGridRowAlignment.AlwaysEnds ||
                    (settings.alignment == ActionGridRowAlignment.ConditionalCenterOrEnds && count > Mathf.Max(0, settings.centerThreshold));
                if (ends && count > 1) gap = Mathf.Max(0, (viewport.x - horizontalPadding - count * cell.x) / (count - 1));
                else alignment = settings.alignment == ActionGridRowAlignment.FixedGapGroup
                    ? settings.groupAlignment : ActionGridHorizontalAlignment.Center;
            }
            return new ActionGridSingleRowResult(new ActionGridLayoutResult(Mathf.Max(1, count), cell, new Vector2(gap, 0),
                (float)Math.Min(float.MaxValue, required), verticalPadding + (count > 0 ? cell.y : 0), viewport), alignment, overflow);
        }
    }
}
