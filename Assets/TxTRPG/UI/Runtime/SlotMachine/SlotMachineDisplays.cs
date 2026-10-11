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

    // One rune line row: the effect name ("공격") and the stacked weight with its allocation ("×2" or "×2 → 6").
    public sealed class SlotRuneLineDisplay
    {
        public SlotRuneLineDisplay(string effectText, string valueText)
        {
            EffectText = effectText ?? string.Empty;
            ValueText = valueText ?? string.Empty;
        }

        public string EffectText { get; }
        public string ValueText { get; }
    }

    // Contents of the summary area: the total box and the rune line table. Every string, including the column titles,
    // the empty text and the overflow format ("외 {0}줄"), is supplied by Application.
    public sealed class SlotSummaryDisplay
    {
        public SlotSummaryDisplay(string totalLabel, string totalValue, string runeColumnTitle, string valueColumnTitle,
            IReadOnlyList<SlotRuneLineDisplay> lines, string emptyText, string overflowFormat)
        {
            TotalLabel = totalLabel ?? string.Empty;
            TotalValue = totalValue ?? string.Empty;
            RuneColumnTitle = runeColumnTitle ?? string.Empty;
            ValueColumnTitle = valueColumnTitle ?? string.Empty;
            var count = lines?.Count ?? 0;
            var copy = new SlotRuneLineDisplay[count];
            for (var i = 0; i < count; i++) copy[i] = lines[i] ?? throw new ArgumentException("Rune lines cannot be null.", nameof(lines));
            Lines = Array.AsReadOnly(copy);
            EmptyText = emptyText ?? string.Empty;
            OverflowFormat = overflowFormat ?? string.Empty;
        }

        public string TotalLabel { get; }
        public string TotalValue { get; }
        public string RuneColumnTitle { get; }
        public string ValueColumnTitle { get; }
        public IReadOnlyList<SlotRuneLineDisplay> Lines { get; }
        public string EmptyText { get; }
        public string OverflowFormat { get; }
    }

    // An accepted spin: one stop per reel, the strip copies taken at spin time, and the summaries to show while spinning
    // and after the reels stop.
    public sealed class SlotSpinDisplay
    {
        public SlotSpinDisplay(IReadOnlyList<SlotReelStopDisplay> reels, int paylineTotal,
            SlotSummaryDisplay spinningSummary, SlotSummaryDisplay resultSummary)
        {
            if (reels == null || reels.Count == 0) throw new ArgumentException("A spin display requires at least one reel.", nameof(reels));
            var copy = new SlotReelStopDisplay[reels.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = reels[i] ?? throw new ArgumentException("Reel stops cannot be null.", nameof(reels));
            Reels = Array.AsReadOnly(copy);
            PaylineTotal = paylineTotal;
            SpinningSummary = spinningSummary ?? throw new ArgumentNullException(nameof(spinningSummary));
            ResultSummary = resultSummary ?? throw new ArgumentNullException(nameof(resultSummary));
        }

        public IReadOnlyList<SlotReelStopDisplay> Reels { get; }
        public int PaylineTotal { get; }
        public SlotSummaryDisplay SpinningSummary { get; }
        public SlotSummaryDisplay ResultSummary { get; }
    }
}
