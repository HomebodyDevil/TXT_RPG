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
        public void SavedPrefabs_HaveRequiredComposition()
        {
            QuickItemsUiProjectBuilder.ValidateSavedPrefabs();
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuPrefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.ModalWindowPrefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuDemoPrefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuScreenPrefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.InventoryWindowPrefabPath), Is.Not.Null);

            var menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuPrefabPath);
            Assert.That(menuPrefab.GetComponent<GameMenuPanel>(), Is.Not.Null);
            Assert.That(menuPrefab.GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(menuPrefab.transform.Find("Viewport/Content").GetComponent<GameMenuLayoutGroup>(), Is.Not.Null);
            Assert.That(menuPrefab.GetComponentsInChildren<GameMenuButtonView>(true).Length, Is.EqualTo(3));
            Assert.That(menuPrefab.GetComponentsInChildren<GameMenuButtonView>(true).Single(view => view.name == "Inventory").DisplayMode,
                Is.EqualTo(GameMenuButtonDisplayMode.ImageWithLabel));
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
            AssertSystemPageIsEmptyAndClosable(binder.WindowService);
            AssertModalContainer(binder.WindowService);
            AssertErrorLayout(binder.WindowService);
            AssertTypedRequests(binder.Menu);

        }

        [Test]
        public void MainSceneInventoryIntegration_IsSavedOnceAndPreservesMenuTransform()
        {
            var scene = EditorSceneManager.OpenScene(QuickItemsUiProjectBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                var menu = components.OfType<GameMenuPanel>().Single();
                var service = components.OfType<GameWindowService>().Single();
                Assert.That(service.Pages.Count(page => page != null && page.PageId == GamePageIds.Inventory), Is.EqualTo(1));
                Assert.That(service.Pages.Single(page => page.PageId == GamePageIds.Inventory), Is.TypeOf<InventoryGameWindowPage>());
                AssertSystemPageIsEmptyAndClosable(service);
                AssertModalContainer(service);
                AssertErrorLayout(service);
                Assert.That(menu.WindowService, Is.SameAs(service));
                Assert.That(menu.QuickItemGrid, Is.Not.Null);
                Assert.That(menu.transform.parent.name, Is.EqualTo("ContentLayer"));
                Assert.That(components.OfType<ModalWindowHost>().Count(), Is.EqualTo(1));
                AssertTypedRequests(menu);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void TargetedSettingsMigration_PreservesPrefabComposition()
        {
            var modal = AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.ModalWindowPrefabPath);
            Assert.That(modal, Is.Not.Null);
            AssertSystemPageIsEmptyAndClosable(modal.GetComponent<GameWindowService>());
            Assert.That(modal.GetComponentsInChildren<InventoryGameWindowPage>(true).Length, Is.EqualTo(1));
            Assert.That(modal.GetComponentsInChildren<StatusGameWindowPage>(true).Length, Is.EqualTo(1));
            AssertModalContainer(modal.GetComponent<GameWindowService>());
            AssertErrorLayout(modal.GetComponent<GameWindowService>());
        }

        private static void AssertSystemPageIsEmptyAndClosable(GameWindowService service)
        {
            var system = service.Pages.Single(page => page != null && page.PageId == GamePageIds.System);
            Assert.That(system, Is.TypeOf<MessageGameWindowPage>());
            Assert.That(system.DisplayTitle, Is.EqualTo("System Settings"));
            Assert.That(((MessageGameWindowPage)system).ConfiguredMessage, Is.Empty);
            Assert.That(system.InitialFocus, Is.SameAs(service.Host.CloseButton.gameObject));
        }

        private static void AssertModalContainer(GameWindowService service)
        {
            Assert.That(service.Host.ContentContainer, Is.Not.Null);
            Assert.That(service.Host.ContentContainer.ValidateConfiguration(out var reason), Is.True, reason);
            Assert.That(service.Pages.All(page => page.transform.IsChildOf(service.Host.ContentContainer.ContentRoot)), Is.True);
            Assert.That(service.Host.ContentContainer.OverlayRoot, Is.Not.SameAs(service.Host.ContentContainer.ContentRoot));
        }

        private static void AssertErrorLayout(GameWindowService service)
        {
            var root = service.Host.transform.Find("Window/ErrorStateRoot") as RectTransform;
            Assert.That(root, Is.Not.Null);
            var error = root.Find("Error") as RectTransform;
            Assert.That(error, Is.Not.Null);
            var rect = error;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.localEulerAngles.z, Is.EqualTo(0).Within(.001f));
            Assert.That(root.GetComponentInChildren<Button>(true), Is.Not.Null);
        }

        private static void AssertTypedRequests(GameMenuPanel menu)
        {
            var inventory = menu.Buttons.Single(binding => binding.PageId == GamePageIds.Inventory);
            var system = menu.Buttons.Single(binding => binding.PageId == GamePageIds.System);
            Assert.That(inventory.OverridesRequestPresentation, Is.True);
            Assert.That(inventory.ContentKind, Is.EqualTo(ModalContentKind.ItemGrid));
            Assert.That(inventory.RequestTitle, Is.EqualTo("Bag"));
            Assert.That(system.OverridesRequestPresentation, Is.True);
            Assert.That(system.ContentKind, Is.EqualTo(ModalContentKind.CustomContent));
        }
    }
}
