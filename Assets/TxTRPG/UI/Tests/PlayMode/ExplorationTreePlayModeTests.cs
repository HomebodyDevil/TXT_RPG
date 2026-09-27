#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Exploration;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Application.Exploration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace TxTRPG.UI.Tests
{
    public sealed class ExplorationTreePlayModeTests
    {
        private sealed class RandomSource:IExplorationRandomSource{public int Next(int max)=>0;}
        private static ExplorationRunState Run(string id="tree-test",int count=3)=>new(id,new HealthState(100),new ExplorationNodeGenerator(count,new[]{new ExplorationNodeWeight("combat",1)},new RandomSource()));
        private static void Complete(ExplorationRunState run){var n=run.CurrentChoices[1%run.CurrentChoices.Count];run.Select(run.CurrentChoiceSetId,n.Id);run.CompleteActive(n.Id,"test");}
        private static IEnumerator Frames(){yield return null;yield return null;yield return null;}
        private sealed class Fixture:IDisposable
        {
            public readonly GameObject Canvas;
            public readonly ExplorationNodeTreePanel Panel;
            public Fixture(float width=360,float height=600)
            {
                var source=Object.FindObjectsByType<ExplorationNodeTreePanel>(FindObjectsSortMode.None).Single();
                Canvas=new GameObject("TreeTestCanvas",typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
                Canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;Canvas.GetComponent<Canvas>().sortingOrder=32767;
                var clone=Object.Instantiate(source.gameObject,Canvas.transform);var rect=(RectTransform)clone.transform;
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(width,height);
                Panel=clone.GetComponent<ExplorationNodeTreePanel>();
                foreach(var view in clone.GetComponentsInChildren<ExplorationTreeNodeView>(true))Object.DestroyImmediate(view.gameObject);
                foreach(var view in clone.GetComponentsInChildren<ExplorationTreeEdgeView>(true))Object.DestroyImmediate(view.gameObject);
            }
            public void Dispose()=>Object.DestroyImmediate(Canvas);
        }
        [UnityTest]
        public IEnumerator ReuseScrollPolicyRunReplacementResizeAndEffects()
        {
            using var f=new Fixture();var run=Run();f.Panel.Show(ExplorationTreeProjection.Create(run));yield return Frames();
            var original=f.Panel.VisibleNodes[run.CurrentChoices[0].Id];var selectedBefore=EventSystem.current.currentSelectedGameObject;
            f.Panel.Show(ExplorationTreeProjection.Create(run));yield return Frames();Assert.That(f.Panel.VisibleNodes[run.CurrentChoices[0].Id],Is.SameAs(original));Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(selectedBefore));
            for(var i=0;i<10;i++)Complete(run);f.Panel.Show(ExplorationTreeProjection.Create(run));yield return Frames();Assert.That(f.Panel.Scroll.verticalNormalizedPosition,Is.Zero.Within(.01));
            f.Panel.Scroll.verticalNormalizedPosition=1;yield return Frames();var past=f.Panel.Scroll.content.anchoredPosition;
            Complete(run);f.Panel.Show(ExplorationTreeProjection.Create(run));yield return Frames();Assert.That(f.Panel.Scroll.content.anchoredPosition.y,Is.EqualTo(past.y).Within(.1));
            f.Panel.GoToLatest();yield return Frames();Assert.That(f.Panel.Scroll.verticalNormalizedPosition,Is.Zero.Within(.01));
            var current=f.Panel.VisibleNodes.Values.First();var identity=current.NodeId;var anchor=((RectTransform)current.transform).anchoredPosition;
            current.VisualRoot.localScale=Vector3.one*1.3f;current.Icon.color=Color.magenta;Assert.That(((RectTransform)current.transform).anchoredPosition,Is.EqualTo(anchor));
            current.gameObject.SetActive(false);current.gameObject.SetActive(true);Assert.That(current.VisualRoot.localScale,Is.EqualTo(Vector3.one));Assert.That(current.NodeId,Is.EqualTo(identity));
            var old=f.Panel.Scroll.content.anchoredPosition;f.Panel.RefreshStyle();yield return Frames();Assert.That(f.Panel.Scroll.content.anchoredPosition,Is.EqualTo(old));
            foreach(var size in new[]{new Vector2(120,360),new Vector2(700,340),new Vector2(360,600)})
            {((RectTransform)f.Panel.transform).sizeDelta=size;yield return Frames();Assert.That(f.Panel.Scroll.content.rect.width,Is.GreaterThanOrEqualTo(f.Panel.Scroll.viewport.rect.width));Assert.That(((RectTransform)f.Panel.LatestButton.transform).rect.width,Is.GreaterThanOrEqualTo(44));Assert.That(((RectTransform)f.Panel.LatestButton.transform).rect.height,Is.GreaterThanOrEqualTo(44));}
            var other=Run("replacement",1);f.Panel.Show(ExplorationTreeProjection.Create(other));yield return Frames();Assert.That(f.Panel.VisibleNodes.Values.All(n=>n.RunId=="replacement"),Is.True);Assert.That(f.Panel.Layout.Nodes.Length,Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator VirtualizedHundredAndFiveHundredStagesMeasureViewsAndRefresh()
        {
            using var f=new Fixture();yield return Frames();
            foreach(var stages in new[]{100,500})
            {
                var run=Run("long-"+stages);for(var i=1;i<stages;i++)Complete(run);
                var snapshot=ExplorationTreeProjection.Create(run);var bytes=GC.GetAllocatedBytesForCurrentThread();var clock=System.Diagnostics.Stopwatch.StartNew();
                f.Panel.Show(snapshot);clock.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;yield return Frames();
                Assert.That(f.Panel.Layout.Nodes.Length,Is.EqualTo(1+stages*3));Assert.That(f.Panel.VisibleNodes.Count,Is.LessThan(40));Assert.That(f.Panel.VisibleEdges.Count,Is.LessThan(40));
                f.Panel.Scroll.verticalNormalizedPosition=1;yield return Frames();Assert.That(f.Panel.VisibleNodes.ContainsKey("root"),Is.True);
                f.Panel.GoToLatest();yield return Frames();Assert.That(f.Panel.VisibleNodes.ContainsKey(run.CurrentChoices[0].Id),Is.True);
                Debug.Log($"TREE_VIEW_MEASUREMENT stages={stages} records={f.Panel.Layout.Nodes.Length} liveNodes={f.Panel.VisibleNodes.Count} liveEdges={f.Panel.VisibleEdges.Count} refreshMs={clock.Elapsed.TotalMilliseconds:F3} allocatedBytes={bytes}");
            }
        }
        [UnityTest]
        public IEnumerator SpriteAssetRoundTripLongNamesAndStableLineEndpoints()
        {
            using var f=new Fixture();yield return Frames();
            var style=Object.Instantiate(f.Panel.Style);var path="Assets/__TreeStyleRoundTrip_"+Guid.NewGuid().ToString("N")+".asset";
            var original=f.Panel.Style;
            try
            {
                style.lineSprite=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TxTRPG/UI/Icons/TemporaryMenu/inventory.png");
                style.types[0].sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TxTRPG/UI/Icons/TemporaryMenu/system.png");
                style.types[0].displayName="아주 긴 한글 탐험 노드 이름과 여러 지역의 전투 기록";
                UnityEditor.AssetDatabase.CreateAsset(style,path);UnityEditor.AssetDatabase.SaveAssetIfDirty(style);
                UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceUpdate);
                var saved=UnityEditor.AssetDatabase.LoadAssetAtPath<ExplorationTreeStyle>(path);Assert.That(saved.lineSprite,Is.Not.Null);Assert.That(saved.types[0].sprite.name,Is.EqualTo("system"));
                var so=new UnityEditor.SerializedObject(f.Panel);so.FindProperty("style").objectReferenceValue=saved;so.ApplyModifiedPropertiesWithoutUndo();
                var run=Run();f.Panel.Show(ExplorationTreeProjection.Create(run));yield return Frames();
                var card=f.Panel.VisibleNodes[run.CurrentChoices[0].Id];Assert.That(card.Icon.sprite,Is.SameAs(saved.types[0].sprite));
                Assert.That(card.Title.rectTransform.rect.height,Is.GreaterThanOrEqualTo(card.Title.preferredHeight-.5f));
                foreach(var edge in f.Panel.VisibleEdges.Values)
                {
                    Assert.That(edge.Line.sprite,Is.SameAs(saved.lineSprite));
                    var p=f.Panel.Layout.Nodes.Single(n=>n.Node.Id==edge.ParentId);var c=f.Panel.Layout.Nodes.Single(n=>n.Node.Id==edge.NodeId);
                    var rect=(RectTransform)edge.transform;
                    Assert.That(Vector3.Distance(rect.TransformPoint(new Vector3(-rect.rect.width*.5f,0)),f.Panel.Scroll.content.TransformPoint(p.Outgoing)),Is.LessThan(.1f));
                    Assert.That(Vector3.Distance(rect.TransformPoint(new Vector3(rect.rect.width*.5f,0)),f.Panel.Scroll.content.TransformPoint(c.Incoming)),Is.LessThan(.1f));
                }
                saved.lineSprite=null;f.Panel.RefreshStyle();yield return Frames();Assert.That(f.Panel.VisibleEdges.Values.All(e=>e.Line.sprite==null),Is.True);
                var texture=new Texture2D(32,64);var sprite=Sprite.Create(texture,new Rect(0,0,32,64),Vector2.one*.5f);
                try{saved.types[0].sprite=sprite;f.Panel.RefreshStyle();yield return Frames();Assert.That(card.Icon.preserveAspect,Is.True);Assert.That(card.Icon.sprite.rect.height,Is.EqualTo(64));}
                finally{Object.Destroy(sprite);Object.Destroy(texture);}
            }
            finally
            {
                var so=new UnityEditor.SerializedObject(f.Panel);so.FindProperty("style").objectReferenceValue=original;so.ApplyModifiedPropertiesWithoutUndo();
                UnityEditor.AssetDatabase.DeleteAsset(path);
            }
        }
        [UnityTest]
        public IEnumerator ControllerRefreshesActiveCompletedFailedAndRebindWithoutChangingSession()
        {
            var controller=Object.FindObjectsByType<ExplorationRunController>(FindObjectsSortMode.None).Single();
            var tree=Object.FindObjectsByType<ExplorationNodeTreePanel>(FindObjectsSortMode.None).Single();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var field=typeof(ExplorationRunController).GetField("run",flags);var original=controller.Run;
            var combat=(TxTRPG.Application.Dice.TemporaryDiceRollMenuController)new UnityEditor.SerializedObject(controller).FindProperty("combatController").objectReferenceValue;
            try
            {
                var run=Run("controller-boundaries");field.SetValue(controller,run);
                typeof(ExplorationRunController).GetMethod("RefreshRunPresentation",flags).Invoke(controller,null);yield return Frames();
                var select=typeof(ExplorationRunController).GetMethod("SelectChoice",flags,null,new[]{typeof(string),typeof(string)},null);
                select.Invoke(controller,new object[]{run.CurrentChoiceSetId,run.CurrentChoices[0].Id});yield return Frames();
                Assert.That(tree.Snapshot.Nodes.Single(n=>n.Id==run.ActiveNodeId).Status,Is.EqualTo(ExplorationNodeStatus.Active));Assert.That(tree.gameObject.activeInHierarchy,Is.True);
                var finished=typeof(ExplorationRunController).GetMethod("OnCombatFinished",flags);
                finished.Invoke(controller,new object[]{true});yield return Frames();
                Assert.That(tree.Snapshot.Nodes.Count,Is.EqualTo(7));Assert.That(tree.Snapshot.Nodes.Count(n=>n.Status==ExplorationNodeStatus.Completed&&!n.IsRoot),Is.EqualTo(1));
                select.Invoke(controller,new object[]{run.CurrentChoiceSetId,run.CurrentChoices[1].Id});yield return Frames();finished.Invoke(controller,new object[]{false});yield return Frames();
                Assert.That(tree.Snapshot.Nodes.Single(n=>n.Id==run.ActiveNodeId).Status,Is.EqualTo(ExplorationNodeStatus.Failed));
            }
            finally{combat.ClearCombat();field.SetValue(controller,original);typeof(ExplorationRunController).GetMethod("RefreshRunPresentation",flags).Invoke(controller,null);}
            var init=(System.Threading.Tasks.Task)typeof(ExplorationRunController).GetMethod("InitializeAsync").Invoke(controller,new object[]{null,System.Threading.CancellationToken.None});while(!init.IsCompleted)yield return null;if(init.IsFaulted)throw init.Exception;
            Assert.That(controller.Run,Is.SameAs(original));Assert.That(tree.Snapshot.RunId,Is.EqualTo(original.RunId));Assert.That(tree.Snapshot.Nodes.Count,Is.EqualTo(original.Nodes.Count));
        }
        [UnityTest]
        public IEnumerator ScreenNavigationReachesTreeAndModalKeepsPriority()
        {
            var tree=Object.FindObjectsByType<ExplorationNodeTreePanel>(FindObjectsSortMode.None).Single();
            var cards=Object.FindObjectsByType<ExplorationNodeChoiceCardList>(FindObjectsSortMode.None).Single();
            var windows=cards.WindowService;var events=EventSystem.current;var previous=events.currentSelectedGameObject;
            try
            {
                events.SetSelectedGameObject(cards.Cards[0].Button.gameObject);
                for(var i=0;i<5&&events.currentSelectedGameObject!=tree.BrowseButton.gameObject;i++)
                {
                    ExecuteEvents.Execute(events.currentSelectedGameObject,new AxisEventData(events){moveDir=MoveDirection.Left,moveVector=Vector2.left},ExecuteEvents.moveHandler);yield return Frames();
                }
                Assert.That(events.currentSelectedGameObject,Is.SameAs(tree.BrowseButton.gameObject),"Card navigation must reach the tree entry.");
                ExecuteEvents.Execute(tree.BrowseButton.gameObject,new BaseEventData(events),ExecuteEvents.submitHandler);yield return Frames();
                Assert.That(events.currentSelectedGameObject,Is.SameAs(tree.Navigation.gameObject));
                var task=windows.OpenAsync(windows.Pages[0].PageId,tree.BrowseButton.gameObject);while(!task.IsCompleted)yield return null;Assert.That(task.Result,Is.True);yield return Frames();
                var focus=events.currentSelectedGameObject;var offset=tree.Scroll.content.anchoredPosition;
                tree.Navigation.OnMove(new AxisEventData(events){moveVector=Vector2.down,moveDir=MoveDirection.Down});tree.Navigation.OnCancel(new BaseEventData(events));
                Assert.That(events.currentSelectedGameObject,Is.SameAs(focus));Assert.That(tree.Scroll.content.anchoredPosition,Is.EqualTo(offset));
                windows.Close();yield return Frames();Assert.That(events.currentSelectedGameObject,Is.SameAs(tree.BrowseButton.gameObject));
            }
            finally{if(windows.IsOpen)windows.Close();events.SetSelectedGameObject(previous);}
        }
        [UnityTest]
        public IEnumerator ResponsivePageStacksScrollsAndRestoresWideLayout()
        {
            var screen=Object.FindObjectsByType<TxTRPG.UI.ResponsiveMainScreen>(FindObjectsSortMode.None).Single();
            var viewport=screen.PageScroll.viewport;var min=viewport.anchorMin;var max=viewport.anchorMax;var size=viewport.sizeDelta;var pos=viewport.anchoredPosition;
            var tree=Object.FindObjectsByType<ExplorationNodeTreePanel>(FindObjectsSortMode.None).Single();
            var main=screen.PageScroll.content.GetComponent<TxTRPG.UI.FlexibleLayoutPanel>();var original=main.ContentRoot.GetChild(0).GetComponent<TxTRPG.UI.FlexibleLayoutItem>();var mode=original.SizeMode;var weight=original.Weight;
            try
            {
                viewport.anchorMin=viewport.anchorMax=Vector2.one*.5f;viewport.sizeDelta=new Vector2(390,844);yield return Frames();yield return Frames();
                Assert.That(screen.IsNarrow,Is.True);Assert.That(main.CurrentAxis,Is.EqualTo(TxTRPG.UI.FlexibleLayoutAxis.Vertical));Assert.That(tree.Scroll.viewport.rect.width,Is.GreaterThan(250));
                var content=screen.PageScroll.content;Assert.That(content.rect.height,Is.GreaterThan(viewport.rect.height));
                screen.PageScroll.verticalNormalizedPosition=1;yield return Frames();var before=content.anchoredPosition.y;
                tree.Scroll.GetComponent<TxTRPG.UI.NestedScrollRectBridge>().OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-3)});yield return Frames();Assert.That(content.anchoredPosition.y,Is.GreaterThan(before));
                screen.PageScroll.verticalNormalizedPosition=0;yield return Frames();Assert.That(screen.PageScroll.verticalNormalizedPosition,Is.Zero.Within(.01));
                viewport.sizeDelta=new Vector2(342,766);yield return Frames();Assert.That(tree.Scroll.viewport.rect.width,Is.GreaterThan(200));
                viewport.sizeDelta=new Vector2(1920,1080);yield return Frames();Assert.That(screen.IsNarrow,Is.False);Assert.That(main.CurrentAxis,Is.EqualTo(TxTRPG.UI.FlexibleLayoutAxis.Horizontal));Assert.That(original.SizeMode,Is.EqualTo(mode));Assert.That(original.Weight,Is.EqualTo(weight));
            }
            finally{viewport.anchorMin=min;viewport.anchorMax=max;viewport.sizeDelta=size;viewport.anchoredPosition=pos;}
            yield return Frames();
        }
        [UnityTest]
        public IEnumerator NarrowPageAcceptsRealTouchFromInnerTreeAndRevealsMenuFocus()
        {
            var screen=Object.FindObjectsByType<TxTRPG.UI.ResponsiveMainScreen>(FindObjectsSortMode.None).Single();var viewport=screen.PageScroll.viewport;
            var min=viewport.anchorMin;var max=viewport.anchorMax;var size=viewport.sizeDelta;var pos=viewport.anchoredPosition;
            var previous=Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);foreach(var e in previous)e.enabled=false;
            var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var go=new GameObject("PageTouchInput",typeof(EventSystem),typeof(InputSystemUIInputModule));go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                viewport.anchorMin=viewport.anchorMax=Vector2.one*.5f;viewport.sizeDelta=new Vector2(390,844);yield return Frames();yield return Frames();
                var tree=Object.FindObjectsByType<ExplorationNodeTreePanel>(FindObjectsSortMode.None).Single();screen.PageScroll.verticalNormalizedPosition=1;yield return Frames();
                var start=screen.PageScroll.content.anchoredPosition.y;var point=RectTransformUtility.WorldToScreenPoint(null,tree.Scroll.viewport.position);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=7,position=point,phase=UnityEngine.InputSystem.TouchPhase.Began});yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=7,position=point+new Vector2(0,60),phase=UnityEngine.InputSystem.TouchPhase.Moved});yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=7,position=point+new Vector2(0,120),phase=UnityEngine.InputSystem.TouchPhase.Moved});yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=7,position=point+new Vector2(0,120),phase=UnityEngine.InputSystem.TouchPhase.Ended});yield return Frames();
                Assert.That(screen.PageScroll.content.anchoredPosition.y,Is.GreaterThan(start+10));
                var menu=screen.PageScroll.content.GetComponentInChildren<TxTRPG.UI.Windows.GameMenuPanel>();var button=menu.Buttons.Last(b=>b.Visible).Button;
                go.GetComponent<EventSystem>().SetSelectedGameObject(button.gameObject);yield return Frames();yield return Frames();
                Assert.That(menu.LayoutInsufficientSpace,Is.False);var pointInViewport=viewport.InverseTransformPoint(button.transform.position);Assert.That(viewport.rect.Contains(pointInViewport),Is.True);
                ScreenCapture.CaptureScreenshot("Assets/Screenshots/exploration-tree-phone-bottom.png");yield return Frames();
            }
            finally{Object.DestroyImmediate(go);InputSystem.RemoveDevice(touch);InputSystem.settings.backgroundBehavior=background;foreach(var e in previous)if(e!=null)e.enabled=true;viewport.anchorMin=min;viewport.anchorMax=max;viewport.sizeDelta=size;viewport.anchoredPosition=pos;}
            yield return Frames();
        }
        [UnityTest]
        public IEnumerator RealKeyboardGamepadMouseAndTouchNavigation()
        {
            using var f=new Fixture(360,400);var run=Run();for(var i=0;i<12;i++)Complete(run);f.Panel.Show(ExplorationTreeProjection.Create(run));yield return Frames();
            var previous=Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);foreach(var e in previous)e.enabled=false;
            var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var go=new GameObject("TreeInput",typeof(EventSystem),typeof(InputSystemUIInputModule));var events=go.GetComponent<EventSystem>();go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var keyboard=InputSystem.AddDevice<Keyboard>();var pad=InputSystem.AddDevice<Gamepad>();var mouse=InputSystem.AddDevice<Mouse>();var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                yield return Frames();events.SetSelectedGameObject(f.Panel.BrowseButton.gameObject);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return Frames();Assert.That(events.currentSelectedGameObject,Is.SameAs(f.Panel.Navigation.gameObject));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames();
                var start=f.Panel.Scroll.content.anchoredPosition.y;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow));yield return Frames();Assert.That(f.Panel.Scroll.content.anchoredPosition.y,Is.LessThan(start));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return Frames();Assert.That(events.currentSelectedGameObject,Is.SameAs(f.Panel.BrowseButton.gameObject));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames();InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return Frames();Assert.That(events.currentSelectedGameObject,Is.SameAs(f.Panel.Navigation.gameObject));
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return Frames();start=f.Panel.Scroll.content.anchoredPosition.y;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadUp));yield return Frames();Assert.That(f.Panel.Scroll.content.anchoredPosition.y,Is.LessThan(start));
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return Frames();InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return Frames();Assert.That(f.Panel.Scroll.verticalNormalizedPosition,Is.Zero.Within(.01));
                var point=RectTransformUtility.WorldToScreenPoint(null,f.Panel.Scroll.viewport.position);InputSystem.QueueStateEvent(mouse,new MouseState{position=point,scroll=new Vector2(0,120)});yield return Frames();Assert.That(f.Panel.Scroll.verticalNormalizedPosition,Is.GreaterThan(0));
                f.Panel.GoToLatest();InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(-100,-100)});yield return Frames();start=f.Panel.Scroll.content.anchoredPosition.y;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=3,position=point,phase=UnityEngine.InputSystem.TouchPhase.Began});yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=3,position=point+new Vector2(0,-60),phase=UnityEngine.InputSystem.TouchPhase.Moved});yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=3,position=point+new Vector2(0,-120),phase=UnityEngine.InputSystem.TouchPhase.Moved});yield return Frames();
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=3,position=point+new Vector2(0,-120),phase=UnityEngine.InputSystem.TouchPhase.Ended});yield return Frames();
                Assert.That(f.Panel.Scroll.content.anchoredPosition.y,Is.LessThan(start));Assert.That(run.SelectedPath.Count,Is.EqualTo(12));
            }
            finally{Object.DestroyImmediate(go);InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(touch);InputSystem.settings.backgroundBehavior=background;foreach(var e in previous)if(e!=null)e.enabled=true;}
        }
    }
}
#endif
