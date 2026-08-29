using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;

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

            Assert.That(panelPrefab.GetComponent<FlexibleLayoutPanel>(), Is.Not.Null);
            Assert.That(panelPrefab.transform.childCount, Is.Zero);
            Assert.That(demoPrefab.GetComponentsInChildren<FlexibleLayoutPanel>(true).Length, Is.EqualTo(2));
            Assert.That(demoPrefab.transform.Find("MainContent/StoryTextPanelArea"), Is.Not.Null);
            Assert.That(demoPrefab.transform.Find("MainContent/ActionGridPanelArea"), Is.Not.Null);
            Assert.That(demoPrefab.transform.Find("CharacterDisplayPanelArea"), Is.Not.Null);
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
