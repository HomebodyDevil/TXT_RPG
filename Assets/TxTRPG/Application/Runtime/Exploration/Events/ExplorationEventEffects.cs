using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.Gameplay.Exploration.Events;
using TxTRPG.Gameplay.Runes;
using TxTRPG.Gameplay.SlotMachine;
using UnityEngine;
using SlotMachineModel = TxTRPG.Gameplay.SlotMachine.SlotMachine;

// Built-in exploration event effects. A new effect is one [Serializable] class with a parameterless constructor and
// serialized fields only that implements IExplorationEventEffect; it then appears in the Inspector dropdown. Effects are
// shared authoring data, so Apply keeps no run state in fields. When a class is renamed or moved, add
// [UnityEngine.Scripting.APIUpdating.MovedFrom] so existing assets keep their data.
namespace TxTRPG.Application.Exploration.Events
{
    public enum ExplorationReelSelectorMode
    {
        ReelId = 0,
        First = 1,
        Last = 2,
        // 1-based position.
        Position = 3
    }

    [Serializable]
    public sealed class ExplorationReelSelector
    {
        [SerializeField] private ExplorationReelSelectorMode mode = ExplorationReelSelectorMode.First;
        [SerializeField] private string reelId = string.Empty;
        [SerializeField, Min(1)] private int position = 1;

        public ExplorationReelSelector() { }
        public ExplorationReelSelector(ExplorationReelSelectorMode selectorMode, string id = "", int oneBasedPosition = 1)
        { mode = selectorMode; reelId = id; position = oneBasedPosition; }

        public bool Validate(out string reason)
        {
            if (mode == ExplorationReelSelectorMode.ReelId && string.IsNullOrWhiteSpace(reelId)) { reason = "A reel ID is required."; return false; }
            if (mode == ExplorationReelSelectorMode.Position && position < 1) { reason = "The reel position starts at 1."; return false; }
            reason = string.Empty;
            return true;
        }

        public bool TryResolve(SlotMachineModel machine, out SlotReel reel, out int index)
        {
            reel = null; index = -1;
            if (machine == null || machine.ReelCount == 0) return false;
            switch (mode)
            {
                case ExplorationReelSelectorMode.First: index = 0; break;
                case ExplorationReelSelectorMode.Last: index = machine.ReelCount - 1; break;
                case ExplorationReelSelectorMode.Position: index = position - 1; break;
                default:
                    for (var i = 0; i < machine.ReelCount; i++)
                        if (string.Equals(machine.Reels[i].Id, reelId?.Trim(), StringComparison.Ordinal)) { index = i; break; }
                    break;
            }
            if (index < 0 || index >= machine.ReelCount) { index = -1; return false; }
            reel = machine.Reels[index];
            return true;
        }

        public override string ToString() => mode switch
        {
            ExplorationReelSelectorMode.First => "첫 Reel",
            ExplorationReelSelectorMode.Last => "마지막 Reel",
            ExplorationReelSelectorMode.Position => $"{position}번 Reel",
            _ => $"Reel '{reelId}'"
        };
    }

    internal static class ExplorationEventEffectSupport
    {
        public const string NoHealth = "탐험 체력을 사용할 수 없습니다.";
        public const string NoSlotMachine = "슬롯머신을 사용할 수 없습니다.";
        public const string NoRunes = "룬을 사용할 수 없습니다.";
        public const string NoFortune = "행운 치명 규칙을 사용할 수 없습니다.";

        public static bool Fail(out string reason, string message) { reason = message; return false; }
        public static bool Ok(out string reason) { reason = string.Empty; return true; }
    }

    // ---------- Health ----------

    [Serializable]
    public sealed class ChangeHealthEffect : IExplorationEventEffect
    {
        [SerializeField, Tooltip("Positive heals, negative damages. Not 0.")] private int amount;

        public ChangeHealthEffect() { }
        public ChangeHealthEffect(int change) => amount = change;

