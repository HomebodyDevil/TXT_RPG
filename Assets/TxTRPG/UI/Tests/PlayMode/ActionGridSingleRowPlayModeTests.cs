using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
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
    public sealed class ActionGridSingleRowPlayModeTests
    {
        private static IEnumerator Frames() { yield return null; yield return null; yield return null; }
        private static ActionGridPanel Create(Transform parent, float width)
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab"),parent);
            var rect=(RectTransform)root.transform; rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f); rect.pivot=Vector2.one*.5f; rect.anchoredPosition=Vector2.zero; rect.sizeDelta=new Vector2(width+36,240);
            var panel=root.GetComponent<ActionGridPanel>();
            panel.BeginInitialContentSetup();
            panel.SurfaceLayout.ConfigureHeader(false,58,8,new RectOffset(18,18,18,18));
            var settings=panel.GetDisplaySettings(); settings.mode=ActionGridDisplayMode.Balanced; settings.flow=ActionGridFlow.SingleRow;
            panel.ConfigureDisplay(settings); panel.SetEntries(Array.Empty<ActionGridEntry>(),5);
            return panel;
        }
        [UnityTest]
        public IEnumerator Geometry_HeaderCountsResizeAndPositionRecovery()
        {
            var canvas=new GameObject("SingleRowGeometry",typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            try
            {
                var panel=Create(canvas.transform,600); var scroll=panel.ScrollRect;
                yield return Frames(); panel.CompleteInitialContentSetup();
                var settings=panel.GetDisplaySettings(); settings.balanced.padding=new RectOffset(11,29,7,13); panel.ConfigureDisplay(settings);
                foreach(var count in new[]{0,1,2,4,5})
                {
                    panel.SetEntries(Array.Empty<ActionGridEntry>(),count); yield return Frames();
                    var cells=scroll.content.GetComponentsInChildren<ActionGridCell>();
                    Assert.That(cells.Length,Is.EqualTo(count)); Assert.That(scroll.content.rect.width,Is.EqualTo(scroll.viewport.rect.width).Within(.1));
                    if(count==0) continue;
                    var first=(RectTransform)cells[0].transform; var last=(RectTransform)cells[count-1].transform;
                    Assert.That(first.anchoredPosition.y,Is.EqualTo(last.anchoredPosition.y).Within(.1));
                    var left=first.anchoredPosition.x-first.pivot.x*first.rect.width;
                    var right=last.anchoredPosition.x+(1-last.pivot.x)*last.rect.width;
                    if(count>=5) { Assert.That(left,Is.EqualTo(11).Within(.1)); Assert.That(right,Is.EqualTo(571).Within(.1)); }
                    else Assert.That((left+right)*.5f,Is.EqualTo(291).Within(.1));
                }
                var root=(RectTransform)panel.transform; root.sizeDelta=new Vector2(236,240); yield return Frames();
                Assert.That(scroll.content.anchoredPosition.x,Is.Zero.Within(.1)); Assert.That(scroll.horizontalNormalizedPosition,Is.Zero.Within(.01));
                scroll.content.anchoredPosition=new Vector2(-90,0); scroll.StopMovement();
                panel.SetEntries(Array.Empty<ActionGridEntry>(),5); yield return Frames(); Assert.That(scroll.content.anchoredPosition.x,Is.EqualTo(-90).Within(.1));
                panel.transform.Find("Header").gameObject.SetActive(true); yield return Frames(); Assert.That(scroll.content.anchoredPosition.x,Is.EqualTo(-90).Within(.1));
                panel.transform.Find("Header").gameObject.SetActive(false); yield return Frames(); Assert.That(panel.SurfaceLayout.HeaderReservation,Is.Zero);
                root.sizeDelta=new Vector2(266,240); yield return Frames(); Assert.That(scroll.content.anchoredPosition.x,Is.EqualTo(-90).Within(.1));
                foreach(var vertical in new[]{ActionGridRowVerticalAlignment.Top,ActionGridRowVerticalAlignment.Center,ActionGridRowVerticalAlignment.Bottom})
                {
                    settings.singleRow.verticalAlignment=vertical; panel.ConfigureDisplay(settings); yield return Frames();
                    var cell=(RectTransform)scroll.content.GetChild(0); var top=-(cell.anchoredPosition.y+(1-cell.pivot.y)*cell.rect.height);
                    var expected=vertical==ActionGridRowVerticalAlignment.Top ? 7 : vertical==ActionGridRowVerticalAlignment.Bottom ? scroll.viewport.rect.height-13-64 : 7+(scroll.viewport.rect.height-20-64)/2;
                    Assert.That(top,Is.EqualTo(expected).Within(.1));
                }
                root.sizeDelta=new Vector2(636,240); yield return Frames(); Assert.That(scroll.content.anchoredPosition.x,Is.Zero.Within(.1)); Assert.That(scroll.velocity,Is.EqualTo(Vector2.zero));
                for(var i=0;i<3;i++) { settings.flow=ActionGridFlow.Grid; panel.ConfigureDisplay(settings); yield return Frames(); settings.flow=ActionGridFlow.SingleRow; panel.ConfigureDisplay(settings); yield return Frames(); }
                Assert.That(scroll.content.anchorMax,Is.EqualTo(new Vector2(0,1))); Assert.That(scroll.content.rect.width,Is.EqualTo(600).Within(.1));
            }
            finally { Object.Destroy(canvas); }
        }
        [UnityTest]
        public IEnumerator InvalidInitialViewportAndExplicitNavigationDuringPreparation()
        {
            var canvas=new GameObject("SingleRowInitial",typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            try
            {
                var panel=Create(canvas.transform,0); yield return Frames();
                Assert.That(panel.CompleteInitialContentSetup(),Is.False);
                ((RectTransform)panel.transform).sizeDelta=new Vector2(236,240); yield return Frames();
                Assert.That(panel.CompleteInitialContentSetup(),Is.True);
                Assert.That(panel.ScrollRect.content.anchoredPosition.x,Is.Zero.Within(.1));
                // Explicit input wins over any pending data callback.
                panel.NotifyUserScroll(); panel.ScrollRect.content.anchoredPosition=new Vector2(-75,0);
                panel.SetEntries(Array.Empty<ActionGridEntry>(),5); panel.CompleteInitialContentSetup(); yield return Frames();
                Assert.That(panel.ScrollRect.content.anchoredPosition.x,Is.EqualTo(-75).Within(.1));
                var options=panel.GetDisplaySettings(); options.singleRow.scrollbarVisibility=ScrollbarVisibilityMode.Auto; panel.ConfigureDisplay(options); yield return Frames();
                Assert.That(panel.SurfaceLayout.HorizontalScrollbar.gameObject.activeSelf,Is.True);
                Assert.That(panel.ScrollRect.viewport.offsetMin.y,Is.EqualTo(16));
                options.singleRow.scrollbarVisibility=ScrollbarVisibilityMode.Hidden; panel.ConfigureDisplay(options); yield return Frames();
                Assert.That(panel.ScrollRect.viewport.offsetMin,Is.EqualTo(Vector2.zero));
                Assert.That(panel.SurfaceLayout.HorizontalScrollbar.gameObject.activeSelf,Is.False);
                Assert.That(panel.ScrollRect.horizontalScrollbar,Is.Null);
            }
            finally { Object.Destroy(canvas); }
        }
        [UnityTest]
        public IEnumerator InputSystem_MouseWheelTouchDragKeyboardAndGamepad()
        {
            var oldEvents=Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None); foreach(var e in oldEvents) e.enabled=false;
            var background=InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var canvas=new GameObject("SingleRowInput",typeof(Canvas),typeof(GraphicRaycaster)); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay; canvas.GetComponent<Canvas>().sortingOrder=32767;
            var eventObject=new GameObject("SingleRowEvents",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var events=eventObject.GetComponent<EventSystem>(); eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var mouse=InputSystem.AddDevice<Mouse>(); var touch=InputSystem.AddDevice<Touchscreen>(); var keyboard=InputSystem.AddDevice<Keyboard>(); var gamepad=InputSystem.AddDevice<Gamepad>();
            try
            {
                var panel=Create(canvas.transform,220); var scroll=panel.ScrollRect; yield return Frames();
                var cells=scroll.content.GetComponentsInChildren<ActionGridCell>(); var clicks=0; foreach(var cell in cells) cell.Button.onClick.AddListener(()=>clicks++);
                // Automatic initial selection cannot displace the left starting position.
                events.SetSelectedGameObject(cells[4].gameObject); panel.CompleteInitialContentSetup(); yield return Frames();
                Assert.That(scroll.content.anchoredPosition.x,Is.Zero.Within(.1));
                var point=RectTransformUtility.WorldToScreenPoint(null,cells[0].transform.position);
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point,scroll=new Vector2(0,120)}); yield return Frames();
                Assert.That(scroll.content.anchoredPosition.x,Is.LessThan(-1),"Vertical wheel maps horizontally");
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point,scroll=new Vector2(0,-120)}); yield return Frames();
                Assert.That(scroll.content.anchoredPosition.x,Is.Zero.Within(.1),"Opposite wheel returns to start");
                panel.ScrollToTop(); yield return Frames();
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point,scroll=new Vector2(-120,0)}); yield return Frames();
                Assert.That(scroll.content.anchoredPosition.x,Is.LessThan(-1),"Trackpad horizontal delta");
                panel.ScrollToTop(); yield return Frames();
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point,buttons=1}); yield return Frames();
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point+new Vector2(-40,0),buttons=1}); yield return Frames();
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point+new Vector2(-100,0),buttons=1}); yield return Frames();
                InputSystem.QueueStateEvent(mouse,new MouseState {position=point+new Vector2(-100,0)}); yield return Frames();
                Assert.That(scroll.content.anchoredPosition.x,Is.LessThan(-1)); Assert.That(clicks,Is.Zero,"Dragging must not activate a cell");
                panel.ScrollToTop(); InputSystem.QueueStateEvent(mouse,new MouseState {position=Vector2.zero}); yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState {touchId=7,position=point,phase=UnityEngine.InputSystem.TouchPhase.Began}); yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState {touchId=7,position=point+new Vector2(-40,0),phase=UnityEngine.InputSystem.TouchPhase.Moved}); yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState {touchId=7,position=point+new Vector2(-100,0),phase=UnityEngine.InputSystem.TouchPhase.Moved}); yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState {touchId=7,position=point+new Vector2(-100,0),phase=UnityEngine.InputSystem.TouchPhase.Ended}); yield return Frames();
                Assert.That(scroll.content.anchoredPosition.x,Is.LessThan(-1)); Assert.That(clicks,Is.Zero);
                panel.ScrollToTop(); events.SetSelectedGameObject(cells[0].gameObject); yield return Frames();
                for(var i=0;i<4;i++) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow)); yield return Frames(); InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return Frames(); }
                Assert.That(events.currentSelectedGameObject,Is.SameAs(cells[4].gameObject)); Assert.That(scroll.content.anchoredPosition.x,Is.LessThan(-1));
                panel.ScrollToTop(); events.SetSelectedGameObject(cells[0].gameObject); yield return Frames();
                for(var i=0;i<4;i++) { InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.DpadRight)); yield return Frames(); InputSystem.QueueStateEvent(gamepad,new GamepadState()); yield return Frames(); }
                Assert.That(events.currentSelectedGameObject,Is.SameAs(cells[4].gameObject)); Assert.That(scroll.content.anchoredPosition.x,Is.LessThan(-1));
                panel.SetEntries(Array.Empty<ActionGridEntry>(),5); yield return Frames(); Assert.That(scroll.content.anchoredPosition.x,Is.LessThan(-1),"Delayed refresh preserves user navigation");
                Assert.That(scroll.vertical,Is.False);
                var menu=panel.GetComponentInChildren<ActionContextMenu>(true);
                menu.Show(new[]{new ActionMenuOption("check","Check")},_=>{},(RectTransform)cells[4].transform,cells[4].gameObject); yield return Frames();
                Assert.That(menu.IsOpen,Is.True); menu.Hide(); yield return Frames();
                Assert.That(events.currentSelectedGameObject,Is.SameAs(cells[4].gameObject));
                var corners=new Vector3[4]; ((RectTransform)cells[4].transform).GetWorldCorners(corners);
                Assert.That(scroll.viewport.InverseTransformPoint(corners[3]).x,Is.LessThanOrEqualTo(scroll.viewport.rect.xMax+.5f));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(touch); InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(gamepad);
                InputSystem.settings.backgroundBehavior=background; Object.Destroy(eventObject); Object.Destroy(canvas); foreach(var e in oldEvents) if(e!=null) e.enabled=true;
            }
        }
    }
}
