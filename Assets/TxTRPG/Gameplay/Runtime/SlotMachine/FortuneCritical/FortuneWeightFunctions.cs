using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.SlotMachine
{
    public static class FortuneCriticalLimits
    {
        // Smallest multiplier a Fortune Critical can produce; 1 means "no Fortune Critical".
        public const int MinimumMultiplier = 2;
        // Absolute upper bound of a rule's multiplier range. Wider than the current balance (×10) so it can be raised.
        public const int MaximumMultiplier = 100;
        public const int MaximumEffectiveMultiplier = MaximumMultiplier * MaximumMultiplier;
        // Probabilities are drawn as parts per million with IRandomIndexProvider.NextIndex(ProbabilityResolution).
        public const int ProbabilityResolution = 1_000_000;
    }

    // One named parameter of a weight function, with its default and inclusive range (or exclusive minimum).
    public sealed class FortuneFunctionParameter
    {
        public FortuneFunctionParameter(string name, double defaultValue, double minimum, double maximum, string description,
            bool minimumExclusive = false)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A parameter requires a name.", nameof(name)) : name.Trim();
            if (double.IsNaN(minimum) || double.IsNaN(maximum) || minimum > maximum) throw new ArgumentException($"Parameter '{Name}' has an invalid range.");
            DefaultValue = defaultValue;
            Minimum = minimum;
            Maximum = maximum;
            MinimumExclusive = minimumExclusive;
            Description = description ?? string.Empty;
            if (!Accepts(defaultValue)) throw new ArgumentException($"The default of parameter '{Name}' is outside its range.");
        }

        public string Name { get; }
        public double DefaultValue { get; }
        public double Minimum { get; }
        public double Maximum { get; }
        public bool MinimumExclusive { get; }
        public string Description { get; }

        public bool Accepts(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value <= Maximum && (MinimumExclusive ? value > Minimum : value >= Minimum);

        public string RangeText => $"{(MinimumExclusive ? "(" : "[")}{Minimum}, {Maximum}]";
    }

    // Immutable name -> value arguments of a weight function. Names are compared ordinally after trimming; a parameter
    // missing here uses the function's default, and names the function does not know are ignored.
    public sealed class FortuneFunctionArguments : IEquatable<FortuneFunctionArguments>
    {
        private readonly KeyValuePair<string, double>[] entries;

        public static readonly FortuneFunctionArguments Empty = new(Array.Empty<KeyValuePair<string, double>>());

        private FortuneFunctionArguments(KeyValuePair<string, double>[] sorted) => entries = sorted;

        public static FortuneFunctionArguments Create(IEnumerable<KeyValuePair<string, double>> values)
        {
            if (values == null) return Empty;
            var list = new List<KeyValuePair<string, double>>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                var name = pair.Key?.Trim();
                if (string.IsNullOrEmpty(name)) throw new ArgumentException("An argument requires a name.");
                if (!names.Add(name)) throw new ArgumentException($"Argument '{name}' is duplicated.");
                list.Add(new KeyValuePair<string, double>(name, pair.Value));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list.Count == 0 ? Empty : new FortuneFunctionArguments(list.ToArray());
        }

        public static FortuneFunctionArguments Of(string name, double value) =>
            Create(new[] { new KeyValuePair<string, double>(name, value) });

        public IReadOnlyList<KeyValuePair<string, double>> Entries => Array.AsReadOnly(entries);

        public bool TryGet(string name, out double value)
        {
            var key = name?.Trim();
            foreach (var entry in entries)
                if (string.Equals(entry.Key, key, StringComparison.Ordinal)) { value = entry.Value; return true; }
            value = 0d;
            return false;
        }

        public double ValueOf(FortuneFunctionParameter parameter) =>
            TryGet(parameter.Name, out var value) ? value : parameter.DefaultValue;

        // Argument names that are not parameters of the function (shown as Inspector warnings, otherwise ignored).
        public IReadOnlyList<string> UnknownNames(IFortuneWeightFunction function)
        {
            var unknown = new List<string>();
            foreach (var entry in entries)
            {
                var known = false;
                foreach (var parameter in function.Parameters)
                    if (string.Equals(parameter.Name, entry.Key, StringComparison.Ordinal)) { known = true; break; }
                if (!known) unknown.Add(entry.Key);
            }
            return unknown;
        }

        public bool Equals(FortuneFunctionArguments other)
        {
            if (other == null || other.entries.Length != entries.Length) return false;
            for (var i = 0; i < entries.Length; i++)
                if (!string.Equals(entries[i].Key, other.entries[i].Key, StringComparison.Ordinal) || !entries[i].Value.Equals(other.entries[i].Value))
                    return false;
            return true;
        }

        public override bool Equals(object obj) => obj is FortuneFunctionArguments other && Equals(other);
        public override int GetHashCode() => entries.Length;
    }

    // Weight of each multiplier in [minimum, maximum]. Weights need not sum to 1; the distribution normalizes them.
    // Implement this interface and add one line to FortuneWeightFunctionRegistry.Default to add a function.
    public interface IFortuneWeightFunction
    {
        // Stable ID stored in configuration assets. Never change it after shipping.
        string Id { get; }
        string DisplayName { get; }
        IReadOnlyList<FortuneFunctionParameter> Parameters { get; }
        // Must return a finite, non-negative value.
        double Weight(int multiplier, int minimumMultiplier, int maximumMultiplier, FortuneFunctionArguments arguments);
    }

    // x = n - minimum: every step up multiplies the weight by r (the discrete form of an exponential decay).
    public sealed class GeometricFortuneWeightFunction : IFortuneWeightFunction
    {
        public static readonly FortuneFunctionParameter Ratio =
            new("ratio", .7d, 0d, 1d, "한 단계 오를 때마다 확률이 곱해지는 비율", minimumExclusive: true);
        private static readonly FortuneFunctionParameter[] parameters = { Ratio };

        public string Id => "geometric";
        public string DisplayName => "기하 (지수)";
        public IReadOnlyList<FortuneFunctionParameter> Parameters => parameters;
        public double Weight(int multiplier, int minimumMultiplier, int maximumMultiplier, FortuneFunctionArguments arguments) =>
            Math.Pow(arguments.ValueOf(Ratio), multiplier - minimumMultiplier);
    }

    public sealed class PowerFortuneWeightFunction : IFortuneWeightFunction
    {
        public static readonly FortuneFunctionParameter Exponent = new("exponent", 2d, 0d, 8d, "n^(-s)의 지수 s. 클수록 낮은 배수에 몰린다");
        private static readonly FortuneFunctionParameter[] parameters = { Exponent };

        public string Id => "power";
        public string DisplayName => "거듭제곱";
        public IReadOnlyList<FortuneFunctionParameter> Parameters => parameters;
        public double Weight(int multiplier, int minimumMultiplier, int maximumMultiplier, FortuneFunctionArguments arguments) =>
            Math.Pow(multiplier, -arguments.ValueOf(Exponent));
    }

    public sealed class HalfNormalFortuneWeightFunction : IFortuneWeightFunction
    {
        public static readonly FortuneFunctionParameter Sigma = new("sigma", 3d, .1d, 50d, "표준편차 σ. 작을수록 높은 배수가 급격히 줄어든다");
        private static readonly FortuneFunctionParameter[] parameters = { Sigma };

        public string Id => "half-normal";
        public string DisplayName => "반정규";
        public IReadOnlyList<FortuneFunctionParameter> Parameters => parameters;
        public double Weight(int multiplier, int minimumMultiplier, int maximumMultiplier, FortuneFunctionArguments arguments)
        {
            var x = (double)(multiplier - minimumMultiplier);
            var sigma = arguments.ValueOf(Sigma);
            return Math.Exp(-x * x / (2d * sigma * sigma));
        }
    }

    public sealed class LinearFortuneWeightFunction : IFortuneWeightFunction
    {
        public static readonly FortuneFunctionParameter EndWeight = new("endWeight", .1d, 0d, 1d, "최대 배수의 가중치 (최소 배수는 1)");
        private static readonly FortuneFunctionParameter[] parameters = { EndWeight };

        public string Id => "linear";
        public string DisplayName => "선형";
        public IReadOnlyList<FortuneFunctionParameter> Parameters => parameters;
        public double Weight(int multiplier, int minimumMultiplier, int maximumMultiplier, FortuneFunctionArguments arguments)
        {
            var length = maximumMultiplier - minimumMultiplier;
            if (length == 0) return 1d;
            return 1d + (arguments.ValueOf(EndWeight) - 1d) * (multiplier - minimumMultiplier) / length;
        }
    }

    // Every multiplier adds the same expected bonus (n - 1)·P(n).
    public sealed class EqualExpectedFortuneWeightFunction : IFortuneWeightFunction
    {
        public string Id => "equal-expected";
        public string DisplayName => "기대 피해 균등";
        public IReadOnlyList<FortuneFunctionParameter> Parameters => Array.Empty<FortuneFunctionParameter>();
        public double Weight(int multiplier, int minimumMultiplier, int maximumMultiplier, FortuneFunctionArguments arguments) =>
            1d / (multiplier - 1);
    }

    public sealed class UniformFortuneWeightFunction : IFortuneWeightFunction
    {
        public string Id => "uniform";
        public string DisplayName => "균등";
        public IReadOnlyList<FortuneFunctionParameter> Parameters => Array.Empty<FortuneFunctionParameter>();
        public double Weight(int multiplier, int minimumMultiplier, int maximumMultiplier, FortuneFunctionArguments arguments) => 1d;
    }

    // ID -> weight function. Functions are registered explicitly (no reflection or polymorphic serialization), so IL2CPP
    // stripping and class renames cannot break saved configuration.
    public sealed class FortuneWeightFunctionRegistry
    {
        public const string DefaultFunctionId = "geometric";

        private readonly List<IFortuneWeightFunction> functions = new();

        public static FortuneWeightFunctionRegistry Default { get; } = CreateDefault();

        private static FortuneWeightFunctionRegistry CreateDefault()
        {
            var registry = new FortuneWeightFunctionRegistry();
            registry.Register(new GeometricFortuneWeightFunction());
            registry.Register(new PowerFortuneWeightFunction());
            registry.Register(new HalfNormalFortuneWeightFunction());
            registry.Register(new LinearFortuneWeightFunction());
            registry.Register(new EqualExpectedFortuneWeightFunction());
            registry.Register(new UniformFortuneWeightFunction());
            return registry;
        }

        public IReadOnlyList<IFortuneWeightFunction> Functions => functions.AsReadOnly();

        public void Register(IFortuneWeightFunction function)
        {
            if (function == null) throw new ArgumentNullException(nameof(function));
            var id = function.Id?.Trim();
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A weight function requires an id.", nameof(function));
            if (TryGet(id, out _)) throw new ArgumentException($"Weight function '{id}' is already registered.", nameof(function));
            functions.Add(function);
        }

        public bool TryGet(string id, out IFortuneWeightFunction function)
        {
            var key = id?.Trim();
            foreach (var candidate in functions)
                if (string.Equals(candidate.Id, key, StringComparison.Ordinal)) { function = candidate; return true; }
            function = null;
            return false;
        }
    }
}
