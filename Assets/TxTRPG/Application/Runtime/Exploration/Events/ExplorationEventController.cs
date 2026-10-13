using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Application.Combat;
using TxTRPG.Application.Players;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.Exploration.Events;
using TxTRPG.Gameplay.SlotMachine;
using TxTRPG.SceneTransition;
using TxTRPG.UI;
using TxTRPG.UI.Exploration;
using TxTRPG.UI.SlotMachine;
using UnityEngine;

namespace TxTRPG.Application.Exploration.Events
{
    public enum ExplorationEventOutcome
    {
        Completed = 1,
        // The node's event could not be loaded; the node completes and the exploration continues.
        Unavailable = 2,
        Defeated = 3
    }

    // What ExplorationRunController needs from the event node handler.
    public interface IExplorationEventController
    {
        bool HasEvents { get; }
        event Action<string, ExplorationEventOutcome> Completed;
        void Configure(ExplorationEventCatalog catalog);
        IExplorationNodeContentPicker CreateContentPicker();
        bool TryDescribe(string eventId, out string title, out string summary);
        void Begin(ExplorationRunState run, ExplorationNodeRecord node);
        // Binds again to the session record of the active node after a Scene reload; never picks or applies anything twice.
        void Restore(ExplorationRunState run, ExplorationNodeRecord node);
        void Hide();
    }

    // Runs event nodes: the event screen inside ExplorationNodePanel, Story lines, and slot checks on SlotMachinePanel.
    // The session record (PlayerSessionHost.ActiveExplorationEvent) is the source of truth, so a Scene reload binds to it
    // again. A slot check takes the shared action lock, decides its result once (spin + Fortune Critical, no rune
    // allocation), hides the node panel while the reels spin, and reports the final total exactly once.
    [DisallowMultipleComponent]
    public sealed class ExplorationEventController : MonoBehaviour, ISceneInitializer, IExplorationEventController
    {
        [SerializeField] private ExplorationEventView view;
        [SerializeField] private StoryTextPanel storyPanel;
        [SerializeField] private SlotMachinePanel slotPanel;
        [SerializeField, Tooltip("ExplorationNodePanel. Hidden while a slot check spins so the slot machine is visible.")]
        private GameObject nodePanel;
        [SerializeField, Range(1f, 15f), Tooltip("Minimum wait for the spin presentation before the result is shown directly.")]
        private float spinTimeoutSeconds = 3f;
        [SerializeField, Range(0f, 5f)] private float spinTimeoutMarginSeconds = 1f;

        private PlayerSessionHost host;
        private ExplorationEventCatalogRuntime catalog;
        private IRandomIndexProvider random;
        private IRandomIndexProvider fortuneRandom;
        private TemporaryExplorationEventRecord shown;
        private TemporaryExplorationEventSlotCheck presenting;
        private bool initialized;
        private bool reporting;

        public int InitializationOrder => -795;
        public bool HasEvents => catalog != null && catalog.CanPick;
        public ExplorationEventView View => view;
        public event Action<string, ExplorationEventOutcome> Completed;

        public async Task InitializeAsync(SceneInitializationContext context, CancellationToken cancellationToken)
        {
            Unbind();
            initialized = false;
            host = PlayerSessionHost.Instance ?? throw new InvalidOperationException("Exploration events require PlayerSessionHost.");
            await host.EnsureInitializedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (view == null || storyPanel == null) throw new InvalidOperationException("Exploration event references are incomplete.");
            host.TemporaryCombatActionEnded += OnActionEnded;
            random ??= new SystemRandomIndexProvider();
            // Separate seed so Fortune Critical draws never correlate with the reel stops.
            fortuneRandom ??= new SystemRandomIndexProvider(Guid.NewGuid().GetHashCode());
            if (shown == null) view.Hide();
            initialized = true;
        }

        // Missing or invalid catalogs never stop the game: event nodes just do not appear.
        public void Configure(ExplorationEventCatalog source)
        {
            catalog = null;
            if (source == null) { Debug.LogWarning("Exploration events are disabled: no event catalog is assigned.", this); return; }
            var skipped = new List<string>();
            if (!source.TryBuild(out var built, out var error, skipped))
            {
                Debug.LogError($"Exploration events are disabled: the event catalog is invalid. {error}", source);
                return;
            }
            foreach (var reason in skipped) Debug.LogError($"Exploration event was left out of the catalog: {reason}", source);
            if (!built.CanPick) Debug.LogError("Exploration events are disabled: no valid event with a positive weight.", source);
            catalog = built;
        }

        public IExplorationNodeContentPicker CreateContentPicker() => HasEvents ? catalog.CreatePicker() : null;

