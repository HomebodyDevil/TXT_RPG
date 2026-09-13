using System;
using System.Collections.Generic;
using NUnit.Framework;
using TxTRPG.Gameplay.Dice;
using ConfigurableDice = TxTRPG.Gameplay.Dice.Dice;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class DiceTests
    {
        [TestCase(4)]
        [TestCase(6)]
        [TestCase(8)]
        public void Create_ConfigurableFaceCounts(int faceCount)
        {
            var faces = new int[faceCount];
            for (var index = 0; index < faces.Length; index++) faces[index] = index + 1;
            var dice = new ConfigurableDice(faces, 1, faceCount);

            Assert.That(dice.FaceCount, Is.EqualTo(faceCount));
            Assert.That(dice.GetFaceValue(0), Is.EqualTo(1));
            Assert.That(dice.GetFaceValue(faceCount - 1), Is.EqualTo(faceCount));
        }

        [Test]
        public void Roll_SelectsFacesByIndexAndKeepsDuplicates()
        {
            var dice = new ConfigurableDice(new[] { 1, 1, 3, 5 }, 1, 5);
            for (var index = 0; index < dice.FaceCount; index++)
            {
                var source = new RecordingIndexSource(index);
                var result = dice.Roll(source);
                Assert.That(source.RequestedUpperBound, Is.EqualTo(4));
                Assert.That(result.FaceIndex, Is.EqualTo(index));
                Assert.That(result.Value, Is.EqualTo(dice.GetFaceValue(index)));
            }
        }

        [Test]
        public void RuntimeChanges_AreAtomicAndAffectFollowingRolls()
        {
            var dice = new ConfigurableDice(new[] { 1, 2, 3, 4 }, 1, 8);
            dice.SetFaceValue(0, 8);
            Assert.That(dice.Roll(new RecordingIndexSource(0)).Value, Is.EqualTo(8));

            dice.ReplaceFaces(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            Assert.That(dice.FaceCount, Is.EqualTo(8));
            Assert.That(dice.Roll(new RecordingIndexSource(7)).Value, Is.EqualTo(8));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                dice.ReplaceConfiguration(new[] { 0, 1 }, 1, 2));
            Assert.That(dice.FaceCount, Is.EqualTo(8));
            Assert.That(dice.MinimumValue, Is.EqualTo(1));
            Assert.That(dice.MaximumValue, Is.EqualTo(8));
        }

        [Test]
        public void RangeChanges_RejectInvalidExistingFacesWithoutMutation()
        {
            var dice = new ConfigurableDice(new[] { -2, 0, 2 }, -2, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => dice.SetAllowedRange(-1, 2));
            Assert.That(dice.MinimumValue, Is.EqualTo(-2));
            Assert.That(dice.MaximumValue, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { -2, 0, 2 }, dice.FaceValues);

            dice.ReplaceConfiguration(new[] { 0, 1 }, 0, 1);
            CollectionAssert.AreEqual(new[] { 0, 1 }, dice.FaceValues);
        }

        [Test]
        public void InvalidConstructionAndIndices_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurableDice(null, 1, 6));
            Assert.Throws<ArgumentException>(() => new ConfigurableDice(Array.Empty<int>(), 1, 6));
            Assert.Throws<ArgumentException>(() => new ConfigurableDice(new[] { 1 }, 2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConfigurableDice(new[] { 7 }, 1, 6));

            var dice = new ConfigurableDice(new[] { int.MinValue, 0, int.MaxValue }, int.MinValue, int.MaxValue);
            Assert.Throws<ArgumentOutOfRangeException>(() => dice.GetFaceValue(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => dice.SetFaceValue(3, 0));
            Assert.That(dice.GetFaceValue(0), Is.EqualTo(int.MinValue));
            Assert.That(dice.GetFaceValue(2), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void InputAndInstances_DoNotShareMutableFaceState()
        {
            var sourceFaces = new[] { 1, 2, 3, 4 };
            var first = new ConfigurableDice(sourceFaces, 1, 6);
            var second = new ConfigurableDice(sourceFaces, 1, 6);
            sourceFaces[0] = 6;
            first.SetFaceValue(1, 6);

            Assert.That(first.GetFaceValue(0), Is.EqualTo(1));
            Assert.That(second.GetFaceValue(0), Is.EqualTo(1));
            Assert.That(second.GetFaceValue(1), Is.EqualTo(2));
        }

        [Test]
        public void RollResult_RemainsAValueSnapshotAfterConfigurationChanges()
        {
            var dice = new ConfigurableDice(new[] { 2, 4 }, 1, 6);
            var result = dice.Roll(new RecordingIndexSource(1));
            dice.SetFaceValue(1, 6);

            Assert.That(result.FaceIndex, Is.EqualTo(1));
            Assert.That(result.Value, Is.EqualTo(4));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void Roll_RejectsRandomSourceContractViolations(int returnedIndex)
        {
            var dice = new ConfigurableDice(new[] { 1, 2 }, 1, 2);
            Assert.Throws<InvalidOperationException>(() =>
                dice.Roll(new RecordingIndexSource(returnedIndex)));
        }

        [Test]
        public void SystemRandomSource_AlwaysReturnsRequestedRange()
        {
            var source = new SystemRandomIndexSource(12345);
            for (var index = 0; index < 500; index++)
            {
                var result = source.NextIndex(6);
                Assert.That(result, Is.GreaterThanOrEqualTo(0).And.LessThan(6));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => source.NextIndex(0));
        }

        private sealed class RecordingIndexSource : IRandomIndexSource
        {
            private readonly int result;

            public RecordingIndexSource(int result)
            {
                this.result = result;
            }

            public int RequestedUpperBound { get; private set; }

            public int NextIndex(int exclusiveUpperBound)
            {
                RequestedUpperBound = exclusiveUpperBound;
                return result;
            }
        }
    }
}
