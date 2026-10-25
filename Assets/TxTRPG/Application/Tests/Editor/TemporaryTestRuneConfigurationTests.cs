using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Runes;
using TxTRPG.Gameplay.Runes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Tests
{
    public sealed class TemporaryTestRuneConfigurationTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        private TemporaryTestRuneConfiguration Tests(params TemporaryRuneDefinitionEntry[] entries)
        {
            var configuration = Track(ScriptableObject.CreateInstance<TemporaryTestRuneConfiguration>());
            configuration.ConfigureForEditor(entries);
            return configuration;
        }

        [Test]
        public void Defaults_AreTheThreeDocumentedTestRunes()
        {
            var configuration = Track(ScriptableObject.CreateInstance<TemporaryTestRuneConfiguration>());
            Assert.That(configuration.Validate(out var reason), Is.True, reason);
            Assert.That(configuration.Definitions.Select(d => d.Id), Is.EqualTo(new[] { "rune.test.tooltip", "rune.test.long", "rune.test.missing" }));
            Assert.That(configuration.Definitions.All(d => d.EffectKind == RuneEffectKind.Test), Is.True);
        }

        [Test]
        public void Validate_RejectsEmptyDuplicatePrefixAndEffect()
        {
            Assert.That(Tests(new TemporaryRuneDefinitionEntry(" ", "a", RuneEffectKind.Test)).Validate(out var reason), Is.False);
            Assert.That(reason, Does.Contain("empty"));
            Assert.That(Tests(new TemporaryRuneDefinitionEntry("rune.test.a", "a", RuneEffectKind.Test),
                new TemporaryRuneDefinitionEntry("rune.test.a", "b", RuneEffectKind.Test)).Validate(out reason), Is.False);
            Assert.That(reason, Does.Contain("duplicated"));
            Assert.That(Tests(new TemporaryRuneDefinitionEntry("rune.other", "a", RuneEffectKind.Test)).Validate(out reason), Is.False);
            Assert.That(reason, Does.Contain("rune.test."));
            Assert.That(Tests(new TemporaryRuneDefinitionEntry("rune.test.a", "a", RuneEffectKind.Attack)).Validate(out reason), Is.False);
            Assert.That(reason, Does.Contain("Test"));
            Assert.That(Tests().Validate(out reason), Is.True, "an empty configuration is valid");
        }

        [Test]
        public void TryCreate_MergesTestDefinitionsWithoutOwningThem()
        {
            var runes = Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>());
            var tests = Track(ScriptableObject.CreateInstance<TemporaryTestRuneConfiguration>());
            Assert.That(TemporaryRuneState.TryCreate(runes, tests, out var state, out var reason, out var testReason), Is.True, reason);
            Assert.That(testReason, Is.Empty);
            Assert.That(state.TestDefinitionIds, Is.EqualTo(new[] { "rune.test.tooltip", "rune.test.long", "rune.test.missing" }));
            Assert.That(state.Catalog.Contains("rune.test.long"), Is.True);
            Assert.That(state.Inventory.Count, Is.EqualTo(2), "test runes are never owned at the start");
            Assert.That(state.Loadout.LineCount, Is.EqualTo(2));
            Assert.That(TemporaryRuneState.TryCreate(runes, out var plain, out reason), Is.True, reason);
            Assert.That(plain.TestDefinitionIds, Is.Empty);
        }

        [Test]
        public void TryCreate_LeavesOutAnInvalidOrCollidingTestConfiguration()
        {
            var runes = Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>());
            var invalid = Tests(new TemporaryRuneDefinitionEntry("rune.test.a", "a", RuneEffectKind.Heal));
            Assert.That(TemporaryRuneState.TryCreate(runes, invalid, out var state, out var reason, out var testReason), Is.True, reason);
            Assert.That(testReason, Is.Not.Empty);
            Assert.That(state.TestDefinitionIds, Is.Empty);
            Assert.That(state.Catalog.Definitions.Count, Is.EqualTo(2));

            var regularTest = Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>());
            regularTest.ConfigureForEditor(new[]
            {
                new TemporaryRuneDefinitionEntry(TemporaryRuneConfiguration.AttackRuneId, "공격 룬", RuneEffectKind.Attack),
                new TemporaryRuneDefinitionEntry("rune.test.a", "일반으로 등록된 테스트", RuneEffectKind.Test)
            }, new[] { new TemporaryOwnedRuneEntry("a.1", TemporaryRuneConfiguration.AttackRuneId) }, new[] { new TemporaryRuneLineEntry("line.1", "a.1") });
            var colliding = Tests(new TemporaryRuneDefinitionEntry("rune.test.a", "a", RuneEffectKind.Test));
            Assert.That(TemporaryRuneState.TryCreate(regularTest, colliding, out state, out reason, out testReason), Is.True, reason);
            Assert.That(testReason, Does.Contain("rune.test.a"));
            Assert.That(state.TestDefinitionIds, Is.Empty);
        }

        [Test]
        public void CanAllocate_IgnoresTestLines()
        {
            var runes = Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>());
            runes.ConfigureForEditor(new[] { new TemporaryRuneDefinitionEntry(TemporaryRuneConfiguration.AttackRuneId, "공격 룬", RuneEffectKind.Attack) },
                new TemporaryOwnedRuneEntry[0], new TemporaryRuneLineEntry[0]);
            var tests = Track(ScriptableObject.CreateInstance<TemporaryTestRuneConfiguration>());
            Assert.That(TemporaryRuneState.TryCreate(runes, tests, out var state, out var reason, out _), Is.True, reason);
            Assert.That(state.Inventory.TryAdd(new OwnedRune("t.1", "rune.test.tooltip"), out reason), Is.True, reason);
            Assert.That(state.Loadout.TryAddLine(new RuneLoadoutLine("line.test.tooltip", new[] { "t.1" }), out reason), Is.True, reason);
            Assert.That(state.Loadout.TotalWeight, Is.EqualTo(1));
            Assert.That(state.CanAllocate, Is.False, "test lines alone cannot take an action");
            Assert.That(state.Inventory.TryAdd(new OwnedRune("a.1", TemporaryRuneConfiguration.AttackRuneId), out reason), Is.True, reason);
            Assert.That(state.Loadout.TryAddLine(new RuneLoadoutLine("line.1", new[] { "a.1" }), out reason), Is.True, reason);
            Assert.That(state.CanAllocate, Is.True);
        }
    }
}
