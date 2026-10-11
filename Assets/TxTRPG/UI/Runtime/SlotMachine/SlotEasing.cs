using System;

namespace TxTRPG.UI.SlotMachine
{
    // Serialized by number in SlotReelSpinProfile assets: never renumber or reorder the values.
    public enum SlotEasingKind
    {
        Linear = 0,
        InOutSine = 1,
        InOutQuad = 2,
        InOutCubic = 3,
        InOutQuart = 4,
        InOutQuint = 5,
        InOutExpo = 6,
        InOutCirc = 7,
        InOutBack = 8,
        InOutElastic = 9,
        InOutBounce = 10
    }

    // InOut easing functions as defined on easings.net. Every kind satisfies E(0) = 0, E(0.5) = 0.5 and E(1) = 1, so the
    // first half can drive the start of a reel spin and the second half its stop. Pure and allocation free.
    public static class SlotEasing
    {
        public const float MinimumBoundarySlope = .5f;
        public const float MaximumBoundarySlope = 10f;

        public static float Evaluate(SlotEasingKind kind, float t)
        {
            var x = t <= 0f ? 0f : t >= 1f ? 1f : t;
            switch (kind)
            {
                case SlotEasingKind.InOutSine:
                    return (float)(-(Math.Cos(Math.PI * x) - 1d) / 2d);
                case SlotEasingKind.InOutQuad:
                    return x < .5f ? 2f * x * x : 1f - Pow(-2f * x + 2f, 2) / 2f;
                case SlotEasingKind.InOutCubic:
                    return x < .5f ? 4f * x * x * x : 1f - Pow(-2f * x + 2f, 3) / 2f;
                case SlotEasingKind.InOutQuart:
                    return x < .5f ? 8f * Pow(x, 4) : 1f - Pow(-2f * x + 2f, 4) / 2f;
                case SlotEasingKind.InOutQuint:
                    return x < .5f ? 16f * Pow(x, 5) : 1f - Pow(-2f * x + 2f, 5) / 2f;
                case SlotEasingKind.InOutExpo:
                    if (x <= 0f) return 0f;
                    if (x >= 1f) return 1f;
                    return x < .5f
                        ? (float)(Math.Pow(2d, 20d * x - 10d) / 2d)
                        : (float)((2d - Math.Pow(2d, -20d * x + 10d)) / 2d);
                case SlotEasingKind.InOutCirc:
                    return x < .5f
                        ? (float)((1d - Math.Sqrt(Math.Max(0d, 1d - 4d * x * x))) / 2d)
                        : (float)((Math.Sqrt(Math.Max(0d, 1d - Math.Pow(-2d * x + 2d, 2d))) + 1d) / 2d);
                case SlotEasingKind.InOutBack:
                {
                    const float c2 = 1.70158f * 1.525f;
                    return x < .5f
                        ? Pow(2f * x, 2) * ((c2 + 1f) * 2f * x - c2) / 2f
                        : (Pow(2f * x - 2f, 2) * ((c2 + 1f) * (x * 2f - 2f) + c2) + 2f) / 2f;
                }
                case SlotEasingKind.InOutElastic:
                {
                    if (x <= 0f) return 0f;
                    if (x >= 1f) return 1f;
                    const double c5 = 2d * Math.PI / 4.5d;
                    return x < .5f
                        ? (float)(-(Math.Pow(2d, 20d * x - 10d) * Math.Sin((20d * x - 11.125d) * c5)) / 2d)
                        : (float)(Math.Pow(2d, -20d * x + 10d) * Math.Sin((20d * x - 11.125d) * c5) / 2d + 1d);
                }
                case SlotEasingKind.InOutBounce:
                    return x < .5f ? (1f - OutBounce(1f - 2f * x)) / 2f : (1f + OutBounce(2f * x - 1f)) / 2f;
                default:
                    return x;
            }
        }

        // E'(0.5) by central difference, limited to [0.5, 10]. Kinds whose true slope is outside the range (InOutBounce
        // is about 0, InOutCirc is unbounded) show a small speed step where the eased phases meet the constant-speed phase.
        public static float BoundarySlope(SlotEasingKind kind)
        {
            const float h = .001f;
            var slope = (Evaluate(kind, .5f + h) - Evaluate(kind, .5f - h)) / (2f * h);
            if (float.IsNaN(slope)) return 1f;
            return slope < MinimumBoundarySlope ? MinimumBoundarySlope : slope > MaximumBoundarySlope ? MaximumBoundarySlope : slope;
        }

        private static float OutBounce(float x)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (x < 1f / d1) return n1 * x * x;
            if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + .75f; }
            if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + .9375f; }
            x -= 2.625f / d1;
            return n1 * x * x + .984375f;
        }

        private static float Pow(float value, int power)
        {
            var result = 1f;
            for (var i = 0; i < power; i++) result *= value;
            return result;
        }
    }
}