        public bool TryDescribe(string eventId, out string title, out string summary)
        {
            if (catalog != null && catalog.TryGet(eventId, out var found)) { title = found.Title; summary = found.Summary; return true; }
            title = summary = string.Empty;
            return false;
        }

        public void Begin(ExplorationRunState run, ExplorationNodeRecord node)
        {
            if (!initialized) throw new InvalidOperationException("The exploration event controller is not initialized.");
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (node == null) throw new ArgumentNullException(nameof(node));
            ExplorationEventExecution execution = null;
            if (catalog != null && catalog.TryGet(node.ContentId, out var found)) execution = new ExplorationEventExecution(found);
            else Debug.LogWarning($"Exploration event '{node.ContentId}' of {node.Id} could not be loaded.", this);
            var record = new TemporaryExplorationEventRecord(run.RunId, node.Id, node.ContentId, execution);
            host.SetActiveExplorationEvent(record);
            shown = record;
            if (execution != null)
            {
                record.AddStory(ExplorationEventStoryFormatter.Started(execution.Event.Title));
                try { AddStep(record, execution.Start(Context), null); }
                catch (Exception exception)
                {
                    Debug.LogError($"Exploration event '{node.ContentId}' failed to start: {exception}", this);
                    record.AddStory(ExplorationEventStoryFormatter.ActionFailed);
                }
            }
            Present(record);
        }

        public void Restore(ExplorationRunState run, ExplorationNodeRecord node)
        {
            if (!initialized) throw new InvalidOperationException("The exploration event controller is not initialized.");
            var record = host.ActiveExplorationEvent;
            if (record == null || record.RunId != run.RunId || record.NodeId != node.Id) { Begin(run, node); return; }
            shown = record;
            var check = record.SlotCheck;
            // The new Scene's panel shows the event's accepted slot result at rest, whether or not it is applied yet.
            if (check != null) ShowResultAtRest(check);
            if (record.Execution?.State == ExplorationEventExecutionState.AwaitingSlotResult && check != null && !check.Resolved &&
                host.IsTemporaryActionHeld(check.ActionId) && presenting != check)
            {
                // The previous Scene's task still applies it; show the accepted result and wait for the lock release.
                if (nodePanel != null) nodePanel.SetActive(false);
                ShowResultAtRest(check);
                FlushStory(record);
                return;
            }
            Present(record);
        }

        public void Hide()
        {
            shown = null;
            if (view != null) view.Hide();
        }

        private ExplorationEventContext Context => ExplorationEventContext.FromSession(host);

        // Shows the record's current state; a finished event is reported once.
        private void Present(TemporaryExplorationEventRecord record)
        {
            if (this == null || host == null || !ReferenceEquals(host.ActiveExplorationEvent, record) || !ReferenceEquals(shown, record)) return;
            FlushStory(record);
            if (record.IsUnavailable)
            {
                ShowPage(ExplorationEventStoryFormatter.UnavailableTitle, ExplorationEventStoryFormatter.UnavailableBody,
                    new[] { new ExplorationEventChoiceDisplay(ExplorationEventStoryFormatter.ContinueLabel, true, string.Empty) },
                    _ => Report(record, ExplorationEventOutcome.Unavailable));
                return;
            }
            var execution = record.Execution;
            if (execution.IsFinished)
            {
                Report(record, execution.State == ExplorationEventExecutionState.Defeated ? ExplorationEventOutcome.Defeated : ExplorationEventOutcome.Completed);
                return;
            }
            if (presenting == null && nodePanel != null && !nodePanel.activeSelf) nodePanel.SetActive(true);
            var page = execution.CurrentPage;
            var title = string.IsNullOrWhiteSpace(page.Title) ? execution.Event.Title : page.Title;
            if (execution.State == ExplorationEventExecutionState.AwaitingSlotResult)
            {
                PresentAwaitingSlot(record, title, page);
                return;
            }
            var step = execution.AppliedStepCount;
            var options = DescribeOptions(execution);
            var choices = new List<ExplorationEventChoiceDisplay>(options.Count + 1);
            var anyEnabled = false;
            foreach (var option in options)
            {
                anyEnabled |= option.IsEnabled;
                choices.Add(new ExplorationEventChoiceDisplay(option.Label, option.IsEnabled,
                    option.IsEnabled ? string.Empty : ExplorationEventStoryFormatter.ConditionUnmet(option.DisabledReason)));
            }
            if (!anyEnabled) choices.Add(new ExplorationEventChoiceDisplay(ExplorationEventStoryFormatter.EndEventLabel, true, string.Empty));
            ShowPage(title, page.Body, choices, index => OnChoice(record, step, options, index));
        }

