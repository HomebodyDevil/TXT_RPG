using System;
using System.Linq;
using TxTRPG.Gameplay.Exploration.Events;
using UnityEngine;

// Built-in choice conditions. A new condition is one [Serializable] class implementing IExplorationEventCondition, with
// serialized fields only. Add [UnityEngine.Scripting.APIUpdating.MovedFrom] when renaming or moving one.
namespace TxTRPG.Application.Exploration.Events
{
    [Serializable]
    public sealed class HealthAtLeastCondition : IExplorationEventCondition
    {
        [SerializeField, Min(1)] private int value = 1;

        public HealthAtLeastCondition() { }
        public HealthAtLeastCondition(int minimum) => value = minimum;

        public bool Validate(out string reason) =>
            value >= 1 ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "The value must be at least 1.");

        public bool IsSatisfied(IExplorationEventContext context, out string reason)
        {
            reason = ExplorationEventStoryFormatter.HealthRequired(value);
            return context.TryGetHealth(out var health) && health.Current >= value;
        }
    }

    [Serializable]
    public sealed class ReelCountAtLeastCondition : IExplorationEventCondition
    {
        [SerializeField, Min(1)] private int value = 1;

        public ReelCountAtLeastCondition() { }
        public ReelCountAtLeastCondition(int minimum) => value = minimum;

        public bool Validate(out string reason) =>
            value >= 1 ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "The value must be at least 1.");

        public bool IsSatisfied(IExplorationEventContext context, out string reason)
        {
            reason = ExplorationEventStoryFormatter.ReelCountAtLeastRequired(value);
            return context.TryGetSlotMachine(out var machine) && machine.ReelCount >= value;
        }
    }

    [Serializable]
    public sealed class ReelCountAtMostCondition : IExplorationEventCondition
    {
        [SerializeField, Min(1)] private int value = 1;

        public ReelCountAtMostCondition() { }
        public ReelCountAtMostCondition(int maximum) => value = maximum;

        public bool Validate(out string reason) =>
            value >= 1 ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "The value must be at least 1.");

        public bool IsSatisfied(IExplorationEventContext context, out string reason)
        {
            reason = ExplorationEventStoryFormatter.ReelCountAtMostRequired(value);
            return context.TryGetSlotMachine(out var machine) && machine.ReelCount <= value;
        }
    }

    // Counts owned runes of the definition, placed on a line or not.
    [Serializable]
    public sealed class HasRuneDefinitionCondition : IExplorationEventCondition
    {
        [SerializeField] private string definitionId = "rune.attack";
        [SerializeField, Min(1)] private int count = 1;

        public HasRuneDefinitionCondition() { }
        public HasRuneDefinitionCondition(string runeDefinitionId, int runeCount = 1) { definitionId = runeDefinitionId; count = runeCount; }

        public bool Validate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) return ExplorationEventEffectSupport.Fail(out reason, "A rune definition ID is required.");
            return count >= 1 ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "The count must be at least 1.");
        }

        public bool IsSatisfied(IExplorationEventContext context, out string reason)
        {
            var id = definitionId.Trim();
            if (!context.TryGetRunes(out var runes)) { reason = ExplorationEventStoryFormatter.RuneRequired(id, count); return false; }
            var name = runes.Catalog.TryGet(id, out var definition) ? definition.DisplayName : id;
            reason = ExplorationEventStoryFormatter.RuneRequired(name, count);
            return runes.Inventory.Snapshot().Count(rune => rune.DefinitionId == id) >= count;
        }
    }
}
