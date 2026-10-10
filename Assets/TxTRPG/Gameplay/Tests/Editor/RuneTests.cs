using System;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Dice;
using TxTRPG.Gameplay.Runes;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class RuneTests
    {
        private RuneDefinitionCatalog catalog;
        private RuneInventory inventory;

        [SetUp]
        public void SetUp()
        {
            catalog = new RuneDefinitionCatalog(new[]
            {
                new RuneDefinition("rune.attack", "공격 룬", RuneEffectKind.Attack),
                new RuneDefinition("rune.heal", "회복 룬", RuneEffectKind.Heal)
            });
            inventory = new RuneInventory(catalog);
            foreach (var id in new[] { "a1", "a2", "a3", "a4", "a5" }) Add(id, "rune.attack");
            foreach (var id in new[] { "h1", "h2", "h3" }) Add(id, "rune.heal");
        }

        private void Add(string id, string definition) =>
            Assert.That(inventory.TryAdd(new OwnedRune(id, definition), out var reason), Is.True, reason);

        private RuneLoadout Loadout(params (string Line, string[] Runes)[] lines) =>
            new(inventory, lines.Select(line => new RuneLoadoutLine(line.Line, line.Runes)));

        private static int[] Amounts(RuneAllocation allocation) => allocation.Lines.Select(line => line.Amount).ToArray();

        [Test]
        public void Definitions_RejectUnknownEffectsAndDuplicateIds()
        {
            Assert.Throws<ArgumentException>(() => new RuneDefinition("rune.unknown", "?", RuneEffectKind.Unknown));
            Assert.Throws<ArgumentException>(() => new RuneDefinition("rune.future", "?", (RuneEffectKind)99));
            Assert.Throws<ArgumentException>(() => new RuneDefinitionCatalog(new[]
            {
                new RuneDefinition("rune.attack", "a", RuneEffectKind.Attack),
                new RuneDefinition("rune.attack", "b", RuneEffectKind.Heal)
            }));
            Assert.That((int)RuneEffectKind.Unknown, Is.Zero);
            Assert.That((int)RuneEffectKind.Attack, Is.EqualTo(1));
            Assert.That((int)RuneEffectKind.Heal, Is.EqualTo(2));
        }

        [Test]
        public void Inventory_RejectsDuplicatesAndUnknownDefinitions()
        {
            Assert.That(inventory.TryAdd(new OwnedRune("a1", "rune.attack"), out _), Is.False);
            Assert.That(inventory.TryAdd(new OwnedRune("x1", "rune.missing"), out _), Is.False);
            Assert.That(inventory.TryAdd(default, out _), Is.False);
            Assert.That(inventory.Count, Is.EqualTo(8));
        }

        [Test]
        public void Loadout_RejectsInvalidLinesAndKeepsThePreviousLines()
        {
            var loadout = Loadout(("line.1", new[] { "a1" }));
            var changes = 0;
            loadout.Changed += () => changes++;

            Assert.That(loadout.TryAddLine(new RuneLoadoutLine("line.2", new[] { "a2", "h1" }), out var reason), Is.False, "mixed kinds");
            Assert.That(reason, Does.Contain("mixes"));
            Assert.That(loadout.TryAddLine(new RuneLoadoutLine("line.2", new[] { "a1" }), out _), Is.False, "one instance on two lines");
            Assert.That(loadout.TryAddLine(new RuneLoadoutLine("line.2", new[] { "a2", "a2" }), out _), Is.False, "one instance twice");
            Assert.That(loadout.TryAddLine(new RuneLoadoutLine("line.2", new[] { "missing" }), out _), Is.False, "not owned");
            Assert.That(loadout.TryAddLine(new RuneLoadoutLine("line.2", Array.Empty<string>()), out _), Is.False, "empty line");
            Assert.That(loadout.TryAddLine(new RuneLoadoutLine("line.1", new[] { "a2" }), out _), Is.False, "duplicate line id");
            Assert.That(loadout.TryAddRuneToLine("line.1", "h1", out _), Is.False, "stacking a different kind");
            Assert.That(changes, Is.Zero);
            Assert.That(loadout.Lines.Single().RuneInstanceIds, Is.EqualTo(new[] { "a1" }));
            Assert.Throws<ArgumentException>(() => new RuneLoadout(inventory), "an inventory has one loadout");
        }

        [Test]
        public void Loadout_StacksAndSplitsSameKindRunes()
        {
            var loadout = Loadout(("line.1", new[] { "a1" }), ("line.2", new[] { "h1" }));
            Assert.That(loadout.TryAddRuneToLine("line.1", "a2", out var reason), Is.True, reason);
            Assert.That(loadout.TryAddLine(new RuneLoadoutLine("line.3", new[] { "a3" }), out reason), Is.True, reason);
            Assert.That(loadout.Lines.Select(line => line.Weight), Is.EqualTo(new[] { 2, 1, 1 }));
            Assert.That(loadout.TotalWeight, Is.EqualTo(4));
            Assert.That(loadout.TryRemoveRuneFromLine("line.3", "a3", out reason), Is.True, reason);
            Assert.That(loadout.Lines.Select(line => line.LineId), Is.EqualTo(new[] { "line.1", "line.2" }), "removing the last rune removes the line");
        }

        [Test]
        public void Inventory_RefusesToRemoveAPlacedRune()
        {
            var loadout = Loadout(("line.1", new[] { "a1", "a2" }));
            Assert.That(inventory.TryRemove("a1", out var reason), Is.False);
            Assert.That(reason, Does.Contain("line.1"));
            Assert.That(inventory.TryRemove("a3", out reason), Is.True, "an unplaced rune can be removed");
            Assert.That(loadout.TryRemoveRuneFromLine("line.1", "a1", out reason), Is.True, reason);
            Assert.That(inventory.TryRemove("a1", out reason), Is.True, reason);
        }

        [TestCase(10, new[] { 4, 6 })]
        public void Allocation_HealTwoAttackThree(int total, int[] expected)
        {
            var loadout = Loadout(("line.1", new[] { "h1", "h2" }), ("line.2", new[] { "a1", "a2", "a3" }));
            var allocation = WeightedRuneAllocationPolicy.Instance.Allocate(total, loadout, catalog);
            Assert.That(Amounts(allocation), Is.EqualTo(expected));
            Assert.That(allocation.Lines.Select(l => l.EffectKind), Is.EqualTo(new[] { RuneEffectKind.Heal, RuneEffectKind.Attack }));
            Assert.That(allocation.Lines.Select(l => l.Weight), Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        public void Allocation_DocumentedCases()
        {
            var even = Loadout(("line.1", new[] { "a1" }), ("line.2", new[] { "a2" }), ("line.3", new[] { "h1" }));
            Assert.That(Amounts(WeightedRuneAllocationPolicy.Instance.Allocate(10, even, catalog)), Is.EqualTo(new[] { 4, 3, 3 }));
            Assert.That(even.TryReplaceAll(new[]
            {
                new RuneLoadoutLine("line.1", new[] { "a1" }), new RuneLoadoutLine("line.2", new[] { "h1" }),
                new RuneLoadoutLine("line.3", new[] { "a2" }), new RuneLoadoutLine("line.4", new[] { "h2" }),
                new RuneLoadoutLine("line.5", new[] { "a3" })
            }, out var reason), Is.True, reason);
            Assert.That(Amounts(WeightedRuneAllocationPolicy.Instance.Allocate(3, even, catalog)), Is.EqualTo(new[] { 1, 1, 1, 0, 0 }));
            Assert.That(even.TryReplaceAll(new[]
            {
                new RuneLoadoutLine("line.1", new[] { "a1", "a2" }), new RuneLoadoutLine("line.2", new[] { "h1" })
            }, out reason), Is.True, reason);
            Assert.That(Amounts(WeightedRuneAllocationPolicy.Instance.Allocate(0, even, catalog)), Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public void Allocation_PreservesTheTotalAndBreaksTiesByLineOrder()
        {
            var loadout = Loadout(("line.1", new[] { "a1" }), ("line.2", new[] { "h1", "h2" }), ("line.3", new[] { "a2", "a3", "a4" }));
            for (var total = 0; total <= 200; total++)
            {
                var allocation = WeightedRuneAllocationPolicy.Instance.Allocate(total, loadout, catalog);
                Assert.That(allocation.Lines.Sum(line => line.Amount), Is.EqualTo(total));
                Assert.That(allocation.Total, Is.EqualTo(total));
                Assert.That(Amounts(WeightedRuneAllocationPolicy.Instance.Allocate(total, loadout, catalog)), Is.EqualTo(Amounts(allocation)), "deterministic");
            }
            // 1 * 1 / 6, 1 * 2 / 6, 1 * 3 / 6: the largest remainder (line.3) gets the unit.
            Assert.That(Amounts(WeightedRuneAllocationPolicy.Instance.Allocate(1, loadout, catalog)), Is.EqualTo(new[] { 0, 0, 1 }));
            var tie = Loadout2(("line.a", new[] { "a1" }), ("line.b", new[] { "a2" }));
            Assert.That(Amounts(WeightedRuneAllocationPolicy.Instance.Allocate(1, tie, catalog)), Is.EqualTo(new[] { 1, 0 }), "earlier line first on a tie");
        }

        private RuneLoadout Loadout2(params (string Line, string[] Runes)[] lines)
        {
            var separate = new RuneInventory(catalog);
            foreach (var line in lines)
                foreach (var rune in line.Runes)
                    separate.TryAdd(new OwnedRune(rune, "rune.attack"), out _);
            return new RuneLoadout(separate, lines.Select(line => new RuneLoadoutLine(line.Line, line.Runes)));
        }

        [Test]
        public void Allocation_HandlesLargeTotalsWithoutOverflow()
        {
            var loadout = Loadout(("line.1", new[] { "a1", "a2", "a3" }), ("line.2", new[] { "h1" }));
            var allocation = WeightedRuneAllocationPolicy.Instance.Allocate(int.MaxValue, loadout, catalog);
            Assert.That(allocation.Lines.Sum(line => (long)line.Amount), Is.EqualTo((long)int.MaxValue));
            Assert.That(allocation.Lines[0].Amount, Is.EqualTo(1610612735));
        }

        [Test]
        public void Allocation_RequiresWeight()
        {
            var empty = Loadout();
            Assert.Throws<InvalidOperationException>(() => WeightedRuneAllocationPolicy.Instance.Allocate(5, empty, catalog));
            Assert.Throws<ArgumentOutOfRangeException>(() => WeightedRuneAllocationPolicy.Instance.Allocate(-1, empty, catalog));
        }

        [Test]
        public void Combat_AppliesPlayerActionsInOrderWithSourceIds()
        {
            var combat = new TemporaryCombatState(20, 10, 40, 5, 3);
            var result = combat.ExecuteTurn(new[]
            {
                new PlayerCombatAction(PlayerCombatActionKind.Heal, 4, "line.1"),
                new PlayerCombatAction(PlayerCombatActionKind.Attack, 6, "line.2")
            });
            Assert.That(result.Events.Select(e => e.Kind), Is.EqualTo(new[]
            {
                TemporaryCombatEventKind.PlayerHeal, TemporaryCombatEventKind.PlayerAttack, TemporaryCombatEventKind.EnemyAttack
            }));
            Assert.That(result.Events.Select(e => e.SourceId), Is.EqualTo(new[] { "line.1", "line.2", string.Empty }));
            Assert.That(combat.PlayerHealth.Current, Is.EqualTo(9));
            Assert.That(combat.EnemyHealth.Current, Is.EqualTo(34));
            Assert.That(new TemporaryCombatEvent(TemporaryCombatEventKind.Victory, 0, 0).SourceId, Is.Empty);
            Assert.That(default(TemporaryCombatEvent).SourceId, Is.Empty);
        }

        [Test]
        public void Combat_DiceOverloadMatchesEquivalentActions()
        {
            var faces = new[] { new DiceFace(DiceEffectKind.Attack, 3), new DiceFace(DiceEffectKind.Attack, 5), new DiceFace(DiceEffectKind.Heal, 4) };
            var dice = new TemporaryCombatState(20, 10, 3, 5, 3);
            var actions = new TemporaryCombatState(20, 10, 3, 5, 3);
            var diceResult = dice.ExecuteTurn(faces.Select((face, i) => new DiceRollResult(i, face)).ToArray());
            var actionResult = actions.ExecuteTurn(new[]
            {
                new PlayerCombatAction(PlayerCombatActionKind.Attack, 3), new PlayerCombatAction(PlayerCombatActionKind.Attack, 5),
                new PlayerCombatAction(PlayerCombatActionKind.Heal, 4)
            });
            Assert.That(diceResult.Events.Select(e => (e.Kind, e.RequestedAmount, e.AppliedAmount, e.CurrentHealth)),
                Is.EqualTo(actionResult.Events.Select(e => (e.Kind, e.RequestedAmount, e.AppliedAmount, e.CurrentHealth))));
            Assert.That(diceResult.Events[1].Kind, Is.EqualTo(TemporaryCombatEventKind.PlayerAttackSkipped));
            Assert.That(dice.PlayerHealth.Current, Is.EqualTo(actions.PlayerHealth.Current));
        }

        [Test]
        public void Combat_UnsupportedDiceEffectIsRejectedBeforeAnyChange()
        {
            var combat = new TemporaryCombatState(20, 10, 40, 5, 3);
            Assert.Throws<InvalidOperationException>(() => combat.ExecuteTurn(new[]
            {
                new DiceRollResult(0, new DiceFace(DiceEffectKind.Attack, 3)),
                new DiceRollResult(1, new DiceFace(DiceEffectKind.Unknown, 1))
            }));
            Assert.That(combat.EnemyHealth.Current, Is.EqualTo(40));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerCombatAction((PlayerCombatActionKind)0, 1));
        }
    }
}
