using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Gameplay.SlotMachine;
using SlotMachineModel = TxTRPG.Gameplay.SlotMachine.SlotMachine;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class SlotMachineTests
    {
        private static readonly int[] OneToSix = { 1, 2, 3, 4, 5, 6 };

        private static SlotMachineModel DefaultMachine() => new(new[]
        {
            SlotReel.FromValues("reel.1", OneToSix),
            SlotReel.FromValues("reel.2", OneToSix),
            SlotReel.FromValues("reel.3", OneToSix)
        });

        [Test]
        public void Spin_DrawsOncePerReelInOrderAndTotalsThePayline()
        {
            var machine = DefaultMachine();
            var random = new SequenceRandom(2, 4, 1);
            var result = machine.Spin(random);

            Assert.That(random.Bounds, Is.EqualTo(new[] { 6, 6, 6 }));
            Assert.That(result.Reels.Select(r => r.ReelId), Is.EqualTo(new[] { "reel.1", "reel.2", "reel.3" }));
            Assert.That(result.Reels.Select(r => r.PaylineValue), Is.EqualTo(new[] { 3, 5, 2 }));
            Assert.That(result.PaylineTotal, Is.EqualTo(10));
            Assert.That(result.Reels[0].Above.Value, Is.EqualTo(2));
            Assert.That(result.Reels[0].Below.Value, Is.EqualTo(4));
        }

        [Test]
        public void SpinResult_WrapsNeighboursAndIsIndependentOfLaterChanges()
        {
            var machine = DefaultMachine();
            var result = machine.Spin(new SequenceRandom(0, 5, 3));
            Assert.That(result.Reels[0].Above.Value, Is.EqualTo(6), "the cell above the first cell is the last one");
            Assert.That(result.Reels[1].Below.Value, Is.EqualTo(1), "the cell below the last cell is the first one");

            Assert.That(machine.TrySetSymbol("reel.1", 0, 9, out var reason), Is.True, reason);
            Assert.That(machine.TryRemoveReel("reel.3", out reason), Is.True, reason);
            Assert.That(result.Reels.Count, Is.EqualTo(3));
            Assert.That(result.Reels[0].PaylineValue, Is.EqualTo(1));
            Assert.That(result.Reels[0].Strip.Select(s => s.Value), Is.EqualTo(OneToSix));
        }

        [Test]
        public void ReelChanges_AreAtomicAndRaiseChanged()
        {
            var machine = DefaultMachine();
            var changes = 0;
            machine.Changed += () => changes++;

            Assert.That(machine.TryAddReel(SlotReel.FromValues("reel.4", new[] { 1, 1, 2 }), out var reason), Is.True, reason);
            Assert.That(machine.ReelCount, Is.EqualTo(4));
            Assert.That(machine.TryAddSymbol("reel.4", 7, out reason), Is.True, reason);
            Assert.That(machine.TryInsertSymbol("reel.4", 0, 0, out reason), Is.True, reason);
            Assert.That(machine.FindReel("reel.4").Strip.Select(s => s.Value), Is.EqualTo(new[] { 0, 1, 1, 2, 7 }));
            Assert.That(machine.TryReplaceReel(SlotReel.FromValues("reel.2", new[] { 10 }), out reason), Is.True, reason);
            Assert.That(machine.TryReplaceStrip("reel.3", new[] { 4, 4 }, out reason), Is.True, reason);
            Assert.That(machine.TryRemoveSymbol("reel.3", 0, out reason), Is.True, reason);
            Assert.That(machine.TryRemoveReel("reel.4", out reason), Is.True, reason);
            Assert.That(changes, Is.EqualTo(7));
            Assert.That(machine.Reels.Select(r => r.Id), Is.EqualTo(new[] { "reel.1", "reel.2", "reel.3" }));
        }

        [Test]
        public void InvalidChanges_AreRejectedAndKeepThePreviousConfiguration()
        {
            var machine = DefaultMachine();
            var changes = 0;
            machine.Changed += () => changes++;
            var before = machine.Reels.ToArray();

            Assert.That(machine.TryAddReel(SlotReel.FromValues("reel.1", OneToSix), out var reason), Is.False);
            Assert.That(reason, Does.Contain("duplicated"));
            Assert.That(machine.TrySetSymbol("reel.1", 0, -1, out _), Is.False, "negative value");
            Assert.That(machine.TrySetSymbol("reel.1", 6, 1, out _), Is.False, "index out of range");
            Assert.That(machine.TryReplaceStrip("reel.2", Array.Empty<int>(), out _), Is.False, "empty strip");
            Assert.That(machine.TryRemoveSymbol("missing", 0, out _), Is.False, "unknown reel");
            Assert.That(machine.TryAddReel(SlotReel.FromValues("reel.4", new[] { int.MaxValue }), out reason), Is.False, "overflowing total");
            Assert.That(reason, Does.Contain("range"));
            Assert.That(machine.TryRemoveReel("reel.1", out _), Is.True);
            Assert.That(machine.TryRemoveReel("reel.2", out _), Is.True);
            Assert.That(machine.TryRemoveReel("reel.3", out reason), Is.False, "the last reel stays");
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(machine.Reels.Single(), Is.SameAs(before[2]));

            Assert.Throws<ArgumentException>(() => SlotReel.FromValues("empty", Array.Empty<int>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => SlotReel.FromValues("negative", new[] { 1, -2 }));
            Assert.Throws<ArgumentException>(() => new SlotMachineModel(Array.Empty<SlotReel>()));
            Assert.Throws<ArgumentException>(() => new SlotMachineModel(new[] { SlotReel.FromValues("a", OneToSix), SlotReel.FromValues("a", OneToSix) }));
        }

        [Test]
        public void RepeatedValues_AreDrawnPerCell()
        {
            var machine = new SlotMachineModel(new[] { SlotReel.FromValues("weighted", new[] { 1, 1, 1, 6 }) });
            var counts = new Dictionary<int, int>();
            var random = new SystemRandomIndexProvider(1234);
            var stops = new int[4];
            for (var i = 0; i < 4000; i++)
            {
                var stop = machine.Spin(random).Reels[0];
                stops[stop.StopIndex]++;
                counts[stop.PaylineValue] = counts.TryGetValue(stop.PaylineValue, out var count) ? count + 1 : 1;
            }
            Assert.That(stops, Has.All.InRange(850, 1150), "every cell is equally likely");
            Assert.That(counts[1], Is.InRange(2700, 3300), "a value on three cells is drawn three times as often");
        }

        [Test]
        public void Spin_RejectsAnOutOfRangeRandomIndex()
        {
            Assert.Throws<InvalidOperationException>(() => DefaultMachine().Spin(new SequenceRandom(6)));
            Assert.Throws<ArgumentNullException>(() => DefaultMachine().Spin(null));
        }

        private sealed class SequenceRandom : IRandomIndexProvider
        {
            private readonly Queue<int> values;
            public SequenceRandom(params int[] sequence) => values = new Queue<int>(sequence);
            public List<int> Bounds { get; } = new();
            public int NextIndex(int exclusiveUpperBound)
            {
                Bounds.Add(exclusiveUpperBound);
                return values.Dequeue();
            }
        }
    }
}
