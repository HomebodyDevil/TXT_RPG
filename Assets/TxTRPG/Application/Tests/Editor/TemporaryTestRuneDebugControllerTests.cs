using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Runes;
using TxTRPG.Gameplay.Runes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Tests
{
    public sealed class TemporaryTestRuneDebugControllerTests
    {
        private readonly List<Object> created = new();
        private TemporaryRuneState runes;
        private TemporaryTestRuneDebugController controller;

        [SetUp]
        public void SetUp()
        {
            var configuration = Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>());
            var tests = Track(ScriptableObject.CreateInstance<TemporaryTestRuneConfiguration>());
            Assert.That(TemporaryRuneState.TryCreate(configuration, tests, out runes, out var reason, out _), Is.True, reason);
            var gameObject = Track(new GameObject("Controller"));
            controller = gameObject.AddComponent<TemporaryTestRuneDebugController>();
            controller.Connect(runes, runes.TestDefinitionIds);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        private (string, int)[] Lines() => runes.Loadout.Lines.Select(l => (l.LineId, l.Weight)).ToArray();

        [Test]
        public void Add_CreatesALineAtTheEndAndThenStacks()
        {
            Assert.That(controller.TryAdd("rune.test.tooltip", out var message), Is.True, message);
            Assert.That(Lines(), Is.EqualTo(new[] { ("line.1", 1), ("line.2", 1), ("line.test.tooltip", 1) }));
            Assert.That(runes.Inventory.Contains("rune.test.tooltip.debug.1"), Is.True);
            Assert.That(controller.TryAdd("rune.test.tooltip", out message), Is.True, message);
            Assert.That(controller.TryAdd("rune.test.long", out message), Is.True, message);
            Assert.That(Lines(), Is.EqualTo(new[] { ("line.1", 1), ("line.2", 1), ("line.test.tooltip", 2), ("line.test.long", 1) }));
            Assert.That(controller.CountOf("rune.test.tooltip"), Is.EqualTo(2));
        }

        [Test]
        public void Remove_TakesTheNewestAndRemovesTheLastLine()
        {
            controller.TryAdd("rune.test.tooltip", out _);
            controller.TryAdd("rune.test.tooltip", out _);
            Assert.That(controller.TryRemove("rune.test.tooltip", out var message), Is.True, message);
            Assert.That(runes.Inventory.Contains("rune.test.tooltip.debug.2"), Is.False, "the newest instance goes first");
            Assert.That(runes.Inventory.Contains("rune.test.tooltip.debug.1"), Is.True);
            Assert.That(controller.TryRemove("rune.test.tooltip", out message), Is.True, message);
            Assert.That(Lines(), Is.EqualTo(new[] { ("line.1", 1), ("line.2", 1) }));
            Assert.That(controller.TryRemove("rune.test.tooltip", out message), Is.False);
            Assert.That(message, Is.Not.Empty);
        }

        [Test]
        public void RemoveAll_NeverChangesRegularRunes()
        {
            Assert.That(runes.Inventory.TryAdd(new OwnedRune("rune.attack.2", TemporaryRuneConfiguration.AttackRuneId), out var reason), Is.True, reason);
            Assert.That(runes.Loadout.TryAddRuneToLine("line.1", "rune.attack.2", out reason), Is.True, reason);
            foreach (var id in runes.TestDefinitionIds) { controller.TryAdd(id, out _); controller.TryAdd(id, out _); }
            Assert.That(runes.Loadout.LineCount, Is.EqualTo(5));
            Assert.That(controller.TryRemoveAll(out var message), Is.True, message);
            Assert.That(Lines(), Is.EqualTo(new[] { ("line.1", 2), ("line.2", 1) }));
            Assert.That(runes.Inventory.Count, Is.EqualTo(3));
        }

        [Test]
        public void Failures_ChangeNothing()
        {
            var before = Lines();
            Assert.That(controller.TryAdd(TemporaryRuneConfiguration.AttackRuneId, out var message), Is.False, "only test definitions");
            Assert.That(controller.TryAdd("rune.test.unknown", out message), Is.False);
            Assert.That(controller.TryRemove(TemporaryRuneConfiguration.AttackRuneId, out message), Is.False);
            Assert.That(Lines(), Is.EqualTo(before));
            Assert.That(runes.Inventory.Count, Is.EqualTo(2));

            // A line ID taken by another definition: the new line gets a suffix instead of failing.
            Assert.That(runes.Inventory.TryAdd(new OwnedRune("rune.attack.2", TemporaryRuneConfiguration.AttackRuneId), out var reason), Is.True, reason);
            Assert.That(runes.Loadout.TryAddLine(new RuneLoadoutLine("line.test.long", new[] { "rune.attack.2" }), out reason), Is.True, reason);
            Assert.That(controller.TryAdd("rune.test.long", out message), Is.True, message);
            Assert.That(runes.Loadout.Lines.Last().LineId, Is.EqualTo("line.test.long.2"));

            controller.Connect(null, null);
            Assert.That(controller.TryAdd("rune.test.long", out message), Is.False);
            Assert.That(message, Is.EqualTo(TemporaryTestRuneDebugController.NoSessionMessage));
        }
    }
}
