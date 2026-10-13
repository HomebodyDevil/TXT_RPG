using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.SlotMachine
{
    // One Fortune Critical rule: the chance that it happens and, when it does, the multiplier distribution over
    // [MinimumMultiplier, MaximumMultiplier]. A chance of 0 turns it off. Immutable; the probability table is built once.
    public sealed class FortuneCriticalRule : IEquatable<FortuneCriticalRule>
    {
        private readonly int threshold;

        private FortuneCriticalRule(double chance, FortuneMultiplierDistribution distribution, FortuneMultiplierTable table)
        {
            Chance = chance;
            Distribution = distribution;
            Table = table;
            threshold = (int)Math.Round(chance * FortuneCriticalLimits.ProbabilityResolution, MidpointRounding.AwayFromZero);
        }

        public double Chance { get; }
        public int MinimumMultiplier => Table.Minimum;
        public int MaximumMultiplier => Table.Maximum;
        public FortuneMultiplierDistribution Distribution { get; }
        public FortuneMultiplierTable Table { get; }
        // Chance in parts per million, as drawn.
        public int ChancePartsPerMillion => threshold;
        // E[n] once it happens, and the expected multiplier including the chance: 1 + p (E[n] - 1).
        public double ExpectedMultiplierWhenTriggered => Table.ExpectedMultiplier;
        public double ExpectedMultiplier => 1d + Chance * (Table.ExpectedMultiplier - 1d);

        public static bool TryCreate(double chance, int minimumMultiplier, int maximumMultiplier, FortuneMultiplierDistribution distribution,
            FortuneWeightFunctionRegistry registry, out FortuneCriticalRule rule, out string reason)
        {
            rule = null;
            if (!TryValidateChance(chance, out reason)) return false;
            if (!FortuneMultiplierTable.TryCreate(minimumMultiplier, maximumMultiplier, distribution, registry, out var table, out reason)) return false;
            rule = new FortuneCriticalRule(chance, distribution, table);
            return true;
        }

        // The same distribution with another chance; the probability table is reused.
        public bool TryWithChance(double chance, out FortuneCriticalRule rule, out string reason)
        {
            rule = null;
            if (!TryValidateChance(chance, out reason)) return false;
            rule = chance.Equals(Chance) ? this : new FortuneCriticalRule(chance, Distribution, Table);
            return true;
        }

        public static bool TryValidateChance(double chance, out string reason)
        {
            if (double.IsNaN(chance) || chance < 0d || chance > 1d)
            {
                reason = $"The Fortune Critical chance {chance} must be between 0 and 1.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        // 1 when it does not happen. Chance 0 draws nothing; chance 1 skips the occurrence draw; a single-multiplier range
        // skips the multiplier draw.
        public int Roll(IRandomIndexProvider random)
        {
            if (threshold <= 0) return 1;
            if (threshold < FortuneCriticalLimits.ProbabilityResolution)
            {
                var draw = random.NextIndex(FortuneCriticalLimits.ProbabilityResolution);
                if (draw < 0 || draw >= FortuneCriticalLimits.ProbabilityResolution)
                    throw new InvalidOperationException($"The random source returned {draw} for a Fortune Critical chance.");
                if (draw >= threshold) return 1;
            }
            return Table.Sample(random);
        }

        public bool Equals(FortuneCriticalRule other) =>
            other != null && Chance.Equals(other.Chance) && MinimumMultiplier == other.MinimumMultiplier &&
            MaximumMultiplier == other.MaximumMultiplier && Distribution.Equals(other.Distribution);

        public override bool Equals(object obj) => obj is FortuneCriticalRule other && Equals(other);
        public override int GetHashCode() => MinimumMultiplier * 397 ^ MaximumMultiplier;
    }

    public sealed class FortuneCriticalReelRule
    {
        public FortuneCriticalReelRule(string reelId, FortuneCriticalRule rule)
        {
            ReelId = reelId?.Trim() ?? string.Empty;
            Rule = rule;
        }

        public string ReelId { get; }
        public FortuneCriticalRule Rule { get; }
    }

    // The complete Fortune Critical configuration: the rule for reels without their own rule, per reel-ID rules (in
    // order), the total rule and the cap of each reel's effective multiplier. A reel rule may name a reel that does not
    // exist yet; it applies once a reel with that ID is added. Immutable; the With methods return a validated copy.
    public sealed class FortuneCriticalRules : IEquatable<FortuneCriticalRules>
    {
        public const int DisabledMaximumEffectiveMultiplier = 30;

        private readonly FortuneCriticalReelRule[] reelRules;

        private FortuneCriticalRules(FortuneCriticalRule defaultReelRule, FortuneCriticalReelRule[] reelRules,
            FortuneCriticalRule totalRule, int maximumEffectiveMultiplier)
        {
            DefaultReelRule = defaultReelRule;
            this.reelRules = reelRules;
            TotalRule = totalRule;
            MaximumEffectiveMultiplier = maximumEffectiveMultiplier;
        }

        // Every chance is 0 (×2..×10 with the default distribution), so changing only a chance gives a usable rule.
        public static FortuneCriticalRules Disabled { get; } = CreateDisabled();

        private static FortuneCriticalRules CreateDisabled()
        {
            if (!FortuneCriticalRule.TryCreate(0d, FortuneCriticalLimits.MinimumMultiplier, 10, FortuneMultiplierDistribution.Default,
                    FortuneWeightFunctionRegistry.Default, out var off, out var reason))
                throw new InvalidOperationException(reason);
            return new FortuneCriticalRules(off, Array.Empty<FortuneCriticalReelRule>(), off, DisabledMaximumEffectiveMultiplier);
        }

        public FortuneCriticalRule DefaultReelRule { get; }
        public IReadOnlyList<FortuneCriticalReelRule> ReelRules => Array.AsReadOnly(reelRules);
        public FortuneCriticalRule TotalRule { get; }
        public int MaximumEffectiveMultiplier { get; }
        public bool IsDisabled
        {
            get
            {
                if (DefaultReelRule.ChancePartsPerMillion > 0 || TotalRule.ChancePartsPerMillion > 0) return false;
                foreach (var entry in reelRules) if (entry.Rule.ChancePartsPerMillion > 0) return false;
                return true;
            }
        }

        public static bool TryCreate(FortuneCriticalRule defaultReelRule, IEnumerable<FortuneCriticalReelRule> reelRules,
            FortuneCriticalRule totalRule, int maximumEffectiveMultiplier, out FortuneCriticalRules rules, out string reason)
        {
            rules = null;
            if (defaultReelRule == null) { reason = "A default reel rule is required."; return false; }
            if (totalRule == null) { reason = "A total rule is required."; return false; }
            if (!TryValidateMaximumEffectiveMultiplier(maximumEffectiveMultiplier, out reason)) return false;
            var list = new List<FortuneCriticalReelRule>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (reelRules != null)
            {
                foreach (var entry in reelRules)
                {
                    if (entry == null || entry.Rule == null) { reason = $"Reel rule {list.Count + 1} is empty."; return false; }
                    if (entry.ReelId.Length == 0) { reason = $"Reel rule {list.Count + 1} requires a reel id."; return false; }
                    if (!ids.Add(entry.ReelId)) { reason = $"Reel rule '{entry.ReelId}' is duplicated."; return false; }
                    list.Add(entry);
                }
            }
            rules = new FortuneCriticalRules(defaultReelRule, list.ToArray(), totalRule, maximumEffectiveMultiplier);
            reason = string.Empty;
            return true;
        }

        public static bool TryValidateMaximumEffectiveMultiplier(int value, out string reason)
        {
            if (value < 1 || value > FortuneCriticalLimits.MaximumEffectiveMultiplier)
            {
                reason = $"The maximum effective multiplier {value} must be between 1 and {FortuneCriticalLimits.MaximumEffectiveMultiplier}.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        // The reel's own rule, or the default rule. IDs are trimmed and compared ordinally, like reel IDs.
        public FortuneCriticalRule RuleForReel(string reelId) => TryGetReelRule(reelId, out var rule) ? rule : DefaultReelRule;

        public bool TryGetReelRule(string reelId, out FortuneCriticalRule rule)
        {
            var index = IndexOf(reelId);
            rule = index < 0 ? null : reelRules[index].Rule;
            return index >= 0;
        }

        public bool TryWithDefaultReelRule(FortuneCriticalRule rule, out FortuneCriticalRules rules, out string reason) =>
            TryCreate(rule, reelRules, TotalRule, MaximumEffectiveMultiplier, out rules, out reason);

        public bool TryWithTotalRule(FortuneCriticalRule rule, out FortuneCriticalRules rules, out string reason) =>
            TryCreate(DefaultReelRule, reelRules, rule, MaximumEffectiveMultiplier, out rules, out reason);

        public bool TryWithMaximumEffectiveMultiplier(int value, out FortuneCriticalRules rules, out string reason) =>
            TryCreate(DefaultReelRule, reelRules, TotalRule, value, out rules, out reason);

        // Replaces the reel's rule in place, or appends it.
        public bool TryWithReelRule(string reelId, FortuneCriticalRule rule, out FortuneCriticalRules rules, out string reason)
        {
            var list = new List<FortuneCriticalReelRule>(reelRules);
            var entry = new FortuneCriticalReelRule(reelId, rule);
            var index = IndexOf(reelId);
            if (index < 0) list.Add(entry); else list[index] = entry;
            return TryCreate(DefaultReelRule, list, TotalRule, MaximumEffectiveMultiplier, out rules, out reason);
        }

        public bool TryWithoutReelRule(string reelId, out FortuneCriticalRules rules, out string reason)
        {
            var index = IndexOf(reelId);
            if (index < 0) { rules = null; reason = $"Reel '{reelId}' has no Fortune Critical rule."; return false; }
            var list = new List<FortuneCriticalReelRule>(reelRules);
            list.RemoveAt(index);
            return TryCreate(DefaultReelRule, list, TotalRule, MaximumEffectiveMultiplier, out rules, out reason);
        }

        private int IndexOf(string reelId)
        {
            var id = reelId?.Trim();
            for (var i = 0; i < reelRules.Length; i++)
                if (string.Equals(reelRules[i].ReelId, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        public bool Equals(FortuneCriticalRules other)
        {
            if (other == null || MaximumEffectiveMultiplier != other.MaximumEffectiveMultiplier || reelRules.Length != other.reelRules.Length ||
                !DefaultReelRule.Equals(other.DefaultReelRule) || !TotalRule.Equals(other.TotalRule)) return false;
            for (var i = 0; i < reelRules.Length; i++)
                if (!string.Equals(reelRules[i].ReelId, other.reelRules[i].ReelId, StringComparison.Ordinal) || !reelRules[i].Rule.Equals(other.reelRules[i].Rule))
                    return false;
            return true;
        }

        public override bool Equals(object obj) => obj is FortuneCriticalRules other && Equals(other);
        public override int GetHashCode() => MaximumEffectiveMultiplier * 31 + reelRules.Length;
    }
}
