using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TxTRPG.Application.Runes;
using TxTRPG.Gameplay.Runes;
using TxTRPG.UI.Runes;
using TxTRPG.UI.Tooltips;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode probe (rune-tooltip-and-test-runes). Enter Play Mode through AppScene so Candidate_MainScene is the
    // content scene, then run the menu. Through TemporaryTestRuneDebugController it adds the three test runes, then for every
    // card injects pointer enters at three points of the card (the tooltip position must not change and its top-left must be
    // CardArea's top-right unless an axis flipped), checks the box stays inside the screen frame, that rune.test.long is
    // flipped or pushed, card → box keeps and outside closes, the keyword chain opens to depth 3 without repeating a
    // keyword, and that the allocation (line IDs, order, amounts) is the same with and without the test runes. It repeats
    // the frame check at six Game View sizes (temporary Standalone sizes removed by their total index; captures
    // Temp/rune-tooltip-*.png). At the end every test rune is removed again; nothing is saved. Results use the
    // RUNE_TOOLTIP_PROBE prefix. Pointer injection is not a real mouse: check the chain with the mouse once too. Remove the
    // menu once the feature is verified.
    public static class TemporaryRuneTooltipPlayProbe
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Probe Rune Tooltips";
        private const string Prefix = "RUNE_TOOLTIP_PROBE";
        private const int Wait = 250;

        private static readonly (string Name, int Width, int Height)[] Sizes =
        {
            ("1920x1080", 1920, 1080), ("1280x720", 1280, 720), ("2560x1080", 2560, 1080),
            ("2400x1080", 2400, 1080), ("1024x768", 1024, 768), ("2048x1536", 2048, 1536)
        };

        private static bool running;

        [MenuItem(MenuPath, false, 3004)]
        public static async void Run()
        {
            if (running) { Debug.LogWarning($"{Prefix}: already running."); return; }
            running = true;
            var log = new StringBuilder($"{Prefix}:\n");
            var failures = new List<string>();
            TemporaryTestRuneDebugController controller = null;
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode through AppScene first.");
                controller = Object.FindAnyObjectByType<TemporaryTestRuneDebugController>() ?? throw new InvalidOperationException("TemporaryTestRuneDebugController is missing.");
                await TemporarySlotMachinePlayValidation.WaitWhile(() => !controller.IsConnected, 5d);
                if (!controller.IsConnected) throw new InvalidOperationException("The test rune controller is not connected to the session.");
                var grid = Object.FindAnyObjectByType<RuneGridPanel>() ?? throw new InvalidOperationException("RuneGridPanel is missing.");
                var presenter = grid.TooltipPresenter ?? throw new InvalidOperationException("RuneGridPanel has no TooltipPresenter.");
                var runes = controller.Runes;
                log.AppendLine($" test definitions: {string.Join(", ", controller.TestDefinitionIds)}");
                if (controller.TestDefinitionIds.Count != 3) failures.Add($"expected 3 test definitions, found {controller.TestDefinitionIds.Count}");

                var before = Allocations(runes);
                var cardsBefore = grid.ActiveCardCount;
                foreach (var id in controller.TestDefinitionIds)
                    if (!controller.TryAdd(id, out var message)) failures.Add($"add {id}: {message}");
                // A test line placed first must not change the allocation either.
                MoveFirstTestLineToFront(runes, failures);
                await Task.Delay(Wait);
                log.AppendLine($" cards {cardsBefore} → {grid.ActiveCardCount}.");
                if (grid.ActiveCardCount != cardsBefore + 3) failures.Add($"card count {grid.ActiveCardCount}, expected {cardsBefore + 3}");
                var after = Allocations(runes);
                if (!after.SequenceEqual(before)) failures.Add($"allocation changed with test runes:\n    {string.Join("\n    ", before)}\n  vs\n    {string.Join("\n    ", after)}");
                else log.AppendLine($" allocation unchanged with test runes ({before.Count} totals, CanAllocate {runes.CanAllocate}).");

                CheckCards(grid, presenter, "1920x1080 start", log, failures);
                await CheckKeepAndClose(grid, presenter, log, failures);
                await CheckKeywordChain(grid, presenter, log, failures);
                await CheckSizes(grid, presenter, log, failures);
            }
            catch (Exception exception)
            {
                failures.Add(exception is InvalidOperationException ? exception.Message : exception.ToString());
            }
            finally
            {
                if (controller != null && controller.IsConnected)
                {
                    controller.TryRemoveAll(out var message);
                    log.AppendLine($" cleanup: {message}");
                }
                running = false;
            }
            if (failures.Count == 0) Debug.Log(log.Append(" PASS").ToString());
            else Debug.LogError(log.Append(" FAIL:\n  ").Append(string.Join("\n  ", failures)).ToString());
        }

        private static List<string> Allocations(TemporaryRuneState runes) =>
            Enumerable.Range(0, 31).Select(total => string.Join(" ", runes.Allocate(total).Lines.Select(l => $"{l.Order}:{l.LineId}={l.Amount}"))).ToList();

        private static void MoveFirstTestLineToFront(TemporaryRuneState runes, List<string> failures)
        {
            var line = runes.Loadout.Lines.FirstOrDefault(l => l.LineId.StartsWith(TemporaryTestRuneDebugController.TestLinePrefix, StringComparison.Ordinal));
            if (line == null) { failures.Add("no test line to move"); return; }
            var lines = runes.Loadout.Lines.Where(l => l != line).Prepend(line).ToArray();
            if (!runes.Loadout.TryReplaceAll(lines, out var reason)) failures.Add($"move test line: {reason}");
        }

        private static IEnumerable<RuneGridCardTooltipTrigger> Triggers(RuneGridPanel grid) =>
            grid.Cards.Take(grid.ActiveCardCount).Select(card => card.TooltipTrigger).Where(trigger => trigger != null);

        // A plain PointerEventData is never a touch pointer, like the mouse.
        private static PointerEventData Pointer(Vector2 screen) => new(EventSystem.current) { position = screen };

        private static Vector2 ScreenPoint(RectTransform rect, Vector2 normalized)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return RectTransformUtility.WorldToScreenPoint(null, Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], normalized.x), Vector3.Lerp(corners[1], corners[2], normalized.x), normalized.y));
        }

        private static void Exit(RuneGridCardTooltipTrigger trigger) => trigger.OnPointerExit(new PointerEventData(EventSystem.current));

        // Every card: three entry points, one position; top-left on CardArea's top-right unless flipped; inside the frame.
        private static void CheckCards(RuneGridPanel grid, TooltipPresenter presenter, string label, StringBuilder log, List<string> failures)
        {
            foreach (var trigger in Triggers(grid))
            {
                var positions = new List<Vector2>();
                foreach (var point in new[] { new Vector2(.05f, .95f), new Vector2(.5f, .5f), new Vector2(.95f, .05f) })
                {
                    presenter.HideAll();
                    trigger.OnPointerEnter(Pointer(ScreenPoint(trigger.Card.CardArea, point)));
                    var view = presenter.GetView(0);
                    if (presenter.OpenCount != 1 || view == null) { failures.Add($"{label} {trigger.DefinitionId}: no tooltip"); break; }
                    positions.Add(TopLeft(view, presenter.Space));
                }
                if (positions.Count != 3) continue;
                if (positions.Any(p => Vector2.Distance(p, positions[0]) > .01f)) failures.Add($"{label} {trigger.DefinitionId}: position depends on the entry point ({string.Join(", ", positions)})");
                trigger.TryGetAnchorRect(presenter.Space, out var anchor);
                var placement = presenter.GetPlacement(0);
                var box = Bounds(presenter.GetView(0).Rect, presenter.Space);
                var frame = Bounds(presenter.Frame, presenter.Space);
                var margin = presenter.FrameMargin;
                if (!placement.FlippedX && !placement.PushedX && Mathf.Abs(box.xMin - anchor.xMax) > .5f) failures.Add($"{label} {trigger.DefinitionId}: left {box.xMin} ≠ card right {anchor.xMax}");
                if (!placement.FlippedY && !placement.PushedY && Mathf.Abs(box.yMax - anchor.yMax) > .5f) failures.Add($"{label} {trigger.DefinitionId}: top {box.yMax} ≠ card top {anchor.yMax}");
                var inside = box.xMin >= frame.xMin + margin - .5f && box.xMax <= frame.xMax - margin + .5f && box.yMin >= frame.yMin + margin - .5f && box.yMax <= frame.yMax - margin + .5f;
                if (!inside) failures.Add($"{label} {trigger.DefinitionId}: box {box} leaves the frame {frame}");
                if (trigger.DefinitionId == TemporaryTestRuneConfiguration.LongRuneId && !placement.FlippedY && !placement.PushedY)
                    failures.Add($"{label} rune.test.long was neither flipped nor pushed vertically");
                log.AppendLine($" {label} {trigger.DefinitionId,-18} box {box.size.x:F0}×{box.size.y:F0} at ({box.xMin:F0},{box.yMax:F0}), card right-top ({anchor.xMax:F0},{anchor.yMax:F0}), " +
                    $"flip {placement.FlippedX}/{placement.FlippedY}, push {placement.PushedX}/{placement.PushedY}, inside {inside}.");
                Exit(trigger);
            }
            presenter.HideAll();
        }

        private static async Task CheckKeepAndClose(RuneGridPanel grid, TooltipPresenter presenter, StringBuilder log, List<string> failures)
        {
            var trigger = Triggers(grid).First();
            presenter.HideAll();
            trigger.OnPointerEnter(Pointer(ScreenPoint(trigger.Card.CardArea, new Vector2(.5f, .5f))));
            var view = presenter.GetView(0);
            Exit(trigger);
            view.SetPointerOverForTests(true);
            await Task.Delay(Wait);
            var kept = presenter.OpenCount == 1;
            view.SetPointerOverForTests(false);
            await Task.Delay(Wait);
            var closed = presenter.OpenCount == 0;
            log.AppendLine($" card → box kept {kept}; outside the chain closed {closed}.");
            if (!kept) failures.Add("moving from the card to the box closed the tooltip");
            if (!closed) failures.Add("leaving the chain did not close the tooltip");

            trigger.OnPointerEnter(Pointer(ScreenPoint(trigger.Card.CardArea, new Vector2(.5f, .5f))));
            var scroll = grid.ScrollRect;
            var scrollable = scroll.content.rect.height > scroll.viewport.rect.height + 1f;
            if (scrollable)
            {
                scroll.verticalNormalizedPosition = scroll.verticalNormalizedPosition > .5f ? 0f : 1f;
                await Task.Delay(Wait);
                log.AppendLine($" scroll closes the tooltip {presenter.OpenCount == 0}.");
                if (presenter.OpenCount != 0) failures.Add("scrolling the rune grid did not close the tooltip");
                scroll.verticalNormalizedPosition = 1f;
            }
            else log.AppendLine(" rune grid not scrollable at this size; scroll close not checked.");
            Exit(trigger);
            presenter.HideAll();
            await Task.Delay(Wait);
        }

        private static async Task CheckKeywordChain(RuneGridPanel grid, TooltipPresenter presenter, StringBuilder log, List<string> failures)
        {
            var trigger = Triggers(grid).FirstOrDefault(t => t.DefinitionId == TemporaryTestRuneConfiguration.TooltipRuneId);
            if (trigger == null) { failures.Add("no rune.test.tooltip card"); return; }
            presenter.HideAll();
            trigger.OnPointerEnter(Pointer(ScreenPoint(trigger.Card.CardArea, new Vector2(.5f, .5f))));
            var opened = new List<string>();
            for (var depth = 0; depth < 4; depth++)
            {
                var view = presenter.GetView(depth);
                var detector = view?.ActiveTextBlocks.FirstOrDefault(d => { d.Text.ForceMeshUpdate(); return d.Text.textInfo.linkCount > 0; });
                if (detector == null) break;
                view.SetPointerOverForTests(true);
                detector.SetPointerLinkForTests(detector.Text.textInfo.linkInfo[0].GetLinkID());
                await Task.Delay(Wait);
                if (presenter.OpenCount > depth + 1) opened.Add(presenter.GetKeywordId(depth + 1));
            }
            var expected = new[] { TemporaryRuneTooltipApply.AlphaKeywordId, TemporaryRuneTooltipApply.BetaKeywordId, TemporaryRuneTooltipApply.GammaKeywordId };
            log.AppendLine($" keyword chain: {string.Join(" → ", opened)} (open {presenter.OpenCount}, maximum depth {presenter.MaximumKeywordDepth}).");
            if (!opened.SequenceEqual(expected)) failures.Add($"keyword chain {string.Join(" → ", opened)}, expected {string.Join(" → ", expected)}");
            if (presenter.OpenCount != 4) failures.Add($"open tooltips {presenter.OpenCount}, expected 4 (gamma links alpha, already open)");
            for (var depth = 0; depth < presenter.OpenCount; depth++)
            {
                var box = Bounds(presenter.GetView(depth).Rect, presenter.Space);
                var frame = Bounds(presenter.Frame, presenter.Space);
                if (box.xMin < frame.xMin || box.xMax > frame.xMax || box.yMin < frame.yMin || box.yMax > frame.yMax) failures.Add($"keyword tooltip {depth} {box} leaves the frame");
            }
            Exit(trigger);
            presenter.HideAll();
        }

        private static async Task CheckSizes(RuneGridPanel grid, TooltipPresenter presenter, StringBuilder log, List<string> failures)
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
                        { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), entry.Width, entry.Height, "Rune tooltip probe" });
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    selectedIndex.SetValue(view, BuiltinCount() + CustomCount() - 1);
                    view.Repaint();
                    await TemporarySlotMachinePlayValidation.WaitWhile(() => Screen.width != entry.Width || Screen.height != entry.Height, 5d);
                    await Task.Delay(1000);
                    Canvas.ForceUpdateCanvases();
                    if (Screen.width != entry.Width || Screen.height != entry.Height) failures.Add($"{entry.Name}: Game View is {Screen.width}x{Screen.height}");
                    CheckCards(grid, presenter, entry.Name, log, failures);
                    var longTrigger = Triggers(grid).FirstOrDefault(t => t.DefinitionId == TemporaryTestRuneConfiguration.LongRuneId);
                    if (longTrigger != null)
                    {
                        longTrigger.OnPointerEnter(Pointer(ScreenPoint(longTrigger.Card.CardArea, new Vector2(.5f, .5f))));
                        await Task.Delay(100);
                        ScreenCapture.CaptureScreenshot($"Temp/rune-tooltip-{entry.Name}.png");
                        await Task.Delay(400);
                        Exit(longTrigger);
                        presenter.HideAll();
                    }
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

        private static Vector2 TopLeft(TooltipView view, RectTransform space)
        {
            var bounds = Bounds(view.Rect, space);
            return new Vector2(bounds.xMin, bounds.yMax);
        }

        private static Rect Bounds(RectTransform rect, RectTransform space)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = space.InverseTransformPoint(corners[0]);
            Vector2 max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }
    }
}
