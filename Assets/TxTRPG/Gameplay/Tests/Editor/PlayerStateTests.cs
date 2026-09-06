using System;
using System.Collections.Generic;
using NUnit.Framework;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Players;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class PlayerStateTests
    {
        private readonly List<CharacterDefinition> definitions = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var definition in definitions)
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
            definitions.Clear();
        }

        [Test]
        public void SameDefinition_CreatesIndependentCharacterInstances()
        {
            var factory = CreateCharacterFactory("hero", 100);
            var first = factory.Create("hero", "owned-1");
            var second = factory.Create("hero", "owned-2");

            first.Health.ApplyDamage(40);

            Assert.That(first.CharacterDefinitionId, Is.EqualTo("hero"));
            Assert.That(first.CharacterInstanceId, Is.EqualTo("owned-1"));
            Assert.That(first.Health.Current, Is.EqualTo(60));
            Assert.That(second.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void Constructor_RejectsDuplicateInstanceIdsAndInvalidActiveId()
        {
            var factory = CreateCharacterFactory("hero", 100);
            var first = factory.Create("hero", "duplicate");
            var second = factory.Create("hero", "duplicate");

            Assert.Throws<ArgumentException>(() =>
                new PlayerState(new[] { first, second }, "duplicate"));
            Assert.Throws<ArgumentException>(() =>
                new PlayerState(new[] { first }, "not-owned"));
        }

        [Test]
        public void ActiveCharacterChangeAndRemoval_PreservePlayerInvariants()
        {
            var factory = CreateCharacterFactory("hero", 100);
            var first = factory.Create("hero", "owned-1");
            var second = factory.Create("hero", "owned-2");
            var player = new PlayerState(new[] { first, second }, "owned-1");
            var activeChangeCount = 0;
            player.ActiveCharacterChanged += (_, _) => activeChangeCount++;

            Assert.That(player.TrySetActiveCharacter("owned-2"), Is.True);
            Assert.That(player.RemoveCharacter("owned-2"), Is.True);
            Assert.That(player.ActiveCharacterInstanceId, Is.EqualTo("owned-1"));
            Assert.That(activeChangeCount, Is.EqualTo(2));
            Assert.That(player.RemoveCharacter("owned-1"), Is.False);
            Assert.That(player.TrySetActiveCharacter("missing"), Is.False);
        }

        [Test]
        public void MultipleCharacters_SaveAndRestoreRoundTrip()
        {
            var characterFactory = CreateCharacterFactory("hero", 100);
            var playerFactory = new PlayerFactory(characterFactory);
            var first = characterFactory.Create("hero", "owned-1");
            var second = characterFactory.Create("hero", "owned-2");
            first.Health.ApplyDamage(25);
            second.Health.ApplyDamage(60);
            var source = new PlayerState(new[] { first, second }, "owned-2");

            var json = PlayerSaveSerializer.Serialize(source);
            var restored = playerFactory.Restore(PlayerSaveSerializer.Deserialize(json));

            Assert.That(restored.Characters, Has.Count.EqualTo(2));
            Assert.That(restored.ActiveCharacterInstanceId, Is.EqualTo("owned-2"));
            Assert.That(restored.TryGetCharacter("owned-1", out var restoredFirst), Is.True);
            Assert.That(restored.TryGetCharacter("owned-2", out var restoredSecond), Is.True);
            Assert.That(restoredFirst.Health.Current, Is.EqualTo(75));
            Assert.That(restoredSecond.Health.Current, Is.EqualTo(40));
        }

        [Test]
        public void LegacyCharacterSave_UsesDeterministicInstanceId()
        {
            var playerFactory = new PlayerFactory(CreateCharacterFactory("hero", 100));
            var legacy = new CharacterSaveData
            {
                version = 2,
                characterId = "hero",
                currentHealth = 70,
                permanentStatBonuses = new List<SavedStatValue>()
            };

            var firstMigration = PlayerSaveMigrator.FromLegacyCharacter(legacy);
            var secondMigration = PlayerSaveMigrator.FromLegacyCharacter(legacy);
            var restored = playerFactory.Restore(firstMigration);

            Assert.That(firstMigration.activeCharacterInstanceId,
                Is.EqualTo(CharacterSaveMigrator.LegacyCharacterInstanceId));
            Assert.That(secondMigration.activeCharacterInstanceId,
                Is.EqualTo(firstMigration.activeCharacterInstanceId));
            Assert.That(restored.ActiveCharacter.CharacterDefinitionId, Is.EqualTo("hero"));
            Assert.That(restored.ActiveCharacter.Health.Current, Is.EqualTo(70));
        }

        [Test]
        public void Restore_RejectsMissingDefinitionAndClampsCorruptedHealth()
        {
            var characterFactory = CreateCharacterFactory("hero", 80);
            var playerFactory = new PlayerFactory(characterFactory);
            var missingDefinition = CreatePlayerSave("missing", "missing-1", 10);

            Assert.Throws<KeyNotFoundException>(() =>
                playerFactory.Restore(missingDefinition));

            var tooHigh = playerFactory.Restore(CreatePlayerSave("hero", "hero-1", 999));
            var negative = playerFactory.Restore(CreatePlayerSave("hero", "hero-2", -999));
            Assert.That(tooHigh.ActiveCharacter.Health.Current, Is.EqualTo(80));
            Assert.That(negative.ActiveCharacter.Health.Current, Is.Zero);
        }

        private CharacterFactory CreateCharacterFactory(string id, int maxHealth)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definitions.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("characterId").stringValue = id;
            var entries = serialized.FindProperty("baseStats");
            entries.arraySize = 2;
            SetEntry(entries.GetArrayElementAtIndex(0), CoreStatIds.AttackPower, 10);
            SetEntry(entries.GetArrayElementAtIndex(1), CoreStatIds.MaxHealth, maxHealth);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return new CharacterFactory(new[] { definition });
        }

        private static PlayerSaveData CreatePlayerSave(
            string definitionId,
            string instanceId,
            int currentHealth)
        {
            return new PlayerSaveData
            {
                activeCharacterInstanceId = instanceId,
                characters = new List<CharacterSaveData>
                {
                    new()
                    {
                        characterDefinitionId = definitionId,
                        characterInstanceId = instanceId,
                        currentHealth = currentHealth
                    }
                }
            };
        }

        private static void SetEntry(
            SerializedProperty entry,
            StatId statId,
            int value)
        {
            entry.FindPropertyRelative("statId").stringValue = statId.Value;
            entry.FindPropertyRelative("value").intValue = value;
        }
    }
}
