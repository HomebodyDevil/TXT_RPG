using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.Application.Combat;
using TxTRPG.Application.Configuration;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Persistence;
using TxTRPG.Application.Players;
using TxTRPG.Application.Runes;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.Players;
using TxTRPG.Gameplay.Runes;
using TxTRPG.Gameplay.SlotMachine;
using TxTRPG.UI;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using SlotMachineModel = TxTRPG.Gameplay.SlotMachine.SlotMachine;

namespace TxTRPG.Application.Tests
{
    public sealed class SlotFortuneCriticalCombatTests
    {
        private readonly List<Object> created = new();
        private PlayerSessionHost host;
        private StoryTextPanel story;
        private TemporaryCombatConfiguration combatConfiguration;
        private CountingRandom random;
        private CountingRandom fortuneRandom;

        [TearDown]
        public void TearDown()
        {
            PlayerSessionHost.SetInstanceForEditorTests(null);
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        // ---------- Controller ----------

        [UnityTest]
        public IEnumerator Action_SpinsResolvesFortuneAndAllocatesTheFinalTotalOnce()
        {
            // reel.2 always ×3, the total always ×2; no Fortune Critical draw is needed for a single-multiplier range.
            yield return SetUpCombat(Fortune(Definition(0f), new[] { ("reel.2", Definition(1f, 3, 3)) }, Definition(1f, 2, 2)));
            var panel = CreatePanel();
            var controller = CreateController(panel);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            var combat = controller.Combat;
            random.Values.Enqueue(2); random.Values.Enqueue(4); random.Values.Enqueue(1); // 3 + 5 + 2
            var before = story.MessageCount;

            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);

            Assert.That(random.Calls, Is.EqualTo(3), "the spin draws are unchanged");
            Assert.That(fortuneRandom.Calls, Is.EqualTo(0));
            var turn = host.LastTemporarySlotTurn;
            Assert.That(turn.Spin.PaylineTotal, Is.EqualTo(10));
            Assert.That(turn.Fortune.FinalTotal, Is.EqualTo(40), "(3 + 5×3 + 2) ×2");
            Assert.That(turn.Allocation.Lines.Select(l => l.Amount), Is.EqualTo(new[] { 20, 20 }), "the final total is allocated");
            Assert.That(combat.EnemyHealth.Current, Is.EqualTo(20), "the attack line is applied once");
            var lines = SlotCombatStoryFormatter.Turn(turn, combatConfiguration.EnemyName);
            Assert.That(lines.Take(3), Is.EqualTo(new[]
            {
                "[슬롯] (3 + 5×3 + 2) ×2 = 40",
                "[행운 치명] 2번 Reel ×3, 합계 ×2",
                "[룬] 1번 줄 공격 룬 ×1: 20"
            }));
            Assert.That(story.MessageCount - before, Is.EqualTo(lines.Count), "one Story record");
            Assert.That(panel.ReelViews.Cast<SlotReelView>().Select(v => v.FortuneMultiplierText), Is.EqualTo(new[] { "", "×3", "" }));
            Assert.That(panel.TotalMultiplierText, Is.EqualTo("×2"));
            Assert.That(panel.TotalValueText, Is.EqualTo("40"));
        }

        [UnityTest]
        public IEnumerator RandomFortune_UsesItsOwnRandomSourceAfterTheSpin()
        {
            yield return SetUpCombat(Fortune(Definition(.5f, 2, 10, "uniform"), null, Definition(0f)));
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            random.Values.Enqueue(2); random.Values.Enqueue(4); random.Values.Enqueue(1);
            // reel.1 no, reel.2 yes with 999,999 (×10), reel.3 no.
            fortuneRandom.Values.Enqueue(999_999); fortuneRandom.Values.Enqueue(0); fortuneRandom.Values.Enqueue(999_999); fortuneRandom.Values.Enqueue(999_999);
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            Assert.That(random.Calls, Is.EqualTo(3));
            Assert.That(fortuneRandom.Calls, Is.EqualTo(4), "three reel occurrence draws and one multiplier draw; the total rule is off");
            Assert.That(host.LastTemporarySlotTurn.Fortune.Reels.Select(r => r.ReelMultiplier), Is.EqualTo(new[] { 1, 10, 1 }));
            Assert.That(host.LastTemporarySlotTurn.Fortune.FinalTotal, Is.EqualTo(55));
        }

