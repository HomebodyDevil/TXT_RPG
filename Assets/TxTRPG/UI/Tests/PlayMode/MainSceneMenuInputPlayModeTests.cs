using System.Collections;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Items;
using TxTRPG.Application.Players;
using TxTRPG.Application.Dice;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TxTRPG.UI.Tests
{
    public sealed class MainSceneMenuInputPlayModeTests
    {
        private const string AppScenePath = "Assets/Scenes/AppScene.unity";
        private const string MainScenePath = "Assets/Scenes/TMP_MainScene.unity";

        [UnityTest]
        public IEnumerator AppScene_MenuButtonsOpenInventoryAndEmptySystemModal()
        {
            SceneManager.LoadScene(AppScenePath, LoadSceneMode.Single);
            yield return WaitForScene(MainScenePath, 600);

            var mainScene = SceneManager.GetSceneByPath(MainScenePath);
            Assert.That(mainScene.IsValid() && mainScene.isLoaded, Is.True);
            var components = mainScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            var menu = components.OfType<GameMenuPanel>().Single();
            var service = components.OfType<GameWindowService>().Single();
            var inventory = service.Pages.Single(page => page.PageId == GamePageIds.Inventory);
            var system = service.Pages.Single(page => page.PageId == GamePageIds.System);
            var inventoryButton = menu.Buttons.Single(binding => binding.PageId == GamePageIds.Inventory).Button;
            var systemButton = menu.Buttons.Single(binding => binding.PageId == GamePageIds.System).Button;

            Assert.That(inventoryButton.interactable, Is.True, menu.UnavailableReason);
            EventSystem.current.SetSelectedGameObject(inventoryButton.gameObject);
            ExecuteEvents.Execute(inventoryButton.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return WaitForPage(service, GamePageIds.Inventory, 300);
            Assert.That(inventory.gameObject.activeSelf, Is.True);
            Assert.That(service.LastRequest.ContentKind, Is.EqualTo(ModalContentKind.ItemGrid));
            Assert.That(service.LastRequest.DataProvider, Is.TypeOf<InventoryModalDataProvider>());
            Assert.That(service.LastRequest.Configuration, Is.TypeOf<GridContentLayoutSettings>());
            var inventoryPage = (InventoryGameWindowPage)inventory;
            inventoryPage.ConfigureGrid(new GridContentLayoutSettings
            {
                displayMode = InventoryDisplayMode.Paged,
                slotsPerPage = 1,
                fillPageWithEmptySlots = true
            });
            yield return null;
            Assert.That(inventoryPage.CurrentPageNumber, Is.EqualTo(1));
            Assert.That(inventoryPage.VisiblePageNumberButtonCount, Is.EqualTo(1));
            Assert.That(inventoryPage.IsVerticalScrollEnabled, Is.False);
            inventoryPage.SetDisplayMode(InventoryDisplayMode.VerticalScroll);
            yield return null;
            Assert.That(inventoryPage.VisiblePageNumberButtonCount, Is.EqualTo(0));
            Assert.That(inventoryPage.IsVerticalScrollEnabled, Is.True);

            service.Close();
            yield return null;
            Assert.That(service.IsOpen, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(inventoryButton.gameObject));

            Assert.That(systemButton.interactable, Is.True, menu.UnavailableReason);
            EventSystem.current.SetSelectedGameObject(systemButton.gameObject);
            ExecuteEvents.Execute(systemButton.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return WaitForPage(service, GamePageIds.System, 120);
            Assert.That(system.gameObject.activeSelf, Is.True);
            Assert.That(((MessageGameWindowPage)system).ConfiguredMessage, Is.Empty);
            Assert.That(service.LastRequest.ContentKind, Is.EqualTo(ModalContentKind.CustomContent));
            Assert.That(service.LastRequest.DataProvider, Is.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(service.Host.CloseButton.gameObject));

            ExecuteEvents.Execute(service.Host.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            yield return null;
            Assert.That(service.IsOpen, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(systemButton.gameObject));
        }

        [UnityTest]
        public IEnumerator AppScene_TemporaryCombatAction_UpdatesTemporaryStateAndPreservesOperatingHealth()
        {
            SceneManager.LoadScene(AppScenePath, LoadSceneMode.Single);
            yield return WaitForScene(MainScenePath, 600);
            var mainScene = SceneManager.GetSceneByPath(MainScenePath);
            var components = mainScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            var menu = components.OfType<GameMenuPanel>().Single();
            var story = components.OfType<StoryTextPanel>().Single();
            var binding = menu.Buttons.Single(item => item.ActionKind == GameMenuButtonActionKind.Command && item.CommandId == TemporaryDiceRollMenuController.RollAllCommandId);
            var controller = components.OfType<TemporaryDiceRollMenuController>().Single();
            for (var frame = 0; frame < 300 && !binding.Button.interactable; frame++) yield return null;
            Assert.That(binding.Button.interactable, Is.True, menu.UnavailableReason);
            Assert.That(binding.Button.GetComponentInChildren<TMPro.TMP_Text>(true).text, Is.EqualTo("행동"));
            var session = PlayerSessionHost.Instance.Session;
            var healthBefore = session.CurrentPlayer.ActiveCharacter.Health.Current;
            var before = story.MessageCount;
            ExecuteEvents.Execute(binding.Button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;
            Assert.That(story.MessageCount, Is.EqualTo(before + 7));
            Assert.That(controller.Combat.NextEnemyAction, Is.EqualTo(TxTRPG.Gameplay.Combat.EnemyActionKind.Heal));
            ExecuteEvents.Execute(binding.Button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;
            Assert.That(story.MessageCount, Is.EqualTo(before + 14));
            Assert.That(controller.Combat.NextEnemyAction, Is.EqualTo(TxTRPG.Gameplay.Combat.EnemyActionKind.Attack));
            Assert.That(session.CurrentPlayer.ActiveCharacter.Health.Current, Is.EqualTo(healthBefore));
            var texts = story.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(text => text.text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
            Assert.That(texts.Count(text => text.Contains("D4") || text.Contains("D6") || text.Contains("D8")), Is.GreaterThanOrEqualTo(6));
            Assert.That(texts.Count(text => text.Contains("공격 ") || text.Contains("회복 ")), Is.GreaterThanOrEqualTo(6));
            Assert.That(texts.Any(text => text.Contains("[적 행동]")), Is.True);
        }
        private static IEnumerator WaitForScene(string path, int frameLimit)
        {
            for (var frame = 0; frame < frameLimit; frame++)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) yield break;
                yield return null;
            }
            Assert.Fail($"Scene '{path}' did not load within {frameLimit} frames.");
        }

        private static IEnumerator WaitForPage(GameWindowService service, string pageId, int frameLimit)
        {
            for (var frame = 0; frame < frameLimit; frame++)
            {
                if (service.CurrentPageId == pageId) yield break;
                yield return null;
            }
            Assert.Fail($"Page '{pageId}' did not open within {frameLimit} frames.");
        }
    }
}
