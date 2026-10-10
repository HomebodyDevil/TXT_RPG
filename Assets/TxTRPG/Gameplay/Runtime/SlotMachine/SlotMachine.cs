using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.SlotMachine
{
    // One reel cell. Wrapped instead of a bare int so special symbols or image keys can be added later.
    public readonly struct SlotSymbol : IEquatable<SlotSymbol>
    {
        public SlotSymbol(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Slot symbol values cannot be negative.");
            Value = value;
        }

        public int Value { get; }
        public bool Equals(SlotSymbol other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SlotSymbol other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }

    // Immutable reel: a stable ID and an ordered strip. Every cell stops with the same probability, so a repeated value
    // acts as a weight. The With* methods validate and return a new reel; the original is never changed.
    public sealed class SlotReel
    {
        private readonly SlotSymbol[] strip;

        public SlotReel(string id, IEnumerable<SlotSymbol> symbols)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("A reel requires a non-empty id.", nameof(id)) : id.Trim();
            if (symbols == null) throw new ArgumentNullException(nameof(symbols));
            strip = new List<SlotSymbol>(symbols).ToArray();
            if (strip.Length == 0) throw new ArgumentException($"Reel '{Id}' requires at least one symbol.", nameof(symbols));
        }

        public static SlotReel FromValues(string id, IEnumerable<int> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var symbols = new List<SlotSymbol>();
            foreach (var value in values) symbols.Add(new SlotSymbol(value));
            return new SlotReel(id, symbols);
        }

        public string Id { get; }
        public int Count => strip.Length;
        public IReadOnlyList<SlotSymbol> Strip => Array.AsReadOnly(strip);
        public SlotSymbol this[int index] => strip[index];

        public int MaximumValue
        {
            get
            {
                var maximum = 0;
                foreach (var symbol in strip) maximum = Math.Max(maximum, symbol.Value);
                return maximum;
            }
        }

        public SlotReel WithSymbol(int index, SlotSymbol symbol)
        {
            if (index < 0 || index >= strip.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var copy = (SlotSymbol[])strip.Clone();
            copy[index] = symbol;
            return new SlotReel(Id, copy);
        }

        public SlotReel WithInsertedSymbol(int index, SlotSymbol symbol)
        {
            if (index < 0 || index > strip.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var copy = new List<SlotSymbol>(strip);
            copy.Insert(index, symbol);
            return new SlotReel(Id, copy);
        }

        public SlotReel WithoutSymbol(int index)
        {
            if (index < 0 || index >= strip.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (strip.Length == 1) throw new InvalidOperationException($"Reel '{Id}' must keep at least one symbol.");
            var copy = new List<SlotSymbol>(strip);
            copy.RemoveAt(index);
            return new SlotReel(Id, copy);
        }

        public SlotReel WithStrip(IEnumerable<SlotSymbol> symbols) => new SlotReel(Id, symbols);
    }

    public sealed class SlotReelStop
    {
        private readonly SlotSymbol[] strip;

        internal SlotReelStop(SlotReel reel, int stopIndex)
        {
            ReelId = reel.Id;
            StopIndex = stopIndex;
            strip = new SlotSymbol[reel.Count];
            for (var i = 0; i < strip.Length; i++) strip[i] = reel[i];
        }

        public string ReelId { get; }
        public int StopIndex { get; }
        public SlotSymbol Payline => strip[StopIndex];
        public int PaylineValue => Payline.Value;
        // Copy of the reel strip at spin time, so the cells above and below the payline can be drawn from the result alone.
        public IReadOnlyList<SlotSymbol> Strip => Array.AsReadOnly(strip);
        public SlotSymbol Above => strip[(StopIndex - 1 + strip.Length) % strip.Length];
        public SlotSymbol Below => strip[(StopIndex + 1) % strip.Length];
    }

    // Immutable snapshot of one spin. Later configuration changes never affect it.
    public sealed class SlotSpinResult
    {
        private readonly SlotReelStop[] stops;

        internal SlotSpinResult(SlotReelStop[] reelStops)
        {
            stops = reelStops;
            long total = 0;
            foreach (var stop in stops) total += stop.PaylineValue;
            PaylineTotal = checked((int)total);
        }

        public IReadOnlyList<SlotReelStop> Reels => Array.AsReadOnly(stops);
        public int PaylineTotal { get; }
    }

    // Ordered reels with atomic, validated changes. A failed change keeps the previous configuration.
    public sealed class SlotMachine
    {
        private List<SlotReel> reels;

        public SlotMachine(IEnumerable<SlotReel> initialReels)
        {
            if (initialReels == null) throw new ArgumentNullException(nameof(initialReels));
            var candidate = new List<SlotReel>(initialReels);
            if (!Validate(candidate, out var reason)) throw new ArgumentException(reason, nameof(initialReels));
            reels = candidate;
        }

        public IReadOnlyList<SlotReel> Reels => reels.AsReadOnly();
        public int ReelCount => reels.Count;
        public event Action Changed;

        public SlotReel FindReel(string reelId)
        {
            var index = IndexOf(reelId);
            return index < 0 ? null : reels[index];
        }

        // Picks one stop per reel, in reel order, with one random draw each.
        public SlotSpinResult Spin(IRandomIndexProvider random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var stops = new SlotReelStop[reels.Count];
            for (var i = 0; i < reels.Count; i++)
            {
                var reel = reels[i];
                var index = random.NextIndex(reel.Count);
                if (index < 0 || index >= reel.Count)
                    throw new InvalidOperationException($"The random source returned {index} for reel '{reel.Id}' with {reel.Count} cells.");
                stops[i] = new SlotReelStop(reel, index);
            }
            return new SlotSpinResult(stops);
        }

        public bool TryAddReel(SlotReel reel, out string reason) => TryAddReel(reel, reels.Count, out reason);

        public bool TryAddReel(SlotReel reel, int index, out string reason)
        {
            if (reel == null) { reason = "A reel is required."; return false; }
            if (index < 0 || index > reels.Count) { reason = $"Reel index {index} is out of range."; return false; }
            var candidate = new List<SlotReel>(reels);
            candidate.Insert(index, reel);
            return TryCommit(candidate, out reason);
        }

        public bool TryRemoveReel(string reelId, out string reason)
        {
            var index = IndexOf(reelId);
            if (index < 0) { reason = $"Reel '{reelId}' does not exist."; return false; }
            if (reels.Count == 1) { reason = "The slot machine must keep at least one reel."; return false; }
            var candidate = new List<SlotReel>(reels);
            candidate.RemoveAt(index);
            return TryCommit(candidate, out reason);
        }

        // Replaces the reel with the same ID as the given reel, in place.
        public bool TryReplaceReel(SlotReel reel, out string reason)
        {
            if (reel == null) { reason = "A reel is required."; return false; }
            var index = IndexOf(reel.Id);
            if (index < 0) { reason = $"Reel '{reel.Id}' does not exist."; return false; }
            var candidate = new List<SlotReel>(reels) { [index] = reel };
            return TryCommit(candidate, out reason);
        }

        public bool TryReplaceAll(IEnumerable<SlotReel> replacement, out string reason)
        {
            if (replacement == null) { reason = "Reels are required."; return false; }
            return TryCommit(new List<SlotReel>(replacement), out reason);
        }

        public bool TrySetSymbol(string reelId, int index, int value, out string reason) =>
            TryChangeReel(reelId, reel => reel.WithSymbol(index, new SlotSymbol(value)), out reason);

        public bool TryAddSymbol(string reelId, int value, out string reason) =>
            TryChangeReel(reelId, reel => reel.WithInsertedSymbol(reel.Count, new SlotSymbol(value)), out reason);

        public bool TryInsertSymbol(string reelId, int index, int value, out string reason) =>
            TryChangeReel(reelId, reel => reel.WithInsertedSymbol(index, new SlotSymbol(value)), out reason);

        public bool TryRemoveSymbol(string reelId, int index, out string reason) =>
            TryChangeReel(reelId, reel => reel.WithoutSymbol(index), out reason);

        public bool TryReplaceStrip(string reelId, IEnumerable<int> values, out string reason) =>
            TryChangeReel(reelId, reel => SlotReel.FromValues(reel.Id, values), out reason);

        public static bool Validate(IReadOnlyList<SlotReel> candidate, out string reason)
        {
            if (candidate == null || candidate.Count == 0) { reason = "The slot machine requires at least one reel."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            long maximumTotal = 0;
            for (var i = 0; i < candidate.Count; i++)
            {
                var reel = candidate[i];
                if (reel == null) { reason = $"Reel {i + 1} is null."; return false; }
                if (!ids.Add(reel.Id)) { reason = $"Reel id '{reel.Id}' is duplicated."; return false; }
                maximumTotal += reel.MaximumValue;
            }
            if (maximumTotal > int.MaxValue) { reason = "The largest possible payline total exceeds the supported range."; return false; }
            reason = string.Empty;
            return true;
        }

        private bool TryChangeReel(string reelId, Func<SlotReel, SlotReel> change, out string reason)
        {
            var index = IndexOf(reelId);
            if (index < 0) { reason = $"Reel '{reelId}' does not exist."; return false; }
            SlotReel changed;
            try { changed = change(reels[index]); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { reason = exception.Message; return false; }
            var candidate = new List<SlotReel>(reels) { [index] = changed };
            return TryCommit(candidate, out reason);
        }

        private bool TryCommit(List<SlotReel> candidate, out string reason)
        {
            if (!Validate(candidate, out reason)) return false;
            reels = candidate;
            Changed?.Invoke();
            return true;
        }

        private int IndexOf(string reelId)
        {
            var id = reelId?.Trim();
            for (var i = 0; i < reels.Count; i++)
                if (string.Equals(reels[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}
