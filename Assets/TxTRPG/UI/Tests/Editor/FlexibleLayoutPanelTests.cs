using NUnit.Framework;
using System.Linq;
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
            Assert.That(panel.transform.GetChild(0).name, Is.EqualTo("BackgroundLayer"));
            Assert.That(panel.transform.GetChild(1).name, Is.EqualTo("BackgroundContentLayer"));
            Assert.That(panel.transform.GetChild(2).name, Is.EqualTo("ContentLayer"));
            Assert.That(panel.transform.GetChild(3).name, Is.EqualTo("ForegroundLayer"));
            Assert.That(panel.BackgroundContentRoot, Is.EqualTo(panel.transform.Find("BackgroundContentLayer")));
            Assert.That(panel.BackgroundContentLayout, Is.Not.Null);
            Assert.That(panel.BackgroundContentRoot.GetComponent<RectMask2D>().enabled, Is.False);
            Assert.That(panel.BackgroundContentCanvasGroup.interactable, Is.False);
            Assert.That(panel.BackgroundContentCanvasGroup.blocksRaycasts, Is.False);
            Assert.That(panel.transform.Find("ContentLayer"), Is.EqualTo(panel.ContentRoot));
            Assert.That(panel.ContentLayout, Is.Not.Null);
            Assert.That(panel.ContentLayout.transform, Is.EqualTo(panel.ContentRoot));
            Assert.That(panel.ContentRoot.GetComponent<RectMask2D>().enabled, Is.False);
            Assert.That(panel.transform.Find("ForegroundLayer/Border"), Is.Not.Null);
            Assert.That(panel.GetComponentInChildren<FlexibleLayoutBackground>(true), Is.Not.Null);
            Assert.That(panel.ContentRoot.childCount, Is.Zero);
            Assert.That(panel.BackgroundContentRoot.childCount, Is.Zero);
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
            Assert.That(demoPrefab.transform.Find(
                "BackgroundContentLayer/ReservedBackdropSpace"), Is.Not.Null);
            Assert.That(demoPrefab.transform.Find(
                "BackgroundContentLayer/BackdropRegion/VisualRoot/Tint"), Is.Not.Null);
            Assert.That(demoStyle, Is.Not.Null);
            Assert.That(demoStyle.ScaleMode, Is.EqualTo(FlexibleLayoutBackgroundScaleMode.Stretch));
        }

        [Test]
        public void GeneratedPlaceholder_IsEmptyActiveLayoutItem()
        {
            FlexibleLayoutPrefabBuilder.CreateOrUpdatePrefab();
            var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPlaceholder.prefab");

            Assert.That(placeholder, Is.Not.Null);
            Assert.That(placeholder.activeSelf, Is.True);
            Assert.That(placeholder.transform, Is.TypeOf<RectTransform>());
            Assert.That(placeholder.GetComponent<FlexibleLayoutItem>(), Is.Not.Null);
            Assert.That(placeholder.GetComponent<Image>(), Is.Null);
            Assert.That(placeholder.transform.childCount, Is.Zero);
            Assert.That(placeholder.GetComponents<Component>().Length, Is.EqualTo(2));

            var sizes = FlexibleLayoutPanel.CalculateSizes(
                500f,
                20f,
                FlexibleLayoutOverflow.ShrinkBelowMinimum,
                new[]
                {
                    new FlexibleLayoutSizeRequest(FlexibleLayoutSizeMode.Weighted, 1f, 0f, 100f, 180f),
                    new FlexibleLayoutSizeRequest(FlexibleLayoutSizeMode.Weighted, 1f, 0f, 100f, 0f)
                });
            Assert.That(sizes[0], Is.EqualTo(180f).Within(0.01f));
            Assert.That(sizes[1], Is.EqualTo(300f).Within(0.01f));
        }

        [Test]
        public void BackgroundAndContent_DistributeTheirDirectChildrenIndependently()
        {
            FlexibleLayoutPrefabBuilder.CreateOrUpdatePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<FlexibleLayoutPanel>();
                AddWeighted(panel, FlexibleLayoutContentLayer.Content, "FrontA", 1f);
                AddWeighted(panel, FlexibleLayoutContentLayer.Content, "FrontB", 3f);
                ForceLayout(panel);
                var frontWidth = ((RectTransform)panel.ContentRoot.GetChild(0)).rect.width;

                AddWeighted(panel, FlexibleLayoutContentLayer.BackgroundContent, "BackA", 1f);
                AddWeighted(panel, FlexibleLayoutContentLayer.BackgroundContent, "BackB", 1f);
                ForceLayout(panel);

                Assert.That(((RectTransform)panel.ContentRoot.GetChild(0)).rect.width,
                    Is.EqualTo(frontWidth).Within(0.01f));
                Assert.That(((RectTransform)panel.BackgroundContentRoot.GetChild(0)).rect.width,
                    Is.EqualTo(((RectTransform)panel.BackgroundContentRoot.GetChild(1)).rect.width).Within(0.01f));
                Assert.That(((RectTransform)panel.ContentRoot.GetChild(0)).rect.width,
                    Is.Not.EqualTo(((RectTransform)panel.ContentRoot.GetChild(1)).rect.width).Within(0.01f));

                panel.Remove(panel.BackgroundContentRoot.GetChild(1), FlexibleLayoutContentLayer.BackgroundContent);
                ForceLayout(panel);
                Assert.That(((RectTransform)panel.ContentRoot.GetChild(0)).rect.width,
                    Is.EqualTo(frontWidth).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BackgroundIndependentMode_UsesOwnWidthClipAlphaAndInteraction()
        {
            FlexibleLayoutPrefabBuilder.CreateOrUpdatePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<FlexibleLayoutPanel>();
                panel.SetBackgroundUsesContentLayoutSettings(false);
                panel.BackgroundContentRoot.anchorMin = new Vector2(0.5f, 0f);
                panel.BackgroundContentRoot.anchorMax = new Vector2(0.5f, 1f);
                panel.BackgroundContentRoot.sizeDelta = new Vector2(400f, 0f);
                panel.ConfigureBackgroundLayout(
                    FlexibleLayoutAxis.Horizontal,
                    FlexibleLayoutAxisPolicy.VerticalWhenNarrow,
                    500f,
                    0f,
                    new RectOffset(),
                    FlexibleLayoutOverflow.ShrinkBelowMinimum,
                    false,
                    TextAnchor.MiddleCenter);
                panel.SetBackgroundClipContent(true);
                panel.SetBackgroundAlpha(2f);
                panel.SetBackgroundInteraction(true, true);

                Assert.That(panel.BackgroundContentLayout.CurrentAxis, Is.EqualTo(FlexibleLayoutAxis.Vertical));
                Assert.That(panel.BackgroundContentRoot.GetComponent<RectMask2D>().enabled, Is.True);
                Assert.That(panel.BackgroundContentCanvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(panel.BackgroundContentCanvasGroup.interactable, Is.True);
                Assert.That(panel.BackgroundContentCanvasGroup.blocksRaycasts, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void InactivePlaceholder_FollowsIncludeInactivePolicy()
        {
            var root = new GameObject("Layer", typeof(RectTransform), typeof(FlexibleContentLayoutGroup));
            var placeholder = new GameObject("Placeholder", typeof(RectTransform), typeof(FlexibleLayoutItem));
            placeholder.transform.SetParent(root.transform, false);
            placeholder.GetComponent<FlexibleLayoutItem>().Configure(
                FlexibleLayoutSizeMode.Weighted,
                1f,
                newMinimumSize: 120f);
            placeholder.SetActive(false);
            try
            {
                var layout = root.GetComponent<FlexibleContentLayoutGroup>();
                layout.Configure(FlexibleLayoutAxis.Horizontal, FlexibleLayoutAxisPolicy.Fixed,
                    720f, 0f, new RectOffset(), FlexibleLayoutOverflow.ShrinkBelowMinimum,
                    false, TextAnchor.MiddleCenter);
                layout.CalculateLayoutInputHorizontal();
                Assert.That(layout.minWidth, Is.EqualTo(0f));

                layout.Configure(FlexibleLayoutAxis.Horizontal, FlexibleLayoutAxisPolicy.Fixed,
                    720f, 0f, new RectOffset(), FlexibleLayoutOverflow.ShrinkBelowMinimum,
                    true, TextAnchor.MiddleCenter);
                layout.CalculateLayoutInputHorizontal();
                Assert.That(layout.minWidth, Is.EqualTo(120f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BackgroundSharedMode_FollowsContentAndRestoresIndependentRectAndSettings()
        {
            FlexibleLayoutPrefabBuilder.CreateOrUpdatePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<FlexibleLayoutPanel>();
                panel.SetBackgroundUsesContentLayoutSettings(false);
                panel.BackgroundContentRoot.offsetMin = new Vector2(31f, 32f);
                panel.BackgroundContentRoot.offsetMax = new Vector2(-33f, -34f);
                panel.ConfigureBackgroundLayout(
                    FlexibleLayoutAxis.Vertical,
                    FlexibleLayoutAxisPolicy.VerticalWhenNarrow,
                    500f,
                    9f,
                    new RectOffset(1, 2, 3, 4),
                    FlexibleLayoutOverflow.Clip,
                    true,
                    TextAnchor.LowerRight);

                panel.SetBackgroundUsesContentLayoutSettings(true);
                panel.SetContentMargins(11, 12, 13, 14);
                Assert.That(panel.BackgroundContentRoot.offsetMin, Is.EqualTo(panel.ContentRoot.offsetMin));
                Assert.That(panel.BackgroundContentRoot.offsetMax, Is.EqualTo(panel.ContentRoot.offsetMax));
                Assert.That(panel.BackgroundContentLayout.Spacing, Is.EqualTo(panel.ContentLayout.Spacing));

                panel.SetBackgroundUsesContentLayoutSettings(false);
                Assert.That(panel.BackgroundContentRoot.offsetMin, Is.EqualTo(new Vector2(31f, 32f)));
                Assert.That(panel.BackgroundContentRoot.offsetMax, Is.EqualTo(new Vector2(-33f, -34f)));
                Assert.That(panel.BackgroundContentLayout.FixedAxis, Is.EqualTo(FlexibleLayoutAxis.Vertical));
                Assert.That(panel.BackgroundContentLayout.AxisPolicy,
                    Is.EqualTo(FlexibleLayoutAxisPolicy.VerticalWhenNarrow));
                Assert.That(panel.BackgroundContentLayout.Spacing, Is.EqualTo(9f));
                Assert.That(panel.BackgroundContentLayout.Overflow, Is.EqualTo(FlexibleLayoutOverflow.Clip));
                Assert.That(panel.BackgroundContentLayout.IncludeInactiveChildren, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BackgroundMarginOverride_RestoresAuthoredIndependentOffsets()
        {
            FlexibleLayoutPrefabBuilder.CreateOrUpdatePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<FlexibleLayoutPanel>();
                panel.SetBackgroundUsesContentLayoutSettings(false);
                panel.BackgroundContentRoot.offsetMin = new Vector2(21f, 22f);
                panel.BackgroundContentRoot.offsetMax = new Vector2(-23f, -24f);

                panel.SetBackgroundContentMargins(1, 2, 3, 4);
                Assert.That(panel.BackgroundContentRoot.offsetMin, Is.EqualTo(new Vector2(1f, 4f)));
                Assert.That(panel.BackgroundContentRoot.offsetMax, Is.EqualTo(new Vector2(-2f, -3f)));

                panel.UseAuthoredBackgroundContentOffsets();
                Assert.That(panel.BackgroundContentRoot.offsetMin, Is.EqualTo(new Vector2(21f, 22f)));
                Assert.That(panel.BackgroundContentRoot.offsetMax, Is.EqualTo(new Vector2(-23f, -24f)));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LegacyPanel_ContentApiWorksAndBackgroundRequestFailsClearly()
        {
            var root = new GameObject("Legacy", typeof(RectTransform));
            var content = new GameObject("ContentLayer", typeof(RectTransform), typeof(FlexibleContentLayoutGroup));
            content.transform.SetParent(root.transform, false);
            var panel = root.AddComponent<FlexibleLayoutPanel>();
            try
            {
                var serialized = new SerializedObject(panel);
                serialized.FindProperty("contentRoot").objectReferenceValue = content.transform;
                serialized.FindProperty("contentLayout").objectReferenceValue = content.GetComponent<FlexibleContentLayoutGroup>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var child = new GameObject("Front", typeof(RectTransform));
                panel.Add(child.transform, 2f);
                Assert.That(child.transform.parent, Is.EqualTo(content.transform));
                Assert.Throws<System.InvalidOperationException>(() =>
                    panel.GetLayerRoot(FlexibleLayoutContentLayer.BackgroundContent));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UpgradePanel_IsIdempotentAndPreservesContentAndOffsets()
        {
            var root = new GameObject("Legacy", typeof(RectTransform));
            var content = new GameObject("ContentLayer", typeof(RectTransform), typeof(FlexibleContentLayoutGroup));
            content.transform.SetParent(root.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.offsetMin = new Vector2(12f, 13f);
            contentRect.offsetMax = new Vector2(-14f, -15f);
            var child = new GameObject("UserContent", typeof(RectTransform));
            child.transform.SetParent(content.transform, false);
            var panel = root.AddComponent<FlexibleLayoutPanel>();
            try
            {
                var serialized = new SerializedObject(panel);
                serialized.FindProperty("contentRoot").objectReferenceValue = contentRect;
                serialized.FindProperty("contentLayout").objectReferenceValue = content.GetComponent<FlexibleContentLayoutGroup>();
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(FlexibleLayoutUpgradeUtility.UpgradePanel(panel, false), Is.True);
                Assert.That(FlexibleLayoutUpgradeUtility.UpgradePanel(panel, false), Is.False);
                Assert.That(panel.BackgroundContentRoot.offsetMin, Is.EqualTo(contentRect.offsetMin));
                Assert.That(panel.BackgroundContentRoot.offsetMax, Is.EqualTo(contentRect.offsetMax));
                Assert.That(content.transform.Find("UserContent"), Is.Not.Null);
                Assert.That(root.transform.Cast<Transform>().Count(t => t.name == "BackgroundContentLayer"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
                Assert.That(panel.BackgroundContentRoot.GetComponent<RectMask2D>().enabled, Is.True);
                Assert.That(panel.ContentLayout.Overflow, Is.EqualTo(originalOverflow));
                panel.SetClipContent(false);
                Assert.That(panel.ContentRoot.GetComponent<RectMask2D>().enabled, Is.False);
                Assert.That(panel.BackgroundContentRoot.GetComponent<RectMask2D>().enabled, Is.False);
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

        private static void AddWeighted(
            FlexibleLayoutPanel panel,
            FlexibleLayoutContentLayer layer,
            string name,
            float weight)
        {
            var child = new GameObject(name, typeof(RectTransform));
            panel.Add(child.transform, layer, weight);
        }

        private static void ForceLayout(FlexibleLayoutPanel panel)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.ContentRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.BackgroundContentRoot);
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
