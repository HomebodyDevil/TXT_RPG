using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Exploration.Events;
using UnityEngine;

namespace TxTRPG.Application.Exploration.Events
{
    // Marks a [SerializeReference] field (or list) whose implementation class is picked from a dropdown in the Inspector.
    // The candidates are found in the Editor only; nothing is searched by reflection at runtime.
    public sealed class ExplorationEventReferencePickerAttribute : PropertyAttribute { }

    // Effects applied in order, then the next page ID or "End".
    [Serializable]
    public sealed class ExplorationEventBranchDefinition
    {
        [SerializeReference, ExplorationEventReferencePicker] private List<IExplorationEventEffect> effects = new();
        [SerializeField, Tooltip("Page ID to go to, or End.")] private string next = ExplorationEventTargets.End;

        public ExplorationEventBranchDefinition() { }
        public ExplorationEventBranchDefinition(string nextPage, params IExplorationEventEffect[] branchEffects)
        {
            next = nextPage;
            effects = new List<IExplorationEventEffect>(branchEffects ?? Array.Empty<IExplorationEventEffect>());
        }

        public IReadOnlyList<IExplorationEventEffect> Effects => effects ?? (IReadOnlyList<IExplorationEventEffect>)Array.Empty<IExplorationEventEffect>();
        public string Next => next;
        public ExplorationEventBranch CreateBranch() => new(effects, next);
    }

    // Base of the serialized page interactions. A new interaction kind is a [Serializable] subclass with a parameterless
    // constructor and serialized fields only (the asset is shared, so no run state in fields) that creates its Gameplay
    // IExplorationEventInteraction. When a subclass is renamed or moved, add
    // [UnityEngine.Scripting.APIUpdating.MovedFrom] so existing assets keep their data.
    [Serializable]
    public abstract class ExplorationEventInteractionDefinition
    {
        public abstract bool TryCreate(out IExplorationEventInteraction interaction, out string reason);
    }

    [Serializable]
    public sealed class ExplorationEventContinueDefinition : ExplorationEventInteractionDefinition
    {
        [SerializeField] private string label = ExplorationEventStoryFormatter.ContinueLabel;
        [SerializeField] private ExplorationEventBranchDefinition branch = new();

        public ExplorationEventContinueDefinition() { }
        public ExplorationEventContinueDefinition(string buttonLabel, ExplorationEventBranchDefinition then) { label = buttonLabel; branch = then; }

