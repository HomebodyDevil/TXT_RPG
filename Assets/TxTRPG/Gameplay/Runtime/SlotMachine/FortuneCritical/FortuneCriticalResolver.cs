using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.SlotMachine
{
    public sealed class FortuneCriticalReelResult
    {
        internal FortuneCriticalReelResult(string reelId, int baseValue, int reelMultiplier, int totalMultiplier, int cap, long finalValue)
        {
            ReelId = reelId;
            BaseValue = baseValue;
            ReelMultiplier = reelMultiplier;
            UncappedMultiplier = reelMultiplier * totalMultiplier;
            EffectiveMultiplier = Math.Min(cap, UncappedMultiplier);
            FinalValue = finalValue;
        }

        public string ReelId { get; }
        // Payline value of the reel before any multiplier.
        public int BaseValue { get; }
        // This reel's own Fortune Critical multiplier; 1 when it did not happen.
        public int ReelMultiplier { get; }
        // ReelMultiplier × total multiplier, before the cap.
        public int UncappedMultiplier { get; }
        // min(cap, ReelMultiplier × total multiplier): the multiplier actually applied to BaseValue.
        public int EffectiveMultiplier { get; }
        public long FinalValue { get; }
        public bool IsCritical => ReelMultiplier > 1;
        public bool IsCapped => UncappedMultiplier > EffectiveMultiplier;
    }

    // Immutable Fortune Critical snapshot of one spin. Later rule or reel changes never affect it.
    public sealed class FortuneCriticalResult
    {
        private readonly FortuneCriticalReelResult[] reels;

        internal FortuneCriticalResult(SlotSpinResult spin, FortuneCriticalReelResult[] reelResults, int totalMultiplier, int maximumEffectiveMultiplier)
        {
            Spin = spin;
            reels = reelResults;
            TotalMultiplier = totalMultiplier;
            MaximumEffectiveMultiplier = maximumEffectiveMultiplier;
            long total = 0;
            foreach (var reel in reels) total = checked(total + reel.FinalValue);
            // Throws OverflowException when the final total does not fit the supported range.
            FinalTotal = checked((int)total);
        }

        // Every multiplier is 1; the final total is the payline total.
        public static FortuneCriticalResult None(SlotSpinResult spin)
        {
            if (spin == null) throw new ArgumentNullException(nameof(spin));
            var results = new FortuneCriticalReelResult[spin.Reels.Count];
            for (var i = 0; i < results.Length; i++)
            {
                var stop = spin.Reels[i];
                results[i] = new FortuneCriticalReelResult(stop.ReelId, stop.PaylineValue, 1, 1, 1, stop.PaylineValue);
            }
            return new FortuneCriticalResult(spin, results, 1, FortuneCriticalLimits.MaximumEffectiveMultiplier);
        }

        public SlotSpinResult Spin { get; }
        public IReadOnlyList<FortuneCriticalReelResult> Reels => Array.AsReadOnly(reels);
        // The total's own Fortune Critical multiplier; 1 when it did not happen.
        public int TotalMultiplier { get; }
        public int MaximumEffectiveMultiplier { get; }
        public int BaseTotal => Spin.PaylineTotal;
        // Σ BaseValue × EffectiveMultiplier. Rune allocation and combat use this value.
        public int FinalTotal { get; }
        public bool IsTotalCritical => TotalMultiplier > 1;
        public bool HasAnyCritical
        {
            get
            {
                if (IsTotalCritical) return true;
                foreach (var reel in reels) if (reel.IsCritical) return true;
                return false;
            }
        }
        public bool IsAnyCapped
        {
            get
            {
                foreach (var reel in reels) if (reel.IsCapped) return true;
                return false;
            }
        }
    }

    public static class FortuneCriticalResolver
    {
        // Runs after SlotMachine.Spin with its own random source: each reel in result order (its own rule or the default
        // rule), then the total. With the cap C, reel i gets min(C, r_i × t) and the final total is Σ v_i × e_i.
        public static FortuneCriticalResult Resolve(SlotSpinResult spin, FortuneCriticalRules rules, IRandomIndexProvider random)
        {
            if (spin == null) throw new ArgumentNullException(nameof(spin));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var stops = spin.Reels;
            var reelMultipliers = new int[stops.Count];
            for (var i = 0; i < reelMultipliers.Length; i++) reelMultipliers[i] = rules.RuleForReel(stops[i].ReelId).Roll(random);
            var totalMultiplier = rules.TotalRule.Roll(random);
            var cap = rules.MaximumEffectiveMultiplier;
            var results = new FortuneCriticalReelResult[stops.Count];
            for (var i = 0; i < results.Length; i++)
            {
                var stop = stops[i];
                var effective = Math.Min(cap, reelMultipliers[i] * totalMultiplier);
                results[i] = new FortuneCriticalReelResult(stop.ReelId, stop.PaylineValue, reelMultipliers[i], totalMultiplier, cap,
                    checked((long)stop.PaylineValue * effective));
            }
            return new FortuneCriticalResult(spin, results, totalMultiplier, cap);
        }
    }
}
