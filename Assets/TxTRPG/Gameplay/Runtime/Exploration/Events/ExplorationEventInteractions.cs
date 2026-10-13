using System;
using System.Collections.Generic;
using System.Linq;

namespace TxTRPG.Gameplay.Exploration.Events
{
    // Effects applied in order, then the next page ID or ExplorationEventTargets.End.
    public sealed class ExplorationEventBranch
    {
        private readonly IExplorationEventEffect[] effects;

        public ExplorationEventBranch(IEnumerable<IExplorationEventEffect> branchEffects, string next)
        {
            effects = branchEffects?.ToArray() ?? Array.Empty<IExplorationEventEffect>();
            Next = next?.Trim() ?? string.Empty;
        }

        public IReadOnlyList<IExplorationEventEffect> Effects => Array.AsReadOnly(effects);
        public string Next { get; }
    }

    public enum ExplorationEventInputKind
    {
        Continue = 1,
        Choose = 2,
        BeginSlotCheck = 3,
        SlotResult = 4
    }

    public readonly struct ExplorationEventInput
    {
        private ExplorationEventInput(ExplorationEventInputKind kind, int optionIndex, int slotTotal)
        {
            Kind = kind; OptionIndex = optionIndex; SlotTotal = slotTotal;
        }

        public ExplorationEventInputKind Kind { get; }
        public int OptionIndex { get; }
        public int SlotTotal { get; }

        public static ExplorationEventInput Continue() => new(ExplorationEventInputKind.Continue, 0, 0);
        public static ExplorationEventInput Choose(int optionIndex) => new(ExplorationEventInputKind.Choose, optionIndex, 0);
        public static ExplorationEventInput BeginSlotCheck() => new(ExplorationEventInputKind.BeginSlotCheck, 0, 0);
        public static ExplorationEventInput SlotResult(int finalTotal) => new(ExplorationEventInputKind.SlotResult, 0, finalTotal);
    }

    // One button of the current page: the input it sends, its label, and whether it can be used now (with the reason).
    public sealed class ExplorationEventOption
    {
        public ExplorationEventOption(ExplorationEventInputKind input, int optionIndex, string label, bool isEnabled, string disabledReason)
        {
            Input = input; OptionIndex = optionIndex; Label = label ?? string.Empty; IsEnabled = isEnabled; DisabledReason = disabledReason ?? string.Empty;
        }

        public ExplorationEventInputKind Input { get; }
        public int OptionIndex { get; }
        public string Label { get; }
        public bool IsEnabled { get; }
        public string DisabledReason { get; }
    }

    // What an accepted input does: either it starts a slot check (no effects yet), or it takes a branch.
    public sealed class ExplorationEventResolution
    {
        private ExplorationEventResolution(ExplorationEventBranch branch, bool startsSlotCheck, string description)
        {
            Branch = branch; StartsSlotCheck = startsSlotCheck; Description = description ?? string.Empty;
        }

        public ExplorationEventBranch Branch { get; }
        public bool StartsSlotCheck { get; }
        // The chosen option label or the slot range that matched, for the Story.
        public string Description { get; }

        public static ExplorationEventResolution Take(ExplorationEventBranch branch, string description) =>
            new(branch ?? throw new ArgumentNullException(nameof(branch)), false, description);
        public static ExplorationEventResolution StartSlotCheck(string description) => new(null, true, description);
    }

    // The player interaction of a page. A new interaction kind (for example a random outcome or a combat) implements
    // this; the execution only sees inputs, branches and slot-check starts.
    public interface IExplorationEventInteraction
    {
        IEnumerable<ExplorationEventBranch> Branches { get; }
        IEnumerable<IExplorationEventCondition> Conditions { get; }
        bool Validate(out string reason);
        // Non-blocking authoring problems (overlapping slot ranges, no option without conditions).
        IEnumerable<string> CollectWarnings();
        IReadOnlyList<ExplorationEventOption> DescribeOptions(IExplorationEventContext context);
        bool TryResolve(ExplorationEventInput input, IExplorationEventContext context, out ExplorationEventResolution resolution, out string reason);
    }

    public sealed class ExplorationEventContinue : IExplorationEventInteraction
    {
        public ExplorationEventContinue(string label, ExplorationEventBranch branch)
        {
            Label = label ?? string.Empty;
            Branch = branch ?? throw new ArgumentNullException(nameof(branch));
        }