        [UnityTest]
        public IEnumerator AddedReel_UsesItsOwnRuleOrTheDefaultRule()
        {
            yield return SetUpCombat(Fortune(Definition(1f, 2, 2), new[] { ("reel.5", Definition(1f, 5, 5)) }, Definition(0f)));
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            var machine = host.TemporarySlotMachine.Machine;
            Assert.That(machine.TryAddReel(SlotReel.FromValues("reel.4", new[] { 1 }), out var reason), Is.True, reason);
            Assert.That(machine.TryAddReel(SlotReel.FromValues("reel.5", new[] { 1 }), out reason), Is.True, reason);
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            Assert.That(host.LastTemporarySlotTurn.Fortune.Reels.Select(r => r.ReelMultiplier), Is.EqualTo(new[] { 2, 2, 2, 2, 5 }));
        }

        [UnityTest]
        public IEnumerator ChangesAfterAcceptance_DoNotChangeTheAcceptedResult()
        {
            yield return SetUpCombat(Fortune(Definition(0f), new[] { ("reel.2", Definition(1f, 3, 3)) }, Definition(0f)));
            var panel = CreatePanel(10f);
            var controller = CreateController(panel);
            controller.SetSpinTimeoutOverrideForEditor(.3f);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            random.Values.Enqueue(2); random.Values.Enqueue(4); random.Values.Enqueue(1);
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            var accepted = host.PendingSlotAction;
            Assert.That(accepted.Fortune.FinalTotal, Is.EqualTo(20));

            var state = host.TemporaryFortuneCritical;
            Assert.That(state.TrySetReelChance("reel.2", 0d, out var reason), Is.True, reason);
            Assert.That(state.TrySetTotalChance(1d, out reason), Is.True, reason);
            LogAssert.Expect(LogType.Warning, new Regex("SLOT_SPIN_TIMEOUT"));
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);

            var turn = host.LastTemporarySlotTurn;
            Assert.That(turn.Fortune, Is.SameAs(accepted.Fortune));
            Assert.That(turn.Fortune.Reels.Select(r => r.ReelMultiplier), Is.EqualTo(new[] { 1, 3, 1 }));
            Assert.That(turn.Fortune.TotalMultiplier, Is.EqualTo(1));
            Assert.That(panel.ReelViews[1].PaylineValue, Is.EqualTo(5));
            Assert.That(((SlotReelView)panel.ReelViews[1]).FortuneMultiplierText, Is.EqualTo("×3"), "the accepted multiplier is shown");
            Assert.That(panel.TotalMultiplierText, Is.Empty);
            Assert.That(panel.TotalValueText, Is.EqualTo("20"));
        }

        [UnityTest]
        public IEnumerator SceneReload_RestoresTheStoredFortuneResult()
        {
            yield return SetUpCombat(Fortune(Definition(0f), new[] { ("reel.3", Definition(1f, 4, 4)) }, Definition(1f, 2, 2)));
            var panel = CreatePanel(10f);
            var first = CreateController(panel);
            first.SetSpinTimeoutOverrideForEditor(.3f);
            yield return Wait(first.InitializeAsync(default, CancellationToken.None));
            first.BeginCombat(new HealthState(30, 30));
            random.Values.Enqueue(0); random.Values.Enqueue(0); random.Values.Enqueue(1); // 1 + 1 + 2×4 = 10, ×2 = 20
            Assert.That(first.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            var accepted = host.PendingSlotAction;

            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(panel.transform.root.gameObject);
            var restoredPanel = CreatePanel();
            var second = CreateController(restoredPanel);
            yield return Wait(second.InitializeAsync(default, CancellationToken.None));
            AssertShows(restoredPanel, accepted.Fortune, new[] { "", "", "×4" }, "×2", "the pending accepted spin");
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            Assert.That(fortuneRandom.Calls, Is.EqualTo(0), "never resolved again");

            // A later Scene reload shows the last applied turn of the current node from the stored record.
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(restoredPanel.transform.root.gameObject);
            var thirdPanel = CreatePanel();
            var third = CreateController(thirdPanel);
            yield return Wait(third.InitializeAsync(default, CancellationToken.None));
            AssertShows(thirdPanel, host.LastTemporarySlotTurn.Fortune, new[] { "", "", "×4" }, "×2", "the last applied turn");
        }

        [UnityTest]
        public IEnumerator OverflowingFinalTotal_IsNotAccepted()
        {
            var slots = Track(ScriptableObject.CreateInstance<TemporarySlotMachineConfiguration>());
            slots.ConfigureForEditor(new[] { new TemporarySlotReelDefinition("reel.1", 1_000_000_000) });
            yield return SetUpCombat(Fortune(Definition(1f, 3, 3), null, Definition(0f)), slots);
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            LogAssert.Expect(LogType.Error, new Regex("could not be accepted"));
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.False);
            Assert.That(host.IsTemporaryCombatActionPending, Is.False, "the lock is released");
            Assert.That(host.PendingSlotAction, Is.Null);
            Assert.That(host.LastTemporarySlotTurn, Is.Null);
            Assert.That(controller.Combat.EnemyHealth.Current, Is.EqualTo(40));
            Assert.That(controller.CanExecute(TemporarySlotCombatController.SpinCommandId, out var reason), Is.True, reason);
        }

