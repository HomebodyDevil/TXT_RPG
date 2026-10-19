using System;

namespace TxTRPG.Gameplay.Encounters
{
    // Serialized as an int (configuration assets, future save data): keep the values fixed.
    public enum EncounterKind
    {
        Combat = 0,
        Event = 1,
        Rest = 2
    }

    // Immutable roll parameters. Chances are clamped to 0..1 (NaN becomes 0); weights below 0 or NaN become 0.
    public sealed class EncounterRollSettings
    {
        public const float DefaultOccurrenceChance = .5f;
        public const float DefaultElementInclusionChance = .5f;
        public const float DefaultWeight = 1f;

        public static EncounterRollSettings Default { get; } = new(
            DefaultOccurrenceChance, DefaultElementInclusionChance, DefaultWeight, DefaultWeight, DefaultWeight);

        public EncounterRollSettings(float occurrenceChance, float elementInclusionChance,
            float combatWeight, float eventWeight, float restWeight)
        {
            OccurrenceChance = Chance(occurrenceChance);
            ElementInclusionChance = Chance(elementInclusionChance);
            CombatWeight = Weight(combatWeight);
            EventWeight = Weight(eventWeight);
            RestWeight = Weight(restWeight);
        }

        public float OccurrenceChance { get; }
        public float ElementInclusionChance { get; }
        public float CombatWeight { get; }
        public float EventWeight { get; }
        public float RestWeight { get; }

        public float GetWeight(EncounterKind kind) => kind switch
        {
            EncounterKind.Combat => CombatWeight,
            EncounterKind.Event => EventWeight,
            EncounterKind.Rest => RestWeight,
            _ => 0f
        };

        public EncounterRollSettings WithOccurrenceChance(float value) => new(value, ElementInclusionChance, CombatWeight, EventWeight, RestWeight);
        public EncounterRollSettings WithElementInclusionChance(float value) => new(OccurrenceChance, value, CombatWeight, EventWeight, RestWeight);
        public EncounterRollSettings WithCombatWeight(float value) => new(OccurrenceChance, ElementInclusionChance, value, EventWeight, RestWeight);
        public EncounterRollSettings WithEventWeight(float value) => new(OccurrenceChance, ElementInclusionChance, CombatWeight, value, RestWeight);
        public EncounterRollSettings WithRestWeight(float value) => new(OccurrenceChance, ElementInclusionChance, CombatWeight, EventWeight, value);

        public EncounterRollSettings WithWeights(float combat, float @event, float rest) =>
            new(OccurrenceChance, ElementInclusionChance, combat, @event, rest);

        private static float Chance(float value) => float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);
        private static float Weight(float value) => float.IsNaN(value) || value < 0f ? 0f : float.IsPositiveInfinity(value) ? float.MaxValue : value;
    }
}
