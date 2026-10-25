using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TxTRPG.Application.Players;
using TxTRPG.Application.SlotMachine;
using TxTRPG.UI.Shapes;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Editor
{
    // Play Mode preview of the SlotReelDock in the loaded content Scene (permanent verification tool). Spins use the
    // session slot machine's reels (TemporarySlotMachineConfiguration.asset without a session) and a preview-only random
    // stop; they never change the session, runes or Fortune Critical state. Add/Remove Reel change only the reels the
    // preview shows (later spins use them until Play Mode ends). Card shape changes are runtime-only and not saved.
    // Every result is logged with the SLOT_REEL_DOCK_PREVIEW prefix.
    public static class SlotReelDockPreviewMenu
    {
        public const string MenuRoot = "Tools/TxT RPG/Application/Preview/Slot Reel Dock/";
        public const string SpinMenuPath = MenuRoot + "Spin";
        public const string SpinWithMultiplierMenuPath = MenuRoot + "Spin With Total Multiplier";
        public const string AddReelMenuPath = MenuRoot + "Add Reel";
        public const string RemoveReelMenuPath = MenuRoot + "Remove Reel";
        public const string ToggleShownMenuPath = MenuRoot + "Toggle Shown";
        public const string CycleCardShapeMenuPath = MenuRoot + "Cycle Card Shape";
        private const string Prefix = "SLOT_REEL_DOCK_PREVIEW";

        private static readonly UiShape[] ShapeCycle = { UiShape.Rectangle, UiShape.RoundedRectangle, UiShape.Circle, UiShape.Ellipse };
        private static readonly System.Random PreviewRandom = new();
        private static int previewDockId;
        private static List<SlotReelDisplay> previewReels;
        private static SlotSpinDisplay lastSpin;

        [MenuItem(SpinMenuPath)]
        public static void Spin() => Spin(false);

        [MenuItem(SpinWithMultiplierMenuPath)]
        public static void SpinWithTotalMultiplier() => Spin(true);

        [MenuItem(AddReelMenuPath)]
        public static void AddReel()
        {
            if (!TryGetDock(out var dock)) return;
            var reels = Reels(dock);
            var last = reels[reels.Count - 1];
            reels.Add(new SlotReelDisplay($"preview.reel.{reels.Count + 1}", last.Values));
            dock.SetReels(reels.ToArray());
            Debug.Log($"{Prefix}: reels {reels.Count} (dock shows {dock.ReelCount}{(dock.IsSpinning ? ", applied after the spin" : string.Empty)}).");
        }

        [MenuItem(RemoveReelMenuPath)]
        public static void RemoveReel()
        {
            if (!TryGetDock(out var dock)) return;
            var reels = Reels(dock);
            if (reels.Count <= 1) { Debug.LogWarning($"{Prefix}: at least one reel is kept."); return; }
            reels.RemoveAt(reels.Count - 1);
            dock.SetReels(reels.ToArray());
            Debug.Log($"{Prefix}: reels {reels.Count} (dock shows {dock.ReelCount}{(dock.IsSpinning ? ", applied after the spin" : string.Empty)}).");
        }

        [MenuItem(ToggleShownMenuPath)]
        public static void ToggleShown()
        {
            if (!TryGetDock(out var dock)) return;
            dock.SetShown(!dock.IsShown);
            Debug.Log($"{Prefix}: shown {dock.IsShown}, PresentationRoot {dock.PresentationRoot.anchoredPosition}, alpha {dock.CanvasGroup.alpha:F2}.");
        }

        [MenuItem(CycleCardShapeMenuPath)]
        public static void CycleCardShape()
        {
            if (!TryGetDock(out var dock)) return;
            if (dock.TotalCard == null) { Debug.LogWarning($"{Prefix}: the dock has no total card."); return; }
            var current = Array.IndexOf(ShapeCycle, dock.TotalCard.Appearance.Shape);
            var next = ShapeCycle[(current + 1) % ShapeCycle.Length];
            dock.TotalCard.SetAppearance(dock.TotalCard.Appearance.WithShape(next));
            var size = ((RectTransform)dock.TotalCard.transform).rect.size;
            Debug.Log($"{Prefix}: card shape {next}, size {size}, fill radius {dock.TotalCard.FillGraphic?.CornerRadius:F1}.");
        }

        private static async void Spin(bool totalMultiplier)
        {
            if (!TryGetDock(out var dock)) return;
            if (dock.IsSpinning && lastSpin != null)
            {
                dock.StopAtResult(lastSpin);
                Debug.Log($"{Prefix}: the running spin was stopped at its result.");
            }
            var reels = Reels(dock);
            var stops = new SlotReelStopDisplay[reels.Count];
            var progress = new SlotSummaryDisplay[reels.Count];
            var total = 0;
            for (var i = 0; i < stops.Length; i++)
            {
                var stop = PreviewRandom.Next(reels[i].Values.Count);
                total += reels[i].Values[stop];
                stops[i] = new SlotReelStopDisplay(reels[i].ReelId, stop, reels[i].Values);
                // The running total adds each payline value as its reel stops, like the real spin display.
                progress[i] = TemporarySlotCombatController.CreateSummary(SlotCombatStoryFormatter.TotalValue(total), Array.Empty<SlotRuneLineDisplay>());
            }
            var multiplier = totalMultiplier ? 2 : 1;
            var spin = new SlotSpinDisplay(stops, total,
                TemporarySlotCombatController.CreateSummary(SlotCombatStoryFormatter.TotalValue(0), Array.Empty<SlotRuneLineDisplay>()),
                progress, totalMultiplier,
                TemporarySlotCombatController.CreateSummary(SlotCombatStoryFormatter.TotalValue(total * multiplier), Array.Empty<SlotRuneLineDisplay>(),
                    SlotCombatStoryFormatter.MultiplierText(multiplier)));
            lastSpin = spin;
            var host = PlayerSessionHost.Instance;
            var reduceMotion = host != null && host.TemporaryDicePreferences != null && host.TemporaryDicePreferences.ReduceMotion;
            Debug.Log($"{Prefix}: spin {reels.Count} reels, payline {string.Join("+", stops.Select(s => s.PaylineValue))} = {total}" +
                $"{(totalMultiplier ? " ×2" : string.Empty)}, reduce motion {reduceMotion}.");
            try
            {
                var done = await dock.PlaySpinAsync(spin, reduceMotion, CancellationToken.None);
                if (dock == null) return;
                Debug.Log($"{Prefix}: spin {(done ? "completed" : "interrupted")}; card label '{dock.TotalLabelText}' value '{dock.TotalValueText}' " +
                    $"multiplier '{dock.TotalMultiplierText}', reels {dock.ReelCount}, dividers {dock.ActiveDividerCount}.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // The reels the preview shows, starting from the session (or configuration) reels in each Play session.
        private static List<SlotReelDisplay> Reels(SlotReelDock dock)
        {
            if (previewReels != null && previewDockId == dock.GetInstanceID()) return previewReels;
            previewDockId = dock.GetInstanceID();
            previewReels = SessionReels();
            lastSpin = null;
            return previewReels;
        }

        private static List<SlotReelDisplay> SessionReels()
        {
            var state = PlayerSessionHost.Instance != null ? PlayerSessionHost.Instance.TemporarySlotMachine : null;
            if (state != null)
                return state.Machine.Reels.Select(reel => new SlotReelDisplay(reel.Id, reel.Strip.Select(symbol => symbol.Value).ToArray())).ToList();
            var configuration = AssetDatabase.LoadAssetAtPath<TemporarySlotMachineConfiguration>(TemporarySlotMachineIntegration.SlotConfigurationPath);
            var definitions = configuration != null && configuration.Reels.Count > 0
                ? configuration.Reels
                : TemporarySlotMachineConfiguration.CreateDefaultReels();
            Debug.Log($"{Prefix}: no session slot machine; using {(configuration != null ? TemporarySlotMachineIntegration.SlotConfigurationPath : "the default reels")}.");
            return definitions.Select(definition => new SlotReelDisplay(definition.Id, definition.Values)).ToList();
        }

        private static bool TryGetDock(out SlotReelDock dock)
        {
            dock = null;
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Prefix}: enter Play Mode first."); return false; }
            var docks = UnityEngine.Object.FindObjectsByType<SlotReelDock>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (docks.Length != 1)
            {
                Debug.LogWarning($"{Prefix}: expected exactly one active SlotReelDock in the loaded Scenes, found {docks.Length}.");
                return false;
            }
            dock = docks[0];
            return true;
        }
    }
}