        public string Label { get; }
        public ExplorationEventBranch Branch { get; }
        public IEnumerable<ExplorationEventBranch> Branches { get { yield return Branch; } }
        public IEnumerable<IExplorationEventCondition> Conditions => Array.Empty<IExplorationEventCondition>();

        public bool Validate(out string reason) { reason = string.Empty; return true; }
        public IEnumerable<string> CollectWarnings() => Array.Empty<string>();

        public IReadOnlyList<ExplorationEventOption> DescribeOptions(IExplorationEventContext context) =>
            new[] { new ExplorationEventOption(ExplorationEventInputKind.Continue, 0, Label, true, string.Empty) };

        public bool TryResolve(ExplorationEventInput input, IExplorationEventContext context, out ExplorationEventResolution resolution, out string reason)
        {
            resolution = null;
            if (input.Kind != ExplorationEventInputKind.Continue) { reason = "This page only accepts Continue."; return false; }
            resolution = ExplorationEventResolution.Take(Branch, Label);
            reason = string.Empty;
            return true;
        }
    }

    public sealed class ExplorationEventChoiceOption
    {
        private readonly IExplorationEventCondition[] conditions;

        public ExplorationEventChoiceOption(string label, IEnumerable<IExplorationEventCondition> optionConditions, ExplorationEventBranch branch)
        {
            Label = label ?? string.Empty;
            conditions = optionConditions?.ToArray() ?? Array.Empty<IExplorationEventCondition>();
            Branch = branch ?? throw new ArgumentNullException(nameof(branch));
        }

        public string Label { get; }
        public IReadOnlyList<IExplorationEventCondition> Conditions => Array.AsReadOnly(conditions);
        public ExplorationEventBranch Branch { get; }

        // The first unmet condition's reason; a condition that throws counts as unmet.
        public bool IsAvailable(IExplorationEventContext context, out string reason)
        {
            foreach (var condition in conditions)
            {
                bool satisfied;
                reason = string.Empty;
                try { satisfied = condition != null && condition.IsSatisfied(context, out reason); }
                catch (Exception exception) { reason = exception.Message; satisfied = false; }
                if (!satisfied) { reason ??= string.Empty; return false; }
            }
            reason = string.Empty;
            return true;
        }
    }

    public sealed class ExplorationEventChoice : IExplorationEventInteraction
    {
        private readonly ExplorationEventChoiceOption[] options;

        public ExplorationEventChoice(IEnumerable<ExplorationEventChoiceOption> choiceOptions)
        {
            options = choiceOptions?.ToArray() ?? Array.Empty<ExplorationEventChoiceOption>();
        }

        public IReadOnlyList<ExplorationEventChoiceOption> Options => Array.AsReadOnly(options);
        public IEnumerable<ExplorationEventBranch> Branches => options.Where(item => item != null).Select(item => item.Branch);
        public IEnumerable<IExplorationEventCondition> Conditions => options.Where(item => item != null).SelectMany(item => item.Conditions);

        public bool Validate(out string reason)
        {
            if (options.Length == 0) { reason = "A choice requires at least one option."; return false; }
            for (var i = 0; i < options.Length; i++)
                if (options[i] == null) { reason = $"Option {i + 1} is empty."; return false; }
            reason = string.Empty;
            return true;
        }

        public IEnumerable<string> CollectWarnings()
        {
            if (options.Length > 0 && options.All(item => item == null || item.Conditions.Count > 0))
                yield return "Every option has a condition; the event cannot continue when none is met.";
        }

        public IReadOnlyList<ExplorationEventOption> DescribeOptions(IExplorationEventContext context)
        {
            var result = new ExplorationEventOption[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                var available = options[i].IsAvailable(context, out var reason);
                result[i] = new ExplorationEventOption(ExplorationEventInputKind.Choose, i, options[i].Label, available, reason);
            }
            return result;
        }

        public bool TryResolve(ExplorationEventInput input, IExplorationEventContext context, out ExplorationEventResolution resolution, out string reason)
        {
            resolution = null;
            if (input.Kind != ExplorationEventInputKind.Choose) { reason = "This page only accepts a choice."; return false; }
            if (input.OptionIndex < 0 || input.OptionIndex >= options.Length) { reason = $"Option {input.OptionIndex} does not exist."; return false; }
            var option = options[input.OptionIndex];
            if (!option.IsAvailable(context, out reason)) return false;
            resolution = ExplorationEventResolution.Take(option.Branch, option.Label);
            return true;
        }
    }

