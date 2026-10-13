using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Exploration.Events;
using TxTRPG.Application.Runes;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration.Events;
using TxTRPG.Gameplay.Runes;
using TxTRPG.Gameplay.SlotMachine;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class ExplorationEventEffectTests
    {
        private readonly List<Object> created = new();
        private HealthState health;
        private TemporarySlotMachineState slots;
        private TemporaryRuneState runes;
        private TemporaryFortuneCriticalState fortune;
        private ExplorationEventContext context;

        [SetUp]
        public void SetUp()
        {
            health = new HealthState(30, 20);
            Assert.That(TemporarySlotMachineState.TryCreate(Track(ScriptableObject.CreateInstance<TemporarySlotMachineConfiguration>()), out slots, out var reason), Is.True, reason);
            Assert.That(TemporaryRuneState.TryCreate(Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>()), out runes, out reason), Is.True, reason);
            fortune = new TemporaryFortuneCriticalState(FortuneCriticalRules.Disabled);
            context = new ExplorationEventContext(health, slots, runes, fortune);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        private static ExplorationEventEffectResult Apply(IExplorationEventEffect effect, IExplorationEventContext target)
        {
            Assert.That(effect.Validate(out var reason), Is.True, reason);
            return effect.Apply(target);
        }

        // ---------- Health ----------

        [Test]
        public void ChangeHealth_HealsAndDamagesWithinRange()
        {
            Assert.That(Apply(new ChangeHealthEffect(15), context).Message, Does.Contain("+10").And.Contain("30/30"));
            Assert.That(Apply(new ChangeHealthEffect(-7), context).Message, Does.Contain("-7").And.Contain("23/30"));
            Apply(new ChangeHealthEffect(-99), context);
            Assert.That(health.Current, Is.Zero);
            Assert.That(new ChangeHealthEffect(0).Validate(out _), Is.False);
        }

        [Test]
        public void RestoreHealth_FillsToMaximum()
        {
            Apply(new RestoreHealthEffect(), context);
            Assert.That(health.Current, Is.EqualTo(30));
        }

        [Test]
        public void ChangeMaximumHealth_RaisesBothOrClipsAndNeverReachesZero()
        {
            Apply(new ChangeMaximumHealthEffect(5), context);
            Assert.That((health.Current, health.Maximum), Is.EqualTo((25, 35)));
            Apply(new ChangeMaximumHealthEffect(5, false), context);
            Assert.That((health.Current, health.Maximum), Is.EqualTo((25, 40)));
            Apply(new ChangeMaximumHealthEffect(-30), context);
            Assert.That((health.Current, health.Maximum), Is.EqualTo((10, 10)));
            Apply(new ChangeMaximumHealthEffect(-100), context);
            Assert.That((health.Current, health.Maximum), Is.EqualTo((1, 1)), "the maximum stays at least 1, so the current value never becomes 0");
        }

        // ---------- Reels ----------

        [Test]
        public void AddAndRemoveReelSymbols()
        {
            var first = new ExplorationReelSelector(ExplorationReelSelectorMode.First);
            Apply(new AddReelSymbolEffect(first, 7, 2), context);
            Assert.That(Values("reel.1"), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 7 }));
            Apply(new RemoveReelSymbolEffect(new ExplorationReelSelector(ExplorationReelSelectorMode.ReelId, "reel.1"), 7, 1), context);
            Assert.That(Values("reel.1"), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7 }));
            Assert.That(Apply(new RemoveReelSymbolEffect(first, 9), context).Status, Is.EqualTo(ExplorationEventEffectStatus.Skipped), "no such value");
            Assert.That(Apply(new AddReelSymbolEffect(new ExplorationReelSelector(ExplorationReelSelectorMode.Position, "", 9), 1), context).Status,
                Is.EqualTo(ExplorationEventEffectStatus.Skipped), "a missing reel is skipped");
        }

        [Test]
        public void RemoveReelSymbol_KeepsTheLastCell()
        {
            Assert.That(slots.Machine.TryReplaceStrip("reel.2", new[] { 1, 1 }, out var reason), Is.True, reason);
            var result = Apply(new RemoveReelSymbolEffect(new ExplorationReelSelector(ExplorationReelSelectorMode.Position, "", 2), 1, 5), context);
            Assert.That(result.Status, Is.EqualTo(ExplorationEventEffectStatus.Applied));
            Assert.That(Values("reel.2"), Is.EqualTo(new[] { 1 }));
            Assert.That(Apply(new RemoveReelSymbolEffect(new ExplorationReelSelector(ExplorationReelSelectorMode.Position, "", 2), 1), context).Status,
                Is.EqualTo(ExplorationEventEffectStatus.Skipped));
            Assert.That(Values("reel.2"), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void AddReel_UsesAFreeSuffixAndRemoveReelKeepsOne()
        {
            Apply(new AddReelEffect("reel.1", 1, 2), context);
            Apply(new AddReelEffect("reel.1", 3), context);
            Assert.That(slots.Machine.Reels.Select(reel => reel.Id), Is.EqualTo(new[] { "reel.1", "reel.2", "reel.3", "reel.1.2", "reel.1.3" }));
            for (var i = 0; i < 4; i++) Apply(new RemoveReelEffect(new ExplorationReelSelector(ExplorationReelSelectorMode.Last)), context);
            Assert.That(slots.Machine.ReelCount, Is.EqualTo(1));
            Assert.That(Apply(new RemoveReelEffect(new ExplorationReelSelector(ExplorationReelSelectorMode.Last)), context).Status, Is.EqualTo(ExplorationEventEffectStatus.Skipped));
            Assert.That(slots.Machine.ReelCount, Is.EqualTo(1));
        }

        // ---------- Fortune Critical ----------

        [Test]
        public void FortuneChance_CreatesAReelRuleAndClamps()
        {
            Apply(new ChangeFortuneChanceEffect(ExplorationFortuneTarget.DefaultReel, ExplorationValueChangeMode.Add, .02f), context);
            Assert.That(fortune.Rules.DefaultReelRule.Chance, Is.EqualTo(.02d).Within(1e-9));
            Assert.That(fortune.Rules.TryGetReelRule("reel.2", out _), Is.False);
            Apply(new ChangeFortuneChanceEffect(ExplorationFortuneTarget.Reel, ExplorationValueChangeMode.Add, .1f, "reel.2"), context);
            Assert.That(fortune.Rules.TryGetReelRule("reel.2", out var reelRule), Is.True, "a reel without its own rule gets a copy of the default rule");
            Assert.That(reelRule.Chance, Is.EqualTo(.12d).Within(1e-6));
            Apply(new ChangeFortuneChanceEffect(ExplorationFortuneTarget.Total, ExplorationValueChangeMode.Add, -5f), context);
            Assert.That(fortune.Rules.TotalRule.Chance, Is.Zero);
            Apply(new ChangeFortuneChanceEffect(ExplorationFortuneTarget.Total, ExplorationValueChangeMode.Add, 5f), context);
            Assert.That(fortune.Rules.TotalRule.Chance, Is.EqualTo(1d));
            Assert.That(new ChangeFortuneChanceEffect(ExplorationFortuneTarget.Total, ExplorationValueChangeMode.Set, 2f).Validate(out _), Is.False);
        }

        [Test]
        public void FortuneCap_IsClampedToTheAllowedRange()
        {
            Apply(new ChangeFortuneCapEffect(ExplorationValueChangeMode.Add, 5), context);
            Assert.That(fortune.Rules.MaximumEffectiveMultiplier, Is.EqualTo(FortuneCriticalRules.DisabledMaximumEffectiveMultiplier + 5));
            Apply(new ChangeFortuneCapEffect(ExplorationValueChangeMode.Set, 100000), context);
            Assert.That(fortune.Rules.MaximumEffectiveMultiplier, Is.EqualTo(FortuneCriticalLimits.MaximumEffectiveMultiplier));
            Apply(new ChangeFortuneCapEffect(ExplorationValueChangeMode.Add, -100000), context);
            Assert.That(fortune.Rules.MaximumEffectiveMultiplier, Is.EqualTo(1));
        }

        // ---------- Runes ----------

        [Test]
        public void GrantRune_NewLineStackAndUnequipped()
        {
            Apply(new GrantRuneEffect(TemporaryRuneConfiguration.AttackRuneId), context);
            Assert.That(runes.Loadout.Lines.Select(line => (line.LineId, line.Weight)), Is.EqualTo(new[] { ("line.1", 1), ("line.2", 1), ("line.event.1", 1) }));
            Assert.That(runes.Inventory.Contains("rune.attack.event.1"), Is.True);
            Apply(new GrantRuneEffect(TemporaryRuneConfiguration.HealRuneId, 2, ExplorationRunePlacement.StackOnLine), context);
            Assert.That(runes.Loadout.Lines.Single(line => line.LineId == "line.2").Weight, Is.EqualTo(3), "stacked on the first heal line");
            Apply(new GrantRuneEffect(TemporaryRuneConfiguration.AttackRuneId, 1, ExplorationRunePlacement.Unequipped), context);
            Assert.That(runes.Inventory.Count, Is.EqualTo(6));
            Assert.That(runes.Loadout.TryFindLineOf("rune.attack.event.2", out _), Is.False);
        }

        [Test]
        public void GrantRune_UndoesTheInventoryWhenPlacingFails()
        {
            var before = runes.Inventory.Count;
            var result = Apply(new GrantRuneEffect(TemporaryRuneConfiguration.HealRuneId, 1, ExplorationRunePlacement.StackOnLine, "line.1"), context);
            Assert.That(result.Status, Is.EqualTo(ExplorationEventEffectStatus.Skipped), "line.1 holds attack runes");
            Assert.That(runes.Inventory.Count, Is.EqualTo(before));
            Assert.That(Apply(new GrantRuneEffect("rune.unknown"), context).Status, Is.EqualTo(ExplorationEventEffectStatus.Skipped));
            Assert.That(runes.Inventory.Count, Is.EqualTo(before));
        }

        // ---------- Missing capabilities and conditions ----------

        [Test]
        public void MissingCapabilities_SkipEffectsAndFailConditions()
        {
            var empty = new ExplorationEventContext(null, null, null, null);
            IExplorationEventEffect[] effects =
            {
                new ChangeHealthEffect(1), new RestoreHealthEffect(), new ChangeMaximumHealthEffect(1),
                new AddReelSymbolEffect(new ExplorationReelSelector(), 1), new RemoveReelSymbolEffect(new ExplorationReelSelector(), 1),
                new AddReelEffect("r", 1), new RemoveReelEffect(new ExplorationReelSelector()),
                new ChangeFortuneChanceEffect(ExplorationFortuneTarget.Total, ExplorationValueChangeMode.Add, .1f), new ChangeFortuneCapEffect(ExplorationValueChangeMode.Add, 1),
                new GrantRuneEffect("rune.attack")
            };
            foreach (var effect in effects) Assert.That(Apply(effect, empty).Status, Is.EqualTo(ExplorationEventEffectStatus.Skipped), effect.GetType().Name);
            Assert.That(new HealthAtLeastCondition(1).IsSatisfied(empty, out _), Is.False);
            Assert.That(new ReelCountAtMostCondition(9).IsSatisfied(empty, out _), Is.False);
        }

        [Test]
        public void Conditions_ReportReasons()
        {
            Assert.That(new HealthAtLeastCondition(20).IsSatisfied(context, out _), Is.True);
            Assert.That(new HealthAtLeastCondition(21).IsSatisfied(context, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("체력 21 이상 필요"));
            Assert.That(new ReelCountAtLeastCondition(3).IsSatisfied(context, out _), Is.True);
            Assert.That(new ReelCountAtLeastCondition(4).IsSatisfied(context, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("Reel 4개 이상 필요"));
            Assert.That(new ReelCountAtMostCondition(2).IsSatisfied(context, out _), Is.False);
            Assert.That(new HasRuneDefinitionCondition(TemporaryRuneConfiguration.AttackRuneId, 1).IsSatisfied(context, out _), Is.True);
            Assert.That(new HasRuneDefinitionCondition(TemporaryRuneConfiguration.AttackRuneId, 2).IsSatisfied(context, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("공격 룬 2개 필요"));
        }

        private int[] Values(string reelId) => slots.Machine.FindReel(reelId).Strip.Select(symbol => symbol.Value).ToArray();
    }
}
