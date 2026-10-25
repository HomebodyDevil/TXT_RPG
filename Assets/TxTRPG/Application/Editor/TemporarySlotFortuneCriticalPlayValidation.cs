using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Players;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.SlotMachine;
using TxTRPG.UI;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode verification for slot-fortune-critical. Enter Play Mode through AppScene with a fresh exploration
    // run (awaiting a node choice), then run the menu. It forces Fortune Critical through the session state API (reel.2
    // and the total always trigger), drives Submit, a pointer click and Reduce Motion, checks the multiplier texts, the
    // Story lines and the layout at four viewports (with screenshots in Application.temporaryCachePath), and restores the
    // session rules at the end. Results are logged with the SLOT_FORTUNE_PLAY_VALIDATE prefix. It changes session state
    // only; no asset is saved.
    public static class TemporarySlotFortuneCriticalPlayValidation
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Validate Slot Fortune Critical (Play Mode)";

        private static readonly (string Name, Vector2 Size)[] Viewports =
        {
            ("desktop-1920x1080", new Vector2(1920f, 1080f)),
            ("ultrawide-21x9", new Vector2(2520f, 1080f)),
            ("tablet-1024x768", new Vector2(1024f, 768f)),
            ("phone-390x844", new Vector2(390f, 844f))
        };

        [MenuItem(MenuPath)]
        public static async void Validate()
        {
            var log = new StringBuilder("SLOT_FORTUNE_PLAY_VALIDATE:\n");
            PlayerSessionHost host = null;
            FortuneCriticalRules original = null;
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode from AppScene first.");
                host = PlayerSessionHost.Instance ?? throw new InvalidOperationException("The player session is missing.");
                var state = host.TemporaryFortuneCritical ?? throw new InvalidOperationException("The Fortune Critical state is missing.");
                original = state.Rules;
                Require(!original.IsDisabled, "the AppRoot configuration enables Fortune Critical");
                log.AppendLine($" session rules from the asset: reel p={original.DefaultReelRule.Chance}, total p={original.TotalRule.Chance}, " +
                    $"×{original.DefaultReelRule.MinimumMultiplier}..×{original.DefaultReelRule.MaximumMultiplier}, cap ×{original.MaximumEffectiveMultiplier}.");
                var refs = TemporarySlotMachinePlayValidation.Find();
                if (refs.Run.Run == null || refs.Run.Run.Phase != ExplorationPhase.AwaitingChoice)
                    throw new InvalidOperationException("Validation requires a fresh awaiting-choice exploration run.");
                TemporarySlotMachinePlayValidation.StartCombat(refs.Run);
                await TemporarySlotMachinePlayValidation.Frames(3);

                // Only reel.2 and the total trigger, with fixed small multipliers so the enemy survives several spins.
                Require(state.TrySetDefaultReelChance(0d, out var reason), reason);
                Require(state.TrySetReelRule("reel.2", Fixed(3), out reason), reason);
                Require(state.TrySetTotalRule(Fixed(2), out reason), reason);

                // 1. Keyboard / controller Submit.
                EventSystem.current.SetSelectedGameObject(refs.Action.Button.gameObject);
                await TemporarySlotMachinePlayValidation.Frames(1);
                TemporarySlotMachinePlayValidation.Submit(refs.Action);
                var accepted = host.PendingSlotAction ?? throw new InvalidOperationException("Submit did not accept a spin.");
                await TemporarySlotMachinePlayValidation.Frames(2);
                Require(refs.Panel.IsSpinning, "the reels spin");
                Require(ReelTexts(refs.Panel) == ",," && refs.Panel.TotalMultiplierText.Length == 0, $"no multiplier while spinning ({ReelTexts(refs.Panel)})");
                Require(refs.Panel.TotalValueText == SlotCombatStoryFormatter.TotalValue(0), $"the spinning total starts at 0 ({refs.Panel.TotalValueText})");
                await CheckTotalStep(refs, accepted, log);
                await TemporarySlotMachinePlayValidation.WaitWhile(() => host.IsTemporaryCombatActionPending, 10d);
                CheckShown(refs, host, accepted, log, "submit");

                // 2. Layout at four viewports, with the real result and with the widest two-digit texts.
                await CheckViewports(refs, host, log);

                // 3. Pointer click (mouse; touch raises the same pointer events on the uGUI button).
                EnsureEnemyAlive(refs);
                await TemporarySlotMachinePlayValidation.WaitWhile(() => !refs.Action.IsInteractable, 5d);
                ExecuteEvents.Execute(refs.Action.Button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                    ExecuteEvents.pointerClickHandler);
                accepted = host.PendingSlotAction ?? throw new InvalidOperationException("A pointer click did not accept a spin.");
                await TemporarySlotMachinePlayValidation.WaitWhile(() => host.IsTemporaryCombatActionPending, 10d);
                CheckShown(refs, host, accepted, log, "pointer click");

                // 4. Reduce Motion, with only the reel multiplier.
                Require(state.TrySetTotalChance(0d, out reason), reason);
                EnsureEnemyAlive(refs);
                host.TemporaryDicePreferences.ReduceMotion = true;
                try
                {
                    await TemporarySlotMachinePlayValidation.WaitWhile(() => !refs.Action.IsInteractable, 5d);
                    refs.Action.Button.onClick.Invoke();
                    accepted = host.PendingSlotAction ?? throw new InvalidOperationException("Reduce Motion did not accept a spin.");
                    await TemporarySlotMachinePlayValidation.WaitWhile(() => host.IsTemporaryCombatActionPending, 10d);
                    CheckShown(refs, host, accepted, log, "reduce motion");
                }
                finally { host.TemporaryDicePreferences.ReduceMotion = false; }

                Require(state.TryReplaceAll(original, out reason) && state.Rules == original, "the session rules are restored");
                log.AppendLine(" session rules restored to the asset values; no asset was saved.");
                Debug.Log(log.Append(" PASS").ToString());
            }
            catch (Exception exception)
            {
                if (host != null && original != null) host.TemporaryFortuneCritical.TryReplaceAll(original, out _);
                Debug.LogError(log.Append(" FAIL: ").Append(exception is InvalidOperationException ? exception.Message : exception.ToString()).ToString());
            }
        }

        private static FortuneCriticalRule Fixed(int multiplier)
        {
            if (!FortuneCriticalRule.TryCreate(1d, multiplier, multiplier, FortuneMultiplierDistribution.Default, FortuneWeightFunctionRegistry.Default,
                    out var rule, out var reason)) throw new InvalidOperationException(reason);
            return rule;
        }

        // Large final totals can end the combat; a new combat keeps the later steps runnable (session state only).
        private static void EnsureEnemyAlive(TemporarySlotMachinePlayValidation.References refs)
        {
            var combat = refs.Controller.Combat;
            if (combat != null && !combat.IsComplete && combat.EnemyHealth.Current > 25) return;
            refs.Controller.BeginCombat(combat != null && !combat.PlayerHealth.IsDefeated ? combat.PlayerHealth : new HealthState(30, 30));
        }

        // With the total forced to trigger, the last running total (no total multiplier) stays for the profile's wait
        // before the total multiplier and the final total replace it.
        private static async Task CheckTotalStep(TemporarySlotMachinePlayValidation.References refs, TemporarySlotAcceptedAction accepted,
            StringBuilder log)
        {
            var expected = TemporarySlotCombatController.CreateSpinDisplay(accepted.Spin, accepted.Fortune, accepted.Allocation);
            Require(expected.HasTotalStep, "the forced total Fortune Critical makes a total step");
            var lastRunning = expected.ProgressSummaries[expected.ProgressSummaries.Count - 1].TotalValue;
            await TemporarySlotMachinePlayValidation.WaitWhile(() => refs.Panel.IsSpinning && refs.Panel.TotalValueText != lastRunning, 10d);
            var waitStart = EditorApplication.timeSinceStartup;
            Require(refs.Panel.IsSpinning && refs.Panel.TotalValueText == lastRunning && refs.Panel.TotalMultiplierText.Length == 0,
                $"the last running total {lastRunning} is shown without the total multiplier ({refs.Panel.TotalValueText} '{refs.Panel.TotalMultiplierText}')");
            await TemporarySlotMachinePlayValidation.WaitWhile(() => refs.Panel.TotalMultiplierText.Length == 0, 5d);
            var waited = EditorApplication.timeSinceStartup - waitStart;
            var profile = refs.Panel.SpinProfileForEditor;
            var delay = (profile != null ? profile.Settings : SlotReelSpinSettings.Default).TotalCriticalRevealDelaySeconds;
            Require(refs.Panel.TotalMultiplierText == SlotCombatStoryFormatter.MultiplierText(accepted.Fortune.TotalMultiplier),
                $"the total multiplier appears after the wait ('{refs.Panel.TotalMultiplierText}')");
            Require(waited >= delay * .75d, $"the total step waited {waited:F2}s (profile {delay:F2}s)");
            log.AppendLine($" total step: running {string.Join(" → ", expected.ProgressSummaries.Select(s => s.TotalValue))}, " +
                $"then {refs.Panel.TotalMultiplierText} {refs.Panel.TotalValueText} after {waited:F2}s (profile {delay:F2}s).");
        }

        private static string ReelTexts(SlotMachinePanel panel) =>
            string.Join(",", panel.ReelViews.OfType<SlotReelView>().Select(v => v.FortuneMultiplierText));

        private static void CheckShown(TemporarySlotMachinePlayValidation.References refs, PlayerSessionHost host, TemporarySlotAcceptedAction accepted,
            StringBuilder log, string label)
        {
            var fortune = accepted.Fortune;
            Require(host.LastTemporarySlotTurn != null && host.LastTemporarySlotTurn.Fortune == fortune, $"{label}: the accepted Fortune result was applied");
            var expected = string.Join(",", fortune.Reels.Select(r => SlotCombatStoryFormatter.MultiplierText(r.ReelMultiplier)));
            Require(ReelTexts(refs.Panel) == expected, $"{label}: reel multipliers '{ReelTexts(refs.Panel)}' (expected '{expected}')");
            Require(refs.Panel.TotalMultiplierText == SlotCombatStoryFormatter.MultiplierText(fortune.TotalMultiplier),
                $"{label}: total multiplier '{refs.Panel.TotalMultiplierText}'");
            Require(refs.Panel.TotalValueText == SlotCombatStoryFormatter.TotalValue(fortune.FinalTotal), $"{label}: final total shown ({refs.Panel.TotalValueText})");
            Require(accepted.Allocation.Lines.Sum(l => l.Amount) == fortune.FinalTotal, $"{label}: the final total is allocated");
            var story = Object.FindAnyObjectByType<StoryTextPanel>();
            var texts = story != null ? story.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToList() : null;
            var spinLine = SlotCombatStoryFormatter.Spin(fortune);
            var summary = SlotCombatStoryFormatter.FortuneSummary(fortune);
            Require(texts != null && texts.Any(t => t.Contains(spinLine)) && texts.Any(t => t.Contains(summary)), $"{label}: Story shows '{spinLine}' and '{summary}'");
            log.AppendLine($" {label}: Story '{spinLine}' / '{summary}', reels '{ReelTexts(refs.Panel)}', total '{refs.Panel.TotalMultiplierText}' {refs.Panel.TotalValueText}.");
        }

        private static async Task CheckViewports(TemporarySlotMachinePlayValidation.References refs, PlayerSessionHost host, StringBuilder log)
        {
            if (refs.Screen == null || refs.Screen.PageScroll == null) { log.AppendLine(" viewports: ResponsiveMainScreen missing; not verified."); return; }
            var folder = Path.Combine(UnityEngine.Application.temporaryCachePath, "SlotFortuneCritical");
            Directory.CreateDirectory(folder);
            var viewport = refs.Screen.PageScroll.viewport;
            var saved = (viewport.anchorMin, viewport.anchorMax, viewport.sizeDelta, viewport.anchoredPosition);
            var savedScroll = refs.Screen.PageScroll.verticalNormalizedPosition;
            var last = host.LastTemporarySlotTurn;
            var shown = TemporarySlotCombatController.CreateSpinDisplay(last.Spin, last.Fortune, last.Allocation);
            try
            {
                foreach (var (name, size) in Viewports)
                {
                    viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
                    viewport.sizeDelta = size; viewport.anchoredPosition = Vector2.zero;
                    await Task.Delay(200);
                    Canvas.ForceUpdateCanvases();
                    refs.Action.Placement.UpdatePlacement();
                    ScrollIntoView(refs);
                    await TemporarySlotMachinePlayValidation.Frames(2);
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"{name}.png"));
                    await TemporarySlotMachinePlayValidation.Frames(2);
                    var totalBox = refs.Panel.TotalMultiplierRootForEditor.parent as RectTransform;
                    log.AppendLine($" {name}: TotalBox {totalBox.rect.size.x:F0}x{totalBox.rect.size.y:F0}, multiplier {(refs.Panel.TotalMultiplierLayoutForEditor.PlacedBeside ? "beside" : "above")} the value.");
                    CheckOverlaps(refs, $"{name} result");

                    // The widest texts: two digits on every reel and on the total.
                    foreach (var view in refs.Panel.ReelViews) view.ShowFortuneMultiplier("×10");
                    refs.Panel.SetSummary(TemporarySlotCombatController.CreateSummary(SlotCombatStoryFormatter.TotalValue(last.Fortune.FinalTotal),
                        shown.ResultSummary.Lines, "×10"));
                    Canvas.ForceUpdateCanvases();
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"{name}-x10.png"));
                    await TemporarySlotMachinePlayValidation.Frames(2);
                    CheckOverlaps(refs, $"{name} ×10");
                    refs.Panel.StopAtResult(shown);
                    var panelSize = ((RectTransform)refs.Panel.transform).rect.size;
                    log.AppendLine($" {name}: panel {panelSize.x:F0}x{panelSize.y:F0}, multipliers clear of the payline digits, total value, label and action button (×3/×2 and ×10).");
                }
                log.AppendLine($" screenshots: {folder}");
            }
            finally
            {
                viewport.anchorMin = saved.anchorMin; viewport.anchorMax = saved.anchorMax;
                viewport.sizeDelta = saved.sizeDelta; viewport.anchoredPosition = saved.anchoredPosition;
                refs.Screen.PageScroll.verticalNormalizedPosition = savedScroll;
                await Task.Delay(200);
            }
        }

        // Scrolls the page so the slot machine panel is visible in the screenshot (narrow layouts stack the panels).
        private static void ScrollIntoView(TemporarySlotMachinePlayValidation.References refs)
        {
            var scroll = refs.Screen.PageScroll;
            var content = scroll.content;
            var viewport = scroll.viewport;
            if (content == null || viewport == null) return;
            var overflow = content.rect.height - viewport.rect.height;
            if (overflow <= 1f) return;
            var panel = (RectTransform)refs.Panel.transform;
            var panelTop = content.InverseTransformPoint(panel.TransformPoint(new Vector3(0f, panel.rect.yMax, 0f))).y;
            var fromTop = content.rect.yMax - panelTop;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - fromTop / overflow);
            Canvas.ForceUpdateCanvases();
        }

        // Glyph bounds (not rects): a multiplier never covers a payline digit, the total value or label, stays inside its
        // reel or total box, and the action button never covers it.
        private static void CheckOverlaps(TemporarySlotMachinePlayValidation.References refs, string label)
        {
            var button = TemporarySlotMachinePlayValidation.WorldRect((RectTransform)refs.Action.Button.transform);
            foreach (var view in refs.Panel.ReelViews.OfType<SlotReelView>())
            {
                if (view.FortuneMultiplierText.Length == 0) continue;
                var badge = GlyphRect(view.FortuneTextForEditor);
                var window = TemporarySlotMachinePlayValidation.WorldRect(view.WindowForEditor);
                var payline = view.CellTextsForEditor[view.VisibleCellCount / 2 + 1];
                Require(!badge.Overlaps(GlyphRect(payline)), $"{label}: {view.name} multiplier {badge} covers the payline digit {GlyphRect(payline)}");
                Require(badge.xMin >= window.xMin - 1f && badge.xMax <= window.xMax + 1f, $"{label}: {view.name} multiplier {badge} stays inside the reel {window}");
                Require(!button.Overlaps(badge), $"{label}: the action button covers the {view.name} multiplier");
            }
            if (refs.Panel.TotalMultiplierText.Length == 0) return;
            var total = GlyphRect(refs.Panel.TotalMultiplierTextForEditor);
            var texts = refs.Panel.GetComponentsInChildren<TMP_Text>(true);
            var value = texts.First(t => t.name == "TotalValue");
            var totalLabel = texts.First(t => t.name == "TotalLabel");
            // A very short TotalBox lets the multiplier extend above it, into the summary area's margin.
            var box = TemporarySlotMachinePlayValidation.WorldRect((RectTransform)value.transform.parent);
            var summary = TemporarySlotMachinePlayValidation.WorldRect((RectTransform)value.transform.parent.parent);
            Require(!total.Overlaps(GlyphRect(value)), $"{label}: the total multiplier {total} covers the total value {GlyphRect(value)}");
            // The total shows its value only; an (empty) inactive label has no glyphs to cover.
            Require(!totalLabel.gameObject.activeSelf || !total.Overlaps(GlyphRect(totalLabel)), $"{label}: the total multiplier covers the total label");
            Require(total.xMin >= box.xMin - 1f && total.xMax <= box.xMax + 1f && total.yMin >= box.yMin - 1f && total.yMax <= summary.yMax + 1f,
                $"{label}: the total multiplier {total} stays over TotalBox {box} within the summary area");
            Require(!button.Overlaps(total), $"{label}: the action button covers the total multiplier");
        }

        private static Rect GlyphRect(TMP_Text text)
        {
            text.ForceMeshUpdate();
            var bounds = text.textBounds;
            var min = text.transform.TransformPoint(bounds.min);
            var max = text.transform.TransformPoint(bounds.max);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
