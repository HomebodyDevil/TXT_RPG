using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Players;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.SlotMachine;
using TxTRPG.UI.SlotMachine;
using UnityEngine;

namespace TxTRPG.Application.SlotMachine
{
    // Candidate_MainScene: shows the session slot machine (PlayerSessionHost.TemporarySlotMachine) on the bottom SlotReelDock
    // and handles the slot tab of the development debug panel. Reel count and Fortune Critical chance changes go to the
    // session state through its Try methods (combat and exploration slot checks of the same session use them too); the
    // configuration assets are never changed and nothing is saved. A debug spin resolves Fortune Critical for real and
    // shows the result on the dock and the tab only: it never reaches runes, combat, Story or records.
    // Without the debug panel (Release Player) it only keeps the dock in sync with the session reels.
    [DisallowMultipleComponent]
    public sealed class SlotReelDockDebugController : MonoBehaviour
    {
        public const string NoSessionMessage = "세션이 없습니다. AppScene에서 Play를 시작하세요.";
        public const string ResetDoneMessage = "기본값으로 되돌렸습니다.";
        public const int DefaultMaximumReelCount = 10;
        public const string ReelIdPrefix = "reel.";

        [SerializeField] private SlotReelDock dock;
        [SerializeField, Tooltip("Slot tab of EncounterDebugPanel. Kept while the panel is inactive in a Release Player.")]
        private SlotDebugTabView slotTab;
        [SerializeField, Tooltip("Read by 기본값으로 only; never changed.")]
        private TemporarySlotMachineConfiguration slotMachineConfiguration;
        [SerializeField, Tooltip("Read by 기본값으로 only; never changed.")]
        private TemporaryFortuneCriticalConfiguration fortuneCriticalConfiguration;
        [SerializeField, Min(1)] private int maximumReelCount = DefaultMaximumReelCount;

        private TemporarySlotMachineState slots;
        private TemporaryFortuneCriticalState fortune;
        private TemporaryDiceResultPreferences preferences;
        private IRandomIndexProvider spinRandom;
        private IRandomIndexProvider fortuneRandom;
        private CancellationTokenSource spinCancellation;
        private bool listening;
        private bool stateSubscribed;
        private bool spinning;
        private bool reportedMissingDock;
        private bool reportedMissingTab;
        private bool reportedNoSession;

        public SlotReelDock Dock => dock;
        public SlotDebugTabView SlotTab => slotTab;
        public int MaximumReelCount => Mathf.Max(1, maximumReelCount);
        public bool IsConnected => slots != null;
        public bool IsSpinning => spinning;
        public TemporarySlotMachineState SlotMachine => slots;
        public TemporaryFortuneCriticalState FortuneCritical => fortune;
        public string LastResultMessage { get; private set; } = string.Empty;

        private void Start() => _ = ConnectAsync(PlayerSessionHost.Instance);

        private void OnDestroy()
        {
            CancelSpin();
            UnsubscribeState();
            StopListening();
        }

        public async Task ConnectAsync(PlayerSessionHost host)
        {
            if (!HasDock()) return;
            if (host == null) { Connect(null, null, null); return; }
            try
            {
                await host.WhenReady;
            }
            catch (Exception exception)
            {
                if (this == null) return;
                Debug.LogWarning($"SlotReelDockDebugController could not use the player session: {exception.Message}", this);
                Connect(null, null, null);
                return;
            }
            if (this == null) return;
            if (host.TemporarySlotMachine == null)
                Debug.LogWarning($"SlotReelDockDebugController has no session slot machine. {host.TemporarySlotCombatError}", this);
            Connect(host.TemporarySlotMachine, host.TemporaryFortuneCritical, host.TemporaryDicePreferences);
        }

