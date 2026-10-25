using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TxTRPG.UI.SlotMachine
{
    // Display contract a slot controller uses to show reel configurations, accepted spins and summaries. Implemented by
    // SlotMachinePanel (reels with the summary area) and SlotReelDock (reels with a total card). Members are limited to
    // what TemporarySlotCombatController and ExplorationEventController call. Unity does not serialize interface fields,
    // so a controller that accepts any presenter keeps a MonoBehaviour field and checks for this interface.
    public interface ISlotMachinePresenter
    {
        bool IsSpinning { get; }

        // Applies a reel configuration; deferred until a running spin ends.
        void SetReels(IReadOnlyList<SlotReelDisplay> reels);

        // Every string comes from the display data. An implementation may show only part of the summary.
        void SetSummary(SlotSummaryDisplay summary);

        // Presents an already accepted spin: the spinning summary, the running total as reels stop (when the display has
        // progress summaries) and the result summary. False when it was interrupted or the reels cannot be shown.
        Task<bool> PlaySpinAsync(SlotSpinDisplay result, bool reduceMotion, CancellationToken cancellationToken);

        // Puts every reel at rest on the accepted result immediately and invalidates a running presentation.
        void StopAtResult(SlotSpinDisplay result);

        // Configuration error: the reels and summary are hidden and the reason is shown.
        void ShowError(string message);
        void ClearError();

        float GetMaximumPresentationSeconds(int reelCount);
    }
}
