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
                var random = new SequenceRandom(3, 4, 1);
                var rolls = new TemporaryPlayerDiceState(config).RollAll(random);
                Assert.That(rolls.Count, Is.EqualTo(3));
                Assert.That(rolls[0].DisplayName, Is.EqualTo("D4")); Assert.That(rolls[0].Result.Value, Is.EqualTo(4));
                Assert.That(rolls[1].DisplayName, Is.EqualTo("D6")); Assert.That(rolls[1].Result.Value, Is.EqualTo(5));
                Assert.That(rolls[2].DisplayName, Is.EqualTo("D8")); Assert.That(rolls[2].Result.Value, Is.EqualTo(2));
                Assert.That(random.CallCount, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(config); }
        }
        [Test]
        public void DisabledAndEmptyConfigurations_DoNotConsumeRandom()
        {
            var config = ScriptableObject.CreateInstance<TemporaryDiceConfiguration>();
            try { config.ConfigureForEditor(false, System.Array.Empty<TemporaryDieDefinition>()); var random = new SequenceRandom(); Assert.That(new TemporaryPlayerDiceState(config).RollAll(random), Is.Empty); Assert.That(random.CallCount, Is.Zero); }
            finally { Object.DestroyImmediate(config); }
        }
        [Test]
        public void DuplicateIdentifiers_AreRejected()
        {
            var config = ScriptableObject.CreateInstance<TemporaryDiceConfiguration>();
            try { config.ConfigureForEditor(true, new[] { Make("same", "D4", 4), Make("same", "D6", 6) }); Assert.Throws<System.InvalidOperationException>(() => new TemporaryPlayerDiceState(config)); }
            finally { Object.DestroyImmediate(config); }
        }
        private static TemporaryDieDefinition Make(string id,string name,int faces) { var d=new TemporaryDieDefinition(); d.ConfigureForEditor(id,name,faces); return d; }
        private sealed class SequenceRandom : IRandomIndexSource
        {
            private readonly int[] values; private int index; public SequenceRandom(params int[] values) => this.values=values; public int CallCount { get; private set; }
            public int NextIndex(int upper) { CallCount++; var value=values[index++]; Assert.That(value,Is.InRange(0,upper-1)); return value; }
        }
    }
}