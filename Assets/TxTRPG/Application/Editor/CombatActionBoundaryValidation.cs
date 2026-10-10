using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Exploration;
using TxTRPG.Application.Players;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.UI;
using TxTRPG.UI.Shapes;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // Play Mode verification for the boundary combat action button. Start Play Mode from AppScene with a fresh
    // exploration run (awaiting a node choice). Results are logged with the COMBAT_ACTION_VALIDATE prefix.
    public static class CombatActionBoundaryValidation
    {
        public const string MenuPath = "Tools/TxT RPG/UI/Combat/Validate Combat Action Button (Play Mode)";

        private static readonly (string Name, Vector2 Size)[] Viewports =
        {
            ("phone portrait", new Vector2(390f, 844f)),
            ("phone landscape", new Vector2(844f, 390f)),
            ("tablet", new Vector2(768f, 1024f)),
            ("narrow desktop", new Vector2(1024f, 640f)),
            ("ultrawide", new Vector2(2560f, 1080f))
        };

        [MenuItem(MenuPath)]
        public static async void Validate()
        {
            var log = new StringBuilder("COMBAT_ACTION_VALIDATE:\n");
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
                var host = PlayerSessionHost.Instance ?? throw new InvalidOperationException("The player session is missing.");
                var run = UnityEngine.Object.FindAnyObjectByType<ExplorationRunController>() ?? throw new InvalidOperationException("ExplorationRunController is missing.");
                var combat = UnityEngine.Object.FindAnyObjectByType<TemporaryDiceRollMenuController>() ?? throw new InvalidOperationException("Combat controller is missing.");
                var action = combat.ActionButtonForEditor ?? throw new InvalidOperationException("The boundary action button is not connected.");
                var windows = UnityEngine.Object.FindAnyObjectByType<GameWindowService>();
                var menu = UnityEngine.Object.FindAnyObjectByType<GameMenuPanel>();
                var screen = UnityEngine.Object.FindAnyObjectByType<ResponsiveMainScreen>();
                var placement = action.Placement;
                var textPanel = (RectTransform)action.transform.parent.parent;
                if (run.Run == null || run.Run.Phase != ExplorationPhase.AwaitingChoice) throw new InvalidOperationException("Validation requires a fresh awaiting-choice exploration run.");

                Require(!action.IsShown, "hidden while choosing a node");
                Require(menu.Buttons.All(item => item.ActionKind != GameMenuButtonActionKind.Command) && menu.Buttons.Count == 3, "menu keeps exactly System/Bag/Status");
                log.AppendLine($" menu layout: {menu.CurrentLayout.Columns}x{menu.CurrentLayout.Rows} insufficient={menu.LayoutInsufficientSpace}");

                StartCombat(run);
                await Frames(2);
                Require(action.IsShown && action.IsInteractable, $"shown and enabled in combat ({action.UnavailableReason})");
                CheckCentered(action, placement, textPanel, "wide", log);
                await CheckLayoutInvariance(action, placement, log);

                var viewport = screen.PageScroll.viewport;
                var saved = (viewport.anchorMin, viewport.anchorMax, viewport.sizeDelta, viewport.anchoredPosition);
                try
                {
                    foreach (var (name, size) in Viewports)
                    {
                        viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
                        viewport.sizeDelta = size; viewport.anchoredPosition = Vector2.zero;
                        await Task.Delay(150);
                        CheckCentered(action, placement, textPanel, name, log);
                        if (name == "phone portrait") await CheckNarrowReveal(action, screen, log);
                    }
                }
                finally
                {
                    viewport.anchorMin = saved.anchorMin; viewport.anchorMax = saved.anchorMax;
                    viewport.sizeDelta = saved.sizeDelta; viewport.anchoredPosition = saved.anchoredPosition;
                    await Task.Delay(150);
                }
                CheckCentered(action, placement, textPanel, "wide restored", log);

                CheckShapesAndHitArea(action, log);
                await CheckEffects(action, log);
                await CheckFocusRelease(action, menu, log);

                // Submit twice in one frame: exactly one roll is accepted; the button stays visible but disabled.
                var turns = host.TemporaryDiceHistory.CurrentTurns.Count;
                EventSystem.current.SetSelectedGameObject(action.Button.gameObject);
                await Frames(1);
                ExecuteEvents.Execute(action.Button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                ExecuteEvents.Execute(action.Button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Require(host.IsTemporaryDiceActionPending, "submit accepted the roll");
                Require(action.IsShown && !action.IsInteractable, "visible but disabled while rolling");
                var selectedDuringRoll = EventSystem.current.currentSelectedGameObject;
                await WaitWhile(() => host.IsTemporaryDiceActionPending, 10d);
                Require(host.TemporaryDiceHistory.CurrentTurns.Count == turns + 1, "rapid submits applied exactly one turn");
                var selectedAfterRoll = EventSystem.current.currentSelectedGameObject;
                log.AppendLine($" selection during roll={(selectedDuringRoll != null ? selectedDuringRoll.name : "none")}, after roll={(selectedAfterRoll != null ? selectedAfterRoll.name : "none")}");
                await Task.Delay(150);
                if (windows.IsOpen)
                {
                    Require(!action.IsInteractable, "disabled while the result window is open");
                    windows.Close();
                    await Frames(2);
                    var restored = EventSystem.current.currentSelectedGameObject;
                    Require(restored == action.Button.gameObject, $"focus returns to the action button after closing the result (selected: {(restored != null ? restored.name : "none")})");
                }
                else log.AppendLine(" result window: not open after the roll (automatic results off?).");
                // Another game window (Bag) also disables it.
                if (!combat.Combat.IsComplete)
                {
                    Require(action.IsInteractable, $"enabled again after the result ({action.UnavailableReason})");
                    _ = windows.OpenAsync(GamePageIds.Inventory);
                    await Frames(3);
                    Require(windows.IsOpen && !action.IsInteractable, "disabled while a game window is open");
                    windows.Close();
                    await Frames(2);
                    Require(action.IsInteractable, "enabled after the game window closes");
                }

                // Finish the combat by clicking; the button must hide on victory or defeat.
                for (var round = 0; round < 60 && combat.Combat != null && !combat.Combat.IsComplete; round++)
                {
                    await WaitWhile(() => !action.IsInteractable && combat.Combat != null && !combat.Combat.IsComplete, 5d);
                    if (combat.Combat == null || combat.Combat.IsComplete) break;
                    action.Button.onClick.Invoke();
                    await WaitWhile(() => host.IsTemporaryDiceActionPending, 10d);
                    await Task.Delay(100);
                    if (windows.IsOpen) windows.Close();
                    await Frames(1);
                }
                await Frames(2);
                Require(combat.Combat == null || combat.Combat.IsComplete, "combat finished within 60 rounds");
                Require(!action.IsShown, "hidden after combat ends");
                var finalSelection = EventSystem.current.currentSelectedGameObject;
                Require(finalSelection == null || finalSelection.activeInHierarchy, $"focus is not left on a hidden object ({(finalSelection != null ? finalSelection.name : "none")})");
                log.AppendLine($" combat ended: phase={run.Run.Phase}, button hidden.");
                if (run.Run.Phase == ExplorationPhase.AwaitingChoice)
                {
                    StartCombat(run);
                    await Frames(2);
                    Require(action.IsShown && action.IsInteractable, "shown again for the next combat");
                    log.AppendLine(" next combat: button shown again.");
                    // Defeat path: keep the player at 1 HP so the enemy attack ends the run.
                    for (var round = 0; round < 60 && combat.Combat != null && !combat.Combat.IsComplete; round++)
                    {
                        await WaitWhile(() => !action.IsInteractable && combat.Combat != null && !combat.Combat.IsComplete, 5d);
                        if (combat.Combat == null || combat.Combat.IsComplete) break;
                        var player = combat.Combat.PlayerHealth;
                        if (player.Current > 1) player.ApplyDamage(player.Current - 1);
                        action.Button.onClick.Invoke();
                        await WaitWhile(() => host.IsTemporaryDiceActionPending, 10d);
                        await Task.Delay(100);
                        if (windows.IsOpen) windows.Close();
                        await Frames(1);
                    }
                    await Frames(2);
                    Require(!action.IsShown, $"hidden after the second combat ends (phase {run.Run.Phase})");
                    var afterDefeat = EventSystem.current.currentSelectedGameObject;
                    Require(afterDefeat == null || afterDefeat.activeInHierarchy, "focus is not left on a hidden object after the second combat");
                    log.AppendLine($" second combat ended: phase={run.Run.Phase}, enemyDefeated={combat.Combat?.EnemyHealth.IsDefeated}, button hidden.");
                }
                Debug.Log(log.Append(" PASS").ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError(log.Append(" FAIL: ").Append(exception.Message).ToString());
            }
        }

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

        private static void CheckCentered(BoundaryCommandButton action, PanelBoundaryPlacement placement, RectTransform textPanel, string label, StringBuilder log)
        {
            Canvas.ForceUpdateCanvases();
            placement.UpdatePlacement();
            Require(placement.HasBoundary && action.IsShown, $"{label}: boundary exists and the button is shown");
            var container = (RectTransform)placement.transform;
            var first = LocalRect(container, placement.FirstPanel);
            var second = LocalRect(container, placement.SecondPanel);
            Require(PanelBoundaryCalculator.TryCalculate(first, second, out var expected, out var orientation), $"{label}: boundary calculation");
            var button = LocalRect(container, (RectTransform)action.Button.transform);
            var bounds = LocalRect(container, textPanel);
            var clamped = PanelBoundaryCalculator.ClampInside(expected, button.size, container.rect);
            Require(Vector2.Distance(button.center, clamped) < .6f, $"{label}: button center {button.center} vs boundary {expected} (clamped {clamped})");
            Require(bounds.xMin - .5f <= button.xMin && button.xMax <= bounds.xMax + .5f && bounds.yMin - .5f <= button.yMin && button.yMax <= bounds.yMax + .5f,
                $"{label}: button {button} stays inside Text_FlexibleLayoutPanel {bounds}");
            log.AppendLine($" {label}: orientation={orientation} boundary={expected} button={button.center} size={button.size} text={bounds.size} clampedShift={(clamped - expected).magnitude:0.##}");
        }

        private static async Task CheckLayoutInvariance(BoundaryCommandButton action, PanelBoundaryPlacement placement, StringBuilder log)
        {
            var text = (RectTransform)action.transform.parent.parent;
            string Snapshot() => string.Join("|", new[] { placement.FirstPanel, placement.SecondPanel, text }.Select(rect => $"{rect.rect}@{rect.position}"));
            var with = Snapshot();
            action.gameObject.SetActive(false);
            await Frames(2);
            var without = Snapshot();
            action.gameObject.SetActive(true);
            await Frames(2);
            Require(with == without, $"panel rects do not depend on the button ({with} vs {without})");
            Require(action.IsShown, "shown again after re-enabling");
            log.AppendLine(" layout invariance: Enemy/Story/Text rects identical with and without the button.");
        }

        private static async Task CheckNarrowReveal(BoundaryCommandButton action, ResponsiveMainScreen screen, StringBuilder log)
        {
            var scroll = screen.PageScroll;
            if (!scroll.vertical) { log.AppendLine(" narrow: page scroll not active (skipped reveal)."); return; }
            scroll.verticalNormalizedPosition = 0f;
            EventSystem.current.SetSelectedGameObject(null);
            await Frames(2);
            EventSystem.current.SetSelectedGameObject(action.Button.gameObject);
            await Frames(3);
            var viewport = scroll.viewport;
            var corners = new Vector3[4];
            ((RectTransform)action.Button.transform).GetWorldCorners(corners);
            var bottom = viewport.InverseTransformPoint(corners[0]).y;
            var top = viewport.InverseTransformPoint(corners[1]).y;
            Require(top <= viewport.rect.yMax + 1f && bottom >= viewport.rect.yMin - 1f, "narrow: selecting the button reveals it in the page scroll");
            EventSystem.current.SetSelectedGameObject(null);
            log.AppendLine(" narrow: ResponsiveMainScreen.Reveal scrolled the selected button into view.");
        }

        private static void CheckShapesAndHitArea(BoundaryCommandButton action, StringBuilder log)
        {
            var view = action.ShapedView;
            var original = view.Appearance;
            var hit = view.HitArea;
            var rect = ((RectTransform)hit.transform).rect;
            var corner = rect.max - Vector2.one * 2f;
            try
            {
                var circle = original; circle.visualMode = ShapedButtonVisualMode.ProceduralShape; circle.shape = UiShape.Circle; circle.shapeHitTest = true;
                view.ApplyAppearance(circle);
                Require(hit.ContainsLocalPoint(Vector2.zero) && !hit.ContainsLocalPoint(corner), "circle input follows the shape");
                var rounded = circle; rounded.shape = UiShape.RoundedRectangle; rounded.cornerRadius = 12f;
                view.ApplyAppearance(rounded);
                Require(!hit.ContainsLocalPoint(rect.max - Vector2.one * .5f) && hit.ContainsLocalPoint(new Vector2(rect.xMax - 2f, 0f)), "rounded rectangle input");
                var square = circle; square.shape = UiShape.Rectangle;
                view.ApplyAppearance(square);
                Require(hit.ContainsLocalPoint(corner), "rectangle input");
                var image = circle; image.visualMode = ShapedButtonVisualMode.Image;
                view.ApplyAppearance(image);
                Require(hit.Shape == UiShape.Rectangle && action.Button.targetGraphic is Image, "image mode uses rectangle input and the Image as tint target");
                var size = ((RectTransform)action.Button.transform).rect.size;
                Require(size.x >= 48f && size.y >= 48f, $"touch target is at least 48x48 ({size})");
                log.AppendLine($" shapes: circle/rounded/rectangle/image verified; size={size}.");
            }
            finally { view.ApplyAppearance(original); }
        }

        private static async Task CheckEffects(BoundaryCommandButton action, StringBuilder log)
        {
            var view = action.ShapedView;
            var shared = Canvas.GetDefaultCanvasMaterial();
            var background = view.VisualRoot.GetComponentsInChildren<UiShapeGraphic>(true).First();
            var placementBefore = ((RectTransform)action.Button.transform).anchoredPosition;
            var hash = shared.ComputeCRC();
            try
            {
                view.SetEffectMaterialsForEditor(shared, shared, shared, shared);
                var instance = view.GetOrCreateEffectInstance(ShapedButtonEffectTarget.Background);
                instance.color = new Color(1f, .5f, .5f, 1f);
                Require(background.material == instance && shared.ComputeCRC() == hash, "instance material used; shared material unchanged");
                view.VisualRoot.localScale = Vector3.one * 1.2f; // Simulated scale effect on the visual root.
                Require(((RectTransform)action.Button.transform).anchoredPosition == placementBefore && view.HitArea.transform.lossyScale.x < 1.01f * action.Button.transform.lossyScale.x,
                    "visual effects do not move the placement or input area");
                view.SetEffectsAllowed(false);
                Require(background.material == background.defaultMaterial, "effects can be turned off (Reduce Motion)");
                view.SetEffectsAllowed(true);
                view.ResetEffects();
                Require(background.material == shared, "reset restores the slot material");
                await Frames(1); // Object.Destroy completes at the end of the frame in Play Mode.
                Require(instance == null, "reset destroys the owned instance");
                log.AppendLine(" effects: instance/shared/reset/reduce-motion verified with the default UI material.");
            }
            finally
            {
                view.VisualRoot.localScale = Vector3.one;
                view.SetEffectMaterialsForEditor(null, null, null, null);
                view.SetEffectsAllowed(TxTRPG.UI.Exploration.ExplorationCardPlayerPreferences.AllowsMotion);
            }
        }

        private static async Task CheckFocusRelease(BoundaryCommandButton action, GameMenuPanel menu, StringBuilder log)
        {
            var system = menu.Buttons.Single(item => item.PageId == GamePageIds.System).Button;
            Require(action.Button.navigation.mode == Navigation.Mode.Explicit && action.Button.navigation.selectOnRight == system &&
                action.Button.navigation.selectOnDown == system, "explicit navigation Right/Down -> System");
            EventSystem.current.SetSelectedGameObject(action.Button.gameObject);
            action.SetAvailable(false);
            await Frames(1);
            Require(EventSystem.current.currentSelectedGameObject == null, "hiding while focused clears focus (exploration no-initial-selection rule)");
            action.SetAvailable(true);
            await Frames(1);
            Require(action.IsShown && action.IsInteractable, "restored after the focus check");
            EventSystem.current.SetSelectedGameObject(null);
            log.AppendLine(" focus: explicit Right/Down -> System; hidden-while-focused clears the selection.");
        }

        private static Rect LocalRect(RectTransform space, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = (Vector2)space.InverseTransformPoint(corners[0]); var max = min;
            for (var i = 1; i < 4; i++) { var p = (Vector2)space.InverseTransformPoint(corners[i]); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
