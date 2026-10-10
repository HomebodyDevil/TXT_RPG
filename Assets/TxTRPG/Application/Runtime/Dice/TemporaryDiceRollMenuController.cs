using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Combat;
using TxTRPG.Application.Players;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Dice;
using TxTRPG.SceneTransition;
using TxTRPG.UI;
using TxTRPG.UI.Dice;
using TxTRPG.UI.Windows;
using UnityEngine;

namespace TxTRPG.Application.Dice
{
    [DisallowMultipleComponent]
    public sealed class TemporaryDiceRollMenuController : MonoBehaviour, IGameMenuCommandHandler, ISceneInitializer, ITemporaryCombatController
    {
        public const string RollAllCommandId = "temporary-dice.roll-all";

        [SerializeField] private GameMenuPanel menu;
        [SerializeField, Tooltip("The combat action button on the EnemyDisplayPanel/StoryTextPanel boundary.")]
        private BoundaryCommandButton actionButton;
        [SerializeField] private StoryTextPanel storyPanel;
        [SerializeField] private TemporaryCombatConfiguration combatConfiguration;
        [SerializeField] private EnemyDisplayPanel enemyPanel;
        [SerializeField] private HealthBarPanel playerHealthBar;
        [SerializeField] private HealthBarPanel enemyHealthBar;
        [SerializeField] private TMP_Text nextEnemyActionText;
        [SerializeField] private OwnedDiceSessionBinder ownedDiceBinder;
        [SerializeField] private TemporaryDiceHistoryMode historyMode = TemporaryDiceHistoryMode.LatestCombatNode;
        [SerializeField, Min(1)] private int maximumHistoryCombats = 4;
        [SerializeField, Range(1f, 15f), Tooltip("Minimum wait for the dice presentation before the text fallback is used.")]
        private float rollTimeoutSeconds = 3f;
        [SerializeField, Range(0f, 5f), Tooltip("Added to the panel's longest presentation to form the actual timeout.")]
        private float rollTimeoutMarginSeconds = 1f;

        public const float MaximumRollTimeoutSeconds = TemporaryCombatPresentationTimeout.MaximumSeconds;

        private PlayerSessionHost sessionHost;
        private TemporaryCombatState combat;
        private bool initialized;
        private bool executing;
        private IRandomIndexSource random;

        public int InitializationOrder => -800;
        public bool IsReady => initialized && combat != null && sessionHost?.TemporaryDice != null;
        public TemporaryCombatState Combat => combat;
        public event Action<bool> CombatFinished;

        // The timeout never cuts a normal presentation short: max(minimum, longest presentation + margin), within [1, 15]s.
        public static float ResolveRollTimeout(float minimumSeconds, float presentationSeconds, float marginSeconds) =>
            TemporaryCombatPresentationTimeout.Resolve(minimumSeconds, presentationSeconds, marginSeconds);

        public async Task InitializeAsync(SceneInitializationContext context, CancellationToken cancellationToken)
        {
            if (sessionHost != null) sessionHost.TemporaryDiceStoryReady -= OnStoryReady;
            if (ownedDiceBinder != null) ownedDiceBinder.BlockingChanged -= RefreshMenuExecution;
            if (menu != null) menu.ExecutionStateRefreshed -= RefreshActionButton;
            ReleaseCombat();
            initialized = false;
            // The dice system is reserved: a disabled controller (TMP_MainScene uses the slot machine) still receives
            // InitializeAsync, because scene participants include inactive components. It then takes no part at all:
            // no subscriptions, Story output, result windows or action button updates.
            if (!isActiveAndEnabled) { sessionHost = null; return; }
            RefreshActionButton();
            sessionHost = PlayerSessionHost.Instance;
            if (sessionHost == null) throw new InvalidOperationException("Temporary combat requires PlayerSessionHost.");
            await sessionHost.EnsureInitializedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            sessionHost.TemporaryDiceHistory.Configure(historyMode, maximumHistoryCombats);
            sessionHost.TemporaryDiceStoryReady += OnStoryReady;
            if (sessionHost.PendingDiceStory != null) OnStoryReady(sessionHost.PendingDiceStory);
            if (ownedDiceBinder != null) ownedDiceBinder.BlockingChanged += RefreshMenuExecution;
            if (menu != null) menu.ExecutionStateRefreshed += RefreshActionButton;

            if (combatConfiguration == null || !combatConfiguration.EnabledForScene)
            {
                RefreshPresentation();
                RefreshMenuExecution();
                return;
            }

            random = new SystemRandomIndexSource();
            initialized = true;
            if (sessionHost.TemporaryExplorationCombat != null) AttachCombat(sessionHost.TemporaryExplorationCombat);
            else ClearCombat();
        }

