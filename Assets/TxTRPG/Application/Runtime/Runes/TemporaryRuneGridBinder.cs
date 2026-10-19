using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TxTRPG.Application.Players;
using TxTRPG.UI.Runes;
using UnityEngine;

namespace TxTRPG.Application.Runes
{
    // Shows the session rune lines (PlayerSessionHost.TemporaryRunes) in a RuneGridPanel: one card per line with its weight
    // as the badge. Rebuilds the entries only when TemporaryRuneState.Changed fires; never changes the rune state. A missing
    // host, a failed session or missing rune state shows a failure message and never blocks the game.
    [DisallowMultipleComponent]
    public sealed class TemporaryRuneGridBinder : MonoBehaviour
    {
        public const string EmptyMessage = "장착된 룬이 없습니다";
        public const string UnavailableMessage = "룬 정보를 불러올 수 없습니다";
        public const string UnknownRuneName = "알 수 없는 룬";

        [SerializeField] private RuneGridPanel panel;

        private TemporaryRuneState runes;
        private bool subscribed;
        private bool reportedMissingPanel;

        public RuneGridPanel Panel => panel;
        public TemporaryRuneState Runes => runes;

        public void Configure(RuneGridPanel target) => panel = target;

        private void Start() => _ = ConnectAsync(PlayerSessionHost.Instance);

        private void OnEnable()
        {
            if (runes == null) return;
            Subscribe();
            Refresh();
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        public async Task ConnectAsync(PlayerSessionHost host)
        {
            if (!HasPanel()) return;
            if (host == null)
            {
                Debug.LogWarning("TemporaryRuneGridBinder found no PlayerSessionHost; start Play from AppScene.", this);
                Connect(null);
                return;
            }
            try
            {
                await host.WhenReady;
            }
            catch (Exception exception)
            {
                if (this == null) return;
                Debug.LogWarning($"TemporaryRuneGridBinder could not use the player session: {exception.Message}", this);
                Connect(null);
                return;
            }
            // The binder may have been destroyed (scene unloaded) while waiting.
            if (this == null) return;
            if (host.TemporaryRunes == null)
                Debug.LogWarning($"TemporaryRuneGridBinder has no rune state. {host.TemporarySlotCombatError}", this);
            Connect(host.TemporaryRunes);
        }

        // Connects to a rune state (null shows the failure message). Replaces any previous subscription.
        public void Connect(TemporaryRuneState state)
        {
            if (!HasPanel()) return;
            Unsubscribe();
            runes = state;
            if (runes == null)
            {
                panel.ShowMessage(UnavailableMessage);
                return;
            }
            if (enabled && gameObject.activeInHierarchy) Subscribe();
            Refresh();
        }

        public static RuneGridEntryDisplay[] CreateEntries(TemporaryRuneState runes)
        {
            if (runes == null) return Array.Empty<RuneGridEntryDisplay>();
            var lines = runes.Loadout.Lines;
            var result = new RuneGridEntryDisplay[lines.Count];
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var definitionId = runes.Loadout.DefinitionIdOf(line);
                result[i] = runes.Catalog.TryGet(definitionId, out var definition)
                    ? new RuneGridEntryDisplay(line.LineId, definition.Id, definition.DisplayName, line.Weight)
                    : new RuneGridEntryDisplay(line.LineId, string.Empty, UnknownRuneName, line.Weight);
            }
            return result;
        }

        private void Refresh()
        {
            if (runes == null || !HasPanel()) return;
            IReadOnlyList<RuneGridEntryDisplay> entries = CreateEntries(runes);
            if (entries.Count == 0) panel.ShowMessage(EmptyMessage);
            else panel.SetEntries(entries);
        }

        private void Subscribe()
        {
            if (subscribed || runes == null) return;
            runes.Changed += Refresh;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (runes != null) runes.Changed -= Refresh;
            subscribed = false;
        }

        private bool HasPanel()
        {
            if (panel != null) return true;
            if (!reportedMissingPanel)
            {
                reportedMissingPanel = true;
                Debug.LogError("TemporaryRuneGridBinder has no RuneGridPanel assigned.", this);
            }
            return false;
        }
    }
}
