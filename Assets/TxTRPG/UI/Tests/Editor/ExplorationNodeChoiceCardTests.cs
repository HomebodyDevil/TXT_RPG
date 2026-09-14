using System.Collections.Generic;
using NUnit.Framework;
using TxTRPG.UI.Editor;
using TxTRPG.UI.Exploration;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class ExplorationNodeChoiceCardTests
    {
        [Test]
        public void SavedPrefab_HasRequiredEffectAndInputRoots()
        {
            ExplorationNodeChoiceCardPrefabBuilder.ValidatePrefab();
        }

        [TestCase(760f, 3)]
        [TestCase(500f, 2)]
        [TestCase(260f, 1)]
        public void Layout_WrapsByActualWidthAndCentersEachRow(float width, int expectedColumns)
        {
            var root = new GameObject("Layout", typeof(RectTransform), typeof(ExplorationNodeChoiceLayoutGroup));
            try
            {
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(width, 800f);
                var layout = root.GetComponent<ExplorationNodeChoiceLayoutGroup>();
                layout.Configure(210f, 300f, 18f, 18f, new RectOffset(12, 12, 12, 12));
                var children = new List<RectTransform>();
                for (var i = 0; i < 5; i++) { var child = new GameObject($"Card{i}", typeof(RectTransform)); child.transform.SetParent(root.transform, false); children.Add((RectTransform)child.transform); }
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(layout.ColumnCount, Is.EqualTo(expectedColumns));
                var lastRowStart = expectedColumns * (5 / expectedColumns);
                if (lastRowStart < 5) Assert.That(children[lastRowStart].anchoredPosition.x, Is.GreaterThanOrEqualTo(0f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Card_RebindRemovesOnlyItsOwnSelectionCallbackAndResetsMotion()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ExplorationNodeChoiceCardPrefabBuilder.PrefabPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<ExplorationNodeChoiceCardView>();
                var externalCalls = 0; view.Button.onClick.AddListener(() => externalCalls++);
                var firstCalls = 0; var secondCalls = 0;
                view.Bind(new ExplorationNodeChoiceCardData(new ExplorationNodeChoiceRequest("run", "set-1", "node-1"), "A", "B", "", null, true), _ => firstCalls++);
                view.Bind(new ExplorationNodeChoiceCardData(new ExplorationNodeChoiceRequest("run", "set-2", "node-2"), "C", "D", "미구현", null, true), _ => secondCalls++);
                view.Button.onClick.Invoke();
                Assert.That(firstCalls, Is.Zero); Assert.That(secondCalls, Is.EqualTo(1)); Assert.That(externalCalls, Is.EqualTo(1));
                Assert.That(instance.transform.Find("MotionRoot").localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(TextAnchor.UpperLeft, 72f, -92f)]
        [TestCase(TextAnchor.UpperCenter, 130f, -92f)]
        [TestCase(TextAnchor.UpperRight, 188f, -92f)]
        [TestCase(TextAnchor.MiddleLeft, 72f, -250f)]
        [TestCase(TextAnchor.MiddleCenter, 130f, -250f)]
        [TestCase(TextAnchor.MiddleRight, 188f, -250f)]
        [TestCase(TextAnchor.LowerLeft, 72f, -408f)]
        [TestCase(TextAnchor.LowerCenter, 130f, -408f)]
        [TestCase(TextAnchor.LowerRight, 188f, -408f)]
        public void Layout_AlignsIncompleteRowWithoutChangingOrder(TextAnchor alignment, float expectedCenterX, float expectedCenterY)
        {
            var root = new GameObject("Viewport", typeof(RectTransform));
            var content = new GameObject("Content", typeof(RectTransform), typeof(ExplorationNodeChoiceLayoutGroup));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(260f, 500f);
                content.transform.SetParent(root.transform, false);
                var rect = (RectTransform)content.transform; rect.sizeDelta = new Vector2(260f, 500f);
                var layout = content.GetComponent<ExplorationNodeChoiceLayoutGroup>();
                layout.Configure(120f, 160f, 0f, 0f, new RectOffset(12, 12, 12, 12), alignment);
                var card = new GameObject("Card", typeof(RectTransform)); card.transform.SetParent(content.transform, false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(((RectTransform)card.transform).anchoredPosition.x, Is.EqualTo(expectedCenterX).Within(.01f));
                Assert.That(((RectTransform)card.transform).anchoredPosition.y, Is.EqualTo(expectedCenterY).Within(.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Layout_PreferredHeightUsesViewportMinimumAndContentWhenOverflowing()
        {
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            var content = new GameObject("Content", typeof(RectTransform), typeof(ExplorationNodeChoiceLayoutGroup));
            try
            {
                ((RectTransform)viewport.transform).sizeDelta = new Vector2(260f, 500f);
                content.transform.SetParent(viewport.transform, false);
                var rect = (RectTransform)content.transform; rect.sizeDelta = new Vector2(260f, 500f);
                var layout = content.GetComponent<ExplorationNodeChoiceLayoutGroup>();
                layout.Configure(120f, 160f, 0f, 10f, new RectOffset(0, 0, 10, 10));
                new GameObject("Card", typeof(RectTransform)).transform.SetParent(content.transform, false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(LayoutUtility.GetPreferredHeight(rect), Is.EqualTo(500f).Within(.01f));
                for (var i = 0; i < 6; i++) new GameObject($"Extra{i}", typeof(RectTransform)).transform.SetParent(content.transform, false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(LayoutUtility.GetPreferredHeight(rect), Is.GreaterThan(500f));
            }
            finally { Object.DestroyImmediate(viewport); }
        }

        [Test]
        public void ShapePresentation_SanitizesInvalidSizeAndSupportsAllShapes()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ExplorationNodeChoiceCardPrefabBuilder.PrefabPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var presentation = instance.GetComponent<ExplorationNodeChoiceCardView>().ShapePresentation;
                foreach (ExplorationCardShape shape in System.Enum.GetValues(typeof(ExplorationCardShape)))
                {
                    var settings = ExplorationCardShapeSettings.Default;
                    settings.visualMode = ExplorationCardVisualMode.ProceduralShape; settings.shape = shape;
                    settings.size = new Vector2(float.NaN, -1f); settings.cornerRadius = float.PositiveInfinity;
                    presentation.Apply(settings);
                    Assert.That(presentation.Settings.size, Is.EqualTo(new Vector2(186f, 144f)));
                    Assert.That(presentation.Settings.cornerRadius, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
