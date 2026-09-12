using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.Application.Items;
using TxTRPG.Content.Items;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Items;
using TxTRPG.Gameplay.Players;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class QuickItemServiceTests
    {
        private readonly List<Object> created = new();
        [TearDown] public void TearDown() { foreach (var value in created) Object.DestroyImmediate(value); created.Clear(); }

        [Test]
        public async Task HealingItem_ConsumesExactlyOnceOnlyWhenHealingSucceeds()
        {
            var (player, service) = CreateState(2, 50);
            Assert.That(service.TryRegister(0, "potion"), Is.True);
            var result = await service.UseAsync(0);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.AppliedAmount, Is.EqualTo(25));
            Assert.That(player.ActiveCharacter.Health.Current, Is.EqualTo(75));
            Assert.That(player.Inventory.GetQuantity("potion"), Is.EqualTo(1));
        }

        [Test]
        public async Task HealingItem_DoesNotConsumeAtFullHealthOrWhenOutOfStock()
        {
            var (fullPlayer, fullService) = CreateState(1, 100);
            fullService.TryRegister(0, "potion");
            Assert.That((await fullService.UseAsync(0)).Failure, Is.EqualTo(ItemUseFailure.TargetAtFullHealth));
            Assert.That(fullPlayer.Inventory.GetQuantity("potion"), Is.EqualTo(1));

            var (emptyPlayer, emptyService) = CreateState(1, 50);
            emptyService.TryRegister(0, "potion");
            Assert.That((await emptyService.UseAsync(0)).Succeeded, Is.True);
            Assert.That((await emptyService.UseAsync(0)).Failure, Is.EqualTo(ItemUseFailure.OutOfStock));
            Assert.That(emptyPlayer.Inventory.GetQuantity("potion"), Is.Zero);
        }

        [Test]
        public async Task InventoryUse_DoesNotChangeQuickSlotAndSharesExecutionRules()
        {
            var (player, service) = CreateState(2, 50);
            var before = player.QuickItems.GetItemDefinitionId(0);
            var result = await service.UseItemAsync("potion");
            Assert.That(result.Succeeded, Is.True);
            Assert.That(player.QuickItems.GetItemDefinitionId(0), Is.EqualTo(before));
            Assert.That(player.Inventory.GetQuantity("potion"), Is.EqualTo(1));
        }

        [Test]
        public async Task TwoServicesForSamePlayer_ShareUseResultAndInventory()
        {
            var (player, first) = CreateState(2, 50);
            var item = created.OfType<ItemDefinition>().Single();
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>(); created.Add(catalog); catalog.ConfigureForEditor(new[] { item });
            var second = new QuickItemService(player, catalog);
            Assert.That((await first.UseItemAsync("potion")).Succeeded, Is.True);
            Assert.That((await second.UseItemAsync("potion")).Succeeded, Is.True);
            Assert.That(player.Inventory.GetQuantity("potion"), Is.Zero);
        }

        private (PlayerState, QuickItemService) CreateState(int quantity, int health)
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(characterDefinition);
            var serialized = new SerializedObject(characterDefinition);
            serialized.FindProperty("characterId").stringValue = "hero";
            var entries = serialized.FindProperty("baseStats"); entries.arraySize = 2;
            SetEntry(entries.GetArrayElementAtIndex(0), CoreStatIds.AttackPower, 10);
            SetEntry(entries.GetArrayElementAtIndex(1), CoreStatIds.MaxHealth, 100);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var character = new CharacterFactory(new[] { characterDefinition }).Create("hero", "hero-1");
            character.Health.ApplyDamage(100 - health);
            var inventory = new InventoryState(); inventory.Add("potion", quantity);
            var player = new PlayerState(new[] { character }, "hero-1", inventory, new QuickItemLoadout());
            var item = ScriptableObject.CreateInstance<ItemDefinition>(); created.Add(item);
            item.ConfigureForEditor("potion", "Potion", string.Empty, ItemEffectKind.Healing, 25);
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>(); created.Add(catalog); catalog.ConfigureForEditor(new[] { item });
            return (player, new QuickItemService(player, catalog));
        }
        private static void SetEntry(SerializedProperty entry, StatId statId, int value) { entry.FindPropertyRelative("statId").stringValue = statId.Value; entry.FindPropertyRelative("value").intValue = value; }
    }
}