        public void BeginCombat(HealthState playerHealth)
        {
            if (!initialized) throw new InvalidOperationException("Temporary combat controller is not initialized.");
            if (playerHealth == null) throw new ArgumentNullException(nameof(playerHealth));
            ReleaseCombat();
            combat = new TemporaryCombatState(playerHealth, combatConfiguration.EnemyMaximumHealth, combatConfiguration.EnemyAttackAmount, combatConfiguration.EnemyHealAmount);
            sessionHost.SetTemporaryExplorationCombat(combat);
            BeginHistoryScope();
            SubscribeCombat();
            ApplyEnemyPresentation();
            RefreshPresentation();
            RefreshMenuExecution();
        }

        public void ClearCombat()
        {
            ReleaseCombat();
            sessionHost?.SetTemporaryExplorationCombat(null);
            enemyPanel?.SetEnemies(Array.Empty<EnemyPresentation>());
            RefreshPresentation();
            RefreshMenuExecution();
        }
        public bool CanExecute(string commandId, out string unavailableReason)
        {
            if (!string.Equals(commandId?.Trim(), RollAllCommandId, StringComparison.Ordinal))
            { unavailableReason = "Unknown game-menu command."; return false; }
            if (executing) { unavailableReason = "The temporary combat action is already being processed."; return false; }
            if (sessionHost != null && sessionHost.IsTemporaryDiceActionPending)
            { unavailableReason = "A temporary dice action is still being completed."; return false; }
            if (ownedDiceBinder != null && ownedDiceBinder.BlocksNextAction ||
                menu != null && menu.WindowService != null && menu.WindowService.BlocksGameplayInput)
            { unavailableReason = "A result or game window is open."; return false; }
            if (!IsReady) { unavailableReason = "Temporary combat is not ready."; return false; }
            if (combat.IsComplete) { unavailableReason = "The temporary combat is complete."; return false; }
            if (storyPanel == null || !storyPanel.isActiveAndEnabled)
            { unavailableReason = "Story output is not ready."; return false; }
            if (sessionHost.TemporaryDice.Count == 0)
            { unavailableReason = "No temporary dice are owned."; return false; }
            unavailableReason = string.Empty;
            return true;
        }

        public bool TryExecute(string commandId)
        {
            if (!CanExecute(commandId, out var reason)) { Debug.LogWarning(reason, this); return false; }
            if (!sessionHost.TryBeginTemporaryDiceAction(combat, out var actionId)) return false;
            executing = true;
            RefreshMenuExecution();
            try
            {
                var rolls = sessionHost.TemporaryDice.RollAll(random);
                var acceptedHost = sessionHost;
                var acceptedCombat = combat;
                var runId = acceptedHost.TemporaryExploration?.RunId ?? string.Empty;
                var nodeId = acceptedHost.TemporaryExploration?.ActiveNodeId ?? string.Empty;
                _ = CompleteAcceptedActionAsync(acceptedHost, acceptedCombat, runId, nodeId,
                    actionId, rolls);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Temporary combat action could not be accepted: {exception.Message}", this);
                sessionHost.EndTemporaryDiceAction(actionId);
                executing = false;
                RefreshMenuExecution();
                return false;
            }
        }

        private async Task CompleteAcceptedActionAsync(PlayerSessionHost acceptedHost,
            TemporaryCombatState acceptedCombat, string runId, string nodeId,
            long actionId, IReadOnlyList<PlayerDieRoll> rolls)
        {
            try
            {
                await AnimateOrFallbackAsync(acceptedHost, rolls);
                if (PlayerSessionHost.Instance != acceptedHost ||
                    acceptedHost.TemporaryExplorationCombat != acceptedCombat ||
                    (acceptedHost.TemporaryExploration?.RunId ?? string.Empty) != runId ||
                    (acceptedHost.TemporaryExploration?.ActiveNodeId ?? string.Empty) != nodeId)
                {
                    Debug.LogWarning("An accepted dice roll belongs to a previous session or combat and was not applied.");
                    return;
                }
                var diceResults = new List<DiceRollResult>(rolls.Count);
                for (var i = 0; i < rolls.Count; i++) diceResults.Add(rolls[i].Result);
                // ExecuteTurn is deliberately invoked once; a partial failure is reported, never retried.
                var result = acceptedCombat.ExecuteTurn(diceResults);
                var record = acceptedHost.TemporaryDiceHistory.AddTurn(runId, nodeId, rolls, result);
                try { acceptedHost.PublishTemporaryDiceStory(record); }
                catch (Exception exception) { Debug.LogError($"Combat Story display failed: {exception}"); }
                if (this != null)
                {
                    try { ApplyEnemyPresentation(); RefreshPresentation(); }
                    catch (Exception exception) { Debug.LogWarning($"Combat UI refresh failed: {exception.Message}"); }
                }
                if (acceptedCombat.IsComplete)
                {
                    try
                    {
                        acceptedHost.NotifyTemporaryCombatCompleted(acceptedCombat, acceptedCombat.EnemyHealth.IsDefeated);
                        if (this != null) CombatFinished?.Invoke(acceptedCombat.EnemyHealth.IsDefeated);
                    }
                    catch (Exception exception) { Debug.LogError($"Combat completion notification failed: {exception}"); }
                }
                try { acceptedHost.QueueTemporaryDiceResult(record); }
                catch (Exception exception) { Debug.LogWarning($"Dice result modal could not open: {exception.Message}"); }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Temporary combat action failed after any already-applied changes: {exception}");
                if (storyPanel != null)
                    storyPanel.AddMessage(TemporaryCombatStoryText.ActionFailed);
            }
            finally
            {
                acceptedHost.EndTemporaryDiceAction(actionId);
                executing = false;
                if (this != null) RefreshMenuExecution();
            }
        }

