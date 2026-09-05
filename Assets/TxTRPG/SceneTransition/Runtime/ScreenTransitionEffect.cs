using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.SceneTransition
{
    public abstract class ScreenTransitionEffect : MonoBehaviour, IScreenTransitionEffect
    {
        public abstract Task CoverAsync(
            TransitionContext context,
            CancellationToken cancellationToken);

        public abstract Task RevealAsync(
            TransitionContext context,
            CancellationToken cancellationToken);

        public abstract void CompleteImmediately();
    }
}
