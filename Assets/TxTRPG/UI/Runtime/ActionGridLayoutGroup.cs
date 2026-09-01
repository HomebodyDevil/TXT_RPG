using System;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    [AddComponentMenu("Layout/TxT RPG Action Grid Layout Group")]
    public sealed class ActionGridLayoutGroup : GridLayoutGroup
    {
        private ActionGridHorizontalAlignment incompleteRowAlignment =
            ActionGridHorizontalAlignment.Left;

        public ActionGridHorizontalAlignment IncompleteRowAlignment
        {
            get => incompleteRowAlignment;
            set
            {
                if (incompleteRowAlignment == value)
                {
                    return;
                }

                incompleteRowAlignment = value;
                SetDirty();
            }
        }

        [Obsolete("Use IncompleteRowAlignment.")]
        public ActionGridHorizontalAlignment HorizontalAlignment
        {
            get => IncompleteRowAlignment;
            set => IncompleteRowAlignment = value;
        }

        public override void SetLayoutVertical()
        {
            base.SetLayoutVertical();
            AlignIncompleteTrailingRow();
        }

        public static float CalculateTrailingRowOffset(
            ActionGridHorizontalAlignment alignment,
            int columns,
            int trailingCellCount,
            float cellWidth,
            float horizontalSpacing,
            bool startsFromRight = false)
        {
            columns = Mathf.Max(1, columns);
            trailingCellCount = Mathf.Clamp(trailingCellCount, 0, columns);
            if (trailingCellCount == 0 || trailingCellCount == columns)
            {
                return 0f;
            }

            var unusedWidth = (columns - trailingCellCount) *
                              (Mathf.Max(0f, cellWidth) + Mathf.Max(0f, horizontalSpacing));
            var offset = alignment switch
            {
                ActionGridHorizontalAlignment.Left => 0f,
                ActionGridHorizontalAlignment.Right => unusedWidth,
                _ => unusedWidth * 0.5f
            };

            if (!startsFromRight)
            {
                return offset;
            }

            return alignment switch
            {
                ActionGridHorizontalAlignment.Left => -unusedWidth,
                ActionGridHorizontalAlignment.Right => 0f,
                _ => -unusedWidth * 0.5f
            };
        }

        private void AlignIncompleteTrailingRow()
        {
            if (startAxis != Axis.Horizontal || constraint != Constraint.FixedColumnCount)
            {
                return;
            }

            var columns = Mathf.Max(1, constraintCount);
            var trailingCellCount = rectChildren.Count % columns;
            if (trailingCellCount == 0)
            {
                return;
            }

            var startsFromRight = startCorner == Corner.UpperRight ||
                                  startCorner == Corner.LowerRight;
            var offset = CalculateTrailingRowOffset(
                incompleteRowAlignment,
                columns,
                trailingCellCount,
                cellSize.x,
                spacing.x,
                startsFromRight);
            if (Mathf.Approximately(offset, 0f))
            {
                return;
            }

            var firstTrailingIndex = rectChildren.Count - trailingCellCount;
            for (var i = firstTrailingIndex; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                child.anchoredPosition = new Vector2(
                    child.anchoredPosition.x + offset,
                    child.anchoredPosition.y);
            }
        }
    }
}
