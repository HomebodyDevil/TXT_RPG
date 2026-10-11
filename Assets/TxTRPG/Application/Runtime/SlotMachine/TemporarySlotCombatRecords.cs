using System;
using System.Collections.Generic;
using System.Text;
using TxTRPG.Application.Combat;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Runes;
using TxTRPG.Gameplay.SlotMachine;

namespace TxTRPG.Application.SlotMachine
{
    // A spin accepted by the action button, kept by the session until it is applied. It cannot be spun again; a Scene
    // reload or a game window keeps the same record and it is applied exactly once.
    public sealed class TemporarySlotAcceptedAction
    {
        public TemporarySlotAcceptedAction(long actionId, string runId, string nodeId, TemporaryCombatState combat,
            SlotSpinResult spin, RuneAllocation allocation)
        {
            ActionId = actionId;
            RunId = runId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            Spin = spin ?? throw new ArgumentNullException(nameof(spin));
            Allocation = allocation ?? throw new ArgumentNullException(nameof(allocation));
        }

        public long ActionId { get; }
        public string RunId { get; }
        public string NodeId { get; }
        public TemporaryCombatState Combat { get; }
        public SlotSpinResult Spin { get; }
        public RuneAllocation Allocation { get; }
        public bool Applied { get; private set; }

        internal void MarkApplied() => Applied = true;
    }

    public sealed class TemporarySlotTurnRecord
    {
        public TemporarySlotTurnRecord(string runId, string nodeId, SlotSpinResult spin, RuneAllocation allocation,
            TemporaryCombatTurnResult turn)
        {
            RunId = runId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            Spin = spin ?? throw new ArgumentNullException(nameof(spin));
            Allocation = allocation ?? throw new ArgumentNullException(nameof(allocation));
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            var events = new TemporaryCombatEvent[turn.Events.Count];
            for (var i = 0; i < events.Length; i++) events[i] = turn.Events[i];
            Events = Array.AsReadOnly(events);
        }

        public string RunId { get; }
        public string NodeId { get; }
        public SlotSpinResult Spin { get; }
        public RuneAllocation Allocation { get; }
        public IReadOnlyList<TemporaryCombatEvent> Events { get; }
    }

    // The one place where rune effects become combat actions. Unsupported effects are rejected, never guessed.
    public static class RuneCombatActions
    {
        public static PlayerCombatActionKind ToActionKind(RuneEffectKind effect) => effect switch
        {
            RuneEffectKind.Attack => PlayerCombatActionKind.Attack,
            RuneEffectKind.Heal => PlayerCombatActionKind.Heal,
            _ => throw new InvalidOperationException($"Rune effect '{effect}' has no combat action.")
        };

        // Line order is kept; lines that received 0 take no action. Each action's source is its rune line ID.
        public static IReadOnlyList<PlayerCombatAction> FromAllocation(RuneAllocation allocation)
        {
            if (allocation == null) throw new ArgumentNullException(nameof(allocation));
            var actions = new List<PlayerCombatAction>(allocation.Lines.Count);
            foreach (var line in allocation.Lines)
            {
                var kind = ToActionKind(line.EffectKind);
                if (line.Amount > 0) actions.Add(new PlayerCombatAction(kind, line.Amount, line.LineId));
            }
            return actions;
        }
    }

    // Texts of the slot machine combat path (Story lines and panel texts). Replaceable by a localization provider later,
    // like DiceRollStoryFormatter.
    public static class SlotCombatStoryFormatter
    {
        // Slot machine panel summary texts.
        public const string TotalLabel = "합계";
        public const string IdleTotalValue = "—";
        public const string SpinningTotalValue = "…";
        public const string RuneColumnTitle = "룬";
        public const string ValueColumnTitle = "쌓임 → 분배";
        public const string NoRuneLines = "장착된 룬이 없습니다";
        public const string OverflowFormat = "외 {0}줄";

        public static string TotalValue(int total) => total.ToString();

        // Rune line row value before a spin ("×2") and after it ("×2 → 6").
        public static string LineWeight(int weight) => $"×{weight}";
        public static string LineAllocation(int weight, int amount) => $"×{weight} → {amount}";

        public static string EffectName(RuneEffectKind effect) => effect switch
        {
            RuneEffectKind.Attack => "공격",
            RuneEffectKind.Heal => "회복",
            _ => "알 수 없는 효과"
        };

        // "[슬롯] 3 + 5 + 2 = 10"
        public static string Spin(SlotSpinResult spin)
        {
            var builder = new StringBuilder("[슬롯] ");
            for (var i = 0; i < spin.Reels.Count; i++)
            {
                if (i > 0) builder.Append(" + ");
                builder.Append(spin.Reels[i].PaylineValue);
            }
            return builder.Append(" = ").Append(spin.PaylineTotal).ToString();
        }

        // "[룬] 1번 줄 공격 룬 ×1: 5", or "... : 0 (실행하지 않음)" for a line that received nothing.
        public static string Line(RuneAllocationLine line) =>
            $"[룬] {line.Order}번 줄 {line.DisplayName} ×{line.Weight}: {line.Amount}{(line.Amount == 0 ? " (실행하지 않음)" : string.Empty)}";

        // Story lines of one turn: the spin, then each rune line followed by its own combat events (matched by SourceId,
        // not by event position), then the enemy action and the outcome.
        public static IReadOnlyList<string> Turn(TemporarySlotTurnRecord record, string enemyName)
        {
            var lines = new List<string> { Spin(record.Spin) };
            foreach (var line in record.Allocation.Lines)
            {
                lines.Add(Line(line));
                foreach (var combatEvent in record.Events)
                {
                    if (!string.Equals(combatEvent.SourceId, line.LineId, StringComparison.Ordinal)) continue;
                    var text = TemporaryCombatStoryText.PlayerEvent(combatEvent, enemyName);
                    if (text != null) lines.Add(text);
                }
            }
            foreach (var combatEvent in record.Events)
            {
                var text = TemporaryCombatStoryText.OutcomeEvent(combatEvent, enemyName);
                if (text != null) lines.Add(text);
            }
            return lines;
        }
    }
}