        private async Task AnimateOrFallbackAsync(PlayerSessionHost acceptedHost, IReadOnlyList<PlayerDieRoll> rolls)
        {
            var panel = ownedDiceBinder != null ? ownedDiceBinder.Panel : null;
            if (panel == null || !panel.isActiveAndEnabled) return;
            var display = new DieRollDisplay[rolls.Count];
            for (var i = 0; i < display.Length; i++)
                display[i] = new DieRollDisplay(rolls[i].Id, rolls[i].Result.FaceIndex,
                    rolls[i].Result.EffectKind, rolls[i].Result.Amount);
            using var cancellation = new CancellationTokenSource();
            try
            {
                var timeout = ResolveRollTimeout(rollTimeoutSeconds, panel.MaximumPresentationSeconds, rollTimeoutMarginSeconds);
#if UNITY_EDITOR
                if (editorTimeoutOverrideSeconds > 0f) timeout = editorTimeoutOverrideSeconds;
#endif
                var animation = panel.PlayRollAsync(display,
                    acceptedHost.TemporaryDicePreferences.ReduceMotion, cancellation.Token);
                var finished = await Task.WhenAny(animation, Task.Delay(Mathf.CeilToInt(timeout * 1000f)));
                if (finished != animation)
                {
                    cancellation.Cancel();
                    // The dice are put at rest with the accepted faces before the text fallback and the result.
                    if (panel != null) panel.StopRollAtResults();
                    _ = ObserveLateAnimationAsync(animation);
                    if (panel != null) panel.ShowRollFallback(display);
                    Debug.LogWarning($"OWNED_DICE_ROLL_TIMEOUT: presentation exceeded {timeout:F2}s; the dice were stopped at the accepted faces.");
                    return;
                }
                if (!await animation) panel.ShowRollFallback(display);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Dice animation failed; applying the accepted result: {exception.Message}");
                if (panel != null) panel.ShowRollFallback(display);
            }
        }

        private static async Task ObserveLateAnimationAsync(Task task)
        { try { await task; } catch (Exception exception) { Debug.LogWarning($"Late dice animation failed: {exception.Message}"); } }

        private void BeginHistoryScope()
        {
            var runId = sessionHost?.TemporaryExploration?.RunId ?? string.Empty;
            var nodeId = sessionHost?.TemporaryExploration?.ActiveNodeId ?? string.Empty;
            sessionHost?.TemporaryDiceHistory.BeginCombat(runId, nodeId);
        }

        // The menu raises ExecutionStateRefreshed (also when game windows open or close), which refreshes the action button.
        private void RefreshMenuExecution()
        {
            if (menu != null) menu.RefreshExecutionState();
            else RefreshActionButton();
        }

        private void RefreshActionButton()
        {
            if (actionButton == null) return;
            actionButton.SetAvailable(IsReady && !combat.IsComplete);
        }

        private void OnStoryReady(TemporaryDiceTurnRecord record)
        {
            if (storyPanel == null || !storyPanel.isActiveAndEnabled) return;
            RecordTurn(record.Rolls, record.Events);
            sessionHost?.ClearPendingDiceStory(record);
        }

