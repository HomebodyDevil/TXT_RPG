using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Editor;
using TxTRPG.UI.Exploration;
using TxTRPG.UI.Shapes;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.UI.Tests
{
    public sealed class PanelBoundaryCalculatorTests
    {
        [Test]
        public void VerticalStack_ZeroSpacing_CentersOnSharedEdge()
        {
            var upper = Rect.MinMaxRect(-100f, 0f, 100f, 200f);
            var lower = Rect.MinMaxRect(-100f, -150f, 100f, 0f);
            Assert.That(PanelBoundaryCalculator.TryCalculate(upper, lower, out var center, out var orientation), Is.True);
            Assert.That(orientation, Is.EqualTo(PanelBoundaryOrientation.Horizontal));
            Assert.That(center, Is.EqualTo(new Vector2(0f, 0f)));
        }

        [Test]
        public void VerticalStack_PositiveSpacing_UsesGapMidlineAndCommonWidth()
        {
            var upper = Rect.MinMaxRect(-80f, 12f, 120f, 300f);
            var lower = Rect.MinMaxRect(-100f, -200f, 100f, 0f);
            Assert.That(PanelBoundaryCalculator.TryCalculate(lower, upper, out var center, out var orientation), Is.True);
            Assert.That(orientation, Is.EqualTo(PanelBoundaryOrientation.Horizontal));
            Assert.That(center.y, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(center.x, Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void HorizontalRow_ProducesVerticalBoundary()
        {
            var left = Rect.MinMaxRect(0f, 0f, 300f, 400f);
            var right = Rect.MinMaxRect(310f, 50f, 600f, 400f);
            Assert.That(PanelBoundaryCalculator.TryCalculate(left, right, out var center, out var orientation), Is.True);
            Assert.That(orientation, Is.EqualTo(PanelBoundaryOrientation.Vertical));
            Assert.That(center, Is.EqualTo(new Vector2(305f, 225f)));
        }

        [Test]
        public void MissingOrOverlappingPanels_HaveNoBoundary()
        {
            var panel = Rect.MinMaxRect(0f, 0f, 100f, 100f);
            Assert.That(PanelBoundaryCalculator.TryCalculate(panel, Rect.zero, out _, out var none), Is.False);
            Assert.That(none, Is.EqualTo(PanelBoundaryOrientation.None));
            Assert.That(PanelBoundaryCalculator.TryCalculate(panel, Rect.MinMaxRect(50f, 50f, 150f, 150f), out _, out _), Is.False);
            Assert.That(PanelBoundaryCalculator.TryCalculate(panel, Rect.MinMaxRect(200f, 200f, 300f, 300f), out _, out _), Is.False, "No common span.");
        }

        [Test]
        public void ClampInside_KeepsTargetWithinBoundsOrCentersWhenTooLarge()
        {
            var bounds = Rect.MinMaxRect(0f, 0f, 200f, 100f);
            Assert.That(PanelBoundaryCalculator.ClampInside(new Vector2(5f, 98f), new Vector2(40f, 40f), bounds), Is.EqualTo(new Vector2(20f, 80f)));
            Assert.That(PanelBoundaryCalculator.ClampInside(new Vector2(5f, 50f), new Vector2(40f, 140f), bounds), Is.EqualTo(new Vector2(20f, 50f)));
        }
    }

    public sealed class UiShapeGeometryTests
    {
        private readonly List<Vector2> scratch = new();

        [Test]
        public void Contains_FollowsVisibleShape()
        {
            var rect = new Rect(-36f, -36f, 72f, 72f);
            var corner = new Vector2(34f, 34f);
            Assert.That(UiShapeGeometry.Contains(rect, UiShape.Circle, 0f, 16, Vector2.zero, scratch), Is.True);
            Assert.That(UiShapeGeometry.Contains(rect, UiShape.Circle, 0f, 16, corner, scratch), Is.False);
            Assert.That(UiShapeGeometry.Contains(rect, UiShape.Rectangle, 0f, 16, corner, scratch), Is.True);
            Assert.That(UiShapeGeometry.Contains(rect, UiShape.RoundedRectangle, 16f, 16, corner, scratch), Is.False);
            Assert.That(UiShapeGeometry.Contains(rect, UiShape.RoundedRectangle, 16f, 16, new Vector2(34f, 0f), scratch), Is.True);
            Assert.That(UiShapeGeometry.Contains(rect, UiShape.Circle, 0f, 16, new Vector2(40f, 0f), scratch), Is.False, "Outside the rect.");
        }

        [Test]
        public void CardShapeEnum_MirrorsSharedShapeEnum()
        {
            var card = Enum.GetValues(typeof(ExplorationCardShape)).Cast<ExplorationCardShape>().ToArray();
            Assert.That(card.Select(value => value.ToString()), Is.EqualTo(Enum.GetNames(typeof(UiShape))));
            Assert.That(card.Select(value => (int)value), Is.EqualTo(Enum.GetValues(typeof(UiShape)).Cast<UiShape>().Select(value => (int)value).ToArray()));
            Assert.That(Enum.GetNames(typeof(ExplorationCardShapeDrawMode)), Is.EqualTo(Enum.GetNames(typeof(UiShapeDrawMode))));
        }

        [Test]
        public void CardGraphic_AndSharedGraphic_ProduceIdenticalMeshes()
        {
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var card = new GameObject("Card", typeof(RectTransform)).AddComponent<ExplorationCardShapeGraphic>();
                var shared = new GameObject("Shared", typeof(RectTransform)).AddComponent<UiShapeGraphic>();
                card.transform.SetParent(root.transform, false); shared.transform.SetParent(root.transform, false);
                card.rectTransform.sizeDelta = shared.rectTransform.sizeDelta = new Vector2(186f, 144f);
                var serialized = new SerializedObject(shared);
                serialized.FindProperty("curveSegments").intValue = 6; // The card graphic's segment count.
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (ExplorationCardShape shape in Enum.GetValues(typeof(ExplorationCardShape)))
                foreach (ExplorationCardShapeDrawMode mode in Enum.GetValues(typeof(ExplorationCardShapeDrawMode)))
                {
                    card.Configure(shape, 14f, mode, 3f);
                    shared.Configure((UiShape)shape, 14f, (UiShapeDrawMode)mode, 3f);
                    Assert.That(Vertices(shared), Is.EqualTo(Vertices(card)), $"{shape}/{mode}");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static List<Vector3> Vertices(Graphic graphic)
        {
            var mesh = new Mesh();
            try
            {
                using var helper = new VertexHelper();
                typeof(Graphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
                return mesh.vertices.ToList();
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }

    public sealed class BoundaryCommandButtonTests
    {
        private sealed class FakeHandler : MonoBehaviour, IGameMenuCommandHandler
        {
            public bool Executable = true;
            public int Executions;
            public bool CanExecute(string commandId, out string unavailableReason)
            { unavailableReason = Executable ? string.Empty : "blocked"; return Executable && commandId == "test.command"; }
            public bool TryExecute(string commandId) { if (!CanExecute(commandId, out _)) return false; Executions++; Executable = false; return true; }
        }

        private GameObject canvas;
        private GameObject eventSystem;
        private BoundaryCommandButton command;
        private FakeHandler handler;
        private RectTransform upper, lower;
        private Button fallback;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvasRect = (RectTransform)canvas.transform; canvasRect.sizeDelta = new Vector2(800f, 600f);
            var content = new GameObject("ContentLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(canvasRect, false); content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.offsetMin = content.offsetMax = Vector2.zero;
            upper = CreatePanel("Upper", content, new Vector2(0f, 150f), new Vector2(600f, 288f));
            lower = CreatePanel("Lower", content, new Vector2(0f, -150f), new Vector2(600f, 288f));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(BoundaryCommandButtonPrefabUtility.EnsurePrefab(), content);
            command = instance.GetComponent<BoundaryCommandButton>();
            handler = canvas.AddComponent<FakeHandler>();
            fallback = new GameObject("Fallback", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            fallback.transform.SetParent(content, false);
            command.Placement.ConfigureForEditor(upper, lower, command.Placement.Target);
            command.ConfigureForEditor(command.Placement, command.Button.gameObject, command.Button, command.ShapedView, handler, "test.command", fallback);
            eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            InvokeLifecycle(eventSystem.GetComponent<EventSystem>(), "OnEnable"); // Registers EventSystem.current; it does not run in Edit Mode.
            command.BindForEditorTests();
        }

        [TearDown]
        public void TearDown()
        {
            command.UnbindForEditorTests();
            InvokeLifecycle(eventSystem.GetComponent<EventSystem>(), "OnDisable");
            Object.DestroyImmediate(eventSystem);
            Object.DestroyImmediate(canvas);
        }

        private static void InvokeLifecycle(EventSystem target, string method) =>
            typeof(EventSystem).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        [Test]
        public void Prefab_KeepsInputRootSeparateFromEffectVisuals()
        {
            var root = command.Button.transform;
            Assert.That(command.GetComponent<LayoutElement>().ignoreLayout, Is.True);
            Assert.That(command.GetComponent<FlexibleLayoutItem>(), Is.Null);
            Assert.That(root.Find("HitArea").GetComponent<UiShapeHitArea>().raycastTarget, Is.True);
            Assert.That(root.Find("HitArea").IsChildOf(command.ShapedView.VisualRoot), Is.False);
            Assert.That(root.GetComponent<GameMenuButtonView>(), Is.Not.Null);
            Assert.That(command.ShapedView.VisualRoot.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
            Assert.That(((RectTransform)root).sizeDelta, Is.EqualTo(BoundaryCommandButtonPrefabUtility.DefaultButtonSize));
            Assert.That(command.Button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(command.ShapedView.Appearance.shape, Is.EqualTo(UiShape.Circle));
        }

        [Test]
        public void Placement_CentersTargetOnBoundaryWithoutMovingPanels()
        {
            var before = new object[] { upper.rect, lower.rect, upper.anchoredPosition, lower.anchoredPosition }.Select(value => value.ToString()).ToArray();
            command.SetAvailable(true);
            command.Placement.ForceUpdate();
            var target = (RectTransform)command.Button.transform;
            Assert.That(command.Placement.HasBoundary, Is.True);
            Assert.That(command.Placement.Orientation, Is.EqualTo(PanelBoundaryOrientation.Horizontal));
            Assert.That(target.anchoredPosition.x, Is.EqualTo(0f).Within(.01f));
            Assert.That(target.anchoredPosition.y, Is.EqualTo(0f).Within(.01f));
            var after = new object[] { upper.rect, lower.rect, upper.anchoredPosition, lower.anchoredPosition }.Select(value => value.ToString()).ToArray();
            Assert.That(after, Is.EqualTo(before));

            lower.gameObject.SetActive(false);
            command.Placement.ForceUpdate();
            Assert.That(command.Placement.HasBoundary, Is.False);
            Assert.That(command.IsShown, Is.False, "Without a boundary the button hides.");
            lower.gameObject.SetActive(true);
            command.Placement.ForceUpdate();
            Assert.That(command.IsShown, Is.True);
        }

        [Test]
        public void Availability_AndExecutionState_FollowTheHandler()
        {
            Assert.That(command.IsShown, Is.False, "Hidden until the owner makes it available.");
            command.SetAvailable(true);
            Assert.That(command.IsShown, Is.True);
            Assert.That(command.IsInteractable, Is.True);
            Assert.That(command.ShapedView.VisualOpacity, Is.EqualTo(1f));

            command.Button.onClick.Invoke();
            command.Button.onClick.Invoke();
            Assert.That(handler.Executions, Is.EqualTo(1), "A repeated input is rejected by the handler state.");
            command.RefreshExecutionState();
            Assert.That(command.IsShown, Is.True);
            Assert.That(command.IsInteractable, Is.False);
            Assert.That(command.ShapedView.VisualOpacity, Is.LessThan(1f), "Disabled is not communicated by tint alone.");
            Assert.That(command.UnavailableReason, Is.EqualTo("blocked"));

            command.SetAvailable(false);
            Assert.That(command.IsShown, Is.False);
            Assert.That(command.Execute(), Is.False);
        }

        [Test]
        public void HidingWhileFocused_MovesFocusToFallbackOrClearsIt()
        {
            command.SetAvailable(true);
            EventSystem.current.SetSelectedGameObject(command.Button.gameObject);
            command.SetAvailable(false);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(fallback.gameObject));

            command.SetAvailable(true);
            EventSystem.current.SetSelectedGameObject(command.Button.gameObject);
            fallback.interactable = false;
            command.SetAvailable(false);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
        }

        [Test]
        public void FocusLostWhileDisabled_ReturnsWhenUsableAgain()
        {
            command.SetAvailable(true);
            EventSystem.current.SetSelectedGameObject(command.Button.gameObject);
            handler.Executable = false;
            command.RefreshExecutionState();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null, "Unity deselects a Selectable that becomes non-interactable.");
            var hiddenWindowControl = new GameObject("ConfirmButton");
            try
            {
                EventSystem.current.SetSelectedGameObject(hiddenWindowControl);
                hiddenWindowControl.SetActive(false); // A closed result window leaves its hidden control selected.
                handler.Executable = true;
                command.RefreshExecutionState();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(command.Button.gameObject));

                var visibleOther = fallback.gameObject;
                handler.Executable = false; command.RefreshExecutionState();
                EventSystem.current.SetSelectedGameObject(visibleOther);
                handler.Executable = true; command.RefreshExecutionState();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(visibleOther), "Focus the player moved elsewhere is kept.");
            }
            finally { Object.DestroyImmediate(hiddenWindowControl); }
        }

        [Test]
        public void ShapeModes_ConfigureVisualsAndHitArea()
        {
            var view = command.ShapedView;
            var hit = view.HitArea;
            var corner = ((RectTransform)hit.transform).rect.max - Vector2.one;
            view.ApplyAppearance(ShapedButtonAppearance.Default);
            Assert.That(hit.ContainsLocalPoint(Vector2.zero), Is.True);
            Assert.That(hit.ContainsLocalPoint(corner), Is.False, "Circle input follows the visible shape.");

            var appearance = ShapedButtonAppearance.Default;
            appearance.shape = UiShape.RoundedRectangle; appearance.cornerRadius = 10f;
            view.ApplyAppearance(appearance);
            Assert.That(hit.Shape, Is.EqualTo(UiShape.RoundedRectangle));
            appearance.shape = UiShape.Rectangle;
            view.ApplyAppearance(appearance);
            Assert.That(hit.ContainsLocalPoint(corner), Is.True);

            appearance.visualMode = ShapedButtonVisualMode.Image;
            appearance.shape = UiShape.Circle;
            view.ApplyAppearance(appearance);
            var background = view.VisualRoot.Find("Background").GetComponent<UiShapeGraphic>();
            var backgroundImage = view.VisualRoot.Find("BackgroundImage").GetComponent<Image>();
            Assert.That(background.enabled, Is.False);
            Assert.That(backgroundImage.enabled, Is.True);
            Assert.That(hit.Shape, Is.EqualTo(UiShape.Rectangle), "Image mode uses the rectangle by default.");
            Assert.That(command.Button.targetGraphic, Is.SameAs(backgroundImage));
            Assert.That(((RectTransform)command.Button.transform).sizeDelta, Is.EqualTo(BoundaryCommandButtonPrefabUtility.DefaultButtonSize));
        }

        [Test]
        public void EffectMaterials_UseOwnedInstancesAndResetCleanly()
        {
            var view = command.ShapedView;
            var shared = new Material(Canvas.GetDefaultCanvasMaterial()) { name = "SharedEffectForTest" };
            var sharedColor = shared.color;
            try
            {
                view.SetEffectMaterialsForEditor(shared, shared, shared, shared);
                var background = view.VisualRoot.Find("Background").GetComponent<Graphic>();
                Assert.That(background.material, Is.SameAs(shared));
                var instance = view.GetOrCreateEffectInstance(ShapedButtonEffectTarget.Background);
                Assert.That(instance, Is.Not.SameAs(shared));
                Assert.That(background.material, Is.SameAs(instance));
                instance.color = Color.red;
                Assert.That(shared.color, Is.EqualTo(sharedColor), "The shared Material is never modified.");

                view.SetEffectsAllowed(false);
                Assert.That(background.material, Is.SameAs(background.defaultMaterial), "Reduce Motion removes animated effects.");
                view.SetEffectsAllowed(true);
                Assert.That(background.material, Is.SameAs(instance));

                view.ResetEffects();
                Assert.That(instance == null, Is.True, "The instance is destroyed on reset.");
                Assert.That(background.material, Is.SameAs(shared));
                Assert.That(((RectTransform)command.Button.transform).sizeDelta, Is.EqualTo(BoundaryCommandButtonPrefabUtility.DefaultButtonSize));
            }
            finally
            {
                view.SetEffectMaterialsForEditor(null, null, null, null);
                Object.DestroyImmediate(shared);
            }
        }
    }
}
