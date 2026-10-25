using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.SlotMachine;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode probe (slot-total-value-only-and-card-centered-on-reel-edge). Enter Play Mode through AppScene so
    // Candidate_MainScene is the content scene, then run the menu. It checks that the hidden dock's card is below the
    // SlotReelDockClip parent, shows the dock, checks that the card has no visible label, that the value fills ContentRoot
    // centered and that the card center lies on the reel machine's (and Frame's) top edge clear of the payline, spins once
    // with the total chance at 1, then shows '999' with '×30' on the card (display only) at four Game View sizes (temporary
    // Standalone custom sizes removed again by their total index), logs the value font size in canvas units and whether the
    // multiplier stays inside the card without covering the value glyphs, captures Temp/slot-total-value-only-*.png, hides
    // the dock and checks the card leaves the frame. Session state only; the reels and Fortune rules are reset at the end and
    // nothing is saved. Results use the SLOT_TOTAL_VALUE_ONLY_PROBE prefix. Safe to rerun in a new Play session; remove once
    // the value-only total card is verified.
    public static class TemporarySlotTotalValueOnlyPlayProbe
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Probe Slot Total Value Only (Play Mode)";
        private const string Prefix = "SLOT_TOTAL_VALUE_ONLY_PROBE";

        private static readonly (string Name, int Width, int Height)[] Sizes =
        {
            ("desktop-1920x1080", 1920, 1080), ("ultrawide-2520x1080", 2520, 1080), ("tablet-1024x768", 1024, 768), ("phone-390x844", 390, 844)
        };

        private static bool running;

        [MenuItem(MenuPath, false, 3003)]
        public static async void Run()
        {
            if (running) { Debug.LogWarning($"{Prefix}: already running."); return; }
            running = true;
            var log = new StringBuilder($"{Prefix}:\n");
            var failures = new List<string>();
            SlotReelDockDebugController controller = null;
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode through AppScene first.");
                controller = Object.FindAnyObjectByType<SlotReelDockDebugController>() ?? throw new InvalidOperationException("SlotReelDockDebugController is missing.");
                await TemporarySlotMachinePlayValidation.WaitWhile(() => !controller.IsConnected, 5d);
                if (!controller.IsConnected) throw new InvalidOperationException("The slot debug controller is not connected to the session.");
                var dock = controller.Dock;
                var card = dock.TotalCard;
                log.AppendLine($" overhangRatio {card.OverhangRatio:F2}.");
                if (!Mathf.Approximately(card.OverhangRatio, .5f)) failures.Add($"overhangRatio is {card.OverhangRatio}, expected 0.5");

                CheckHidden(dock, "at start", log, failures);
                controller.ShowDock();
                await Task.Delay(900);
                CheckCard(dock, "shown idle", log, failures);
                if (dock.TotalValueText != "0") failures.Add($"idle total '{dock.TotalValueText}', expected 0");

                if (!controller.FortuneCritical.TrySetTotalChance(1d, out var reason)) throw new InvalidOperationException(reason);
                await controller.SpinAsync();
                log.AppendLine($" spin: '{controller.LastResultMessage}', card '{dock.TotalValueText}' '{dock.TotalMultiplierText}'.");
                CheckCard(dock, "after spin", log, failures);
                if (dock.TotalMultiplierText.Length == 0) failures.Add("the total chance 1 produced no card multiplier");

                card.SetTotal(SlotCombatStoryFormatter.TotalLabel, "999", "×30");
                await CheckSizes(dock, log, failures);

                controller.HideDock();
                await Task.Delay(900);
                CheckHidden(dock, "after hide", log, failures);
            }
            catch (Exception exception)
            {
                failures.Add(exception is InvalidOperationException ? exception.Message : exception.ToString());
            }
            finally
            {
                if (controller != null && !controller.IsSpinning) controller.ResetToDefaults();
                running = false;
            }
            log.AppendLine(" session reels and Fortune rules reset to the configuration assets; nothing saved.");
            if (failures.Count == 0) Debug.Log(log.Append(" PASS").ToString());
            else Debug.LogError(log.Append(" FAIL:\n  ").Append(string.Join("\n  ", failures)).ToString());
        }

        private static void CheckHidden(SlotReelDock dock, string label, StringBuilder log, List<string> failures)
        {
            var card = WorldRect((RectTransform)dock.TotalCard.transform);
            var clip = WorldRect((RectTransform)dock.transform.parent);
            var below = card.yMax <= clip.yMin + .5f;
            log.AppendLine($" {label}: shown {dock.IsShown}, card top {card.yMax:F1} vs {dock.transform.parent.name} bottom {clip.yMin:F1} (below {below}).");
            if (dock.IsShown || !below) failures.Add($"{label}: the hidden card is not below {dock.transform.parent.name}");
        }

        private static void CheckCard(SlotReelDock dock, string label, StringBuilder log, List<string> failures)
        {
            var card = dock.TotalCard;
            var texts = card.ContentRoot.GetComponentsInChildren<TMP_Text>(true);
            var totalLabel = texts.First(t => t.name == "TotalLabel");
            var value = texts.First(t => t.name == "TotalValue");
            var cardRect = WorldRect((RectTransform)card.transform);
            var machine = WorldRect(dock.ReelMachine);
            var frame = dock.ReelMachine.Find("Frame") as RectTransform;
            var frameRect = frame != null ? WorldRect(frame) : machine;
            var payline = WorldRect(dock.PaylineFrameRect);
            var content = WorldRect(card.ContentRoot);
            var valueRect = WorldRect(value.rectTransform);
            var scale = ScaleOf(card);
            log.AppendLine($" {label}: label active {totalLabel.gameObject.activeSelf} '{totalLabel.text}', value '{value.text}' font {value.fontSize:F1}, " +
                $"card center y {(cardRect.center.y - machine.yMax) / scale:F2} from the machine top, Frame top {(frameRect.yMax - machine.yMax) / scale:F2}, " +
                $"card bottom {(cardRect.yMin - payline.yMax) / scale:F1} above the payline (canvas units).");
            if (totalLabel.gameObject.activeSelf) failures.Add($"{label}: TotalLabel is shown");
            if (value.alignment != TextAlignmentOptions.Center) failures.Add($"{label}: TotalValue alignment {value.alignment}");
            if (Vector2.Distance(valueRect.center, content.center) > .5f || Mathf.Abs(valueRect.width - content.width) > .5f || Mathf.Abs(valueRect.height - content.height) > .5f)
                failures.Add($"{label}: TotalValue {valueRect} does not fill ContentRoot {content}");
            if (Mathf.Abs(cardRect.center.y - machine.yMax) > .5f) failures.Add($"{label}: card center {cardRect.center.y} is not on the machine top {machine.yMax}");
            if (Mathf.Abs(frameRect.yMax - machine.yMax) > .5f) failures.Add($"{label}: Frame top {frameRect.yMax} differs from the machine top {machine.yMax}");
            if (cardRect.yMin < payline.yMax - .5f) failures.Add($"{label}: the card covers the payline ({cardRect.yMin} < {payline.yMax})");
        }

        private static async Task CheckSizes(SlotReelDock dock, StringBuilder log, List<string> failures)
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
                        { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), entry.Width, entry.Height, "Slot total value only probe" });
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    selectedIndex.SetValue(view, BuiltinCount() + CustomCount() - 1);
                    view.Repaint();
                    await TemporarySlotMachinePlayValidation.WaitWhile(() => Screen.width != entry.Width || Screen.height != entry.Height, 5d);
                    await Task.Delay(1000);
                    Canvas.ForceUpdateCanvases();
                    if (Screen.width != entry.Width || Screen.height != entry.Height)
                        failures.Add($"{entry.Name}: Game View is {Screen.width}x{Screen.height}");
                    CheckCard(dock, entry.Name, log, failures);
                    log.AppendLine($"   {Fit(dock.TotalCard, failures, entry.Name)}");
                    ScreenCapture.CaptureScreenshot($"Temp/slot-total-value-only-{entry.Name}.png");
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

        // '999' and '×30': value and multiplier fonts in canvas units, the multiplier inside the card and clear of the value glyphs.
        private static string Fit(SlotTotalCardView card, List<string> failures, string name)
        {
            var texts = card.ContentRoot.GetComponentsInChildren<TMP_Text>(true);
            var value = texts.First(t => t.name == "TotalValue");
            var multiplier = texts.FirstOrDefault(t => t.text == card.TotalMultiplierText && t != value);
            if (multiplier == null) { failures.Add($"{name}: the multiplier text is missing"); return "multiplier missing"; }
            card.TotalMultiplierLayoutForEditor.Refresh();
            var cardRect = WorldRect((RectTransform)card.transform);
            var valueGlyphs = GlyphRect(value);
            var multiplierGlyphs = GlyphRect(multiplier);
            var scale = ScaleOf(card);
            var overflow = value.isTextOverflowing || value.preferredWidth > value.rectTransform.rect.width + .5f;
            var inside = multiplierGlyphs.xMin >= cardRect.xMin - 1f && multiplierGlyphs.xMax <= cardRect.xMax + 1f &&
                multiplierGlyphs.yMin >= cardRect.yMin - 1f && multiplierGlyphs.yMax <= cardRect.yMax + 1f;
            var covers = multiplierGlyphs.Overlaps(valueGlyphs);
            if (overflow) failures.Add($"{name}: '999' overflows ({value.preferredWidth:F0} > {value.rectTransform.rect.width:F0})");
            if (!inside) failures.Add($"{name}: '×30' {multiplierGlyphs} leaves the card {cardRect}");
            if (covers) failures.Add($"{name}: '×30' {multiplierGlyphs} covers the value glyphs {valueGlyphs}");
            var placedBeside = card.TotalMultiplierLayoutForEditor.PlacedBeside;
            return $"card {((RectTransform)card.transform).rect.size}, ContentRoot {card.ContentRoot.rect.size}, canvas scale {scale:F3}; " +
                $"'999' font {value.fontSize:F1} (screen {value.fontSize * scale:F1}px), glyph height {valueGlyphs.height / scale:F1}; " +
                $"'×30' font {multiplier.fontSize:F1} {(placedBeside ? "beside" : "above")}, inside {inside}, covers value {covers}.";
        }

        private static float ScaleOf(Component component) => component.GetComponentInParent<Canvas>().rootCanvas.transform.lossyScale.x;

        private static Rect GlyphRect(TMP_Text text)
        {
            text.ForceMeshUpdate();
            var bounds = text.textBounds;
            var min = text.transform.TransformPoint(bounds.min);
            var max = text.transform.TransformPoint(bounds.max);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
