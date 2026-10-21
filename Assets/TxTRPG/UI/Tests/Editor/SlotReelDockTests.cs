using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.UI.Shapes;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class SlotReelDockTests
    {
        private const string DockPrefabPath = "Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotReelDock.prefab";
        private static readonly int[] OneToSix = { 1, 2, 3, 4, 5, 6 };
        private GameObject canvas;
        private RectTransform host;
        private SlotReelDock dock;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("DockCanvas", typeof(Canvas));
            host = new GameObject("OverlayLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            host.SetParent(canvas.transform, false);
            host.anchorMin = host.anchorMax = new Vector2(.5f, .5f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DockPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab: {DockPrefabPath}");
            dock = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, host)).GetComponent<SlotReelDock>();
            dock.SetRespectSafeAreaForEditor(false);
            dock.SetSpinSettingsForEditor(SlotReelSpinSettings.Instant);
            Arrange(new Vector2(1920f, 1080f));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(canvas);

        private void Arrange(Vector2 size)
        {
            host.sizeDelta = size;
            dock.RefreshLayout();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(dock.ReelArea);
            dock.UpdatePaylineFrameForEditor();
        }

        private static SlotReelDisplay[] Reels(int count) =>
            Enumerable.Range(1, count).Select(i => new SlotReelDisplay($"reel.{i}", OneToSix)).ToArray();

        private static SlotSummaryDisplay Summary(string total, string multiplier = "") =>
            new("합계", total, "룬", "쌓임 → 분배", new[] { new SlotRuneLineDisplay("공격", "×1 → 5") }, "장착된 룬이 없습니다", "외 {0}줄", multiplier);

        private static SlotSpinDisplay Spin(string totalMultiplier, params int[] stops) => new(
            stops.Select((stop, i) => new SlotReelStopDisplay($"reel.{i + 1}", stop, OneToSix, i == 1 ? "×3" : string.Empty)).ToArray(),
            stops.Sum(stop => OneToSix[stop]),
            Summary("…"),
            Summary((stops.Sum(stop => OneToSix[stop]) * 2).ToString(), totalMultiplier));

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [Test]
        public void Prefab_HasTheDockHierarchyWithoutInputTargetsOrRuneTable()
        {
            Assert.That(dock.PresentationRoot, Is.Not.Null);
            Assert.That(dock.PresentationRoot.parent, Is.EqualTo(dock.transform));
            Assert.That(dock.ReelMachine.parent, Is.EqualTo(dock.PresentationRoot));
            Assert.That(dock.TotalCard, Is.Not.Null);
            Assert.That(dock.TotalCard.transform.parent, Is.EqualTo(dock.PresentationRoot));
            Assert.That(dock.TotalCard.transform.GetSiblingIndex(), Is.GreaterThan(dock.ReelMachine.GetSiblingIndex()), "the card draws above the reels");
            Assert.That(dock.TotalCard.VisualRoot, Is.Not.Null);
            Assert.That(dock.TotalCard.FillGraphic, Is.Not.Null);
            Assert.That(dock.TotalCard.BorderGraphic, Is.Not.Null);
            Assert.That(dock.TotalCard.BorderGraphic.gameObject.activeSelf, Is.EqualTo(dock.TotalCard.Appearance.BorderEnabled),
                "the border GameObject follows the Prefab's appearance");
            Assert.That(dock.PresentationRoot.Find("EffectRoot"), Is.Not.Null);
            Assert.That(dock.ReelCount, Is.EqualTo(3));
            Assert.That(dock.ReelViews.All(view => PrefabUtility.GetCorrespondingObjectFromOriginalSource(view.gameObject) != null), Is.True,
                "reels are SlotReel.prefab instances");
            Assert.That(dock.GetComponentsInChildren<SlotRuneTableView>(true), Is.Empty, "the dock has no rune table");
            Assert.That(dock.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget && !(g is TMPro.TMP_SubMeshUI)), Is.Empty,
                "the dock takes no input");
            Assert.That(dock.CanvasGroup.blocksRaycasts, Is.False);
            Assert.That(dock.CanvasGroup.interactable, Is.False);
            Assert.That(dock.SpinProfileForEditor, Is.Not.Null);
            Assert.That(dock.SkinCatalogForEditor, Is.Not.Null);
            Assert.That(dock.SkinTargetsForEditor.All(target => target.HasDefaults), Is.True, "every reel skin target has recorded defaults");
            Assert.That(dock.TotalLabelText, Is.EqualTo("합계"));
            Assert.That(dock.TotalMultiplierText, Is.Empty);
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void ReelCountChanges_KeepDividersOrderAndPayline(int count)
        {
            dock.SetReels(Reels(count));
            Arrange(new Vector2(1920f, 1080f));
            Assert.That(dock.ReelCount, Is.EqualTo(count));
            Assert.That(dock.ActiveDividerCount, Is.EqualTo(count - 1));
            var area = dock.ReelArea;
            var order = Enumerable.Range(0, area.childCount).Select(i => area.GetChild(i)).Where(t => t.gameObject.activeSelf)
                .Select(t => t.GetComponent<SlotReelViewBase>() != null ? "R" : "D");
            Assert.That(string.Concat(order), Is.EqualTo(string.Join("D", Enumerable.Repeat("R", count))));
            var frame = WorldRect(dock.PaylineFrameRect);
            var first = WorldRect(((SlotReelView)dock.ReelViews[0]).WindowForEditor);
            var last = WorldRect(((SlotReelView)dock.ReelViews[count - 1]).WindowForEditor);
            Assert.That(frame.xMin, Is.EqualTo(first.xMin).Within(.5f));
            Assert.That(frame.xMax, Is.EqualTo(last.xMax).Within(.5f));
            Assert.That(frame.height, Is.EqualTo(first.height / 5f).Within(.5f));
        }

        [Test]
        public void SpinAndStopAtResult_EndInTheSameStateAndIgnoreRuneLines()
        {
            dock.SetReels(Reels(3));
            var task = dock.PlaySpinAsync(Spin("×2", 2, 0, 5), true, CancellationToken.None);
            Assert.That(task.IsCompleted && task.Result, Is.True);
            var spun = dock.ReelViews.Select(v => v.PaylineValue).ToArray();
            Assert.That(spun, Is.EqualTo(new[] { 3, 1, 6 }));
            Assert.That(dock.TotalLabelText, Is.EqualTo("합계"));
            Assert.That(dock.TotalValueText, Is.EqualTo("20"));
            Assert.That(dock.TotalMultiplierText, Is.EqualTo("×2"));
            Assert.That(((SlotReelView)dock.ReelViews[1]).FortuneMultiplierText, Is.EqualTo("×3"));
            Assert.That(dock.IsSpinning, Is.False);

            dock.SetSummary(Summary("—"));
            dock.StopAtResult(Spin("×2", 2, 0, 5));
            Assert.That(dock.ReelViews.Select(v => v.PaylineValue).ToArray(), Is.EqualTo(spun));
            Assert.That(dock.TotalValueText, Is.EqualTo("20"));
            Assert.That(dock.TotalMultiplierText, Is.EqualTo("×2"));
            Assert.That(dock.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text.Contains("공격")), Is.False, "rune lines are ignored");

            dock.StopAtResult(Spin(string.Empty, 0, 0, 0));
            Assert.That(dock.TotalMultiplierText, Is.Empty, "an empty multiplier hides it");
        }

        [Test]
        public void ShowAndHide_MovePresentationRootOnly()
        {
            var shown = 0;
            dock.ShownChanged += _ => shown++;
            var rootSize = ((RectTransform)dock.transform).rect.size;
            var rootPosition = ((RectTransform)dock.transform).anchoredPosition;
            var areaSize = dock.ReelArea.rect.size;
            var cardPosition = ((RectTransform)dock.TotalCard.transform).anchoredPosition;
            dock.Show();
            Assert.That(shown, Is.Zero, "the same state raises nothing");
            Assert.That(dock.IsShown, Is.True);

            dock.Hide();
            Assert.That(shown, Is.EqualTo(1));
            Assert.That(dock.IsShown, Is.False);
            Assert.That(dock.CanvasGroup.alpha, Is.Zero);
            Assert.That(dock.gameObject.activeSelf, Is.True, "the dock stays active so a running spin continues");
            Assert.That(WorldRect(dock.PresentationRoot).yMax, Is.LessThanOrEqualTo(WorldRect(host).yMin + .5f), "hidden below the parent");
            Assert.That(((RectTransform)dock.transform).rect.size, Is.EqualTo(rootSize));
            Assert.That(((RectTransform)dock.transform).anchoredPosition, Is.EqualTo(rootPosition));
            Assert.That(dock.ReelArea.rect.size, Is.EqualTo(areaSize));
            Assert.That(((RectTransform)dock.TotalCard.transform).anchoredPosition, Is.EqualTo(cardPosition));
            dock.Hide();
            Assert.That(shown, Is.EqualTo(1));

            var hiddenSpin = dock.PlaySpinAsync(Spin(string.Empty, 1, 1, 1), true, CancellationToken.None);
            Assert.That(hiddenSpin.IsCompleted && hiddenSpin.Result, Is.True, "a hidden dock still presents the spin");

            dock.Show();
            Assert.That(shown, Is.EqualTo(2));
            Assert.That(dock.CanvasGroup.alpha, Is.EqualTo(1f));
            Assert.That(dock.PresentationRoot.anchoredPosition, Is.EqualTo(dock.ShownPosition));
        }

        [Test]
        public void DockedOffset_IsLimitedToTheParent()
        {
            dock.SetDockedOffset(new Vector2(5000f, 5000f));
            Assert.That(dock.DockedOffset, Is.EqualTo(new Vector2(5000f, 5000f)));
            var presentation = WorldRect(dock.PresentationRoot);
            var parent = WorldRect(host);
            Assert.That(presentation.xMax, Is.LessThanOrEqualTo(parent.xMax + .5f));
            Assert.That(presentation.yMax, Is.LessThanOrEqualTo(parent.yMax + .5f));
            dock.SetDockedOffset(Vector2.zero);
            Assert.That(dock.ShownPosition, Is.EqualTo(Vector2.zero));
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1024f, 768f)]
        [TestCase(390f, 844f)]
        [TestCase(750f, 1334f)]
        public void Placement_RespectsHeightWidthAndParentTop(float width, float height)
        {
            Arrange(new Vector2(width, height));
            var machineHeight = dock.ReelMachine.rect.height;
            Assert.That(machineHeight, Is.InRange(140f - .5f, 320f + .5f));
            var root = (RectTransform)dock.transform;
            var expectedWidth = Mathf.Min(width - 32f, 1200f);
            Assert.That(root.rect.width, Is.EqualTo(expectedWidth).Within(.5f));
            var rootRect = WorldRect(root);
            var parent = WorldRect(host);
            Assert.That(rootRect.center.x, Is.EqualTo(parent.center.x).Within(.5f), "centered");
            Assert.That(rootRect.yMin - parent.yMin, Is.EqualTo(12f).Within(.5f), "bottom margin");
            var card = WorldRect((RectTransform)dock.TotalCard.transform);
            Assert.That(card.yMax, Is.LessThanOrEqualTo(parent.yMax + .5f), "the card stays below the parent's top edge");
            Assert.That(card.yMax, Is.LessThanOrEqualTo(rootRect.yMax + .5f), "the dock reserves the card overhang");
            Assert.That(card.xMin, Is.GreaterThanOrEqualTo(rootRect.xMin - .5f));
            Assert.That(card.xMax, Is.LessThanOrEqualTo(rootRect.xMax + .5f));
        }

        [Test]
        public void CardShapes_ReachTheShapeGraphics()
        {
            var card = dock.TotalCard;
            var size = ((RectTransform)card.transform).rect.size;
            foreach (var shape in new[] { UiShape.Rectangle, UiShape.RoundedRectangle, UiShape.Circle, UiShape.Ellipse })
            {
                card.SetAppearance(card.Appearance.WithShape(shape));
                Assert.That(card.FillGraphic.Shape, Is.EqualTo(shape));
            }
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Triangle.*drawn as Rectangle"));
            card.SetAppearance(card.Appearance.WithShape(UiShape.Triangle));
            Assert.That(card.FillGraphic.Shape, Is.EqualTo(UiShape.Rectangle), "Triangle falls back to Rectangle");

            card.SetAppearance(card.Appearance.WithShape(UiShape.RoundedRectangle).WithCornerRadius(SlotTotalCardCornerRadiusUnit.ShortSideRatio, .2f));
            size = ((RectTransform)card.transform).rect.size;
            var shortSide = Mathf.Min(size.x, size.y);
            Assert.That(card.FillGraphic.CornerRadius, Is.EqualTo(shortSide * .2f).Within(.01f));
            card.SetAppearance(card.Appearance.WithCornerRadius(SlotTotalCardCornerRadiusUnit.ShortSideRatio, 3f));
            Assert.That(card.FillGraphic.CornerRadius, Is.EqualTo(shortSide * .5f).Within(.01f), "ratio clamps to 0.5");
            card.SetAppearance(card.Appearance.WithCornerRadius(SlotTotalCardCornerRadiusUnit.Pixels, 10f));
            Assert.That(card.FillGraphic.CornerRadius, Is.EqualTo(10f).Within(.01f));
            card.SetAppearance(card.Appearance.WithCornerRadius(SlotTotalCardCornerRadiusUnit.Pixels, 9999f));
            Assert.That(card.FillGraphic.CornerRadius, Is.EqualTo(shortSide * .5f).Within(.01f), "pixels clamp to half the short side");

            card.SetAppearance(card.Appearance.WithShape(UiShape.Ellipse));
            size = ((RectTransform)card.transform).rect.size;
            Assert.That(card.ContentRoot.rect.size.x, Is.EqualTo(size.x * .707f).Within(.5f));
            Assert.That(card.ContentRoot.rect.size.y, Is.EqualTo(size.y * .707f).Within(.5f));
            card.SetAppearance(card.Appearance.WithShape(UiShape.Circle));
            size = ((RectTransform)card.transform).rect.size;
            Assert.That(size.x, Is.EqualTo(size.y).Within(.5f), "Circle uses aspect ratio 1");
            Assert.That(card.ContentRoot.rect.size.x, Is.EqualTo(Mathf.Min(size.x, size.y) * .707f).Within(.5f));

            card.SetAppearance(card.Appearance.WithBorder(true, Color.black, 3f));
            Assert.That(card.BorderGraphic.gameObject.activeSelf, Is.True);
            Assert.That(card.BorderGraphic.DrawMode, Is.EqualTo(UiShapeDrawMode.InnerBorder));
            Assert.That(card.FillGraphic.raycastTarget || card.BorderGraphic.raycastTarget, Is.False);
        }

        // Puts the documented card defaults on this test instance only (the Prefab asset is not changed), so placement
        // tests do not depend on the look a developer chose for SlotReelDock.prefab.
        private void UseDefaultCardSettings()
        {
            var card = new SerializedObject(dock.TotalCard);
            card.FindProperty("heightRatio").floatValue = .42f;
            card.FindProperty("minimumHeight").floatValue = 56f;
            card.FindProperty("maximumHeight").floatValue = 140f;
            card.FindProperty("aspectRatio").floatValue = 1.8f;
            card.FindProperty("horizontalPosition").floatValue = .5f;
            card.FindProperty("overhangRatio").floatValue = .5f;
            card.ApplyModifiedPropertiesWithoutUndo();
            dock.TotalCard.SetAppearance(SlotTotalCardAppearance.Default);
            Arrange(new Vector2(1920f, 1080f));
            dock.TotalCard.Refresh(true);
        }

        [Test]
        public void CardPosition_DefaultIsCenteredHalfAboveAndClearOfThePayline()
        {
            UseDefaultCardSettings();
            var machine = WorldRect(dock.ReelMachine);
            var card = WorldRect((RectTransform)dock.TotalCard.transform);
            Assert.That(card.center.x, Is.EqualTo(machine.center.x).Within(.5f));
            Assert.That(card.yMax - machine.yMax, Is.EqualTo(card.height * .5f).Within(.5f), "half of the card is above the machine");
            var payline = WorldRect(dock.PaylineFrameRect);
            Assert.That(card.yMin, Is.GreaterThanOrEqualTo(payline.yMax), "the card does not cover the payline cell");
            var window = WorldRect(((SlotReelView)dock.ReelViews[0]).WindowForEditor);
            var cell = window.height / 5f;
            Assert.That(card.yMin, Is.GreaterThanOrEqualTo(window.yMax - cell - .5f), "the card bottom stays within the topmost visible cell (+2)");
        }

        [Test]
        public void SafeInsets_ParentInsideSafeArea_AreZero()
        {
            // 2400x1080 phone with a 100 px left cutout: the screen frame (1920x1080 at scale 1) sits inside the safe area.
            var safe = new Rect(100f, 0f, 2300f, 1080f);
            var frame = new Rect(340f, 0f, 1920f, 1080f);
            Assert.That(SlotReelDock.CalculateSafeInsets(frame, safe, Vector2.one), Is.EqualTo(Vector4.zero));
        }

        [Test]
        public void SafeInsets_FullScreenParent_MatchScreenInsets()
        {
            // Same insets as the previous whole-screen calculation: (left, right, bottom, top) in parent units.
            var screen = new Rect(0f, 0f, 2400f, 1080f);
            var safe = new Rect(100f, 30f, 2250f, 1000f);
            var insets = SlotReelDock.CalculateSafeInsets(screen, safe, new Vector2(.5f, .5f));
            Assert.That(insets.x, Is.EqualTo(50f).Within(.001f));
            Assert.That(insets.y, Is.EqualTo(25f).Within(.001f));
            Assert.That(insets.z, Is.EqualTo(15f).Within(.001f));
            Assert.That(insets.w, Is.EqualTo(25f).Within(.001f));
        }

        [Test]
        public void SafeInsets_InvalidScale_AreZero()
        {
            var screen = new Rect(0f, 0f, 100f, 100f);
            Assert.That(SlotReelDock.CalculateSafeInsets(screen, new Rect(10f, 0f, 90f, 100f), Vector2.zero), Is.EqualTo(Vector4.zero));
        }
            [Test]
        public void Prefab_UsesTheEasedTransition()
        {
            var transition = dock.TransitionBehaviourForEditor as SlotReelDockEasedTransition;
            Assert.That(transition, Is.Not.Null, "SlotReelDock.prefab assigns SlotReelDockEasedTransition");
            Assert.That(transition.gameObject, Is.EqualTo(dock.gameObject));
            Assert.That(transition.ShowDuration, Is.EqualTo(SlotReelDockEasedTransition.DefaultDuration));
            Assert.That(transition.HideDuration, Is.EqualTo(SlotReelDockEasedTransition.DefaultDuration));
            Assert.That(transition.Easing, Is.EqualTo(SlotEasingKind.InOutBack));
        }

        [Test]
        public void Transition_ReceivesTheDirection_AndIsCancelledByTheNextChange()
        {
            // Without a synchronization context the dock's continuation runs inside Complete.
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try { TransitionScenario(); }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        }

        private void TransitionScenario()
        {
            var fake = dock.gameObject.AddComponent<RecordingDockTransition>();
            dock.SetTransitionForEditor(fake, true);
            dock.ReduceMotion = true;

            dock.Hide();
            Assert.That(fake.Calls.Count, Is.EqualTo(1));
            Assert.That(fake.Calls[0].Showing, Is.False);
            Assert.That(fake.Calls[0].ReduceMotion, Is.True);
            Assert.That(fake.Calls[0].To, Is.EqualTo(dock.HiddenOffset));
            Assert.That(dock.CanvasGroup.alpha, Is.EqualTo(1f), "visible while the transition runs");
            Assert.That(dock.IsTransitionRunningForEditor, Is.True);

            dock.PresentationRoot.anchoredPosition = new Vector2(0f, -40f);
            dock.Show();
            Assert.That(fake.Calls[0].Token.IsCancellationRequested, Is.True, "the running hide is cancelled");
            Assert.That(fake.Calls.Count, Is.EqualTo(2));
            Assert.That(fake.Calls[1].Showing, Is.True);
            Assert.That(fake.Calls[1].From, Is.EqualTo(new Vector2(0f, -40f)), "starts from the current position");

            fake.Complete(1);
            Assert.That(dock.IsTransitionRunningForEditor, Is.False);
            Assert.That(dock.PresentationRoot.anchoredPosition, Is.EqualTo(dock.ShownPosition));
            Assert.That(dock.CanvasGroup.alpha, Is.EqualTo(1f));
        }

        private sealed class RecordingDockTransition : MonoBehaviour, ISlotReelDockTransition
        {
            public readonly struct Call
            {
                public Call(Vector2 from, Vector2 to, bool showing, bool reduceMotion, CancellationToken token, TaskCompletionSource<bool> completion)
                { From = from; To = to; Showing = showing; ReduceMotion = reduceMotion; Token = token; Completion = completion; }
                public Vector2 From { get; }
                public Vector2 To { get; }
                public bool Showing { get; }
                public bool ReduceMotion { get; }
                public CancellationToken Token { get; }
                public TaskCompletionSource<bool> Completion { get; }
            }

            public readonly List<Call> Calls = new();

            public Task PlayAsync(RectTransform target, Vector2 from, Vector2 to, bool showing, bool reduceMotion, CancellationToken cancellationToken)
            {
                var completion = new TaskCompletionSource<bool>();
                Calls.Add(new Call(from, to, showing, reduceMotion, cancellationToken, completion));
                return completion.Task;
            }

            public void Complete(int index) => Calls[index].Completion.SetResult(true);
        }
    }
}
