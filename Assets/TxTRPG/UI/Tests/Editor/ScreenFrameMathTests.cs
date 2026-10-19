using NUnit.Framework;
using TxTRPG.UI.ScreenFrame;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class ScreenFrameMathTests
    {
        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private const float Tolerance = .01f;

        private static ScreenFrameLayout Full(float width, float height) =>
            ScreenFrameMath.Calculate(new Vector2(width, height), new Rect(0f, 0f, width, height), Reference);

        [Test]
        public void Reference16x9_ScaleOneWithoutBars()
        {
            var layout = Full(1920f, 1080f);
            Assert.That(layout.IsValid);
            Assert.That(layout.ScaleFactor, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(layout.FrameRect, Is.EqualTo(new Rect(0f, 0f, 1920f, 1080f)));
            AssertBars(layout, 0f, 0f, 0f, 0f);
        }

        [Test]
        public void Uhd_ScaleTwo()
        {
            var layout = Full(3840f, 2160f);
            Assert.That(layout.ScaleFactor, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(layout.CanvasSize, Is.EqualTo(new Vector2(1920f, 1080f)));
            AssertBars(layout, 0f, 0f, 0f, 0f);
        }

        [Test]
        public void Ultrawide_LeftAndRightBars320()
        {
            var layout = Full(2560f, 1080f);
            Assert.That(layout.ScaleFactor, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(layout.FrameRect.x, Is.EqualTo(320f).Within(Tolerance));
            AssertBars(layout, 0f, 0f, 320f, 320f);
            Assert.That(layout.LeftBar.height, Is.EqualTo(1080f).Within(Tolerance));
        }

        [Test]
        public void FourByThree_TopAndBottomBars()
        {
            var layout = Full(1024f, 768f);
            Assert.That(layout.ScaleFactor, Is.EqualTo(1024f / 1920f).Within(.0001f));
            // Canvas 1920x1440: (1440 - 1080) / 2 = 180 above and below.
            AssertBars(layout, 180f, 180f, 0f, 0f);
            Assert.That(layout.TopBar.width, Is.EqualTo(1920f).Within(Tolerance));
        }

        [Test]
        public void Portrait_TopAndBottomBars()
        {
            var layout = Full(1080f, 1920f);
            Assert.That(layout.ScaleFactor, Is.EqualTo(1080f / 1920f).Within(.0001f));
            var canvasHeight = 1920f / layout.ScaleFactor;
            var bar = (canvasHeight - 1080f) * .5f;
            AssertBars(layout, bar, bar, 0f, 0f);
        }

        [Test]
        public void NotchedSafeArea_FrameCenteredInsideSafeArea()
        {
            // 2400x1080 with a 100 px left cutout: safe 2300 wide, frame 1920 centered in it.
            var layout = ScreenFrameMath.Calculate(new Vector2(2400f, 1080f), new Rect(100f, 0f, 2300f, 1080f), Reference);
            Assert.That(layout.ScaleFactor, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(layout.FrameRect.x, Is.EqualTo(100f + 190f).Within(Tolerance));
            Assert.That(layout.FrameRect.xMin, Is.GreaterThanOrEqualTo(layout.SafeRect.xMin - Tolerance));
            Assert.That(layout.FrameRect.xMax, Is.LessThanOrEqualTo(layout.SafeRect.xMax + Tolerance));
            AssertBars(layout, 0f, 0f, 290f, 190f);
        }

        [Test]
        public void ShortSafeArea_ScalesToSafeHeight()
        {
            var layout = ScreenFrameMath.Calculate(new Vector2(1920f, 1080f), new Rect(0f, 0f, 1920f, 1000f), Reference);
            Assert.That(layout.ScaleFactor, Is.EqualTo(1000f / 1080f).Within(.0001f));
            Assert.That(layout.FrameRect.yMin, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void EmptySafeArea_UsesWholeScreen()
        {
            var layout = ScreenFrameMath.Calculate(new Vector2(2560f, 1080f), new Rect(0f, 0f, 0f, 0f), Reference);
            Assert.That(layout.FrameRect.x, Is.EqualTo(320f).Within(Tolerance));
        }

        [Test]
        public void SafeAreaOutsideScreen_UsesWholeScreen()
        {
            var layout = ScreenFrameMath.Calculate(new Vector2(1920f, 1080f), new Rect(5000f, 5000f, 100f, 100f), Reference);
            Assert.That(layout.ScaleFactor, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void SafeAreaPartlyOutsideScreen_UsesOverlap()
        {
            var layout = ScreenFrameMath.Calculate(new Vector2(2400f, 1080f), new Rect(-100f, 0f, 2300f, 1080f), Reference);
            // Overlap 0..2200: frame centered at 1100.
            Assert.That(layout.FrameRect.center.x, Is.EqualTo(1100f).Within(Tolerance));
        }

        [TestCase(0f, 1080f)]
        [TestCase(1920f, 0f)]
        [TestCase(-1f, 1080f)]
        [TestCase(float.NaN, 1080f)]
        [TestCase(1920f, float.NaN)]
        public void InvalidScreen_IsInvalid(float width, float height)
        {
            var layout = ScreenFrameMath.Calculate(new Vector2(width, height), new Rect(0f, 0f, 100f, 100f), Reference);
            Assert.That(layout.IsValid, Is.False);
        }

        [Test]
        public void ReferenceBelowOne_IsClampedToOne()
        {
            Assert.That(ScreenFrameMath.SanitizeReference(new Vector2(0f, -5f)), Is.EqualTo(Vector2.one));
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1024f, 768f)]
        [TestCase(1080f, 1920f)]
        [TestCase(2400f, 1080f)]
        public void BarsAndFrame_CoverTheCanvas(float width, float height)
        {
            var layout = ScreenFrameMath.Calculate(new Vector2(width, height), new Rect(130f, 20f, width - 200f, height - 40f), Reference);
            var total = Area(layout.FrameRect) + Area(layout.TopBar) + Area(layout.BottomBar) + Area(layout.LeftBar) + Area(layout.RightBar);
            var canvas = layout.CanvasSize.x * layout.CanvasSize.y;
            Assert.That(total, Is.EqualTo(canvas).Within(canvas * 1e-5f));
        }

        private static float Area(Rect rect) => rect.width * rect.height;

        private static void AssertBars(ScreenFrameLayout layout, float top, float bottom, float left, float right)
        {
            Assert.That(layout.TopBar.height, Is.EqualTo(top).Within(Tolerance), "top");
            Assert.That(layout.BottomBar.height, Is.EqualTo(bottom).Within(Tolerance), "bottom");
            Assert.That(layout.LeftBar.width, Is.EqualTo(left).Within(Tolerance), "left");
            Assert.That(layout.RightBar.width, Is.EqualTo(right).Within(Tolerance), "right");
        }
    }
}
