using System;
using System.Collections.Generic;

namespace TxTRPG.UI.SlotMachine
{
    // Display-only data. The UI assembly does not see the slot machine or rune domains; Application converts to these
    // and assembles every user-visible string.
    public sealed class SlotReelDisplay
    {
        public SlotReelDisplay(string reelId, IReadOnlyList<int> values)
        {
            ReelId = reelId ?? string.Empty;
            if (values == null || values.Count == 0) throw new ArgumentException("A reel display requires at least one value.", nameof(values));
            var copy = new int[values.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = values[i];
            Values = Array.AsReadOnly(copy);
        }

        public string ReelId { get; }
        public IReadOnlyList<int> Values { get; }
    }

    public sealed class SlotReelStopDisplay
    {
        public SlotReelStopDisplay(string reelId, int stopIndex, IReadOnlyList<int> strip)
        {
            Reel = new SlotReelDisplay(reelId, strip);
            if (stopIndex < 0 || stopIndex >= Reel.Values.Count) throw new ArgumentOutOfRangeException(nameof(stopIndex));
            StopIndex = stopIndex;
        }

        public SlotReelDisplay Reel { get; }
        public string ReelId => Reel.ReelId;
        public int StopIndex { get; }
        public int PaylineValue => Reel.Values[StopIndex];
    }

    // An accepted spin: one stop per reel, the strip copies taken at spin time, and the texts to show while spinning and
    // after the reels stop.
    public sealed class SlotSpinDisplay
    {
        public SlotSpinDisplay(IReadOnlyList<SlotReelStopDisplay> reels, int paylineTotal,
            string spinningTotalText, string totalText, string allocationText)
        {
            if (reels == null || reels.Count == 0) throw new ArgumentException("A spin display requires at least one reel.", nameof(reels));
            var copy = new SlotReelStopDisplay[reels.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = reels[i] ?? throw new ArgumentException("Reel stops cannot be null.", nameof(reels));
            Reels = Array.AsReadOnly(copy);
            PaylineTotal = paylineTotal;
            SpinningTotalText = spinningTotalText ?? string.Empty;
            TotalText = totalText ?? string.Empty;
            AllocationText = allocationText ?? string.Empty;
        }

        public IReadOnlyList<SlotReelStopDisplay> Reels { get; }
        public int PaylineTotal { get; }
        public string SpinningTotalText { get; }
        public string TotalText { get; }
        public string AllocationText { get; }
    }
}