        private void PresentAwaitingSlot(TemporaryExplorationEventRecord record, string title, ExplorationEventPage page)
        {
            var check = record.SlotCheck;
            var label = (page.Interaction as ExplorationEventSlotCheck)?.Label ?? ExplorationEventStoryFormatter.SlotCheckLabel;
            if (check == null)
            {
                // Accepted, but no result was decided (the spin failed): spinning again is safe.
                ShowPage(title, page.Body, new[] { new ExplorationEventChoiceDisplay(label, host.TemporarySlotMachine != null,
                    ExplorationEventStoryFormatter.SlotUnavailableReason) }, _ => SpinAccepted(record));
                return;
            }
            if (!check.Resolved && presenting != check && !host.IsTemporaryActionHeld(check.ActionId))
            {
                // Nobody is applying the decided result any more: report it now, once.
                ResolveSlotCheck(host, record, check);
                ShowResultAtRest(check);
                Present(record);
                return;
            }
            view.Show(new ExplorationEventPageDisplay(title, page.Body, new[] { new ExplorationEventChoiceDisplay(label, false, string.Empty) }, true), null);
        }

        private IReadOnlyList<ExplorationEventOption> DescribeOptions(ExplorationEventExecution execution)
        {
            var options = execution.DescribeOptions(Context);
            if (host.TemporarySlotMachine != null) return options;
            var adjusted = new ExplorationEventOption[options.Count];
            for (var i = 0; i < options.Count; i++)
                adjusted[i] = options[i].Input == ExplorationEventInputKind.BeginSlotCheck
                    ? new ExplorationEventOption(options[i].Input, options[i].OptionIndex, options[i].Label, false, ExplorationEventStoryFormatter.SlotUnavailableReason)
                    : options[i];
            return adjusted;
        }

        private void ShowPage(string title, string body, IReadOnlyList<ExplorationEventChoiceDisplay> choices, Action<int> onChoice) =>
            view.Show(new ExplorationEventPageDisplay(title, body, choices), onChoice);

