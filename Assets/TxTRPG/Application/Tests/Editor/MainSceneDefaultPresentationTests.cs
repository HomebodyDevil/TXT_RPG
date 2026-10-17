using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Presentation;
using TxTRPG.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.Application.Tests
{
    public sealed class MainSceneDefaultPresentationTests
    {
        [Test]
        public void PresentationStates_KeepReadyEmptyDistinctFromMissingAndFailure()
        {
            Assert.That(PresentationDataState.ReadyEmpty, Is.Not.EqualTo(PresentationDataState.MissingConfiguration));
            Assert.That(PresentationDataState.MissingConfiguration, Is.Not.EqualTo(PresentationDataState.Failed));
            var empty = new StoryPresentationResult(PresentationDataState.ReadyEmpty);
            Assert.That(empty.Messages, Is.Empty);
        }

        [Test]
        [Category("AssetWriting")]
        public void AssetBuilder_CreatesDeploymentAndIsolatedPreviewProfiles()
        {
            var defaults = MainSceneDefaultContentBuilder.CreateOrUpdateAssets();
            Assert.That(defaults, Is.Not.Null);
            Assert.That(defaults.StoryMissingConfigurationText, Is.Not.Empty);
            Assert.That(defaults.UnknownHealthValue, Is.Not.Empty);
            foreach (MainScenePreviewScenario scenario in System.Enum.GetValues(typeof(MainScenePreviewScenario)))
                Assert.That(AssetDatabase.LoadAssetAtPath<MainScenePreviewProfile>($"{MainSceneDefaultContentBuilder.PreviewFolder}/{scenario}.asset"), Is.Not.Null);
            var harness = AssetDatabase.LoadAssetAtPath<GameObject>(MainSceneDefaultContentBuilder.PreviewHarnessPrefabPath);
            Assert.That(harness, Is.Not.Null);
            Assert.That(harness.GetComponent<MainScenePreviewHarness>(), Is.Not.Null);
        }

        // TMP_MainScene keeps the EnemyDisplayPanelDemo Prefab instance, but its startup no longer loads demo
        // enemies; the temporary combat controller owns enemy display. MainScenePresentationController is not
        // part of any operating Scene.
        [Test]
        public void ConfiguredScene_HasNoEnemyDemoWriterAndKeepsCorePanels()
        {
            var loaded = SceneManager.GetSceneByPath(MainSceneDefaultContentBuilder.ScenePath);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                Assert.Ignore("TMP_MainScene has unsaved user changes and must not be overwritten by a test.");
            var scene = EditorSceneManager.OpenScene(MainSceneDefaultContentBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                foreach (var startup in components.OfType<PanelStartupController>())
                {
                    var loader = new SerializedObject(startup).FindProperty("initialDataLoader").objectReferenceValue;
                    Assert.That(loader, Is.Not.InstanceOf<EnemyDisplayPanelDemoLoader>(),
                        $"{startup.name} must not load demo enemies in the operating Scene.");
                }
                Assert.That(components.OfType<TxTRPG.Application.Dice.TemporaryDiceRollMenuController>().Count(), Is.EqualTo(1));
                Assert.That(components.OfType<EnemyDisplayPanel>().Count(), Is.EqualTo(1));
                Assert.That(components.OfType<StoryTextPanel>().Count(), Is.EqualTo(1));
                // One main Actions grid; the inventory window page owns a second, separate grid.
                var grids = components.OfType<ActionGridPanel>().ToArray();
                var inventoryGrids = grids.Where(grid =>
                    grid.GetComponentInParent<TxTRPG.Application.Items.InventoryGameWindowPage>(true) != null).ToArray();
                Assert.That(grids.Length - inventoryGrids.Length, Is.EqualTo(1), "Main Actions grid");
                Assert.That(inventoryGrids.Length, Is.EqualTo(1), "Inventory window grid");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
