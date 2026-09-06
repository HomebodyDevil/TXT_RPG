using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.SceneTransition
{
    [DefaultExecutionOrder(-12000)]
    [DisallowMultipleComponent]
    public sealed class FadeScreenTransitionEffect : ScreenTransitionEffect
    {
        [SerializeField] private Image transitionImage;

        private void Awake()
        {
            if (transitionImage != null)
            {
                transitionImage.raycastTarget = false;
            }

            SetRevealedImmediately();
        }

        public void Configure(Image image)
        {
            transitionImage = image;
            if (transitionImage != null)
            {
                transitionImage.raycastTarget = false;
            }
        }

        public override Task CoverAsync(
            TransitionContext context,
            CancellationToken cancellationToken)
        {
            return AnimateAsync(0f, 1f, ResolveDuration(context, true), context, cancellationToken);
        }

        public override Task RevealAsync(
            TransitionContext context,
            CancellationToken cancellationToken)
        {
            return AnimateAsync(1f, 0f, ResolveDuration(context, false), context, cancellationToken);
        }

        public override void SetCoveredImmediately(Color color)
        {
            if (transitionImage == null)
            {
                return;
            }

            transitionImage.gameObject.SetActive(true);
            SetAlpha(1f, color);
        }

        public override void SetRevealedImmediately()
        {
            SetAlpha(0f, Color.black);
            if (transitionImage != null)
            {
                transitionImage.gameObject.SetActive(false);
            }
        }

        private async Task AnimateAsync(
            float start,
            float end,
            float duration,
            TransitionContext context,
            CancellationToken cancellationToken)
        {
            if (transitionImage == null)
            {
                return;
            }

            transitionImage.gameObject.SetActive(true);
            SetAlpha(start, context.Profile.Color);
            if (duration <= 0f)
            {
                SetAlpha(end, context.Profile.Color);
                if (end <= 0f)
                {
                    transitionImage.gameObject.SetActive(false);
                }
                return;
            }

            // The first rendered animation frame is always exact progress zero.
            await Task.Yield();
            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var progress = Mathf.Clamp01(elapsed / duration);
                SetAlpha(Mathf.Lerp(start, end, progress), context.Profile.Color);
                await Task.Yield();
                var delta = context.Profile.UseUnscaledTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;
                elapsed += Mathf.Min(Mathf.Max(0f, delta), context.Profile.MaximumFrameDelta);
            }

            SetAlpha(end, context.Profile.Color);
            if (end <= 0f)
            {
                transitionImage.gameObject.SetActive(false);
            }
        }

        private static float ResolveDuration(TransitionContext context, bool covering)
        {
            if (!context.ReduceMotion)
            {
                return covering ? context.Profile.CoverDuration : context.Profile.RevealDuration;
            }

            return context.Profile.ReducedMotionMode == ReducedMotionTransitionMode.Instant
                ? 0f
                : context.Profile.ReducedMotionDuration;
        }

        private void SetAlpha(float alpha, Color color)
        {
            if (transitionImage == null)
            {
                return;
            }

            color.a = Mathf.Clamp01(alpha);
            transitionImage.color = color;
        }
    }
}
