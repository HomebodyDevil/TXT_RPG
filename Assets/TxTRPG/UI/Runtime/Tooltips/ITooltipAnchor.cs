using UnityEngine;

namespace TxTRPG.UI.Tooltips
{
    // What opened a tooltip (a rune card, a keyword link). The presenter places the tooltip from the anchor rectangle,
    // keeps it while the pointer is over the anchor and closes it at once when the anchor becomes invalid or moves.
    public interface ITooltipAnchor
    {
        // False once the anchor is disabled, destroyed or shows something else.
        bool IsAnchorValid { get; }
        bool IsPointerOver { get; }
        // Anchor rectangle in the local space of `space` (y up). False when it has none.
        bool TryGetAnchorRect(RectTransform space, out Rect rect);
    }
}
