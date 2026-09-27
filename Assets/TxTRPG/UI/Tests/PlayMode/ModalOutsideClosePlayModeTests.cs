using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.UI.Windows;
using TxTRPG.Application.Items;
using TxTRPG.Application.Exploration;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.UI.Tests
{
    public sealed class ModalOutsideClosePlayModeTests
    {
        private Mouse mouse;
        private Touchscreen touch;
        private GameWindowService service;
        private EventSystem events;
        private Vector2 outside;
        private Vector2 inside;
        private int closeCount;

        [UnityTest]
        public IEnumerator RealInput_BackdropBoundariesDragTouchAndLifetime()
        {
            var oldEvents = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var old in oldEvents) old.enabled = false;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var canvasObject = new GameObject("ModalInputTestCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32767;
            var eventObject = new GameObject("ModalTestEvents", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events = eventObject.GetComponent<EventSystem>();
            var module = eventObject.GetComponent<InputSystemUIInputModule>(); module.AssignDefaultActions();
            mouse = InputSystem.AddDevice<Mouse>(); touch = InputSystem.AddDevice<Touchscreen>();
            var behind = new GameObject("Behind", typeof(RectTransform), typeof(Image), typeof(Button));
            behind.transform.SetParent(canvasObject.transform, false);
            var behindRect = (RectTransform)behind.transform; behindRect.anchorMin = Vector2.zero; behindRect.anchorMax = Vector2.one; behindRect.offsetMin = behindRect.offsetMax = Vector2.zero;
            var behindClicks = 0; behind.GetComponent<Button>().onClick.AddListener(() => behindClicks++);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ModalWindowHost.prefab");
            var modal = Object.Instantiate(prefab, canvasObject.transform);
            service = modal.GetComponent<GameWindowService>();
            service.Host.CloseRequested += CountClose;
            try
            {
                yield return null; yield return null;
                yield return Open(behind);
                Canvas.ForceUpdateCanvases();
                outside = new Vector2(5, 5);
                inside = RectTransformUtility.WorldToScreenPoint(null, service.Host.WindowRect.position);
                Assert.That(Raycast(outside), Is.SameAs(modal), "Real GraphicRaycaster must hit the backdrop.");
                yield return Click(inside); AssertOpen("inside empty space");
                yield return Click(RectTransformUtility.WorldToScreenPoint(null, modal.transform.Find("Window/Title").position)); AssertOpen("title");
                yield return Click(outside, 2); AssertOpen("right click");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = outside, scroll = new Vector2(0, 120) });
                yield return Frames(); AssertOpen("scroll wheel");
                yield return MouseStateAt(inside, 1); yield return MouseStateAt(outside, 1); yield return MouseStateAt(outside, 0); AssertOpen("inside to outside");
                yield return MouseStateAt(outside, 1); yield return MouseStateAt(inside, 1); yield return MouseStateAt(inside, 0); AssertOpen("outside to inside");
                yield return MouseStateAt(outside, 1); yield return MouseStateAt(outside + new Vector2(60, 0), 1); yield return MouseStateAt(outside, 0); AssertOpen("outside drag");
                // A graphics-only owned overlay outside Window must not be treated as backdrop.
                var overlay = new GameObject("OwnedOutsideControl", typeof(RectTransform), typeof(Image), typeof(Button));
                overlay.transform.SetParent(service.Host.ContentContainer.OverlayRoot, false);
                var overlayRect = (RectTransform)overlay.transform; overlayRect.sizeDelta = new Vector2(70, 70); overlayRect.position = new Vector3(50, 50, 0);
                var overlayClicks = 0; overlay.GetComponent<Button>().onClick.AddListener(() => overlayClicks++);
                yield return Frames(); yield return Click(new Vector2(50, 50));
                Assert.That(overlayClicks, Is.EqualTo(1)); AssertOpen("owned overlay button outside Window");
                Object.Destroy(overlay); yield return Frames();
                yield return Click(outside); AssertClosed();
                Assert.That(closeCount, Is.EqualTo(1)); Assert.That(behindClicks, Is.Zero);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(behind));
                // Stale mouse-up may not close a newly opened request.
                yield return Open(behind); yield return MouseStateAt(outside, 1);
                service.Close(); yield return Open(behind); yield return MouseStateAt(outside, 0); AssertOpen("reopened before release");
                // Distinct touch IDs cannot pair; a canceled touch is never a click.
                yield return TouchAt(1, outside, UnityEngine.InputSystem.TouchPhase.Began);
                yield return TouchAt(2, inside, UnityEngine.InputSystem.TouchPhase.Began);
                yield return TouchAt(2, inside, UnityEngine.InputSystem.TouchPhase.Ended); AssertOpen("second finger release");
                yield return TouchAt(1, outside, UnityEngine.InputSystem.TouchPhase.Canceled); AssertOpen("canceled touch");
                yield return TouchAt(3, outside, UnityEngine.InputSystem.TouchPhase.Began);
                yield return TouchAt(3, outside, UnityEngine.InputSystem.TouchPhase.Ended); AssertClosed();
                Assert.That(behindClicks, Is.Zero);
                // Existing Close and Cancel still use the service lifecycle.
                yield return Open(behind);
                yield return Click(RectTransformUtility.WorldToScreenPoint(null, service.Host.CloseButton.transform.position)); AssertClosed();
                yield return Open(behind);
                ExecuteEvents.Execute(service.Host.gameObject, new BaseEventData(events), ExecuteEvents.cancelHandler); AssertClosed();
                // Different Canvas scales keep the stored Window boundary authoritative.
                foreach (var scale in new[] { .5f, .75f, 1.25f })
                {
                    canvas.scaleFactor = scale; yield return Open(behind); Canvas.ForceUpdateCanvases();
                    inside = RectTransformUtility.WorldToScreenPoint(null, service.Host.WindowRect.position);
                    yield return Click(inside); AssertOpen("scaled Window interior");
                    yield return Click(outside); AssertClosed();
                }
                canvas.scaleFactor = 1;
                // Replace the page list with a controlled, cancellable preparation in this disposable fixture.
                var slowObject = new GameObject("SlowPage", typeof(RectTransform)); slowObject.transform.SetParent(service.Host.ContentContainer.ContentRoot, false);
                var slow = slowObject.AddComponent<ModalSlowTestPage>(); slow.ConfigureForEditor("slow"); slow.Hide();
                service.ConfigureForEditor(service.Host, new GameWindowPage[] { slow });
                var first = service.OpenAsync("slow", behind); yield return Frames();
                yield return Click(outside); AssertClosed(); Assert.That(slow.Token.IsCancellationRequested, Is.True);
                var oldCompletion = slow.Completion;
                var second = service.OpenAsync("slow", behind); yield return Frames();
                oldCompletion.SetResult(true); yield return Frames();
                Assert.That(first.IsCompleted && !first.Result, Is.True); Assert.That(service.IsOpen, Is.True);
                slow.Completion.SetResult(true); yield return Frames(); Assert.That(second.Result, Is.True);
                yield return Click(outside); AssertClosed(); Assert.That(slow.gameObject.activeSelf, Is.False);
                var failed = service.OpenAsync("slow", behind);
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Game window 'slow' failed"));
                slow.Completion.SetException(new InvalidOperationException("test preparation failure")); yield return Frames();
                Assert.That(failed.Result, Is.False); AssertOpen("error state");
                yield return Click(outside); AssertClosed();
                Assert.That(behindClicks, Is.Zero, "No close input may activate the underlying button.");
            }
            finally
            {
                service.Host.CloseRequested -= CountClose;
                service.Close();
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(touch);
                Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(eventObject);
                InputSystem.settings.backgroundBehavior = oldBackground;
                foreach (var old in oldEvents) if (old != null) old.enabled = true;
            }
        }
        [UnityTest]
        public IEnumerator AppScene_AllWindowsAndInventoryContextMenu()
        {
            SceneManager.LoadScene("Assets/Scenes/AppScene.unity");
            yield return null; yield return null;
            GameMenuPanel menu = null;
            for (var frame = 0; frame < 600; frame++)
            {
                var run = Object.FindFirstObjectByType<ExplorationRunController>();
                menu = Object.FindFirstObjectByType<GameMenuPanel>();
                if (menu != null && run != null && run.Run != null) break;
                yield return null;
            }
            Assert.That(menu, Is.Not.Null);
            service = menu.WindowService;
            events = EventSystem.current;
            mouse = InputSystem.AddDevice<Mouse>(); touch = InputSystem.AddDevice<Touchscreen>();
            var oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                foreach (var id in new[] { GamePageIds.Inventory, GamePageIds.System, GamePageIds.Status })
                {
                    var button = menu.Buttons.Single(b => b.PageId == id).Button;
                    Canvas.ForceUpdateCanvases();
                    var buttonPoint = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                    for (var frame = 0; frame < 300 && Raycast(buttonPoint)?.GetComponentInParent<Button>() != button; frame++) yield return null;
                    Assert.That(Raycast(buttonPoint)?.GetComponentInParent<Button>(), Is.SameAs(button), "Wait for the transition input blocker to release the menu.");
                    yield return Click(buttonPoint);
                    for (var frame = 0; frame < 300 && service.CurrentPageId != id; frame++) yield return null;
                    Assert.That(service.CurrentPageId, Is.EqualTo(id), "The opening click must not immediately close the modal.");
                    outside = new Vector2(5, 5);
                    Assert.That(Raycast(outside), Is.SameAs(service.Host.gameObject));
                    var window = service.Host.WindowRect;
                    var title = window.Find("Title");
                    yield return Click(RectTransformUtility.WorldToScreenPoint(null, title.position)); AssertOpen(id + " title");
                    if (id == GamePageIds.Inventory)
                    {
                        var page = (InventoryGameWindowPage)service.Pages.Single(p => p.PageId == id);
                        var grid = page.GetComponentInChildren<ActionGridPanel>(true);
                        var empty = page.transform.Find("EmptyState");
                        Assert.That(empty.gameObject.activeSelf, Is.EqualTo(grid.VisibleCellCount == 0));
                        // A disposable projection exercises the real context-menu hierarchy without touching inventory/save data.
                        var commands = new ProbeCommands(); grid.SetServices(commands, commands);
                        grid.SetEntries(new[] { new ActionGridEntry("probe", ActionGridEntryKind.Item, null, "Probe", quantity: 1) }, 1);
                        yield return Frames();
                        var cell = grid.GetComponentsInChildren<ActionGridCell>().First();
                        yield return Click(RectTransformUtility.WorldToScreenPoint(null, cell.transform.position));
                        var context = page.GetComponentInChildren<ActionContextMenu>(true);
                        Assert.That(context.IsOpen, Is.True);
                        var option = context.GetComponentsInChildren<Button>().First(b => b.interactable);
                        yield return Click(RectTransformUtility.WorldToScreenPoint(null, option.transform.position));
                        Assert.That(commands.Count, Is.EqualTo(1)); AssertOpen("context command");
                        yield return Click(RectTransformUtility.WorldToScreenPoint(null, cell.transform.position));
                        Assert.That(context.IsOpen, Is.True);
                        yield return Click(outside); AssertClosed(); Assert.That(context.IsOpen, Is.False);
                    }
                    else { yield return Click(outside); AssertClosed(); }
                    Assert.That(events.currentSelectedGameObject, Is.SameAs(button.gameObject));
                    var open = service.OpenAsync(id, button.gameObject);
                    while (!open.IsCompleted) yield return null;
                    Assert.That(open.Result, Is.True); yield return Frames();
                    yield return TouchAt(10, outside, UnityEngine.InputSystem.TouchPhase.Began);
                    yield return TouchAt(10, outside, UnityEngine.InputSystem.TouchPhase.Ended); AssertClosed();
                }
            }
            finally
            {
                if (service != null && service.Host != null) service.Close();
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(touch);
                InputSystem.settings.backgroundBehavior = oldBackground;
            }
        }
        private sealed class ProbeCommands : IActionMenuProvider, IActionCommandExecutor
        {
            public int Count;
            public IReadOnlyList<ActionMenuOption> GetOptions(string _) => new[] { new ActionMenuOption("probe", "Probe") };
            public Task<ActionCommandResult> ExecuteAsync(string entry, string command, CancellationToken token)
            { Count++; return Task.FromResult(new ActionCommandResult(true)); }
        }
        private void CountClose() => closeCount++;
        private IEnumerator Open(GameObject focus)
        {
            var task = service.OpenAsync(GamePageIds.System, focus);
            while (!task.IsCompleted) yield return null;
            Assert.That(task.Result, Is.True); yield return Frames();
        }
        private void AssertOpen(string operation) => Assert.That(service.IsOpen, Is.True, operation);
        private void AssertClosed() => Assert.That(service.IsOpen, Is.False);
        private IEnumerator Click(Vector2 point, ushort button = 1)
        { yield return MouseStateAt(point, 0); yield return MouseStateAt(point, button); yield return MouseStateAt(point, 0); }
        private IEnumerator MouseStateAt(Vector2 point, ushort buttons)
        { InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = buttons }); yield return Frames(); }
        private IEnumerator TouchAt(int id, Vector2 point, UnityEngine.InputSystem.TouchPhase phase)
        { InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, position = point, phase = phase }); yield return Frames(); }
        private static IEnumerator Frames() { yield return null; yield return null; }
        private GameObject Raycast(Vector2 point)
        {
            var results = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = point }, results);
            return results.FirstOrDefault().gameObject;
        }
    }
    public sealed class ModalSlowTestPage : GameWindowPage
    {
        public TaskCompletionSource<bool> Completion;
        public CancellationToken Token;
        public override Task PrepareAsync(CancellationToken token)
        { Token = token; Completion = new TaskCompletionSource<bool>(); return Completion.Task; }
    }
}
