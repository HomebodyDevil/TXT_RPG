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

        [Test]
        public void ConfiguredScene_UsesProductionControllerAndNoEnemyDemoWriter()
        {
            var loaded = SceneManager.GetSceneByPath(MainSceneDefaultContentBuilder.ScenePath);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                Assert.Ignore("TMP_MainScene has unsaved user changes and must not be overwritten by a test.");
            var scene = EditorSceneManager.OpenScene(MainSceneDefaultContentBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                Assert.That(components.OfType<MainScenePresentationController>().Count(), Is.EqualTo(1));
                Assert.That(components.OfType<EnemyDisplayPanelDemoLoader>(), Is.Empty);
                Assert.That(components.OfType<StoryTextPanel>().Count(), Is.EqualTo(1));
                Assert.That(components.OfType<ActionGridPanel>().Count(), Is.EqualTo(1));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
