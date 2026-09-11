using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Items;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace TxTRPG.Application.Tests
{
    public sealed class QuickItemsUiProjectBuilderTests
    {
        [Test]
        public void Builder_CreatesContentPrefabsAndConnectsRuntimeScene()
        {
            var loadedScene = SceneManager.GetSceneByPath(QuickItemsUiProjectBuilder.ScenePath);
            if (loadedScene.IsValid() && loadedScene.isLoaded && loadedScene.isDirty)
                Assert.Ignore("TMP_MainScene has unsaved user changes; the builder correctly refuses to overwrite them.");
            var catalog = QuickItemsUiProjectBuilder.CreateOrUpdateContent();
            QuickItemsUiProjectBuilder.ConfigureScene(QuickItemsUiProjectBuilder.ScenePath, catalog);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuPrefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.ModalWindowPrefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuDemoPrefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuScreenPrefabPath), Is.Not.Null);

            var menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuPrefabPath);
            Assert.That(menuPrefab.GetComponent<GameMenuPanel>(), Is.Not.Null);
            Assert.That(menuPrefab.GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(menuPrefab.transform.Find("Viewport/Content").GetComponent<GameMenuLayoutGroup>(), Is.Not.Null);
            Assert.That(menuPrefab.GetComponentsInChildren<GameMenuButtonView>(true).Length, Is.EqualTo(3));
            Assert.That(menuPrefab.transform.Find("Viewport/Content/System/VisualRoot/EffectOverlay"), Is.Not.Null);
            var menu = menuPrefab.GetComponent<GameMenuPanel>();
            Assert.That(menu.HasValidInternalConfiguration, Is.True);
            Assert.That(menu.WindowService, Is.Null);
            Assert.That(menu.QuickItemGrid, Is.Null);

            var screenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuScreenPrefabPath);
            var binder = screenPrefab.GetComponent<GameMenuCompositionBinder>();
            Assert.That(binder, Is.Not.Null);
            Assert.That(binder.Menu, Is.Not.Null);
            Assert.That(binder.WindowService, Is.Not.Null);
            Assert.That(binder.OverlayRoot, Is.Not.Null);

            var scene = EditorSceneManager.OpenScene(QuickItemsUiProjectBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                Assert.That(components.OfType<QuickItemGridPresenter>().Count(), Is.EqualTo(1));
                Assert.That(components.OfType<GameMenuPanel>().Count(), Is.EqualTo(1));
                Assert.That(components.OfType<GameWindowService>().Count(), Is.EqualTo(1));
                Assert.That(components.OfType<ModalWindowHost>().Count(), Is.EqualTo(1));
                Assert.That(components.OfType<ActionGridPanel>().Count(), Is.GreaterThanOrEqualTo(1));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
