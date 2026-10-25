using UnityEngine;

namespace TxTRPG.UI.Tooltips
{
    public readonly struct TooltipPlacementResult
    {
        public TooltipPlacementResult(Vector2 topLeft, bool flippedX, bool flippedY, bool pushedX, bool pushedY)
        {
            TopLeft = topLeft;
            FlippedX = flippedX;
            FlippedY = flippedY;
            PushedX = pushedX;
            PushedY = pushedY;
        }

        // Top-left corner of the tooltip box, in the same space as the inputs (y up).
        public Vector2 TopLeft { get; }
        public bool FlippedX { get; }
        public bool FlippedY { get; }
        public bool PushedX { get; }
        public bool PushedY { get; }
    }

    // Where a tooltip box goes next to its anchor rectangle (a rune card or a keyword's characters). All rectangles are in
    // one space with y up (RectTransform local space). The anchor's top-right corner is the box's top-left corner, so the
    // box sits to the right and opens downward. An axis that leaves the frame (minus margin) flips to the other side of
    // the anchor (right → the anchor's left edge is the box's right edge; down → the anchor's bottom edge is the box's
    // bottom edge); if it still does not fit, the box is pushed inside the frame. A box larger than the frame keeps its
    // top-left corner on the frame's top-left margin.
    public static class TooltipPlacement
    {
        public static TooltipPlacementResult Place(Rect anchor, Vector2 size, Rect frame, float margin)
        {
            margin = Mathf.Max(0f, margin);
            var minX = frame.xMin + margin;
            var maxX = frame.xMax - margin;
            var minY = frame.yMin + margin;
            var maxY = frame.yMax - margin;
            var width = Mathf.Max(0f, size.x);
            var height = Mathf.Max(0f, size.y);

            var left = anchor.xMax;
            var flippedX = false;
            if (left + width > maxX)
            {
                left = anchor.xMin - width;
                flippedX = true;
            }
            var pushedX = false;
            if (left < minX || left + width > maxX)
            {
                left = Mathf.Max(minX, Mathf.Min(left, maxX - width));
                pushedX = true;
            }

            var top = anchor.yMax;
            var flippedY = false;
            if (top - height < minY)
            {
                top = anchor.yMin + height;
                flippedY = true;
            }
            var pushedY = false;
            if (top > maxY || top - height < minY)
            {
                top = Mathf.Min(maxY, Mathf.Max(top, minY + height));
                pushedY = true;
            }
            return new TooltipPlacementResult(new Vector2(left, top), flippedX, flippedY, pushedX, pushedY);
        }
    }
}
