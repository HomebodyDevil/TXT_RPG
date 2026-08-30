using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class FlexibleLayoutPanelTests
    {
        [Test]
        public void WeightedSizes_DistributeSpaceAfterSpacing()
        {
            var sizes = FlexibleLayoutPanel.CalculateSizes(
                1000f,
                20f,
                FlexibleLayoutOverflow.ShrinkBelowMinimum,
                new[]
                {
                    Weighted(7f),
                    Weighted(3f)
                });

            Assert.That(sizes[0], Is.EqualTo(686f).Within(0.001f));
            Assert.That(sizes[1], Is.EqualTo(294f).Within(0.001f));
        }

        [Test]
        public void FixedSize_IsReservedBeforeWeightedDistribution()
        {
            var sizes = FlexibleLayoutPanel.CalculateSizes(
                1000f,
                20f,
                FlexibleLayoutOverflow.ShrinkBelowMinimum,
                new[]
                {
                    new FlexibleLayoutSizeRequest(FlexibleLayoutSizeMode.Fixed, 0f, 200f, 0f, 0f),
                    Weighted(1f)
                });

            Assert.That(sizes[0], Is.EqualTo(200f).Within(0.001f));
            Assert.That(sizes[1], Is.EqualTo(780f).Within(0.001f));
        }

        [Test]
        public void ShrinkOverflow_ScalesMinimumSizesToAvailableSpace()
        {
            var sizes = FlexibleLayoutPanel.CalculateSizes(
                400f,
                20f,
                FlexibleLayoutOverflow.ShrinkBelowMinimum,
                new[]
                {
                    Weighted(1f, 300f),
                    Weighted(1f, 200f)
                });

            Assert.That(sizes[0], Is.EqualTo(228f).Within(0.001f));
            Assert.That(sizes[1], Is.EqualTo(152f).Within(0.001f));
        }

        [Test]
        public void ClipOverflow_PreservesMinimumSizes()
        {
            var sizes = FlexibleLayoutPanel.CalculateSizes(
                400f,
                20f,
                FlexibleLayoutOverflow.Clip,
                new[]
                {
                    Weighted(1f, 300f),
                    Weighted(1f, 200f)
                });

            Assert.That(sizes[0], Is.EqualTo(300f).Within(0.001f));
            Assert.That(sizes[1], Is.EqualTo(200f).Within(0.001f));
        }

        [Test]
        public void MaximumSize_RedistributesRemainingSpace()
        {
            var sizes = FlexibleLayoutPanel.CalculateSizes(
                500f,
                0f,
                FlexibleLayoutOverflow.ShrinkBelowMinimum,
                new[]
                {
                    new FlexibleLayoutSizeRequest(FlexibleLayoutSizeMode.Weighted, 1f, 0f, 0f, 100f),
                    Weighted(1f)
                });

            Assert.That(sizes[0], Is.EqualTo(100f).Within(0.001f));
            Assert.That(sizes[1], Is.EqualTo(400f).Within(0.001f));
        }

        [Test]
        public void GeneratedPrefabAndDemo_ContainRecursivePanelComposition()
        {
            FlexibleLayoutPrefabBuilder.CreateOrUpdatePrefab();
            FlexibleLayoutPrefabBuilder.CreateOrUpdateDemo();

            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab");
            var demoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel/FlexibleLayoutPanelDemo.prefab");
            var demoStyle = AssetDatabase.LoadAssetAtPath<FlexibleLayoutBackgroundStyle>(
                "Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel/FlexibleLayoutBackgroundDemoStyle.asset");

            var panel = panelPrefab.GetComponent<FlexibleLayoutPanel>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.transform.Find("BackgroundLayer/BackgroundVisualRoot/BackgroundA"), Is.Not.Null);
            Assert.That(panel.transform.Find("BackgroundLayer/BackgroundVisualRoot/BackgroundB"), Is.Not.Null);
            Assert.That(panel.transform.Find("BackgroundLayer/BackgroundVisualRoot/BackgroundEffectOverlay"), Is.Not.Null);
            Assert.That(panel.transform.Find("ContentLayer"), Is.EqualTo(panel.ContentRoot));
            Assert.That(panel.transform.Find("ForegroundLayer/Border"), Is.Not.Null);
            Assert.That(panel.GetComponentInChildren<FlexibleLayoutBackground>(true), Is.Not.Null);
            Assert.That(panel.ContentRoot.childCount, Is.Zero);
            Assert.That(panel.transform.Find("BackgroundLayer").GetComponent<LayoutElement>().ignoreLayout, Is.True);
            Assert.That(panel.transform.Find("ContentLayer").GetComponent<LayoutElement>().ignoreLayout, Is.True);
            Assert.That(panel.transform.Find("ForegroundLayer").GetComponent<LayoutElement>().ignoreLayout, Is.True);
            Assert.That(panel.transform.Find("BackgroundLayer").GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(panel.transform.Find("ContentLayer").GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(panel.transform.Find("ForegroundLayer").GetComponent<CanvasGroup>(), Is.Not.Null);
            foreach (var image in panel.GetComponentsInChildren<Image>(true))
            {
                Assert.That(image.raycastTarget, Is.False, image.name);
            }
            Assert.That(demoPrefab.GetComponentsInChildren<FlexibleLayoutPanel>(true).Length, Is.EqualTo(2));
            Assert.That(demoPrefab.transform.Find(
                "ContentLayer/MainContent/ContentLayer/StoryTextPanelArea"), Is.Not.Null);
            Assert.That(demoPrefab.transform.Find(
                "ContentLayer/MainContent/ContentLayer/ActionGridPanelArea"), Is.Not.Null);
            Assert.That(demoPrefab.transform.Find(
                "ContentLayer/CharacterDisplayPanelArea"), Is.Not.Null);
            Assert.That(demoStyle, Is.Not.Null);
            Assert.That(demoStyle.ScaleMode, Is.EqualTo(FlexibleLayoutBackgroundScaleMode.Stretch));
        }

        [Test]
        public void BackgroundStyle_ClampsOpacityAndStoresDisplayPolicy()
        {
            var style = ScriptableObject.CreateInstance<FlexibleLayoutBackgroundStyle>();
            try
            {
                style.Configure(
                    null,
                    Color.red,
                    FlexibleLayoutBackgroundScaleMode.Fill,
                    2f,
                    FlexibleLayoutBackgroundOverflowMode.Visible);

                Assert.That(style.Opacity, Is.EqualTo(1f));
                Assert.That(style.ScaleMode, Is.EqualTo(FlexibleLayoutBackgroundScaleMode.Fill));
                Assert.That(style.OverflowMode, Is.EqualTo(FlexibleLayoutBackgroundOverflowMode.Visible));
            }
            finally
            {
                Object.DestroyImmediate(style);
            }
        }

        private static FlexibleLayoutSizeRequest Weighted(float weight, float minimum = 0f)
        {
            return new FlexibleLayoutSizeRequest(
                FlexibleLayoutSizeMode.Weighted,
                weight,
                0f,
                minimum,
                0f);
        }
    }
}
