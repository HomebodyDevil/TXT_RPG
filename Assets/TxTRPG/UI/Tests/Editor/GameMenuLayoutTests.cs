using NUnit.Framework;
using TxTRPG.UI.Windows;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class GameMenuLayoutTests
    {
        private static readonly Vector2 ButtonSize = new(150, 48);
        private static readonly Vector2 Spacing = new(12, 12);
        private static readonly RectOffset Padding = new(12, 12, 8, 8);

        [TestCase(474, 3)]
        [TestCase(473, 2)]
        public void Wrap_UsesExactColumnBoundary(float innerWidth, int expectedColumns)
        {
            var viewportWidth = innerWidth + Padding.horizontal;
            var result = GameMenuLayoutCalculator.Calculate(GameMenuLayoutMode.Wrap, 5,
                viewportWidth, 500, ButtonSize, Spacing, Padding,
                GameMenuWrapColumnPolicy.AutoFit, 99);
            Assert.That(result.Columns, Is.EqualTo(expectedColumns));
        }

        [Test]
        public void Wrap_FiveButtonsInThreeColumns_RequiresTwoRows()
        {
            var result = GameMenuLayoutCalculator.Calculate(GameMenuLayoutMode.Wrap, 5,
                600, 124, ButtonSize, Spacing, Padding,
                GameMenuWrapColumnPolicy.AutoFit, 99);
            Assert.That(result.Columns, Is.EqualTo(3));
            Assert.That(result.Rows, Is.EqualTo(2));
            Assert.That(result.RequiredHeight, Is.EqualTo(124));
            Assert.That(result.HasInsufficientSpace, Is.False);
        }

        [Test]
        public void MaximumColumns_IsAnUpperBoundRatherThanFixedCount()
        {
            var wide = GameMenuLayoutCalculator.Calculate(GameMenuLayoutMode.Wrap, 20,
                1600, 500, ButtonSize, Spacing, Padding,
                GameMenuWrapColumnPolicy.MaximumColumns, 4);
            var narrow = GameMenuLayoutCalculator.Calculate(GameMenuLayoutMode.Wrap, 20,
                350, 800, ButtonSize, Spacing, Padding,
                GameMenuWrapColumnPolicy.MaximumColumns, 4);
            Assert.That(wide.Columns, Is.EqualTo(4));
            Assert.That(narrow.Columns, Is.EqualTo(2));
        }

        [Test]
        public void LastRowAlignment_DoesNotReverseOrder()
        {
            const float viewportWidth = 600;
            const float twoButtonRowWidth = 312;
            Assert.That(GameMenuLayoutCalculator.RowStartX(viewportWidth, twoButtonRowWidth,
                Padding, GameMenuHorizontalAlignment.Left), Is.EqualTo(12));
            Assert.That(GameMenuLayoutCalculator.RowStartX(viewportWidth, twoButtonRowWidth,
                Padding, GameMenuHorizontalAlignment.Center), Is.EqualTo(144));
            Assert.That(GameMenuLayoutCalculator.RowStartX(viewportWidth, twoButtonRowWidth,
                Padding, GameMenuHorizontalAlignment.Right), Is.EqualTo(276));
        }

        [Test]
        public void HorizontalScroll_PreservesButtonSizeAndCalculatesCompleteWidth()
        {
            var result = GameMenuLayoutCalculator.Calculate(GameMenuLayoutMode.HorizontalScroll,
                3, 300, 64, ButtonSize, Spacing, Padding,
                GameMenuWrapColumnPolicy.AutoFit, 4);
            Assert.That(result.RequiredWidth, Is.EqualTo(498));
            Assert.That(result.RequiredHeight, Is.EqualTo(64));
            Assert.That(result.Columns, Is.EqualTo(3));
            Assert.That(result.Rows, Is.EqualTo(1));
        }

        [Test]
        public void EmptyAndNarrowLayouts_ReportTheirContracts()
        {
            var empty = GameMenuLayoutCalculator.Calculate(GameMenuLayoutMode.Wrap, 0,
                100, 16, ButtonSize, Spacing, Padding,
                GameMenuWrapColumnPolicy.AutoFit, 4);
            var narrow = GameMenuLayoutCalculator.Calculate(GameMenuLayoutMode.Wrap, 1,
                160, 64, ButtonSize, Spacing, Padding,
                GameMenuWrapColumnPolicy.AutoFit, 4);
            Assert.That(empty.Rows, Is.Zero);
            Assert.That(empty.RequiredHeight, Is.EqualTo(16));
            Assert.That(narrow.Columns, Is.EqualTo(1));
            Assert.That(narrow.InsufficientWidth, Is.True);
        }

        [TestCase(GameMenuScrollbarVisibility.Hidden, false, false)]
        [TestCase(GameMenuScrollbarVisibility.Hidden, true, false)]
        [TestCase(GameMenuScrollbarVisibility.Auto, false, false)]
        [TestCase(GameMenuScrollbarVisibility.Auto, true, true)]
        [TestCase(GameMenuScrollbarVisibility.Always, false, true)]
        public void ScrollbarVisibility_FollowsOverflowPolicy(GameMenuScrollbarVisibility visibility,
            bool overflow, bool expected)
        {
            Assert.That(GameMenuLayoutCalculator.ShouldShowScrollbar(visibility, overflow), Is.EqualTo(expected));
        }

        [TestCase(GameMenuScrollbarSpaceMode.Overlay, false, false)]
        [TestCase(GameMenuScrollbarSpaceMode.Overlay, true, false)]
        [TestCase(GameMenuScrollbarSpaceMode.ReserveAlways, false, true)]
        [TestCase(GameMenuScrollbarSpaceMode.ReserveWhenVisible, false, false)]
        [TestCase(GameMenuScrollbarSpaceMode.ReserveWhenVisible, true, true)]
        public void ScrollbarSpace_FollowsExplicitPolicy(GameMenuScrollbarSpaceMode mode,
            bool visible, bool expected)
        {
            Assert.That(GameMenuLayoutCalculator.ShouldReserveScrollbarSpace(mode, visible), Is.EqualTo(expected));
        }
    }
}
