using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Combat;
using TxTRPG.Application.Players;
using TxTRPG.Application.Runes;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Runes;
using TxTRPG.Gameplay.SlotMachine;
using TxTRPG.SceneTransition;
using TxTRPG.UI;
using TxTRPG.UI.SlotMachine;
using TxTRPG.UI.Windows;
using UnityEngine;

namespace TxTRPG.Application.SlotMachine
{
    // The combat action of TMP_MainScene: the boundary action button spins the slot machine once, the payline total is
    // allocated to the rune lines by weight, every line acts once, then the enemy acts. The steps are separate
    // (accept spin -> allocate -> present -> apply) so a future manual allocation can wait between them.
    [DisallowMultipleComponent]
    public sealed class TemporarySlotCombatController : MonoBehaviour, IGameMenuCommandHandler, ISceneInitializer, ITemporaryCombatController
    {
        public const string SpinCommandId = "temporary-slot.spin";

        [SerializeField] private GameMenuPanel menu;
        [SerializeField, Tooltip("The combat action button on the EnemyDisplayPanel/SlotMachinePanel boundary.")]
        private BoundaryCommandButton actionButton;
        [SerializeField] private StoryTextPanel storyPanel;
        [SerializeField] private TemporaryCombatConfiguration combatConfiguration;
        [SerializeField] private EnemyDisplayPanel enemyPanel;
        [SerializeField] private HealthBarPanel playerHealthBar;
        [SerializeField] private HealthBarPanel enemyHealthBar;
        [SerializeField] private TMP_Text nextEnemyActionText;
        [SerializeField] private SlotMachinePanel slotPanel;
        [SerializeField, Range(1f, 15f), Tooltip("Minimum wait for the spin presentation before the result is shown directly.")]
        private float spinTimeoutSeconds = 3f;
        [SerializeField, Range(0f, 5f), Tooltip("Added to the panel's longest presentation to form the actual timeout.")]
        private float spinTimeoutMarginSeconds = 1f;

        private PlayerSessionHost sessionHost;
        private TemporarySlotMachineState boundSlotMachine;
        private TemporaryRuneState boundRunes;
        private TemporaryCombatState combat;
        private TemporarySlotAcceptedAction presenting;
        // Run and node of the result the panel shows at rest; null while it shows the idle state.
        private string shownRunId;
        private string shownNodeId;
        private bool initialized;
        private bool executing;
        private IRandomIndexProvider random;
        // Separate from the spin random source, so Fortune Critical never changes the spin draws.
        private IRandomIndexProvider fortuneRandom;

        public int InitializationOrder => -800;
        public bool IsReady => initialized && combat != null && sessionHost != null &&
            sessionHost.TemporarySlotMachine != null && sessionHost.TemporaryRunes != null;
        public TemporaryCombatState Combat => combat;
        public SlotMachinePanel Panel => slotPanel;
        public event Action<bool> CombatFinished;

        public async Task InitializeAsync(SceneInitializationContext context, CancellationToken cancellationToken)
        {
            Unbind();
            ReleaseCombat();
            initialized = false;
            if (!isActiveAndEnabled) return;
            RefreshActionButton();
            sessionHost = PlayerSessionHost.Instance;
            if (sessionHost == null) throw new InvalidOperationException("Temporary slot combat requires PlayerSessionHost.");
            await sessionHost.EnsureInitializedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            sessionHost.TemporarySlotStoryReady += OnStoryReady;
            sessionHost.TemporaryCombatActionEnded += OnCombatActionEnded;
            if (menu != null) menu.ExecutionStateRefreshed += RefreshActionButton;
            boundSlotMachine = sessionHost.TemporarySlotMachine;
            boundRunes = sessionHost.TemporaryRunes;
            if (boundSlotMachine != null) boundSlotMachine.Changed += RefreshSlotPanel;
            if (boundRunes != null) boundRunes.Changed += RefreshSlotPanel;
            RefreshSlotPanel();
            RestorePanelResult();
            if (sessionHost.PendingSlotStory != null) OnStoryReady(sessionHost.PendingSlotStory);

            if (combatConfiguration == null || !combatConfiguration.EnabledForScene)
            {
                RefreshPresentation();
                RefreshMenuExecution();
                return;
            }

            random ??= new SystemRandomIndexProvider();
            // An explicit seed: two System.Random instances created in the same tick could share a seed, which would
            // correlate the Fortune Critical draws with the reel stops.
            fortuneRandom ??= new SystemRandomIndexProvider(Guid.NewGuid().GetHashCode());
            initialized = true;
            if (sessionHost.TemporaryExplorationCombat != null) AttachCombat(sessionHost.TemporaryExplorationCombat);
            else ClearCombat();
        }

