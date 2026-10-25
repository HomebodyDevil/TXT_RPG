using System;

namespace TxTRPG.UI.SlotMachine
{
    // Running total of one spin. Reels report when they are at rest on the result; the summary to show is the progress
    // summary of the longest run of stopped reels from the first one, so the total always matches a set of reels that
    // really stopped even if a later reel finishes first. Each run length is returned once, so the owner rebuilds the
    // summary text only when the total changes.
    public sealed class SlotSpinProgress
    {
        private readonly SlotSpinDisplay display;
        private readonly bool[] stopped;
        private int appliedCount;

        public SlotSpinProgress(SlotSpinDisplay display)
        {
            this.display = display ?? throw new ArgumentNullException(nameof(display));
            stopped = new bool[display.Reels.Count];
        }

        // Reels 0..StoppedCount-1 have all stopped.
        public int StoppedCount => appliedCount;

        // Returns the progress summary to show after reel reelIndex stopped, or null when the shown total does not
        // change (an earlier reel is still spinning, the reel was already reported, or the display has no progress).
        public SlotSummaryDisplay ReelStopped(int reelIndex)
        {
            if (reelIndex < 0 || reelIndex >= stopped.Length) throw new ArgumentOutOfRangeException(nameof(reelIndex));
            stopped[reelIndex] = true;
            var count = appliedCount;
            while (count < stopped.Length && stopped[count]) count++;
            if (count == appliedCount) return null;
            appliedCount = count;
            return display.HasProgressSummaries ? display.ProgressSummaries[count - 1] : null;
        }
    }
}