    // A slot result range; both bounds are optional and inclusive.
    public sealed class ExplorationEventSlotOutcome
    {
        public ExplorationEventSlotOutcome(int? minimum, int? maximum, ExplorationEventBranch branch)
        {
            Minimum = minimum; Maximum = maximum;
            Branch = branch ?? throw new ArgumentNullException(nameof(branch));
        }

        public int? Minimum { get; }
        public int? Maximum { get; }
        public ExplorationEventBranch Branch { get; }
        public bool Contains(int total) => (!Minimum.HasValue || total >= Minimum.Value) && (!Maximum.HasValue || total <= Maximum.Value);
        public bool Overlaps(ExplorationEventSlotOutcome other) =>
            (!Minimum.HasValue || !other.Maximum.HasValue || Minimum.Value <= other.Maximum.Value) &&
            (!other.Minimum.HasValue || !Maximum.HasValue || other.Minimum.Value <= Maximum.Value);
    }

    // Spins the session slot machine (the caller does the spin) and branches on the final total: the first matching range
    // from the top, otherwise the fallback.
    public sealed class ExplorationEventSlotCheck : IExplorationEventInteraction
    {
        private readonly ExplorationEventSlotOutcome[] outcomes;

        public ExplorationEventSlotCheck(string label, bool useFortuneCritical, IEnumerable<ExplorationEventSlotOutcome> slotOutcomes, ExplorationEventBranch fallback)
        {
            Label = label ?? string.Empty;
            UseFortuneCritical = useFortuneCritical;
            outcomes = slotOutcomes?.ToArray() ?? Array.Empty<ExplorationEventSlotOutcome>();
            Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        public string Label { get; }
        public bool UseFortuneCritical { get; }
        public IReadOnlyList<ExplorationEventSlotOutcome> Outcomes => Array.AsReadOnly(outcomes);
        public ExplorationEventBranch Fallback { get; }
        public IEnumerable<ExplorationEventBranch> Branches => outcomes.Where(item => item != null).Select(item => item.Branch).Append(Fallback);
        public IEnumerable<IExplorationEventCondition> Conditions => Array.Empty<IExplorationEventCondition>();

        public bool Validate(out string reason)
        {
            for (var i = 0; i < outcomes.Length; i++)
            {
                var outcome = outcomes[i];
                if (outcome == null) { reason = $"Slot range {i + 1} is empty."; return false; }
                if (outcome.Minimum.HasValue && outcome.Maximum.HasValue && outcome.Minimum.Value > outcome.Maximum.Value)
                { reason = $"Slot range {i + 1} has min {outcome.Minimum.Value} greater than max {outcome.Maximum.Value}."; return false; }
            }
            reason = string.Empty;
            return true;
        }

        public IEnumerable<string> CollectWarnings()
        {
            for (var i = 0; i < outcomes.Length; i++)
                for (var j = i + 1; j < outcomes.Length; j++)
                    if (outcomes[i] != null && outcomes[j] != null && outcomes[i].Overlaps(outcomes[j]))
                        yield return $"Slot ranges {i + 1} and {j + 1} overlap; the earlier range wins.";
        }

        public IReadOnlyList<ExplorationEventOption> DescribeOptions(IExplorationEventContext context) =>
            new[] { new ExplorationEventOption(ExplorationEventInputKind.BeginSlotCheck, 0, Label, true, string.Empty) };

        public int FindOutcomeIndex(int total)
        {
            for (var i = 0; i < outcomes.Length; i++) if (outcomes[i].Contains(total)) return i;
            return -1;
        }

        public bool TryResolve(ExplorationEventInput input, IExplorationEventContext context, out ExplorationEventResolution resolution, out string reason)
        {
            resolution = null;
            reason = string.Empty;
            switch (input.Kind)
            {
                case ExplorationEventInputKind.BeginSlotCheck:
                    resolution = ExplorationEventResolution.StartSlotCheck(Label);
                    return true;
                case ExplorationEventInputKind.SlotResult:
                    var index = FindOutcomeIndex(input.SlotTotal);
                    resolution = ExplorationEventResolution.Take(index < 0 ? Fallback : outcomes[index].Branch, input.SlotTotal.ToString());
                    return true;
                default:
                    reason = "This page only accepts a slot check.";
                    return false;
            }
        }
    }
}
