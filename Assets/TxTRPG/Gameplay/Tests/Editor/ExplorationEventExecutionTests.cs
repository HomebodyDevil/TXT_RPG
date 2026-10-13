using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.Exploration.Events;
using TxTRPG.Gameplay.SlotMachine;
using SlotMachineModel = TxTRPG.Gameplay.SlotMachine.SlotMachine;

namespace TxTRPG.Gameplay.Tests
{
    // The effects and conditions here are defined in the test assembly: the execution only needs the contracts, which is
    // the extension check (a new effect is one class).
    public sealed class ExplorationEventExecutionTests
    {
        private const string End = ExplorationEventTargets.End;

        // ---------- Generator and node content ----------

        [Test]
        public void Generator_StoresContentIdsForTypesWithAPicker()
        {
            var picker = new WeightedExplorationContentPicker(new[] { new ExplorationNodeContentWeight("event.a", 1), new ExplorationNodeContentWeight("event.b", 1) });
            var generator = new ExplorationNodeGenerator(3, new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Combat, 1), new ExplorationNodeWeight(ExplorationNodeTypeIds.Event, 1) },
                new Fixed(1, 0, 0, 1, 1, 0), new Dictionary<string, IExplorationNodeContentPicker> { [ExplorationNodeTypeIds.Event] = picker });
            var candidates = generator.GenerateCandidates(0);
            Assert.That(candidates.Select(item => item.TypeId), Is.EqualTo(new[] { ExplorationNodeTypeIds.Event, ExplorationNodeTypeIds.Combat, ExplorationNodeTypeIds.Event }));
            Assert.That(candidates.Select(item => item.ContentId), Is.EqualTo(new[] { "event.a", string.Empty, "event.b" }), "the second event avoids event.a in the same choice set");
        }

        [Test]
        public void Picker_AllowsRepeatsOnlyWhenNothingElseRemains()
        {
            var picker = new WeightedExplorationContentPicker(new[] { new ExplorationNodeContentWeight("only", 2) });
            Assert.That(picker.TryPick(new ExplorationNodeContentRequest(ExplorationNodeTypeIds.Event, 0, new[] { "only" }), new Fixed(0), out var id), Is.True);
            Assert.That(id, Is.EqualTo("only"));
            Assert.That(new WeightedExplorationContentPicker(new[] { new ExplorationNodeContentWeight("zero", 0) })
                .TryPick(new ExplorationNodeContentRequest("event", 0, null), new Fixed(0), out _), Is.False);
        }

        [Test]
        public void Generator_RedrawsWithoutTheTypeWhosePickerFails()
        {
            var generator = new ExplorationNodeGenerator(2, new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Event, 5), new ExplorationNodeWeight(ExplorationNodeTypeIds.Combat, 5) },
                new Fixed(0, 0), new Dictionary<string, IExplorationNodeContentPicker> { [ExplorationNodeTypeIds.Event] = new FailingPicker() });
            Assert.That(generator.GenerateCandidates(0).Select(item => item.TypeId), Is.All.EqualTo(ExplorationNodeTypeIds.Combat));
            var onlyEvents = new ExplorationNodeGenerator(1, new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Event, 5) },
                new Fixed(0), new Dictionary<string, IExplorationNodeContentPicker> { [ExplorationNodeTypeIds.Event] = new FailingPicker() });
            Assert.Throws<InvalidOperationException>(() => onlyEvents.GenerateCandidates(0));
        }

        [Test]
        public void RunState_RecordsContentIdAndLeavesItEmptyForOtherNodes()
        {
            var picker = new WeightedExplorationContentPicker(new[] { new ExplorationNodeContentWeight("event.a", 1) });
            var run = new ExplorationRunState("run", new HealthState(30), new ExplorationNodeGenerator(2,
                new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Event, 1), new ExplorationNodeWeight(ExplorationNodeTypeIds.Combat, 1) },
                new Fixed(0, 0, 1), new Dictionary<string, IExplorationNodeContentPicker> { [ExplorationNodeTypeIds.Event] = picker }));
            Assert.That(run.CurrentChoices.Select(node => (node.TypeId, node.ContentId)),
                Is.EqualTo(new[] { (ExplorationNodeTypeIds.Event, "event.a"), (ExplorationNodeTypeIds.Combat, string.Empty) }));
            Assert.That(run.Nodes.Single(node => node.Id == ExplorationRunState.RootNodeId).ContentId, Is.Empty);
        }

        // ---------- Execution ----------

        [Test]
        public void Pages_MoveInOrderAndEndCompletes()
        {
            var context = new Context();
            var execution = new ExplorationEventExecution(Create(
                Page("a", Continue("b")),
                Page("b", Continue(End))));
            execution.Start(context);
            Assert.That(execution.CurrentPageId, Is.EqualTo("a"));
            Assert.That(execution.TryContinue(context, execution.AppliedStepCount, out _, out var reason), Is.True, reason);
            Assert.That(execution.CurrentPageId, Is.EqualTo("b"));
            Assert.That(execution.TryContinue(context, execution.AppliedStepCount, out var step, out reason), Is.True, reason);
            Assert.That(step.State, Is.EqualTo(ExplorationEventExecutionState.Completed));
            Assert.That(execution.IsFinished, Is.True);
        }

        [Test]
        public void Choice_RejectsUnmetConditionWithoutChange()
        {
            var context = new Context();
            var counter = new Counter();
            var execution = new ExplorationEventExecution(Create(Page("a", new ExplorationEventChoice(new[]
            {
                new ExplorationEventChoiceOption("locked", new IExplorationEventCondition[] { new Never() }, new ExplorationEventBranch(new[] { counter }, End)),
                new ExplorationEventChoiceOption("open", null, new ExplorationEventBranch(null, End))
            }))));
            execution.Start(context);
            var options = execution.DescribeOptions(context);
            Assert.That(options.Select(option => option.IsEnabled), Is.EqualTo(new[] { false, true }));
            Assert.That(options[0].DisabledReason, Is.EqualTo("never"));
            var before = execution.AppliedStepCount;
            Assert.That(execution.TryChoose(context, 0, before, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("never"));
            Assert.That(counter.Applied, Is.Zero);
            Assert.That(execution.AppliedStepCount, Is.EqualTo(before));
            Assert.That(execution.TryChoose(context, 1, before, out _, out _), Is.True);
        }

        [TestCase(3, "low")]
        [TestCase(6, "low")]
        [TestCase(7, "mid")]
        [TestCase(10, "mid")]
        [TestCase(11, "high")]
        [TestCase(99, "high")]
        public void SlotCheck_PicksTheFirstMatchingRange(int total, string expected)
        {
            var context = new Context();
            var execution = new ExplorationEventExecution(Create(
                Page("start", new ExplorationEventSlotCheck("spin", true, new[]
                {
                    new ExplorationEventSlotOutcome(null, 6, new ExplorationEventBranch(null, "low")),
                    new ExplorationEventSlotOutcome(7, 10, new ExplorationEventBranch(null, "mid")),
                    new ExplorationEventSlotOutcome(8, 9, new ExplorationEventBranch(null, "unused")),
                    new ExplorationEventSlotOutcome(11, null, new ExplorationEventBranch(null, "high"))
                }, new ExplorationEventBranch(null, "fallback"))),
                Page("low", Continue(End)), Page("mid", Continue(End)), Page("high", Continue(End)), Page("unused", Continue(End)), Page("fallback", Continue(End))));
            execution.Start(context);
            Assert.That(execution.TryResolveSlotCheck(context, total, -1, out _, out _), Is.False, "the slot check must be started first");
            Assert.That(execution.TryBeginSlotCheck(context, execution.AppliedStepCount, out _, out var reason), Is.True, reason);
            Assert.That(execution.State, Is.EqualTo(ExplorationEventExecutionState.AwaitingSlotResult));
            Assert.That(execution.TryContinue(context, -1, out _, out _), Is.False, "no other input while awaiting the result");
            Assert.That(execution.TryResolveSlotCheck(context, total, execution.AppliedStepCount, out _, out reason), Is.True, reason);
            Assert.That(execution.CurrentPageId, Is.EqualTo(expected));
        }

        [Test]
        public void SlotCheck_UsesFallbackWhenNoRangeMatches()
        {
            var context = new Context();
            var execution = new ExplorationEventExecution(Create(
                Page("start", new ExplorationEventSlotCheck("spin", false, new[] { new ExplorationEventSlotOutcome(5, 6, new ExplorationEventBranch(null, End)) },
                    new ExplorationEventBranch(null, "fallback"))),
                Page("fallback", Continue(End))));
            execution.Start(context);
            execution.TryBeginSlotCheck(context, -1, out _, out _);
            execution.TryResolveSlotCheck(context, 20, -1, out _, out _);
            Assert.That(execution.CurrentPageId, Is.EqualTo("fallback"));
            var created = Create(Page("start", new ExplorationEventSlotCheck("spin", false, new[]
            {
                new ExplorationEventSlotOutcome(1, 5, new ExplorationEventBranch(null, End)),
                new ExplorationEventSlotOutcome(5, 9, new ExplorationEventBranch(null, End))
            }, new ExplorationEventBranch(null, End))));
            Assert.That(created.CollectWarnings().Count(), Is.EqualTo(1), "overlapping ranges are a warning, not an error");
        }

        [Test]
        public void OnEnter_IsAppliedOnceEachTimeThePageIsEntered()
        {
            var context = new Context();
            var counter = new Counter();
            var execution = new ExplorationEventExecution(Create(
                Page("a", Continue("b")),
                new ExplorationEventPage("b", "", "", new IExplorationEventEffect[] { counter }, Continue("a"))));
            execution.Start(context);
            execution.TryContinue(context, -1, out _, out _);
            Assert.That(counter.Applied, Is.EqualTo(1));
            execution.TryContinue(context, -1, out _, out _);
            execution.TryContinue(context, -1, out _, out _);
            Assert.That(counter.Applied, Is.EqualTo(2));
            Assert.That(execution.VisitsOf("b"), Is.EqualTo(2));
        }

        [Test]
        public void HealthZero_StopsRemainingEffectsAndDefeats()
        {
            var context = new Context { Health = new HealthState(10, 4) };
            var after = new Counter();
            var execution = new ExplorationEventExecution(Create(
                new ExplorationEventPage("a", "", "", new IExplorationEventEffect[] { new Damage(6), after }, Continue(End))));
            var step = execution.Start(context);
            Assert.That(execution.State, Is.EqualTo(ExplorationEventExecutionState.Defeated));
            Assert.That(after.Applied, Is.Zero);
            Assert.That(step.Entries.Last().Kind, Is.EqualTo(ExplorationEventStepEntryKind.Defeated));
            Assert.That(execution.TryContinue(context, -1, out _, out _), Is.False);
        }

        [Test]
        public void PageVisitLimit_EndsTheEventWithAnError()
        {
            var context = new Context();
            var execution = new ExplorationEventExecution(Create(Page("loop", Continue("loop"))));
            execution.Start(context);
            var guard = 0;
            while (!execution.IsFinished && guard++ < 200) execution.TryContinue(context, -1, out _, out _);
            Assert.That(execution.State, Is.EqualTo(ExplorationEventExecutionState.Completed));
            Assert.That(execution.TotalPageVisits, Is.EqualTo(ExplorationEventLimits.MaximumPageVisits));
            Assert.That(execution.Error, Is.Not.Empty);
        }

        [Test]
        public void RepeatedInput_AppliesEffectsOnce()
        {
            var context = new Context();
            var counter = new Counter();
            var execution = new ExplorationEventExecution(Create(Page("a", Continue("b", counter)), Page("b", Continue(End))));
            execution.Start(context);
            var step = execution.AppliedStepCount;
            Assert.That(execution.TryContinue(context, step, out _, out _), Is.True);
            Assert.That(execution.TryContinue(context, step, out _, out var reason), Is.False, "the same input for the old step is stale");
            Assert.That(reason, Does.Contain("step"));
            Assert.That(counter.Applied, Is.EqualTo(1));
        }

        [Test]
        public void InputsThatDoNotFitThePageAreRejected()
        {
            var context = new Context();
            var execution = new ExplorationEventExecution(Create(Page("a", Continue(End))));
            Assert.That(execution.TryContinue(context, -1, out _, out _), Is.False, "not started");
            execution.Start(context);
            Assert.That(execution.TryChoose(context, 0, -1, out _, out _), Is.False);
            Assert.That(execution.TryBeginSlotCheck(context, -1, out _, out _), Is.False);
            Assert.That(execution.State, Is.EqualTo(ExplorationEventExecutionState.AwaitingInput));
        }

        [Test]
        public void EffectFailures_AreSkippedAndTheEventContinues()
        {
            var context = new Context();
            var counter = new Counter();
            var execution = new ExplorationEventExecution(Create(new ExplorationEventPage("a", "", "",
                new IExplorationEventEffect[] { new Throwing(), new Skipping(), counter }, Continue(End))));
            var step = execution.Start(context);
            Assert.That(step.Entries.Select(entry => entry.Kind), Is.EqualTo(new[]
                { ExplorationEventStepEntryKind.EffectFailed, ExplorationEventStepEntryKind.EffectSkipped, ExplorationEventStepEntryKind.EffectApplied }));
            Assert.That(counter.Applied, Is.EqualTo(1));
        }

        [Test]
        public void AllOptionsDisabled_CanOnlyEndTheEvent()
        {
            var context = new Context();
            var execution = new ExplorationEventExecution(Create(Page("a", new ExplorationEventChoice(new[]
                { new ExplorationEventChoiceOption("locked", new IExplorationEventCondition[] { new Never() }, new ExplorationEventBranch(null, End)) }))));
            execution.Start(context);
            Assert.That(execution.TryEndWithoutOptions(context, execution.AppliedStepCount, out _, out var reason), Is.True, reason);
            Assert.That(execution.State, Is.EqualTo(ExplorationEventExecutionState.Completed));
            Assert.That(Create(Page("a", new ExplorationEventChoice(new[]
                { new ExplorationEventChoiceOption("locked", new IExplorationEventCondition[] { new Never() }, new ExplorationEventBranch(null, End)) }))).CollectWarnings(), Is.Not.Empty);
        }

        [Test]
        public void Validation_RejectsBrokenEvents()
        {
            Assert.That(TryCreate("start", new[] { Page("start", Continue("missing")) }, out var reason), Is.False);
            Assert.That(reason, Does.Contain("missing"));
            Assert.That(TryCreate("nope", new[] { Page("start", Continue(End)) }, out _), Is.False, "unknown start page");
            Assert.That(TryCreate("start", new[] { Page("start", Continue(End)), Page("start", Continue(End)) }, out reason), Is.False);
            Assert.That(reason, Does.Contain("duplicated"));
            Assert.That(TryCreate("start", new[] { Page("start", null) }, out _), Is.False, "no interaction");
            Assert.That(TryCreate("start", new[] { Page("start", new ExplorationEventChoice(null)) }, out _), Is.False, "no option");
            Assert.That(TryCreate("start", new[] { Page("start", Continue(End, new Invalid())) }, out reason), Is.False);
            Assert.That(reason, Does.Contain("invalid on purpose"));
            Assert.That(TryCreate("start", new[] { Page("start", Continue(End, (IExplorationEventEffect)null)) }, out _), Is.False, "null effect");
            Assert.That(TryCreate("start", new[] { Page("start", new ExplorationEventSlotCheck("s", true,
                new[] { new ExplorationEventSlotOutcome(9, 3, new ExplorationEventBranch(null, End)) }, new ExplorationEventBranch(null, End))) }, out reason), Is.False);
            Assert.That(reason, Does.Contain("greater than max"));
        }

        // ---------- Helpers ----------

        private static ExplorationEvent Create(params ExplorationEventPage[] pages)
        {
            Assert.That(ExplorationEvent.TryCreate("event.test", "Test", "", pages[0].PageId, pages, out var created, out var reason), Is.True, reason);
            return created;
        }

        private static bool TryCreate(string start, ExplorationEventPage[] pages, out string reason) =>
            ExplorationEvent.TryCreate("event.test", "Test", "", start, pages, out _, out reason);

        private static ExplorationEventPage Page(string id, IExplorationEventInteraction interaction) => new(id, "", "", null, interaction);
        private static ExplorationEventContinue Continue(string next, params IExplorationEventEffect[] effects) => new("next", new ExplorationEventBranch(effects, next));

        private sealed class Context : IExplorationEventContext
        {
            public HealthState Health = new(30);
            public bool TryGetHealth(out HealthState health) { health = Health; return health != null; }
            public bool TryGetSlotMachine(out SlotMachineModel machine) { machine = null; return false; }
            public bool TryGetRunes(out ExplorationEventRuneAccess runes) { runes = null; return false; }
            public bool TryGetFortuneCritical(out IFortuneCriticalRuleStore fortuneCritical) { fortuneCritical = null; return false; }
        }

        private sealed class Counter : IExplorationEventEffect
        {
            public int Applied { get; private set; }
            public bool Validate(out string reason) { reason = string.Empty; return true; }
            public ExplorationEventEffectResult Apply(IExplorationEventContext context) { Applied++; return ExplorationEventEffectResult.Applied("counted"); }
        }

        private sealed class Damage : IExplorationEventEffect
        {
            private readonly int amount;
            public Damage(int value) => amount = value;
            public bool Validate(out string reason) { reason = string.Empty; return true; }
            public ExplorationEventEffectResult Apply(IExplorationEventContext context)
            {
                context.TryGetHealth(out var health);
                health.ApplyDamage(amount);
                return ExplorationEventEffectResult.Applied();
            }
        }

        private sealed class Throwing : IExplorationEventEffect
        {
            public bool Validate(out string reason) { reason = string.Empty; return true; }
            public ExplorationEventEffectResult Apply(IExplorationEventContext context) => throw new InvalidOperationException("boom");
        }

        private sealed class Skipping : IExplorationEventEffect
        {
            public bool Validate(out string reason) { reason = string.Empty; return true; }
            public ExplorationEventEffectResult Apply(IExplorationEventContext context) => ExplorationEventEffectResult.Skipped("not available");
        }

        private sealed class Invalid : IExplorationEventEffect
        {
            public bool Validate(out string reason) { reason = "invalid on purpose"; return false; }
            public ExplorationEventEffectResult Apply(IExplorationEventContext context) => ExplorationEventEffectResult.Applied();
        }

        private sealed class Never : IExplorationEventCondition
        {
            public bool Validate(out string reason) { reason = string.Empty; return true; }
            public bool IsSatisfied(IExplorationEventContext context, out string reason) { reason = "never"; return false; }
        }

        private sealed class FailingPicker : IExplorationNodeContentPicker
        {
            public bool TryPick(ExplorationNodeContentRequest request, IExplorationRandomSource random, out string contentId) { contentId = string.Empty; return false; }
        }

        private sealed class Fixed : IExplorationRandomSource
        {
            private readonly int[] values;
            private int index;
            public Fixed(params int[] values) => this.values = values;
            public int Next(int exclusiveMaximum) => values[index++ % values.Length] % exclusiveMaximum;
        }
    }
}
