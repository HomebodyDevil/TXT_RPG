using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.Encounters
{
    public enum EncounterRollOutcome
    {
        Occurred,
        NoElementsSelected,
        ChanceFailed,
        NoKindAvailable
    }

    public sealed class EncounterRollResult
    {
        private static readonly string[] NoElements = Array.Empty<string>();

        private EncounterRollResult(EncounterRollOutcome outcome, EncounterKind kind, IReadOnlyList<string> elementIds)
        {
            Outcome = outcome;
            Kind = kind;
            ElementIds = elementIds ?? NoElements;
        }

        public EncounterRollOutcome Outcome { get; }
        // Meaningful only when Outcome is Occurred.
        public EncounterKind Kind { get; }
        // At least one id (in input order) when Outcome is Occurred, otherwise empty.
        public IReadOnlyList<string> ElementIds { get; }
        public bool Occurred => Outcome == EncounterRollOutcome.Occurred;

        public static EncounterRollResult NotOccurred(EncounterRollOutcome outcome)
        {
            if (outcome == EncounterRollOutcome.Occurred) throw new ArgumentException("Use Occurrence for an occurred result.", nameof(outcome));
            return new EncounterRollResult(outcome, default, NoElements);
        }

        public static EncounterRollResult Occurrence(EncounterKind kind, IReadOnlyList<string> elementIds)
        {
            if (elementIds == null || elementIds.Count == 0) throw new ArgumentException("An occurred encounter needs at least one element.", nameof(elementIds));
            return new EncounterRollResult(EncounterRollOutcome.Occurred, kind, elementIds);
        }
    }

    // Decides whether an encounter happens, for which selected elements, and of which kind.
    // Random calls, in order: occurrence (1), one inclusion check per selected element, a uniform pick when none was
    // included, then the kind. Occurrence and the element/kind draws are separate calls, so neither changes the other's odds.
    public sealed class EncounterRoller
    {
        public const int ChanceResolution = 10000;
        public const int WeightResolution = 1000;
        private static readonly EncounterKind[] Kinds = { EncounterKind.Combat, EncounterKind.Event, EncounterKind.Rest };

        private readonly IRandomIndexProvider random;

        public EncounterRoller(IRandomIndexProvider random) =>
            this.random = random ?? throw new ArgumentNullException(nameof(random));

        public EncounterRollResult Roll(IReadOnlyList<string> selectedElementIds, EncounterRollSettings settings)
        {
            settings ??= EncounterRollSettings.Default;
            if (selectedElementIds == null || selectedElementIds.Count == 0)
                return EncounterRollResult.NotOccurred(EncounterRollOutcome.NoElementsSelected);
            long totalWeight = 0;
            foreach (var kind in Kinds) totalWeight += WeightUnits(settings.GetWeight(kind));
            if (totalWeight <= 0) return EncounterRollResult.NotOccurred(EncounterRollOutcome.NoKindAvailable);

            if (!Check(settings.OccurrenceChance)) return EncounterRollResult.NotOccurred(EncounterRollOutcome.ChanceFailed);

            var included = new List<string>(selectedElementIds.Count);
            foreach (var id in selectedElementIds)
                if (Check(settings.ElementInclusionChance)) included.Add(id);
            if (included.Count == 0) included.Add(selectedElementIds[random.NextIndex(selectedElementIds.Count)]);

            // Weights are whole thousandths; a total above int.MaxValue is scaled down proportionally.
            var scale = totalWeight > int.MaxValue ? (double)int.MaxValue / totalWeight : 1d;
            var units = new int[Kinds.Length];
            var total = 0;
            for (var index = 0; index < Kinds.Length; index++)
            {
                units[index] = (int)Math.Floor(WeightUnits(settings.GetWeight(Kinds[index])) * scale);
                total += units[index];
            }
            var pick = random.NextIndex(Math.Max(1, total));
            var chosen = Kinds[Kinds.Length - 1];
            for (var index = 0; index < Kinds.Length; index++)
            {
                if (pick < units[index]) { chosen = Kinds[index]; break; }
                pick -= units[index];
            }
            return EncounterRollResult.Occurrence(chosen, included);
        }

        // NextIndex(10000) < round(p × 10000): p = 0 always fails, p = 1 always succeeds.
        private bool Check(float chance)
        {
            var threshold = (int)Math.Round(chance * ChanceResolution, MidpointRounding.AwayFromZero);
            return random.NextIndex(ChanceResolution) < threshold;
        }

        private static long WeightUnits(float weight) =>
            weight <= 0f ? 0L : (long)Math.Min(Math.Round((double)weight * WeightResolution, MidpointRounding.AwayFromZero), int.MaxValue);
    }
}