        // Connects to the session states (a null slot machine is the no-session state). Replaces any previous connection.
        public void Connect(TemporarySlotMachineState slotMachine, TemporaryFortuneCriticalState fortuneCritical,
            TemporaryDiceResultPreferences dicePreferencesSource)
        {
            if (!HasDock()) return;
            HasTab();
            Listen();
            UnsubscribeState();
            slots = slotMachine;
            fortune = slotMachine != null ? fortuneCritical : null;
            preferences = dicePreferencesSource;
            dock.ReduceMotion = ReduceMotion;
            if (slots == null)
            {
                if (!reportedNoSession)
                {
                    reportedNoSession = true;
                    Debug.LogWarning("SlotReelDockDebugController found no player session; start Play from AppScene.", this);
                }
                if (slotTab != null)
                {
                    slotTab.SetSessionAvailable(false);
                    slotTab.SetShown(dock.IsShown);
                }
                ShowResult(NoSessionMessage);
                return;
            }
            spinRandom ??= new SystemRandomIndexProvider(Guid.NewGuid().GetHashCode());
            // Separate source with its own seed, so Fortune Critical never changes the spin draws (as in combat).
            fortuneRandom ??= new SystemRandomIndexProvider(Guid.NewGuid().GetHashCode());
            slots.Changed += Refresh;
            if (fortune != null) fortune.Changed += Refresh;
            stateSubscribed = true;
            if (slotTab != null) slotTab.SetSessionAvailable(true);
            Refresh();
            dock.SetSummary(TemporarySlotCombatController.CreateSummary(SlotCombatStoryFormatter.IdleTotalValue, Array.Empty<SlotRuneLineDisplay>()));
        }

        private bool ReduceMotion => preferences != null && preferences.ReduceMotion;
        private FortuneCriticalRules Rules => fortune != null ? fortune.Rules : FortuneCriticalRules.Disabled;

        // Session reels on the dock; reels and chances on the tab.
        private void Refresh()
        {
            if (this == null) { UnsubscribeState(); return; }
            if (slots == null || dock == null) return;
            var machine = slots.Machine;
            var displays = new SlotReelDisplay[machine.ReelCount];
            var entries = new SlotDebugReelEntry[machine.ReelCount];
            var rules = Rules;
            for (var i = 0; i < displays.Length; i++)
            {
                var reel = machine.Reels[i];
                var values = new int[reel.Count];
                for (var j = 0; j < values.Length; j++) values[j] = reel[j].Value;
                displays[i] = new SlotReelDisplay(reel.Id, values);
                entries[i] = new SlotDebugReelEntry(reel.Id, ReelLabel(i), (float)rules.RuleForReel(reel.Id).Chance);
            }
            dock.SetReels(displays);
            if (slotTab == null) return;
            slotTab.SetReels(entries, machine.ReelCount > 1, machine.ReelCount < MaximumReelCount);
            slotTab.SetTotalChance((float)rules.TotalRule.Chance);
            slotTab.SetShown(dock.IsShown);
        }

        public static string ReelLabel(int index) => $"Reel {(index + 1).ToString(CultureInfo.InvariantCulture)}";