        public bool Validate(out string reason) =>
            amount != 0 ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "The health change cannot be 0.");

        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetHealth(out var health)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoHealth);
            var change = amount > 0 ? health.Heal(amount) : health.ApplyDamage(amount == int.MinValue ? int.MaxValue : -amount);
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.HealthChanged(amount > 0 ? change.AppliedAmount : -change.AppliedAmount, health.Current, health.Maximum));
        }
    }

    [Serializable]
    public sealed class RestoreHealthEffect : IExplorationEventEffect
    {
        public bool Validate(out string reason) => ExplorationEventEffectSupport.Ok(out reason);

        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetHealth(out var health)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoHealth);
            health.RestoreToFull();
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.HealthRestored(health.Current, health.Maximum));
        }
    }

    [Serializable]
    public sealed class ChangeMaximumHealthEffect : IExplorationEventEffect
    {
        [SerializeField, Tooltip("Not 0. The new maximum is at least 1.")] private int amount;
        [SerializeField, Tooltip("On an increase, heal by the same amount.")] private bool alsoChangeCurrent = true;

        public ChangeMaximumHealthEffect() { }
        public ChangeMaximumHealthEffect(int change, bool changeCurrent = true) { amount = change; alsoChangeCurrent = changeCurrent; }

        public bool Validate(out string reason) =>
            amount != 0 ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "The maximum health change cannot be 0.");

        // A decrease clips the current value to the new maximum and never reaches 0 (the maximum stays at least 1).
        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetHealth(out var health)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoHealth);
            var previous = health.Maximum;
            var target = (int)Math.Clamp((long)previous + amount, 1L, int.MaxValue);
            health.SetMaximum(target);
            if (target > previous && alsoChangeCurrent) health.Heal(target - previous);
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.MaximumHealthChanged(target - previous, health.Current, health.Maximum));
        }
    }

    // ---------- Slot machine reels ----------

    [Serializable]
    public sealed class AddReelSymbolEffect : IExplorationEventEffect
    {
        [SerializeField] private ExplorationReelSelector reel = new();
        [SerializeField, Min(0)] private int value;
        [SerializeField, Min(1)] private int count = 1;

        public AddReelSymbolEffect() { }
        public AddReelSymbolEffect(ExplorationReelSelector target, int symbolValue, int symbolCount = 1) { reel = target; value = symbolValue; count = symbolCount; }

        public bool Validate(out string reason)
        {
            if (reel == null) return ExplorationEventEffectSupport.Fail(out reason, "A reel is required.");
            if (!reel.Validate(out reason)) return false;
            if (value < 0) return ExplorationEventEffectSupport.Fail(out reason, "Slot values cannot be negative.");
            return count >= 1 ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "The count must be at least 1.");
        }

        // All cells are added to the end of the strip in one validated change.
        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetSlotMachine(out var machine)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoSlotMachine);
            if (!reel.TryResolve(machine, out var target, out var index)) return ExplorationEventEffectResult.Skipped(ExplorationEventStoryFormatter.ReelNotFound(reel.ToString()));
            var values = target.Strip.Select(symbol => symbol.Value).Concat(Enumerable.Repeat(value, count)).ToArray();
            if (!machine.TryReplaceStrip(target.Id, values, out var reason)) return ExplorationEventEffectResult.Skipped(reason);
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.ReelSymbolsAdded(index, target.Id, value, count));
        }
    }

    [Serializable]
    public sealed class RemoveReelSymbolEffect : IExplorationEventEffect
    {
        [SerializeField] private ExplorationReelSelector reel = new();
        [SerializeField, Min(0)] private int value;
        [SerializeField, Min(1)] private int count = 1;

        public RemoveReelSymbolEffect() { }
        public RemoveReelSymbolEffect(ExplorationReelSelector target, int symbolValue, int symbolCount = 1) { reel = target; value = symbolValue; count = symbolCount; }

        public bool Validate(out string reason)
        {
            if (reel == null) return ExplorationEventEffectSupport.Fail(out reason, "A reel is required.");
            if (!reel.Validate(out reason)) return false;
            return count >= 1 ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "The count must be at least 1.");
        }

        // Removes matching cells from the front. The last cell of a reel is never removed; when nothing can be removed the
        // effect is skipped.
        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetSlotMachine(out var machine)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoSlotMachine);
            if (!reel.TryResolve(machine, out var target, out var index)) return ExplorationEventEffectResult.Skipped(ExplorationEventStoryFormatter.ReelNotFound(reel.ToString()));
            var values = target.Strip.Select(symbol => symbol.Value).ToList();
            var removed = 0;
            for (var i = 0; i < values.Count && removed < count && values.Count > 1;)
            {
                if (values[i] == value) { values.RemoveAt(i); removed++; }
                else i++;
            }
            if (removed == 0) return ExplorationEventEffectResult.Skipped(ExplorationEventStoryFormatter.ReelSymbolNotRemovable(index, target.Id, value));
            if (!machine.TryReplaceStrip(target.Id, values, out var reason)) return ExplorationEventEffectResult.Skipped(reason);
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.ReelSymbolsRemoved(index, target.Id, value, removed, count));
        }
    }

    [Serializable]
    public sealed class AddReelEffect : IExplorationEventEffect
    {
        [SerializeField, Tooltip("When the ID exists, the first free suffix (.2, .3, ...) is used.")] private string reelId = "reel.event";
        [SerializeField] private int[] values = { 1, 2, 3, 4, 5, 6 };

        public AddReelEffect() { }
        public AddReelEffect(string id, params int[] reelValues) { reelId = id; values = reelValues ?? Array.Empty<int>(); }

        public bool Validate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(reelId)) return ExplorationEventEffectSupport.Fail(out reason, "A reel ID is required.");
            if (values == null || values.Length == 0) return ExplorationEventEffectSupport.Fail(out reason, "A reel requires at least one value.");
            return values.All(item => item >= 0) ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "Slot values cannot be negative.");
        }

        public static string FreeReelId(SlotMachineModel machine, string requested)
        {
            var id = requested.Trim();
            if (machine.FindReel(id) == null) return id;
            for (var suffix = 2; ; suffix++)
                if (machine.FindReel($"{id}.{suffix}") == null) return $"{id}.{suffix}";
        }

        // The new reel follows Fortune Critical's rule for its ID, or the default reel rule.
        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetSlotMachine(out var machine)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoSlotMachine);
            var id = FreeReelId(machine, reelId);
            if (!machine.TryAddReel(SlotReel.FromValues(id, values), out var reason)) return ExplorationEventEffectResult.Skipped(reason);
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.ReelAdded(id, values, machine.ReelCount));
        }
    }

    [Serializable]
    public sealed class RemoveReelEffect : IExplorationEventEffect
    {
        [SerializeField] private ExplorationReelSelector reel = new(ExplorationReelSelectorMode.Last);

        public RemoveReelEffect() { }
        public RemoveReelEffect(ExplorationReelSelector target) => reel = target;

        public bool Validate(out string reason) =>
            reel != null ? reel.Validate(out reason) : ExplorationEventEffectSupport.Fail(out reason, "A reel is required.");

        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetSlotMachine(out var machine)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoSlotMachine);
            if (!reel.TryResolve(machine, out var target, out var index)) return ExplorationEventEffectResult.Skipped(ExplorationEventStoryFormatter.ReelNotFound(reel.ToString()));
            if (machine.ReelCount <= 1) return ExplorationEventEffectResult.Skipped(ExplorationEventStoryFormatter.LastReelKept);
            if (!machine.TryRemoveReel(target.Id, out var reason)) return ExplorationEventEffectResult.Skipped(reason);
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.ReelRemoved(index, target.Id, machine.ReelCount));
        }
    }

    // ---------- Fortune Critical ----------

    public enum ExplorationFortuneTarget
    {
        DefaultReel = 0,
        Reel = 1,
        Total = 2
    }

    public enum ExplorationValueChangeMode
    {
        Set = 0,
        Add = 1
    }

    [Serializable]
    public sealed class ChangeFortuneChanceEffect : IExplorationEventEffect
    {
        [SerializeField] private ExplorationFortuneTarget target = ExplorationFortuneTarget.DefaultReel;
        [SerializeField, Tooltip("Used when the target is Reel.")] private string reelId = string.Empty;
        [SerializeField] private ExplorationValueChangeMode mode = ExplorationValueChangeMode.Add;
        [SerializeField, Tooltip("Chance (0-1). Add accepts negative values. The result is clamped to 0-1.")] private float value;

        public ChangeFortuneChanceEffect() { }
        public ChangeFortuneChanceEffect(ExplorationFortuneTarget fortuneTarget, ExplorationValueChangeMode changeMode, float chance, string targetReelId = "")
        { target = fortuneTarget; mode = changeMode; value = chance; reelId = targetReelId; }

        public bool Validate(out string reason)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return ExplorationEventEffectSupport.Fail(out reason, "The chance must be a number.");
            if (mode == ExplorationValueChangeMode.Set && (value < 0f || value > 1f)) return ExplorationEventEffectSupport.Fail(out reason, "A set chance must be between 0 and 1.");
            if (target == ExplorationFortuneTarget.Reel && string.IsNullOrWhiteSpace(reelId)) return ExplorationEventEffectSupport.Fail(out reason, "A reel ID is required.");
            return ExplorationEventEffectSupport.Ok(out reason);
        }

        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetFortuneCritical(out var store)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoFortune);
            var current = target switch
            {
                ExplorationFortuneTarget.Total => store.Rules.TotalRule.Chance,
                ExplorationFortuneTarget.Reel => store.Rules.RuleForReel(reelId).Chance,
                _ => store.Rules.DefaultReelRule.Chance
            };
            // Rounded to the drawn resolution so 0.05 + 0.02 does not become 0.07000000000000001.
            var next = Math.Round(Math.Clamp(mode == ExplorationValueChangeMode.Set ? value : current + value, 0d, 1d), 6);
            string reason;
            var ok = target switch
            {
                ExplorationFortuneTarget.Total => store.TrySetTotalChance(next, out reason),
                ExplorationFortuneTarget.Reel => store.TrySetReelChance(reelId, next, out reason),
                _ => store.TrySetDefaultReelChance(next, out reason)
            };
            if (!ok) return ExplorationEventEffectResult.Skipped(reason);
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.FortuneChanceChanged(TargetName, current, next));
        }

        private string TargetName => target switch
        {
            ExplorationFortuneTarget.Total => "합계",
            ExplorationFortuneTarget.Reel => $"Reel '{reelId}'",
            _ => "Reel 기본"
        };
    }

    [Serializable]
    public sealed class ChangeFortuneCapEffect : IExplorationEventEffect
    {
        [SerializeField] private ExplorationValueChangeMode mode = ExplorationValueChangeMode.Add;
        [SerializeField, Tooltip("Maximum effective multiplier. The result is clamped to the allowed range.")] private int value;

        public ChangeFortuneCapEffect() { }
        public ChangeFortuneCapEffect(ExplorationValueChangeMode changeMode, int cap) { mode = changeMode; value = cap; }

        public bool Validate(out string reason) =>
            mode == ExplorationValueChangeMode.Add && value == 0
                ? ExplorationEventEffectSupport.Fail(out reason, "Adding 0 changes nothing.")
                : ExplorationEventEffectSupport.Ok(out reason);

        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetFortuneCritical(out var store)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoFortune);
            var current = store.Rules.MaximumEffectiveMultiplier;
            var next = (int)Math.Clamp(mode == ExplorationValueChangeMode.Set ? value : (long)current + value, 1L, FortuneCriticalLimits.MaximumEffectiveMultiplier);
            if (!store.TrySetMaximumEffectiveMultiplier(next, out var reason)) return ExplorationEventEffectResult.Skipped(reason);
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.FortuneCapChanged(current, next));
        }
    }

    // ---------- Runes ----------

    public enum ExplorationRunePlacement
    {
        NewLine = 0,
        // On the given line, or on the first line of the same rune definition.
        StackOnLine = 1,
        Unequipped = 2
    }

    [Serializable]
    public sealed class GrantRuneEffect : IExplorationEventEffect
    {
        [SerializeField] private string definitionId = "rune.attack";
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField] private ExplorationRunePlacement placement = ExplorationRunePlacement.NewLine;
        [SerializeField, Tooltip("StackOnLine only. Empty uses the first line of the same rune definition.")] private string lineId = string.Empty;

        public GrantRuneEffect() { }
        public GrantRuneEffect(string runeDefinitionId, int runeCount = 1, ExplorationRunePlacement runePlacement = ExplorationRunePlacement.NewLine, string targetLineId = "")
        { definitionId = runeDefinitionId; count = runeCount; placement = runePlacement; lineId = targetLineId; }

        // The session catalog is decided at runtime, so an unknown definition ID is skipped in Apply, not rejected here.
        public bool Validate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) return ExplorationEventEffectSupport.Fail(out reason, "A rune definition ID is required.");
            return count >= 1 ? ExplorationEventEffectSupport.Ok(out reason) : ExplorationEventEffectSupport.Fail(out reason, "The count must be at least 1.");
        }

        // Adds the runes, then places them. When placing fails, the added runes are removed again.
        public ExplorationEventEffectResult Apply(IExplorationEventContext context)
        {
            if (!context.TryGetRunes(out var runes)) return ExplorationEventEffectResult.Skipped(ExplorationEventEffectSupport.NoRunes);
            var id = definitionId.Trim();
            if (!runes.Catalog.TryGet(id, out var definition)) return ExplorationEventEffectResult.Skipped(ExplorationEventStoryFormatter.UnknownRune(id));
            var added = new List<string>();
            string reason;
            for (var i = 0; i < count; i++)
            {
                var instanceId = FreeId(runes.Inventory.Contains, $"{id}.event");
                if (!runes.Inventory.TryAdd(new OwnedRune(instanceId, id), out reason)) return Rollback(runes, added, reason);
                added.Add(instanceId);
            }
            var placedLine = string.Empty;
            switch (placement)
            {
                case ExplorationRunePlacement.NewLine:
                    placedLine = FreeId(lineExists => runes.Loadout.Lines.Any(line => line.LineId == lineExists), "line.event");
                    if (!runes.Loadout.TryAddLine(new RuneLoadoutLine(placedLine, added), out reason)) return Rollback(runes, added, reason);
                    break;
                case ExplorationRunePlacement.StackOnLine:
                    placedLine = string.IsNullOrWhiteSpace(lineId)
                        ? runes.Loadout.Lines.FirstOrDefault(line => runes.Loadout.DefinitionIdOf(line) == id)?.LineId
                        : lineId.Trim();
                    if (string.IsNullOrEmpty(placedLine)) return Rollback(runes, added, $"No rune line holds '{id}'.");
                    var stacked = new List<string>();
                    foreach (var instanceId in added)
                    {
                        if (runes.Loadout.TryAddRuneToLine(placedLine, instanceId, out reason)) { stacked.Add(instanceId); continue; }
                        foreach (var undo in stacked) runes.Loadout.TryRemoveRuneFromLine(placedLine, undo, out _);
                        return Rollback(runes, added, reason);
                    }
                    break;
            }
            return ExplorationEventEffectResult.Applied(ExplorationEventStoryFormatter.RuneGranted(definition.DisplayName, count, placement, placedLine));
        }

        private static ExplorationEventEffectResult Rollback(ExplorationEventRuneAccess runes, List<string> added, string reason)
        {
            foreach (var instanceId in added) runes.Inventory.TryRemove(instanceId, out _);
            return ExplorationEventEffectResult.Skipped(reason);
        }

        private static string FreeId(Func<string, bool> exists, string prefix)
        {
            for (var n = 1; ; n++)
                if (!exists($"{prefix}.{n}")) return $"{prefix}.{n}";
        }
    }
}
