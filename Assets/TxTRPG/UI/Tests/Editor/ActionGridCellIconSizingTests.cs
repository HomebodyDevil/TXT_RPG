using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class ActionGridCellIconSizingTests
    {
        private static readonly RectOffset LegacyPadding = new(10, 10, 10, 10);

        [Test]
        public void SmallCell_UsesExpectedRelativeAndLegacySizes()
        {
            var contentSize = new Vector2(39f, 39f);
            var relative = ActionGridCell.CalculateIconSize(contentSize,
                ActionGridIconSizingMode.RelativeToContent, 0.9f, LegacyPadding);
            var fixedPadding = ActionGridCell.CalculateIconSize(contentSize,
                ActionGridIconSizingMode.FixedPadding, 0.9f, LegacyPadding);
            Assert.That(relative.x, Is.EqualTo(35.1f).Within(0.001f));
            Assert.That(relative.y, Is.EqualTo(35.1f).Within(0.001f));
            Assert.That(fixedPadding.x, Is.EqualTo(19f).Within(0.001f));
            Assert.That(fixedPadding.y, Is.EqualTo(19f).Within(0.001f));
        }

        [TestCase(32f, 26f)]
        [TestCase(45f, 39f)]
        [TestCase(72f, 66f)]
        [TestCase(96f, 90f)]
        [TestCase(128f, 122f)]
        [TestCase(200f, 194f)]
        public void RelativeSizing_TracksSquareContent(float cellSize, float contentSize)
        {
            var result = ActionGridCell.CalculateIconSize(new Vector2(contentSize, contentSize),
                ActionGridIconSizingMode.RelativeToContent, 0.9f, LegacyPadding);
            Assert.That(result.x, Is.EqualTo(contentSize * 0.9f).Within(0.001f), cellSize.ToString());
            Assert.That(result.y, Is.EqualTo(contentSize * 0.9f).Within(0.001f), cellSize.ToString());
        }

        [Test]
        public void NonSquareAndBoundaryRatios_AreStable()
        {
            Assert.That(ActionGridCell.CalculateIconSize(new Vector2(80f, 40f),
                ActionGridIconSizingMode.RelativeToContent, 1f, null), Is.EqualTo(new Vector2(80f, 40f)));
            Assert.That(ActionGridCell.CalculateIconSize(new Vector2(80f, 40f),
                ActionGridIconSizingMode.RelativeToContent, 0f, null), Is.EqualTo(Vector2.zero));
            Assert.That(ActionGridCell.CalculateIconSize(Vector2.zero,
                ActionGridIconSizingMode.RelativeToContent, 0.9f, null), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void InvalidRatioAndOversizedPadding_CannotInvertRect()
        {
            var invalidRatio = ActionGridCell.CalculateIconSize(new Vector2(100f, 50f),
                ActionGridIconSizingMode.RelativeToContent, float.NaN, null);
            Assert.That(invalidRatio, Is.EqualTo(new Vector2(90f, 45f)));

            var oversized = ActionGridCell.CalculateIconSize(new Vector2(12f, 7f),
                ActionGridIconSizingMode.FixedPadding, 1f, new RectOffset(30, 40, 50, 60));
            Assert.That(oversized.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(oversized.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void UpgradedPrefab_PersistsRelativeLayoutAndOtherOverlayRects()
        {
            ActionGridPrefabBuilder.UpgradeCellIconLayout();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab");
            var cell = prefab.GetComponent<ActionGridCell>();
            var icon = (RectTransform)prefab.transform.Find("ContentRoot/Icon");
            var quantity = (RectTransform)prefab.transform.Find("ContentRoot/Quantity");
            var selection = (RectTransform)prefab.transform.Find("SelectionFrame");

            Assert.That(cell.IconSizingMode, Is.EqualTo(ActionGridIconSizingMode.RelativeToContent));
            Assert.That(cell.IconAreaRatio, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(Vector2.Distance(icon.anchorMin, new Vector2(0.05f, 0.05f)), Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(icon.anchorMax, new Vector2(0.95f, 0.95f)), Is.LessThan(0.0001f));
            Assert.That(icon.GetComponent<Image>().preserveAspect, Is.True);
            Assert.That(quantity.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(selection.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(selection.anchorMax, Is.EqualTo(Vector2.one));
        }
    }
}

