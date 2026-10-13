using System;
using System.Collections.Generic;
using System.Linq;

namespace TxTRPG.Gameplay.Exploration.Events
{
    public sealed class ExplorationEventPage
    {
        private readonly IExplorationEventEffect[] onEnter;

        public ExplorationEventPage(string pageId, string title, string body, IEnumerable<IExplorationEventEffect> onEnterEffects, IExplorationEventInteraction interaction)
        {
            PageId = pageId?.Trim() ?? string.Empty;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            onEnter = onEnterEffects?.ToArray() ?? Array.Empty<IExplorationEventEffect>();
            Interaction = interaction;
        }

        public string PageId { get; }
        public string Title { get; }
        public string Body { get; }
        public IReadOnlyList<IExplorationEventEffect> OnEnter => Array.AsReadOnly(onEnter);
        public IExplorationEventInteraction Interaction { get; }
    }

    // Immutable, validated event. Create it only through TryCreate.
    public sealed class ExplorationEvent
    {
        private readonly Dictionary<string, ExplorationEventPage> pages;

        private ExplorationEvent(string eventId, string title, string summary, string startPageId, Dictionary<string, ExplorationEventPage> eventPages)
        {
            EventId = eventId; Title = title; Summary = summary; StartPageId = startPageId; pages = eventPages;
        }

        public string EventId { get; }
        public string Title { get; }
        public string Summary { get; }
        public string StartPageId { get; }
        public IReadOnlyCollection<ExplorationEventPage> Pages => pages.Values;

        public ExplorationEventPage GetPage(string pageId) =>
            pages.TryGetValue(pageId?.Trim() ?? string.Empty, out var page) ? page : throw new KeyNotFoundException($"Page '{pageId}' does not exist.");

        public static bool TryCreate(string eventId, string title, string summary, string startPageId, IEnumerable<ExplorationEventPage> source,
            out ExplorationEvent result, out string reason)
        {
            result = null;
            eventId = eventId?.Trim() ?? string.Empty;
            if (eventId.Length == 0) { reason = "The event ID is empty."; return false; }
            if (source == null) { reason = "The event has no pages."; return false; }
            var map = new Dictionary<string, ExplorationEventPage>(StringComparer.Ordinal);
            var index = 0;
            foreach (var page in source)
            {
                index++;
                if (page == null) { reason = $"Page {index} is empty."; return false; }
                if (page.PageId.Length == 0) { reason = $"Page {index} has no page ID."; return false; }
                if (ExplorationEventTargets.IsEnd(page.PageId)) { reason = $"Page ID '{page.PageId}' is reserved."; return false; }
                if (!map.TryAdd(page.PageId, page)) { reason = $"Page ID '{page.PageId}' is duplicated."; return false; }
            }
            if (map.Count == 0) { reason = "The event has no pages."; return false; }
            var start = startPageId?.Trim() ?? string.Empty;
            if (!map.ContainsKey(start)) { reason = $"Start page '{start}' does not exist."; return false; }
            foreach (var page in map.Values)
            {
                var where = $"Page '{page.PageId}'";
                if (!ValidateEffects(page.OnEnter, $"{where} onEnter", out reason)) return false;
                if (page.Interaction == null) { reason = $"{where} has no interaction."; return false; }
                if (!page.Interaction.Validate(out reason)) { reason = $"{where}: {reason}"; return false; }
                foreach (var branch in page.Interaction.Branches)
                {
                    if (branch == null) { reason = $"{where} has an empty branch."; return false; }
                    if (!ExplorationEventTargets.IsEnd(branch.Next) && !map.ContainsKey(branch.Next))
                    { reason = $"{where} goes to '{branch.Next}', which is neither a page nor {ExplorationEventTargets.End}."; return false; }
                    if (!ValidateEffects(branch.Effects, where, out reason)) return false;
                }
                foreach (var condition in page.Interaction.Conditions)
                {
                    if (condition == null) { reason = $"{where} has an empty condition."; return false; }
                    if (!condition.Validate(out var conditionReason)) { reason = $"{where} condition {condition.GetType().Name}: {conditionReason}"; return false; }
                }
            }
            result = new ExplorationEvent(eventId, title ?? string.Empty, summary ?? string.Empty, start, map);
            reason = string.Empty;
            return true;
        }

        public IEnumerable<string> CollectWarnings()
        {
            foreach (var page in pages.Values)
                foreach (var warning in page.Interaction.CollectWarnings())
                    yield return $"Page '{page.PageId}': {warning}";
        }

