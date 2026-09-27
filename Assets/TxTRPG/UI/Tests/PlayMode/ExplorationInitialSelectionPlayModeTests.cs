using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Exploration;
using TxTRPG.UI.Windows;
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
    public sealed class ExplorationInitialSelectionPlayModeTests
    {
        private static IEnumerator Frames() { yield return null; yield return null; yield return null; }
        private sealed class Fixture : IDisposable
        {
            private readonly EventSystem[] previous = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            private readonly System.Collections.Generic.List<CanvasGroup> blockers = new();
            private readonly InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
            public readonly GameObject Canvas, EventsObject;
            public readonly EventSystem Events;
            public readonly ExplorationNodeChoiceCardList List;
            public readonly Keyboard Keyboard;
            public readonly Gamepad Pad;
            public readonly Mouse Mouse;
            public readonly Touchscreen Touch;
            public readonly ScrollRect Scroll;
            public int Choices;
            public Fixture(float width=740)
            {
                foreach (var e in previous) e.enabled=false;
                foreach (var list in Object.FindObjectsByType<ExplorationNodeChoiceCardList>(FindObjectsSortMode.None))
                {
                    var blocker=list.gameObject.AddComponent<CanvasGroup>(); blocker.interactable=false; blockers.Add(blocker);
                }
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                Canvas=new GameObject("ExplorationInputTest",typeof(Canvas),typeof(GraphicRaycaster));
                Canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                Canvas.GetComponent<Canvas>().sortingOrder=32767;
                EventsObject=new GameObject("ExplorationEvents",typeof(EventSystem),typeof(InputSystemUIInputModule));
                Events=EventsObject.GetComponent<EventSystem>(); EventsObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                Keyboard=InputSystem.AddDevice<Keyboard>(); Pad=InputSystem.AddDevice<Gamepad>(); Mouse=InputSystem.AddDevice<Mouse>(); Touch=InputSystem.AddDevice<Touchscreen>();
                var root=new GameObject("List",typeof(RectTransform),typeof(Image),typeof(ScrollRect),typeof(ExplorationNodeChoiceCardList)); root.transform.SetParent(Canvas.transform,false);
                var rect=(RectTransform)root.transform; rect.sizeDelta=new Vector2(width,340);
                var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D)); viewport.transform.SetParent(root.transform,false);
                var vr=(RectTransform)viewport.transform; vr.anchorMin=Vector2.zero; vr.anchorMax=Vector2.one; vr.offsetMin=vr.offsetMax=Vector2.zero;
                var content=new GameObject("Content",typeof(RectTransform),typeof(ExplorationNodeChoiceLayoutGroup),typeof(ContentSizeFitter)); content.transform.SetParent(viewport.transform,false);
                var cr=(RectTransform)content.transform; cr.anchorMin=new Vector2(0,1); cr.anchorMax=Vector2.one; cr.pivot=new Vector2(.5f,1); cr.sizeDelta=Vector2.zero;
                content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                Scroll=root.GetComponent<ScrollRect>(); Scroll.viewport=vr; Scroll.content=cr; Scroll.horizontal=false; Scroll.movementType=ScrollRect.MovementType.Clamped;
                List=root.GetComponent<ExplorationNodeChoiceCardList>();
                List.ConfigureForEditor(Scroll,cr,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ExplorationNodeChoiceCard.prefab").GetComponent<ExplorationNodeChoiceCardView>(),content.GetComponent<ExplorationNodeChoiceLayoutGroup>());
                InputSystem.QueueStateEvent(Mouse,new MouseState {position=new Vector2(-100,-100)});
                Show();
            }
            public void Show(int count=3, bool firstEnabled=true) => List.Show(Enumerable.Range(0,count).Select(i=>new ExplorationNodeChoiceCardData(new ExplorationNodeChoiceRequest("run","set","node"+i),"Node "+i,"Description","",null,i!=0 || firstEnabled)).ToArray(),_=>Choices++);
            public void Key(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard,new KeyboardState(keys));
            public void Dispose()
            {
                Object.DestroyImmediate(Canvas); Object.DestroyImmediate(EventsObject);
                foreach(var blocker in blockers) if(blocker!=null) Object.DestroyImmediate(blocker);
                InputSystem.RemoveDevice(Keyboard); InputSystem.RemoveDevice(Pad); InputSystem.RemoveDevice(Mouse); InputSystem.RemoveDevice(Touch);
                InputSystem.settings.backgroundBehavior=background;
                foreach(var e in previous) if(e!=null)e.enabled=true;
            }
        }
        private static bool Focus(ExplorationNodeChoiceCardView card) => card.GetComponentsInChildren<Transform>(true).First(t=>t.name=="FocusVisual").gameObject.activeSelf;

        [UnityTest]
        public IEnumerator InitialSubmitFirstDirectionSimultaneousAndSeparateSubmit()
        {
            using var f=new Fixture(); yield return Frames();
            Assert.That(f.Events.currentSelectedGameObject,Is.Null);
            Assert.That(f.List.Cards.All(c=>!Focus(c)),Is.True);
            f.Key(Key.Enter); yield return Frames(); Assert.That(f.Choices,Is.Zero); Assert.That(f.Events.currentSelectedGameObject,Is.Null);
            f.Key(); yield return Frames();
            foreach(var direction in new[]{Key.UpArrow,Key.DownArrow,Key.LeftArrow,Key.RightArrow})
            {
                f.Show(); yield return Frames(); f.Key(direction,Key.Enter); yield return Frames();
                Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(f.List.Cards[0].gameObject)); Assert.That(f.Choices,Is.Zero);
                f.Key(); yield return Frames();
            }
            f.Key(Key.RightArrow); yield return Frames(); Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(f.List.Cards[1].gameObject));
            f.Key(); yield return Frames(); f.Key(Key.Enter); yield return new WaitForSecondsRealtime(.6f);
            Assert.That(f.Choices,Is.EqualTo(1)); yield return Frames(); Assert.That(f.Choices,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator HeldInputPooledFocusEmptyDisabledAndLifetime()
        {
            using var f=new Fixture(); yield return Frames();
            f.Key(Key.RightArrow,Key.Enter); yield return Frames();
            f.Show(); yield return new WaitForSecondsRealtime(.7f); Assert.That(f.Events.currentSelectedGameObject,Is.Null); Assert.That(f.Choices,Is.Zero);
            f.Key(); yield return Frames();
            for(var i=0;i<3;i++)
            {
                var reused=f.List.Cards[i]; f.Events.SetSelectedGameObject(reused.gameObject); f.Show(); yield return Frames();
                Assert.That(f.List.Cards[i],Is.SameAs(reused)); Assert.That(f.Events.currentSelectedGameObject,Is.Null); Assert.That(Focus(reused),Is.False);
            }
            f.Show(0); yield return Frames(); f.Key(Key.DownArrow); yield return Frames(); Assert.That(f.Events.currentSelectedGameObject,Is.Null);
            f.Key(); yield return Frames(); f.Show(3,false); yield return Frames(); f.Key(Key.DownArrow); yield return Frames();
            Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(f.List.Cards[1].gameObject));
            f.Key(); yield return Frames(); f.Key(Key.Enter); yield return null; f.List.Hide(); f.Show(); yield return new WaitForSecondsRealtime(.6f);
            Assert.That(f.Choices,Is.Zero,"Old confirmation cannot select the replacement set");
            f.List.gameObject.SetActive(false); f.List.gameObject.SetActive(true); f.Show(); yield return Frames(); Assert.That(f.Events.currentSelectedGameObject,Is.Null);
            Object.DestroyImmediate(f.EventsObject); f.Show(0); f.List.Hide();
        }
        [UnityTest]
        public IEnumerator GamepadWrappedScrollAndSubmit()
        {
            using var f=new Fixture(250); yield return Frames();
            InputSystem.QueueStateEvent(f.Pad,new GamepadState().WithButton(GamepadButton.DpadDown)); yield return Frames();
            Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(f.List.Cards[0].gameObject));
            for(var i=1;i<3;i++)
            {
                InputSystem.QueueStateEvent(f.Pad,new GamepadState()); yield return Frames();
                InputSystem.QueueStateEvent(f.Pad,new GamepadState().WithButton(GamepadButton.DpadDown)); yield return Frames();
                Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(f.List.Cards[i].gameObject));
            }
            Assert.That(f.Scroll.verticalNormalizedPosition,Is.LessThan(.1f));
            var corners=new Vector3[4]; ((RectTransform)f.List.Cards[2].transform).GetWorldCorners(corners);
            Assert.That(f.Scroll.viewport.InverseTransformPoint(corners[0]).y,Is.GreaterThanOrEqualTo(f.Scroll.viewport.rect.yMin-.5f));
            InputSystem.QueueStateEvent(f.Pad,new GamepadState()); yield return Frames();
            InputSystem.QueueStateEvent(f.Pad,new GamepadState().WithButton(GamepadButton.South)); yield return new WaitForSecondsRealtime(.6f); Assert.That(f.Choices,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator ModalLoadingAndOtherUiOwnInputAndRestoreFocus()
        {
            using var f=new Fixture(); yield return Frames();
            var modal=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ModalWindowHost.prefab"),f.Canvas.transform);
            var service=modal.GetComponent<GameWindowService>(); f.List.ConfigureWindowServiceForEditor(service);
            service.Host.SetVisible(true); f.Events.SetSelectedGameObject(null);
            f.Key(Key.RightArrow,Key.Enter); yield return Frames(); Assert.That(f.Events.currentSelectedGameObject,Is.Null); Assert.That(f.Choices,Is.Zero);
            f.Show(); yield return Frames(); Assert.That(f.Events.currentSelectedGameObject,Is.Null);
            service.Host.SetVisible(false); f.Key(); yield return Frames(); f.Key(Key.LeftArrow); yield return Frames();
            var first=f.List.Cards[0].gameObject; Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(first));
            var task=service.OpenAsync("system",first); while(!task.IsCompleted)yield return null;
            Assert.That(task.Result,Is.True); var modalFocus=f.Events.currentSelectedGameObject;
            f.Show(); Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(modalFocus));
            service.Close(); yield return Frames(); Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(first));
            f.Events.SetSelectedGameObject(service.Host.CloseButton.gameObject); var other=f.Events.currentSelectedGameObject;
            f.Show(); f.Key(); yield return Frames(); f.Key(Key.LeftArrow); yield return Frames(); Assert.That(f.Events.currentSelectedGameObject,Is.Not.EqualTo(first)); Assert.That(f.Choices,Is.Zero);
        }
        [UnityTest]
        public IEnumerator MouseHoverReuseClickAndTouchTapDrag()
        {
            using var f=new Fixture(250); yield return Frames();
            var point=RectTransformUtility.WorldToScreenPoint(null,f.List.Cards[0].transform.position);
            InputSystem.QueueStateEvent(f.Mouse,new MouseState {position=point}); yield return Frames();
            Assert.That(Focus(f.List.Cards[0]),Is.True); Assert.That(f.Events.currentSelectedGameObject,Is.Null);
            f.Show(); yield return Frames(); Assert.That(Focus(f.List.Cards[0]),Is.True,"Stationary pointer hover survives reuse");
            InputSystem.QueueStateEvent(f.Mouse,new MouseState {position=point,buttons=1}); yield return Frames();
            InputSystem.QueueStateEvent(f.Mouse,new MouseState {position=point}); yield return new WaitForSecondsRealtime(.6f); Assert.That(f.Choices,Is.EqualTo(1));
            f.Show(); InputSystem.QueueStateEvent(f.Mouse,new MouseState {position=new Vector2(-100,-100)}); yield return Frames();
            InputSystem.QueueStateEvent(f.Touch,new TouchState {touchId=1,position=point,phase=UnityEngine.InputSystem.TouchPhase.Began}); yield return Frames();
            InputSystem.QueueStateEvent(f.Touch,new TouchState {touchId=1,position=point+new Vector2(0,70),phase=UnityEngine.InputSystem.TouchPhase.Moved}); yield return Frames();
            InputSystem.QueueStateEvent(f.Touch,new TouchState {touchId=1,position=point+new Vector2(0,140),phase=UnityEngine.InputSystem.TouchPhase.Moved}); yield return Frames();
            InputSystem.QueueStateEvent(f.Touch,new TouchState {touchId=1,position=point+new Vector2(0,140),phase=UnityEngine.InputSystem.TouchPhase.Ended}); yield return Frames(); Assert.That(f.Choices,Is.EqualTo(1));
            f.Show(); yield return Frames();
            InputSystem.QueueStateEvent(f.Touch,new TouchState {touchId=2,position=point,phase=UnityEngine.InputSystem.TouchPhase.Began}); yield return Frames();
            InputSystem.QueueStateEvent(f.Touch,new TouchState {touchId=2,position=point,phase=UnityEngine.InputSystem.TouchPhase.Ended}); yield return new WaitForSecondsRealtime(.6f); Assert.That(f.Choices,Is.EqualTo(2));
        }
    }
}
