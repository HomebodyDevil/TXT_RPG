using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TxTRPG.Application.Players;
using TxTRPG.Gameplay.Runes;
using TxTRPG.UI.Runes;
using UnityEngine;

namespace TxTRPG.Application.Runes
{
    // Candidate_MainScene: the rune tab of the development debug panel. Adds and removes test runes (the test definitions
    // of the session catalog, PlayerSessionHost.TemporaryTestRuneDefinitionIds) through the regular Inventory and Loadout
    // change APIs, so the rune grid, tooltips and the slot allocation receive them like any rune. A new rune stacks on the
    // line of the same definition or goes on a new line "line.test.<suffix>" at the end; its instance ID is
    // "<definition>.debug.<n>". Removing takes the newest debug instance. Regular runes and lines are never changed, the
    // configuration assets are never changed and nothing is saved. Without the debug panel (Release Player) it does
    // nothing.
    [DisallowMultipleComponent]
    public sealed class TemporaryTestRuneDebugController : MonoBehaviour
    {
        public const string NoSessionMessage = "세션이 없습니다. AppScene에서 Play를 시작하세요.";
        public const string DebugInstanceInfix = ".debug.";
        public const string TestLinePrefix = "line.test.";

        [SerializeField, Tooltip("Rune tab of EncounterDebugPanel. Kept while the panel is inactive in a Release Player.")]
        private RuneDebugTabView runeTab;

        private TemporaryRuneState runes;
        private IReadOnlyList<string> testIds = Array.Empty<string>();
        private int sequence;
        private bool listening;
        private bool subscribed;

        public RuneDebugTabView RuneTab => runeTab;
        public TemporaryRuneState Runes => runes;
        public IReadOnlyList<string> TestDefinitionIds => testIds;
        public bool IsConnected => runes != null;
        public string LastResultMessage { get; private set; } = string.Empty;

        public void Configure(RuneDebugTabView view) => runeTab = view;

        private void Start() => _ = ConnectAsync(PlayerSessionHost.Instance);

        private void OnEnable()
        {
            if (runes == null) return;
            Subscribe();
            Refresh();
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            StopListening();
        }

        public async Task ConnectAsync(PlayerSessionHost host)
        {
            if (host == null) { Connect(null, null); return; }
            try
            {
                await host.WhenReady;
            }
            catch (Exception exception)
            {
                if (this == null) return;
                Debug.LogWarning($"TemporaryTestRuneDebugController could not use the player session: {exception.Message}", this);
                Connect(null, null);
                return;
            }
            if (this == null) return;
            Connect(host.TemporaryRunes, host.TemporaryTestRuneDefinitionIds);
        }

        // Connects to a session rune state (null is the no-session state). Replaces any previous connection.
        public void Connect(TemporaryRuneState state, IReadOnlyList<string> testDefinitionIds)
        {
            Listen();
            Unsubscribe();
            runes = state;
            testIds = state != null && testDefinitionIds != null ? testDefinitionIds : Array.Empty<string>();
            if (runeTab != null) runeTab.SetSessionAvailable(runes != null);
            if (runes == null)
            {
                Refresh();
                ShowResult(NoSessionMessage);
                return;
            }
            if (isActiveAndEnabled) Subscribe();
            Refresh();
            ShowResult(testIds.Count == 0 ? RuneDebugTabView.NoDefinitionsMessage : string.Empty);
        }

        public int CountOf(string definitionId)
        {
            if (runes == null) return 0;
            var count = 0;
            foreach (var rune in runes.Inventory.Snapshot())
                if (string.Equals(rune.DefinitionId, definitionId, StringComparison.Ordinal)) count++;
            return count;
        }

        public bool TryAdd(string definitionId, out string message)
        {
            if (!CanChange(definitionId, out message)) return Finish(false, message, out message);
            var inventory = runes.Inventory;
            var loadout = runes.Loadout;
            string instanceId;
            do instanceId = $"{definitionId}{DebugInstanceInfix}{++sequence}"; while (inventory.Contains(instanceId));
            if (!inventory.TryAdd(new OwnedRune(instanceId, definitionId), out var reason)) return Finish(false, reason, out message);

            string lineId = null;
            foreach (var line in loadout.Lines)
                if (string.Equals(loadout.DefinitionIdOf(line), definitionId, StringComparison.Ordinal)) { lineId = line.LineId; break; }
            bool placed;
            if (lineId != null) placed = loadout.TryAddRuneToLine(lineId, instanceId, out reason);
            else
            {
                lineId = NewLineId(definitionId);
                placed = loadout.TryAddLine(new RuneLoadoutLine(lineId, new[] { instanceId }), out reason);
            }
            if (!placed)
            {
                // Undo the inventory change, so a failed add changes nothing.
                inventory.TryRemove(instanceId, out _);
                return Finish(false, reason, out message);
            }
            return Finish(true, $"{DisplayName(definitionId)} 추가 ({lineId}, {CountOf(definitionId)}개)", out message);
        }

        public bool TryRemove(string definitionId, out string message)
        {
            if (!CanChange(definitionId, out message)) return Finish(false, message, out message);
            if (!TryRemoveNewest(definitionId, out var reason)) return Finish(false, reason, out message);
            return Finish(true, $"{DisplayName(definitionId)} 제거 ({CountOf(definitionId)}개 남음)", out message);
        }

