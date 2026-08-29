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
