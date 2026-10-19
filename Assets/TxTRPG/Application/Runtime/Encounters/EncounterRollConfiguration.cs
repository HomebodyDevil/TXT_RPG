using TxTRPG.Gameplay.Encounters;
using UnityEngine;

namespace TxTRPG.Application.Encounters
{
    // Default encounter roll values. The debug panel changes only the running settings, never this asset.
    [CreateAssetMenu(menuName = "TxT RPG/Encounters/Encounter Roll Configuration", fileName = "EncounterRollConfiguration")]
    public sealed class EncounterRollConfiguration : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float occurrenceChance = EncounterRollSettings.DefaultOccurrenceChance;
        [SerializeField, Range(0f, 1f)] private float elementInclusionChance = EncounterRollSettings.DefaultElementInclusionChance;
        [SerializeField, Min(0f)] private float combatWeight = EncounterRollSettings.DefaultWeight;
        [SerializeField, Min(0f)] private float eventWeight = EncounterRollSettings.DefaultWeight;
        [SerializeField, Min(0f)] private float restWeight = EncounterRollSettings.DefaultWeight;

        public EncounterRollSettings CreateSettings() =>
            new(occurrenceChance, elementInclusionChance, combatWeight, eventWeight, restWeight);

        public void SetValues(EncounterRollSettings settings)
        {
            settings ??= EncounterRollSettings.Default;
            occurrenceChance = settings.OccurrenceChance;
            elementInclusionChance = settings.ElementInclusionChance;
            combatWeight = settings.CombatWeight;
            eventWeight = settings.EventWeight;
            restWeight = settings.RestWeight;
        }
    }
}