        // Removes every debug instance of every test definition. Regular runes are never touched.
        public bool TryRemoveAll(out string message)
        {
            if (runes == null) return Finish(false, NoSessionMessage, out message);
            var removed = 0;
            foreach (var id in testIds)
            {
                while (HasDebugInstance(id))
                {
                    if (!TryRemoveNewest(id, out var reason)) return Finish(false, reason, out message);
                    removed++;
                }
            }
            return Finish(true, $"테스트 룬 {removed}개 제거", out message);
        }

        private bool TryRemoveNewest(string definitionId, out string reason)
        {
            var inventory = runes.Inventory;
            var loadout = runes.Loadout;
            var snapshot = inventory.Snapshot();
            OwnedRune? newest = null;
            for (var i = snapshot.Count - 1; i >= 0; i--)
                if (IsDebugInstance(snapshot[i], definitionId)) { newest = snapshot[i]; break; }
            if (newest == null) { reason = $"{DisplayName(definitionId)}: 제거할 테스트 룬이 없습니다."; return false; }
            var instanceId = newest.Value.InstanceId;
            var lineIndex = -1;
            string lineId = null;
            if (loadout.TryFindLineOf(instanceId, out var foundLine))
            {
                lineId = foundLine;
                for (var i = 0; i < loadout.LineCount; i++) if (loadout.Lines[i].LineId == lineId) { lineIndex = i; break; }
                if (!loadout.TryRemoveRuneFromLine(lineId, instanceId, out reason)) return false;
            }
            if (inventory.TryRemove(instanceId, out reason)) return true;
            // Put the rune back where it was, so a failed removal changes nothing.
            if (lineId != null)
            {
                if (!loadout.TryAddRuneToLine(lineId, instanceId, out _) && lineIndex >= 0)
                    loadout.TryInsertLine(Mathf.Min(lineIndex, loadout.LineCount), new RuneLoadoutLine(lineId, new[] { instanceId }), out _);
            }
            return false;
        }

        private bool HasDebugInstance(string definitionId)
        {
            foreach (var rune in runes.Inventory.Snapshot()) if (IsDebugInstance(rune, definitionId)) return true;
            return false;
        }

        private static bool IsDebugInstance(OwnedRune rune, string definitionId) =>
            string.Equals(rune.DefinitionId, definitionId, StringComparison.Ordinal) &&
            rune.InstanceId.StartsWith(definitionId + DebugInstanceInfix, StringComparison.Ordinal);

        private string NewLineId(string definitionId)
        {
            var suffix = definitionId.StartsWith(TemporaryTestRuneConfiguration.IdPrefix, StringComparison.Ordinal)
                ? definitionId.Substring(TemporaryTestRuneConfiguration.IdPrefix.Length) : definitionId;
            var lineId = TestLinePrefix + suffix;
            var candidate = lineId;
            for (var n = 2; LineExists(candidate); n++) candidate = $"{lineId}.{n}";
            return candidate;
        }

        private bool LineExists(string lineId)
        {
            foreach (var line in runes.Loadout.Lines) if (line.LineId == lineId) return true;
            return false;
        }

        private bool CanChange(string definitionId, out string message)
        {
            if (runes == null) { message = NoSessionMessage; return false; }
            foreach (var id in testIds)
                if (string.Equals(id, definitionId, StringComparison.Ordinal)) { message = string.Empty; return true; }
            message = $"'{definitionId}'는 테스트 룬 정의가 아닙니다.";
            return false;
        }

        private string DisplayName(string definitionId) =>
            runes != null && runes.Catalog.TryGet(definitionId, out var definition) ? definition.DisplayName : definitionId;

        private bool Finish(bool success, string result, out string message)
        {
            message = result ?? string.Empty;
            ShowResult(message);
            Refresh();
            return success;
        }

        private void Refresh()
        {
            if (this == null) { Unsubscribe(); return; }
            if (runeTab == null) return;
            var entries = new RuneDebugEntryDisplay[runes != null ? testIds.Count : 0];
            for (var i = 0; i < entries.Length; i++)
                entries[i] = new RuneDebugEntryDisplay(testIds[i], DisplayName(testIds[i]), CountOf(testIds[i]));
            runeTab.SetEntries(entries);
        }

        private void ShowResult(string message)
        {
            LastResultMessage = message ?? string.Empty;
            if (runeTab != null) runeTab.ShowResult(LastResultMessage);
        }

        private void Listen()
        {
            if (listening || runeTab == null) return;
            listening = true;
            runeTab.AddRequested += OnAddRequested;
            runeTab.RemoveRequested += OnRemoveRequested;
            runeTab.RemoveAllRequested += OnRemoveAllRequested;
        }

        private void StopListening()
        {
            if (!listening || runeTab == null) return;
            runeTab.AddRequested -= OnAddRequested;
            runeTab.RemoveRequested -= OnRemoveRequested;
            runeTab.RemoveAllRequested -= OnRemoveAllRequested;
            listening = false;
        }

        private void OnAddRequested(string id) => TryAdd(id, out _);
        private void OnRemoveRequested(string id) => TryRemove(id, out _);
        private void OnRemoveAllRequested() => TryRemoveAll(out _);

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
    }
}
