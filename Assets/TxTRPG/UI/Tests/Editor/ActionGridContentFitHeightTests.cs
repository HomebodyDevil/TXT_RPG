using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.UI.Tests
{
    // Uses temporary scene objects only and destroys them; no assets are written.
    public sealed class ActionGridContentFitHeightTests
    {
        private const string PanelPrefab = "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab";

        [Test]
        public void Height_UsesCellGapAndBodyMargins()
        {
            Assert.That(ActionGridContentFitHeight.CalculateHeight(64, 12, 18, 18, 0, 0), Is.EqualTo(124f));
        }

        [Test]
        public void Height_AddsHeaderAndScrollbarReservations()
        {
            Assert.That(ActionGridContentFitHeight.CalculateHeight(64, 12, 18, 18, 58 + 8, 0), Is.EqualTo(190f));
            Assert.That(ActionGridContentFitHeight.CalculateHeight(64, 12, 18, 18, 0, 12 + 4), Is.EqualTo(140f));
        }

        [TestCase(0f, 0f)]
        [TestCase(-5f, 0f)]
        [TestCase(float.NaN, 12f)]
        [TestCase(float.PositiveInfinity, 12f)]
        [TestCase(float.NegativeInfinity, 12f)]
        [TestCase(10000f, 4096f)]
        [TestCase(7.5f, 7.5f)]
        public void Gap_IsNormalized(float value, float expected)
        {
            Assert.That(ActionGridContentFitHeight.NormalizeGap(value), Is.EqualTo(expected));
            Assert.That(ActionGridContentFitHeight.CalculateHeight(64, value, 0, 0, 0, 0), Is.EqualTo(64f + 2f * expected));
        }

        [Test]
        public void Group_WithoutSource_KeepsItemResult()
        {
            var root = CreateGroup(out _, out _, out _, out var third);
            try
            {
                Rebuild(root);
                Assert.That(third.rect.height, Is.EqualTo(180f).Within(0.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Panel_SuppliesHeightAndPlacesCellsGapFromViewport()
        {
            // The parent queries the component as an IFlexibleLayoutSizeSource; disabling it or returning false
            // (Grid flow) falls back to the FlexibleLayoutItem values.
            var root = CreateGroup(out _, out var health, out var character, out var placeholder);
            Object.DestroyImmediate(placeholder.gameObject);
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefab));
            try
            {
                go.transform.SetParent(root.transform, false);
                var item = go.GetComponent<FlexibleLayoutItem>() ?? go.AddComponent<FlexibleLayoutItem>();
                item.Configure(FlexibleLayoutSizeMode.Fixed, 1, 100, 180);
                var panel = go.GetComponent<ActionGridPanel>();
                var surface = ActionGridSurfaceAuthoring.Ensure(panel);
                var settings = panel.GetDisplaySettings();
                settings.mode = ActionGridDisplayMode.Balanced; settings.flow = ActionGridFlow.SingleRow;
                panel.ConfigureDisplay(settings);
                surface.ConfigureHeader(false, 58, 8, new RectOffset(18, 18, 18, 18));
                Rebuild(root);
                var grid = panel.ScrollRect.content.GetComponent<GridLayoutGroup>();
                var characterBefore = character.rect.height;
                var leftBefore = grid.padding.left; var rightBefore = grid.padding.right;
                var spacingBefore = grid.spacing; var alignmentBefore = grid.childAlignment;
                Assert.That(((RectTransform)go.transform).rect.height, Is.EqualTo(180f).Within(0.01f));

                var fit = go.AddComponent<ActionGridContentFitHeight>();
                fit.Initialize(panel, 12);
                Rebuild(root);
                Assert.That(fit.State, Is.EqualTo(ActionGridContentFitState.Supplying));
                Assert.That(((RectTransform)go.transform).rect.height, Is.EqualTo(124f).Within(0.5f));
                Assert.That(panel.ScrollRect.viewport.rect.height, Is.EqualTo(88f).Within(0.5f));
                Assert.That(character.rect.height, Is.EqualTo(characterBefore + 56f).Within(0.5f));
                Assert.That(health.rect.height, Is.EqualTo(50f).Within(0.01f));
                Assert.That(grid.padding.top, Is.EqualTo(12)); Assert.That(grid.padding.bottom, Is.EqualTo(12));
                Assert.That(grid.padding.left, Is.EqualTo(leftBefore)); Assert.That(grid.padding.right, Is.EqualTo(rightBefore));
                Assert.That(grid.spacing, Is.EqualTo(spacingBefore)); Assert.That(grid.childAlignment, Is.EqualTo(alignmentBefore));
                Assert.That(panel.ScrollRect.viewport.rect.height - grid.padding.vertical, Is.EqualTo(grid.cellSize.y).Within(0.5f));
                Assert.That(panel.GetDisplaySettings().balanced.padding.top, Is.EqualTo(14));

                surface.SetHeaderVisible(true);
                Rebuild(root);
                Assert.That(((RectTransform)go.transform).rect.height, Is.EqualTo(124f + 66f).Within(0.5f));
                Assert.That(panel.ScrollRect.viewport.rect.height, Is.EqualTo(88f).Within(0.5f));
                surface.SetHeaderVisible(false);

                fit.SetGap(24);
                Rebuild(root);
                Assert.That(((RectTransform)go.transform).rect.height, Is.EqualTo(148f).Within(0.5f));
                Assert.That(character.rect.height, Is.EqualTo(characterBefore + 32f).Within(0.5f));

                settings = panel.GetDisplaySettings(); settings.flow = ActionGridFlow.Grid;
                panel.ConfigureDisplay(settings);
                Rebuild(root);
                Assert.That(fit.State, Is.EqualTo(ActionGridContentFitState.GridFlow));
                Assert.That(((RectTransform)go.transform).rect.height, Is.EqualTo(180f).Within(0.5f));
                Assert.That(grid.padding.top, Is.EqualTo(14)); Assert.That(grid.padding.bottom, Is.EqualTo(14));

                settings.flow = ActionGridFlow.SingleRow; panel.ConfigureDisplay(settings);
                fit.enabled = false;
                Rebuild(root);
                Assert.That(((RectTransform)go.transform).rect.height, Is.EqualTo(180f).Within(0.5f));
                Assert.That(grid.padding.top, Is.EqualTo(14));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject CreateGroup(out FlexibleContentLayoutGroup group, out RectTransform health,
            out RectTransform character, out RectTransform actions)
        {
            var root = new GameObject("ContentLayer", typeof(RectTransform), typeof(FlexibleContentLayoutGroup));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(600, 800);
            group = root.GetComponent<FlexibleContentLayoutGroup>();
            group.Configure(FlexibleLayoutAxis.Vertical, FlexibleLayoutAxisPolicy.Fixed, 720, 12, new RectOffset(),
                FlexibleLayoutOverflow.ShrinkBelowMinimum, false, TextAnchor.UpperLeft);
            health = Child(root, "HealthBarPanel", FlexibleLayoutSizeMode.Fixed, 0.15f, 50, 50);
            character = Child(root, "CharacterDisplayPanel", FlexibleLayoutSizeMode.Weighted, 2, 100, 0);
            actions = Child(root, "ActionGridPanel", FlexibleLayoutSizeMode.Fixed, 1, 100, 180);
            return root;
        }

        private static RectTransform Child(GameObject root, string name, FlexibleLayoutSizeMode mode, float weight, float fixedSize, float minimum)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(FlexibleLayoutItem));
            child.transform.SetParent(root.transform, false);
            child.GetComponent<FlexibleLayoutItem>().Configure(mode, weight, fixedSize, minimum);
            return (RectTransform)child.transform;
        }

        private static void Rebuild(GameObject root)
        {
            for (var i = 0; i < 3; i++) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
        }
    }
}
