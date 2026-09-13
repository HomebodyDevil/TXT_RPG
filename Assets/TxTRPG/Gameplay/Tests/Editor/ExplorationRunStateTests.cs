using System;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class ExplorationRunStateTests
    {
        [Test]
        public void Creation_RecordsOnlyRootAndInitialChoices()
        {
            var run = CreateRun(0, 99, 0);
            Assert.That(run.Nodes.Count, Is.EqualTo(4));
            Assert.That(run.CurrentChoices.Count, Is.EqualTo(3));
            Assert.That(run.Nodes.Single(item => item.Id == ExplorationRunState.RootNodeId).Depth, Is.EqualTo(-1));
            Assert.That(run.CurrentChoices.All(item => item.ParentId == ExplorationRunState.RootNodeId && item.Depth == 0), Is.True);
        }

        [Test]
        public void Select_RecordsActiveAndUnchosenAndRejectsStaleChoice()
        {
            var run = CreateRun(0, 99, 0); var choiceSet = run.CurrentChoiceSetId; var choices = run.CurrentChoices.ToArray();
            var selected = run.Select(choiceSet, choices[1].Id);
            Assert.That(selected.Status, Is.EqualTo(ExplorationNodeStatus.Active));
            Assert.That(choices.Where(item => item != selected).All(item => item.Status == ExplorationNodeStatus.Unchosen), Is.True);
            Assert.That(run.SelectedPath, Is.EqualTo(new[] { selected.Id }));
            Assert.Throws<InvalidOperationException>(() => run.Select(choiceSet, choices[0].Id));
        }

        [Test]
        public void Complete_GeneratesChildrenOnlyOnceUnderSelectedNode()
        {
            var run = CreateRun(0, 99, 0); var choices = run.CurrentChoices.ToArray(); var selected = run.Select(run.CurrentChoiceSetId, choices[0].Id);
            run.CompleteActive(selected.Id, "CombatVictory");
            Assert.That(run.Nodes.Count, Is.EqualTo(7));
            Assert.That(run.CurrentChoices.All(item => item.ParentId == selected.Id && item.Depth == 1), Is.True);
            Assert.That(choices[1].Status, Is.EqualTo(ExplorationNodeStatus.Unchosen));
            Assert.That(run.Nodes.All(item => item.ParentId != choices[1].Id), Is.True);
            Assert.Throws<InvalidOperationException>(() => run.CompleteActive(selected.Id, "duplicate"));
        }

        [Test]
        public void ThreeCompletions_PreservePathParentageAndUnchosenHistory()
        {
            var run = CreateRun(0, 99, 0, 99, 0, 99, 0, 99, 0);
            for (var depth = 0; depth < 3; depth++) { var node = run.Select(run.CurrentChoiceSetId, run.CurrentChoices[0].Id); run.CompleteActive(node.Id, "done"); }
            Assert.That(run.SelectedPath.Count, Is.EqualTo(3));
            for (var i = 1; i < run.SelectedPath.Count; i++) Assert.That(run.Nodes.Single(item => item.Id == run.SelectedPath[i]).ParentId, Is.EqualTo(run.SelectedPath[i - 1]));
            Assert.That(run.Nodes.Count(item => item.Status == ExplorationNodeStatus.Unchosen), Is.EqualTo(6));
        }

        [Test]
        public void SharedHealth_PersistsAcrossNodesWhilePlaceholderDoesNotChangeIt()
        {
            var health = new HealthState(30, 30); var run = CreateRun(health, 0, 99, 0, 99, 0, 99);
            var combat = run.Select(run.CurrentChoiceSetId, run.CurrentChoices[0].Id); health.ApplyDamage(7); run.CompleteActive(combat.Id, "CombatVictory");
            var placeholder = run.Select(run.CurrentChoiceSetId, run.CurrentChoices[0].Id); var before = health.Current; run.CompleteActive(placeholder.Id, "PlaceholderAcknowledged");
            Assert.That(health.Current, Is.EqualTo(before)); Assert.That(health.Current, Is.EqualTo(23));
        }

        [Test]
        public void Generator_AllowsDuplicateTypesAndRejectsInvalidConfiguration()
        {
            var generator = new ExplorationNodeGenerator(3, new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Combat, 50), new ExplorationNodeWeight(ExplorationNodeTypeIds.RecoveryUpgrade, 50) }, new FixedRandom(0, 0, 0));
            Assert.That(generator.Generate(), Is.All.EqualTo(ExplorationNodeTypeIds.Combat));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplorationNodeGenerator(0, new[] { new ExplorationNodeWeight("x", 1) }, new FixedRandom(0)));
            Assert.Throws<ArgumentException>(() => new ExplorationNodeGenerator(1, new[] { new ExplorationNodeWeight("x", 0) }, new FixedRandom(0)));
        }

        [Test]
        public void Fail_BlocksFurtherProgressAndPreservesFailureRecord()
        {
            var run = CreateRun(0, 99, 0); var node = run.Select(run.CurrentChoiceSetId, run.CurrentChoices[0].Id); run.FailActive(node.Id, "CombatDefeat");
            Assert.That(run.Phase, Is.EqualTo(ExplorationPhase.Failed)); Assert.That(node.Status, Is.EqualTo(ExplorationNodeStatus.Failed)); Assert.That(node.CompletionReason, Is.EqualTo("CombatDefeat"));
            Assert.Throws<InvalidOperationException>(() => run.CompleteActive(node.Id, "late"));
        }

        private static ExplorationRunState CreateRun(params int[] values) => CreateRun(new HealthState(30), values);
        private static ExplorationRunState CreateRun(HealthState health, params int[] values) => new("run-test", health, new ExplorationNodeGenerator(3, new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Combat, 50), new ExplorationNodeWeight(ExplorationNodeTypeIds.RecoveryUpgrade, 50) }, new FixedRandom(values)));
        private sealed class FixedRandom : IExplorationRandomSource { private readonly int[] values; private int index; public FixedRandom(params int[] values) => this.values = values; public int Next(int exclusiveMaximum) => values[index++ % values.Length]; }
    }
}