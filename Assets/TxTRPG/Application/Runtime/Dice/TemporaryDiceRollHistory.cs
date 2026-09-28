using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Combat;

namespace TxTRPG.Application.Dice
{
    public enum TemporaryDiceHistoryMode { LatestCombatNode, RecentSessionCombats }

    public sealed class TemporaryDiceTurnRecord
    {
        public TemporaryDiceTurnRecord(string runId, string nodeId, int order,
            IReadOnlyList<PlayerDieRoll> rolls, TemporaryCombatTurnResult turn)
        {
            RunId = runId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            Order = order;
            var rollCopy = new PlayerDieRoll[rolls.Count];
            for (var i = 0; i < rollCopy.Length; i++) rollCopy[i] = rolls[i];
            Rolls = Array.AsReadOnly(rollCopy);
            var eventCopy = new TemporaryCombatEvent[turn.Events.Count];
            for (var i = 0; i < eventCopy.Length; i++) eventCopy[i] = turn.Events[i];
            Events = Array.AsReadOnly(eventCopy);
            IsVictory = Array.Exists(eventCopy, item => item.Kind == TemporaryCombatEventKind.Victory);
            IsDefeat = Array.Exists(eventCopy, item => item.Kind == TemporaryCombatEventKind.Defeat);
        }

        public string RunId { get; }
        public string NodeId { get; }
        public int Order { get; }
        public IReadOnlyList<PlayerDieRoll> Rolls { get; }
        public IReadOnlyList<TemporaryCombatEvent> Events { get; }
        public bool IsVictory { get; }
        public bool IsDefeat { get; }
    }

    public sealed class TemporaryDiceRollHistory
    {
        private sealed class CombatRecords
        {
            public string RunId;
            public string NodeId;
            public readonly List<TemporaryDiceTurnRecord> Turns = new();
        }

        private readonly List<CombatRecords> combats = new();
        private CombatRecords current;
        private TemporaryDiceHistoryMode mode;
        private int maximumCombats = 4;
        public TemporaryDiceHistoryMode Mode => mode;
        public int MaximumCombats => maximumCombats;
        public IReadOnlyList<TemporaryDiceTurnRecord> CurrentTurns =>
            current == null ? Array.Empty<TemporaryDiceTurnRecord>() : current.Turns.AsReadOnly();
        public event Action Changed;

        public void Configure(TemporaryDiceHistoryMode newMode, int newMaximumCombats)
        {
            if (!Enum.IsDefined(typeof(TemporaryDiceHistoryMode), newMode))
                throw new ArgumentOutOfRangeException(nameof(newMode));
            mode = newMode;
            maximumCombats = Math.Max(1, newMaximumCombats);
            Trim();
            Changed?.Invoke();
        }

        public void BeginCombat(string runId, string nodeId)
        {
            runId ??= string.Empty;
            nodeId ??= string.Empty;
            if (current != null && current.RunId == runId && current.NodeId == nodeId) return;
            current = new CombatRecords { RunId = runId, NodeId = nodeId };
            combats.Add(current);
            Trim();
            Changed?.Invoke();
        }

        public TemporaryDiceTurnRecord AddTurn(string runId, string nodeId,
            IReadOnlyList<PlayerDieRoll> rolls, TemporaryCombatTurnResult turn)
        {
            if (current == null || current.RunId != (runId ?? string.Empty) ||
                current.NodeId != (nodeId ?? string.Empty))
                throw new InvalidOperationException("The combat record scope changed before the turn completed.");
            var record = new TemporaryDiceTurnRecord(current.RunId, current.NodeId,
                current.Turns.Count + 1, rolls, turn);
            current.Turns.Add(record);
            Changed?.Invoke();
            return record;
        }

        public IReadOnlyList<TemporaryDiceTurnRecord> Snapshot()
        {
            var result = new List<TemporaryDiceTurnRecord>();
            foreach (var combat in combats)
                result.AddRange(combat.Turns);
            return result.AsReadOnly();
        }

        private void Trim()
        {
            var limit = mode == TemporaryDiceHistoryMode.LatestCombatNode ? 1 : maximumCombats;
            while (combats.Count > limit)
                combats.RemoveAt(0);
        }
    }

    public sealed class TemporaryDiceResultPreferences
    {
        public bool AutoShowResults { get; set; } = true;
        public bool ReduceMotion { get; set; }
    }
}
