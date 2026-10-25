using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Players;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.SlotMachine;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode probe (slot-progressive-total-display). Enter Play Mode through AppScene so Candidate_MainScene
    // is the content scene, then run the menu. It first checks that the dock starts off screen with the idle total 0, then drives the DEBUG panel's slot Tab controller (SpinAsync, the same call
    // as the 회전 button) with the total chance at 1 and 0, with Reduce Motion and with 10 reels, samples the dock's total
    // card every frame and checks: 0 at the start, only the running totals of the stopped reels (recomputed from the
    // logged result with the cap), no total multiplier before the result, the final total, and the total Fortune Critical
    // wait. It then switches the Game View through four sizes (temporary Standalone custom sizes removed again by their
    // total index), logs the total card text fit and captures Temp/slot-progressive-total-*.png. Session state only;
    // the reels and Fortune rules are reset at the end and nothing is saved. Results use the SLOT_PROGRESSIVE_TOTAL_PROBE
    // prefix. Safe to rerun in a new Play session; remove once the progressive total display is verified.
    public static class TemporarySlotProgressiveTotalPlayProbe
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Probe Slot Progressive Total (Play Mode)";
        private const string Prefix = "SLOT_PROGRESSIVE_TOTAL_PROBE";

        private static readonly (string Name, int Width, int Height)[] Sizes =
        {
            ("desktop-1920x1080", 1920, 1080), ("ultrawide-2520x1080", 2520, 1080), ("tablet-1024x768", 1024, 768), ("phone-390x844", 390, 844)
        };

        private static bool running;

        [MenuItem(MenuPath, false, 3002)]
        public static async void Run()
        {
            if (running) { Debug.LogWarning($"{Prefix}: already running."); return; }
            running = true;
            var log = new StringBuilder($"{Prefix}:\n");
            var failures = new List<string>();
            SlotReelDockDebugController controller = null;
            PlayerSessionHost host = null;
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode through AppScene first.");
                controller = Object.FindAnyObjectByType<SlotReelDockDebugController>() ?? throw new InvalidOperationException("SlotReelDockDebugController is missing.");
                await TemporarySlotMachinePlayValidation.WaitWhile(() => !controller.IsConnected, 5d);
                if (!controller.IsConnected) throw new InvalidOperationException("The slot debug controller is not connected to the session.");
                host = PlayerSessionHost.Instance ?? throw new InvalidOperationException("The player session is missing.");
                var fortune = controller.FortuneCritical ?? throw new InvalidOperationException("The Fortune Critical state is missing.");
                var profile = controller.Dock.SpinProfileForEditor;
                var delay = (profile != null ? profile.Settings : TxTRPG.UI.SlotMachine.SlotReelSpinSettings.Default).TotalCriticalRevealDelaySeconds;
                log.AppendLine($" dock profile wait {delay:F2}s, cap ×{fortune.Rules.MaximumEffectiveMultiplier}.");
                // Play starts with the dock below the screen and the idle total 0.
                var dock = controller.Dock;
                var rootCorners = new Vector3[4];
                var parentCorners = new Vector3[4];
                dock.PresentationRoot.GetWorldCorners(rootCorners);
                ((RectTransform)dock.transform.parent).GetWorldCorners(parentCorners);
                var below = rootCorners[1].y <= parentCorners[0].y + .5f;
                log.AppendLine($" at start: shown {dock.IsShown}, alpha {dock.CanvasGroup.alpha:F2}, PresentationRoot {dock.PresentationRoot.anchoredPosition} " +
                    $"(hidden {dock.HiddenOffset}), below the parent {below}, total '{dock.TotalValueText}', show button {controller.SlotTab.ShowButton.interactable}.");
                if (dock.IsShown || !below || dock.CanvasGroup.alpha > 0f) failures.Add("the dock does not start off screen");
                if (dock.TotalValueText != SlotCombatStoryFormatter.IdleTotalValue || dock.TotalValueText != "0")
                    failures.Add($"the idle total is '{dock.TotalValueText}', expected 0");
                if (!controller.SlotTab.ShowButton.interactable || controller.SlotTab.HideButton.interactable)
                    failures.Add("the slot Tab move buttons do not match the hidden start");
                controller.ShowDock();
                await Task.Delay(800);

                await Scenario(controller, host, 1d, false, delay, "3 reels, total p=1", log, failures);
                await Scenario(controller, host, 0d, false, delay, "3 reels, total p=0", log, failures);
                await Scenario(controller, host, 1d, true, delay, "3 reels, total p=1, Reduce Motion", log, failures);
                for (var i = 0; i < 7; i++) controller.ChangeReelCount(1);
                Require(fortune.TrySetDefaultReelChance(1d, out var reason), reason);
                await Scenario(controller, host, 1d, false, delay, $"{controller.SlotMachine.Machine.ReelCount} reels, reels and total p=1", log, failures);
                await CheckSizes(controller, log, failures);
            }
            catch (Exception exception)
            {
                failures.Add(exception is InvalidOperationException ? exception.Message : exception.ToString());
            }
            finally
            {
                if (host != null && host.TemporaryDicePreferences != null) host.TemporaryDicePreferences.ReduceMotion = false;
                if (controller != null && !controller.IsSpinning) controller.ResetToDefaults();
                running = false;
            }
            log.AppendLine(" session reels and Fortune rules reset to the configuration assets; nothing saved.");
            if (failures.Count == 0) Debug.Log(log.Append(" PASS").ToString());
            else Debug.LogError(log.Append(" FAIL:\n  ").Append(string.Join("\n  ", failures)).ToString());
        }

        private static async Task Scenario(SlotReelDockDebugController controller, PlayerSessionHost host, double totalChance, bool reduceMotion,
            float delay, string label, StringBuilder log, List<string> failures)
        {
            var fortune = controller.FortuneCritical;
            Require(fortune.TrySetTotalChance(totalChance, out var reason), reason);
            host.TemporaryDicePreferences.ReduceMotion = reduceMotion;
            var dock = controller.Dock;
            var samples = new List<(double Time, string Value, string Multiplier)>();
            var start = EditorApplication.timeSinceStartup;
            void Sample()
            {
                var value = dock.TotalValueText;
                var multiplier = dock.TotalMultiplierText;
                if (samples.Count == 0 || samples[samples.Count - 1].Value != value || samples[samples.Count - 1].Multiplier != multiplier)
                    samples.Add((EditorApplication.timeSinceStartup - start, value, multiplier));
            }
            var spin = controller.SpinAsync();
            Sample();
            while (!spin.IsCompleted && EditorApplication.timeSinceStartup - start < 15d)
            {
                await Task.Yield();
                Sample();
            }
            await spin;
            Sample();

            void Check(bool ok, string what) { if (!ok) failures.Add($"{label}: {what}"); }
            var message = controller.LastResultMessage;
            var expected = ExpectedRunningTotals(message, fortune.Rules.MaximumEffectiveMultiplier, out var final, out var totalMultiplier);
            var sequence = string.Join(" → ", samples.Select(s => $"{s.Value}{(s.Multiplier.Length > 0 ? " " + s.Multiplier : string.Empty)}@{s.Time:F2}"));
            log.AppendLine($" {label}: result '{message}', expected running {string.Join(",", expected)}; shown {sequence}");
            Check(samples[0].Value == "0" && samples[0].Multiplier.Length == 0, $"starts at 0 ({samples[0].Value})");
            var index = 0;
            for (var i = 0; i < samples.Count - 1; i++)
            {
                Check(samples[i].Multiplier.Length == 0, $"no total multiplier before the result ({samples[i].Multiplier})");
                var at = expected.IndexOf(samples[i].Value, index);
                Check(at >= 0, $"'{samples[i].Value}' is a running total in order");
                if (at >= 0) index = at;
            }
            var last = samples[samples.Count - 1];
            Check(last.Value == final, $"ends on the final total {final} ({last.Value})");
            Check(last.Multiplier == (totalMultiplier > 1 ? $"×{totalMultiplier}" : string.Empty), $"total multiplier '{last.Multiplier}'");
            Check(samples.Count <= expected.Count + 1, $"at most one update per reel plus the result ({samples.Count})");
            if (totalMultiplier > 1 && samples.Count >= 2)
            {
                var previous = samples[samples.Count - 2];
                var gap = last.Time - previous.Time;
                log.AppendLine($"   total step: {previous.Value} for {gap:F2}s, then {last.Multiplier} {last.Value}.");
                if (reduceMotion) Check(gap < .3d, $"Reduce Motion skips the wait ({gap:F2}s)");
                else Check(previous.Value == expected[expected.Count - 1] && gap >= delay * .75d && gap < delay + .5d,
                    $"the last running total stays for the wait ({previous.Value}, {gap:F2}s)");
            }
            else if (totalChance >= 1d) Check(false, "the total chance 1 produced no total multiplier");
            else Check(last.Value == expected[expected.Count - 1], "without a total multiplier the last running total is the final total");
        }

        // "3, 5×2, 2 → 합계 ×3 = 45 (…)": running totals 0, v1×min(C,r1), … and the final total.
        private static List<string> ExpectedRunningTotals(string message, int cap, out string final, out int totalMultiplier)
        {
            var match = Regex.Match(message ?? string.Empty, @"^(?<reels>[^→=]+?)(?: → 합계 ×(?<total>\d+))? = (?<final>\d+)");
            if (!match.Success) throw new InvalidOperationException($"Unexpected result message '{message}'.");
            final = match.Groups["final"].Value;
            totalMultiplier = match.Groups["total"].Success ? int.Parse(match.Groups["total"].Value, CultureInfo.InvariantCulture) : 1;
            var totals = new List<string> { "0" };
            long running = 0;
            foreach (var part in match.Groups["reels"].Value.Split(','))
            {
                var pieces = part.Trim().Split('×');
                var value = long.Parse(pieces[0], CultureInfo.InvariantCulture);
                var multiplier = pieces.Length > 1 ? int.Parse(pieces[1], CultureInfo.InvariantCulture) : 1;
                running += value * Math.Min(cap, multiplier);
                totals.Add(running.ToString(CultureInfo.InvariantCulture));
            }
            return totals;
        }

        // The last result stays on the card; each size logs the value and multiplier text fit in canvas units.
        private static async Task CheckSizes(SlotReelDockDebugController controller, StringBuilder log, List<string> failures)
        {
            var assembly = typeof(EditorWindow).Assembly;
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            var selectedIndex = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var previousIndex = (int)selectedIndex.GetValue(view);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(singleton, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            int BuiltinCount() => (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            int CustomCount() => (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            var customBefore = CustomCount();
            try
            {
                foreach (var entry in Sizes)
                {
                    var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[]
                        { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), entry.Width, entry.Height, "Slot progressive total probe" });
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    selectedIndex.SetValue(view, BuiltinCount() + CustomCount() - 1);
                    view.Repaint();
                    await TemporarySlotMachinePlayValidation.WaitWhile(() => Screen.width != entry.Width || Screen.height != entry.Height, 5d);
                    await Task.Delay(1000);
                    Canvas.ForceUpdateCanvases();
                    if (Screen.width != entry.Width || Screen.height != entry.Height)
                        failures.Add($"{entry.Name}: Game View is {Screen.width}x{Screen.height}");
                    log.AppendLine($" {entry.Name}: {Fit(controller, failures, entry.Name)}");
                    ScreenCapture.CaptureScreenshot($"Temp/slot-progressive-total-{entry.Name}.png");
                    await Task.Delay(400);
                    // Removes the probe size by its total index (builtin + custom).
                    selectedIndex.SetValue(view, previousIndex);
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { BuiltinCount() + CustomCount() - 1 });
                }
            }
            finally
            {
                while (CustomCount() > customBefore) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { BuiltinCount() + CustomCount() - 1 });
                selectedIndex.SetValue(view, previousIndex);
                view.Repaint();
            }
        }

        private static string Fit(SlotReelDockDebugController controller, List<string> failures, string name)
        {
            var card = controller.Dock.TotalCard;
            var texts = card.GetComponentsInChildren<TMP_Text>(true);
            var value = texts.FirstOrDefault(t => t.name == "TotalValue");
            var multiplier = texts.FirstOrDefault(t => card.TotalMultiplierText.Length > 0 && t.text == card.TotalMultiplierText);
            var canvas = card.GetComponentInParent<Canvas>().rootCanvas;
            var scale = canvas.transform.lossyScale.x;
            string Describe(TMP_Text text)
            {
                if (text == null) return "missing";
                text.ForceMeshUpdate();
                var rect = text.rectTransform.rect;
                var overflow = text.isTextOverflowing || text.preferredWidth > rect.width + .5f;
                if (overflow) failures.Add($"{name}: '{text.text}' overflows ({text.preferredWidth:F0} > {rect.width:F0})");
                var corners = new Vector3[4];
                text.rectTransform.GetWorldCorners(corners);
                var cardCorners = new Vector3[4];
                ((RectTransform)card.transform).GetWorldCorners(cardCorners);
                var inside = corners[0].x >= cardCorners[0].x - 1f && corners[2].x <= cardCorners[2].x + 1f &&
                    corners[0].y >= cardCorners[0].y - 1f && corners[2].y <= cardCorners[2].y + 1f;
                return $"'{text.text}' font {text.fontSize:F1} (screen {text.fontSize * text.transform.lossyScale.x:F1}px) " +
                    $"rect {rect.width:F0}x{rect.height:F0} preferred {text.preferredWidth:F0}{(overflow ? " OVERFLOW" : string.Empty)}{(inside ? string.Empty : " OUTSIDE-CARD")}";
            }
            return $"card {((RectTransform)card.transform).rect.size}, canvas scale {scale:F3}; value {Describe(value)}; multiplier {Describe(multiplier)}";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