        public void BeginCombat(HealthState playerHealth)
        {
            if (!initialized) throw new InvalidOperationException("Temporary slot combat controller is not initialized.");
            if (playerHealth == null) throw new ArgumentNullException(nameof(playerHealth));
            ReleaseCombat();
            combat = new TemporaryCombatState(playerHealth, combatConfiguration.EnemyMaximumHealth,
                combatConfiguration.EnemyAttackAmount, combatConfiguration.EnemyHealAmount);
            sessionHost.SetTemporaryExplorationCombat(combat);
            SubscribeCombat();
            ApplyEnemyPresentation();
            RefreshPresentation();
            RefreshSlotPanel();
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
            if (!string.Equals(commandId?.Trim(), SpinCommandId, StringComparison.Ordinal))
            { unavailableReason = "Unknown game-menu command."; return false; }
            if (executing) { unavailableReason = "The temporary combat action is already being processed."; return false; }
            if (sessionHost != null && sessionHost.IsTemporaryCombatActionPending)
            { unavailableReason = "A temporary combat action is still being completed."; return false; }
            if (menu != null && menu.WindowService != null && menu.WindowService.BlocksGameplayInput)
            { unavailableReason = "A game window is open."; return false; }
            if (!IsReady) { unavailableReason = "Temporary slot combat is not ready."; return false; }
            if (combat.IsComplete) { unavailableReason = "The temporary combat is complete."; return false; }
            if (storyPanel == null || !storyPanel.isActiveAndEnabled)
            { unavailableReason = "Story output is not ready."; return false; }
            if (sessionHost.TemporarySlotMachine.ReelCount == 0)
            { unavailableReason = "The slot machine has no reels."; return false; }
            if (!sessionHost.TemporaryRunes.CanAllocate)
            { unavailableReason = "No rune line is equipped."; return false; }
            unavailableReason = string.Empty;
            return true;
        }

        public bool TryExecute(string commandId)
        {
            if (!CanExecute(commandId, out var reason)) { Debug.LogWarning(reason, this); return false; }
            if (!sessionHost.TryBeginTemporaryCombatAction(combat, out var actionId)) return false;
            executing = true;
            RefreshMenuExecution();
            try
            {
                var accepted = AcceptSpin(actionId);
                _ = CompleteAcceptedActionAsync(sessionHost, accepted);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Temporary slot action could not be accepted: {exception.Message}", this);
                sessionHost.EndTemporaryCombatAction(actionId);
                executing = false;
                RefreshMenuExecution();
                return false;
            }
        }

        // Step 1-2: the result is decided here, once (spin -> Fortune Critical -> allocation of the final total). From now
        // on it cannot be spun again. An overflowing final total throws and the action is not accepted.
        private TemporarySlotAcceptedAction AcceptSpin(long actionId)
        {
            var spin = sessionHost.TemporarySlotMachine.Spin(random);
            var rules = sessionHost.TemporaryFortuneCritical?.Rules ?? FortuneCriticalRules.Disabled;
            var fortune = FortuneCriticalResolver.Resolve(spin, rules, fortuneRandom);
            var allocation = sessionHost.TemporaryRunes.Allocate(fortune.FinalTotal);
            var accepted = new TemporarySlotAcceptedAction(actionId,
                sessionHost.TemporaryExploration?.RunId ?? string.Empty,
                sessionHost.TemporaryExploration?.ActiveNodeId ?? string.Empty,
                combat, spin, fortune, allocation);
            sessionHost.AcceptTemporarySlotAction(accepted);
            MarkResultShown(accepted.RunId, accepted.NodeId);
            return accepted;
        }

