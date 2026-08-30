using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public abstract class PanelRevealTransition : MonoBehaviour
    {
        public abstract void PrepareHidden(CanvasGroup target);
        public abstract Task RevealAsync(CanvasGroup target, CancellationToken cancellationToken);
        public abstract void CompleteImmediately(CanvasGroup target);
    }
}