        private static bool ValidateEffects(IReadOnlyList<IExplorationEventEffect> effects, string where, out string reason)
        {
            for (var i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null) { reason = $"{where} effect {i + 1} is empty."; return false; }
                if (!effects[i].Validate(out var effectReason)) { reason = $"{where} effect {i + 1} ({effects[i].GetType().Name}): {effectReason}"; return false; }
            }
            reason = string.Empty;
            return true;
        }
    }

    public enum ExplorationEventExecutionState
    {
        NotStarted = 0,
        AwaitingInput = 1,
        AwaitingSlotResult = 2,
        Completed = 3,
        Defeated = 4
    }

    public enum ExplorationEventStepEntryKind
    {
        // The accepted input (option label, slot total).
        Input = 1,
        EffectApplied = 2,
        EffectSkipped = 3,
        // The effect threw; earlier effects stay applied and the event continues.
        EffectFailed = 4,
        PageVisitLimit = 5,
        Defeated = 6
    }

    public readonly struct ExplorationEventStepEntry
    {
        public ExplorationEventStepEntry(ExplorationEventStepEntryKind kind, string text)
        {
            Kind = kind; Text = text ?? string.Empty;
        }

        public ExplorationEventStepEntryKind Kind { get; }
        public string Text { get; }
    }

    public sealed class ExplorationEventStepResult
    {
        public ExplorationEventStepResult(IReadOnlyList<ExplorationEventStepEntry> entries, ExplorationEventExecutionState state, string pageId)
        {
            Entries = entries ?? Array.Empty<ExplorationEventStepEntry>();
            State = state;
            PageId = pageId ?? string.Empty;
        }

        public IReadOnlyList<ExplorationEventStepEntry> Entries { get; }
        public ExplorationEventExecutionState State { get; }
        public string PageId { get; }
    }

    // Progress of one event run. One accepted input is one step: its branch effects, the next page and that page's onEnter
    // effects. An input that does not fit the state, a stale step number or an unmet condition is rejected without any
    // change, so repeating the same input never applies its effects twice. Effects run in order; once health is 0 the
    // remaining effects are not applied and the state becomes Defeated.
    public sealed class ExplorationEventExecution
    {
        private readonly Dictionary<string, int> visits = new(StringComparer.Ordinal);

        public ExplorationEventExecution(ExplorationEvent explorationEvent)
        {
            Event = explorationEvent ?? throw new ArgumentNullException(nameof(explorationEvent));
        }

        public ExplorationEvent Event { get; }
        public ExplorationEventExecutionState State { get; private set; } = ExplorationEventExecutionState.NotStarted;
        public string CurrentPageId { get; private set; } = string.Empty;
        public ExplorationEventPage CurrentPage => CurrentPageId.Length == 0 ? null : Event.GetPage(CurrentPageId);
        public int AppliedStepCount { get; private set; }
        public int TotalPageVisits { get; private set; }
        public bool IsFinished => State == ExplorationEventExecutionState.Completed || State == ExplorationEventExecutionState.Defeated;
        // Set when the event was ended by the page visit limit.
        public string Error { get; private set; } = string.Empty;

        public int VisitsOf(string pageId) => visits.TryGetValue(pageId ?? string.Empty, out var count) ? count : 0;

        public ExplorationEventStepResult Start(IExplorationEventContext context)
        {
            if (State != ExplorationEventExecutionState.NotStarted) throw new InvalidOperationException("The event has already started.");
            var entries = new List<ExplorationEventStepEntry>();
            Enter(Event.StartPageId, context, entries);
            return Commit(entries);
        }

        public IReadOnlyList<ExplorationEventOption> DescribeOptions(IExplorationEventContext context) =>
            State == ExplorationEventExecutionState.AwaitingInput && CurrentPage != null
                ? CurrentPage.Interaction.DescribeOptions(context)
                : Array.Empty<ExplorationEventOption>();

        // expectedStep is the AppliedStepCount the input was made for; -1 skips the check.
        public bool TryContinue(IExplorationEventContext context, int expectedStep, out ExplorationEventStepResult step, out string reason) =>
            TryInput(ExplorationEventInput.Continue(), context, expectedStep, ExplorationEventExecutionState.AwaitingInput, out step, out reason);

        public bool TryChoose(IExplorationEventContext context, int optionIndex, int expectedStep, out ExplorationEventStepResult step, out string reason) =>
            TryInput(ExplorationEventInput.Choose(optionIndex), context, expectedStep, ExplorationEventExecutionState.AwaitingInput, out step, out reason);

        // Accepting the slot check applies nothing; the caller spins once and reports the final total.
        public bool TryBeginSlotCheck(IExplorationEventContext context, int expectedStep, out ExplorationEventStepResult step, out string reason) =>
            TryInput(ExplorationEventInput.BeginSlotCheck(), context, expectedStep, ExplorationEventExecutionState.AwaitingInput, out step, out reason);

        public bool TryResolveSlotCheck(IExplorationEventContext context, int finalTotal, int expectedStep, out ExplorationEventStepResult step, out string reason) =>
            TryInput(ExplorationEventInput.SlotResult(finalTotal), context, expectedStep, ExplorationEventExecutionState.AwaitingSlotResult, out step, out reason);

        // Ends the event when no option of the current page can be used (every condition unmet).
        public bool TryEndWithoutOptions(IExplorationEventContext context, int expectedStep, out ExplorationEventStepResult step, out string reason)
        {
            step = null;
            if (!CheckStep(ExplorationEventExecutionState.AwaitingInput, expectedStep, out reason)) return false;
            if (DescribeOptions(context).Any(option => option.IsEnabled)) { reason = "An option can still be used."; return false; }
            State = ExplorationEventExecutionState.Completed;
            step = Commit(new List<ExplorationEventStepEntry>());
            return true;
        }

        private bool TryInput(ExplorationEventInput input, IExplorationEventContext context, int expectedStep, ExplorationEventExecutionState requiredState,
            out ExplorationEventStepResult step, out string reason)
        {
            step = null;
            if (!CheckStep(requiredState, expectedStep, out reason)) return false;
            if (!CurrentPage.Interaction.TryResolve(input, context, out var resolution, out reason)) return false;
            var entries = new List<ExplorationEventStepEntry> { new(ExplorationEventStepEntryKind.Input, resolution.Description) };
            if (resolution.StartsSlotCheck)
            {
                if (input.Kind != ExplorationEventInputKind.BeginSlotCheck) { reason = "Only the slot check input can start a slot check."; return false; }
                State = ExplorationEventExecutionState.AwaitingSlotResult;
            }
            else if (ApplyEffects(resolution.Branch.Effects, context, entries))
            {
                Enter(resolution.Branch.Next, context, entries);
            }
            step = Commit(entries);
            return true;
        }

        private bool CheckStep(ExplorationEventExecutionState requiredState, int expectedStep, out string reason)
        {
            if (State != requiredState) { reason = $"The event is {State}, not {requiredState}."; return false; }
            if (expectedStep >= 0 && expectedStep != AppliedStepCount) { reason = $"The input was made for step {expectedStep}, but the event is at step {AppliedStepCount}."; return false; }
            reason = string.Empty;
            return true;
        }

        private void Enter(string target, IExplorationEventContext context, List<ExplorationEventStepEntry> entries)
        {
            if (ExplorationEventTargets.IsEnd(target)) { State = ExplorationEventExecutionState.Completed; return; }
            if (TotalPageVisits >= ExplorationEventLimits.MaximumPageVisits)
            {
                Error = $"The event '{Event.EventId}' exceeded {ExplorationEventLimits.MaximumPageVisits} page visits and was ended.";
                entries.Add(new ExplorationEventStepEntry(ExplorationEventStepEntryKind.PageVisitLimit, Error));
                State = ExplorationEventExecutionState.Completed;
                return;
            }
            var page = Event.GetPage(target);
            TotalPageVisits++;
            visits[page.PageId] = VisitsOf(page.PageId) + 1;
            CurrentPageId = page.PageId;
            State = ExplorationEventExecutionState.AwaitingInput;
            ApplyEffects(page.OnEnter, context, entries);
        }

        // False when health reached 0 (the state is then Defeated).
        private bool ApplyEffects(IReadOnlyList<IExplorationEventEffect> effects, IExplorationEventContext context, List<ExplorationEventStepEntry> entries)
        {
            foreach (var effect in effects)
            {
                try
                {
                    var result = effect.Apply(context) ?? ExplorationEventEffectResult.Skipped("The effect returned no result.");
                    entries.Add(new ExplorationEventStepEntry(result.Status == ExplorationEventEffectStatus.Applied
                        ? ExplorationEventStepEntryKind.EffectApplied : ExplorationEventStepEntryKind.EffectSkipped, result.Message));
                }
                catch (Exception exception)
                {
                    entries.Add(new ExplorationEventStepEntry(ExplorationEventStepEntryKind.EffectFailed, $"{effect.GetType().Name}: {exception.Message}"));
                }
                if (context != null && context.TryGetHealth(out var health) && health.IsDefeated)
                {
                    entries.Add(new ExplorationEventStepEntry(ExplorationEventStepEntryKind.Defeated, string.Empty));
                    State = ExplorationEventExecutionState.Defeated;
                    return false;
                }
            }
            return true;
        }

        private ExplorationEventStepResult Commit(List<ExplorationEventStepEntry> entries)
        {
            AppliedStepCount++;
            return new ExplorationEventStepResult(entries.ToArray(), State, CurrentPageId);
        }
    }
}
