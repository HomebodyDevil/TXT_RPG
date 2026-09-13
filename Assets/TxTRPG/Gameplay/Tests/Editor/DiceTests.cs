using System;
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
        public void Create_ConfigurableEffectFaces(int faceCount)
        {
            var dice = new ConfigurableDice(CreateFaces(faceCount), 1, faceCount);
            Assert.That(dice.FaceCount, Is.EqualTo(faceCount));
            Assert.That(dice.GetFace(0).EffectKind, Is.EqualTo(DiceEffectKind.Attack));
            Assert.That(dice.GetFace(faceCount - 1).Amount, Is.EqualTo(faceCount));
        }

        [Test]
        public void Roll_SelectsByIndex_AndPreservesDuplicateFaces()
        {
            var duplicate = new DiceFace(DiceEffectKind.Heal, 2);
            var dice = new ConfigurableDice(new[] { duplicate, duplicate, new DiceFace(DiceEffectKind.Attack, 3) }, 1, 3);
            for (var index = 0; index < dice.FaceCount; index++)
            {
                var result = dice.Roll(new RecordingIndexSource(index));
                Assert.That(result.FaceIndex, Is.EqualTo(index));
                Assert.That(result.EffectKind, Is.EqualTo(dice.GetFace(index).EffectKind));
                Assert.That(result.Amount, Is.EqualTo(dice.GetFace(index).Amount));
            }
        }

        [Test]
        public void ResolveFace_DoesNotConsumeRandom_AndMatchesRollResult()
        {
            var dice = new ConfigurableDice(CreateFaces(4), 1, 4);
            var resolved = dice.ResolveFace(2);
            var source = new RecordingIndexSource(2);
            var rolled = dice.Roll(source);
            Assert.That(source.CallCount, Is.EqualTo(1));
            Assert.That(resolved.FaceIndex, Is.EqualTo(rolled.FaceIndex));
            Assert.That(resolved.EffectKind, Is.EqualTo(rolled.EffectKind));
            Assert.That(resolved.Amount, Is.EqualTo(rolled.Amount));
        }

        [Test]
        public void RuntimeChanges_AreAtomic()
        {
            var dice = new ConfigurableDice(CreateFaces(4), 1, 8);
            dice.SetFace(0, new DiceFace(DiceEffectKind.Heal, 8));
            Assert.That(dice.ResolveFace(0).EffectKind, Is.EqualTo(DiceEffectKind.Heal));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                dice.ReplaceConfiguration(new[] { new DiceFace(DiceEffectKind.Attack, 0) }, 1, 2));
            Assert.That(dice.FaceCount, Is.EqualTo(4));
            Assert.That(dice.MaximumValue, Is.EqualTo(8));
        }

        [Test]
        public void InvalidEffectsAmountsAndIndices_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurableDice(null, 1, 6));
            Assert.Throws<ArgumentException>(() => new ConfigurableDice(Array.Empty<DiceFace>(), 1, 6));
            Assert.Throws<ArgumentException>(() => new ConfigurableDice(CreateFaces(1), 2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ConfigurableDice(new[] { new DiceFace(DiceEffectKind.Unknown, 1) }, 1, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ConfigurableDice(new[] { new DiceFace((DiceEffectKind)99, 1) }, 1, 6));
            var dice = new ConfigurableDice(CreateFaces(2), 1, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => dice.ResolveFace(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => dice.SetFace(2, new DiceFace(DiceEffectKind.Attack, 1)));
        }

        [Test]
        public void InputIsCopied_AndRollResultRemainsSnapshot()
        {
            var source = CreateFaces(2);
            var dice = new ConfigurableDice(source, 1, 6);
            var result = dice.ResolveFace(1);
            source[1] = new DiceFace(DiceEffectKind.Attack, 6);
            dice.SetFace(1, new DiceFace(DiceEffectKind.Attack, 5));
            Assert.That(result.EffectKind, Is.EqualTo(DiceEffectKind.Heal));
            Assert.That(result.Amount, Is.EqualTo(2));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void Roll_RejectsRandomSourceContractViolations(int returnedIndex)
        {
            var dice = new ConfigurableDice(CreateFaces(2), 1, 2);
            Assert.Throws<InvalidOperationException>(() => dice.Roll(new RecordingIndexSource(returnedIndex)));
        }

        private static DiceFace[] CreateFaces(int count)
        {
            var faces = new DiceFace[count];
            for (var i = 0; i < count; i++)
                faces[i] = new DiceFace(i % 2 == 0 ? DiceEffectKind.Attack : DiceEffectKind.Heal, i + 1);
            return faces;
        }

        private sealed class RecordingIndexSource : IRandomIndexSource
        {
            private readonly int result;
            public RecordingIndexSource(int result) => this.result = result;
            public int CallCount { get; private set; }
            public int NextIndex(int exclusiveUpperBound) { CallCount++; return result; }
        }
    }
}