        public override bool TryCreate(out IExplorationEventInteraction interaction, out string reason)
        {
            interaction = null;
            if (branch == null) { reason = "Continue has no branch."; return false; }
            interaction = new ExplorationEventContinue(label, branch.CreateBranch());
            reason = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class ExplorationEventChoiceOptionDefinition
    {
        [SerializeField] private string label = string.Empty;
        [SerializeReference, ExplorationEventReferencePicker] private List<IExplorationEventCondition> conditions = new();
        [SerializeField] private ExplorationEventBranchDefinition branch = new();

        public ExplorationEventChoiceOptionDefinition() { }
        public ExplorationEventChoiceOptionDefinition(string optionLabel, ExplorationEventBranchDefinition then, params IExplorationEventCondition[] optionConditions)
        {
            label = optionLabel;
            branch = then;
            conditions = new List<IExplorationEventCondition>(optionConditions ?? Array.Empty<IExplorationEventCondition>());
        }

        public ExplorationEventChoiceOption Create() =>
            new(label, conditions, (branch ?? throw new InvalidOperationException($"Option '{label}' has no branch.")).CreateBranch());
    }

    [Serializable]
    public sealed class ExplorationEventChoiceDefinition : ExplorationEventInteractionDefinition
    {
        [SerializeField] private List<ExplorationEventChoiceOptionDefinition> options = new();

        public ExplorationEventChoiceDefinition() { }
        public ExplorationEventChoiceDefinition(params ExplorationEventChoiceOptionDefinition[] choiceOptions) =>
            options = new List<ExplorationEventChoiceOptionDefinition>(choiceOptions ?? Array.Empty<ExplorationEventChoiceOptionDefinition>());

        public override bool TryCreate(out IExplorationEventInteraction interaction, out string reason)
        {
            interaction = null;
            var created = new List<ExplorationEventChoiceOption>();
            for (var i = 0; i < (options?.Count ?? 0); i++)
            {
                if (options[i] == null) { reason = $"Option {i + 1} is empty."; return false; }
                created.Add(options[i].Create());
            }
            interaction = new ExplorationEventChoice(created);
            reason = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class ExplorationEventSlotOutcomeDefinition
    {
        [SerializeField] private bool hasMinimum;
        [SerializeField] private int minimum;
        [SerializeField] private bool hasMaximum;
        [SerializeField] private int maximum;
        [SerializeField] private ExplorationEventBranchDefinition branch = new();

        public ExplorationEventSlotOutcomeDefinition() { }
        public ExplorationEventSlotOutcomeDefinition(int? min, int? max, ExplorationEventBranchDefinition then)
        {
            hasMinimum = min.HasValue; minimum = min ?? 0; hasMaximum = max.HasValue; maximum = max ?? 0; branch = then;
        }

        public ExplorationEventSlotOutcome Create() =>
            new(hasMinimum ? minimum : null, hasMaximum ? maximum : null,
                (branch ?? throw new InvalidOperationException("A slot range has no branch.")).CreateBranch());
    }

    [Serializable]
    public sealed class ExplorationEventSlotCheckDefinition : ExplorationEventInteractionDefinition
    {
        [SerializeField] private string label = ExplorationEventStoryFormatter.SlotCheckLabel;
        [SerializeField, Tooltip("Apply the session Fortune Critical rules to the spin, as in combat.")]
        private bool useFortuneCritical = true;
        [SerializeField, Tooltip("Inclusive ranges of the final total, checked from the top. The first match wins.")]
        private List<ExplorationEventSlotOutcomeDefinition> outcomes = new();
        [SerializeField, Tooltip("Used when no range matches.")] private ExplorationEventBranchDefinition fallback = new();

        public ExplorationEventSlotCheckDefinition() { }
        public ExplorationEventSlotCheckDefinition(string buttonLabel, bool fortune, ExplorationEventBranchDefinition otherwise,
            params ExplorationEventSlotOutcomeDefinition[] ranges)
        {
            label = buttonLabel; useFortuneCritical = fortune; fallback = otherwise;
            outcomes = new List<ExplorationEventSlotOutcomeDefinition>(ranges ?? Array.Empty<ExplorationEventSlotOutcomeDefinition>());
        }

        public override bool TryCreate(out IExplorationEventInteraction interaction, out string reason)
        {
            interaction = null;
            if (fallback == null) { reason = "The slot check has no fallback."; return false; }
            var created = new List<ExplorationEventSlotOutcome>();
            for (var i = 0; i < (outcomes?.Count ?? 0); i++)
            {
                if (outcomes[i] == null) { reason = $"Slot range {i + 1} is empty."; return false; }
                created.Add(outcomes[i].Create());
            }
            interaction = new ExplorationEventSlotCheck(label, useFortuneCritical, created, fallback.CreateBranch());
            reason = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class ExplorationEventPageDefinition
    {
        [SerializeField] private string pageId = "start";
        [SerializeField, Tooltip("Optional. The event title is shown when empty.")] private string title = string.Empty;
        [SerializeField, TextArea(3, 10)] private string body = string.Empty;
        [SerializeReference, ExplorationEventReferencePicker, Tooltip("Applied once each time the page is entered.")]
        private List<IExplorationEventEffect> onEnter = new();
        [SerializeReference, ExplorationEventReferencePicker] private ExplorationEventInteractionDefinition interaction = new ExplorationEventContinueDefinition();

        public ExplorationEventPageDefinition() { }
        public ExplorationEventPageDefinition(string id, string pageTitle, string pageBody, ExplorationEventInteractionDefinition pageInteraction,
            params IExplorationEventEffect[] enterEffects)
        {
            pageId = id; title = pageTitle; body = pageBody; interaction = pageInteraction;
            onEnter = new List<IExplorationEventEffect>(enterEffects ?? Array.Empty<IExplorationEventEffect>());
        }

        public string PageId => pageId?.Trim() ?? string.Empty;

        public bool TryCreate(out ExplorationEventPage page, out string reason)
        {
            page = null;
            if (interaction == null) { reason = $"Page '{PageId}' has no interaction."; return false; }
            if (!interaction.TryCreate(out var created, out reason)) { reason = $"Page '{PageId}': {reason}"; return false; }
            page = new ExplorationEventPage(pageId, title, body, onEnter, created);
            return true;
        }
    }

    // One exploration event, authored in the Inspector: pages with onEnter effects and one interaction each. The asset is
    // read-only at runtime; TryCreateEvent turns it into a validated Gameplay ExplorationEvent.
    [CreateAssetMenu(menuName = "TxT RPG/Exploration/Event", fileName = "ExplorationEvent")]
    public sealed class ExplorationEventDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable ID stored in exploration node records, for example event.test.signpost.")]
        private string eventId = string.Empty;
        [SerializeField] private string title = string.Empty;
        [SerializeField, Tooltip("Short description shown on the node card.")] private string summary = string.Empty;
        [SerializeField] private string startPageId = "start";
        [SerializeField] private List<ExplorationEventPageDefinition> pages = new() { new ExplorationEventPageDefinition() };

        public string EventId => eventId?.Trim() ?? string.Empty;
        public string Title => title ?? string.Empty;
        public string Summary => summary ?? string.Empty;

        public bool TryCreateEvent(out ExplorationEvent result, out string reason)
        {
            result = null;
            try
            {
                var created = new List<ExplorationEventPage>();
                for (var i = 0; i < (pages?.Count ?? 0); i++)
                {
                    if (pages[i] == null) { reason = $"Page {i + 1} is empty."; return false; }
                    if (!pages[i].TryCreate(out var page, out reason)) return false;
                    created.Add(page);
                }
                return ExplorationEvent.TryCreate(eventId, title, summary, startPageId, created, out result, out reason);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                reason = exception.Message;
                return false;
            }
        }

        // Validation errors and authoring warnings, for the Inspector and the validation menu.
        public IReadOnlyList<string> CollectWarnings()
        {
            var warnings = new List<string>();
            if (TryCreateEvent(out var created, out _)) warnings.AddRange(created.CollectWarnings());
            return warnings;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(string id, string eventTitle, string eventSummary, string startPage, IEnumerable<ExplorationEventPageDefinition> eventPages)
        {
            eventId = id; title = eventTitle; summary = eventSummary; startPageId = startPage;
            pages = new List<ExplorationEventPageDefinition>(eventPages ?? Array.Empty<ExplorationEventPageDefinition>());
        }
#endif
    }
}
