using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TxTRPG.Application.Players;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.SlotMachine;
using TxTRPG.UI.DebugPanels;
using TxTRPG.UI.Encounters;
using TxTRPG.UI.ScreenFrame;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode check (candidate-main-scene-debug-panel-tabs-and-slot-debug). Enter Play Mode through AppScene so
    // Candidate_MainScene is the content scene, then run the menu. It presses the slot tab's buttons and sliders like a
    // user (onClick / Slider.value) and changes only session state (reels, Fortune rules, the dice ReduceMotion preference,
    // the dock's shown state and the selected tab), which it restores to the Play-start state at the end. Nothing is saved;
    // safe to rerun. Results use the SLOT_DEBUG_TAB_CHECK prefix; two Game View captures go to %TEMP%/TxTRPG-SlotDebugTabCheck. Retire it with TemporaryDebugPanelTabsApply.
    public static class TemporarySlotDebugTabPlayCheck
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Slot Debug Tab Play Check";
        private const string Tag = "SLOT_DEBUG_TAB_CHECK";
        private const float FrameTimeout = 15f;
        private static bool running;

        [MenuItem(MenuPath, false, 3003)]
        public static async void Run()
        {
            if (running) { Debug.LogWarning($"{Tag}: already running."); return; }
            if (!EditorApplication.isPlaying) { Debug.LogError($"{Tag}: FAIL enter Play Mode through AppScene first."); return; }
            var scene = SceneManager.GetSceneByPath(TemporaryDebugPanelTabsApply.ScenePath);
            if (!scene.isLoaded) { Debug.LogError($"{Tag}: FAIL Candidate_MainScene is not the loaded content scene."); return; }
            var host = PlayerSessionHost.Instance;
            var controller = Find<SlotReelDockDebugController>(scene);
            var panel = Find<EncounterDebugPanel>(scene);
            var tabs = panel != null ? panel.GetComponent<DebugPanelTabGroup>() : null;
            if (host == null || controller == null || panel == null || tabs == null || controller.SlotTab == null || controller.Dock == null)
            { Debug.LogError($"{Tag}: FAIL missing session host, controller, debug panel, tab group, slot tab or dock."); return; }
            if (!controller.IsConnected || host.TemporarySlotMachine == null || host.TemporaryFortuneCritical == null)
            { Debug.LogError($"{Tag}: FAIL the controller is not connected to the session."); return; }

            running = true;
            var view = controller.SlotTab;
            var dock = controller.Dock;
            var slots = host.TemporarySlotMachine;
            var fortune = host.TemporaryFortuneCritical;
            var originalReels = slots.Machine.Reels.ToArray();
            var originalRules = fortune.Rules;
            var originalShown = dock.IsShown;
            var originalTab = tabs.SelectedIndex;
            var originalReduceMotion = host.TemporaryDicePreferences.ReduceMotion;
            var encounterValues = panel.CurrentValues;
            var encounterResult = panel.ResultText.text;
            var passed = true;
            try
            {
                // 0. Placement: the clip has the overlay's size and the dock is inside it.
                var clip = dock.transform.parent as RectTransform;
                var overlay = clip != null ? clip.parent as RectTransform : null;
                var dockRect = (RectTransform)dock.transform;
                passed &= Report(0, clip != null && clip.name == TemporaryDebugPanelTabsApply.ClipName && clip.GetComponent<RectMask2D>() != null &&
                    overlay != null && clip.rect.size == overlay.rect.size,
                    $"clip {(clip != null ? clip.rect.size.ToString() : "-")} overlay {(overlay != null ? overlay.rect.size.ToString() : "-")} " +
                    $"dock size {dockRect.rect.size} pos {dockRect.anchoredPosition} shown {dock.ShownPosition} hidden {dock.HiddenOffset}");

                // 1. Start state.
                var title = panel.transform.Find("Header/Title")?.GetComponent<TMPro.TMP_Text>()?.text;
                passed &= Report(1, tabs.SelectedIndex == 0 && tabs.Tabs[0].Page.activeInHierarchy && !tabs.Tabs[1].Page.activeSelf && title == "DEBUG" &&
                    panel.IsValid(out _), $"selected {tabs.SelectedIndex}, title '{title}', encounter values {encounterValues}, result '{encounterResult}'");

                // 2. Slot tab rows follow the session.
                tabs.Tabs[1].Button.onClick.Invoke();
                await NextFrame();
                var rowsMatch = RowsMatch(view, slots, fortune, out var rows);
                passed &= Report(2, tabs.SelectedIndex == 1 && !tabs.Tabs[0].Page.activeSelf && rowsMatch, rows);

                // 3. + up to the maximum, − down to one.
                var stripsCopied = true;
                for (var i = 0; i < 20 && view.PlusButton.interactable; i++)
                {
                    var last = Values(slots.Machine.Reels[slots.ReelCount - 1]);
                    view.PlusButton.onClick.Invoke();
                    stripsCopied &= Values(slots.Machine.Reels[slots.ReelCount - 1]).SequenceEqual(last);
                }
                await NextFrame();
                var atMaximum = $"session {slots.ReelCount}, dock {dock.ReelCount}, rows {view.ActiveRowCount}, + {view.PlusButton.interactable}, page {view.PageLayout.preferredHeight:0.#}";
                var maximumOk = slots.ReelCount == controller.MaximumReelCount && dock.ReelCount == controller.MaximumReelCount && !view.PlusButton.interactable &&
                    stripsCopied;
                // The page shows as much as fits: content height, limited so the panel ends 16 above the frame bottom.
                var scroll = view.GetComponent<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                var contentHeight = scroll != null ? scroll.content.rect.height : 0f;
                var viewportHeight = scroll != null ? scroll.viewport.rect.height : 0f;
                var available = view.AvailablePageHeight();
                var panelRect = (RectTransform)panel.transform;
                var panelParent = (RectTransform)panelRect.parent;
                var panelBottom = WorldRect(panelRect).yMin;
                var parentBottom = WorldRect(panelParent).yMin;
                var marginInUnits = (panelBottom - parentBottom) / panelParent.lossyScale.y;
                var fits = contentHeight <= available + .5f;
                var heightOk = fits
                    ? Mathf.Abs(viewportHeight - contentHeight) <= .5f
                    : Mathf.Abs(viewportHeight - available) <= .5f && marginInUnits >= view.BottomMargin - .5f;
                var sizing = $"content {contentHeight:0.#}, viewport {viewportHeight:0.#}, available {available:0.#}, panel bottom margin {marginInUnits:0.#}, " +
                    (fits ? "all controls visible" : "scrolls");
                await Capture("slot-tab-10-reels");
                for (var i = 0; i < 20 && view.MinusButton.interactable; i++) view.MinusButton.onClick.Invoke();
                await NextFrame();
                passed &= Report(3, maximumOk && heightOk && marginInUnits >= view.BottomMargin - .5f && slots.ReelCount == 1 && dock.ReelCount == 1 && !view.MinusButton.interactable,
                    $"{atMaximum}, strips copied {stripsCopied}, {sizing}; after − session {slots.ReelCount}, − {view.MinusButton.interactable}");

                // 4. Reel 1 and the total at 100 %: both multiply; others at 0 % do not.
                view.PlusButton.onClick.Invoke();
                await NextFrame();
                view.GetRow(0).Slider.value = 100f;
                view.GetRow(1).Slider.value = 0f;
                view.TotalSlider.value = 100f;
                view.SpinButton.onClick.Invoke();
                var spinLimited = !view.SpinButton.interactable && !view.ResetButton.interactable && view.TotalSlider.interactable;
                await WaitUntil(() => !controller.IsSpinning);
                var critical = controller.LastResultMessage;
                var parts = critical.Split(new[] { " → " }, StringSplitOptions.None);
                var reelParts = parts[0].Split(new[] { ", " }, StringSplitOptions.None);
                var criticalOk = reelParts.Length == 2 && reelParts[0].Contains('×') && !reelParts[1].Contains('×') && critical.Contains("→ 합계 ×") &&
                    dock.TotalMultiplierText.StartsWith("×") && critical.Contains($"= {dock.TotalValueText}");
                var criticalCard = $"'{dock.TotalValueText}' '{dock.TotalMultiplierText}'";
                view.GetRow(0).Slider.value = 0f;
                view.TotalSlider.value = 0f;
                view.SpinButton.onClick.Invoke();
                await WaitUntil(() => !controller.IsSpinning);
                var plain = controller.LastResultMessage;
                passed &= Report(4, spinLimited && criticalOk && !plain.Contains('×') && string.IsNullOrEmpty(dock.TotalMultiplierText),
                    $"limits while spinning {spinLimited}; 100%: '{critical}' card {criticalCard}; 0%: '{plain}' card '{dock.TotalValueText}'");

                // 5. Eased hide/show (unscaled time), overshoot, direction change.
                var transition = dock.TransitionBehaviourForEditor as SlotReelDockEasedTransition;
                host.TemporaryDicePreferences.ReduceMotion = false;
                if (!dock.IsShown) { view.ShowButton.onClick.Invoke(); await WaitUntil(() => !dock.IsTransitionRunningForEditor); }
                var hide = await Measure(dock, () => view.HideButton.onClick.Invoke());
                var hiddenOk = dock.PresentationRoot.anchoredPosition == dock.HiddenOffset && Mathf.Approximately(dock.CanvasGroup.alpha, 0f);
                var showMeasured = await Measure(dock, () => view.ShowButton.onClick.Invoke());
                var shownOk = dock.PresentationRoot.anchoredPosition == dock.ShownPosition && Mathf.Approximately(dock.CanvasGroup.alpha, 1f);
                var expectedHide = transition != null ? transition.HideDuration : -1f;
                var expectedShow = transition != null ? transition.ShowDuration : -1f;
                // The transition ends within one frame of its duration; observing the end adds at most one more frame.
                var hideTimed = hide.Seconds >= expectedHide - .001f && hide.Seconds <= expectedHide + 2f * hide.LastFrame + .001f;
                var showTimed = showMeasured.Seconds >= expectedShow - .001f && showMeasured.Seconds <= expectedShow + 2f * showMeasured.LastFrame + .001f;
                var overshoot = hide.MaximumY > dock.ShownPosition.y + .01f;

                view.HideButton.onClick.Invoke();
                await NextFrame(); await NextFrame(); await NextFrame();
                var turn = dock.PresentationRoot.anchoredPosition;
                view.ShowButton.onClick.Invoke();
                await NextFrame();
                var afterTurn = dock.PresentationRoot.anchoredPosition;
                var span = Mathf.Abs(dock.ShownPosition.y - dock.HiddenOffset.y);
                // The new transition starts from the current position (no jump back to either end).
                var turnOk = Mathf.Abs(afterTurn.y - turn.y) < span * .25f;
                await WaitUntil(() => !dock.IsTransitionRunningForEditor);
                passed &= Report(5, transition != null && hiddenOk && shownOk && hideTimed && showTimed && overshoot && turnOk && dock.IsShown,
                    $"hide {hide.Seconds:0.000}s/{expectedHide}s (frame {hide.LastFrame:0.000}) max y {hide.MaximumY:0.#} > shown {dock.ShownPosition.y:0.#}, " +
                    $"end {hiddenOk}; show {showMeasured.Seconds:0.000}s/{expectedShow}s end {shownOk}; turn at {turn.y:0.#} → {afterTurn.y:0.#}");

                // 6. Reduce Motion moves at once.
                host.TemporaryDicePreferences.ReduceMotion = true;
                view.HideButton.onClick.Invoke();
                var instantHide = !dock.IsTransitionRunningForEditor && dock.PresentationRoot.anchoredPosition == dock.HiddenOffset;
                view.ShowButton.onClick.Invoke();
                var instantShow = !dock.IsTransitionRunningForEditor && dock.PresentationRoot.anchoredPosition == dock.ShownPosition;
                host.TemporaryDicePreferences.ReduceMotion = originalReduceMotion;
                passed &= Report(6, instantHide && instantShow, $"hide {instantHide}, show {instantShow}");

                // 7a. Clipping: halfway out of view, every dock graphic is clipped by SlotReelDockClip, which covers the frame.
                var frameController = Find<ScreenFrameController>(scene);
                var clipRect = dock.transform.parent as RectTransform;
                var middle = Vector2.Lerp(dock.ShownPosition, dock.HiddenOffset, .5f);
                dock.PresentationRoot.anchoredPosition = middle;
                await NextFrame();
                await Capture("dock-halfway-hidden");
                // Graphics that cross the clip's bottom edge must be clipped (RectMask2D skips the ones fully inside).
                var bottom = clipRect != null ? WorldRect(clipRect).yMin : 0f;
                var crossing = dock.GetComponentsInChildren<Graphic>(false)
                    .Where(graphic => graphic.enabled && WorldRect(graphic.rectTransform).yMin < bottom - .5f && WorldRect(graphic.rectTransform).yMax > bottom + .5f).ToArray();
                var unclipped = crossing.Where(graphic => !(graphic is MaskableGraphic maskable && maskable.maskable) || !graphic.canvasRenderer.hasRectClipping).ToArray();
                var sameAsFrame = frameController != null && clipRect != null && WorldRect(clipRect) == WorldRect(frameController.FrameRoot);
                dock.PresentationRoot.anchoredPosition = dock.ShownPosition;
                passed &= Report(7, crossing.Length > 0 && unclipped.Length == 0 && sameAsFrame,
                    $"{crossing.Length} graphics cross the frame bottom, unclipped [{string.Join(", ", unclipped.Select(graphic => $"{graphic.name}:{graphic.GetType().Name}"))}]; " +
                    $"clip world rect {(clipRect != null ? WorldRect(clipRect).ToString() : "-")} = frame {sameAsFrame}");

                // 8. Reset restores the configuration values.
                view.PlusButton.onClick.Invoke();
                view.ResetButton.onClick.Invoke();
                var configurationOk = controller.SlotMachineConfigurationForEditor.TryCreateMachine(out var configured, out var reason1) &&
                    controller.FortuneCriticalConfigurationForEditor.TryCreateRules(FortuneWeightFunctionRegistry.Default, out var configuredRules, out var reason2) &&
                    configured.Reels.Select(Signature).SequenceEqual(slots.Machine.Reels.Select(Signature)) && fortune.Rules.Equals(configuredRules);
                rowsMatch = RowsMatch(view, slots, fortune, out rows);
                passed &= Report(8, configurationOk && controller.LastResultMessage == SlotReelDockDebugController.ResetDoneMessage && rowsMatch,
                    $"reels {string.Join(" | ", slots.Machine.Reels.Select(Signature))}; {rows}");
            }
            catch (Exception exception)
            {
                passed = false;
                Debug.LogError($"{Tag}: FAIL {exception}");
            }
            finally
            {
                host.TemporaryDicePreferences.ReduceMotion = originalReduceMotion;
                if (!controller.IsSpinning)
                {
                    slots.Machine.TryReplaceAll(originalReels, out _);
                    fortune.TryReplaceAll(originalRules, out _);
                }
                dock.SetShown(originalShown);
                tabs.Select(originalTab);
                var restored = slots.Machine.Reels.Select(Signature).SequenceEqual(originalReels.Select(Signature)) && fortune.Rules.Equals(originalRules) &&
                    dock.IsShown == originalShown && tabs.SelectedIndex == originalTab && panel.CurrentValues.Equals(encounterValues) && panel.ResultText.text == encounterResult;
                passed &= Report(9, restored, "restored the Play-start reels, rules, dock state, tab and encounter values");
                Debug.Log(passed ? $"{Tag}: PASS all steps" : $"{Tag}: FAIL see the steps above");
                running = false;
            }
        }

        private readonly struct Motion
        {
            public Motion(float seconds, float lastFrame, float maximumY) { Seconds = seconds; LastFrame = lastFrame; MaximumY = maximumY; }
            public float Seconds { get; }
            public float LastFrame { get; }
            public float MaximumY { get; }
        }

        private static async Task<Motion> Measure(SlotReelDock dock, Action start)
        {
            var begin = Time.unscaledTime;
            var maximum = dock.PresentationRoot.anchoredPosition.y;
            var lastFrame = 0f;
            start();
            while (dock.IsTransitionRunningForEditor && Time.unscaledTime - begin < FrameTimeout)
            {
                await NextFrame();
                lastFrame = Time.unscaledDeltaTime;
                maximum = Mathf.Max(maximum, dock.PresentationRoot.anchoredPosition.y);
            }
            return new Motion(Time.unscaledTime - begin, lastFrame, maximum);
        }

        private static bool RowsMatch(SlotDebugTabView view, TemporarySlotMachineState slots, TemporaryFortuneCriticalState fortune, out string detail)
        {
            var builder = new StringBuilder($"rows {view.ActiveRowCount}/session {slots.ReelCount}:");
            var ok = view.ActiveRowCount == slots.ReelCount;
            for (var i = 0; ok && i < slots.ReelCount; i++)
            {
                var row = view.GetRow(i);
                var reel = slots.Machine.Reels[i];
                var expected = Mathf.Round((float)fortune.Rules.RuleForReel(reel.Id).Chance * 100f);
                ok &= row.ReelId == reel.Id && Mathf.Approximately(row.Slider.value, expected) && row.Label.text == SlotReelDockDebugController.ReelLabel(i);
                builder.Append($" {row.Label.text}={row.Value.text}");
            }
            var total = Mathf.Round((float)fortune.Rules.TotalRule.Chance * 100f);
            ok &= Mathf.Approximately(view.TotalSlider.value, total);
            builder.Append($" total={view.TotalValue.text}");
            detail = builder.ToString();
            return ok;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static int[] Values(SlotReel reel) => reel.Strip.Select(symbol => symbol.Value).ToArray();
        private static string Signature(SlotReel reel) => $"{reel.Id}:{string.Join(",", Values(reel))}";

        private static bool Report(int step, bool ok, string detail)
        {
            var message = $"{Tag}: step {step} {(ok ? "PASS" : "FAIL")} {detail}";
            if (ok) Debug.Log(message); else Debug.LogError(message);
            return ok;
        }

        private static async Task NextFrame() => await Task.Yield();

        // Game View captures for a visual check, outside Assets (%TEMP%/TxTRPG-SlotDebugTabCheck).
        private static async Task Capture(string name)
        {
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TxTRPG-SlotDebugTabCheck");
            System.IO.Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, $"{name}-{Screen.width}x{Screen.height}.png"));
            await NextFrame();
            await NextFrame();
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            var begin = Time.unscaledTime;
            while (!condition())
            {
                if (Time.unscaledTime - begin > FrameTimeout) throw new TimeoutException("Timed out waiting in Play Mode.");
                await NextFrame();
            }
        }

        private static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<T>(true)).FirstOrDefault(found => found != null);
    }
}
