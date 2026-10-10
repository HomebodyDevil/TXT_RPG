using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Exploration;
using TxTRPG.Application.Players;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.SlotMachine;
using TxTRPG.SceneTransition;
using TxTRPG.UI;
using TxTRPG.UI.Dice;
using TxTRPG.UI.SlotMachine;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode verification for slot-machine-and-rune-combat. Enter Play Mode through AppScene with a fresh
    // exploration run (awaiting a node choice), then run the menu. It drives Submit, pointer click, Reduce Motion, a game
    // window and a Scene reload during a spin, runtime reel changes and several viewports, then finishes the combat.
    // Results are logged with the SLOT_MACHINE_PLAY_VALIDATE prefix. It changes only session (Play Mode) state.
    public static class TemporarySlotMachinePlayValidation
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Validate Slot Machine Combat (Play Mode)";

        // Registered content scene used to leave and re-enter TMP_MainScene during a spin.
        private const string OtherContentScene = "Assets/Scenes/MainScene_VisualProposal.unity";

        private static readonly (string Name, Vector2 Size)[] Viewports =
        {
            ("desktop 1920x1080", new Vector2(1920f, 1080f)),
            ("ultrawide 21:9", new Vector2(2520f, 1080f)),
            ("tablet 1024x768", new Vector2(1024f, 768f)),
            ("phone 390x844", new Vector2(390f, 844f))
        };

        [MenuItem(MenuPath)]
        public static async void Validate()
        {
            var log = new StringBuilder("SLOT_MACHINE_PLAY_VALIDATE:\n");
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode from AppScene first.");
                var host = PlayerSessionHost.Instance ?? throw new InvalidOperationException("The player session is missing.");
                var stories = 0;
                host.TemporarySlotStoryReady += _ => stories++;
                var refs = Find();
                if (refs.Run.Run == null || refs.Run.Run.Phase != ExplorationPhase.AwaitingChoice)
                    throw new InvalidOperationException("Validation requires a fresh awaiting-choice exploration run.");

                // 1. Before combat: the dice are hidden, the slot machine shows three reels, the idle total and the weights.
                var dicePanel = Object.FindAnyObjectByType<OwnedDicePanel>(FindObjectsInactive.Include);
                Require(dicePanel == null || !dicePanel.gameObject.activeInHierarchy, "OwnedDicePanel is not shown");
                var stageCamera = dicePanel != null ? new SerializedObject(dicePanel).FindProperty("stageCamera").objectReferenceValue as Camera : null;
                Require(stageCamera == null || !stageCamera.enabled, "the dice stage camera does not render");
                var diceController = Object.FindAnyObjectByType<TemporaryDiceRollMenuController>(FindObjectsInactive.Include);
                Require(diceController == null || !diceController.enabled && !diceController.IsReady, "the reserved dice controller takes no part");
                Require(refs.Panel.ReelCount == 3, $"three reels ({refs.Panel.ReelCount})");
                Require(refs.Panel.TotalText == SlotCombatStoryFormatter.IdleTotal, $"idle total ({refs.Panel.TotalText})");
                Require(refs.Panel.AllocationText == "공격 ×1 · 회복 ×1", $"weights shown ({refs.Panel.AllocationText})");
                Require(!refs.Action.IsShown, "the action button is hidden while choosing a node");
                Require(!refs.Panel.GetComponent<CanvasGroup>().interactable, "node selection blocks the slot machine panel");
                log.AppendLine(" before combat: dice hidden, 3 reels, '합계 —', weights shown, panel blocked by node selection.");

                StartCombat(refs.Run);
                await Frames(3);
                Require(refs.Action.IsShown && refs.Action.IsInteractable, $"the action is available in combat ({refs.Action.UnavailableReason})");
                Require(refs.Panel.GetComponent<CanvasGroup>().interactable, "the panel is not blocked during combat");

                // 2. Layout at several viewports.
                await CheckViewports(refs, log);

                // 3. Submit (keyboard / controller), twice in one frame, with a game window opened and closed mid-spin.
                EventSystem.current.SetSelectedGameObject(refs.Action.Button.gameObject);
                await Frames(1);
                var enemyBefore = refs.Controller.Combat.EnemyHealth.Current;
                var watch = Stopwatch.StartNew();
                Submit(refs.Action); Submit(refs.Action);
                Require(host.IsTemporaryCombatActionPending && host.PendingSlotAction != null, "Submit accepted the spin");
                var accepted = host.PendingSlotAction;
                await Frames(2);
                Require(refs.Panel.IsSpinning, "the reels spin");
                Require(refs.Action.IsShown && !refs.Action.IsInteractable, "the button is visible but disabled while spinning");
                Require(refs.Panel.TotalText == SlotCombatStoryFormatter.SpinningTotal, $"spinning text ({refs.Panel.TotalText})");
                _ = refs.Windows.OpenAsync(GamePageIds.Inventory);
                await Frames(3);
                refs.Windows.Close();
                await WaitWhile(() => host.IsTemporaryCombatActionPending, 10d);
                watch.Stop();
                Require(stories == 1, $"two Submits and a window during the spin applied exactly one turn ({stories})");
                CheckResult(refs, host, accepted, log, "submit");
                var attack = accepted.Allocation.Lines.Where(l => l.EffectKind == TxTRPG.Gameplay.Runes.RuneEffectKind.Attack).Sum(l => l.Amount);
                // The first enemy action is an attack, so the enemy health changes only by the attack lines.
                Require(refs.Controller.Combat.EnemyHealth.Current == Math.Max(0, enemyBefore - attack), "the attack lines hit the enemy once");
                log.AppendLine($" submit: {SlotCombatStoryFormatter.Spin(accepted.Spin)}, {SlotCombatStoryFormatter.Allocation(accepted.Allocation)}, presentation {watch.Elapsed.TotalSeconds:F2}s, window open/close mid-spin, one turn.");

                // 4. Pointer click (mouse; touch uses the same pointer events on the uGUI button).
                await WaitWhile(() => !refs.Action.IsInteractable && !refs.Controller.Combat.IsComplete, 5d);
                if (!refs.Controller.Combat.IsComplete)
                {
                    var clickData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute(refs.Action.Button.gameObject, clickData, ExecuteEvents.pointerClickHandler);
                    Require(host.PendingSlotAction != null, "a pointer click accepted the spin");
                    accepted = host.PendingSlotAction;
                    await WaitWhile(() => host.IsTemporaryCombatActionPending, 10d);
                    Require(stories == 2, "pointer click applied one turn");
                    CheckResult(refs, host, accepted, log, "pointer click");
                }

                // 5. Reduce Motion: the reels show their results almost at once, ending in the same state.
                if (!refs.Controller.Combat.IsComplete)
                {
                    host.TemporaryDicePreferences.ReduceMotion = true;
                    try
                    {
                        await WaitWhile(() => !refs.Action.IsInteractable, 5d);
                        watch.Restart();
                        refs.Action.Button.onClick.Invoke();
                        accepted = host.PendingSlotAction;
                        await WaitWhile(() => host.IsTemporaryCombatActionPending, 10d);
                        watch.Stop();
                        Require(watch.Elapsed.TotalSeconds < 1.2d, $"Reduce Motion is short ({watch.Elapsed.TotalSeconds:F2}s)");
                        CheckResult(refs, host, accepted, log, $"reduce motion {watch.Elapsed.TotalSeconds:F2}s");
                    }
                    finally { host.TemporaryDicePreferences.ReduceMotion = false; }
                }

                // 6. Runtime configuration: 4 and 5 reels and a changed cell, from the next spin on.
                if (!refs.Controller.Combat.IsComplete)
                {
                    var machine = host.TemporarySlotMachine.Machine;
                    Require(machine.TryAddReel(SlotReel.FromValues("reel.4", new[] { 1, 2, 3, 4, 5, 6 }), out var reason), reason);
                    await Frames(2);
                    Require(refs.Panel.ReelCount == 4, "four reels shown");
                    Require(machine.TryAddReel(SlotReel.FromValues("reel.5", new[] { 7, 8, 9 }), out reason), reason);
                    Require(machine.TrySetSymbol("reel.1", 0, 12, out reason), reason);
                    await Frames(2);
                    Require(refs.Panel.ReelCount == 5, "five reels shown");
                    CheckReelLayout(refs, "five reels");
                    await WaitWhile(() => !refs.Action.IsInteractable, 5d);
                    refs.Action.Button.onClick.Invoke();
                    accepted = host.PendingSlotAction;
                    Require(accepted.Spin.Reels.Count == 5, "the next spin uses five reels");
                    await WaitWhile(() => host.IsTemporaryCombatActionPending, 10d);
                    CheckResult(refs, host, accepted, log, "five reels");
                    Require(machine.TryRemoveReel("reel.5", out reason) && machine.TryRemoveReel("reel.4", out reason) &&
                        machine.TrySetSymbol("reel.1", 0, 1, out reason), reason);
                    await Frames(2);
                    Require(refs.Panel.ReelCount == 3, "back to three reels");
                }

                // 7. Scene reload during a spin: the accepted result is applied once and shown by the new Scene.
                if (!refs.Controller.Combat.IsComplete)
                {
                    var transition = Object.FindAnyObjectByType<SceneTransitionService>();
                    if (transition == null) log.AppendLine(" scene reload: SceneTransitionService not found; not verified.");
                    else
                    {
                        await WaitWhile(() => !refs.Action.IsInteractable, 5d);
                        var before = stories;
                        var combat = refs.Controller.Combat;
                        var enemy = combat.EnemyHealth.Current;
                        refs.Action.Button.onClick.Invoke();
                        accepted = host.PendingSlotAction;
                        // The transition service does not reload the active content scene, so the spin leaves through
                        // another registered content scene and comes back to TMP_MainScene.
                        var scenePath = refs.Controller.gameObject.scene.path;
                        await transition.LoadContentSceneAsync(OtherContentScene);
                        Require(host.PendingSlotAction == null || host.PendingSlotAction == accepted, "the accepted spin is kept across the Scene change");
                        await transition.LoadContentSceneAsync(scenePath);
                        await WaitWhile(() => host.IsTemporaryCombatActionPending, 10d);
                        await Frames(3);
                        refs = Find();
                        Require(host.PendingSlotStory == null, "the Story record was written in the returned Scene");
                        Require(stories == before + 1, $"the spin accepted before the reload was applied once ({stories - before})");
                        Require(host.LastTemporarySlotTurn.Spin == accepted.Spin, "the applied result is the accepted one");
                        Require(refs.Controller.Combat == combat, "the new Scene continues the same combat");
                        Require(refs.Panel.TotalText == SlotCombatStoryFormatter.Total(accepted.Spin.PaylineTotal), $"the new panel shows the result ({refs.Panel.TotalText})");
                        log.AppendLine($" scene re-entry mid-spin (via MainScene_VisualProposal): applied once (enemy {enemy} -> {combat.EnemyHealth.Current}), new panel shows '{refs.Panel.TotalText}'.");
                    }
                }

                // 8. Finish the combat; the button hides and node selection returns and blocks the panel.
                for (var round = 0; round < 60 && refs.Controller.Combat != null && !refs.Controller.Combat.IsComplete; round++)
                {
                    await WaitWhile(() => !refs.Action.IsInteractable && refs.Controller.Combat != null && !refs.Controller.Combat.IsComplete, 5d);
                    if (refs.Controller.Combat == null || refs.Controller.Combat.IsComplete) break;
                    host.TemporaryDicePreferences.ReduceMotion = true;
                    refs.Action.Button.onClick.Invoke();
                    await WaitWhile(() => host.IsTemporaryCombatActionPending, 10d);
                    await Frames(1);
                }
                host.TemporaryDicePreferences.ReduceMotion = false;
                await Frames(3);
                Require(!refs.Action.IsShown, "the action button hides after the combat");
                var nodePanel = refs.Run.transform.root.GetComponentsInChildren<Transform>(true).First(t => t.name == "ExplorationNodePanel");
                Require(nodePanel.gameObject.activeInHierarchy, "node selection (or the run result) is shown again");
                Require(!refs.Panel.GetComponent<CanvasGroup>().interactable, "node selection blocks the slot machine panel again");
                log.AppendLine($" combat ended: phase={refs.Run.Run.Phase}, button hidden, node panel shown and covering the slot machine.");
                Debug.Log(log.Append(" PASS").ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError(log.Append(" FAIL: ").Append(exception.Message).ToString());
            }
        }

        private sealed class References
        {
            public ExplorationRunController Run;
            public TemporarySlotCombatController Controller;
            public SlotMachinePanel Panel;
            public BoundaryCommandButton Action;
            public GameWindowService Windows;
            public ResponsiveMainScreen Screen;
        }

        private static References Find()
        {
            var controller = Object.FindAnyObjectByType<TemporarySlotCombatController>() ?? throw new InvalidOperationException("TemporarySlotCombatController is missing.");
            return new References
            {
                Run = Object.FindAnyObjectByType<ExplorationRunController>() ?? throw new InvalidOperationException("ExplorationRunController is missing."),
                Controller = controller,
                Panel = controller.Panel ?? throw new InvalidOperationException("The slot machine panel is not connected."),
                Action = controller.ActionButtonForEditor ?? throw new InvalidOperationException("The action button is not connected."),
                Windows = Object.FindAnyObjectByType<GameWindowService>(),
                Screen = Object.FindAnyObjectByType<ResponsiveMainScreen>()
            };
        }

        private static void CheckResult(References refs, PlayerSessionHost host, TemporarySlotAcceptedAction accepted, StringBuilder log, string label)
        {
            Require(host.LastTemporarySlotTurn != null && host.LastTemporarySlotTurn.Spin == accepted.Spin, $"{label}: the accepted spin was applied");
            Require(refs.Panel.ReelViews.Select(v => v.PaylineValue).SequenceEqual(accepted.Spin.Reels.Select(r => r.PaylineValue)),
                $"{label}: payline cells match the result");
            foreach (var view in refs.Panel.ReelViews.OfType<SlotReelView>())
            {
                var strip = view.Strip;
                Require(view.ValueAt(-1) == strip[(view.StopIndex - 1 + strip.Count) % strip.Count] &&
                    view.ValueAt(1) == strip[(view.StopIndex + 1) % strip.Count], $"{label}: neighbours are the previous and next strip values");
            }
            Require(refs.Panel.TotalText == SlotCombatStoryFormatter.Total(accepted.Spin.PaylineTotal), $"{label}: total shown ({refs.Panel.TotalText})");
            Require(refs.Panel.AllocationText == SlotCombatStoryFormatter.Allocation(accepted.Allocation), $"{label}: allocation shown ({refs.Panel.AllocationText})");
            Require(accepted.Allocation.Lines.Sum(l => l.Amount) == accepted.Spin.PaylineTotal, $"{label}: allocation adds up to the total");
            if (label != "submit") log.AppendLine($" {label}: {SlotCombatStoryFormatter.Spin(accepted.Spin)}, {SlotCombatStoryFormatter.Allocation(accepted.Allocation)}.");
        }

        private static async Task CheckViewports(References refs, StringBuilder log)
        {
            if (refs.Screen == null || refs.Screen.PageScroll == null) { log.AppendLine(" viewports: ResponsiveMainScreen missing; not verified."); return; }
            var viewport = refs.Screen.PageScroll.viewport;
            var saved = (viewport.anchorMin, viewport.anchorMax, viewport.sizeDelta, viewport.anchoredPosition);
            try
            {
                foreach (var (name, size) in Viewports)
                {
                    viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
                    viewport.sizeDelta = size; viewport.anchoredPosition = Vector2.zero;
                    await Task.Delay(200);
                    Canvas.ForceUpdateCanvases();
                    refs.Action.Placement.UpdatePlacement();
                    var panelSize = ((RectTransform)refs.Panel.transform).rect.size;
                    CheckReelLayout(refs, name);
                    log.AppendLine($" {name}: panel {panelSize.x:F0}x{panelSize.y:F0}, reel window {WindowOf(refs.Panel.ReelViews[0]).rect.size.x:F0}x{WindowOf(refs.Panel.ReelViews[0]).rect.size.y:F0}, button clear of reels and total.");
                }
            }
            finally
            {
                viewport.anchorMin = saved.anchorMin; viewport.anchorMax = saved.anchorMax;
                viewport.sizeDelta = saved.sizeDelta; viewport.anchoredPosition = saved.anchoredPosition;
                await Task.Delay(200);
            }
        }

        // The boundary button never covers a reel window or the total, the reels do not overlap, and the payline digits fit.
        private static void CheckReelLayout(References refs, string label)
        {
            Canvas.ForceUpdateCanvases();
            var button = WorldRect((RectTransform)refs.Action.Button.transform);
            var total = refs.Panel.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "TotalText");
            Require(!button.Overlaps(WorldRect(total.rectTransform)), $"{label}: the action button does not cover the total");
            var windows = refs.Panel.ReelViews.Select(WindowOf).ToArray();
            for (var i = 0; i < windows.Length; i++)
            {
                var rect = WorldRect(windows[i]);
                Require(rect.width > 1f && rect.height > 1f, $"{label}: reel {i + 1} is visible");
                Require(!button.Overlaps(rect), $"{label}: the action button does not cover reel {i + 1}");
                if (i > 0) Require(!WorldRect(windows[i - 1]).Overlaps(rect), $"{label}: reels {i} and {i + 1} do not overlap");
                var cell = windows[i].GetComponentsInChildren<TMP_Text>(true).First(t => t.transform.parent.parent.name == "SymbolCell_2");
                var preferred = cell.GetPreferredValues(cell.text);
                var cellRect = ((RectTransform)cell.transform.parent.parent).rect;
                Require(preferred.x <= cellRect.width + .5f && preferred.y <= cellRect.height + .5f,
                    $"{label}: payline digit of reel {i + 1} fits ({preferred} in {cellRect.size})");
            }
        }

        private static RectTransform WindowOf(SlotReelViewBase view) => (RectTransform)view.transform.Find("Window");

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void Submit(BoundaryCommandButton action) =>
            ExecuteEvents.Execute(action.Button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);

        private static void StartCombat(ExplorationRunController controller)
        {
            var run = controller.Run;
            var select = typeof(ExplorationRunController).GetMethod("SelectChoice", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string) }, null)
                ?? throw new InvalidOperationException("The exploration choice handler is missing.");
            var continueButton = typeof(ExplorationRunController).GetField("continueButton", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(controller) as Button;
            var choice = run.CurrentChoices.FirstOrDefault(item => item.TypeId == ExplorationNodeTypeIds.Combat);
            for (var attempts = 0; choice == null && attempts < 20; attempts++)
            {
                var recovery = run.CurrentChoices.FirstOrDefault(item => item.TypeId == ExplorationNodeTypeIds.RecoveryUpgrade) ?? throw new InvalidOperationException("No combat or recovery choice.");
                select.Invoke(controller, new object[] { run.CurrentChoiceSetId, recovery.Id });
                continueButton.onClick.Invoke();
                choice = run.CurrentChoices.FirstOrDefault(item => item.TypeId == ExplorationNodeTypeIds.Combat);
            }
            if (choice == null) throw new InvalidOperationException("No combat node appeared within 20 choice sets.");
            select.Invoke(controller, new object[] { run.CurrentChoiceSetId, choice.Id });
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static async Task Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var frame = Time.frameCount;
                var deadline = EditorApplication.timeSinceStartup + 2d;
                while (Time.frameCount == frame && EditorApplication.timeSinceStartup < deadline) await Task.Yield();
            }
        }

        private static async Task WaitWhile(Func<bool> condition, double seconds)
        {
            var deadline = EditorApplication.timeSinceStartup + seconds;
            while (condition() && EditorApplication.timeSinceStartup < deadline) await Task.Yield();
        }
    }
}
