using System;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.SlotMachine;

namespace TxTRPG.UI.Tests
{
    public sealed class SlotReelSpinPlannerTests
    {
        private static readonly SlotEasingKind[] AllEasings = (SlotEasingKind[])Enum.GetValues(typeof(SlotEasingKind));
        // Easings whose true slope at 0.5 lies outside [0.5, 10]; their speed may step at the phase boundaries.
        private static readonly SlotEasingKind[] SlopeLimited = { SlotEasingKind.InOutCirc, SlotEasingKind.InOutBounce };

        [Test]
        public void EveryEasing_PassesThroughZeroHalfAndOne([ValueSource(nameof(AllEasings))] SlotEasingKind kind)
        {
            Assert.That(SlotEasing.Evaluate(kind, 0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(SlotEasing.Evaluate(kind, .5f), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(SlotEasing.Evaluate(kind, 1f), Is.EqualTo(1f).Within(1e-5f));
            for (var i = 0; i <= 1000; i++)
            {
                var value = SlotEasing.Evaluate(kind, i / 1000f);
                Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False, $"{kind} at {i / 1000f}");
            }
            Assert.That(SlotEasing.BoundarySlope(kind), Is.InRange(SlotEasing.MinimumBoundarySlope, SlotEasing.MaximumBoundarySlope));
        }

        [Test]
        public void EasingValues_AreStableNumbers()
        {
            // Serialized assets store the enum by number.
            Assert.That((int)SlotEasingKind.Linear, Is.EqualTo(0));
            Assert.That((int)SlotEasingKind.InOutElastic, Is.EqualTo(9));
            Assert.That((int)SlotEasingKind.InOutBounce, Is.EqualTo(10));
            Assert.That(SlotReelSpinSettings.Default.Easing, Is.EqualTo(SlotEasingKind.InOutElastic));
        }

        [Test]
        public void FixedSeed_KeepsEveryPhaseWithinItsJitter()
        {
            var settings = SlotReelSpinSettings.Default;
            var plans = SlotReelSpinPlanner.Create(settings, 3, false, new Random(42));
            for (var i = 0; i < plans.Length; i++)
            {
                var plan = plans[i];
                Assert.That(plan.DelaySeconds, Is.InRange(Math.Max(0f, i * settings.StartIntervalSeconds - settings.StartJitterSeconds) - 1e-5f,
                    i * settings.StartIntervalSeconds + settings.StartJitterSeconds + 1e-5f));
                Assert.That(plan.AccelerationSeconds, Is.InRange(settings.AccelerationSeconds - settings.AccelerationJitterSeconds - 1e-5f,
                    settings.AccelerationSeconds + settings.AccelerationJitterSeconds + 1e-5f));
                Assert.That(plan.DecelerationSeconds, Is.InRange(settings.DecelerationSeconds - settings.DecelerationJitterSeconds - 1e-5f,
                    settings.DecelerationSeconds + settings.DecelerationJitterSeconds + 1e-5f));
                // The cruise phase may only grow beyond its jitter to keep the stop gap.
                Assert.That(plan.CruiseSeconds, Is.GreaterThanOrEqualTo(settings.CruiseSeconds + i * settings.StopIntervalSeconds - settings.CruiseJitterSeconds - 1e-5f));
            }
            Assert.That(SlotReelSpinPlanner.Create(settings, 3, false, new Random(42)).Select(p => p.EndSeconds),
                Is.EqualTo(plans.Select(p => p.EndSeconds)), "the same seed gives the same plan");
        }

        [Test]
        public void ManySeeds_StopLeftToRightWithinTheMaximum()
        {
            var settings = SlotReelSpinSettings.Default;
            foreach (var reels in new[] { 1, 3, 5 })
            {
                var maximum = SlotReelSpinPlanner.GetMaximumPresentationSeconds(settings, reels);
                for (var seed = 0; seed < 1200; seed++)
                {
                    var plans = SlotReelSpinPlanner.Create(settings, reels, false, new Random(seed));
                    for (var i = 0; i < plans.Length; i++)
                    {
                        Assert.That(plans[i].EndSeconds, Is.LessThanOrEqualTo(maximum + 1e-4f), $"seed {seed} reel {i}");
                        if (i > 0)
                            Assert.That(plans[i].EndSeconds - plans[i - 1].EndSeconds,
                                Is.GreaterThanOrEqualTo(settings.MinimumStopGapSeconds - 1e-4f), $"seed {seed} reel {i}");
                    }
                }
            }
            Assert.That(SlotReelSpinPlanner.GetMaximumPresentationSeconds(settings, 3), Is.InRange(2.8f, 3.2f));
        }

        [Test]
        public void MaximumPresentation_IncludesTheTotalCriticalWait()
        {
            Assert.That(SlotReelSpinSettings.Default.TotalCriticalRevealDelaySeconds, Is.EqualTo(.4f).Within(1e-6f));
            Assert.That(SlotReelSpinSettings.Instant.TotalCriticalRevealDelaySeconds, Is.EqualTo(0f));
            var without = Settings(0f);
            var with = Settings(.4f);
            foreach (var reels in new[] { 1, 3, 10 })
            {
                Assert.That(SlotReelSpinPlanner.GetMaximumPresentationSeconds(with, reels),
                    Is.EqualTo(SlotReelSpinPlanner.GetMaximumPresentationSeconds(without, reels) + .4f).Within(1e-5f), $"{reels} reels");
            }
            Assert.That(Settings(-1f).TotalCriticalRevealDelaySeconds, Is.EqualTo(0f), "a negative wait is clamped");
            Assert.That(SlotReelSpinPlanner.GetMaximumPresentationSeconds(SlotReelSpinSettings.Instant, 3), Is.EqualTo(0f));

            static SlotReelSpinSettings Settings(float wait) => new(SlotEasingKind.Linear, .08f, .2f, .1f, .4f, .6f, .6f,
                .02f, .05f, .1f, .08f, 18f, 8f, true, .1f, .1f, 2, .2f, .3f, 1.2f, .8f, .15f, wait);
        }

        [Test]
        public void ReduceMotion_ShowsEachReelAfterItsFixedDelay()
        {
            var plans = SlotReelSpinPlanner.Create(SlotReelSpinSettings.Default, 3, true, new Random(1));
            Assert.That(plans.All(p => p.ReduceMotion), Is.True);
            Assert.That(plans.Select(p => p.EndSeconds), Is.EqualTo(new[] { .15f, .3f, .45f }).Within(1e-5f));
            var curve = new SlotReelSpinCurve(plans[0], 2, 6);
            Assert.That(curve.HasMotion, Is.False);
            Assert.That(curve.EffectStrength(.1f), Is.EqualTo(0f), "no afterimages or stretch with Reduce Motion");
        }

        [Test]
        public void PositionCurve_StartsAtZeroAndLandsOnTheResult([ValueSource(nameof(AllEasings))] SlotEasingKind kind)
        {
            var plan = Plan(kind, .35f, .7f, .7f);
            foreach (var delta in new[] { 0, 1, 5 })
            {
                var curve = new SlotReelSpinCurve(plan, delta, 6);
                Assert.That(curve.Loops, Is.GreaterThanOrEqualTo(1));
                Assert.That(curve.Distance, Is.EqualTo(delta + curve.Loops * 6));
                Assert.That(curve.Speed, Is.GreaterThanOrEqualTo(plan.Settings.MinimumCruiseCellsPerSecond - 1e-4f));
                Assert.That(curve.Position(0f), Is.EqualTo(0f).Within(1e-4f));
                Assert.That(curve.Position(curve.DurationSeconds), Is.EqualTo(curve.Distance));
                Assert.That(curve.Position(curve.DurationSeconds - 1e-4f), Is.EqualTo(curve.Distance).Within(.05f));
            }
        }

        [Test]
        public void PositionCurve_IsContinuousAtThePhaseBoundaries([ValueSource(nameof(AllEasings))] SlotEasingKind kind)
        {
            var plan = Plan(kind, .35f, .7f, .7f);
            var curve = new SlotReelSpinCurve(plan, 3, 6);
            const float h = 1e-4f;
            foreach (var boundary in new[] { plan.AccelerationSeconds, plan.AccelerationSeconds + plan.CruiseSeconds })
            {
                var before = curve.Position(boundary - h);
                var after = curve.Position(boundary + h);
                if (SlopeLimited.Contains(kind))
                {
                    // Continuous, but the eased side may be much steeper or flatter than the constant speed.
                    Assert.That(Math.Abs(after - before), Is.LessThan(.05f), $"{kind}: position continues at {boundary}");
                    continue;
                }
                Assert.That(after - before, Is.LessThan(curve.Speed * 4f * h + 1e-3f), $"{kind}: position continues at {boundary}");
                // Speed just inside each eased phase equals the constant speed.
                var dt = 2e-3f;
                var speedBefore = (curve.Position(boundary - h) - curve.Position(boundary - h - dt)) / dt;
                var speedAfter = (curve.Position(boundary + h + dt) - curve.Position(boundary + h)) / dt;
                Assert.That(speedBefore, Is.EqualTo(curve.Speed).Within(curve.Speed * .1f), $"{kind}: speed before {boundary}");
                Assert.That(speedAfter, Is.EqualTo(curve.Speed).Within(curve.Speed * .1f), $"{kind}: speed after {boundary}");
            }
        }

        [Test]
        public void ElasticEasing_OvershootsAndReturns()
        {
            var curve = new SlotReelSpinCurve(Plan(SlotEasingKind.InOutElastic, .35f, .7f, .7f), 2, 6);
            var samples = Enumerable.Range(0, 2001).Select(i => curve.Position(curve.DurationSeconds * i / 2000f)).ToArray();
            Assert.That(samples.Min(), Is.LessThan(0f), "starts by moving slightly backwards");
            Assert.That(samples.Max(), Is.GreaterThan(curve.Distance), "passes the result before settling");
            Assert.That(samples.Last(), Is.EqualTo(curve.Distance));
        }

        [Test]
        public void EffectStrength_OnlyInTheConstantSpeedPhase()
        {
            var plan = Plan(SlotEasingKind.InOutElastic, .35f, 1f, .7f);
            var curve = new SlotReelSpinCurve(plan, 1, 6);
            Assert.That(curve.EffectStrength(.2f), Is.EqualTo(0f), "start phase");
            Assert.That(curve.EffectStrength(.35f + .5f), Is.EqualTo(1f).Within(1e-5f), "middle of the constant-speed phase");
            Assert.That(curve.EffectStrength(1.35f), Is.EqualTo(0f), "the effect is gone when the stop phase begins");
            Assert.That(curve.EffectStrength(1.35f - .01f), Is.LessThan(.1f));
            Assert.That(curve.EffectStrength(1.6f), Is.EqualTo(0f), "stop phase");
            var shortCruise = new SlotReelSpinCurve(Plan(SlotEasingKind.Linear, .35f, .1f, .7f), 1, 6);
            Assert.That(Enumerable.Range(0, 101).Max(i => shortCruise.EffectStrength(.35f + i / 1000f)), Is.LessThan(1f),
                "a short constant-speed phase uses the smaller ramp");
        }

        [Test]
        public void InstantSettings_HaveNoMotion()
        {
            var plans = SlotReelSpinPlanner.Create(SlotReelSpinSettings.Instant, 3, false, new Random(3));
            Assert.That(plans.All(p => p.EndSeconds == 0f), Is.True);
            Assert.That(new SlotReelSpinCurve(plans[2], 4, 6).HasMotion, Is.False);
        }

        [Test]
        public void Settings_ClampJitterToHalfTheBase()
        {
            var settings = new SlotReelSpinSettings(SlotEasingKind.Linear, .08f, .2f, .1f, .4f, .6f, .6f,
                1f, 1f, 1f, 1f, 18f, 8f, true, .1f, .1f, 9, .2f, .3f, 1.2f, .8f, .15f);
            Assert.That(settings.StartJitterSeconds, Is.EqualTo(.04f).Within(1e-6f));
            Assert.That(settings.AccelerationJitterSeconds, Is.EqualTo(.2f).Within(1e-6f));
            Assert.That(settings.CruiseJitterSeconds, Is.EqualTo(.3f).Within(1e-6f));
            Assert.That(settings.DecelerationJitterSeconds, Is.EqualTo(.3f).Within(1e-6f));
            Assert.That(settings.AfterimageCount, Is.EqualTo(SlotReelSpinSettings.MaximumAfterimageCount));
        }

        private static SlotReelSpinPlan Plan(SlotEasingKind kind, float acceleration, float cruise, float deceleration)
        {
            var defaults = SlotReelSpinSettings.Default;
            var settings = new SlotReelSpinSettings(kind, 0f, 0f, 0f, acceleration, cruise, deceleration, 0f, 0f, 0f, 0f,
                defaults.CruiseCellsPerSecond, defaults.MinimumCruiseCellsPerSecond, true, defaults.EffectFadeInSeconds,
                defaults.EffectFadeOutSeconds, defaults.AfterimageCount, defaults.AfterimageSpacing, defaults.AfterimageAlpha,
                defaults.MaximumVerticalStretch, defaults.CruiseTextAlpha, defaults.ReducedMotionSecondsPerReel);
            return new SlotReelSpinPlan(0f, acceleration, cruise, deceleration, settings, false, 0f);
        }
    }
}
