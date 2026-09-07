using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class HealthBarPanelTests
    {
        [Test]
        public void CalculateLayout_SupportsNineFixedAlignments()
        {
            var reference = new Rect(-100f, -50f, 200f, 100f);
            foreach (HealthBarHorizontalAlignment horizontal in System.Enum.GetValues(typeof(HealthBarHorizontalAlignment)))
            foreach (HealthBarVerticalAlignment vertical in System.Enum.GetValues(typeof(HealthBarVerticalAlignment)))
            {
                var result = HealthBarLayoutController.CalculateLayout(
                    reference, horizontal, vertical,
                    HealthBarAxisSizeMode.Fixed, HealthBarAxisSizeMode.Fixed,
                    new Vector2(40f, 20f), new RectOffset(), Vector2.zero);
                Assert.That(result.size, Is.EqualTo(new Vector2(40f, 20f)));
                var expectedX = horizontal switch
                {
                    HealthBarHorizontalAlignment.Left => -80f,
                    HealthBarHorizontalAlignment.Right => 80f,
                    _ => 0f
                };
                var expectedY = vertical switch
                {
                    HealthBarVerticalAlignment.Bottom => -40f,
                    HealthBarVerticalAlignment.Top => 40f,
                    _ => 0f
                };
                Assert.That(result.center, Is.EqualTo(new Vector2(expectedX, expectedY)));
                Assert.That(result.xMin, Is.GreaterThanOrEqualTo(reference.xMin));
                Assert.That(result.xMax, Is.LessThanOrEqualTo(reference.xMax));
                Assert.That(result.yMin, Is.GreaterThanOrEqualTo(reference.yMin));
                Assert.That(result.yMax, Is.LessThanOrEqualTo(reference.yMax));
            }
        }

        [Test]
        public void CalculateLayout_UsesPaddedCenterOffsetStretchAndSafeClamp()
        {
            var result = HealthBarLayoutController.CalculateLayout(
                new Rect(0f, 0f, 100f, 60f),
                HealthBarHorizontalAlignment.Center, HealthBarVerticalAlignment.Middle,
                HealthBarAxisSizeMode.Stretch, HealthBarAxisSizeMode.Fixed,
                new Vector2(500f, 100f), new RectOffset(10, 30, 5, 15),
                new Vector2(3f, -2f));
            Assert.That(result.width, Is.EqualTo(60f));
            Assert.That(result.height, Is.EqualTo(40f));
            Assert.That(result.center, Is.EqualTo(new Vector2(43f, 33f)));

            var collapsed = HealthBarLayoutController.CalculateLayout(
                new Rect(0f, 0f, 20f, 10f),
                HealthBarHorizontalAlignment.Left, HealthBarVerticalAlignment.Bottom,
                HealthBarAxisSizeMode.Fixed, HealthBarAxisSizeMode.Fixed,
                new Vector2(50f, 50f), new RectOffset(30, 30, 20, 20), Vector2.zero);
            Assert.That(collapsed.width, Is.Zero);
            Assert.That(collapsed.height, Is.Zero);
            Assert.That(collapsed.center.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(collapsed.center.y, Is.EqualTo(5f).Within(0.001f));
        }

        [TestCase(520f, 64f, 12, 12, 12, 12, 496f, 40f)]
        [TestCase(520f, 64f, 12, 12, 20, 20, 496f, 24f)]
        [TestCase(320f, 100f, 10, 30, 20, 10, 280f, 70f)]
        [TestCase(20f, 10f, 12, 12, 12, 12, 0f, 0f)]
        public void CalculateLayout_PaddingDrivenSizingMatchesContract(
            float width, float height,
            int left, int right, int top, int bottom,
            float expectedWidth, float expectedHeight)
        {
            var result = HealthBarLayoutController.CalculateLayout(
                new Rect(17f, -23f, width, height),
                HealthBarHorizontalAlignment.Right, HealthBarVerticalAlignment.Top,
                HealthBarAxisSizeMode.Stretch, HealthBarAxisSizeMode.Stretch,
                new Vector2(float.NaN, float.PositiveInfinity),
                new RectOffset(left, right, top, bottom),
                new Vector2(float.NaN, float.NegativeInfinity));
            Assert.That(result.size.x, Is.EqualTo(expectedWidth).Within(0.001f));
            Assert.That(result.size.y, Is.EqualTo(expectedHeight).Within(0.001f));
            Assert.That(float.IsNaN(result.x), Is.False);
            Assert.That(float.IsInfinity(result.y), Is.False);
        }

        [Test]
        public void CalculateLayout_OverPaddingIsScaledProportionallyWithoutChangingInput()
        {
            var padding = new RectOffset(30, 10, 12, 12);
            var result = HealthBarLayoutController.CalculateLayout(
                new Rect(0f, 0f, 20f, 10f),
                HealthBarHorizontalAlignment.Center, HealthBarVerticalAlignment.Middle,
                HealthBarAxisSizeMode.Stretch, HealthBarAxisSizeMode.Stretch,
                Vector2.zero, padding, Vector2.zero);
            Assert.That(result.size, Is.EqualTo(Vector2.zero));
            Assert.That(result.center.x, Is.EqualTo(15f).Within(0.001f));
            Assert.That(result.center.y, Is.EqualTo(5f).Within(0.001f));
            Assert.That(padding.left, Is.EqualTo(30));
            Assert.That(padding.right, Is.EqualTo(10));
        }

        [Test]
        public void GeneratedPrefab_IsCenteredAndUsesIndependentEffectRoots()
        {
            CharacterStatusPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CharacterStatusPrefabBuilder.HealthBarPrefabPath);
            var panel = prefab.GetComponent<HealthBarPanel>();
            var layout = prefab.GetComponent<HealthBarLayoutController>();
            Assert.That(layout.HorizontalAlignment, Is.EqualTo(HealthBarHorizontalAlignment.Center));
            Assert.That(layout.VerticalAlignment, Is.EqualTo(HealthBarVerticalAlignment.Middle));
            Assert.That(layout.HorizontalSizeMode, Is.EqualTo(HealthBarAxisSizeMode.Stretch));
            Assert.That(layout.VerticalSizeMode, Is.EqualTo(HealthBarAxisSizeMode.Stretch));
            Assert.That(layout.FixedSize.y, Is.EqualTo(24f));
            Assert.That(layout.Padding.left, Is.EqualTo(12));
            Assert.That(layout.Padding.right, Is.EqualTo(12));
            Assert.That(layout.Padding.top, Is.EqualTo(12));
            Assert.That(layout.Padding.bottom, Is.EqualTo(12));
            Assert.That(layout.BarRoot.rect.size, Is.EqualTo(new Vector2(496f, 40f)));
            Assert.That(layout.BarRoot.rect.center.y, Is.EqualTo(layout.ReferenceArea.rect.center.y).Within(0.01f));
            Assert.That(panel.BackgroundVisualRoot.name, Is.EqualTo("BackgroundVisualRoot"));
            Assert.That(panel.BarVisualRoot.name, Is.EqualTo("BarVisualRoot"));
            Assert.That(panel.FillVisualRoot.name, Is.EqualTo("FillVisualRoot"));
            Assert.That(panel.BorderVisualRoot.name, Is.EqualTo("BorderVisualRoot"));
            Assert.That(panel.TextVisualRoot.name, Is.EqualTo("TextVisualRoot"));
            Assert.That(panel.Slider.fillRect.name, Is.EqualTo("Fill"));
            Assert.That(panel.Slider.targetGraphic.name, Is.EqualTo("FillImage"));
            Assert.That(panel.Slider.direction, Is.EqualTo(Slider.Direction.LeftToRight));
            foreach (var graphic in prefab.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }

        [Test]
        public void RuntimeAlignmentChange_DoesNotOverwriteVisualEffectTransform()
        {
            var root = CreateLayout(out var layout, out var visual);
            try
            {
                visual.localScale = Vector3.one * 1.2f;
                layout.SetSizeModes(HealthBarAxisSizeMode.Fixed, HealthBarAxisSizeMode.Fixed);
                layout.SetFixedSize(40f, 20f);
                layout.SetAlignment(HealthBarHorizontalAlignment.Right, HealthBarVerticalAlignment.Top);
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one * 1.2f));
                Assert.That(layout.BarRoot.anchoredPosition.x, Is.GreaterThan(0f));
                Assert.That(layout.BarRoot.anchoredPosition.y, Is.GreaterThan(0f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Effects_ClearOnUnregisterDisableAndReapplyAsInitial()
        {
            var root = new GameObject("Health", typeof(RectTransform), typeof(HealthBarPanel));
            var effect = root.AddComponent<RecordingHealthBarEffect>();
            var panel = root.GetComponent<HealthBarPanel>();
            try
            {
                panel.RegisterEffect(effect);
                panel.Apply(Presentation(100));
                Assert.That(effect.ApplyCount, Is.EqualTo(1));
                Assert.That(effect.LastWasInitial, Is.True);
                panel.Apply(Presentation(75));
                Assert.That(effect.ApplyCount, Is.EqualTo(2));
                Assert.That(effect.LastWasInitial, Is.False);
                Assert.That(panel.UnregisterEffect(effect), Is.True);
                Assert.That(effect.ClearCount, Is.EqualTo(1));
                panel.RegisterEffect(effect);
                Assert.That(effect.ApplyCount, Is.EqualTo(3));
                Assert.That(effect.LastWasInitial, Is.True);
                panel.SetEffectsEnabled(false);
                Assert.That(effect.ClearCount, Is.EqualTo(2));
                panel.Apply(Presentation(50));
                Assert.That(effect.ApplyCount, Is.EqualTo(3));
                panel.SetEffectsEnabled(true);
                Assert.That(effect.ApplyCount, Is.EqualTo(4));
                Assert.That(effect.LastWasInitial, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Upgrade_IsIdempotentAndPreservesSliderHierarchy()
        {
            var root = new GameObject("LegacyHealth", typeof(RectTransform), typeof(HealthBarPanel));
            var background = new GameObject("BackgroundLayer", typeof(RectTransform));
            background.transform.SetParent(root.transform, false);
            var bar = new GameObject("BarRoot", typeof(RectTransform));
            bar.transform.SetParent(root.transform, false);
            var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(bar.transform, false);
            var panel = root.GetComponent<HealthBarPanel>();
            panel.Configure(sliderObject.GetComponent<Slider>());
            try
            {
                var originalParent = sliderObject.transform.parent;
                Assert.That(HealthBarPanelUpgradeUtility.UpgradePanel(panel, false), Is.True);
                Assert.That(HealthBarPanelUpgradeUtility.UpgradePanel(panel, false), Is.False);
                Assert.That(sliderObject.transform.parent, Is.EqualTo(originalParent));
                Assert.That(root.GetComponent<HealthBarLayoutController>(), Is.Not.Null);
                Assert.That(panel.BarVisualRoot, Is.EqualTo(sliderObject.transform));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ConvertToPaddingSizing_PreservesExistingSettingsAndIsIdempotent()
        {
            var root = CreateLayout(out var layout, out _);
            var panel = root.AddComponent<HealthBarPanel>();
            try
            {
                layout.SetSizeModes(HealthBarAxisSizeMode.Fixed, HealthBarAxisSizeMode.Fixed);
                layout.SetFixedSize(137f, 19f);
                layout.SetPadding(3, 7, 11, 13);
                layout.SetOffset(new Vector2(5f, -9f));
                Assert.That(HealthBarPanelUpgradeUtility.ConvertPanelToPaddingSizing(panel, false), Is.True);
                Assert.That(HealthBarPanelUpgradeUtility.ConvertPanelToPaddingSizing(panel, false), Is.False);
                Assert.That(layout.HorizontalSizeMode, Is.EqualTo(HealthBarAxisSizeMode.Stretch));
                Assert.That(layout.VerticalSizeMode, Is.EqualTo(HealthBarAxisSizeMode.Stretch));
                Assert.That(layout.FixedSize, Is.EqualTo(new Vector2(137f, 19f)));
                Assert.That(layout.Padding.left, Is.EqualTo(3));
                Assert.That(layout.Padding.right, Is.EqualTo(7));
                Assert.That(layout.Padding.top, Is.EqualTo(11));
                Assert.That(layout.Padding.bottom, Is.EqualTo(13));
                Assert.That(layout.Offset, Is.EqualTo(new Vector2(5f, -9f)));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void GeneratedDemo_ContainsNineAlignmentsAndRuntimePulseExample()
        {
            HealthBarPanelDemoBuilder.CreateOrUpdateDemo();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPanelDemoBuilder.DemoPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<HealthBarPanel>(true).Length, Is.EqualTo(10));
            Assert.That(prefab.GetComponentInChildren<HealthBarPanelDemoController>(true), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<HealthBarScalePulseEffect>(true), Is.Not.Null);
            var runtime = prefab.transform.Find("RuntimeAlignmentAndPulse")
                .GetComponent<HealthBarLayoutController>();
            Assert.That(runtime.HorizontalSizeMode, Is.EqualTo(HealthBarAxisSizeMode.Stretch));
            Assert.That(runtime.VerticalSizeMode, Is.EqualTo(HealthBarAxisSizeMode.Stretch));
        }

        private static GameObject CreateLayout(out HealthBarLayoutController layout, out RectTransform visual)
        {
            var root = new GameObject("Health", typeof(RectTransform), typeof(HealthBarLayoutController));
            ((RectTransform)root.transform).sizeDelta = new Vector2(200f, 100f);
            var background = new GameObject("Background", typeof(RectTransform));
            background.transform.SetParent(root.transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            var bar = new GameObject("BarRoot", typeof(RectTransform));
            bar.transform.SetParent(root.transform, false);
            var visualObject = new GameObject("BarVisualRoot", typeof(RectTransform));
            visualObject.transform.SetParent(bar.transform, false);
            visual = (RectTransform)visualObject.transform;
            layout = root.GetComponent<HealthBarLayoutController>();
            layout.Configure(backgroundRect, (RectTransform)bar.transform,
                HealthBarHorizontalAlignment.Center, HealthBarVerticalAlignment.Middle,
                HealthBarAxisSizeMode.Stretch, HealthBarAxisSizeMode.Fixed,
                new Vector2(100f, 24f), new RectOffset(12, 12, 0, 0), Vector2.zero);
            return root;
        }

        private static CharacterStatusPresentation Presentation(int current) =>
            new(new CharacterNamePresentation(string.Empty),
                new HealthPresentation(current, 100, "HP", $"{current} / 100"));
    }

    public sealed class RecordingHealthBarEffect : HealthBarEffect
    {
        public int ApplyCount { get; private set; }
        public int ClearCount { get; private set; }
        public bool LastWasInitial { get; private set; }
        public override void Apply(in HealthPresentation previous, in HealthPresentation current, bool isInitialValue)
        {
            ApplyCount++;
            LastWasInitial = isInitialValue;
        }
        public override void Clear() => ClearCount++;
    }
}
