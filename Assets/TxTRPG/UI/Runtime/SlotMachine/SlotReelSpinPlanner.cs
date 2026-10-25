using System;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    // Timing of one reel for one spin: wait DelaySeconds, then the start phase (first half of the easing), the
    // constant-speed phase with the high-speed effect, and the stop phase (second half of the easing).
    public readonly struct SlotReelSpinPlan
    {
        public SlotReelSpinPlan(float delaySeconds, float accelerationSeconds, float cruiseSeconds, float decelerationSeconds,
            SlotReelSpinSettings settings, bool reduceMotion, float reducedMotionSeconds)
        {
            DelaySeconds = Mathf.Max(0f, delaySeconds);
            AccelerationSeconds = Mathf.Max(0f, accelerationSeconds);
            CruiseSeconds = Mathf.Max(0f, cruiseSeconds);
            DecelerationSeconds = Mathf.Max(0f, decelerationSeconds);
            Settings = settings;
            ReduceMotion = reduceMotion;
            ReducedMotionSeconds = Mathf.Max(0f, reducedMotionSeconds);
        }

        public float DelaySeconds { get; }
        public float AccelerationSeconds { get; }
        public float CruiseSeconds { get; }
        public float DecelerationSeconds { get; }
        // Easing, speed and effect values (copied, so the reel never reads the profile asset).
        public SlotReelSpinSettings Settings { get; }
        public bool ReduceMotion { get; }
        public float ReducedMotionSeconds { get; }
        public float MotionSeconds => AccelerationSeconds + CruiseSeconds + DecelerationSeconds;
        public float EndSeconds => ReduceMotion ? ReducedMotionSeconds : DelaySeconds + MotionSeconds;
    }

    // Plans the reels of one spin. Jitter comes from a presentation-only System.Random, never from the result random.
    public static class SlotReelSpinPlanner
    {
        public const float MinimumEasedPhaseSeconds = .05f;

        public static SlotReelSpinPlan[] Create(SlotReelSpinSettings settings, int reelCount, bool reduceMotion, System.Random random)
        {
            reelCount = Mathf.Max(0, reelCount);
            var plans = new SlotReelSpinPlan[reelCount];
            if (reduceMotion)
            {
                for (var i = 0; i < reelCount; i++)
                    plans[i] = new SlotReelSpinPlan(0f, 0f, 0f, 0f, settings, true, (i + 1) * settings.ReducedMotionSecondsPerReel);
                return plans;
            }
            random ??= new System.Random();
            var previousEnd = 0f;
            for (var i = 0; i < reelCount; i++)
            {
                var delay = Mathf.Max(0f, i * settings.StartIntervalSeconds + Jitter(random, settings.StartJitterSeconds));
                var acceleration = Phase(settings.AccelerationSeconds, Jitter(random, settings.AccelerationJitterSeconds));
                var cruise = Mathf.Max(0f, settings.CruiseSeconds + i * settings.StopIntervalSeconds + Jitter(random, settings.CruiseJitterSeconds));
                var deceleration = Phase(settings.DecelerationSeconds, Jitter(random, settings.DecelerationJitterSeconds));
                var end = delay + acceleration + cruise + deceleration;
                // Reels always stop left to right, at least MinimumStopGapSeconds apart.
                if (i > 0 && end < previousEnd + settings.MinimumStopGapSeconds)
                {
                    cruise += previousEnd + settings.MinimumStopGapSeconds - end;
                    end = previousEnd + settings.MinimumStopGapSeconds;
                }
                previousEnd = end;
                plans[i] = new SlotReelSpinPlan(delay, acceleration, cruise, deceleration, settings, false, 0f);
            }
            return plans;
        }

        // Worst case over every jitter and the stop-gap correction, plus the total Fortune Critical wait (always added,
        // whether or not the spin has a total step); the controller's timeout is based on it.
        public static float GetMaximumPresentationSeconds(SlotReelSpinSettings settings, int reelCount)
        {
            reelCount = Mathf.Max(1, reelCount);
            var acceleration = Phase(settings.AccelerationSeconds, settings.AccelerationJitterSeconds);
            var deceleration = Phase(settings.DecelerationSeconds, settings.DecelerationJitterSeconds);
            var end = 0f;
            for (var i = 0; i < reelCount; i++)
            {
                var raw = i * settings.StartIntervalSeconds + settings.StartJitterSeconds + acceleration +
                    settings.CruiseSeconds + i * settings.StopIntervalSeconds + settings.CruiseJitterSeconds + deceleration;
                end = i == 0 ? raw : Mathf.Max(raw, end + settings.MinimumStopGapSeconds);
            }
            return Mathf.Max(end, reelCount * settings.ReducedMotionSecondsPerReel) + settings.TotalCriticalRevealDelaySeconds;
        }

        // Eased phases keep a small minimum so the easing is visible; a zero base (Instant) stays zero.
        private static float Phase(float baseSeconds, float jitter) =>
            baseSeconds <= 0f ? 0f : Mathf.Max(MinimumEasedPhaseSeconds, baseSeconds + jitter);

        private static float Jitter(System.Random random, float amplitude) =>
            amplitude <= 0f ? 0f : (float)((random.NextDouble() * 2d - 1d) * amplitude);
    }

    // Position (in cells) of one reel over time for a plan and a travel distance. The constant speed is chosen so the
    // travel ends exactly on the result: D = deltaCells + loops × cellCount and v = D / (n / k + c + m / k), with k the
    // easing slope at 0.5, which keeps position and speed continuous at the phase boundaries. Elastic and Back easings
    // move slightly below 0 at the start and beyond D before the stop.
    public readonly struct SlotReelSpinCurve
    {
        private readonly SlotEasingKind easing;
        private readonly float acceleration;
        private readonly float cruise;
        private readonly float deceleration;
        private readonly float accelerationDistance;
        private readonly float decelerationDistance;
        private readonly float fadeIn;
        private readonly float fadeOut;
        private readonly bool effects;

        public SlotReelSpinCurve(SlotReelSpinPlan plan, int deltaCells, int cellCount)
        {
            var settings = plan.Settings;
            easing = settings.Easing;
            acceleration = plan.AccelerationSeconds;
            cruise = plan.CruiseSeconds;
            deceleration = plan.DecelerationSeconds;
            fadeIn = settings.EffectFadeInSeconds;
            fadeOut = settings.EffectFadeOutSeconds;
            effects = settings.MotionEffectsEnabled && !plan.ReduceMotion;
            BoundarySlope = SlotEasing.BoundarySlope(easing);
            DurationSeconds = plan.ReduceMotion ? 0f : plan.MotionSeconds;
            cellCount = Mathf.Max(1, cellCount);
            deltaCells = ((deltaCells % cellCount) + cellCount) % cellCount;
            var weightedSeconds = acceleration / BoundarySlope + cruise + deceleration / BoundarySlope;
            if (DurationSeconds <= 0f || weightedSeconds <= 0f)
            {
                Loops = 0;
                Distance = deltaCells;
                Speed = 0f;
                DurationSeconds = 0f;
                accelerationDistance = decelerationDistance = 0f;
                return;
            }
            // Loops whose speed is closest to the target, at least 1 and never below the minimum speed.
            var ideal = (settings.CruiseCellsPerSecond * weightedSeconds - deltaCells) / cellCount;
            var lower = Mathf.Max(1, Mathf.FloorToInt(ideal));
            var upper = Mathf.Max(1, Mathf.CeilToInt(ideal));
            var lowerSpeed = (deltaCells + lower * cellCount) / weightedSeconds;
            var upperSpeed = (deltaCells + upper * cellCount) / weightedSeconds;
            var loops = Mathf.Abs(lowerSpeed - settings.CruiseCellsPerSecond) <= Mathf.Abs(upperSpeed - settings.CruiseCellsPerSecond) ? lower : upper;
            var minimumLoops = Mathf.CeilToInt((settings.MinimumCruiseCellsPerSecond * weightedSeconds - deltaCells) / cellCount);
            Loops = Mathf.Max(loops, minimumLoops, 1);
            Distance = deltaCells + Loops * cellCount;
            Speed = Distance / weightedSeconds;
            accelerationDistance = Speed * acceleration / BoundarySlope;
            decelerationDistance = Speed * deceleration / BoundarySlope;
        }

        public int Loops { get; }
        public float Distance { get; }
        public float Speed { get; }
        public float DurationSeconds { get; }
        public float BoundarySlope { get; }
        public bool HasMotion => DurationSeconds > 0f;

        // t is the time since this reel's start delay ended.
        public float Position(float t)
        {
            if (!HasMotion || t >= DurationSeconds) return Distance;
            if (t <= 0f) return 0f;
            if (t < acceleration)
                return accelerationDistance * 2f * SlotEasing.Evaluate(easing, t / acceleration * .5f);
            if (t < acceleration + cruise)
                return accelerationDistance + Speed * (t - acceleration);
            var u = (t - acceleration - cruise) / deceleration;
            return accelerationDistance + Speed * cruise + decelerationDistance * (2f * SlotEasing.Evaluate(easing, .5f + u * .5f) - 1f);
        }

        // 0 during the eased phases; rises over fadeIn after the start phase and reaches 0 again exactly when the stop
        // phase begins. A short constant-speed phase uses the smaller of the two ramps, so it may never reach 1.
        public float EffectStrength(float t)
        {
            if (!effects || !HasMotion || t < acceleration || t >= acceleration + cruise) return 0f;
            var rise = fadeIn <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, (t - acceleration) / fadeIn);
            var fall = fadeOut <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, (acceleration + cruise - t) / fadeOut);
            return Mathf.Min(rise, fall);
        }
    }
}
