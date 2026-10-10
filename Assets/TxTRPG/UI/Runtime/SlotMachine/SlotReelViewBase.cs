using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    public readonly struct SlotReelSpinTiming
    {
        public SlotReelSpinTiming(float spinSeconds, float decelerationSeconds, float reducedMotionSeconds, bool reduceMotion)
        {
            SpinSeconds = Mathf.Max(0f, spinSeconds);
            DecelerationSeconds = Mathf.Max(0f, decelerationSeconds);
            ReducedMotionSeconds = Mathf.Max(0f, reducedMotionSeconds);
            ReduceMotion = reduceMotion;
        }

        // Constant-speed time before this reel starts slowing down (already includes the left-to-right stagger).
        public float SpinSeconds { get; }
        public float DecelerationSeconds { get; }
        public float ReducedMotionSeconds { get; }
        public bool ReduceMotion { get; }
        public float TotalSeconds => ReduceMotion ? ReducedMotionSeconds : SpinSeconds + DecelerationSeconds;
    }

    // Display contract between SlotMachinePanel and one reel. The panel only uses this, so a different reel
    // implementation (for example a 3D cylinder) can replace SlotReelView without changing the panel.
    public abstract class SlotReelViewBase : MonoBehaviour
    {
        public abstract string ReelId { get; }
        public abstract int PaylineValue { get; }
        // Strip index currently on the payline.
        public abstract int StopIndex { get; }
        public abstract IReadOnlyList<int> Strip { get; }

        // Shows the strip at rest with the given cell on the payline.
        public abstract void ShowStop(SlotReelDisplay reel, int stopIndex);

        // Plays the presentation for an already decided stop and ends exactly on it. Returns false when it was
        // interrupted (cancellation, disable or destroy); the caller then calls ShowStop.
        public abstract Task<bool> PlaySpinAsync(SlotReelDisplay reel, int stopIndex, SlotReelSpinTiming timing,
            CancellationToken cancellationToken);

        // Font size range from the panel; the reel derives the actual size from its cell height.
        public abstract void SetFontSizeRange(float minimum, float maximum);
    }
}
