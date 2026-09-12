using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class ActionGridCellEmptySlotSizingTests
    {
        private static readonly RectOffset LegacyPadding = new(14, 14, 14, 14);

        [Test]
        public void ContentForty_UsesExpectedRelativeAndLegacySizes()
        {
            var contentSize = new Vector2(40f, 40f);
            var relative = ActionGridCell.CalculateIconSize(contentSize,
                ActionGridIconSizingMode.RelativeToContent, 0.9f, LegacyPadding);
            var fixedPadding = ActionGridCell.CalculateIconSize(contentSize,
                ActionGridIconSizingMode.FixedPadding, 0.9f, LegacyPadding);

            Assert.That(relative.x, Is.EqualTo(36f).Within(0.001f));
            Assert.That(relative.y, Is.EqualTo(36f).Within(0.001f));
            Assert.That(fixedPadding.x, Is.EqualTo(12f).Within(0.001f));
            Assert.That(fixedPadding.y, Is.EqualTo(12f).Within(0.001f));
        }

        [TestCase(0f, 0f, 0.9f)]
        [TestCase(26f, 40f, 0.9f)]
        [TestCase(194f, 94f, 0.9f)]
        [TestCase(40f, 40f, 0f)]
        [TestCase(40f, 40f, 1f)]
        public void RelativeSizing_HandlesRepresentativeSizes(float width, float height, float ratio)
        {
            var result = ActionGridCell.CalculateIconSize(new Vector2(width, height),
                ActionGridIconSizingMode.RelativeToContent, ratio, LegacyPadding);
            Assert.That(result.x, Is.EqualTo(width * ratio).Within(0.001f));
            Assert.That(result.y, Is.EqualTo(height * ratio).Within(0.001f));
        }

        [Test]
        public void InvalidRatioAndOversizedPadding_AreSafe()
        {
            var invalid = ActionGridCell.CalculateIconSize(new Vector2(40f, 20f),
                ActionGridIconSizingMode.RelativeToContent, float.PositiveInfinity, null);
            var oversized = ActionGridCell.CalculateIconSize(new Vector2(8f, 5f),
                ActionGridIconSizingMode.FixedPadding, 0.9f, new RectOffset(30, 40, 50, 60));

            Assert.That(invalid, Is.EqualTo(new Vector2(36f, 18f)));
            Assert.That(oversized.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(oversized.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void UpgradedPrefab_PersistsIndependentEmptySlotLayout()
        {
            ActionGridPrefabBuilder.UpgradeCellEmptySlotLayout();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab");
            var cell = prefab.GetComponent<ActionGridCell>();
            var icon = (RectTransform)prefab.transform.Find("ContentRoot/Icon");
            var emptySlot = (RectTransform)prefab.transform.Find("ContentRoot/EmptySlot");

            Assert.That(cell.EmptySlotSizingMode,
                Is.EqualTo(ActionGridIconSizingMode.RelativeToContent));
            Assert.That(cell.EmptySlotAreaRatio, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(Vector2.Distance(emptySlot.anchorMin, new Vector2(0.05f, 0.05f)),
                Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(emptySlot.anchorMax, new Vector2(0.95f, 0.95f)),
                Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(icon.anchorMin, new Vector2(0.05f, 0.05f)),
                Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(icon.anchorMax, new Vector2(0.95f, 0.95f)),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void BindTransitions_PreserveEmptySlotLayoutAndVisibility()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab");
            var instance = Object.Instantiate(prefab).GetComponent<ActionGridCell>();
            try
            {
                instance.ConfigureEmptySlotSizing(ActionGridIconSizingMode.RelativeToContent, 0.75f);
                instance.BindEmpty(0, null);
                Assert.That(instance.IsEmptySlotVisible, Is.True);

                instance.Bind(0, new ActionGridEntry("test", ActionGridEntryKind.Item, null, "Test"), null);
                Assert.That(instance.IsEmptySlotVisible, Is.False);

                instance.BindEmpty(0, null);
                Assert.That(instance.IsEmptySlotVisible, Is.True);
                Assert.That(instance.EmptySlotAreaRatio, Is.EqualTo(0.75f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }
    }
}
