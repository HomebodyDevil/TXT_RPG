using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Items;
using TxTRPG.Content.Items;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Players;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.Application.Tests
{
    public sealed class InventoryEmptyStateTests
    {
        private readonly List<Object> created = new();
        private GameObject root;
        private InventoryGameWindowPage page;
        private PlayerState player;
        private ActionGridPanel grid;
        private TMP_Text empty;

        [SetUp]
        public void SetUp()
        {
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.InventoryWindowPrefabPath));
            root.SetActive(false);
            page = root.GetComponent<InventoryGameWindowPage>();
            grid = root.GetComponentInChildren<ActionGridPanel>(true);
            empty = root.transform.Find("EmptyState").GetComponent<TMP_Text>();
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("characterId").stringValue = "test-hero";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            player = new PlayerState(new[] { new CharacterFactory(new[] { definition }).Create("test-hero", "hero-1") }, "hero-1");
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>(); created.Add(catalog);
            var items = new List<ItemDefinition>();
            for (var i = 0; i < 13; i++)
            {
                var item = ScriptableObject.CreateInstance<ItemDefinition>(); created.Add(item);
                item.ConfigureForEditor("item-" + i.ToString("D2"), "Item", "", ItemEffectKind.Healing, 1);
                item.SetCategoryForEditor("consumable"); items.Add(item);
            }
            catalog.ConfigureForEditor(items);
            typeof(InventoryGameWindowPage).GetField("player", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(page, player);
            var settings = new SerializedObject(page);
            settings.FindProperty("catalog").objectReferenceValue = catalog;
            // Edit Mode cannot Destroy generated numbered buttons; they are unrelated to this projection test.
            settings.FindProperty("pageNumbersRoot").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (var value in created) Object.DestroyImmediate(value);
            created.Clear();
        }
        [TestCase(InventoryDisplayMode.VerticalScroll, 0, 0, false, 0)]
        [TestCase(InventoryDisplayMode.VerticalScroll, 0, 1, false, 1)]
        [TestCase(InventoryDisplayMode.VerticalScroll, 0, 12, false, 12)]
        [TestCase(InventoryDisplayMode.VerticalScroll, 1, 0, false, 1)]
        [TestCase(InventoryDisplayMode.VerticalScroll, 13, 12, false, 13)]
        [TestCase(InventoryDisplayMode.Paged, 0, 0, false, 0)]
        [TestCase(InventoryDisplayMode.Paged, 0, 0, true, 12)]
        [TestCase(InventoryDisplayMode.Paged, 1, 0, false, 1)]
        [TestCase(InventoryDisplayMode.Paged, 1, 0, true, 12)]
        [TestCase(InventoryDisplayMode.Paged, 12, 0, false, 12)]
        [TestCase(InventoryDisplayMode.Paged, 12, 0, true, 12)]
        [TestCase(InventoryDisplayMode.Paged, 13, 0, false, 1)]
        [TestCase(InventoryDisplayMode.Paged, 13, 0, true, 12)]
        public void EmptyLabelTracksBoundSlotsIncludingLastPage(InventoryDisplayMode mode, int count, int minimum, bool fill, int expected)
        {
            for (var i = 0; i < count; i++) player.Inventory.Add("item-" + i.ToString("D2"), 1);
            page.ConfigureGrid(new GridContentLayoutSettings { displayMode = mode, minimumScrollSlots = minimum, slotsPerPage = 12, fillPageWithEmptySlots = fill });
            page.GoToPage(999);
            Assert.That(grid.VisibleCellCount, Is.EqualTo(expected));
            Assert.That(empty.gameObject.activeSelf, Is.EqualTo(expected == 0));
            Assert.That(player.Inventory.Quantities.Count(), Is.EqualTo(count));
        }
        [Test]
        public void PresentationSourceSurvivesRefreshAndReturnsToManual()
        {
            var settings = new GridContentLayoutSettings { displayMode = InventoryDisplayMode.VerticalScroll };
            page.ConfigureGrid(settings);
            Assert.That(grid.DisplayMode, Is.EqualTo(ActionGridDisplayMode.Manual));
            settings.presentation.mode = ActionGridDisplayMode.DistributedSpacing;
            settings.presentation.distributedSpacing.maximumHorizontalGap = 27;
            page.ConfigureGrid(settings);
            grid.SetDisplayMode(ActionGridDisplayMode.LargeSlots);
            page.SelectCategory("consumable");
            Assert.That(grid.DisplayMode, Is.EqualTo(ActionGridDisplayMode.DistributedSpacing));
            Assert.That(grid.GetDisplaySettings().distributedSpacing.maximumHorizontalGap, Is.EqualTo(27));
            Assert.That(grid.VisibleCellCount, Is.EqualTo(12));
            settings.presentation.mode = ActionGridDisplayMode.Manual;
            page.ConfigureGrid(settings);
            Assert.That(grid.DisplayMode, Is.EqualTo(ActionGridDisplayMode.Manual));
            Assert.That(grid.Spacing, Is.EqualTo(settings.spacing));
        }
        [Test]
        public void SingleRowIsOwnedByPageAndRefreshKeepsHorizontalAxis()
        {
            var settings = new GridContentLayoutSettings { displayMode = InventoryDisplayMode.VerticalScroll };
            settings.presentation.flow = ActionGridFlow.SingleRow;
            settings.presentation.singleRow.centerThreshold = 6;
            page.ConfigureGrid(settings);
            Assert.That(grid.SurfaceLayout, Is.Not.Null);
            Assert.That(grid.IsSingleRow, Is.True);
            Assert.That(grid.ScrollRect.vertical, Is.False);
            page.SelectCategory("consumable");
            Assert.That(grid.IsSingleRow, Is.True);
            Assert.That(grid.GetDisplaySettings().singleRow.centerThreshold, Is.EqualTo(6));
            page.SetDisplayMode(InventoryDisplayMode.Paged);
            Assert.That(grid.ScrollRect.horizontal, Is.True);
            Assert.That(grid.ScrollRect.vertical, Is.False);
            settings.presentation.flow = ActionGridFlow.Grid;
            page.ConfigureGrid(settings);
            Assert.That(grid.IsSingleRow, Is.False);
            Assert.That(grid.ScrollRect.vertical, Is.True);
        }
        [Test]
        public void FilteringRemovalModeChangesAndUnknownSlotsUseSameRule()
        {
            player.Inventory.Add("unknown", 1);
            page.ConfigureGrid(new GridContentLayoutSettings { displayMode = InventoryDisplayMode.Paged, fillPageWithEmptySlots = false });
            Assert.That(empty.gameObject.activeSelf, Is.False);
            page.SelectCategory("consumable"); Assert.That(empty.gameObject.activeSelf, Is.True);
            page.SelectCategory("misc"); Assert.That(empty.gameObject.activeSelf, Is.False);
            player.Inventory.TryConsume("unknown");
            page.GoToPage(2); Assert.That(empty.gameObject.activeSelf, Is.True);
            Assert.That(page.CurrentPageNumber, Is.EqualTo(1));
            page.SetDisplayMode(InventoryDisplayMode.VerticalScroll); Assert.That(empty.gameObject.activeSelf, Is.False);
            grid.ScrollRect.verticalNormalizedPosition = 0; Assert.That(empty.gameObject.activeSelf, Is.False);
            page.Hide(); page.Show(); Assert.That(empty.gameObject.activeSelf, Is.False);
        }
        [Test]
        public void SavedAssetsHaveExplicitBoundariesAndNonRaycastingEmptyLabel()
        {
            foreach (var path in new[] { QuickItemsUiProjectBuilder.ModalWindowPrefabPath, QuickItemsUiProjectBuilder.GameMenuScreenPrefabPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var host in prefab.GetComponentsInChildren<ModalWindowHost>(true)) AssertHost(host);
            }
            Assert.That(empty.raycastTarget, Is.False);
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.InventoryWindowPrefabPath);
            Assert.That(saved.transform.Find("EmptyState").gameObject.activeSelf, Is.False);
        }
        [Test]
        public void MainSceneInheritsBoundaryWithoutNewOverrides()
        {
            var scene = EditorSceneManager.OpenScene(QuickItemsUiProjectBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var host = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ModalWindowHost>(true)).Single();
                AssertHost(host);
                Assert.That(new SerializedObject(host).FindProperty("windowRect").prefabOverride, Is.False);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        private static void AssertHost(ModalWindowHost host)
        {
            Assert.That(host.WindowRect, Is.SameAs(host.transform.Find("Window")));
            Assert.That(host.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(host.WindowRect.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(host.ContentContainer.OverlayRoot.IsChildOf(host.WindowRect), Is.True);
        }
    }
}
