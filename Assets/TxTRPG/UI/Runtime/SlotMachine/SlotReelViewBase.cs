using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
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

        // Plays the presentation for an already decided stop and ends exactly on it. The reel waits plan.DelaySeconds
        // first, still showing its previous stop. Returns false when it was interrupted (cancellation, disable or
        // destroy); the caller then calls ShowStop. Every motion effect is cleared when it ends or is interrupted.
        public abstract Task<bool> PlaySpinAsync(SlotReelDisplay reel, int stopIndex, SlotReelSpinPlan plan,
            CancellationToken cancellationToken);

        // Font size range from the panel; the reel derives the actual size from its cell height.
        public abstract void SetFontSizeRange(float minimum, float maximum);

        // World-space corners (bottom-left, top-left, top-right, bottom-right) of the payline cell across the reel
        // window, used by the panel to place the payline frame. False when the reel has no layout yet.
        public abstract bool GetPaylineWorldCorners(Vector3[] corners);

        // Applies the panel skin to this reel's own graphics; null restores the recorded Prefab default. A reel
        // implementation without skin support keeps this no-op.
        public virtual void ApplySkin(SlotMachinePanelSkin skin) { }
    }
}