        private async Task CompleteAcceptedActionAsync(PlayerSessionHost acceptedHost, TemporarySlotAcceptedAction accepted)
        {
            try
            {
                await PresentAsync(acceptedHost, accepted);
                if (PlayerSessionHost.Instance != acceptedHost ||
                    acceptedHost.TemporaryExplorationCombat != accepted.Combat ||
                    (acceptedHost.TemporaryExploration?.RunId ?? string.Empty) != accepted.RunId ||
                    (acceptedHost.TemporaryExploration?.ActiveNodeId ?? string.Empty) != accepted.NodeId)
                {
                    Debug.LogWarning("An accepted slot spin belongs to a previous session or combat and was not applied.");
                    return;
                }
                if (accepted.Applied) return;
                var actions = RuneCombatActions.FromAllocation(accepted.Allocation);
                // ExecuteTurn is deliberately invoked once; a partial failure is reported, never retried.
                accepted.MarkApplied();
                var result = accepted.Combat.ExecuteTurn(actions);
                var record = new TemporarySlotTurnRecord(accepted.RunId, accepted.NodeId, accepted.Spin, accepted.Fortune, accepted.Allocation, result);
                try { acceptedHost.PublishTemporarySlotStory(record); }
                catch (Exception exception) { Debug.LogError($"Combat Story display failed: {exception}"); }
                if (this != null)
                {
                    try { ApplyEnemyPresentation(); RefreshPresentation(); }
                    catch (Exception exception) { Debug.LogWarning($"Combat UI refresh failed: {exception.Message}"); }
                }
                if (accepted.Combat.IsComplete)
                {
                    try
                    {
                        acceptedHost.NotifyTemporaryCombatCompleted(accepted.Combat, accepted.Combat.EnemyHealth.IsDefeated);
                        if (this != null) CombatFinished?.Invoke(accepted.Combat.EnemyHealth.IsDefeated);
                    }
                    catch (Exception exception) { Debug.LogError($"Combat completion notification failed: {exception}"); }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Temporary slot action failed after any already-applied changes: {exception}");
                if (this != null && storyPanel != null) storyPanel.AddMessage(TemporaryCombatStoryText.ActionFailed);
            }
            finally
            {
                acceptedHost.ClearPendingSlotAction(accepted);
                if (this != null) { executing = false; presenting = null; }
                acceptedHost.EndTemporaryCombatAction(accepted.ActionId);
                if (this != null) RefreshMenuExecution();
            }
        }

        // Step 3: the presentation never changes the result. Without a usable panel, or after the timeout, the reels
        // are put at rest on the accepted result and the result is applied anyway.
        private async Task PresentAsync(PlayerSessionHost acceptedHost, TemporarySlotAcceptedAction accepted)
        {
            var panel = slotPanel;
            if (panel == null || !panel.isActiveAndEnabled) return;
            var display = CreateSpinDisplay(accepted);
            presenting = accepted;
            using var cancellation = new CancellationTokenSource();
            try
            {
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
                    Debug.LogWarning($"SLOT_SPIN_TIMEOUT: presentation exceeded {timeout:F2}s; the reels were stopped at the accepted result.");
                    return;
                }
                if (!await animation && panel != null) panel.StopAtResult(display);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Slot spin animation failed; applying the accepted result: {exception.Message}");
                if (panel != null) panel.StopAtResult(display);
            }
        }

        private static async Task ObserveLateAnimationAsync(Task task)
        { try { await task; } catch (Exception exception) { Debug.LogWarning($"Late slot animation failed: {exception.Message}"); } }

        private static SlotSpinDisplay CreateSpinDisplay(TemporarySlotAcceptedAction accepted) =>
            CreateSpinDisplay(accepted.Spin, accepted.Fortune, accepted.Allocation);

        public static SlotSpinDisplay CreateSpinDisplay(SlotSpinResult spin, RuneAllocation allocation) =>
            CreateSpinDisplay(spin, FortuneCriticalResult.None(spin), allocation);

        // Each reel shows its own Fortune Critical multiplier; the total box shows the final total and the total multiplier.
        public static SlotSpinDisplay CreateSpinDisplay(SlotSpinResult spin, FortuneCriticalResult fortune, RuneAllocation allocation)
        {
            if (fortune == null) throw new ArgumentNullException(nameof(fortune));
            var reels = new SlotReelStopDisplay[spin.Reels.Count];
            for (var i = 0; i < reels.Length; i++)
            {
                var stop = spin.Reels[i];
                var values = new int[stop.Strip.Count];
                for (var j = 0; j < values.Length; j++) values[j] = stop.Strip[j].Value;
                var multiplier = i < fortune.Reels.Count ? fortune.Reels[i].ReelMultiplier : 1;
                reels[i] = new SlotReelStopDisplay(stop.ReelId, stop.StopIndex, values, SlotCombatStoryFormatter.MultiplierText(multiplier));
            }
            var spinning = new SlotRuneLineDisplay[allocation.Lines.Count];
            var result = new SlotRuneLineDisplay[allocation.Lines.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var line = allocation.Lines[i];
                var effect = SlotCombatStoryFormatter.EffectName(line.EffectKind);
                spinning[i] = new SlotRuneLineDisplay(effect, SlotCombatStoryFormatter.LineWeight(line.Weight));
                // Lines that received 0 are shown too ("×1 → 0").
                result[i] = new SlotRuneLineDisplay(effect, SlotCombatStoryFormatter.LineAllocation(line.Weight, line.Amount));
            }
            return new SlotSpinDisplay(reels, spin.PaylineTotal,
                CreateSummary(SlotCombatStoryFormatter.SpinningTotalValue, spinning),
                CreateSummary(SlotCombatStoryFormatter.TotalValue(fortune.FinalTotal), result,
                    SlotCombatStoryFormatter.MultiplierText(fortune.TotalMultiplier)));
        }

