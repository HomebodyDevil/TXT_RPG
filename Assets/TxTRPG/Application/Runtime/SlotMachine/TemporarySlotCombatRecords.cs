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
            : this(actionId, runId, nodeId, combat, spin, FortuneCriticalResult.None(spin ?? throw new ArgumentNullException(nameof(spin))), allocation)
        {
        }

        public TemporarySlotAcceptedAction(long actionId, string runId, string nodeId, TemporaryCombatState combat,
            SlotSpinResult spin, FortuneCriticalResult fortune, RuneAllocation allocation)
        {
            ActionId = actionId;
            RunId = runId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            Spin = spin ?? throw new ArgumentNullException(nameof(spin));
            Fortune = fortune ?? throw new ArgumentNullException(nameof(fortune));
            if (!ReferenceEquals(fortune.Spin, spin)) throw new ArgumentException("The Fortune Critical result belongs to another spin.", nameof(fortune));
            Allocation = allocation ?? throw new ArgumentNullException(nameof(allocation));
        }

        public long ActionId { get; }
        public string RunId { get; }
        public string NodeId { get; }
        public TemporaryCombatState Combat { get; }
        public SlotSpinResult Spin { get; }
        // Fortune Critical multipliers decided with the spin; Allocation distributes its FinalTotal.
        public FortuneCriticalResult Fortune { get; }
        public RuneAllocation Allocation { get; }
        public bool Applied { get; private set; }

        internal void MarkApplied() => Applied = true;
    }

    public sealed class TemporarySlotTurnRecord
    {
        public TemporarySlotTurnRecord(string runId, string nodeId, SlotSpinResult spin, RuneAllocation allocation,
            TemporaryCombatTurnResult turn)
            : this(runId, nodeId, spin, FortuneCriticalResult.None(spin ?? throw new ArgumentNullException(nameof(spin))), allocation, turn)
        {
        }

        public TemporarySlotTurnRecord(string runId, string nodeId, SlotSpinResult spin, FortuneCriticalResult fortune,
            RuneAllocation allocation, TemporaryCombatTurnResult turn)
        {
            RunId = runId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            Spin = spin ?? throw new ArgumentNullException(nameof(spin));
            Fortune = fortune ?? throw new ArgumentNullException(nameof(fortune));
            if (!ReferenceEquals(fortune.Spin, spin)) throw new ArgumentException("The Fortune Critical result belongs to another spin.", nameof(fortune));
            Allocation = allocation ?? throw new ArgumentNullException(nameof(allocation));
            if (turn == null) throw new ArgumentNullException(nameof(turn));
            var events = new TemporaryCombatEvent[turn.Events.Count];
            for (var i = 0; i < events.Length; i++) events[i] = turn.Events[i];
            Events = Array.AsReadOnly(events);
        }

        public string RunId { get; }
        public string NodeId { get; }
        public SlotSpinResult Spin { get; }
        public FortuneCriticalResult Fortune { get; }
        public RuneAllocation Allocation { get; }
        public IReadOnlyList<TemporaryCombatEvent> Events { get; }
    }

    // The one place where rune effects become combat actions. Unsupported effects are rejected, never guessed. Test runes
    // receive no allocation, so they never reach this point; they are rejected like any effect without an action.
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
        // Slot machine panel summary texts. The total shows only its value, so the label is empty and the views hide
        // their TotalLabel; Story and result texts spell "합계" themselves.
        public const string TotalLabel = "";
        public const string IdleTotalValue = "0";
        public const string RuneColumnTitle = "룬";
        public const string ValueColumnTitle = "쌓임 → 분배";
        public const string NoRuneLines = "장착된 룬이 없습니다";
        public const string OverflowFormat = "외 {0}줄";
        // System name in Story lines.
        public const string FortuneCriticalName = "행운 치명";

        // The total box value: 0 when a spin starts, then the running total and the final total.
        public static string TotalValue(int total) => total.ToString();

        // Fortune Critical multiplier next to a reel's payline cell or the total ("×3"); empty when it did not happen.
        public static string MultiplierText(int multiplier) => multiplier > 1 ? $"×{multiplier}" : string.Empty;

        // Rune line row value before a spin ("×2") and after it ("×2 → 6").
        public static string LineWeight(int weight) => $"×{weight}";
        public static string LineAllocation(int weight, int amount) => $"×{weight} → {amount}";

        public static string EffectName(RuneEffectKind effect) => effect switch
        {
            RuneEffectKind.Attack => "공격",
            RuneEffectKind.Heal => "회복",
            RuneEffectKind.Test => "테스트",
            _ => "알 수 없는 효과"
        };

        // "[슬롯] 3 + 5 + 2 = 10"
        public static string Spin(SlotSpinResult spin) => Spin(FortuneCriticalResult.None(spin));

        // "[슬롯] 3 + 5×3 + 2 = 20", "[슬롯] (3 + 5 + 2) ×2 = 20" or "[슬롯] (3 + 5×3 + 2) ×2 = 40". The last value is the
        // final total, after the cap.
        public static string Spin(FortuneCriticalResult fortune)
        {
            var builder = new StringBuilder("[슬롯] ");
            if (fortune.IsTotalCritical) builder.Append('(');
            for (var i = 0; i < fortune.Reels.Count; i++)
            {
                if (i > 0) builder.Append(" + ");
                var reel = fortune.Reels[i];
                builder.Append(reel.BaseValue);
                if (reel.IsCritical) builder.Append('×').Append(reel.ReelMultiplier);
            }
            if (fortune.IsTotalCritical) builder.Append(") ×").Append(fortune.TotalMultiplier);
            return builder.Append(" = ").Append(fortune.FinalTotal).ToString();
        }

        // "[행운 치명] 2번 Reel ×3, 합계 ×2", or null when nothing happened. Reels are numbered in result order from 1.
        public static string FortuneSummary(FortuneCriticalResult fortune)
        {
            if (!fortune.HasAnyCritical) return null;
            var parts = new List<string>();
            for (var i = 0; i < fortune.Reels.Count; i++)
                if (fortune.Reels[i].IsCritical) parts.Add($"{i + 1}번 Reel ×{fortune.Reels[i].ReelMultiplier}");
            if (fortune.IsTotalCritical) parts.Add($"합계 ×{fortune.TotalMultiplier}");
            return $"[{FortuneCriticalName}] {string.Join(", ", parts)}";
        }

        // "[행운 치명] 최대 배수 ×30 적용: 2번 Reel (×50 → ×30)", or null when no reel was capped.
        public static string FortuneCap(FortuneCriticalResult fortune)
        {
            if (!fortune.IsAnyCapped) return null;
            var parts = new List<string>();
            for (var i = 0; i < fortune.Reels.Count; i++)
            {
                var reel = fortune.Reels[i];
                if (reel.IsCapped) parts.Add($"{i + 1}번 Reel (×{reel.UncappedMultiplier} → ×{reel.EffectiveMultiplier})");
            }
            return $"[{FortuneCriticalName}] 최대 배수 ×{fortune.MaximumEffectiveMultiplier} 적용: {string.Join(", ", parts)}";
        }

        // "[룬] 1번 줄 공격 룬 ×1: 5", or "... : 0 (실행하지 않음)" for a line that received nothing.
        public static string Line(RuneAllocationLine line) =>
            $"[룬] {line.Order}번 줄 {line.DisplayName} ×{line.Weight}: {line.Amount}{(line.Amount == 0 ? " (실행하지 않음)" : string.Empty)}";

        // Story lines of one turn: the spin and its Fortune Critical lines, then each rune line followed by its own combat events (matched by SourceId,
        // not by event position), then the enemy action and the outcome.
        public static IReadOnlyList<string> Turn(TemporarySlotTurnRecord record, string enemyName)
        {
            var lines = new List<string> { Spin(record.Fortune) };
            var summary = FortuneSummary(record.Fortune);
            if (summary != null) lines.Add(summary);
            var cap = FortuneCap(record.Fortune);
            if (cap != null) lines.Add(cap);
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
