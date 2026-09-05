using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class FadePanelRevealTransition : PanelRevealTransition
    {
        [SerializeField] private bool animateReveal;
        [SerializeField, Min(0f)] private float duration = 0.35f;
        [SerializeField, Range(0.001f, 0.1f)] private float maximumFrameDelta = 0.05f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool reduceMotion;

        public override void PrepareHidden(CanvasGroup target) { if (target != null) target.alpha = 0f; }
        public override void CompleteImmediately(CanvasGroup target) { if (target != null) target.alpha = 1f; }

        public override async Task RevealAsync(CanvasGroup target, CancellationToken cancellationToken)
        {
            if (target == null) return;
            target.alpha = 0f;
            if (!animateReveal || reduceMotion || duration <= 0f) { target.alpha = 1f; return; }
            await Task.Yield();
            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                target.alpha = Mathf.Clamp01(elapsed / duration);
                await Task.Yield();
                var delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsed += Mathf.Min(Mathf.Max(0f, delta), maximumFrameDelta);
            }
            target.alpha = 1f;
        }
    }
}
