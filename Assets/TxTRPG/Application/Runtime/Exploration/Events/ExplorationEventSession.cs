using System;
using System.Collections.Generic;
using TxTRPG.Application.Players;
using TxTRPG.Application.Runes;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration.Events;
using TxTRPG.Gameplay.SlotMachine;
using SlotMachineModel = TxTRPG.Gameplay.SlotMachine.SlotMachine;

namespace TxTRPG.Application.Exploration.Events
{
    // IExplorationEventContext over the session state: the exploration health, the session slot machine, runes and
    // Fortune Critical rules. A state that does not exist is reported as an unavailable capability.
    public sealed class ExplorationEventContext : IExplorationEventContext
    {
        private readonly HealthState health;
        private readonly SlotMachineModel machine;
        private readonly ExplorationEventRuneAccess runes;
        private readonly IFortuneCriticalRuleStore fortuneCritical;

        public ExplorationEventContext(HealthState explorationHealth, TemporarySlotMachineState slotMachine, TemporaryRuneState runeState,
            IFortuneCriticalRuleStore fortune)
        {
            health = explorationHealth;
            machine = slotMachine?.Machine;
            runes = runeState == null ? null : new ExplorationEventRuneAccess(runeState.Catalog, runeState.Inventory, runeState.Loadout);
            fortuneCritical = fortune;
        }

        public static ExplorationEventContext FromSession(PlayerSessionHost host) =>
            host == null ? new ExplorationEventContext(null, null, null, null) :
            new ExplorationEventContext(host.TemporaryExploration?.PlayerHealth, host.TemporarySlotMachine, host.TemporaryRunes, host.TemporaryFortuneCritical);

        public bool TryGetHealth(out HealthState value) { value = health; return value != null; }
        public bool TryGetSlotMachine(out SlotMachineModel value) { value = machine; return value != null; }
        public bool TryGetRunes(out ExplorationEventRuneAccess value) { value = runes; return value != null; }
        public bool TryGetFortuneCritical(out IFortuneCriticalRuleStore value) { value = fortuneCritical; return value != null; }
    }

    // A slot check whose result was decided once (spin + Fortune Critical) and is kept until it is resolved exactly once.
    public sealed class TemporaryExplorationEventSlotCheck
    {
        public TemporaryExplorationEventSlotCheck(long actionId, int step, SlotSpinResult spin, FortuneCriticalResult fortune)
        {
            ActionId = actionId;
            Step = step;
            Spin = spin ?? throw new ArgumentNullException(nameof(spin));
            Fortune = fortune ?? throw new ArgumentNullException(nameof(fortune));
            if (!ReferenceEquals(fortune.Spin, spin)) throw new ArgumentException("The Fortune Critical result belongs to another spin.", nameof(fortune));
        }

        public long ActionId { get; }
        // The execution step the result is reported for.
        public int Step { get; }
        public SlotSpinResult Spin { get; }
        public FortuneCriticalResult Fortune { get; }
        public bool Resolved { get; private set; }

        internal bool TryMarkResolved()
        {
            if (Resolved) return false;
            Resolved = true;
            return true;
        }
    }

    // The event node in progress, kept by PlayerSessionHost. A controller created by a Scene reload binds to it again; it
    // never picks a new event or applies a step twice.
    public sealed class TemporaryExplorationEventRecord
    {
        private readonly List<string> pendingStory = new();

        public TemporaryExplorationEventRecord(string runId, string nodeId, string eventId, ExplorationEventExecution execution)
        {
            RunId = runId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            Execution = execution;
        }

        public string RunId { get; }
        public string NodeId { get; }
        public string EventId { get; }
        // Null when the event could not be loaded (the node then completes as EventUnavailable).
        public ExplorationEventExecution Execution { get; }
        public bool IsUnavailable => Execution == null;
        public TemporaryExplorationEventSlotCheck SlotCheck { get; internal set; }
        // Story lines not yet written to a Story panel (for example while the Scene was reloading).
        public IReadOnlyList<string> PendingStory => pendingStory;

        public void AddStory(IEnumerable<string> lines)
        {
            if (lines == null) return;
            foreach (var line in lines) if (!string.IsNullOrWhiteSpace(line)) pendingStory.Add(line);
        }

        public void AddStory(string line) { if (!string.IsNullOrWhiteSpace(line)) pendingStory.Add(line); }

        public IReadOnlyList<string> TakeStory()
        {
            var lines = pendingStory.ToArray();
            pendingStory.Clear();
            return lines;
        }
    }
}
