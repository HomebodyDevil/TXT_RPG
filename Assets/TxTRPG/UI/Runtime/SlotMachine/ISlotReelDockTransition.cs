using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    // Extension point for SlotReelDock's show/hide motion (for example sliding in from below the screen). Implement it on
    // a MonoBehaviour and assign it to SlotReelDock.transitionBehaviour. The transition moves target.anchoredPosition from
    // `from` to `to` and must end exactly on `to`; showing is true when the dock moves into view and false when it moves
    // out. With reduceMotion it should finish immediately or with minimal motion.
    // The dock cancels the token when its state changes again before the transition ends and then starts the next one
    // from the current position. When the task faults, the dock puts the target on `to` itself.
    public interface ISlotReelDockTransition
    {
        Task PlayAsync(RectTransform target, Vector2 from, Vector2 to, bool showing, bool reduceMotion, CancellationToken cancellationToken);
    }
}