        public static SlotSummaryDisplay CreateSummary(string totalValue, IReadOnlyList<SlotRuneLineDisplay> lines) =>
            CreateSummary(totalValue, lines, string.Empty);

        public static SlotSummaryDisplay CreateSummary(string totalValue, IReadOnlyList<SlotRuneLineDisplay> lines, string totalMultiplier) =>
            new(SlotCombatStoryFormatter.TotalLabel, totalValue, SlotCombatStoryFormatter.RuneColumnTitle,
                SlotCombatStoryFormatter.ValueColumnTitle, lines, SlotCombatStoryFormatter.NoRuneLines,
                SlotCombatStoryFormatter.OverflowFormat, totalMultiplier);

        // Shows the reel configuration and the rune weights. While this controller presents a spin, the panel defers the
        // reel change until the presentation ends.
        private void RefreshSlotPanel()
        {
            if (this == null) { Unbind(); return; }
            if (slotPanel == null || sessionHost == null) return;
            var slots = sessionHost.TemporarySlotMachine;
            var runes = sessionHost.TemporaryRunes;
            if (slots == null || runes == null)
            {
                slotPanel.ShowError(string.IsNullOrEmpty(sessionHost.TemporarySlotCombatError)
                    ? "슬롯머신을 준비하지 못했습니다." : sessionHost.TemporarySlotCombatError);
                return;
            }
            slotPanel.ClearError();
            var reels = new SlotReelDisplay[slots.Machine.ReelCount];
            for (var i = 0; i < reels.Length; i++)
            {
                var reel = slots.Machine.Reels[i];
                var values = new int[reel.Count];
                for (var j = 0; j < values.Length; j++) values[j] = reel[j].Value;
                reels[i] = new SlotReelDisplay(reel.Id, values);
            }
            slotPanel.SetReels(reels);
            if (presenting != null || slotPanel.IsSpinning) return;
            if (shownRunId != null && IsCurrentNode(shownRunId, shownNodeId)) return;
            shownRunId = shownNodeId = null;
            slotPanel.SetSummary(CreateSummary(SlotCombatStoryFormatter.IdleTotalValue, WeightLines(runes)));
        }

        // A controller created by a Scene reload shows the accepted spin (still being applied) or the last applied turn of
        // the current combat node, at rest. It never spins or applies it again.
        private void RestorePanelResult()
        {
            if (slotPanel == null || sessionHost == null) return;
            var pending = sessionHost.PendingSlotAction;
            if (pending != null)
            {
                slotPanel.StopAtResult(CreateSpinDisplay(pending));
                MarkResultShown(pending.RunId, pending.NodeId);
                return;
            }
            var last = sessionHost.LastTemporarySlotTurn;
            if (last != null && IsCurrentNode(last.RunId, last.NodeId))
            {
                slotPanel.StopAtResult(CreateSpinDisplay(last.Spin, last.Fortune, last.Allocation));
                MarkResultShown(last.RunId, last.NodeId);
            }
        }

        private void MarkResultShown(string runId, string nodeId)
        {
            shownRunId = runId ?? string.Empty;
            shownNodeId = nodeId ?? string.Empty;
        }

        private bool IsCurrentNode(string runId, string nodeId) =>
            sessionHost != null && sessionHost.TemporaryExploration != null &&
            sessionHost.TemporaryExploration.RunId == runId &&
            sessionHost.TemporaryExploration.ActiveNodeId == nodeId;

        // Rune line rows before a spin: effect name and stacked weight ("×1").
        private static SlotRuneLineDisplay[] WeightLines(TemporaryRuneState runes)
        {
            var lines = new SlotRuneLineDisplay[runes.Loadout.LineCount];
            var index = 0;
            foreach (var line in runes.Loadout.Lines)
            {
                var effect = runes.Catalog.TryGet(runes.Loadout.DefinitionIdOf(line), out var definition)
                    ? definition.EffectKind : RuneEffectKind.Unknown;
                lines[index++] = new SlotRuneLineDisplay(SlotCombatStoryFormatter.EffectName(effect),
                    SlotCombatStoryFormatter.LineWeight(line.Weight));
            }
            return lines;
        }