        private void RecordTurn(IReadOnlyList<PlayerDieRoll> rolls, IReadOnlyList<TemporaryCombatEvent> events)
        {
            var eventIndex = 0;
            for (var i = 0; i < rolls.Count; i++, eventIndex++)
            {
                var roll = rolls[i];
                var combatEvent = events[eventIndex];
                storyPanel.AddMessage(DiceRollStoryFormatter.Format(roll));
                var playerText = TemporaryCombatStoryText.PlayerEvent(combatEvent, combatConfiguration.EnemyName);
                if (playerText != null) storyPanel.AddMessage(playerText);
            }

            for (; eventIndex < events.Count; eventIndex++)
            {
                var outcomeText = TemporaryCombatStoryText.OutcomeEvent(events[eventIndex], combatConfiguration.EnemyName);
                if (outcomeText != null) storyPanel.AddMessage(outcomeText);
            }
        }

        private void AttachCombat(TemporaryCombatState existingCombat)
        {
            ReleaseCombat();
            combat = existingCombat;
            BeginHistoryScope();
            SubscribeCombat();
            ApplyEnemyPresentation();
            RefreshPresentation();
            RefreshMenuExecution();
        }

        private void SubscribeCombat()
        {
            combat.PlayerHealth.Changed += OnHealthChanged;
            combat.EnemyHealth.Changed += OnHealthChanged;
        }
        private void OnHealthChanged(HealthChangeResult _) => RefreshPresentation();

        private void RefreshPresentation()
        {
            if (combat == null)
            {
                playerHealthBar?.Clear();
                enemyHealthBar?.Clear();
                if (nextEnemyActionText != null) nextEnemyActionText.text = "전투 노드를 선택하세요";
                return;
            }

            ApplyHealth(playerHealthBar, combat.PlayerHealth, "플레이어 HP");
            ApplyHealth(enemyHealthBar, combat.EnemyHealth, $"{combatConfiguration.EnemyName} HP");
            if (nextEnemyActionText != null)
            {
                nextEnemyActionText.text = combat.IsComplete
                    ? (combat.EnemyHealth.IsDefeated ? "전투 종료: 승리" : "전투 종료: 패배")
                    : $"다음 적 행동: {(combat.NextEnemyAction == EnemyActionKind.Attack ? $"공격 {combatConfiguration.EnemyAttackAmount}" : $"회복 {combatConfiguration.EnemyHealAmount}")}";
            }
        }

        private void ApplyEnemyPresentation()
        {
            if (enemyPanel == null || combat == null) return;
            enemyPanel.SetEnemies(new[]
            {
                new EnemyPresentation(
                    combatConfiguration.EnemyInstanceId,
                    combatConfiguration.EnemyId,
                    isTargeted: !combat.EnemyHealth.IsDefeated,
                    isDefeated: combat.EnemyHealth.IsDefeated)
            });
        }

        private static void ApplyHealth(HealthBarPanel panel, HealthState health, string label)
        {
            if (panel == null || health == null) return;
            panel.Apply(new CharacterStatusPresentation(
                default,
                new HealthPresentation(health.Current, health.Maximum, label, $"{health.Current} / {health.Maximum}")));
        }

        private void ReleaseCombat()
        {
            if (combat != null)
            {
                combat.PlayerHealth.Changed -= OnHealthChanged;
                combat.EnemyHealth.Changed -= OnHealthChanged;
            }
            combat = null;
        }

        private void OnDestroy()
        {
            if (ownedDiceBinder != null) ownedDiceBinder.BlockingChanged -= RefreshMenuExecution;
            if (menu != null) menu.ExecutionStateRefreshed -= RefreshActionButton;
            if (sessionHost != null) sessionHost.TemporaryDiceStoryReady -= OnStoryReady;
            ReleaseCombat();
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            GameMenuPanel targetMenu,
            StoryTextPanel targetStory,
            TemporaryCombatConfiguration configuration = null,
            EnemyDisplayPanel targetEnemyPanel = null,
            HealthBarPanel targetPlayerHealth = null,
            HealthBarPanel targetEnemyHealth = null,
            TMP_Text targetNextAction = null)
        {
            menu = targetMenu;
            storyPanel = targetStory;
            combatConfiguration = configuration;
            enemyPanel = targetEnemyPanel;
            playerHealthBar = targetPlayerHealth;
            enemyHealthBar = targetEnemyHealth;
            nextEnemyActionText = targetNextAction;
        }

        public void SetRandomForTests(IRandomIndexSource source) => random = source;
        private float editorTimeoutOverrideSeconds;
        // Verification only: a positive value replaces the computed timeout; zero restores it.
        public void SetRollTimeoutOverrideForEditor(float seconds) => editorTimeoutOverrideSeconds = Mathf.Max(0f, seconds);
        public void ConfigureOwnedDiceForEditor(OwnedDiceSessionBinder binder) => ownedDiceBinder = binder;
        public void ConfigureActionButtonForEditor(BoundaryCommandButton button) => actionButton = button;
        public BoundaryCommandButton ActionButtonForEditor => actionButton;
#endif
    }
}
