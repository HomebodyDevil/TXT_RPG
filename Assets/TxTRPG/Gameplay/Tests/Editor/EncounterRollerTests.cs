using System;
using System.Collections.Generic;
using NUnit.Framework;
using TxTRPG.Gameplay.Encounters;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class EncounterRollerTests
    {
        private static readonly EncounterRollSettings Even = new(.5f, .5f, 1f, 1f, 1f);

        [Test]
        public void NoSelection_UsesNoRandom()
        {
            var random = new Scripted();
            var roller = new EncounterRoller(random);
            Assert.That(roller.Roll(Array.Empty<string>(), Even).Outcome, Is.EqualTo(EncounterRollOutcome.NoElementsSelected));
            Assert.That(roller.Roll(null, Even).Outcome, Is.EqualTo(EncounterRollOutcome.NoElementsSelected));
            Assert.That(random.Bounds, Is.Empty);
        }

        [Test]
        public void ZeroWeights_UseNoRandom()
        {
            var random = new Scripted();
            var result = new EncounterRoller(random).Roll(new[] { "rune" }, new EncounterRollSettings(1f, 1f, 0f, 0f, 0f));
            Assert.That(result.Outcome, Is.EqualTo(EncounterRollOutcome.NoKindAvailable));
            Assert.That(result.ElementIds, Is.Empty);
            Assert.That(random.Bounds, Is.Empty);
        }

        [Test]
        public void OccurrenceChance_BoundariesAndResolution()
        {
            // p = 0 fails even on index 0; p = 1 succeeds even on the last index.
            var zero = new Scripted(0);
            Assert.That(new EncounterRoller(zero).Roll(new[] { "rune" }, Even.WithOccurrenceChance(0f)).Outcome, Is.EqualTo(EncounterRollOutcome.ChanceFailed));
            Assert.That(zero.Bounds, Is.EqualTo(new[] { EncounterRoller.ChanceResolution }), "a failed occurrence draws exactly once");

            var one = new Scripted(9999, 9999, 0, 0);
            Assert.That(new EncounterRoller(one).Roll(new[] { "rune" }, Even.WithOccurrenceChance(1f)).Outcome, Is.EqualTo(EncounterRollOutcome.Occurred));

            // p = 0.5 -> threshold 5000.
            Assert.That(new EncounterRoller(new Scripted(4999, 0, 0)).Roll(new[] { "rune" }, Even).Outcome, Is.EqualTo(EncounterRollOutcome.Occurred));
            Assert.That(new EncounterRoller(new Scripted(5000)).Roll(new[] { "rune" }, Even).Outcome, Is.EqualTo(EncounterRollOutcome.ChanceFailed));
        }

        [Test]
        public void Elements_AreIncludedIndependently_InInputOrder()
        {
            // occurrence pass, a in, b out, c in, kind index 0.
            var random = new Scripted(0, 100, 9000, 4999, 0);
            var result = new EncounterRoller(random).Roll(new[] { "a", "b", "c" }, Even);
            Assert.That(result.Outcome, Is.EqualTo(EncounterRollOutcome.Occurred));
            Assert.That(result.ElementIds, Is.EqualTo(new[] { "a", "c" }));
            Assert.That(random.Bounds, Is.EqualTo(new[] { 10000, 10000, 10000, 10000, 3000 }));
        }

        [Test]
        public void NothingIncluded_PicksOneUniformly()
        {
            var random = new Scripted(0, 9999, 9999, 9999, 2, 0);
            var result = new EncounterRoller(random).Roll(new[] { "a", "b", "c" }, Even);
            Assert.That(result.ElementIds, Is.EqualTo(new[] { "c" }));
            Assert.That(random.Bounds, Is.EqualTo(new[] { 10000, 10000, 10000, 10000, 3, 3000 }));
        }

        [Test]
        public void AllIncluded_KeepsInputOrder()
        {
            var result = new EncounterRoller(new Scripted(0, 0, 0, 0)).Roll(new[] { "z", "a" }, Even.WithElementInclusionChance(1f));
            Assert.That(result.ElementIds, Is.EqualTo(new[] { "z", "a" }));
        }

        [TestCase(0, EncounterKind.Combat)]
        [TestCase(999, EncounterKind.Combat)]
        [TestCase(1000, EncounterKind.Event)]
        [TestCase(1999, EncounterKind.Event)]
        [TestCase(2000, EncounterKind.Rest)]
        [TestCase(2999, EncounterKind.Rest)]
        public void Kind_FollowsWeightRanges(int index, EncounterKind expected)
        {
            var result = new EncounterRoller(new Scripted(0, 0, index)).Roll(new[] { "rune" }, Even);
            Assert.That(result.Kind, Is.EqualTo(expected));
        }

        [Test]
        public void ZeroWeightKind_IsNeverChosen()
        {
            var settings = new EncounterRollSettings(1f, 1f, 0f, 2f, 0f);
            for (var index = 0; index < 2000; index += 250)
            {
                var random = new Scripted(0, 0, index);
                var result = new EncounterRoller(random).Roll(new[] { "rune" }, settings);
                Assert.That(result.Kind, Is.EqualTo(EncounterKind.Event));
                Assert.That(random.Bounds[2], Is.EqualTo(2000));
            }
        }

        [Test]
        public void OccurrenceAndElementDraws_AreSeparateCalls()
        {
            // The inclusion check gets its own draw: the same small value that passed occurrence is not reused.
            var random = new Scripted(0, 9999, 0, 0);
            var result = new EncounterRoller(random).Roll(new[] { "a" }, new EncounterRollSettings(.5f, .5f, 1f, 1f, 1f));
            Assert.That(result.Outcome, Is.EqualTo(EncounterRollOutcome.Occurred));
            Assert.That(random.Bounds.Count, Is.EqualTo(4), "occurrence, inclusion, uniform fallback, kind");
            Assert.That(random.Bounds[0], Is.EqualTo(10000));
            Assert.That(random.Bounds[1], Is.EqualTo(10000));
            Assert.That(random.Bounds[2], Is.EqualTo(1));
        }

        [Test]
        public void Settings_AreClamped()
        {
            var settings = new EncounterRollSettings(float.NaN, 2f, -1f, float.NaN, 3.5f);
            Assert.That(settings.OccurrenceChance, Is.EqualTo(0f));
            Assert.That(settings.ElementInclusionChance, Is.EqualTo(1f));
            Assert.That(settings.CombatWeight, Is.EqualTo(0f));
            Assert.That(settings.EventWeight, Is.EqualTo(0f));
            Assert.That(settings.RestWeight, Is.EqualTo(3.5f));
            Assert.That(new EncounterRollSettings(-.5f, -1f, 1f, 1f, 1f).OccurrenceChance, Is.EqualTo(0f));
            var copy = settings.WithCombatWeight(2f);
            Assert.That(copy.CombatWeight, Is.EqualTo(2f));
            Assert.That(copy.RestWeight, Is.EqualTo(3.5f));
            Assert.That(settings.CombatWeight, Is.EqualTo(0f), "With* returns a copy");
            Assert.That(EncounterRollSettings.Default.OccurrenceChance, Is.EqualTo(.5f));
            Assert.That(EncounterRollSettings.Default.ElementInclusionChance, Is.EqualTo(.5f));
            Assert.That(EncounterRollSettings.Default.CombatWeight + EncounterRollSettings.Default.EventWeight + EncounterRollSettings.Default.RestWeight, Is.EqualTo(3f));
        }

        [Test]
        public void KindValues_AreFixed()
        {
            Assert.That((int)EncounterKind.Combat, Is.EqualTo(0));
            Assert.That((int)EncounterKind.Event, Is.EqualTo(1));
            Assert.That((int)EncounterKind.Rest, Is.EqualTo(2));
        }

        private sealed class Scripted : IRandomIndexProvider
        {
            private readonly Queue<int> values;
            public Scripted(params int[] values) => this.values = new Queue<int>(values);
            public List<int> Bounds { get; } = new();

            public int NextIndex(int exclusiveUpperBound)
            {
                Bounds.Add(exclusiveUpperBound);
                if (values.Count == 0) throw new InvalidOperationException("Unexpected random call.");
                var value = values.Dequeue();
                Assert.That(value, Is.InRange(0, exclusiveUpperBound - 1));
                return value;
            }
        }
    }
}