        // A spin applied by a controller of the previous Scene finished: refresh this Scene's presentation and button.
        private void OnCombatActionEnded()
        {
            if (this == null) { Unbind(); return; }
            if (!executing)
            {
                var last = sessionHost?.LastTemporarySlotTurn;
                if (slotPanel != null && last != null && !slotPanel.IsSpinning && IsCurrentNode(last.RunId, last.NodeId))
                {
                    slotPanel.StopAtResult(CreateSpinDisplay(last.Spin, last.Fortune, last.Allocation));
                    MarkResultShown(last.RunId, last.NodeId);
                }
                ApplyEnemyPresentation();
                RefreshPresentation();
            }
            RefreshMenuExecution();
        }

        // Session events can outlive a destroyed controller when OnDestroy did not run; such a controller ignores them.
        private void OnStoryReady(TemporarySlotTurnRecord record)
        {
            if (this == null) { Unbind(); return; }
            if (storyPanel == null || !storyPanel.isActiveAndEnabled) return;
            var enemyName = combatConfiguration != null ? combatConfiguration.EnemyName : "적";
            foreach (var line in SlotCombatStoryFormatter.Turn(record, enemyName)) storyPanel.AddMessage(line);
            sessionHost?.ClearPendingSlotStory(record);
        }

        // The menu raises ExecutionStateRefreshed (also when game windows open or close), which refreshes the action button.
        private void RefreshMenuExecution()
        {
            if (menu != null) menu.RefreshExecutionState();
            else RefreshActionButton();
        }

        private void RefreshActionButton()
        {
            if (this == null || actionButton == null) return;
            actionButton.SetAvailable(IsReady && !combat.IsComplete);
        }

        private void AttachCombat(TemporaryCombatState existingCombat)
        {
            ReleaseCombat();
            combat = existingCombat;
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
            if (enemyPanel == null || combat == null || combatConfiguration == null) return;
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

        private void Unbind()
        {
            if (sessionHost != null)
            {
                sessionHost.TemporarySlotStoryReady -= OnStoryReady;
                sessionHost.TemporaryCombatActionEnded -= OnCombatActionEnded;
            }
            if (boundSlotMachine != null) boundSlotMachine.Changed -= RefreshSlotPanel;
            if (boundRunes != null) boundRunes.Changed -= RefreshSlotPanel;
            if (menu != null) menu.ExecutionStateRefreshed -= RefreshActionButton;
            boundSlotMachine = null;
            boundRunes = null;
            sessionHost = null;
        }

        private void OnDestroy()
        {
            Unbind();
            ReleaseCombat();
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            GameMenuPanel targetMenu,
            BoundaryCommandButton targetButton,
            StoryTextPanel targetStory,
            TemporaryCombatConfiguration configuration,
            EnemyDisplayPanel targetEnemyPanel,
            HealthBarPanel targetPlayerHealth,
            HealthBarPanel targetEnemyHealth,
            TMP_Text targetNextAction,
            SlotMachinePanel targetSlotPanel)
        {
            menu = targetMenu;
            actionButton = targetButton;
            storyPanel = targetStory;
            combatConfiguration = configuration;
            enemyPanel = targetEnemyPanel;
            playerHealthBar = targetPlayerHealth;
            enemyHealthBar = targetEnemyHealth;
            nextEnemyActionText = targetNextAction;
            slotPanel = targetSlotPanel;
        }

        public void SetRandomForTests(IRandomIndexProvider source) => random = source;
        public void SetFortuneRandomForTests(IRandomIndexProvider source) => fortuneRandom = source;
        private float editorTimeoutOverrideSeconds;
        // Verification only: a positive value replaces the computed timeout; zero restores it.
        public void SetSpinTimeoutOverrideForEditor(float seconds) => editorTimeoutOverrideSeconds = Mathf.Max(0f, seconds);
        public BoundaryCommandButton ActionButtonForEditor => actionButton;
        public GameMenuPanel MenuForEditor => menu;
        public StoryTextPanel StoryForEditor => storyPanel;
        public TemporaryCombatConfiguration CombatConfigurationForEditor => combatConfiguration;
#endif
    }
}