        private void OnChoice(TemporaryExplorationEventRecord record, int step, IReadOnlyList<ExplorationEventOption> options, int index)
        {
            if (!ReferenceEquals(host?.ActiveExplorationEvent, record) || !ReferenceEquals(shown, record)) return;
            var execution = record.Execution;
            var context = Context;
            ExplorationEventStepResult result;
            string reason;
            ExplorationEventInputKind? input = null;
            bool accepted;
            try
            {
                if (index == options.Count) accepted = execution.TryEndWithoutOptions(context, step, out result, out reason);
                else if (index < 0 || index > options.Count) { Present(record); return; }
                else
                {
                    var option = options[index];
                    input = option.Input;
                    switch (option.Input)
                    {
                        case ExplorationEventInputKind.BeginSlotCheck:
                            BeginSlotCheck(record, step);
                            return;
                        case ExplorationEventInputKind.Choose:
                            accepted = execution.TryChoose(context, option.OptionIndex, step, out result, out reason);
                            break;
                        default:
                            accepted = execution.TryContinue(context, step, out result, out reason);
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Exploration event input failed: {exception}", this);
                record.AddStory(ExplorationEventStoryFormatter.ActionFailed);
                Present(record);
                return;
            }
            if (accepted) AddStep(record, result, input);
            else Debug.LogWarning($"Exploration event input was rejected: {reason}", this);
            Present(record);
        }

        // ---------- Slot check ----------

        private void BeginSlotCheck(TemporaryExplorationEventRecord record, int step)
        {
            if (!host.TryBeginTemporarySlotPresentation(out var actionId))
            {
                record.AddStory(ExplorationEventStoryFormatter.SlotBusy);
                Present(record);
                return;
            }
            try
            {
                if (!record.Execution.TryBeginSlotCheck(Context, step, out _, out var reason))
                {
                    host.EndTemporaryCombatAction(actionId);
                    Debug.LogWarning($"Exploration event slot check was rejected: {reason}", this);
                    Present(record);
                    return;
                }
                var check = AcceptSpin(record, actionId);
                _ = CompleteSlotCheckAsync(host, record, check);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Exploration event slot check could not be accepted: {exception}", this);
                host.EndTemporaryCombatAction(actionId);
                record.AddStory(ExplorationEventStoryFormatter.ActionFailed);
                Present(record);
            }
        }

        private void SpinAccepted(TemporaryExplorationEventRecord record)
        {
            if (!ReferenceEquals(host?.ActiveExplorationEvent, record) || record.SlotCheck != null) return;
            if (!host.TryBeginTemporarySlotPresentation(out var actionId)) { record.AddStory(ExplorationEventStoryFormatter.SlotBusy); Present(record); return; }
            try { _ = CompleteSlotCheckAsync(host, record, AcceptSpin(record, actionId)); }
            catch (Exception exception)
            {
                Debug.LogError($"Exploration event slot check could not be accepted: {exception}", this);
                host.EndTemporaryCombatAction(actionId);
                record.AddStory(ExplorationEventStoryFormatter.ActionFailed);
                Present(record);
            }
        }

        // The result is decided here, once, and kept in the session record; it is never spun again.
        private TemporaryExplorationEventSlotCheck AcceptSpin(TemporaryExplorationEventRecord record, long actionId)
        {
            var slots = host.TemporarySlotMachine ?? throw new InvalidOperationException("The slot machine is not available.");
            var interaction = record.Execution.CurrentPage.Interaction as ExplorationEventSlotCheck;
            var spin = slots.Spin(random);
            var fortune = interaction == null || interaction.UseFortuneCritical
                ? FortuneCriticalResolver.Resolve(spin, host.TemporaryFortuneCritical?.Rules ?? FortuneCriticalRules.Disabled, fortuneRandom)
                : FortuneCriticalResult.None(spin);
            var check = new TemporaryExplorationEventSlotCheck(actionId, record.Execution.AppliedStepCount, spin, fortune);
            record.SlotCheck = check;
            return check;
        }

        private async Task CompleteSlotCheckAsync(PlayerSessionHost acceptedHost, TemporaryExplorationEventRecord record, TemporaryExplorationEventSlotCheck check)
        {
            presenting = check;
            try
            {
                if (nodePanel != null) nodePanel.SetActive(false);
                await PresentSpinAsync(acceptedHost, check);
            }
            catch (Exception exception) { Debug.LogWarning($"Event slot presentation failed; applying the accepted result: {exception.Message}"); }
            finally
            {
                try { ResolveSlotCheck(acceptedHost, record, check); }
                catch (Exception exception)
                {
                    Debug.LogError($"Exploration event slot result could not be applied: {exception}");
                    record.AddStory(ExplorationEventStoryFormatter.ActionFailed);
                }
                acceptedHost.EndTemporaryCombatAction(check.ActionId);
                if (this != null)
                {
                    presenting = null;
                    if (ReferenceEquals(acceptedHost.ActiveExplorationEvent, record) && nodePanel != null) nodePanel.SetActive(true);
                    Present(record);
                }
            }
        }

        // Reports the final total to the execution exactly once, from whichever controller gets there first.
        private static void ResolveSlotCheck(PlayerSessionHost acceptedHost, TemporaryExplorationEventRecord record, TemporaryExplorationEventSlotCheck check)
        {
            if (!check.TryMarkResolved()) return;
            if (!ReferenceEquals(acceptedHost.ActiveExplorationEvent, record) || !ReferenceEquals(record.SlotCheck, check))
            {
                Debug.LogWarning("An accepted event slot check belongs to a previous event and was not applied.");
                return;
            }
            record.AddStory(ExplorationEventStoryFormatter.SlotCheck(check.Fortune));
            if (record.Execution.TryResolveSlotCheck(ExplorationEventContext.FromSession(acceptedHost), check.Fortune.FinalTotal, check.Step,
                    out var step, out var reason))
                AddStep(record, step, ExplorationEventInputKind.SlotResult);
            else
                Debug.LogWarning($"Exploration event slot result was rejected: {reason}");
        }

        private async Task PresentSpinAsync(PlayerSessionHost acceptedHost, TemporaryExplorationEventSlotCheck check)
        {
            var panel = slotPanel;
            if (panel == null || !panel.isActiveAndEnabled) return;
            var display = CreateDisplay(acceptedHost, check);
            using var cancellation = new CancellationTokenSource();
            var timeout = TemporaryCombatPresentationTimeout.Resolve(spinTimeoutSeconds,
                panel.GetMaximumPresentationSeconds(display.Reels.Count), spinTimeoutMarginSeconds);
#if UNITY_EDITOR
            if (editorTimeoutOverrideSeconds > 0f) timeout = editorTimeoutOverrideSeconds;
#endif
            var animation = panel.PlaySpinAsync(display, acceptedHost.TemporaryDicePreferences.ReduceMotion, cancellation.Token);
            var finished = await Task.WhenAny(animation, Task.Delay(Mathf.CeilToInt(timeout * 1000f)));
            if (finished != animation)
            {
                cancellation.Cancel();
                if (panel != null) panel.StopAtResult(display);
                _ = ObserveLateAnimationAsync(animation);
                Debug.LogWarning($"EVENT_SLOT_SPIN_TIMEOUT: presentation exceeded {timeout:F2}s; the reels were stopped at the accepted result.");
                return;
            }
            if (!await animation && panel != null) panel.StopAtResult(display);
        }

        private static async Task ObserveLateAnimationAsync(Task task)
        { try { await task; } catch (Exception exception) { Debug.LogWarning($"Late event slot animation failed: {exception.Message}"); } }

        private static SlotSpinDisplay CreateDisplay(PlayerSessionHost source, TemporaryExplorationEventSlotCheck check) =>
            TemporarySlotCombatController.CreateSpinDisplay(check.Spin, check.Fortune,
                source.TemporaryRunes != null ? TemporarySlotCombatController.WeightLines(source.TemporaryRunes) : Array.Empty<SlotRuneLineDisplay>());

        private void ShowResultAtRest(TemporaryExplorationEventSlotCheck check)
        {
            if (slotPanel == null || slotPanel.IsSpinning) return;
            try { slotPanel.StopAtResult(CreateDisplay(host, check)); }
            catch (Exception exception) { Debug.LogWarning($"Event slot result display failed: {exception.Message}", this); }
        }

        // ---------- Story and completion ----------

        private static void AddStep(TemporaryExplorationEventRecord record, ExplorationEventStepResult step, ExplorationEventInputKind? input)
        {
            record.AddStory(ExplorationEventStoryFormatter.Step(step, input));
            foreach (var entry in step.Entries)
            {
                if (entry.Kind == ExplorationEventStepEntryKind.EffectSkipped) Debug.LogWarning($"Exploration event effect skipped ({record.EventId}): {entry.Text}");
                else if (entry.Kind == ExplorationEventStepEntryKind.EffectFailed || entry.Kind == ExplorationEventStepEntryKind.PageVisitLimit)
                    Debug.LogError($"Exploration event error ({record.EventId}): {entry.Text}");
            }
        }

        private void FlushStory(TemporaryExplorationEventRecord record)
        {
            if (storyPanel == null || !storyPanel.isActiveAndEnabled) return;
            foreach (var line in record.TakeStory()) storyPanel.AddMessage(line);
        }

        private void Report(TemporaryExplorationEventRecord record, ExplorationEventOutcome outcome)
        {
            if (reporting || !ReferenceEquals(host.ActiveExplorationEvent, record)) return;
            reporting = true;
            try
            {
                if (outcome == ExplorationEventOutcome.Completed) record.AddStory(ExplorationEventStoryFormatter.Ended(record.Execution.Event.Title));
                FlushStory(record);
                view.Hide();
                Completed?.Invoke(record.NodeId, outcome);
                host.ClearActiveExplorationEvent(record);
                if (ReferenceEquals(shown, record)) shown = null;
            }
            catch (Exception exception) { Debug.LogError($"Exploration event completion failed: {exception}", this); }
            finally { reporting = false; }
        }

        // A slot check applied by a controller of the previous Scene (or this one) released the lock: show the new state.
        private void OnActionEnded()
        {
            if (this == null) { Unbind(); return; }
            if (presenting != null || host == null || shown == null || !ReferenceEquals(host.ActiveExplorationEvent, shown)) return;
            if (shown.SlotCheck != null) ShowResultAtRest(shown.SlotCheck);
            Present(shown);
        }

        private void Unbind()
        {
            if (host != null) host.TemporaryCombatActionEnded -= OnActionEnded;
            host = null;
        }

        private void OnDestroy() => Unbind();

#if UNITY_EDITOR
        public void ConfigureForEditor(ExplorationEventView targetView, StoryTextPanel targetStory, SlotMachinePanel targetSlotPanel, GameObject targetNodePanel)
        { view = targetView; storyPanel = targetStory; slotPanel = targetSlotPanel; nodePanel = targetNodePanel; }

        public void SetRandomForTests(IRandomIndexProvider source) => random = source;
        public void SetFortuneRandomForTests(IRandomIndexProvider source) => fortuneRandom = source;
        private float editorTimeoutOverrideSeconds;
        public void SetSpinTimeoutOverrideForEditor(float seconds) => editorTimeoutOverrideSeconds = Mathf.Max(0f, seconds);
        public StoryTextPanel StoryForEditor => storyPanel;
        public SlotMachinePanel SlotPanelForEditor => slotPanel;
        public GameObject NodePanelForEditor => nodePanel;
        public bool IsPresentingForEditor => presenting != null;
        // Same path as a click on the given choice of the shown page.
        public void ChooseForEditor(int index) => view.RequestForEditor(index);
#endif
    }
}
