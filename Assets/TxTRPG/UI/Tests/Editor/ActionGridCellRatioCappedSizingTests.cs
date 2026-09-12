using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class ActionGridCellRatioCappedSizingTests
    {
        [TestCase(40f, 40f, 36f, 36f)]
        [TestCase(100f, 100f, 64f, 64f)]
        [TestCase(100f, 60f, 64f, 54f)]
        [TestCase(71.1f, 71.1f, 63.99f, 63.99f)]
        [TestCase(71.111115f, 71.111115f, 64f, 64f)]
        [TestCase(72f, 72f, 64f, 64f)]
        public void RatioCappedSizing_UsesAxisWiseMinimum(
            float width, float height, float expectedWidth, float expectedHeight)
        {
            var result = ActionGridCell.CalculateIconSize(new Vector2(width, height),
                ActionGridIconSizingMode.RelativeWithMaxSize, 0.9f, null,
                new Vector2(64f, 64f));
            Assert.That(result.x, Is.EqualTo(expectedWidth).Within(0.01f));
            Assert.That(result.y, Is.EqualTo(expectedHeight).Within(0.01f));
        }

        [Test]
        public void RatioCappedSizing_NormalizesInvalidAndNegativeValues()
        {
            var invalid = ActionGridCell.CalculateIconSize(new Vector2(100f, 100f),
                ActionGridIconSizingMode.RelativeWithMaxSize, float.NaN, null,
                new Vector2(float.PositiveInfinity, float.NaN));
            var negative = ActionGridCell.CalculateIconSize(new Vector2(100f, 100f),
                ActionGridIconSizingMode.RelativeWithMaxSize, 0.9f, null,
                new Vector2(-1f, 30f));

            Assert.That(invalid, Is.EqualTo(new Vector2(64f, 64f)));
            Assert.That(negative, Is.EqualTo(new Vector2(0f, 30f)));
        }

        [Test]
        public void Upgrade_PersistsIndependentCappedSettingsAndActualRects()
        {
            ActionGridPrefabBuilder.UpgradeCellRatioCappedLayout();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab");
            var cell = prefab.GetComponent<ActionGridCell>();

            Assert.That(cell.IconSizingMode,
                Is.EqualTo(ActionGridIconSizingMode.RelativeWithMaxSize));
            Assert.That(cell.EmptySlotSizingMode,
                Is.EqualTo(ActionGridIconSizingMode.RelativeWithMaxSize));
            Assert.That(cell.IconMaximumSize, Is.EqualTo(new Vector2(64f, 64f)));
            Assert.That(cell.EmptySlotMaximumSize, Is.EqualTo(new Vector2(64f, 64f)));

            var instance = Object.Instantiate(prefab).GetComponent<ActionGridCell>();
            try
            {
                ((RectTransform)instance.transform).sizeDelta = new Vector2(200f, 120f);
                Canvas.ForceUpdateCanvases();
                instance.ApplyIconLayout();
                instance.ApplyEmptySlotLayout();
                Assert.That(instance.IconRect.rect.size, Is.EqualTo(new Vector2(64f, 64f)));
                Assert.That(instance.EmptySlotRect.rect.size, Is.EqualTo(new Vector2(64f, 64f)));
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void IconAndEmptySlotCaps_RemainIndependent()
        {
            ActionGridPrefabBuilder.UpgradeCellRatioCappedLayout();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab");
            var instance = Object.Instantiate(prefab).GetComponent<ActionGridCell>();
            try
            {
                ((RectTransform)instance.transform).sizeDelta = new Vector2(120f, 120f);
                instance.ConfigureIconRatioCappedSizing(0.5f, new Vector2(20f, 30f));
                instance.ConfigureEmptySlotRatioCappedSizing(0.8f, new Vector2(50f, 40f));
                Canvas.ForceUpdateCanvases();
                instance.ApplyIconLayout();
                instance.ApplyEmptySlotLayout();

                Assert.That(instance.IconRect.rect.size, Is.EqualTo(new Vector2(20f, 30f)));
                Assert.That(instance.EmptySlotRect.rect.size, Is.EqualTo(new Vector2(50f, 40f)));
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }
    }
}
