using System;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Application.Players;
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
        private PlayerSessionHost sessionHost;
        private bool initialized;
        private IRandomIndexSource random;
        public int InitializationOrder => -800;
        public bool IsReady => initialized && sessionHost != null && sessionHost.TemporaryDice != null;

        public async Task InitializeAsync(SceneInitializationContext context, CancellationToken cancellationToken)
        {
            initialized = false;
            sessionHost = PlayerSessionHost.Instance;
            if (sessionHost == null) throw new InvalidOperationException("Temporary dice command requires PlayerSessionHost.");
            await sessionHost.EnsureInitializedAsync(cancellationToken);
            random = new SystemRandomIndexSource();
            initialized = true;
            menu.RefreshExecutionState();
        }

        public bool CanExecute(string commandId, out string unavailableReason)
        {
            if (!string.Equals(commandId?.Trim(), RollAllCommandId, StringComparison.Ordinal))
            { unavailableReason = "Unknown game-menu command."; return false; }
            if (!IsReady) { unavailableReason = "Temporary dice session is not ready."; return false; }
            if (storyPanel == null || !storyPanel.isActiveAndEnabled)
            { unavailableReason = "Story output is not ready."; return false; }
            if (sessionHost.TemporaryDice.Count == 0)
            { unavailableReason = "No temporary dice are owned."; return false; }
            unavailableReason = string.Empty; return true;
        }

        public bool TryExecute(string commandId)
        {
            if (!CanExecute(commandId, out var reason)) { Debug.LogWarning(reason, this); return false; }
            try
            {
                var rolls = sessionHost.TemporaryDice.RollAll(random);
                foreach (var roll in rolls)
                    storyPanel.AddMessage($"[주사위] {roll.Order}번 주사위 ({roll.DisplayName}): {roll.Result.Value}");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Temporary dice roll failed: {exception.Message}", this);
                return false;
            }
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(GameMenuPanel targetMenu, StoryTextPanel targetStory)
        { menu = targetMenu; storyPanel = targetStory; }
        public void SetRandomForTests(IRandomIndexSource source) => random = source;
#endif
    }
}