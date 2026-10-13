using System;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Runes;
using TxTRPG.Gameplay.SlotMachine;
using SlotMachineModel = TxTRPG.Gameplay.SlotMachine.SlotMachine;

namespace TxTRPG.Gameplay.Exploration.Events
{
    public static class ExplorationEventLimits
    {
        // Page entries allowed in one event run (loops are allowed). Exceeding it ends the event with an error.
        public const int MaximumPageVisits = 64;
    }

    public static class ExplorationEventTargets
    {
        // The "next" value that ends the event.
        public const string End = "End";
        public static bool IsEnd(string target) => string.Equals(target?.Trim(), End, StringComparison.Ordinal);
    }

    // Runes an effect may change: the session catalog, the owned runes and the rune lines.
    public sealed class ExplorationEventRuneAccess
    {
        public ExplorationEventRuneAccess(RuneDefinitionCatalog catalog, RuneInventory inventory, RuneLoadout loadout)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public RuneDefinitionCatalog Catalog { get; }
        public RuneInventory Inventory { get; }
        public RuneLoadout Loadout { get; }
    }

    // The capabilities effects and conditions use. Each one may be unavailable (for example the slot machine when its
    // configuration is invalid); an effect then skips instead of failing the event. A new game system an effect needs
    // is added here as another TryGet capability and connected in the Application adapter.
    public interface IExplorationEventContext
    {
        bool TryGetHealth(out HealthState health);
        bool TryGetSlotMachine(out SlotMachineModel machine);
        bool TryGetRunes(out ExplorationEventRuneAccess runes);
        bool TryGetFortuneCritical(out IFortuneCriticalRuleStore fortuneCritical);
    }

    public enum ExplorationEventEffectStatus
    {
        Applied = 1,
        Skipped = 2
    }

    public sealed class ExplorationEventEffectResult
    {
        private ExplorationEventEffectResult(ExplorationEventEffectStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public ExplorationEventEffectStatus Status { get; }
        // Applied: the Story line of the change (may be empty). Skipped: why it could not be applied.
        public string Message { get; }

        public static ExplorationEventEffectResult Applied(string storyLine = "") => new(ExplorationEventEffectStatus.Applied, storyLine);
        public static ExplorationEventEffectResult Skipped(string reason) => new(ExplorationEventEffectStatus.Skipped, reason);
    }

    // A state change of an event. Implementations are shared, stateless authoring objects (a ScriptableObject owns them),
    // so Apply must not store run state in fields. An effect that changes several things undoes its own changes when it
    // cannot finish, so it is never half applied.
    public interface IExplorationEventEffect
    {
        // Static checks that do not need the session (amounts, required IDs).
        bool Validate(out string reason);
        ExplorationEventEffectResult Apply(IExplorationEventContext context);
    }

    // Whether a choice option can be picked now. The reason is shown next to a disabled option.
    public interface IExplorationEventCondition
    {
        bool Validate(out string reason);
        bool IsSatisfied(IExplorationEventContext context, out string reason);
    }
}
