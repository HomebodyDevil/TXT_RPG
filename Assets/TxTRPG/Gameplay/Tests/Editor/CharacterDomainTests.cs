using System;
using System.Collections.Generic;
using NUnit.Framework;
using TxTRPG.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class CharacterDomainTests
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
        public void Definition_CreatesIndependentRuntimeStatsAndFullHealth()
        {
            var definition = CreateDefinition(
                "hero",
                new BaseStatEntry(CoreStatIds.AttackPower, 12),
                new BaseStatEntry(CoreStatIds.MaxHealth, 90));

            var first = definition.CreateRuntimeState();
            var second = definition.CreateRuntimeState();
            first.Health.ApplyDamage(30);

            Assert.That(first.Stats.AttackPower, Is.EqualTo(12));
            Assert.That(first.Stats.MaxHealth, Is.EqualTo(90));
            Assert.That(first.Health.Current, Is.EqualTo(60));
            Assert.That(second.Health.Current, Is.EqualTo(90));
        }

        [Test]
        public void StatBlock_RejectsNegativeAndDuplicateBaseStats()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatBlock(new[]
            {
                new BaseStatEntry(CoreStatIds.AttackPower, -1),
                new BaseStatEntry(CoreStatIds.MaxHealth, 100)
            }));

            Assert.Throws<ArgumentException>(() => new StatBlock(new[]
            {
                new BaseStatEntry(CoreStatIds.AttackPower, 10),
                new BaseStatEntry(CoreStatIds.AttackPower, 20),
                new BaseStatEntry(CoreStatIds.MaxHealth, 100)
            }));

            Assert.Throws<ArgumentOutOfRangeException>(() => new StatBlock(new[]
            {
                new BaseStatEntry(CoreStatIds.AttackPower, 10),
                new BaseStatEntry(CoreStatIds.MaxHealth, 0)
            }));
        }

        [Test]
        public void Health_ClampsDamageHealingAndRejectsNegativeAmounts()
        {
            var health = new HealthState(100);

            var damage = health.ApplyDamage(int.MaxValue);
            var healing = health.Heal(int.MaxValue);

            Assert.That(damage.CurrentHealth, Is.Zero);
            Assert.That(damage.AppliedAmount, Is.EqualTo(100));
            Assert.That(healing.CurrentHealth, Is.EqualTo(100));
            Assert.That(healing.AppliedAmount, Is.EqualTo(100));
            Assert.Throws<ArgumentOutOfRangeException>(() => health.ApplyDamage(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => health.Heal(-1));
        }

        [Test]
        public void Health_RaisesDefeatedOnlyOnFirstPositiveToZeroTransition()
        {
            var health = new HealthState(10);
            var defeatedCount = 0;
            health.Defeated += _ => defeatedCount++;

            health.ApplyDamage(10);
            health.ApplyDamage(10);

            Assert.That(defeatedCount, Is.EqualTo(1));
            Assert.That(health.IsDefeated, Is.True);
        }

        [Test]
        public void MaximumDecrease_ClampsCurrentWithoutHealingOnIncrease()
        {
            var health = new HealthState(100, 90);

            health.SetMaximum(80);
            Assert.That(health.Current, Is.EqualTo(80));

            health.SetMaximum(120);
            Assert.That(health.Current, Is.EqualTo(80));
        }

        [Test]
        public void PassthroughDamageResolver_ReturnsUnmodifiedDamage()
        {
            var resolver = new PassthroughDamageResolver();
            var result = resolver.Resolve(new DamageRequest("hero", "enemy", 17));

            Assert.That(result.RawDamage, Is.EqualTo(17));
            Assert.That(result.FinalDamage, Is.EqualTo(17));
            Assert.That(result.PreventedDamage, Is.Zero);
            Assert.That(result.AmplifiedDamage, Is.Zero);

            var amplified = new DamageResult(10, 15);
            Assert.That(amplified.PreventedDamage, Is.Zero);
            Assert.That(amplified.AmplifiedDamage, Is.EqualTo(5));
        }

        [Test]
        public void SaveRoundTrip_RestoresKnownBonusesAndIgnoresUnknownStats()
        {
            var definition = CreateDefinition(
                "hero",
                new BaseStatEntry(CoreStatIds.AttackPower, 12),
                new BaseStatEntry(CoreStatIds.MaxHealth, 100));
            var factory = new CharacterFactory(new[] { definition });
            var restored = factory.Restore(new CharacterSaveData
            {
                version = CharacterSaveData.CurrentVersion,
                characterId = "hero",
                characterDefinitionId = "hero",
                characterInstanceId = "hero-0",
                currentHealth = 70,
                permanentStatBonuses = new List<SavedStatValue>
                {
                    new(CoreStatIds.AttackPower, 3),
                    new(new StatId("removed.unknown_stat"), 99)
                }
            });
            restored.Health.ApplyDamage(5);

            var json = CharacterSaveSerializer.Serialize(restored);
            var roundTrip = factory.Restore(CharacterSaveSerializer.Deserialize(json));

            Assert.That(restored.Stats.IgnoredBonusStatIds, Has.Count.EqualTo(1));
            Assert.That(roundTrip.Stats.AttackPower, Is.EqualTo(15));
            Assert.That(roundTrip.Health.Current, Is.EqualTo(65));
            Assert.That(roundTrip.Stats.IgnoredBonusStatIds, Is.Empty);
        }

        [Test]
        public void VersionOneSave_MigratesAndClampsCorruptedHealth()
        {
            var definition = CreateDefinition(
                "hero",
                new BaseStatEntry(CoreStatIds.AttackPower, 10),
                new BaseStatEntry(CoreStatIds.MaxHealth, 80));
            var factory = new CharacterFactory(new[] { definition });
            var oldSave = new CharacterSaveData
            {
                version = 1,
                characterId = "hero",
                currentHealth = 999,
                permanentStatBonuses = null
            };

            var migrated = CharacterSaveMigrator.Migrate(oldSave);
            var state = factory.Restore(migrated);

            Assert.That(migrated.version, Is.EqualTo(CharacterSaveData.CurrentVersion));
            Assert.That(migrated.permanentStatBonuses, Is.Empty);
            Assert.That(state.Health.Current, Is.EqualTo(80));

            oldSave.currentHealth = -999;
            Assert.That(factory.Restore(oldSave).Health.Current, Is.Zero);

            var legacyCallerData = new CharacterSaveData
            {
                characterId = "hero",
                currentHealth = 20
            };
            var legacyCallerState = factory.Restore(legacyCallerData);
            Assert.That(legacyCallerState.CharacterInstanceId,
                Is.EqualTo(CharacterSaveMigrator.LegacyCharacterInstanceId));
        }

        [Test]
        public void Restore_RejectsMissingCharacterDefinition()
        {
            var factory = new CharacterFactory(Array.Empty<CharacterDefinition>());
            var save = new CharacterSaveData
            {
                characterId = "missing.character",
                currentHealth = 10
            };

            Assert.Throws<KeyNotFoundException>(() => factory.Restore(save));
        }

        private CharacterDefinition CreateDefinition(
            string characterId,
            params BaseStatEntry[] baseStats)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definitions.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("characterId").stringValue = characterId;
            var entries = serialized.FindProperty("baseStats");
            entries.arraySize = baseStats.Length;
            for (var index = 0; index < baseStats.Length; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("statId").stringValue = baseStats[index].StatId.Value;
                entry.FindPropertyRelative("value").intValue = baseStats[index].Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
