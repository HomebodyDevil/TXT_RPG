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
            Assert.That(panel.ContentLayout, Is.Not.Null);
            Assert.That(panel.ContentLayout.transform, Is.EqualTo(panel.ContentRoot));
            Assert.That(panel.ContentRoot.GetComponent<RectMask2D>().enabled, Is.False);
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

        [Test]
        public void GeneratedSampleMainDemo_MirrorsSampleSceneColumnComposition()
        {
            FlexibleLayoutPrefabBuilder.CreateOrUpdateSampleMainDemo();
            var demoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel/Sample_Main_FlexibleLayoutPanelDemo.prefab");
            Assert.That(demoPrefab, Is.Not.Null);
            Assert.That(((RectTransform)demoPrefab.transform).sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));
            var content = demoPrefab.GetComponent<FlexibleLayoutPanel>().ContentRoot;
            Assert.That(content.childCount, Is.EqualTo(3));
            AssertSampleColumn(content, 0, "TMP_FlexibleLayoutPanel", "ActionGridPanelDemo", 1f);
            AssertSampleColumn(content, 1, "Text_FlexibleLayoutPanel", "StoryTextPanelDemo", 3f);
            AssertSampleColumn(content, 2, "Character_FlexibleLayoutPanel", "CharacterDisplayPanelDemo", 1f);
        }

        [Test]
        public void ContentLayout_UsesContentWidthForResponsiveAxis()
        {
            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(FlexibleContentLayoutGroup));
            try
            {
                var rect = (RectTransform)contentObject.transform;
                rect.sizeDelta = new Vector2(600f, 400f);
                var layout = contentObject.GetComponent<FlexibleContentLayoutGroup>();
                layout.Configure(FlexibleLayoutAxis.Horizontal, FlexibleLayoutAxisPolicy.VerticalWhenNarrow,
                    720f, 0f, new RectOffset(), FlexibleLayoutOverflow.ShrinkBelowMinimum,
                    false, TextAnchor.MiddleCenter);
                Assert.That(layout.CurrentAxis, Is.EqualTo(FlexibleLayoutAxis.Vertical));
            }
            finally
            {
                Object.DestroyImmediate(contentObject);
            }
        }

        [Test]
        public void ContentLayout_AppliesAsymmetricPaddingInsideContentRect()
        {
            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(FlexibleContentLayoutGroup));
            try
            {
                var rect = (RectTransform)contentObject.transform;
                rect.sizeDelta = new Vector2(600f, 400f);
                var layout = contentObject.GetComponent<FlexibleContentLayoutGroup>();
                layout.Configure(FlexibleLayoutAxis.Horizontal, FlexibleLayoutAxisPolicy.Fixed,
                    720f, 10f, new RectOffset(20, 40, 30, 50),
                    FlexibleLayoutOverflow.ShrinkBelowMinimum, false, TextAnchor.MiddleCenter);
                var first = new GameObject("First", typeof(RectTransform), typeof(FlexibleLayoutItem));
                var second = new GameObject("Second", typeof(RectTransform), typeof(FlexibleLayoutItem));
                first.transform.SetParent(rect, false);
                second.transform.SetParent(rect, false);

                layout.CalculateLayoutInputHorizontal();
                layout.CalculateLayoutInputVertical();
                layout.SetLayoutHorizontal();
                layout.SetLayoutVertical();

                var firstRect = (RectTransform)first.transform;
                var secondRect = (RectTransform)second.transform;
                Assert.That(firstRect.rect.width, Is.EqualTo(265f).Within(0.01f));
                Assert.That(secondRect.rect.width, Is.EqualTo(265f).Within(0.01f));
                Assert.That(firstRect.anchoredPosition.x, Is.EqualTo(152.5f).Within(0.01f));
                Assert.That(secondRect.anchoredPosition.x, Is.EqualTo(427.5f).Within(0.01f));
                Assert.That(firstRect.rect.height, Is.EqualTo(320f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(contentObject);
            }
        }

        [Test]
        public void ClipContent_TogglesContentMaskWithoutChangingSizingPolicy()
        {
            FlexibleLayoutPrefabBuilder.CreateOrUpdatePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<FlexibleLayoutPanel>();
                var originalOverflow = panel.ContentLayout.Overflow;
                panel.SetClipContent(true);
                Assert.That(panel.ContentRoot.GetComponent<RectMask2D>().enabled, Is.True);
                Assert.That(panel.ContentLayout.Overflow, Is.EqualTo(originalOverflow));
                panel.SetClipContent(false);
                Assert.That(panel.ContentRoot.GetComponent<RectMask2D>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PanelInsets_ExposeIndependentPaddingAndMargins()
        {
            FlexibleLayoutPrefabBuilder.CreateOrUpdatePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<FlexibleLayoutPanel>();
                panel.SetPadding(11, 12, 13, 14);
                panel.SetContentMargins(21, 22, 23, 24);

                Assert.That(panel.ContentPadding.left, Is.EqualTo(11));
                Assert.That(panel.ContentPadding.right, Is.EqualTo(12));
                Assert.That(panel.ContentPadding.top, Is.EqualTo(13));
                Assert.That(panel.ContentPadding.bottom, Is.EqualTo(14));
                Assert.That(panel.ContentMargins.left, Is.EqualTo(21));
                Assert.That(panel.ContentMargins.right, Is.EqualTo(22));
                Assert.That(panel.ContentMargins.top, Is.EqualTo(23));
                Assert.That(panel.ContentMargins.bottom, Is.EqualTo(24));
                Assert.That(panel.ContentRoot.offsetMin, Is.EqualTo(new Vector2(21f, 24f)));
                Assert.That(panel.ContentRoot.offsetMax, Is.EqualTo(new Vector2(-22f, -23f)));
            }
            finally
            {
                Object.DestroyImmediate(instance);
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

        private static void AssertSampleColumn(Transform content, int siblingIndex,
            string columnName, string demoName, float weight)
        {
            var column = content.GetChild(siblingIndex);
            Assert.That(column.name, Is.EqualTo(columnName));
            Assert.That(column.GetComponent<FlexibleLayoutItem>().Weight, Is.EqualTo(weight));
            var layout = column.GetComponent<FlexibleLayoutPanel>();
            Assert.That(layout, Is.Not.Null);
            Assert.That(layout.ContentRoot.Find(demoName), Is.Not.Null);
        }
    }
}
