using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    // Show/hide motion of SlotReelDock: moves the target from `from` to `to` along an easing curve (easeInOutBack by
    // default, which overshoots a little at both ends; the curve is never clamped). Show and hide have their own duration,
    // set by the developer on SlotReelDock.prefab or a Scene instance; every Show/Hide of the dock uses them. A direction
    // change uses the full duration of the new direction from the current position. Time is unscaled. With reduceMotion or
    // a zero duration the target is put on `to` at once. Only target.anchoredPosition changes; a cancelled transition
    // leaves the target where it is.
    [DisallowMultipleComponent]
    public sealed class SlotReelDockEasedTransition : MonoBehaviour, ISlotReelDockTransition
    {
        public const float DefaultDuration = .3f;

        [SerializeField, Min(0f), Tooltip("Seconds to move into view.")] private float showDuration = DefaultDuration;
        [SerializeField, Min(0f), Tooltip("Seconds to move out of view.")] private float hideDuration = DefaultDuration;
        [SerializeField, Tooltip("Position curve. InOutBack overshoots at both ends.")]
        private SlotEasingKind easing = SlotEasingKind.InOutBack;

        private Func<float> deltaTime = UnscaledDeltaTime;
        private Func<Task> nextFrame = NextFrame;

        public float ShowDuration => showDuration;
        public float HideDuration => hideDuration;
        public SlotEasingKind Easing => easing;

        public float DurationFor(bool showing) => showing ? showDuration : hideDuration;

        public async Task PlayAsync(RectTransform target, Vector2 from, Vector2 to, bool showing, bool reduceMotion,
            CancellationToken cancellationToken)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            cancellationToken.ThrowIfCancellationRequested();
            var duration = DurationFor(showing);
            if (reduceMotion || !(duration > 0f))
            {
                target.anchoredPosition = to;
                return;
            }
            var elapsed = 0f;
            target.anchoredPosition = from;
            while (elapsed < duration)
            {
                await nextFrame();
                cancellationToken.ThrowIfCancellationRequested();
                if (target == null) return;
                elapsed += Mathf.Max(0f, deltaTime());
                target.anchoredPosition = Evaluate(from, to, easing, elapsed / duration);
            }
            target.anchoredPosition = to;
        }

        // from + (to - from) × E(t); t at or past 1 is exactly `to`.
        public static Vector2 Evaluate(Vector2 from, Vector2 to, SlotEasingKind kind, float t) =>
            t >= 1f ? to : from + (to - from) * SlotEasing.Evaluate(kind, t);

        private static float UnscaledDeltaTime() => Time.unscaledDeltaTime;

        // Same frame wait as the reel spin presentation (SlotReelView): the Unity synchronization context resumes on the
        // next frame.
        private static async Task NextFrame() => await Task.Yield();

        private void OnValidate()
        {
            showDuration = Mathf.Max(0f, showDuration);
            hideDuration = Mathf.Max(0f, hideDuration);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(float show, float hide, SlotEasingKind kind)
        {
            showDuration = Mathf.Max(0f, show);
            hideDuration = Mathf.Max(0f, hide);
            easing = kind;
        }

        // Tests only: replaces the frame wait and the time step. Null restores the Unity defaults.
        public void SetTimeSourceForEditor(Func<float> frameDeltaTime, Func<Task> waitForNextFrame)
        {
            deltaTime = frameDeltaTime ?? UnscaledDeltaTime;
            nextFrame = waitForNextFrame ?? NextFrame;
        }
#endif
    }
}
