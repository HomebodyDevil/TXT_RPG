using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TxTRPG.Application.Runes;
using TxTRPG.UI.Runes;
using TxTRPG.UI.Tooltips;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode probe (rune-tooltip-and-test-runes). Unlike Probe Rune Tooltips, it drives the Input System mouse
    // (queued Mouse state events, Game View pixels), so the EventSystem raycast, the card HitAreas, the tooltip box and
    // the keyword link detection run their real pointer paths: card hover opens, card → box keeps, box → keyword opens
    // the keyword tooltip, a point outside closes, and a wheel over a card scrolls the rune grid when it can scroll. It is
    // still not a physical mouse. Adds rune.test.tooltip and removes it again; nothing is saved. Prefix
    // RUNE_TOOLTIP_MOUSE_PROBE. Remove once the feature is verified.
    public static class TemporaryRuneTooltipMouseProbe
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Probe Rune Tooltips With Input System Mouse";
        private const string Prefix = "RUNE_TOOLTIP_MOUSE_PROBE";
        private static bool running;

        [MenuItem(MenuPath, false, 3005)]
        public static async void Run()
        {
            if (running) return;
            running = true;
            var log = new StringBuilder($"{Prefix}:\n");
            var failures = new List<string>();
            TemporaryTestRuneDebugController controller = null;
            Mouse mouse = null;
            var addedMouse = false;
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode through AppScene first.");
                controller = Object.FindAnyObjectByType<TemporaryTestRuneDebugController>() ?? throw new InvalidOperationException("TemporaryTestRuneDebugController is missing.");
                var grid = Object.FindAnyObjectByType<RuneGridPanel>() ?? throw new InvalidOperationException("RuneGridPanel is missing.");
                var presenter = grid.TooltipPresenter ?? throw new InvalidOperationException("RuneGridPanel has no TooltipPresenter.");
                mouse = Mouse.current;
                if (mouse == null) { mouse = InputSystem.AddDevice<Mouse>(); addedMouse = true; }
                await CheckRuneTab(mouse, controller, grid, log, failures);
                if (!controller.TryAdd(TemporaryTestRuneConfiguration.TooltipRuneId, out var message)) throw new InvalidOperationException(message);
                await Task.Delay(300);
                var trigger = grid.Cards.Take(grid.ActiveCardCount).Select(c => c.TooltipTrigger)
                    .First(t => t != null && t.DefinitionId == TemporaryTestRuneConfiguration.TooltipRuneId);

                await MoveTo(mouse, new Vector2(Screen.width * .6f, Screen.height * .5f));
                await MoveTo(mouse, Center(trigger.Card.CardArea));
                Check(presenter.OpenCount == 1 && trigger.IsPointerOver, $"card hover opens (open {presenter.OpenCount}, over {trigger.IsPointerOver})", log, failures);
                await MoveTo(mouse, Center(trigger.Card.BadgeArea));
                Check(presenter.OpenCount == 1, $"card → badge keeps (open {presenter.OpenCount})", log, failures);

                var view = presenter.GetView(0);
                var detector = view.ActiveTextBlocks.First(d => { d.Text.ForceMeshUpdate(); return d.Text.textInfo.linkCount > 0; });
                await MoveTo(mouse, Center(view.Background.rectTransform, .9f, .05f));
                Check(presenter.OpenCount == 1 && view.IsPointerOver, $"card → box keeps (open {presenter.OpenCount}, box over {view.IsPointerOver})", log, failures);
                await MoveTo(mouse, LinkCenter(detector));
                await Task.Delay(300);
                Check(presenter.OpenCount == 2 && presenter.GetKeywordId(1) == TemporaryRuneTooltipApply.AlphaKeywordId,
                    $"keyword hover opens '{presenter.GetKeywordId(1)}' (open {presenter.OpenCount}, link '{detector.CurrentLinkId}')", log, failures);
                if (presenter.OpenCount == 2)
                {
                    await MoveTo(mouse, Center(presenter.GetView(1).Background.rectTransform, .5f, .1f));
                    Check(presenter.OpenCount == 2, $"keyword → keyword box keeps (open {presenter.OpenCount})", log, failures);
                }
                await MoveTo(mouse, new Vector2(Screen.width * .6f, Screen.height * .5f));
                await Task.Delay(200);
                Check(presenter.OpenCount == 0, $"outside the chain closes (open {presenter.OpenCount})", log, failures);

                var scroll = grid.ScrollRect;
                var scrollable = scroll.content.rect.height > scroll.viewport.rect.height + 1f;
                await MoveTo(mouse, Center(trigger.Card.CardArea));
                var before = scroll.verticalNormalizedPosition;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Center(trigger.Card.CardArea), scroll = new Vector2(0f, -120f) });
                await Task.Delay(300);
                log.AppendLine($" wheel over a card: scrollable {scrollable}, position {before:F2} → {scroll.verticalNormalizedPosition:F2}, open {presenter.OpenCount}.");
                if (scrollable && Mathf.Approximately(before, scroll.verticalNormalizedPosition)) failures.Add("the wheel over a card did not scroll the rune grid");
                await MoveTo(mouse, new Vector2(Screen.width * .6f, Screen.height * .5f));
            }
            catch (Exception exception)
            {
                failures.Add(exception is InvalidOperationException ? exception.Message : exception.ToString());
            }
            finally
            {
                if (controller != null && controller.IsConnected) controller.TryRemoveAll(out _);
                if (addedMouse && mouse != null) InputSystem.RemoveDevice(mouse);
                running = false;
            }
            if (failures.Count == 0) Debug.Log(log.Append(" PASS").ToString());
            else Debug.LogError(log.Append(" FAIL:\n  ").Append(string.Join("\n  ", failures)).ToString());
        }

        // DEBUG panel rune tab through mouse clicks: select the tab, + on the first row twice, − once, 모두 제거; then back to
        // the first tab.
        private static async Task CheckRuneTab(Mouse mouse, TemporaryTestRuneDebugController controller, RuneGridPanel grid, StringBuilder log, List<string> failures)
        {
            var tab = controller.RuneTab;
            var group = tab.GetComponentInParent<TxTRPG.UI.DebugPanels.DebugPanelTabGroup>();
            var previous = group.SelectedIndex;
            var runeTabIndex = group.Tabs.ToList().FindIndex(t => t.Page == tab.gameObject);
            await Click(mouse, Center((RectTransform)group.Tabs[runeTabIndex].Button.transform));
            Check(group.SelectedIndex == runeTabIndex && tab.gameObject.activeInHierarchy, $"rune tab selected by click (index {group.SelectedIndex})", log, failures);
            var row = tab.GetRow(0);
            var id = row.DefinitionId;
            var cards = grid.ActiveCardCount;
            await Click(mouse, Center((RectTransform)row.PlusButton.transform));
            await Click(mouse, Center((RectTransform)row.PlusButton.transform));
            Check(controller.CountOf(id) == 2 && row.CountText.text == "2" && grid.ActiveCardCount == cards + 1,
                $"+ twice: {id} count {controller.CountOf(id)}, row '{row.CountText.text}', cards {cards} → {grid.ActiveCardCount}", log, failures);
            await Click(mouse, Center((RectTransform)row.MinusButton.transform));
            Check(controller.CountOf(id) == 1 && row.CountText.text == "1", $"− once: count {controller.CountOf(id)}, result '{tab.ResultText.text}'", log, failures);
            await Click(mouse, Center((RectTransform)tab.RemoveAllButton.transform));
            Check(controller.CountOf(id) == 0 && grid.ActiveCardCount == cards, $"모두 제거: count {controller.CountOf(id)}, cards {grid.ActiveCardCount}, result '{tab.ResultText.text}'", log, failures);
            await Click(mouse, Center((RectTransform)group.Tabs[previous].Button.transform));
            Check(group.SelectedIndex == previous, $"back to tab {previous}", log, failures);
        }

        private static async Task Click(Mouse mouse, Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            await Task.Delay(100);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            await Task.Delay(100);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            await Task.Delay(250);
        }

        private static void Check(bool ok, string text, StringBuilder log, List<string> failures)
        {
            log.AppendLine($" {text}: {(ok ? "ok" : "FAILED")}");
            if (!ok) failures.Add(text);
        }

        private static async Task MoveTo(Mouse mouse, Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            await Task.Delay(250);
        }

        private static Vector2 Center(RectTransform rect, float x = .5f, float y = .5f)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var point = Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], x), Vector3.Lerp(corners[1], corners[2], x), y);
            return RectTransformUtility.WorldToScreenPoint(null, point);
        }

        private static Vector2 LinkCenter(TooltipKeywordLinkDetector detector)
        {
            var info = detector.Text.textInfo;
            var link = info.linkInfo[0];
            var character = info.characterInfo[link.linkTextfirstCharacterIndex + link.linkTextLength / 2];
            var local = new Vector3((character.bottomLeft.x + character.topRight.x) * .5f, (character.descender + character.ascender) * .5f);
            return RectTransformUtility.WorldToScreenPoint(null, detector.Text.rectTransform.TransformPoint(local));
        }
    }
}
