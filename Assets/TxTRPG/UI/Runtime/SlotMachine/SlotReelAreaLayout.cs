using System;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // Horizontal layout of the reels and the dividers between them. LayoutApplied is raised after every layout pass
    // (size or child changes), so SlotMachinePanel can place the payline frame without per-frame checks.
    public sealed class SlotReelAreaLayout : HorizontalLayoutGroup
    {
        public event Action LayoutApplied;

        public override void SetLayoutVertical()
        {
            base.SetLayoutVertical();
            LayoutApplied?.Invoke();
        }
    }
}
