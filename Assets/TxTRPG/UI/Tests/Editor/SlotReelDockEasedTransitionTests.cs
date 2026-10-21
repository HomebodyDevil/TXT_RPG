using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.UI.SlotMachine;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    // Drives the transition frame by frame with an injected time step; no Play Mode or saved asset is used.
    public sealed class SlotReelDockEasedTransitionTests
    {
        private static readonly Vector2 Shown = Vector2.zero;
        private static readonly Vector2 Hidden = new(0f, -400f);
        private GameObject host;
        private RectTransform target;
        private SlotReelDockEasedTransition transition;
        private TaskCompletionSource<bool> frame;
        private float step;
        private SynchronizationContext previousContext;

        [SetUp]
        public void SetUp()
        {
            // Without a synchronization context the continuation runs inside SetResult, so each Frame call is one frame.
            previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            host = new GameObject("TransitionHost", typeof(RectTransform));
            target = new GameObject("PresentationRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            target.SetParent(host.transform, false);
            transition = host.AddComponent<SlotReelDockEasedTransition>();
            transition.SetTimeSourceForEditor(() => step, () => (frame = new TaskCompletionSource<bool>()).Task);
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            UnityEngine.Object.DestroyImmediate(host);
        }

        // Completes the pending frame wait; the continuation runs synchronously.
        private void Frame(float seconds)
        {
            step = seconds;
            var pending = frame;
            Assert.That(pending, Is.Not.Null, "the transition waits for a frame");
            frame = null;
            pending.SetResult(true);
        }

        private int FramesUntilDone(Task task, float seconds, List<float> positions = null)
        {
            var frames = 0;
            while (!task.IsCompleted && frames < 1000)
            {
                Frame(seconds);
                frames++;
                positions?.Add(target.anchoredPosition.y);
            }
            return frames;
        }

        [Test]
        public void Defaults_AreThreeTenthsInOutBack()
        {
            Assert.That(transition.ShowDuration, Is.EqualTo(.3f));
            Assert.That(transition.HideDuration, Is.EqualTo(.3f));
            Assert.That(transition.Easing, Is.EqualTo(SlotEasingKind.InOutBack));
        }

        [Test]
        public void EachDirection_UsesItsOwnDuration_AndEndsExactlyOnTarget()
        {
            transition.ConfigureForEditor(.2f, .5f, SlotEasingKind.InOutBack);
            var show = transition.PlayAsync(target, Hidden, Shown, true, false, CancellationToken.None);
            Assert.That(FramesUntilDone(show, .1f), Is.EqualTo(2), "show takes 0.2 s");
            Assert.That(target.anchoredPosition, Is.EqualTo(Shown));

            var hide = transition.PlayAsync(target, Shown, Hidden, false, false, CancellationToken.None);
            Assert.That(FramesUntilDone(hide, .1f), Is.EqualTo(5), "hide takes 0.5 s");
            Assert.That(target.anchoredPosition, Is.EqualTo(Hidden));
            Assert.That(show.IsCompletedSuccessfully && hide.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public void InOutBack_OvershootsBeyondTheEnds()
        {
            var positions = new List<float>();
            var hide = transition.PlayAsync(target, Shown, Hidden, false, false, CancellationToken.None);
            FramesUntilDone(hide, .3f / 30f, positions);
            Assert.That(positions.Exists(y => y > Shown.y + .01f), Is.True, "moves up before leaving (back)");
            Assert.That(positions.Exists(y => y < Hidden.y - .01f), Is.True, "passes the hidden position before settling");
            Assert.That(target.anchoredPosition, Is.EqualTo(Hidden));
            Assert.That(SlotReelDockEasedTransition.Evaluate(Shown, Hidden, SlotEasingKind.InOutBack, .1f).y, Is.GreaterThan(0f));
        }

        [TestCase(true, .3f)]
        [TestCase(false, 0f)]
        public void ReduceMotionOrZeroDuration_MovesImmediately(bool reduceMotion, float duration)
        {
            transition.ConfigureForEditor(duration, duration, SlotEasingKind.InOutBack);
            var task = transition.PlayAsync(target, Shown, Hidden, false, reduceMotion, CancellationToken.None);
            Assert.That(task.IsCompletedSuccessfully, Is.True);
            Assert.That(frame, Is.Null, "no frame is awaited");
            Assert.That(target.anchoredPosition, Is.EqualTo(Hidden));
        }

        [Test]
        public void Cancellation_KeepsTheCurrentPosition()
        {
            using var cancellation = new CancellationTokenSource();
            var task = transition.PlayAsync(target, Shown, Hidden, false, false, cancellation.Token);
            Frame(.15f);
            var middle = target.anchoredPosition;
            Assert.That(middle, Is.Not.EqualTo(Shown).And.Not.EqualTo(Hidden));
            cancellation.Cancel();
            Frame(.15f);
            Assert.That(task.IsCanceled, Is.True);
            Assert.That(target.anchoredPosition, Is.EqualTo(middle));
        }
    }
}
