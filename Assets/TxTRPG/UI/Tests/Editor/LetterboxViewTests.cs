using System.Text.RegularExpressions;
using NUnit.Framework;
using TxTRPG.UI.ScreenFrame;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.UI.Tests
{
    public sealed class LetterboxViewTests
    {
        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        private GameObject layer;
        private LetterboxView view;
        private ScreenFrameProfile profile;
        private GameObject barsPrefab;
        private GameObject plainPrefab;

        [SetUp]
        public void SetUp()
        {
            layer = new GameObject("LetterboxLayer", typeof(RectTransform));
            view = layer.AddComponent<LetterboxView>();
            profile = ScriptableObject.CreateInstance<ScreenFrameProfile>();
            // Template objects stand in for Prefabs; Instantiate copies them the same way.
            barsPrefab = LetterboxBarsPresentation.Create(null, Color.blue).gameObject;
            barsPrefab.name = "CustomBars";
            foreach (var image in barsPrefab.GetComponentsInChildren<Image>(true)) image.raycastTarget = true;
            plainPrefab = new GameObject("NoPresentation", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(layer);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(barsPrefab);
            Object.DestroyImmediate(plainPrefab);
        }

        private static ScreenFrameLayout Ultrawide() =>
            ScreenFrameMath.Calculate(new Vector2(2560f, 1080f), new Rect(0f, 0f, 2560f, 1080f), Reference);

        private static ScreenFrameLayout FourByThree() =>
            ScreenFrameMath.Calculate(new Vector2(1024f, 768f), new Rect(0f, 0f, 1024f, 768f), Reference);

        [Test]
        public void Initialize_CreatesProfilePresentationAndForwardsLayout()
        {
            profile.Configure(Reference, true, barsPrefab, Color.black);
            view.SetProfile(profile);
            view.ApplyLayout(Ultrawide());
            view.Initialize();

            Assert.That(view.IsUsingFallback, Is.False);
            Assert.That(view.PresentationInstance.transform.parent, Is.EqualTo(layer.transform));
            Assert.That(layer.transform.childCount, Is.EqualTo(1));
            var bars = (LetterboxBarsPresentation)view.Presentation;
            Assert.That(bars.Left.sizeDelta, Is.EqualTo(new Vector2(320f, 1080f)));
            Assert.That(bars.Right.anchoredPosition, Is.EqualTo(new Vector2(2240f, 0f)));
        }

        [Test]
        public void SetPresentation_DestroysPreviousAndAppliesLastLayout()
        {
            profile.Configure(Reference, true, barsPrefab, Color.black);
            view.SetProfile(profile);
            view.Initialize();
            view.ApplyLayout(FourByThree());
            var previous = view.PresentationInstance;

            view.SetPresentation(null);

            Assert.That(previous == null, "the previous instance is destroyed");
            Assert.That(layer.transform.childCount, Is.EqualTo(1));
            Assert.That(view.IsUsingFallback);
            var bars = (LetterboxBarsPresentation)view.Presentation;
            Assert.That(bars.Top.sizeDelta.y, Is.EqualTo(180f).Within(.01f));
            Assert.That(bars.Bottom.sizeDelta.y, Is.EqualTo(180f).Within(.01f));
        }

        [Test]
        public void MissingPresentationPrefab_UsesFallbackBars()
        {
            profile.Configure(Reference, true, null, Color.red);
            view.SetProfile(profile);
            view.Initialize();
            Assert.That(view.IsUsingFallback);
            Assert.That(((LetterboxBarsPresentation)view.Presentation).TopImage.color, Is.EqualTo(Color.red));
        }

        [Test]
        public void MissingProfile_UsesBlackFallbackBars()
        {
            LogAssert.Expect(LogType.Error, new Regex("no ScreenFrameProfile"));
            view.Initialize();
            Assert.That(view.IsUsingFallback);
            Assert.That(((LetterboxBarsPresentation)view.Presentation).TopImage.color, Is.EqualTo(Color.black));
        }

        [Test]
        public void PrefabWithoutInterface_UsesFallbackBars()
        {
            profile.Configure(Reference, true, plainPrefab, Color.black);
            view.SetProfile(profile);
            LogAssert.Expect(LogType.Error, new Regex("no ILetterboxPresentation"));
            view.Initialize();
            Assert.That(view.IsUsingFallback);
            Assert.That(layer.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingPresentation_IsReplacedWithFallbackBars()
        {
            profile.Configure(Reference, true, null, Color.black);
            view.SetProfile(profile);
            var throwing = new GameObject("Throwing", typeof(RectTransform));
            throwing.transform.SetParent(layer.transform, false);
            view.AttachPresentationForEditor(throwing, new ThrowingPresentation());
            LogAssert.Expect(LogType.Error, new Regex("failed and was replaced"));
            LogAssert.Expect(LogType.Exception, new Regex("letterbox test failure"));
            view.ApplyLayout(Ultrawide());
            Assert.That(throwing == null, "the failing presentation is destroyed");
            Assert.That(view.IsUsingFallback);
            Assert.That(layer.transform.childCount, Is.EqualTo(1));
            Assert.That(((LetterboxBarsPresentation)view.Presentation).Left.sizeDelta.x, Is.EqualTo(320f).Within(.01f));
        }

        private sealed class ThrowingPresentation : ILetterboxPresentation
        {
            public void ApplyLayout(ScreenFrameLayout layout) => throw new System.InvalidOperationException("letterbox test failure");
        }

        [Test]
        public void AllGraphics_AreNotRaycastTargets()
        {
            profile.Configure(Reference, true, barsPrefab, Color.black);
            view.SetProfile(profile);
            view.Initialize();
            var graphics = layer.GetComponentsInChildren<Graphic>(true);
            Assert.That(graphics, Is.Not.Empty);
            foreach (var graphic in graphics) Assert.That(graphic.raycastTarget, Is.False, graphic.name);

            view.SetPresentation(null);
            foreach (var graphic in layer.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }

        [Test]
        public void ZeroSizeBars_AreDisabled()
        {
            profile.Configure(Reference, true, null, Color.black);
            view.SetProfile(profile);
            view.Initialize();
            view.ApplyLayout(Ultrawide());
            var bars = (LetterboxBarsPresentation)view.Presentation;
            Assert.That(bars.TopImage.enabled, Is.False);
            Assert.That(bars.BottomImage.enabled, Is.False);
            Assert.That(bars.LeftImage.enabled);
            Assert.That(bars.RightImage.enabled);

            view.ApplyLayout(ScreenFrameMath.Calculate(new Vector2(1920f, 1080f), new Rect(0f, 0f, 1920f, 1080f), Reference));
            Assert.That(bars.LeftImage.enabled, Is.False);
            Assert.That(bars.RightImage.enabled, Is.False);
        }
    }
}
