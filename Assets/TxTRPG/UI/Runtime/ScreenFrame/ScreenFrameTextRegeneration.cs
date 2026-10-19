using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TxTRPG.UI.ScreenFrame
{
    // TextMeshPro on a Screen Space - Overlay canvas generates its SDF scale independent of the canvas scale, but when
    // the canvas scale changes by more than 20% its per-frame check multiplies the existing scale by the change, which
    // makes the text look bolder or blurrier until the text is regenerated. Marking the text as changed makes it
    // regenerate before the next canvas render and skips that check. Inactive text keeps the mark and regenerates when
    // it is enabled.
    public static class ScreenFrameTextRegeneration
    {
        // Marks every TMP_Text under root (active or not) for regeneration. buffer is reused to avoid allocations.
        public static int MarkForRegeneration(Transform root, List<TMP_Text> buffer)
        {
            if (root == null || buffer == null) return 0;
            buffer.Clear();
            root.GetComponentsInChildren(true, buffer);
            foreach (var text in buffer)
            {
                if (text != null) text.havePropertiesChanged = true;
            }
            var count = buffer.Count;
            buffer.Clear();
            return count;
        }
    }
}
