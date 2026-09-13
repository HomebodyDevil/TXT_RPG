using NUnit.Framework;
using TxTRPG.Application.Dice;
using TxTRPG.Gameplay.Dice;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class TemporaryPlayerDiceStateTests
    {
        [Test]
        public void DefaultStyleConfiguration_RollsEveryDieOnceInStableOrder()
        {
            var config = ScriptableObject.CreateInstance<TemporaryDiceConfiguration>();
            try
            {
                config.ConfigureForEditor(true, new[] { Make("d4", "D4", 4), Make("d6", "D6", 6), Make("d8", "D8", 8) });
                var random = new SequenceRandom(2, 3, 0);
                var rolls = new TemporaryPlayerDiceState(config).RollAll(random);
                Assert.That(rolls.Count, Is.EqualTo(3));
                Assert.That(rolls[0].Result.EffectKind, Is.EqualTo(DiceEffectKind.Attack));
                Assert.That(rolls[0].Result.Amount, Is.EqualTo(3));
                Assert.That(rolls[1].Result.EffectKind, Is.EqualTo(DiceEffectKind.Heal));
                Assert.That(rolls[1].Result.Amount, Is.EqualTo(4));
                Assert.That(rolls[2].Result.EffectKind, Is.EqualTo(DiceEffectKind.Attack));
                Assert.That(rolls[2].Result.Amount, Is.EqualTo(1));
                Assert.That(random.CallCount, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void Formatter_MapsSupportedAndUnknownEffectsWithoutClaimingApplication()
        {
            var attack = new PlayerDieRoll("d4", "D4", 1, 4, new DiceRollResult(2, new DiceFace(DiceEffectKind.Attack, 3)));
            var unknown = new PlayerDieRoll("custom", "Custom", 2, 1, new DiceRollResult(0, new DiceFace(DiceEffectKind.Unknown, 9)));
            Assert.That(DiceRollStoryFormatter.Format(attack), Is.EqualTo("[주사위] 1번 주사위 (D4): 공격 3"));
            Assert.That(DiceRollStoryFormatter.Format(unknown), Does.Contain("알 수 없는 효과 9"));
            Assert.That(DiceRollStoryFormatter.Format(attack), Does.Not.Contain("적용"));
        }

        [Test]
        public void DisabledConfiguration_DoesNotConsumeRandom()
        {
            var config = ScriptableObject.CreateInstance<TemporaryDiceConfiguration>();
            try
            {
                config.ConfigureForEditor(false, System.Array.Empty<TemporaryDieDefinition>());
                var random = new SequenceRandom();
                Assert.That(new TemporaryPlayerDiceState(config).RollAll(random), Is.Empty);
                Assert.That(random.CallCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void DuplicateIdentifiers_AreRejected()
        {
            var config = ScriptableObject.CreateInstance<TemporaryDiceConfiguration>();
            try
            {
                config.ConfigureForEditor(true, new[] { Make("same", "D4", 4), Make("same", "D6", 6) });
                Assert.Throws<System.InvalidOperationException>(() => new TemporaryPlayerDiceState(config));
            }
            finally { Object.DestroyImmediate(config); }
        }

        private static TemporaryDieDefinition Make(string id, string name, int faces)
        {
            var definition = new TemporaryDieDefinition();
            definition.ConfigureForEditor(id, name, faces);
            return definition;
        }

        private sealed class SequenceRandom : IRandomIndexSource
        {
            private readonly int[] values;
            private int index;
            public SequenceRandom(params int[] values) => this.values = values;
            public int CallCount { get; private set; }
            public int NextIndex(int upper) { CallCount++; var value = values[index++]; Assert.That(value, Is.InRange(0, upper - 1)); return value; }
        }
    }
}
