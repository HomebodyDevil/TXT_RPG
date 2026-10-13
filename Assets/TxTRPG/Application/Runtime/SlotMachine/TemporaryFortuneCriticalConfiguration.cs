using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.SlotMachine;
using UnityEngine;

namespace TxTRPG.Application.SlotMachine
{
    [Serializable]
    public sealed class TemporaryFortuneFunctionArgument
    {
        [SerializeField] private string name = string.Empty;
        [SerializeField] private double value;

        public TemporaryFortuneFunctionArgument() { }
        public TemporaryFortuneFunctionArgument(string argumentName, double argumentValue)
        {
            name = argumentName;
            value = argumentValue;
        }

        public string Name => name?.Trim() ?? string.Empty;
        public double Value => value;
    }

    // Serialized form of one FortuneCriticalRule. Function parameters are stored by name, so reordering a function's
    // parameters never breaks an asset; a missing parameter uses the function default.
    [Serializable]
    public sealed class TemporaryFortuneCriticalRuleDefinition
    {
        [SerializeField, Range(0f, 1f), Tooltip("Chance that this Fortune Critical happens (0 turns it off). Temporary balance value.")]
        private float chance;
        [SerializeField, Tooltip("Smallest multiplier when it happens (2 or more). Temporary balance value.")]
        private int minimumMultiplier = FortuneCriticalLimits.MinimumMultiplier;
        [SerializeField, Tooltip("Largest multiplier when it happens (100 or less). Temporary balance value.")]
        private int maximumMultiplier = 10;
        [SerializeField, Tooltip("Function: a registered weight function with parameters. Table: one weight per multiplier from the minimum.")]
        private FortuneDistributionSource distributionSource = FortuneDistributionSource.Function;
        [SerializeField, Tooltip("Stable ID of a function in FortuneWeightFunctionRegistry.Default (geometric, power, half-normal, linear, equal-expected, uniform).")]
        private string functionId = FortuneWeightFunctionRegistry.DefaultFunctionId;
        [SerializeField, Tooltip("Function parameters by name. A missing parameter uses the function default.")]
        private List<TemporaryFortuneFunctionArgument> parameters = new() { new TemporaryFortuneFunctionArgument("ratio", .7d) };
        [SerializeField, Tooltip("Table distribution: weights from the minimum multiplier; the length must be maximum - minimum + 1.")]
        private List<float> tableWeights = new();

        public TemporaryFortuneCriticalRuleDefinition() { }

        public TemporaryFortuneCriticalRuleDefinition(float ruleChance, int minimum = FortuneCriticalLimits.MinimumMultiplier, int maximum = 10,
            string function = FortuneWeightFunctionRegistry.DefaultFunctionId, IEnumerable<TemporaryFortuneFunctionArgument> arguments = null,
            FortuneDistributionSource source = FortuneDistributionSource.Function, IEnumerable<float> weights = null)
        {
            chance = ruleChance;
            minimumMultiplier = minimum;
            maximumMultiplier = maximum;
            functionId = function;
            distributionSource = source;
            parameters = arguments != null ? new List<TemporaryFortuneFunctionArgument>(arguments) : new List<TemporaryFortuneFunctionArgument>();
            tableWeights = weights != null ? new List<float>(weights) : new List<float>();
        }

        public float Chance => chance;
        public int MinimumMultiplier => minimumMultiplier;
        public int MaximumMultiplier => maximumMultiplier;
        public FortuneDistributionSource DistributionSource => distributionSource;
        public string FunctionId => functionId?.Trim() ?? string.Empty;
        public IReadOnlyList<TemporaryFortuneFunctionArgument> Parameters => parameters ?? new List<TemporaryFortuneFunctionArgument>();
        public IReadOnlyList<float> TableWeights => tableWeights ?? new List<float>();

