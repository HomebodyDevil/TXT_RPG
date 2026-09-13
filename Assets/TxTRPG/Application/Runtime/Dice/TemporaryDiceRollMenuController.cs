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
using TxTRPG.UI.Windows;
using UnityEngine;

namespace TxTRPG.Application.Dice
{
    [DisallowMultipleComponent]
    public sealed class TemporaryDiceRollMenuController : MonoBehaviour, IGameMenuCommandHandler, ISceneInitializer
    {
        public const string RollAllCommandId = "temporary-dice.roll-all";

        [SerializeField] private GameMenuPanel menu;
        [SerializeField] private StoryTextPanel storyPanel;
        [SerializeField] private TemporaryCombatConfiguration combatConfiguration;
        [SerializeField] private EnemyDisplayPanel enemyPanel;
        [SerializeField] private HealthBarPanel playerHealthBar;
        [SerializeField] private HealthBarPanel enemyHealthBar;
        [SerializeField] private TMP_Text nextEnemyActionText;

        private PlayerSessionHost sessionHost;
        private TemporaryCombatState combat;
        private bool initialized;
        private bool executing;
        private IRandomIndexSource random;

        public int InitializationOrder => -800;
        public bool IsReady => initialized && combat != null && sessionHost?.TemporaryDice != null;
        public TemporaryCombatState Combat => combat;
        public event Action<bool> CombatFinished;

        public async Task InitializeAsync(SceneInitializationContext context, CancellationToken cancellationToken)
        {
            ReleaseCombat();
            initialized = false;
            sessionHost = PlayerSessionHost.Instance;
            if (sessionHost == null) throw new InvalidOperationException("Temporary combat requires PlayerSessionHost.");
            await sessionHost.EnsureInitializedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (combatConfiguration == null || !combatConfiguration.EnabledForScene)
            {
                RefreshPresentation();
                menu.RefreshExecutionState();
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
            SubscribeCombat();
            ApplyEnemyPresentation();
            RefreshPresentation();
            menu.RefreshExecutionState();
        }

        public void ClearCombat()
        {
            ReleaseCombat();
            sessionHost?.SetTemporaryExplorationCombat(null);
            enemyPanel?.SetEnemies(Array.Empty<EnemyPresentation>());
            RefreshPresentation();
            menu?.RefreshExecutionState();
        }
        public bool CanExecute(string commandId, out string unavailableReason)
        {
            if (!string.Equals(commandId?.Trim(), RollAllCommandId, StringComparison.Ordinal))
            { unavailableReason = "Unknown game-menu command."; return false; }
            if (executing) { unavailableReason = "The temporary combat action is already being processed."; return false; }
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
            executing = true;
            menu.RefreshExecutionState();
            try
            {
                var rolls = sessionHost.TemporaryDice.RollAll(random);
                var diceResults = new List<DiceRollResult>(rolls.Count);
                for (var i = 0; i < rolls.Count; i++) diceResults.Add(rolls[i].Result);
                var result = combat.ExecuteTurn(diceResults);
                RecordTurn(rolls, result);
                ApplyEnemyPresentation();
                RefreshPresentation();
                if (combat.IsComplete) CombatFinished?.Invoke(combat.EnemyHealth.IsDefeated);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Temporary combat action failed after any already-applied changes: {exception.Message}", this);
                storyPanel.AddMessage("[전투] 행동 처리 중 오류가 발생했습니다. 이미 적용된 결과는 다시 실행하지 않습니다.");
                return false;
            }
            finally
            {
                executing = false;
                menu.RefreshExecutionState();
            }
        }

        private void RecordTurn(IReadOnlyList<PlayerDieRoll> rolls, TemporaryCombatTurnResult turn)
        {
            var eventIndex = 0;
            for (var i = 0; i < rolls.Count; i++, eventIndex++)
            {
                var roll = rolls[i];
                var combatEvent = turn.Events[eventIndex];
                storyPanel.AddMessage(DiceRollStoryFormatter.Format(roll));
                switch (combatEvent.Kind)
                {
                    case TemporaryCombatEventKind.PlayerAttack:
                        storyPanel.AddMessage($"[전투] {combatConfiguration.EnemyName}에게 피해 {combatEvent.AppliedAmount}. 체력 {combatEvent.CurrentHealth}/{combatEvent.MaximumHealth}.");
                        break;
                    case TemporaryCombatEventKind.PlayerHeal:
                        storyPanel.AddMessage($"[전투] 플레이어 회복 요청 {combatEvent.RequestedAmount}, 실제 회복 {combatEvent.AppliedAmount}. 체력 {combatEvent.CurrentHealth}/{combatEvent.MaximumHealth}.");
                        break;
                    case TemporaryCombatEventKind.PlayerAttackSkipped:
                        storyPanel.AddMessage($"[전투] {combatConfiguration.EnemyName}이 이미 쓰러져 공격 {combatEvent.RequestedAmount}을 건너뛰었습니다.");
                        break;
                }
            }

            for (; eventIndex < turn.Events.Count; eventIndex++)
            {
                var combatEvent = turn.Events[eventIndex];
                switch (combatEvent.Kind)
                {
                    case TemporaryCombatEventKind.EnemyAttack:
                        storyPanel.AddMessage($"[적 행동] 공격 {combatEvent.RequestedAmount}. 플레이어가 받은 피해 {combatEvent.AppliedAmount}. 체력 {combatEvent.CurrentHealth}/{combatEvent.MaximumHealth}.");
                        break;
                    case TemporaryCombatEventKind.EnemyHeal:
                        storyPanel.AddMessage($"[적 행동] 회복 요청 {combatEvent.RequestedAmount}, 실제 회복 {combatEvent.AppliedAmount}. 체력 {combatEvent.CurrentHealth}/{combatEvent.MaximumHealth}.");
                        break;
                    case TemporaryCombatEventKind.Victory:
                        storyPanel.AddMessage($"[전투] {combatConfiguration.EnemyName}을 쓰러뜨렸습니다. 임시 전투가 종료되었습니다.");
                        break;
                    case TemporaryCombatEventKind.Defeat:
                        storyPanel.AddMessage("[전투] 플레이어가 전투 불능이 되었습니다. 임시 전투가 종료되었습니다.");
                        break;
                }
            }
        }

        private void AttachCombat(TemporaryCombatState existingCombat)
        {
            ReleaseCombat();
            combat = existingCombat;
            SubscribeCombat();
            ApplyEnemyPresentation();
            RefreshPresentation();
            menu?.RefreshExecutionState();
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

        private void OnDestroy() => ReleaseCombat();

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
#endif
    }
}