        // reel.{n} with the smallest n from 1 that the machine does not have.
        public static string NextReelId(IReadOnlyList<SlotReel> reels)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reel in reels) used.Add(reel.Id);
            for (var n = 1; ; n++)
            {
                var id = ReelIdPrefix + n.ToString(CultureInfo.InvariantCulture);
                if (!used.Contains(id)) return id;
            }
        }

        // +1 adds a copy of the last reel's strip with a new ID; −1 removes the last reel and its own Fortune rule.
        public void ChangeReelCount(int delta)
        {
            if (slots == null || spinning) return;
            var machine = slots.Machine;
            string reason;
            if (delta > 0)
            {
                if (machine.ReelCount >= MaximumReelCount) return;
                var last = machine.Reels[machine.ReelCount - 1];
                var values = new int[last.Count];
                for (var i = 0; i < values.Length; i++) values[i] = last[i].Value;
                SlotReel added;
                try { added = SlotReel.FromValues(NextReelId(machine.Reels), values); }
                catch (ArgumentException exception) { ShowResult(exception.Message); return; }
                if (!machine.TryAddReel(added, out reason)) ShowResult(reason);
            }
            else if (delta < 0)
            {
                if (machine.ReelCount <= 1) return;
                var id = machine.Reels[machine.ReelCount - 1].Id;
                if (!machine.TryRemoveReel(id, out reason)) { ShowResult(reason); return; }
                // A reel added again with this ID follows the default reel rule.
                if (fortune != null && fortune.Rules.TryGetReelRule(id, out _) && !fortune.TryRemoveReelRule(id, out reason)) ShowResult(reason);
            }
        }

        public void SetReelChance(string reelId, float chance)
        {
            if (slots == null) return;
            string reason;
            if (fortune == null) reason = "Fortune Critical 상태가 없습니다.";
            else if (fortune.TrySetReelChance(reelId, chance, out reason)) return;
            ShowResult(reason);
            Refresh();
        }

        public void SetTotalChance(float chance)
        {
            if (slots == null) return;
            string reason;
            if (fortune == null) reason = "Fortune Critical 상태가 없습니다.";
            else if (fortune.TrySetTotalChance(chance, out reason)) return;
            ShowResult(reason);
            Refresh();
        }

        // Spins the session reels, resolves Fortune Critical and plays the result on the dock. The result is shown only.
        public async Task SpinAsync()
        {
            if (slots == null || spinning || dock == null) return;
            SlotSpinResult spin;
            FortuneCriticalResult result;
            try
            {
                spin = slots.Spin(spinRandom);
                result = FortuneCriticalResolver.Resolve(spin, Rules, fortuneRandom);
            }
            catch (OverflowException exception)
            {
                ShowResult($"합계가 지원 범위를 넘었습니다: {exception.Message}");
                return;
            }
            var display = TemporarySlotCombatController.CreateSpinDisplay(spin, result, Array.Empty<SlotRuneLineDisplay>());
            spinning = true;
            if (slotTab != null) slotTab.SetSpinning(true);
            var cancellation = spinCancellation = new CancellationTokenSource();
            var completed = false;
            try
            {
                completed = await dock.PlaySpinAsync(display, ReduceMotion, cancellation.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception, this); }
            finally
            {
                if (spinCancellation == cancellation) spinCancellation = null;
                cancellation.Dispose();
            }
            if (this == null) return;
            if (!completed && dock != null) dock.StopAtResult(display);
            spinning = false;
            if (slotTab != null) slotTab.SetSpinning(false);
            ShowResult(FormatResult(result));
        }

        public void ShowDock()
        {
            if (dock == null) return;
            dock.ReduceMotion = ReduceMotion;
            dock.Show();
        }

        public void HideDock()
        {
            if (dock == null) return;
            dock.ReduceMotion = ReduceMotion;
            dock.Hide();
        }

        // Session reels and Fortune rules back to the configuration assets. The dock's shown state stays.
        public void ResetToDefaults()
        {
            if (slots == null || spinning) return;
            var failures = new List<string>();
            string reason;
            if (slotMachineConfiguration == null) failures.Add("슬롯머신 설정 자산이 없습니다.");
            else if (!slotMachineConfiguration.TryCreateMachine(out var machine, out reason) || !slots.Machine.TryReplaceAll(machine.Reels, out reason))
                failures.Add(reason);
            if (fortune == null) failures.Add("Fortune Critical 상태가 없습니다.");
            else if (fortuneCriticalConfiguration == null) failures.Add("Fortune Critical 설정 자산이 없습니다.");
            else if (!fortuneCriticalConfiguration.TryCreateRules(FortuneWeightFunctionRegistry.Default, out var rules, out reason) ||
                     !fortune.TryReplaceAll(rules, out reason))
                failures.Add(reason);
            ShowResult(failures.Count == 0 ? ResetDoneMessage : string.Join(" ", failures));
        }

        // "3, 5×2, 2 → 합계 ×3 = 45 (상한 ×30 적용)": payline values in reel order with their own multiplier, the total
        // multiplier, the final total and the cap note.
        public static string FormatResult(FortuneCriticalResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var builder = new StringBuilder();
            for (var i = 0; i < result.Reels.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                var reel = result.Reels[i];
                builder.Append(reel.BaseValue.ToString(CultureInfo.InvariantCulture));
                if (reel.ReelMultiplier > 1) builder.Append('×').Append(reel.ReelMultiplier.ToString(CultureInfo.InvariantCulture));
            }
            if (result.TotalMultiplier > 1) builder.Append(" → 합계 ×").Append(result.TotalMultiplier.ToString(CultureInfo.InvariantCulture));
            builder.Append(" = ").Append(result.FinalTotal.ToString(CultureInfo.InvariantCulture));
            if (result.IsAnyCapped) builder.Append(" (상한 ×").Append(result.MaximumEffectiveMultiplier.ToString(CultureInfo.InvariantCulture)).Append(" 적용)");
            return builder.ToString();
        }

        private void ShowResult(string message)
        {
            LastResultMessage = message ?? string.Empty;
            if (slotTab != null) slotTab.ShowResult(LastResultMessage);
        }

        private void Listen()
        {
            if (listening) return;
            listening = true;
            if (dock != null) dock.ShownChanged += OnShownChanged;
            if (slotTab == null) return;
            slotTab.ReelCountChangeRequested += ChangeReelCount;
            slotTab.ReelChanceChanged += SetReelChance;
            slotTab.TotalChanceChanged += SetTotalChance;
            slotTab.SpinRequested += OnSpinRequested;
            slotTab.ShowRequested += ShowDock;
            slotTab.HideRequested += HideDock;
            slotTab.ResetRequested += ResetToDefaults;
        }

        private void StopListening()
        {
            if (!listening) return;
            listening = false;
            if (dock != null) dock.ShownChanged -= OnShownChanged;
            if (slotTab == null) return;
            slotTab.ReelCountChangeRequested -= ChangeReelCount;
            slotTab.ReelChanceChanged -= SetReelChance;
            slotTab.TotalChanceChanged -= SetTotalChance;
            slotTab.SpinRequested -= OnSpinRequested;
            slotTab.ShowRequested -= ShowDock;
            slotTab.HideRequested -= HideDock;
            slotTab.ResetRequested -= ResetToDefaults;
        }

        private void UnsubscribeState()
        {
            if (!stateSubscribed) return;
            stateSubscribed = false;
            if (slots != null) slots.Changed -= Refresh;
            if (fortune != null) fortune.Changed -= Refresh;
        }

        private void OnSpinRequested() => _ = SpinAsync();

        private void OnShownChanged(bool shown) { if (slotTab != null) slotTab.SetShown(shown); }

        private void CancelSpin()
        {
            var cancellation = spinCancellation;
            spinCancellation = null;
            if (cancellation == null) return;
            cancellation.Cancel();
        }

        private bool HasDock()
        {
            if (dock != null) return true;
            if (!reportedMissingDock)
            {
                reportedMissingDock = true;
                Debug.LogError("SlotReelDockDebugController has no SlotReelDock assigned; it does nothing.", this);
            }
            return false;
        }

        private bool HasTab()
        {
            if (slotTab != null) return true;
            if (!reportedMissingTab)
            {
                reportedMissingTab = true;
                Debug.LogWarning("SlotReelDockDebugController has no SlotDebugTabView assigned; the dock still follows the session.", this);
            }
            return false;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(SlotReelDock targetDock, SlotDebugTabView targetTab, TemporarySlotMachineConfiguration slotConfiguration,
            TemporaryFortuneCriticalConfiguration fortuneConfiguration, int maximumReels)
        {
            StopListening();
            dock = targetDock;
            slotTab = targetTab;
            slotMachineConfiguration = slotConfiguration;
            fortuneCriticalConfiguration = fortuneConfiguration;
            maximumReelCount = Mathf.Max(1, maximumReels);
        }

        public void SetRandomForTests(IRandomIndexProvider source) => spinRandom = source;
        public void SetFortuneRandomForTests(IRandomIndexProvider source) => fortuneRandom = source;
        public TemporarySlotMachineConfiguration SlotMachineConfigurationForEditor => slotMachineConfiguration;
        public TemporaryFortuneCriticalConfiguration FortuneCriticalConfigurationForEditor => fortuneCriticalConfiguration;
#endif
    }
}
