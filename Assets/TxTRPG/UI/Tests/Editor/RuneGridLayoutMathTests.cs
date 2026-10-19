using NUnit.Framework;
using TxTRPG.UI.Runes;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class RuneGridLayoutMathTests
    {
        private const float Tolerance = .001f;
        private static readonly RuneGridLayoutSettings Defaults = new();

        [TestCase(0f), TestCase(30f), TestCase(64f), TestCase(100f), TestCase(200f), TestCase(400f), TestCase(1000f), TestCase(2000f)]
        public void Calculate_NeverExceedsTheAvailableWidth(float width)
        {
            var result = RuneGridLayoutMath.Calculate(width, 7, Defaults);
            Assert.That(result.Columns, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.UsedWidth, Is.LessThanOrEqualTo(Mathf.Max(0f, width) + Tolerance));
            Assert.That(result.CardSize, Is.LessThanOrEqualTo(Defaults.MaximumCardSize + Tolerance));
            Assert.That(result.CellSize, Is.EqualTo(result.CardSize * (1f + Defaults.BadgeOverhangRatio)).Within(Tolerance));
            if (width >= Defaults.MinimumCardSize * (1f + Defaults.BadgeOverhangRatio))
                Assert.That(result.CardSize, Is.GreaterThanOrEqualTo(Defaults.MinimumCardSize - Tolerance));
        }

        [Test]
        public void Calculate_NarrowerThanOneMinimumCell_ShrinksToOneColumn()
        {
            var result = RuneGridLayoutMath.Calculate(30f, 3, Defaults);
            Assert.That(result.Columns, Is.EqualTo(1));
            Assert.That(result.CellSize, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(result.CardSize, Is.LessThan(Defaults.MinimumCardSize));
            Assert.That(result.Rows, Is.EqualTo(3));
        }

        [Test]
        public void Calculate_WideArea_AddsColumnsInsteadOfExceedingTheMaximum()
        {
            var result = RuneGridLayoutMath.Calculate(2000f, 2, Defaults);
            // ceil((2000 + 8) / (120 × 1.13 + 8)) = 14 columns, each just under the maximum.
            Assert.That(result.Columns, Is.EqualTo(14));
            Assert.That(result.CardSize, Is.LessThanOrEqualTo(Defaults.MaximumCardSize + Tolerance));
            Assert.That(result.CardSize, Is.GreaterThan(Defaults.MaximumCardSize - 1f));
            Assert.That(result.UsedWidth, Is.EqualTo(2000f).Within(.01f));
        }

        [Test]
        public void Calculate_ExactMaximumFit_UsesTheMaximumCard()
        {
            var cell = Defaults.MaximumCardSize * (1f + Defaults.BadgeOverhangRatio);
            var result = RuneGridLayoutMath.Calculate(cell * 2f + 8f - .005f, 2, Defaults);
            Assert.That(result.Columns, Is.EqualTo(2));
            Assert.That(result.CardSize, Is.EqualTo(Defaults.MaximumCardSize).Within(.01f));
        }

        [Test]
        public void Calculate_ContentHeight_IncludesPaddingRowsAndGaps()
        {
            var result = RuneGridLayoutMath.Calculate(200f, 5, Defaults);
            var rows = Mathf.CeilToInt(5f / result.Columns);
            Assert.That(result.Rows, Is.EqualTo(rows));
            Assert.That(result.ContentHeight, Is.EqualTo(16f + rows * result.CellSize + (rows - 1) * 8f).Within(Tolerance));
            Assert.That(RuneGridLayoutMath.Calculate(200f, 0, Defaults).ContentHeight, Is.EqualTo(16f).Within(Tolerance));
        }

        [Test]
        public void Calculate_InvalidWidth_ReturnsOneColumnOfSizeZero()
        {
            foreach (var width in new[] { 0f, -10f, float.NaN })
            {
                var result = RuneGridLayoutMath.Calculate(width, 3, Defaults);
                Assert.That(result.Columns, Is.EqualTo(1));
                Assert.That(result.CellSize, Is.Zero);
                Assert.That(result.CardSize, Is.Zero);
            }
        }

        [Test]
        public void Calculate_BadgeSizeAndOverhang_FollowTheCard()
        {
            var result = RuneGridLayoutMath.Calculate(400f, 4, Defaults);
            Assert.That(result.BadgeSize, Is.EqualTo(result.CardSize * .33f).Within(Tolerance));
            Assert.That(result.BadgeOverhang, Is.EqualTo(result.CardSize * .13f).Within(Tolerance));
        }

        [Test]
        public void Settings_ClampInvalidValues()
        {
            var settings = new RuneGridLayoutSettings(-5f, -10f, -3f, new RuneGridPadding(-1, -2, -3, -4), 2f, float.NaN);
            Assert.That(settings.MinimumCardSize, Is.EqualTo(1f));
            Assert.That(settings.MaximumCardSize, Is.EqualTo(1f));
            Assert.That(settings.Spacing, Is.Zero);
            Assert.That(settings.PaddingLeft + settings.PaddingRight + settings.PaddingTop + settings.PaddingBottom, Is.Zero);
            Assert.That(settings.BadgeSizeRatio, Is.EqualTo(1f));
            Assert.That(settings.BadgeOverhangRatio, Is.EqualTo(RuneGridLayoutSettings.DefaultBadgeOverhangRatio));
            Assert.That(RuneGridLayoutMath.Calculate(100f, 2, null).Columns, Is.GreaterThanOrEqualTo(1));
        }
    }
}
