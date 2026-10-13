using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Gameplay.SlotMachine;
using SlotMachineModel = TxTRPG.Gameplay.SlotMachine.SlotMachine;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class FortuneCriticalTests
    {
        private const int Million = FortuneCriticalLimits.ProbabilityResolution;
        private static readonly int[] OneToSix = { 1, 2, 3, 4, 5, 6 };
        private static FortuneWeightFunctionRegistry Registry => FortuneWeightFunctionRegistry.Default;

        // ---------- Weight functions and the integer table ----------

        // Expected P(×2), P(×3), P(×5), P(×10) in percent and E[n] from the analysis table of slot-fortune-critical.md
        // (§11.2), n = 2..10. The linear row (weights 11 - n) is endWeight = 1/9.
        [TestCase("geometric", "ratio", .6d, 40.4d, 24.2d, 8.7d, .68d, 3.41d)]
        [TestCase("geometric", "ratio", .7d, 31.3d, 21.9d, 10.7d, 1.80d, 3.95d)]
        [TestCase("geometric", "ratio", .8d, 23.1d, 18.5d, 11.8d, 3.88d, 4.60d)]
        [TestCase("power", "exponent", 1.5d, 35.5d, 19.3d, 9.0d, 3.18d, 4.04d)]
        [TestCase("power", "exponent", 2d, 45.5d, 20.2d, 7.3d, 1.82d, 3.51d)]
        [TestCase("power", "exponent", 3d, 63.3d, 18.7d, 4.0d, .51d, 2.78d)]
        [TestCase("half-normal", "sigma", 2d, 33.3d, 29.4d, 10.8d, .01d, 3.30d)]
        [TestCase("half-normal", "sigma", 3d, 23.6d, 22.3d, 14.3d, .67d, 4.06d)]
        [TestCase("linear", "endWeight", 1d / 9d, 20.0d, 17.8d, 13.3d, 2.22d, 4.67d)]
        [TestCase("equal-expected", null, 0d, 35.3d, 17.7d, 8.8d, 3.93d, 4.18d)]
        [TestCase("uniform", null, 0d, 11.1d, 11.1d, 11.1d, 11.11d, 6.00d)]
        public void DefaultFunctions_MatchTheAnalysisTable(string id, string parameter, double value,
            double p2, double p3, double p5, double p10, double expected)
        {
            var arguments = parameter == null ? FortuneFunctionArguments.Empty : FortuneFunctionArguments.Of(parameter, value);
            var table = Table(2, 10, FortuneMultiplierDistribution.Function(id, arguments));
            Assert.That(table.Probability(2) * 100d, Is.EqualTo(p2).Within(.051d));
            Assert.That(table.Probability(3) * 100d, Is.EqualTo(p3).Within(.051d));
            Assert.That(table.Probability(5) * 100d, Is.EqualTo(p5).Within(.051d));
            Assert.That(table.Probability(10) * 100d, Is.EqualTo(p10).Within(.0051d));
            Assert.That(table.ExpectedMultiplier, Is.EqualTo(expected).Within(.0051d));
        }

        [Test]
        public void DefaultRule_ExpectedMultipliersFollowTheChance()
        {
            var reel = Rule(.05d, 2, 10, FortuneMultiplierDistribution.Default);
            var total = Rule(.03d, 2, 10, FortuneMultiplierDistribution.Default);
            Assert.That(reel.ExpectedMultiplierWhenTriggered, Is.EqualTo(3.955d).Within(.001d));
            Assert.That(reel.ExpectedMultiplier, Is.EqualTo(1.148d).Within(.001d), "1 + p(E[n] - 1)");
            Assert.That(total.ExpectedMultiplier, Is.EqualTo(1.089d).Within(.001d));
        }

        [Test]
        public void IntegerTable_SumsToTheResolutionAndIsDeterministic()
        {
            foreach (var function in Registry.Functions)
            {
                var table = Table(2, 10, FortuneMultiplierDistribution.Function(function.Id, FortuneFunctionArguments.Empty));
                Assert.That(Enumerable.Range(2, 9).Sum(table.PartsPerMillion), Is.EqualTo(Million), function.Id);
                var again = Table(2, 10, FortuneMultiplierDistribution.Function(function.Id, FortuneFunctionArguments.Empty));
                Assert.That(Enumerable.Range(2, 9).Select(again.PartsPerMillion), Is.EqualTo(Enumerable.Range(2, 9).Select(table.PartsPerMillion)));
            }
            // Three equal shares of 333,333.33: the one leftover part goes to the lowest multiplier.
            var tie = Table(2, 4, FortuneMultiplierDistribution.Function("uniform", FortuneFunctionArguments.Empty));
            Assert.That(new[] { 2, 3, 4 }.Select(tie.PartsPerMillion), Is.EqualTo(new[] { 333_334, 333_333, 333_333 }));
        }

        [Test]
        public void TableDistribution_UsesItsWeightsAndReportsUnreachableMultipliers()
        {
            var table = Table(2, 4, FortuneMultiplierDistribution.Table(new[] { 3d, 1d, 0d }));
            Assert.That(new[] { 2, 3, 4 }.Select(table.PartsPerMillion), Is.EqualTo(new[] { 750_000, 250_000, 0 }));
            Assert.That(table.UnreachableMultipliers, Is.Empty, "a zero weight is not a rounding loss");
            var tiny = Table(2, 3, FortuneMultiplierDistribution.Table(new[] { 1d, 1e-9d }));
            Assert.That(tiny.UnreachableMultipliers, Is.EqualTo(new[] { 3 }), "a positive weight rounded to 0 ppm can never be drawn");
        }

        // ---------- Random consumption ----------

        [Test]
        public void ChanceZero_DrawsNothingAndChanceOne_SkipsTheOccurrenceDraw()
        {
            var random = new SequenceRandom();
            Assert.That(Rule(0d, 2, 10, FortuneMultiplierDistribution.Default).Roll(random), Is.EqualTo(1));
            Assert.That(random.Bounds, Is.Empty, "chance 0 consumes no random value");

            random = new SequenceRandom(0);
            Assert.That(Rule(1d, 2, 10, Uniform).Roll(random), Is.EqualTo(2));
            Assert.That(random.Bounds, Is.EqualTo(new[] { Million }), "chance 1 draws the multiplier only");

            random = new SequenceRandom();
            Assert.That(Rule(1d, 4, 4, Uniform).Roll(random), Is.EqualTo(4));
            Assert.That(random.Bounds, Is.Empty, "a single-multiplier range draws nothing");

            random = new SequenceRandom(499_999, 500_000);
            var half = Rule(.5d, 7, 7, Uniform);
            Assert.That(half.Roll(random), Is.EqualTo(7), "a draw below the threshold happens");
            Assert.That(half.Roll(random), Is.EqualTo(1), "a draw at the threshold does not");
        }

        // ---------- Resolution ----------

        [Test]
        public void Resolve_AppliesReelAndTotalMultipliersAndTheCap()
        {
            // v = (3, 5, 2); reel 2 ×10, total ×5, C = 30 -> e = (5, 30, 5), f = (15, 150, 10), F = 175.
            var spin = DefaultMachine().Spin(new SequenceRandom(2, 4, 1));
            var rules = Rules(Rule(.5d, 2, 10, Uniform), Rule(.5d, 2, 10, Uniform), 30);
            // Uniform ×2..×10: ×2 holds 111,112 ppm and every other 111,111; 999,999 is ×10 and 400,000 is ×5.
            var random = new SequenceRandom(999_999, 0, 999_999, 999_999, 0, 400_000);
            var result = FortuneCriticalResolver.Resolve(spin, rules, random);

            Assert.That(random.Bounds.Count, Is.EqualTo(6), "reels in order, then the total");
            Assert.That(result.Reels.Select(r => r.ReelMultiplier), Is.EqualTo(new[] { 1, 10, 1 }));
            Assert.That(result.TotalMultiplier, Is.EqualTo(5));
            Assert.That(result.Reels.Select(r => r.EffectiveMultiplier), Is.EqualTo(new[] { 5, 30, 5 }));
            Assert.That(result.Reels.Select(r => r.FinalValue), Is.EqualTo(new long[] { 15, 150, 10 }));
            Assert.That(result.Reels.Select(r => r.IsCapped), Is.EqualTo(new[] { false, true, false }));
            Assert.That(result.Reels[1].UncappedMultiplier, Is.EqualTo(50));
            Assert.That(result.BaseTotal, Is.EqualTo(10));
            Assert.That(result.FinalTotal, Is.EqualTo(175));
            Assert.That(result.HasAnyCritical && result.IsAnyCapped && result.IsTotalCritical, Is.True);
            Assert.That(spin.PaylineTotal, Is.EqualTo(10), "the spin result is not changed");
        }

        [Test]
        public void Resolve_WithoutCap_IsTheTotalMultiplierTimesTheReelSum()
        {
            var spin = DefaultMachine().Spin(new SequenceRandom(2, 4, 1));
            var rules = Rules(Rule(0d, 2, 10, Uniform), Rule(1d, 2, 2, Uniform), 30);
            var result = FortuneCriticalResolver.Resolve(spin, rules, new SequenceRandom());
            Assert.That(result.FinalTotal, Is.EqualTo(20));
            Assert.That(result.IsAnyCapped, Is.False);
            var none = FortuneCriticalResult.None(spin);
            Assert.That(none.FinalTotal, Is.EqualTo(10));
            Assert.That(none.HasAnyCritical, Is.False);
            Assert.That(none.Reels.All(r => r.EffectiveMultiplier == 1), Is.True);
        }

        [Test]
        public void ReelRules_SelectByIdAndFallBackToTheDefault()
        {
            var off = Rule(0d, 2, 10, Uniform);
            Assert.That(FortuneCriticalRules.TryCreate(off, new[]
            {
                new FortuneCriticalReelRule(" reel.2 ", Rule(1d, 3, 3, Uniform)),
                new FortuneCriticalReelRule("reel.9", Rule(1d, 4, 4, Uniform))
            }, off, 30, out var rules, out var reason), Is.True, "a rule for a reel that does not exist yet is allowed: " + reason);

            var machine = DefaultMachine();
            var result = FortuneCriticalResolver.Resolve(machine.Spin(new SequenceRandom(0, 0, 0)), rules, new SequenceRandom());
            Assert.That(result.Reels.Select(r => r.ReelMultiplier), Is.EqualTo(new[] { 1, 3, 1 }));

            Assert.That(machine.TryAddReel(SlotReel.FromValues("reel.9", OneToSix), out reason), Is.True, reason);
            Assert.That(machine.TryAddReel(SlotReel.FromValues("reel.10", OneToSix), out reason), Is.True, reason);
            result = FortuneCriticalResolver.Resolve(machine.Spin(new SequenceRandom(0, 0, 0, 0, 0)), rules, new SequenceRandom());
            Assert.That(result.Reels.Select(r => r.ReelMultiplier), Is.EqualTo(new[] { 1, 3, 1, 4, 1 }),
                "an added reel uses its own rule, or the default rule");
            Assert.That(rules.RuleForReel("REEL.2"), Is.SameAs(off), "IDs are case-sensitive");
        }

        [Test]
        public void ReelRules_RejectBlankAndDuplicateIds()
        {
            var off = Rule(0d, 2, 10, Uniform);
            Assert.That(FortuneCriticalRules.TryCreate(off, new[] { new FortuneCriticalReelRule("  ", off) }, off, 30, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("reel id"));
            Assert.That(FortuneCriticalRules.TryCreate(off, new[] { new FortuneCriticalReelRule("reel.1", off), new FortuneCriticalReelRule("reel.1 ", off) },
                off, 30, out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("duplicated"));
            Assert.That(FortuneCriticalRules.Disabled.TryWithReelRule("", off, out _, out _), Is.False);
            Assert.That(FortuneCriticalRules.Disabled.TryWithoutReelRule("reel.1", out _, out _), Is.False, "removing a missing rule fails");
        }

        [Test]
        public void InvalidRules_AreRejected()
        {
            Assert.That(TryRule(-.01d, 2, 10, FortuneMultiplierDistribution.Default), Is.False, "negative chance");
            Assert.That(TryRule(1.01d, 2, 10, FortuneMultiplierDistribution.Default), Is.False, "chance above 1");
            Assert.That(TryRule(double.NaN, 2, 10, FortuneMultiplierDistribution.Default), Is.False, "NaN chance");
            Assert.That(TryRule(.1d, 1, 10, FortuneMultiplierDistribution.Default), Is.False, "minimum below 2");
            Assert.That(TryRule(.1d, 2, 101, FortuneMultiplierDistribution.Default), Is.False, "maximum above 100");
            Assert.That(TryRule(.1d, 6, 5, FortuneMultiplierDistribution.Default), Is.False, "minimum above maximum");
            Assert.That(TryRule(.1d, 2, 10, FortuneMultiplierDistribution.Function("missing", FortuneFunctionArguments.Empty)), Is.False, "unknown function");
            Assert.That(TryRule(.1d, 2, 10, FortuneMultiplierDistribution.Function("geometric", FortuneFunctionArguments.Of("ratio", 0d))), Is.False, "ratio must exceed 0");
            Assert.That(TryRule(.1d, 2, 10, FortuneMultiplierDistribution.Function("geometric", FortuneFunctionArguments.Of("ratio", double.NaN))), Is.False, "NaN argument");
            Assert.That(TryRule(.1d, 2, 4, FortuneMultiplierDistribution.Table(new[] { 1d, 1d })), Is.False, "table length mismatch");
            Assert.That(TryRule(.1d, 2, 3, FortuneMultiplierDistribution.Table(new[] { 0d, 0d })), Is.False, "zero weight sum");
            Assert.That(TryRule(.1d, 2, 3, FortuneMultiplierDistribution.Table(new[] { 1d, double.NaN })), Is.False, "NaN weight");
            Assert.That(TryRule(.1d, 2, 3, FortuneMultiplierDistribution.Table(new[] { 1d, -1d })), Is.False, "negative weight");
            Assert.That(TryRule(.1d, 2, 3, FortuneMultiplierDistribution.Table(new[] { 1d, double.PositiveInfinity })), Is.False, "infinite weight");
            var off = Rule(0d, 2, 10, Uniform);
            Assert.That(FortuneCriticalRules.TryCreate(off, null, off, 0, out _, out _), Is.False, "cap below 1");
            Assert.That(FortuneCriticalRules.TryCreate(off, null, off, 10_001, out _, out _), Is.False, "cap above 100 × 100");
            Assert.That(FortuneCriticalRules.TryCreate(null, null, off, 30, out _, out _), Is.False);
            Assert.Throws<ArgumentException>(() => FortuneFunctionArguments.Create(new[]
                { new KeyValuePair<string, double>("ratio", .5d), new KeyValuePair<string, double>(" ratio", .6d) }));
        }

        [Test]
        public void UnknownArgumentNames_AreIgnoredButReported()
        {
            var arguments = FortuneFunctionArguments.Create(new[]
                { new KeyValuePair<string, double>("ratio", .6d), new KeyValuePair<string, double>("legacy", 3d) });
            Assert.That(TryRule(.1d, 2, 10, FortuneMultiplierDistribution.Function("geometric", arguments)), Is.True);
            Registry.TryGet("geometric", out var geometric);
            Assert.That(arguments.UnknownNames(geometric), Is.EqualTo(new[] { "legacy" }));
            var missing = Table(2, 10, FortuneMultiplierDistribution.Function("geometric", FortuneFunctionArguments.Empty));
            var explicitDefault = Table(2, 10, FortuneMultiplierDistribution.Function("geometric", FortuneFunctionArguments.Of("ratio", .7d)));
            Assert.That(missing.PartsPerMillion(2), Is.EqualTo(explicitDefault.PartsPerMillion(2)), "a missing parameter uses the default");
        }

        [Test]
        public void Overflow_ThrowsOverflowException()
        {
            var machine = new SlotMachineModel(new[] { SlotReel.FromValues("reel.1", new[] { 1_000_000_000 }) });
            var spin = machine.Spin(new SequenceRandom(0));
            var rules = Rules(Rule(1d, 3, 3, Uniform), Rule(0d, 2, 10, Uniform), 30);
            Assert.Throws<OverflowException>(() => FortuneCriticalResolver.Resolve(spin, rules, new SequenceRandom()));
        }

        [Test]
        public void FixedSeedSample_MatchesTheExpectedFrequencies()
        {
            const int draws = 100_000;
            var rule = Rule(.4d, 2, 10, FortuneMultiplierDistribution.Default);
            var random = new SystemRandomIndexProvider(20261013);
            var counts = new int[11];
            for (var i = 0; i < draws; i++) counts[rule.Roll(random)]++;
            Assert.That(counts[1] / (double)draws, Is.EqualTo(.6d).Within(.005d), "no Fortune Critical");
            for (var n = 2; n <= 10; n++)
                Assert.That(counts[n] / (double)draws, Is.EqualTo(.4d * rule.Table.Probability(n)).Within(.005d), $"×{n}");
        }

        // ---------- Registry ----------

        [Test]
        public void Registry_HasTheDefaultFunctionsAndAcceptsANewOne()
        {
            Assert.That(Registry.Functions.Select(f => f.Id),
                Is.EqualTo(new[] { "geometric", "power", "half-normal", "linear", "equal-expected", "uniform" }));
            var registry = new FortuneWeightFunctionRegistry();
            registry.Register(new OnlyMaximumFunction());
            Assert.Throws<ArgumentException>(() => registry.Register(new OnlyMaximumFunction()), "duplicate IDs are rejected");
            Assert.That(FortuneCriticalRule.TryCreate(1d, 2, 6, FortuneMultiplierDistribution.Function("test.only-maximum", FortuneFunctionArguments.Empty),
                registry, out var rule, out var reason), Is.True, reason);
            Assert.That(rule.Table.PartsPerMillion(6), Is.EqualTo(Million));
            Assert.That(rule.Roll(new SequenceRandom(123_456)), Is.EqualTo(6));
        }

        [Test]
        public void Rules_WithMethodsReturnValidatedCopies()
        {
            var rules = FortuneCriticalRules.Disabled;
            Assert.That(rules.IsDisabled, Is.True);
            Assert.That(rules.DefaultReelRule.TryWithChance(.2d, out var changed, out var reason), Is.True, reason);
            Assert.That(changed.Table, Is.SameAs(rules.DefaultReelRule.Table), "changing only the chance reuses the table");
            Assert.That(rules.TryWithDefaultReelRule(changed, out var next, out reason), Is.True, reason);
            Assert.That(next.IsDisabled, Is.False);
            Assert.That(rules.IsDisabled, Is.True, "the original is unchanged");
            Assert.That(rules.TryWithMaximumEffectiveMultiplier(0, out var rejected, out _), Is.False);
            Assert.That(rejected, Is.Null);
            Assert.That(next.Equals(rules), Is.False);
            Assert.That(rules.TryWithDefaultReelRule(rules.DefaultReelRule, out var same, out _) && same.Equals(rules), Is.True);
        }

        // ---------- Helpers ----------

        private static readonly FortuneMultiplierDistribution Uniform =
            FortuneMultiplierDistribution.Function("uniform", FortuneFunctionArguments.Empty);

        private static SlotMachineModel DefaultMachine() => new(new[]
        {
            SlotReel.FromValues("reel.1", OneToSix),
            SlotReel.FromValues("reel.2", OneToSix),
            SlotReel.FromValues("reel.3", OneToSix)
        });

        private static FortuneMultiplierTable Table(int minimum, int maximum, FortuneMultiplierDistribution distribution)
        {
            Assert.That(FortuneMultiplierTable.TryCreate(minimum, maximum, distribution, Registry, out var table, out var reason), Is.True, reason);
            return table;
        }

        private static FortuneCriticalRule Rule(double chance, int minimum, int maximum, FortuneMultiplierDistribution distribution)
        {
            Assert.That(FortuneCriticalRule.TryCreate(chance, minimum, maximum, distribution, Registry, out var rule, out var reason), Is.True, reason);
            return rule;
        }

        private static bool TryRule(double chance, int minimum, int maximum, FortuneMultiplierDistribution distribution)
        {
            var created = FortuneCriticalRule.TryCreate(chance, minimum, maximum, distribution, Registry, out var rule, out var reason);
            Assert.That(created ? rule != null : rule == null && !string.IsNullOrEmpty(reason), Is.True);
            return created;
        }

        private static FortuneCriticalRules Rules(FortuneCriticalRule reel, FortuneCriticalRule total, int cap)
        {
            Assert.That(FortuneCriticalRules.TryCreate(reel, null, total, cap, out var rules, out var reason), Is.True, reason);
            return rules;
        }

        // A function added the documented way: one class, one Register call.
        private sealed class OnlyMaximumFunction : IFortuneWeightFunction
        {
            public string Id => "test.only-maximum";
            public string DisplayName => "Test: maximum only";
            public IReadOnlyList<FortuneFunctionParameter> Parameters => Array.Empty<FortuneFunctionParameter>();
            public double Weight(int multiplier, int minimumMultiplier, int maximumMultiplier, FortuneFunctionArguments arguments) =>
                multiplier == maximumMultiplier ? 1d : 0d;
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
