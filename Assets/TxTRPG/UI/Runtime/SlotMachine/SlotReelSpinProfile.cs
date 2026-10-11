using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    // Validated, immutable copy of the reel spin settings. SlotMachinePanel plans every spin from this value, so a
    // missing profile asset falls back to Default and Edit Mode tests can inject Instant.
    public readonly struct SlotReelSpinSettings
    {
        public const float MaximumJitterRatio = .5f;
        public const int MaximumAfterimageCount = 3;

        public SlotReelSpinSettings(SlotEasingKind easing,
            float startIntervalSeconds, float stopIntervalSeconds, float minimumStopGapSeconds,
            float accelerationSeconds, float cruiseSeconds, float decelerationSeconds,
            float startJitterSeconds, float accelerationJitterSeconds, float cruiseJitterSeconds, float decelerationJitterSeconds,
            float cruiseCellsPerSecond, float minimumCruiseCellsPerSecond,
            bool motionEffectsEnabled, float effectFadeInSeconds, float effectFadeOutSeconds,
            int afterimageCount, float afterimageSpacing, float afterimageAlpha, float maximumVerticalStretch, float cruiseTextAlpha,
            float reducedMotionSecondsPerReel)
        {
            Easing = easing;
            StartIntervalSeconds = Positive(startIntervalSeconds);
            StopIntervalSeconds = Positive(stopIntervalSeconds);
            MinimumStopGapSeconds = Positive(minimumStopGapSeconds);
            AccelerationSeconds = Positive(accelerationSeconds);
            CruiseSeconds = Positive(cruiseSeconds);
            DecelerationSeconds = Positive(decelerationSeconds);
            StartJitterSeconds = Jitter(startJitterSeconds, StartIntervalSeconds);
            AccelerationJitterSeconds = Jitter(accelerationJitterSeconds, AccelerationSeconds);
            CruiseJitterSeconds = Jitter(cruiseJitterSeconds, CruiseSeconds);
            DecelerationJitterSeconds = Jitter(decelerationJitterSeconds, DecelerationSeconds);
            MinimumCruiseCellsPerSecond = Mathf.Max(.1f, minimumCruiseCellsPerSecond);
            CruiseCellsPerSecond = Mathf.Max(MinimumCruiseCellsPerSecond, cruiseCellsPerSecond);
            MotionEffectsEnabled = motionEffectsEnabled;
            EffectFadeInSeconds = Positive(effectFadeInSeconds);
            EffectFadeOutSeconds = Positive(effectFadeOutSeconds);
            AfterimageCount = Mathf.Clamp(afterimageCount, 0, MaximumAfterimageCount);
            AfterimageSpacing = Mathf.Clamp(afterimageSpacing, 0f, 1f);
            AfterimageAlpha = Mathf.Clamp01(afterimageAlpha);
            MaximumVerticalStretch = Mathf.Clamp(maximumVerticalStretch, 1f, 3f);
            CruiseTextAlpha = Mathf.Clamp01(cruiseTextAlpha);
            ReducedMotionSecondsPerReel = Positive(reducedMotionSecondsPerReel);
        }

        public SlotEasingKind Easing { get; }
        public float StartIntervalSeconds { get; }
        public float StopIntervalSeconds { get; }
        public float MinimumStopGapSeconds { get; }
        public float AccelerationSeconds { get; }
        public float CruiseSeconds { get; }
        public float DecelerationSeconds { get; }
        public float StartJitterSeconds { get; }
        public float AccelerationJitterSeconds { get; }
        public float CruiseJitterSeconds { get; }
        public float DecelerationJitterSeconds { get; }
        public float CruiseCellsPerSecond { get; }
        public float MinimumCruiseCellsPerSecond { get; }
        public bool MotionEffectsEnabled { get; }
        public float EffectFadeInSeconds { get; }
        public float EffectFadeOutSeconds { get; }
        public int AfterimageCount { get; }
        public float AfterimageSpacing { get; }
        public float AfterimageAlpha { get; }
        public float MaximumVerticalStretch { get; }
        public float CruiseTextAlpha { get; }
        public float ReducedMotionSecondsPerReel { get; }

        // Same values as a new SlotReelSpinProfile asset.
        public static SlotReelSpinSettings Default => new(SlotEasingKind.InOutElastic,
            .08f, .22f, .12f,
            .35f, .7f, .7f,
            .03f, .05f, .12f, .08f,
            18f, 8f,
            true, .12f, .15f,
            2, .18f, .35f, 1.25f, .85f,
            .15f);

        // Verification only: every reel lands on its result in the same call, without waiting for frames.
        public static SlotReelSpinSettings Instant => new(SlotEasingKind.Linear,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 18f, 8f, false, 0f, 0f, 0, 0f, 0f, 1f, 1f, 0f);

        private static float Positive(float value) => float.IsNaN(value) ? 0f : Mathf.Max(0f, value);
        private static float Jitter(float value, float baseValue) => Mathf.Min(Positive(value), baseValue * MaximumJitterRatio);
    }

    // Developer-tunable reel spin presentation. The default asset is
    // Assets/TxTRPG/UI/Styles/SlotMachine/DefaultSlotReelSpinProfile.asset, referenced by SlotMachinePanel.prefab.
    // Only the presentation changes; results, rune allocation and combat never read it.
    [CreateAssetMenu(menuName = "TxT RPG/Slot Machine/Reel Spin Profile", fileName = "SlotReelSpinProfile")]
    public sealed class SlotReelSpinProfile : ScriptableObject
    {
        [Header("Easing")]
        [SerializeField, Tooltip("One InOut easing: its first half drives the start phase, its second half the stop phase. " +
            "The speed where the eased phases meet the constant-speed phase uses E'(0.5) limited to [0.5, 10]; InOutBounce and " +
            "InOutCirc fall outside that range, so their speed steps slightly at the phase boundaries.")]
        private SlotEasingKind easing = SlotEasingKind.InOutElastic;

        [Header("Stagger")]
        [SerializeField, Min(0f), Tooltip("Reel i starts i × this many seconds after the first reel.")]
        private float startIntervalSeconds = .08f;
        [SerializeField, Min(0f), Tooltip("Reel i's constant-speed phase is longer by i × this many seconds.")]
        private float stopIntervalSeconds = .22f;
        [SerializeField, Min(0f), Tooltip("Minimum time between neighbouring reels stopping. Reels always stop left to right.")]
        private float minimumStopGapSeconds = .12f;

        [Header("Phase base durations")]
        [SerializeField, Min(0f), Tooltip("Start phase (n): first half of the easing.")]
        private float accelerationSeconds = .35f;
        [SerializeField, Min(0f), Tooltip("Constant-speed phase of the first reel.")]
        private float cruiseSeconds = .7f;
        [SerializeField, Min(0f), Tooltip("Stop phase (m): second half of the easing.")]
        private float decelerationSeconds = .7f;

        [Header("Jitter (each at most 50% of its base value)")]
        [SerializeField, Min(0f)] private float startJitterSeconds = .03f;
        [SerializeField, Min(0f)] private float accelerationJitterSeconds = .05f;
        [SerializeField, Min(0f)] private float cruiseJitterSeconds = .12f;
        [SerializeField, Min(0f)] private float decelerationJitterSeconds = .08f;

        [Header("Speed")]
        [SerializeField, Min(.1f), Tooltip("Target constant speed in cells per second. The actual speed is adjusted so the reel lands exactly on its result.")]
        private float cruiseCellsPerSecond = 18f;
        [SerializeField, Min(.1f), Tooltip("Lower bound of the adjusted constant speed.")]
        private float minimumCruiseCellsPerSecond = 8f;

        [Header("High-speed effect (afterimages and vertical stretch, no shader)")]
        [SerializeField] private bool motionEffectsEnabled = true;
        [SerializeField, Min(0f), Tooltip("After the start phase, the effect rises from 0 to 1 over this time.")]
        private float effectFadeInSeconds = .12f;
        [SerializeField, Min(0f), Tooltip("The effect falls from 1 to 0 over this time and is 0 when the stop phase begins.")]
        private float effectFadeOutSeconds = .15f;
        [SerializeField, Range(0, SlotReelSpinSettings.MaximumAfterimageCount), Tooltip("Afterimages per cell (at most the afterimage texts in SlotReel.prefab).")]
        private int afterimageCount = 2;
        [SerializeField, Range(0f, 1f), Tooltip("Afterimage spacing as a fraction of the cell height.")]
        private float afterimageSpacing = .18f;
        [SerializeField, Range(0f, 1f), Tooltip("Alpha of the first afterimage at full effect; later ones are fainter.")]
        private float afterimageAlpha = .35f;
        [SerializeField, Range(1f, 3f), Tooltip("Vertical scale of each cell's VisualRoot at full effect.")]
        private float maximumVerticalStretch = 1.25f;
        [SerializeField, Range(0f, 1f), Tooltip("Alpha multiplier of the cell text at full effect.")]
        private float cruiseTextAlpha = .85f;

        [Header("Reduce Motion")]
        [SerializeField, Min(0f), Tooltip("With Reduce Motion, reel i shows its result after (i + 1) × this many seconds, without effects.")]
        private float reducedMotionSecondsPerReel = .15f;

        public SlotReelSpinSettings Settings => new(easing,
            startIntervalSeconds, stopIntervalSeconds, minimumStopGapSeconds,
            accelerationSeconds, cruiseSeconds, decelerationSeconds,
            startJitterSeconds, accelerationJitterSeconds, cruiseJitterSeconds, decelerationJitterSeconds,
            cruiseCellsPerSecond, minimumCruiseCellsPerSecond,
            motionEffectsEnabled, effectFadeInSeconds, effectFadeOutSeconds,
            afterimageCount, afterimageSpacing, afterimageAlpha, maximumVerticalStretch, cruiseTextAlpha,
            reducedMotionSecondsPerReel);

        // Only clamps this asset's own values; never touches other assets or Scenes.
        private void OnValidate()
        {
            var max = SlotReelSpinSettings.MaximumJitterRatio;
            startJitterSeconds = Mathf.Clamp(startJitterSeconds, 0f, startIntervalSeconds * max);
            accelerationJitterSeconds = Mathf.Clamp(accelerationJitterSeconds, 0f, accelerationSeconds * max);
            cruiseJitterSeconds = Mathf.Clamp(cruiseJitterSeconds, 0f, cruiseSeconds * max);
            decelerationJitterSeconds = Mathf.Clamp(decelerationJitterSeconds, 0f, decelerationSeconds * max);
            cruiseCellsPerSecond = Mathf.Max(cruiseCellsPerSecond, minimumCruiseCellsPerSecond);
        }
    }
}
