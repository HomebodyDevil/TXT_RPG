using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class ActionGridPanelTests
    {
        [Test]
        public void Entry_NormalizesPresentationValues()
        {
            var entry = new ActionGridEntry(
                null,
                ActionGridEntryKind.Item,
                null,
                null,
                quantity: -3,
                cooldownNormalized: 2f);

            Assert.That(entry.Id, Is.Empty);
            Assert.That(entry.DisplayName, Is.Empty);
            Assert.That(entry.Quantity, Is.Zero);
            Assert.That(entry.CooldownNormalized, Is.EqualTo(1f));
        }

        [TestCase(760f, 5)]
        [TestCase(360f, 4)]
        [TestCase(170f, 2)]
        [TestCase(60f, 1)]
        public void FixedColumns_TreatsConfiguredValueAsMaximumAndWraps(float width, int expectedColumns)
        {
            var columns = ActionGridPanel.CalculateColumnCount(
                ActionGridLayoutMode.FixedColumns,
                5,
                width,
                72f,
                8f);

            Assert.That(columns, Is.EqualTo(expectedColumns));
        }

        [Test]
        public void AdaptiveColumns_UsesAllColumnsThatFit()
        {
            var columns = ActionGridPanel.CalculateColumnCount(
                ActionGridLayoutMode.AdaptiveCellSize,
                2,
                520f,
                72f,
                8f);

            Assert.That(columns, Is.EqualTo(6));
        }

        [Test]
        public void ContextMenuPosition_PrefersRightOfAnchor()
        {
            var position = ActionContextMenu.CalculatePosition(
                new Rect(-50f, -20f, 40f, 40f),
                new Vector2(100f, 80f),
                new Rect(-300f, -200f, 600f, 400f),
                10f,
                8f);

            Assert.That(position, Is.EqualTo(new Vector2(50f, 0f)));
        }

        [Test]
        public void ContextMenuPosition_FlipsLeftNearRightEdge()
        {
            var position = ActionContextMenu.CalculatePosition(
                new Rect(240f, -20f, 40f, 40f),
                new Vector2(100f, 80f),
                new Rect(-300f, -200f, 600f, 400f),
                10f,
                8f);

            Assert.That(position, Is.EqualTo(new Vector2(180f, 0f)));
        }

        [Test]
        public void ContextMenuPosition_UsesBelowWhenNeitherHorizontalSideFits()
        {
            var position = ActionContextMenu.CalculatePosition(
                new Rect(-20f, 50f, 40f, 40f),
                new Vector2(260f, 80f),
                new Rect(-150f, -200f, 300f, 400f),
                10f,
                8f);

            Assert.That(position, Is.EqualTo(new Vector2(0f, 0f)));
        }

        [Test]
        public void ContextMenuPosition_ClampsOversizedCandidateInsideBounds()
        {
            var position = ActionContextMenu.CalculatePosition(
                new Rect(80f, 70f, 20f, 20f),
                new Vector2(120f, 100f),
                new Rect(-100f, -80f, 200f, 160f),
                8f,
                10f);

            Assert.That(position.x, Is.InRange(-30f, 30f));
            Assert.That(position.y, Is.InRange(-20f, 20f));
        }

        [Test]
        public void GeneratedPrefabs_HaveRequiredBoundariesAndReferences()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();

            var cell = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab");
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionContextMenu.prefab");
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");

            Assert.That(cell.GetComponent<ActionGridCell>(), Is.Not.Null);
            Assert.That(cell.transform.Find("Border"), Is.Not.Null);
            Assert.That(cell.transform.Find("ContentRoot"), Is.Not.Null);
            Assert.That(cell.transform.Find("ContentRoot/CooldownOverlay"), Is.Not.Null);
            Assert.That(cell.transform.Find("SelectionFrame"), Is.Not.Null);
            Assert.That(menu.GetComponent<ActionContextMenu>(), Is.Not.Null);
            Assert.That(menu.transform.Find("OptionList/OptionTemplate"), Is.Not.Null);
            Assert.That(panel.GetComponent<ActionGridPanel>(), Is.Not.Null);
            Assert.That(panel.GetComponentInChildren<GridLayoutGroup>(true), Is.Not.Null);
            Assert.That(panel.transform.Find("ContextMenuAnchor/ActionContextMenu"), Is.Not.Null);
            var contextAnchor = (RectTransform)panel.transform.Find("ContextMenuAnchor");
            Assert.That(contextAnchor.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(contextAnchor.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void GeneratedDemo_ShowsMixedEntriesAndContextMenuPreview()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            ActionGridPanelDemoBuilder.CreateOrUpdateDemo();

            var data = AssetDatabase.LoadAssetAtPath<ActionGridPanelDemoData>(
                "Assets/TxTRPG/UI/DEMO/ActionGridPanel/ActionGridPanelDemoData.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/DEMO/ActionGridPanel/ActionGridPanelDemo.prefab");

            Assert.That(data, Is.Not.Null);
            var entries = data.CreateEntries();
            Assert.That(entries, Has.Some.Matches<ActionGridEntry>(entry => entry.Kind == ActionGridEntryKind.Item));
            Assert.That(entries, Has.Some.Matches<ActionGridEntry>(entry => entry.Kind == ActionGridEntryKind.Skill));
            Assert.That(prefab.GetComponent<ActionGridPanelDemoController>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<ActionGridCell>(true).Length, Is.EqualTo(entries.Count));
            var contextMenu = prefab.GetComponentInChildren<ActionContextMenu>(true);
            Assert.That(contextMenu.gameObject.activeSelf, Is.True);
            Assert.That(contextMenu.GetComponentsInChildren<Button>(true).Count(button => button.gameObject.activeSelf),
                Is.GreaterThanOrEqualTo(3));
        }
    }
}
