using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.SlotMachine
{
    public enum FortuneDistributionSource
    {
        Function = 0,
        Table = 1
    }

    // How likely each multiplier is once a Fortune Critical happens: a registered weight function with its arguments,
    // or a weight table that starts at the minimum multiplier. Immutable.
    public sealed class FortuneMultiplierDistribution : IEquatable<FortuneMultiplierDistribution>
    {
        private readonly double[] tableWeights;

        private FortuneMultiplierDistribution(FortuneDistributionSource source, string functionId, FortuneFunctionArguments arguments, double[] weights)
        {
            Source = source;
            FunctionId = functionId ?? string.Empty;
            Arguments = arguments ?? FortuneFunctionArguments.Empty;
            tableWeights = weights ?? Array.Empty<double>();
        }

        public static FortuneMultiplierDistribution Default { get; } =
            Function(FortuneWeightFunctionRegistry.DefaultFunctionId, FortuneFunctionArguments.Empty);

        public static FortuneMultiplierDistribution Function(string functionId, FortuneFunctionArguments arguments) =>
            new(FortuneDistributionSource.Function, functionId?.Trim(), arguments, null);

        // weights[0] is the weight of the minimum multiplier.
        public static FortuneMultiplierDistribution Table(IEnumerable<double> weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            return new FortuneMultiplierDistribution(FortuneDistributionSource.Table, string.Empty, FortuneFunctionArguments.Empty,
                new List<double>(weights).ToArray());
        }

        public FortuneDistributionSource Source { get; }
        public string FunctionId { get; }
        public FortuneFunctionArguments Arguments { get; }
        public IReadOnlyList<double> TableWeights => Array.AsReadOnly(tableWeights);

        public bool Equals(FortuneMultiplierDistribution other)
        {
            if (other == null || other.Source != Source) return false;
            if (Source == FortuneDistributionSource.Function)
                return string.Equals(FunctionId, other.FunctionId, StringComparison.Ordinal) && Arguments.Equals(other.Arguments);
            if (other.tableWeights.Length != tableWeights.Length) return false;
            for (var i = 0; i < tableWeights.Length; i++) if (!tableWeights[i].Equals(other.tableWeights[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is FortuneMultiplierDistribution other && Equals(other);
        public override int GetHashCode() => (int)Source * 31 + tableWeights.Length;
    }

    // Integer probability table of a distribution over [minimum, maximum]: parts per million that sum to exactly
    // ProbabilityResolution (largest remainder; equal remainders go to the lower multiplier first). Built once per rule,
    // so sampling neither allocates nor uses floating point.
    public sealed class FortuneMultiplierTable
    {
        private readonly int[] partsPerMillion;
        private readonly int[] cumulative;
        private readonly int[] lostMultipliers;

        private FortuneMultiplierTable(int minimum, int maximum, int[] parts, int[] lost)
        {
            Minimum = minimum;
            Maximum = maximum;
            partsPerMillion = parts;
            lostMultipliers = lost;
            cumulative = new int[parts.Length];
            var running = 0;
            double expected = 0d;
            for (var i = 0; i < parts.Length; i++)
            {
                running += parts[i];
                cumulative[i] = running;
                expected += (double)(minimum + i) * parts[i] / FortuneCriticalLimits.ProbabilityResolution;
            }
            ExpectedMultiplier = expected;
        }

        public int Minimum { get; }
        public int Maximum { get; }
        public int Count => partsPerMillion.Length;
        // E[n] given that a Fortune Critical happened, from the integer table (what sampling actually produces).
        public double ExpectedMultiplier { get; }
        // Multipliers with a positive weight whose share rounded to 0 parts per million; they can never be drawn.
        public IReadOnlyList<int> UnreachableMultipliers => Array.AsReadOnly(lostMultipliers);

        public int PartsPerMillion(int multiplier) =>
            multiplier < Minimum || multiplier > Maximum ? 0 : partsPerMillion[multiplier - Minimum];

        public double Probability(int multiplier) => PartsPerMillion(multiplier) / (double)FortuneCriticalLimits.ProbabilityResolution;

        // One draw of NextIndex(ProbabilityResolution); none when the range has a single multiplier.
        public int Sample(IRandomIndexProvider random)
        {
            if (Minimum == Maximum) return Minimum;
            var draw = random.NextIndex(FortuneCriticalLimits.ProbabilityResolution);
            if (draw < 0 || draw >= FortuneCriticalLimits.ProbabilityResolution)
                throw new InvalidOperationException($"The random source returned {draw} for a Fortune Critical multiplier.");
            for (var i = 0; i < cumulative.Length; i++)
                if (draw < cumulative[i]) return Minimum + i;
            return Maximum;
        }

        public static bool TryCreate(int minimum, int maximum, FortuneMultiplierDistribution distribution,
            FortuneWeightFunctionRegistry registry, out FortuneMultiplierTable table, out string reason)
        {
            table = null;
            if (!TryValidateRange(minimum, maximum, out reason)) return false;
            if (distribution == null) { reason = "A multiplier distribution is required."; return false; }
            var count = maximum - minimum + 1;
            var weights = new double[count];
            if (distribution.Source == FortuneDistributionSource.Table)
            {
                if (distribution.TableWeights.Count != count)
                {
                    reason = $"The weight table has {distribution.TableWeights.Count} entries but ×{minimum}..×{maximum} needs {count}.";
                    return false;
                }
                for (var i = 0; i < count; i++) weights[i] = distribution.TableWeights[i];
            }
            else
            {
                if (registry == null) { reason = "A weight function registry is required."; return false; }
                if (!registry.TryGet(distribution.FunctionId, out var function))
                {
                    reason = $"Weight function '{distribution.FunctionId}' is not registered.";
                    return false;
                }
                foreach (var parameter in function.Parameters)
                {
                    var value = distribution.Arguments.ValueOf(parameter);
                    if (!parameter.Accepts(value))
                    {
                        reason = $"Parameter '{parameter.Name}' of '{function.Id}' is {value}, outside {parameter.RangeText}.";
                        return false;
                    }
                }
                for (var i = 0; i < count; i++) weights[i] = function.Weight(minimum + i, minimum, maximum, distribution.Arguments);
            }

            double sum = 0d;
            for (var i = 0; i < count; i++)
            {
                var weight = weights[i];
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0d)
                {
                    reason = $"The weight of ×{minimum + i} is {weight}; weights must be finite and not negative.";
                    return false;
                }
                sum += weight;
            }
            if (!(sum > 0d) || double.IsInfinity(sum)) { reason = "The weights must have a positive, finite sum."; return false; }

            table = new FortuneMultiplierTable(minimum, maximum, Apportion(weights, sum, out var lost, minimum), lost);
            reason = string.Empty;
            return true;
        }

        public static bool TryValidateRange(int minimum, int maximum, out string reason)
        {
            if (minimum < FortuneCriticalLimits.MinimumMultiplier || maximum > FortuneCriticalLimits.MaximumMultiplier || minimum > maximum)
            {
                reason = $"The multiplier range ×{minimum}..×{maximum} must satisfy {FortuneCriticalLimits.MinimumMultiplier} ≤ minimum ≤ maximum ≤ {FortuneCriticalLimits.MaximumMultiplier}.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        // Largest remainder method, deterministic for the same input.
        private static int[] Apportion(double[] weights, double sum, out int[] lost, int minimum)
        {
            const int resolution = FortuneCriticalLimits.ProbabilityResolution;
            var parts = new int[weights.Length];
            var remainders = new double[weights.Length];
            long assigned = 0;
            for (var i = 0; i < weights.Length; i++)
            {
                var quota = weights[i] / sum * resolution;
                var whole = (int)Math.Min(resolution, Math.Floor(quota));
                parts[i] = whole;
                remainders[i] = quota - whole;
                assigned += whole;
            }
            var order = new int[weights.Length];
            for (var i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                var byRemainder = remainders[b].CompareTo(remainders[a]);
                return byRemainder != 0 ? byRemainder : a.CompareTo(b);
            });
            var leftover = resolution - assigned;
            for (var i = 0; leftover > 0; i = (i + 1) % order.Length, leftover--) parts[order[i]]++;
            // Rounding can only overshoot by floating-point noise; take it back from the largest shares.
            while (leftover < 0)
            {
                var largest = 0;
                for (var i = 1; i < parts.Length; i++) if (parts[i] > parts[largest]) largest = i;
                parts[largest]--;
                leftover++;
            }
            var unreachable = new List<int>();
            for (var i = 0; i < parts.Length; i++) if (weights[i] > 0d && parts[i] == 0) unreachable.Add(minimum + i);
            lost = unreachable.ToArray();
            return parts;
        }
    }
}