        // ---------- Configuration and session ----------

        [UnityTest]
        public IEnumerator MissingConfiguration_DisablesFortuneWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Fortune Critical is disabled"));
            yield return SetUpCombat(null);
            Assert.That(host.TemporaryFortuneCritical.Rules.IsDisabled, Is.True);
            Assert.That(host.TemporarySlotCombatError, Is.Empty, "the slot machine combat is not affected");
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            random.Values.Enqueue(2); random.Values.Enqueue(4); random.Values.Enqueue(1);
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            Assert.That(host.LastTemporarySlotTurn.Fortune.FinalTotal, Is.EqualTo(10));
            Assert.That(fortuneRandom.Calls, Is.EqualTo(0));
            Assert.That(SlotCombatStoryFormatter.Turn(host.LastTemporarySlotTurn, "적")[0], Is.EqualTo("[슬롯] 3 + 5 + 2 = 10"));
        }

        [UnityTest]
        public IEnumerator InvalidConfiguration_DisablesFortuneWithAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex("Fortune Critical is disabled because its configuration is invalid.*not registered"));
            yield return SetUpCombat(Fortune(Definition(.5f, 2, 10, "missing-function"), null, Definition(0f)));
            Assert.That(host.TemporaryFortuneCritical.Rules.IsDisabled, Is.True);
            Assert.That(host.TemporarySlotCombatError, Is.Empty);
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            Assert.That(controller.CanExecute(TemporarySlotCombatController.SpinCommandId, out var reason), Is.True, reason);
        }

        [Test]
        public void SessionState_ValidatesChangesAndRaisesChangedOnce()
        {
            var state = new TemporaryFortuneCriticalState(FortuneCriticalRules.Disabled);
            var changes = 0;
            state.Changed += () => changes++;

            Assert.That(state.TrySetDefaultReelChance(.2d, out var reason), Is.True, reason);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(state.TrySetDefaultReelChance(.2d, out reason), Is.True, reason);
            Assert.That(changes, Is.EqualTo(1), "the same value raises nothing");

            var before = state.Rules;
            Assert.That(state.TrySetDefaultReelChance(1.5d, out reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(state.TrySetTotalChance(double.NaN, out _), Is.False);
            Assert.That(state.TrySetMaximumEffectiveMultiplier(0, out _), Is.False);
            Assert.That(state.TrySetReelRule(" ", state.Rules.DefaultReelRule, out _), Is.False);
            Assert.That(state.TrySetDefaultReelRule(null, out _), Is.False);
            Assert.That(state.TryRemoveReelRule("reel.1", out _), Is.False);
            Assert.That(state.TryReplaceAll(null, out _), Is.False);
            Assert.That(state.Rules, Is.SameAs(before), "failed changes keep the previous rules");
            Assert.That(changes, Is.EqualTo(1));

            Assert.That(state.TrySetReelChance("reel.9", .5d, out reason), Is.True, reason);
            Assert.That(state.Rules.TryGetReelRule("reel.9", out var reelRule) && reelRule.Chance.Equals(.5d), Is.True,
                "a reel without its own rule gets a copy of the default rule");
            Assert.That(state.TrySetMaximumEffectiveMultiplier(12, out reason), Is.True, reason);
            Assert.That(state.TryRemoveReelRule("reel.9", out reason), Is.True, reason);
            Assert.That(FortuneCriticalRule.TryCreate(1d, 4, 4, FortuneMultiplierDistribution.Default, FortuneWeightFunctionRegistry.Default,
                out var fixedRule, out reason), Is.True, reason);
            Assert.That(state.TrySetTotalRule(fixedRule, out reason), Is.True, reason);
            Assert.That(changes, Is.EqualTo(5));
            Assert.That(state.TryReplaceAll(FortuneCriticalRules.Disabled, out reason), Is.True, reason);
            Assert.That(state.Rules.IsDisabled, Is.True);
            Assert.That(changes, Is.EqualTo(6));
        }

        [Test]
        public void Configuration_CreatesTheDefaultRulesAndOnValidateClampsOnlyItsValues()
        {
            var configuration = Track(ScriptableObject.CreateInstance<TemporaryFortuneCriticalConfiguration>());
            Assert.That(configuration.TryCreateRules(FortuneWeightFunctionRegistry.Default, out var rules, out var reason), Is.True, reason);
            Assert.That(rules.DefaultReelRule.Chance, Is.EqualTo(.05d).Within(1e-6d));
            Assert.That(rules.TotalRule.Chance, Is.EqualTo(.03d).Within(1e-6d));
            Assert.That((rules.DefaultReelRule.MinimumMultiplier, rules.DefaultReelRule.MaximumMultiplier), Is.EqualTo((2, 10)));
            Assert.That(rules.DefaultReelRule.Distribution.FunctionId, Is.EqualTo("geometric"));
            Assert.That(rules.MaximumEffectiveMultiplier, Is.EqualTo(30));

            configuration.ConfigureForEditor(new TemporaryFortuneCriticalRuleDefinition(1.5f, 1, 200),
                new[] { new TemporaryFortuneCriticalReelRuleDefinition("reel.2", new TemporaryFortuneCriticalRuleDefinition(-1f, 50, 10)) },
                new TemporaryFortuneCriticalRuleDefinition(float.NaN, 3, 3), 0);
            Assert.That(configuration.TryCreateRules(FortuneWeightFunctionRegistry.Default, out _, out _), Is.False);
            configuration.ValidateForEditor();
            Assert.That(configuration.DefaultReelRule.Chance, Is.EqualTo(1f));
            Assert.That((configuration.DefaultReelRule.MinimumMultiplier, configuration.DefaultReelRule.MaximumMultiplier), Is.EqualTo((2, 100)));
            Assert.That(configuration.ReelRules[0].Rule.Chance, Is.EqualTo(0f));
            Assert.That((configuration.ReelRules[0].Rule.MinimumMultiplier, configuration.ReelRules[0].Rule.MaximumMultiplier), Is.EqualTo((50, 50)));
            Assert.That(configuration.TotalRule.Chance, Is.EqualTo(0f));
            Assert.That(configuration.MaximumEffectiveMultiplier, Is.EqualTo(1));
            Assert.That(configuration.TryCreateRules(FortuneWeightFunctionRegistry.Default, out _, out reason), Is.True, reason);
        }

        [Test]
        public void SavedConfigurationAsset_HasTheTemporaryDefaultsAndIsReferencedByAppRoot()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TemporaryFortuneCriticalConfiguration>(TemporarySlotFortuneCriticalApply.ConfigurationPath);
            Assert.That(asset, Is.Not.Null, "Run Apply Slot Fortune Critical first.");
            Assert.That(asset.TryCreateRules(FortuneWeightFunctionRegistry.Default, out var rules, out var reason), Is.True, reason);
            Assert.That(rules.DefaultReelRule.ExpectedMultiplier, Is.EqualTo(1.148d).Within(.001d));
            var appRoot = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.AppRootPath);
            Assert.That(appRoot.GetComponentInChildren<PlayerSessionHost>(true).TemporaryFortuneCriticalConfigurationForEditor, Is.SameAs(asset));
            Assert.DoesNotThrow(TemporarySlotFortuneCriticalApply.Validate);
        }

        // ---------- Story ----------

        [Test]
        public void StoryFormatter_WritesEveryFortuneCase()
        {
            var spin = new SlotMachineModel(new[]
            {
                SlotReel.FromValues("reel.1", new[] { 3 }), SlotReel.FromValues("reel.2", new[] { 5 }), SlotReel.FromValues("reel.3", new[] { 2 })
            }).Spin(new SystemRandomIndexProvider(1));
            var off = FixedRule(0d, 2);

            Assert.That(SlotCombatStoryFormatter.Spin(Resolve(spin, off, null, off, 30)), Is.EqualTo("[슬롯] 3 + 5 + 2 = 10"));
            Assert.That(SlotCombatStoryFormatter.FortuneSummary(Resolve(spin, off, null, off, 30)), Is.Null);
            var reelOnly = Resolve(spin, off, FixedRule(1d, 3), off, 30);
            Assert.That(SlotCombatStoryFormatter.Spin(reelOnly), Is.EqualTo("[슬롯] 3 + 5×3 + 2 = 20"));
            var totalOnly = Resolve(spin, off, null, FixedRule(1d, 2), 30);
            Assert.That(SlotCombatStoryFormatter.Spin(totalOnly), Is.EqualTo("[슬롯] (3 + 5 + 2) ×2 = 20"));
            var both = Resolve(spin, off, FixedRule(1d, 3), FixedRule(1d, 2), 30);
            Assert.That(SlotCombatStoryFormatter.Spin(both), Is.EqualTo("[슬롯] (3 + 5×3 + 2) ×2 = 40"));
            Assert.That(SlotCombatStoryFormatter.FortuneSummary(both), Is.EqualTo("[행운 치명] 2번 Reel ×3, 합계 ×2"));
            Assert.That(SlotCombatStoryFormatter.FortuneCap(both), Is.Null);
            var capped = Resolve(spin, off, FixedRule(1d, 10), FixedRule(1d, 5), 30);
            Assert.That(SlotCombatStoryFormatter.Spin(capped), Is.EqualTo("[슬롯] (3 + 5×10 + 2) ×5 = 175"), "the last value is the capped final total");
            Assert.That(SlotCombatStoryFormatter.FortuneCap(capped), Is.EqualTo("[행운 치명] 최대 배수 ×30 적용: 2번 Reel (×50 → ×30)"));
            Assert.That(SlotCombatStoryFormatter.FortuneCriticalName, Is.EqualTo("행운 치명"));
            Assert.That(SlotCombatStoryFormatter.MultiplierText(1), Is.Empty);
            Assert.That(SlotCombatStoryFormatter.MultiplierText(3), Is.EqualTo("×3"));

            var display = TemporarySlotCombatController.CreateSpinDisplay(spin, capped, AllocationOf(capped.FinalTotal));
            Assert.That(display.Reels.Select(r => r.MultiplierText), Is.EqualTo(new[] { "", "×10", "" }));
            Assert.That(display.ResultSummary.TotalValue, Is.EqualTo("175"));
            Assert.That(display.ResultSummary.TotalMultiplierText, Is.EqualTo("×5"));
            Assert.That(display.SpinningSummary.TotalMultiplierText, Is.Empty);
            var plain = TemporarySlotCombatController.CreateSpinDisplay(spin, AllocationOf(10));
            Assert.That(plain.Reels.All(r => r.MultiplierText.Length == 0) && plain.ResultSummary.TotalValue == "10", Is.True,
                "the two-argument overload shows no multiplier");
        }

        // ---------- Running total ----------

        private static SlotSpinResult ThreeReelSpin() => new SlotMachineModel(new[]
        {
            SlotReel.FromValues("reel.1", new[] { 3 }), SlotReel.FromValues("reel.2", new[] { 5 }), SlotReel.FromValues("reel.3", new[] { 2 })
        }).Spin(new SystemRandomIndexProvider(1));

        private static string[] ProgressTotals(SlotSpinDisplay display) => display.ProgressSummaries.Select(s => s.TotalValue).ToArray();

        [Test]
        public void SpinDisplay_StartsAtZeroAndAddsEachReelBeforeTheTotalMultiplier()
        {
            var spin = ThreeReelSpin();
            var off = FixedRule(0d, 2);
            var both = Resolve(spin, off, FixedRule(1d, 10), FixedRule(1d, 5), 30);
            var display = TemporarySlotCombatController.CreateSpinDisplay(spin, both, AllocationOf(both.FinalTotal));
            Assert.That(display.SpinningSummary.TotalValue, Is.EqualTo(SlotCombatStoryFormatter.TotalValue(0)));
            Assert.That(display.SpinningSummary.TotalMultiplierText, Is.Empty);
            Assert.That(ProgressTotals(display), Is.EqualTo(new[] { "3", "53", "55" }), "v × min(C, r) per stopped reel, without the total multiplier");
            Assert.That(display.HasTotalStep, Is.True);
            Assert.That(display.ResultSummary.TotalValue, Is.EqualTo("175"));
            Assert.That(display.ResultSummary.TotalMultiplierText, Is.EqualTo("×5"));
            foreach (var progress in display.ProgressSummaries)
            {
                Assert.That(progress.TotalMultiplierText, Is.Empty, "no total multiplier before the result");
                Assert.That(progress.Lines.Select(l => l.ValueText), Is.EqualTo(display.SpinningSummary.Lines.Select(l => l.ValueText)),
                    "the rune table keeps the stacked weights until the result");
                Assert.That(progress.Lines.Any(l => l.ValueText.Contains("→")), Is.False, "no allocation before the result");
            }
            Assert.That(display.ResultSummary.Lines.All(l => l.ValueText.Contains("→")), Is.True, "the allocation appears with the result");
        }

        [Test]
        public void SpinDisplay_CapsTheReelMultiplierAndEndsOnTheFinalTotalWithoutATotalCritical()
        {
            var spin = ThreeReelSpin();
            var off = FixedRule(0d, 2);
            // r = 40 is above C = 30, so the running total adds 5 × 30.
            var capped = Resolve(spin, off, FixedRule(1d, 40), off, 30);
            var display = TemporarySlotCombatController.CreateSpinDisplay(spin, capped, AllocationOf(capped.FinalTotal));
            Assert.That(ProgressTotals(display), Is.EqualTo(new[] { "3", "153", "155" }));
            Assert.That(display.HasTotalStep, Is.False);
            Assert.That(ProgressTotals(display).Last(), Is.EqualTo(SlotCombatStoryFormatter.TotalValue(capped.FinalTotal)),
                "without a total Fortune Critical the last running total is the final total");
            Assert.That(display.ResultSummary.TotalMultiplierText, Is.Empty);

            var cappedWithTotal = Resolve(spin, off, FixedRule(1d, 40), FixedRule(1d, 2), 30);
            var withTotal = TemporarySlotCombatController.CreateSpinDisplay(spin, cappedWithTotal, AllocationOf(cappedWithTotal.FinalTotal));
            Assert.That(ProgressTotals(withTotal), Is.EqualTo(new[] { "3", "153", "155" }), "the running total never applies the total multiplier");
            Assert.That(withTotal.ResultSummary.TotalValue, Is.EqualTo("160"), "3×2 + 5×min(30, 80) + 2×2");
            Assert.That(withTotal.HasTotalStep, Is.True);

            // Random reel and total rules: the last running total equals the final total exactly when there is no total step.
            var rnd = new System.Random(7);
            for (var i = 0; i < 50; i++)
            {
                var reel = FixedRule(1d, rnd.Next(2, 60));
                var total = rnd.Next(2) == 0 ? off : FixedRule(1d, rnd.Next(2, 10));
                var fortune = Resolve(spin, reel, null, total, 30);
                var shown = TemporarySlotCombatController.CreateSpinDisplay(spin, fortune, AllocationOf(fortune.FinalTotal));
                Assert.That(shown.HasTotalStep, Is.EqualTo(fortune.IsTotalCritical));
                var last = int.Parse(ProgressTotals(shown).Last());
                if (fortune.IsTotalCritical) Assert.That(last, Is.LessThanOrEqualTo(fortune.FinalTotal));
                else Assert.That(last, Is.EqualTo(fortune.FinalTotal));
            }
        }

        [Test]
        public void SpinDisplay_WithoutFortuneCriticalAddsThePaylineValues()
        {
            var spin = ThreeReelSpin();
            var plain = TemporarySlotCombatController.CreateSpinDisplay(spin, AllocationOf(10));
            Assert.That(plain.SpinningSummary.TotalValue, Is.EqualTo("0"));
            Assert.That(ProgressTotals(plain), Is.EqualTo(new[] { "3", "8", "10" }));
            Assert.That(plain.HasTotalStep, Is.False);
            var lines = new[] { new SlotRuneLineDisplay("공격", "×2") };
            var weighted = TemporarySlotCombatController.CreateSpinDisplay(spin, FortuneCriticalResult.None(spin), lines);
            Assert.That(weighted.SpinningSummary.TotalValue, Is.EqualTo("0"));
            Assert.That(ProgressTotals(weighted), Is.EqualTo(new[] { "3", "8", "10" }));
            Assert.That(weighted.HasTotalStep, Is.False);
            Assert.That(weighted.ProgressSummaries.All(s => s.Lines.Single().ValueText == "×2"), Is.True);
        }

        // ---------- Helpers ----------

        private static FortuneCriticalRule FixedRule(double chance, int multiplier)
        {
            Assert.That(FortuneCriticalRule.TryCreate(chance, multiplier, multiplier, FortuneMultiplierDistribution.Default,
                FortuneWeightFunctionRegistry.Default, out var rule, out var reason), Is.True, reason);
            return rule;
        }

        // reel.2 takes reelRule when given; every other reel takes defaultRule.
        private static FortuneCriticalResult Resolve(SlotSpinResult spin, FortuneCriticalRule defaultRule, FortuneCriticalRule reelRule,
            FortuneCriticalRule totalRule, int cap)
        {
            var perReel = reelRule == null ? null : new[] { new FortuneCriticalReelRule("reel.2", reelRule) };
            Assert.That(FortuneCriticalRules.TryCreate(defaultRule, perReel, totalRule, cap, out var rules, out var reason), Is.True, reason);
            return FortuneCriticalResolver.Resolve(spin, rules, new SystemRandomIndexProvider(1));
        }

        private static RuneAllocation AllocationOf(int total)
        {
            var catalog = new RuneDefinitionCatalog(new[] { new RuneDefinition("rune.attack", "공격 룬", RuneEffectKind.Attack) });
            var inventory = new RuneInventory(catalog);
            inventory.TryAdd(new OwnedRune("a1", "rune.attack"), out _);
            var loadout = new RuneLoadout(inventory, new[] { new RuneLoadoutLine("line.1", new[] { "a1" }) });
            return WeightedRuneAllocationPolicy.Instance.Allocate(total, loadout, catalog);
        }

        private static TemporaryFortuneCriticalRuleDefinition Definition(float chance, int minimum = 2, int maximum = 10, string function = "geometric") =>
            new(chance, minimum, maximum, function);

        private TemporaryFortuneCriticalConfiguration Fortune(TemporaryFortuneCriticalRuleDefinition reelDefault,
            IEnumerable<(string Id, TemporaryFortuneCriticalRuleDefinition Rule)> perReel, TemporaryFortuneCriticalRuleDefinition total)
        {
            var configuration = Track(ScriptableObject.CreateInstance<TemporaryFortuneCriticalConfiguration>());
            configuration.ConfigureForEditor(reelDefault,
                (perReel ?? Array.Empty<(string, TemporaryFortuneCriticalRuleDefinition)>()).Select(r => new TemporaryFortuneCriticalReelRuleDefinition(r.Id, r.Rule)),
                total, 30);
            return configuration;
        }

        private void AssertShows(SlotMachinePanel panel, FortuneCriticalResult fortune, string[] reelTexts, string totalText, string message)
        {
            Assert.That(panel.ReelViews.Cast<SlotReelView>().Select(v => v.FortuneMultiplierText), Is.EqualTo(reelTexts), message);
            Assert.That(panel.TotalMultiplierText, Is.EqualTo(totalText), message);
            Assert.That(panel.TotalValueText, Is.EqualTo(fortune.FinalTotal.ToString()), message);
        }

        private IEnumerator SetUpCombat(TemporaryFortuneCriticalConfiguration fortune, TemporarySlotMachineConfiguration slots = null)
        {
            var profile = AssetDatabase.LoadAssetAtPath<NewGameProfile>(ApplicationProjectBuilder.DefaultNewGameProfilePath);
            var owner = Track(new GameObject("PlayerSessionHost"));
            host = owner.AddComponent<PlayerSessionHost>();
            host.Configure(profile, "slot-fortune-tests.json", false);
            host.UseSessionForEditorTests(new PlayerSession(profile, new MemoryRepository(), new GuidCharacterInstanceIdGenerator()));
            host.ConfigureTemporarySlotCombat(slots ?? Track(ScriptableObject.CreateInstance<TemporarySlotMachineConfiguration>()),
                Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>()));
            host.ConfigureTemporaryFortuneCritical(fortune);
            PlayerSessionHost.SetInstanceForEditorTests(host);
            combatConfiguration = Track(ScriptableObject.CreateInstance<TemporaryCombatConfiguration>());
            random = new CountingRandom();
            fortuneRandom = new CountingRandom();
            var canvas = Track(new GameObject("StoryCanvas", typeof(Canvas)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab");
            story = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<StoryTextPanel>();
            var initialization = host.EnsureInitializedAsync();
            yield return Wait(initialization);
            Assert.That(host.TemporarySlotMachine, Is.Not.Null, host.TemporarySlotCombatError);
            Assert.That(host.TemporaryFortuneCritical, Is.Not.Null);
            host.GetOrCreateTemporaryExploration(() => new ExplorationRunState("run", new HealthState(30, 30),
                new ExplorationNodeGenerator(2, new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Combat, 1) }, new FixedExplorationRandom())));
        }

        private TemporarySlotCombatController CreateController(SlotMachinePanel panel)
        {
            var controller = Track(new GameObject("SlotCombat")).AddComponent<TemporarySlotCombatController>();
            controller.ConfigureForEditor(null, null, story, combatConfiguration, null, null, null, null, panel);
            controller.SetRandomForTests(random);
            controller.SetFortuneRandomForTests(fortuneRandom);
            return controller;
        }

        // A panel whose presentation lasts `seconds` (constant speed only). Time does not advance in Edit Mode, so a
        // positive value ends through the controller timeout; zero finishes without waiting.
        private SlotMachinePanel CreatePanel(float seconds = 0f)
        {
            var canvas = Track(new GameObject("SlotCanvas", typeof(Canvas)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.PanelPrefabPath);
            var panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<SlotMachinePanel>();
            ((RectTransform)panel.transform).sizeDelta = new Vector2(360f, 280f);
            panel.SetSpinSettingsForEditor(seconds <= 0f ? SlotReelSpinSettings.Instant : new SlotReelSpinSettings(SlotEasingKind.Linear,
                0f, 0f, 0f, 0f, seconds, 0f, 0f, 0f, 0f, 0f, 18f, 8f, false, 0f, 0f, 0, 0f, 0f, 1f, 1f, 0f));
            return panel;
        }

        private static IEnumerator Wait(Task task, double seconds = 10d)
        {
            var deadline = EditorApplication.timeSinceStartup + seconds;
            while (!task.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > deadline) Assert.Fail("The operation did not finish in time.");
                yield return null;
            }
            if (task.IsFaulted) throw task.Exception.GetBaseException();
        }

        private static IEnumerator WaitWhile(Func<bool> condition, double seconds = 10d)
        {
            var deadline = EditorApplication.timeSinceStartup + seconds;
            while (condition())
            {
                if (EditorApplication.timeSinceStartup > deadline) Assert.Fail("The condition did not clear in time.");
                yield return null;
            }
        }

        private sealed class CountingRandom : IRandomIndexProvider
        {
            public Queue<int> Values { get; } = new();
            public int Calls { get; private set; }
            public int NextIndex(int exclusiveUpperBound)
            {
                Calls++;
                return Values.Count > 0 ? Values.Dequeue() % exclusiveUpperBound : 0;
            }
        }

        private sealed class FixedExplorationRandom : IExplorationRandomSource
        {
            public int Next(int exclusiveMaximum) => 0;
        }

        private sealed class MemoryRepository : IPlayerSaveRepository
        {
            private PlayerSaveData saved;
            public Task<PlayerSaveData> TryLoadAsync(CancellationToken cancellationToken) => Task.FromResult(saved);
            public Task SaveAsync(PlayerSaveData data, CancellationToken cancellationToken) { saved = data; return Task.CompletedTask; }
        }
    }
}
