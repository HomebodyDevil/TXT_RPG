using NUnit.Framework;
using TxTRPG.UI.Tooltips;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class TooltipPlacementTests
    {
        private static readonly Rect Frame = new(0f, 0f, 1920f, 1080f);
        private const float Margin = 8f;

        [Test]
        public void Default_TopLeftOnTheAnchorTopRight()
        {
            var result = TooltipPlacement.Place(new Rect(100f, 600f, 120f, 120f), new Vector2(300f, 200f), Frame, Margin);
            Assert.That(result.TopLeft, Is.EqualTo(new Vector2(220f, 720f)));
            Assert.That(result.FlippedX || result.FlippedY || result.PushedX || result.PushedY, Is.False);
        }

        [Test]
        public void RightOverflow_FlipsToTheAnchorLeftEdge()
        {
            var result = TooltipPlacement.Place(new Rect(1700f, 600f, 120f, 120f), new Vector2(300f, 200f), Frame, Margin);
            Assert.That(result.TopLeft, Is.EqualTo(new Vector2(1400f, 720f)));
            Assert.That(result.FlippedX, Is.True);
            Assert.That(result.FlippedY, Is.False);
        }

        [Test]
        public void BottomOverflow_FlipsToTheAnchorBottomEdge()
        {
            var result = TooltipPlacement.Place(new Rect(100f, 50f, 120f, 120f), new Vector2(300f, 400f), Frame, Margin);
            Assert.That(result.TopLeft, Is.EqualTo(new Vector2(220f, 450f)), "the box bottom is the anchor bottom");
            Assert.That(result.FlippedY, Is.True);
        }

        [Test]
        public void BothAxes_Flip()
        {
            var result = TooltipPlacement.Place(new Rect(1700f, 50f, 120f, 120f), new Vector2(300f, 400f), Frame, Margin);
            Assert.That(result.TopLeft, Is.EqualTo(new Vector2(1400f, 450f)));
            Assert.That(result.FlippedX && result.FlippedY, Is.True);
        }

        [Test]
        public void StillOverflowing_IsPushedInside()
        {
            // Wide anchor: neither side fits 400 units; the flipped box is pushed back inside the left frame margin.
            var result = TooltipPlacement.Place(new Rect(200f, 600f, 1500f, 100f), new Vector2(400f, 200f), Frame, Margin);
            Assert.That(result.FlippedX && result.PushedX, Is.True);
            Assert.That(result.TopLeft.x, Is.EqualTo(Margin));
            // Tall box near the bottom: flipped up it would leave the top, so it is pushed below the top margin.
            result = TooltipPlacement.Place(new Rect(100f, 100f, 100f, 100f), new Vector2(300f, 1000f), Frame, Margin);
            Assert.That(result.PushedY, Is.True);
            Assert.That(result.TopLeft.y, Is.EqualTo(1080f - Margin));
            Assert.That(result.TopLeft.y - 1000f, Is.GreaterThanOrEqualTo(Margin));
        }

        [Test]
        public void LargerThanTheFrame_KeepsTheTopLeftMargin()
        {
            var result = TooltipPlacement.Place(new Rect(500f, 500f, 100f, 100f), new Vector2(3000f, 2000f), Frame, Margin);
            Assert.That(result.TopLeft, Is.EqualTo(new Vector2(Margin, 1080f - Margin)));
        }

        [Test]
        public void Margin_IsRespectedAndAnchorAboveTheFrameIsPushedDown()
        {
            var result = TooltipPlacement.Place(new Rect(100f, 1000f, 100f, 200f), new Vector2(200f, 100f), Frame, Margin);
            Assert.That(result.TopLeft.y, Is.EqualTo(1080f - Margin));
            Assert.That(result.PushedY, Is.True);
            result = TooltipPlacement.Place(new Rect(1500f, 500f, 100f, 100f), new Vector2(312f, 100f), Frame, Margin);
            Assert.That(result.FlippedX, Is.False, "exactly touching the margin fits");
            result = TooltipPlacement.Place(new Rect(1500f, 500f, 100f, 100f), new Vector2(313f, 100f), Frame, Margin);
            Assert.That(result.FlippedX, Is.True);
        }
    }
}