        public bool TryCreateDistribution(out FortuneMultiplierDistribution distribution, out string reason)
        {
            distribution = null;
            try
            {
                if (distributionSource == FortuneDistributionSource.Table)
                {
                    var weights = new List<double>();
                    foreach (var weight in TableWeights) weights.Add(weight);
                    distribution = FortuneMultiplierDistribution.Table(weights);
                }
                else
                {
                    var arguments = new List<KeyValuePair<string, double>>();
                    foreach (var argument in Parameters)
                    {
                        if (argument == null) continue;
                        arguments.Add(new KeyValuePair<string, double>(argument.Name, argument.Value));
                    }
                    distribution = FortuneMultiplierDistribution.Function(FunctionId, FortuneFunctionArguments.Create(arguments));
                }
                reason = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        public bool TryCreateRule(FortuneWeightFunctionRegistry registry, out FortuneCriticalRule rule, out string reason)
        {
            rule = null;
            return TryCreateDistribution(out var distribution, out reason) &&
                FortuneCriticalRule.TryCreate(chance, minimumMultiplier, maximumMultiplier, distribution, registry, out rule, out reason);
        }

        // Keeps the values of this definition inside their ranges (OnValidate of the owning asset).
        internal void Clamp()
        {
            chance = Mathf.Clamp01(float.IsNaN(chance) ? 0f : chance);
            minimumMultiplier = Mathf.Clamp(minimumMultiplier, FortuneCriticalLimits.MinimumMultiplier, FortuneCriticalLimits.MaximumMultiplier);
            maximumMultiplier = Mathf.Clamp(maximumMultiplier, minimumMultiplier, FortuneCriticalLimits.MaximumMultiplier);
            parameters ??= new List<TemporaryFortuneFunctionArgument>();
            tableWeights ??= new List<float>();
            for (var i = 0; i < tableWeights.Count; i++)
                if (float.IsNaN(tableWeights[i]) || tableWeights[i] < 0f) tableWeights[i] = 0f;
        }
    }

    [Serializable]
    public sealed class TemporaryFortuneCriticalReelRuleDefinition
    {
        [SerializeField, Tooltip("Reel ID (for example reel.2). A reel that does not exist yet is allowed; the rule applies once it is added.")]
        private string reelId = string.Empty;
        [SerializeField] private TemporaryFortuneCriticalRuleDefinition rule = new(.05f);

        public TemporaryFortuneCriticalReelRuleDefinition() { }
        public TemporaryFortuneCriticalReelRuleDefinition(string id, TemporaryFortuneCriticalRuleDefinition definition)
        {
            reelId = id;
            rule = definition;
        }

        public string ReelId => reelId?.Trim() ?? string.Empty;
        public TemporaryFortuneCriticalRuleDefinition Rule => rule;
    }

    // Authoring source of the session Fortune Critical rules. Copied into TemporaryFortuneCriticalState at startup and
    // never changed at runtime. Every default here is a temporary balance value that has not been confirmed.
    [CreateAssetMenu(menuName = "TxT RPG/Temporary Fortune Critical Configuration", fileName = "TemporaryFortuneCriticalConfiguration")]
    public sealed class TemporaryFortuneCriticalConfiguration : ScriptableObject
    {
        public const float DefaultReelChance = .05f;
        public const float DefaultTotalChance = .03f;
        public const int DefaultMaximumEffectiveMultiplier = 30;

        [SerializeField, Tooltip("Rule of every reel without its own rule. Temporary balance: 5%, ×2..×10, geometric r = 0.7.")]
        private TemporaryFortuneCriticalRuleDefinition defaultReelRule = new(DefaultReelChance, arguments: DefaultArguments());
        [SerializeField, Tooltip("Rules by reel ID. They replace the default rule for that reel.")]
        private List<TemporaryFortuneCriticalReelRuleDefinition> reelRules = new();
        [SerializeField, Tooltip("Rule of the total, applied after the reel multipliers. Temporary balance: 3%, ×2..×10, geometric r = 0.7.")]
        private TemporaryFortuneCriticalRuleDefinition totalRule = new(DefaultTotalChance, arguments: DefaultArguments());
        [SerializeField, Tooltip("Cap of each reel's effective multiplier (reel × total). Temporary balance value.")]
        private int maximumEffectiveMultiplier = DefaultMaximumEffectiveMultiplier;

        public TemporaryFortuneCriticalRuleDefinition DefaultReelRule => defaultReelRule;
        public IReadOnlyList<TemporaryFortuneCriticalReelRuleDefinition> ReelRules => reelRules;
        public TemporaryFortuneCriticalRuleDefinition TotalRule => totalRule;
        public int MaximumEffectiveMultiplier => maximumEffectiveMultiplier;

        private static TemporaryFortuneFunctionArgument[] DefaultArguments() =>
            new[] { new TemporaryFortuneFunctionArgument(GeometricFortuneWeightFunction.Ratio.Name, GeometricFortuneWeightFunction.Ratio.DefaultValue) };

        public bool TryCreateRules(FortuneWeightFunctionRegistry registry, out FortuneCriticalRules rules, out string reason)
        {
            rules = null;
            if (defaultReelRule == null) { reason = "Default reel rule: missing"; return false; }
            if (!defaultReelRule.TryCreateRule(registry, out var defaultRule, out reason)) { reason = $"Default reel rule: {reason}"; return false; }
            var entries = new List<FortuneCriticalReelRule>();
            for (var i = 0; i < (reelRules?.Count ?? 0); i++)
            {
                var entry = reelRules[i];
                if (entry == null || entry.Rule == null) { reason = $"Reel rule {i + 1} is empty."; return false; }
                if (!entry.Rule.TryCreateRule(registry, out var rule, out reason)) { reason = $"Reel rule '{entry.ReelId}': {reason}"; return false; }
                entries.Add(new FortuneCriticalReelRule(entry.ReelId, rule));
            }
            if (totalRule == null) { reason = "Total rule: missing"; return false; }
            if (!totalRule.TryCreateRule(registry, out var total, out reason)) { reason = $"Total rule: {reason}"; return false; }
            return FortuneCriticalRules.TryCreate(defaultRule, entries, total, maximumEffectiveMultiplier, out rules, out reason);
        }

        // Clamps this asset's own values only; it never touches other assets or Scenes.
        private void OnValidate()
        {
            defaultReelRule ??= new TemporaryFortuneCriticalRuleDefinition(DefaultReelChance, arguments: DefaultArguments());
            totalRule ??= new TemporaryFortuneCriticalRuleDefinition(DefaultTotalChance, arguments: DefaultArguments());
            reelRules ??= new List<TemporaryFortuneCriticalReelRuleDefinition>();
            defaultReelRule.Clamp();
            totalRule.Clamp();
            foreach (var entry in reelRules) entry?.Rule?.Clamp();
            maximumEffectiveMultiplier = Mathf.Clamp(maximumEffectiveMultiplier, 1, FortuneCriticalLimits.MaximumEffectiveMultiplier);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(TemporaryFortuneCriticalRuleDefinition reelDefault,
            IEnumerable<TemporaryFortuneCriticalReelRuleDefinition> perReel, TemporaryFortuneCriticalRuleDefinition total, int cap)
        {
            defaultReelRule = reelDefault;
            reelRules = new List<TemporaryFortuneCriticalReelRuleDefinition>(perReel ?? Array.Empty<TemporaryFortuneCriticalReelRuleDefinition>());
            totalRule = total;
            maximumEffectiveMultiplier = cap;
        }

        // Tests only: runs the same clamping as the Inspector.
        public void ValidateForEditor() => OnValidate();
#endif
    }

    // Session-lifetime Fortune Critical rules, copied from the configuration once. Runes, items or buffs change them
    // through the Try methods: each validates the candidate, keeps the previous rules on failure, and raises Changed once
    // on success. Setting the same rules again does not raise Changed. An accepted spin keeps its own result.
    public sealed class TemporaryFortuneCriticalState : IFortuneCriticalRuleStore
    {
        public TemporaryFortuneCriticalState(FortuneCriticalRules rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public FortuneCriticalRules Rules { get; private set; }
        public event Action Changed;

        // Missing or invalid configuration never blocks the game: the state starts disabled and the reason is returned.
        public static TemporaryFortuneCriticalState Create(TemporaryFortuneCriticalConfiguration configuration,
            FortuneWeightFunctionRegistry registry, out bool configured, out string reason)
        {
            if (configuration == null)
            {
                configured = false;
                reason = "No Fortune Critical configuration is assigned.";
                return new TemporaryFortuneCriticalState(FortuneCriticalRules.Disabled);
            }
            configured = configuration.TryCreateRules(registry, out var rules, out reason);
            return new TemporaryFortuneCriticalState(configured ? rules : FortuneCriticalRules.Disabled);
        }

        public bool TryReplaceAll(FortuneCriticalRules rules, out string reason)
        {
            if (rules == null) { reason = "Fortune Critical rules are required."; return false; }
            reason = string.Empty;
            Commit(rules);
            return true;
        }

        public bool TrySetDefaultReelRule(FortuneCriticalRule rule, out string reason) =>
            Commit(Rules.TryWithDefaultReelRule(rule, out var rules, out reason), rules);

        public bool TrySetReelRule(string reelId, FortuneCriticalRule rule, out string reason) =>
            Commit(Rules.TryWithReelRule(reelId, rule, out var rules, out reason), rules);

        public bool TryRemoveReelRule(string reelId, out string reason) =>
            Commit(Rules.TryWithoutReelRule(reelId, out var rules, out reason), rules);

        public bool TrySetTotalRule(FortuneCriticalRule rule, out string reason) =>
            Commit(Rules.TryWithTotalRule(rule, out var rules, out reason), rules);

        public bool TrySetMaximumEffectiveMultiplier(int value, out string reason) =>
            Commit(Rules.TryWithMaximumEffectiveMultiplier(value, out var rules, out reason), rules);

        public bool TrySetDefaultReelChance(double chance, out string reason) =>
            Rules.DefaultReelRule.TryWithChance(chance, out var rule, out reason) && TrySetDefaultReelRule(rule, out reason);

        // Changes the chance of the reel's own rule; a reel without one gets a copy of the default rule with this chance.
        public bool TrySetReelChance(string reelId, double chance, out string reason) =>
            Rules.RuleForReel(reelId).TryWithChance(chance, out var rule, out reason) && TrySetReelRule(reelId, rule, out reason);

        public bool TrySetTotalChance(double chance, out string reason) =>
            Rules.TotalRule.TryWithChance(chance, out var rule, out reason) && TrySetTotalRule(rule, out reason);

        private bool Commit(bool valid, FortuneCriticalRules candidate)
        {
            if (!valid) return false;
            Commit(candidate);
            return true;
        }

        private void Commit(FortuneCriticalRules candidate)
        {
            if (Rules.Equals(candidate)) return;
            Rules = candidate;
            Changed?.Invoke();
        }
    }
}
