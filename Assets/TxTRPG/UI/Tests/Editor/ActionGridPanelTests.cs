using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class ActionGridPanelTests
    {
        [Test]
        public void Entry_NormalizesPresentationValues()
        {
            var entry = new ActionGridEntry(
                null,
                ActionGridEntryKind.Item,
                null,
                null,
                quantity: -3,
                cooldownNormalized: 2f);

            Assert.That(entry.Id, Is.Empty);
            Assert.That(entry.DisplayName, Is.Empty);
            Assert.That(entry.Quantity, Is.Zero);
            Assert.That(entry.CooldownNormalized, Is.EqualTo(1f));
        }

        [TestCase(760f, 5)]
        [TestCase(360f, 4)]
        [TestCase(170f, 2)]
        [TestCase(60f, 1)]
        public void FixedColumns_TreatsConfiguredValueAsMaximumAndWraps(float width, int expectedColumns)
        {
            var columns = ActionGridPanel.CalculateColumnCount(
                ActionGridLayoutMode.FixedColumns,
                5,
                width,
                72f,
                8f);

            Assert.That(columns, Is.EqualTo(expectedColumns));
        }

        [Test]
        public void AdaptiveColumns_UsesAllColumnsThatFit()
        {
            var columns = ActionGridPanel.CalculateColumnCount(
                ActionGridLayoutMode.AdaptiveCellSize,
                2,
                520f,
                72f,
                8f);

            Assert.That(columns, Is.EqualTo(6));
        }

        [TestCase(760f)]
        [TestCase(60f)]
        public void ExactColumns_PreservesDeveloperConfiguredCount(float width)
        {
            Assert.That(ActionGridPanel.CalculateColumnCount(
                ActionGridLayoutMode.ExactColumns, 4, width, 72f, 8f), Is.EqualTo(4));
        }

        [TestCase(0, 5, 16f)]
        [TestCase(5, 5, 88f)]
        [TestCase(6, 5, 168f)]
        public void RequiredGridHeight_UsesDisplayedRowsSpacingAndPadding(
            int visibleCellCount,
            int columns,
            float expectedHeight)
        {
            var height = ActionGridPanel.CalculateRequiredGridHeight(
                visibleCellCount,
                columns,
                72f,
                8f,
                new RectOffset(8, 8, 8, 8));

            Assert.That(height, Is.EqualTo(expectedHeight));
        }

        [TestCase(ActionGridHorizontalAlignment.Left, 0f)]
        [TestCase(ActionGridHorizontalAlignment.Center, 52f)]
        [TestCase(ActionGridHorizontalAlignment.Right, 104f)]
        public void TrailingRow_UsesConfiguredHorizontalAlignment(
            ActionGridHorizontalAlignment alignment,
            float expectedOffset)
        {
            var offset = ActionGridLayoutGroup.CalculateTrailingRowOffset(
                alignment,
                3,
                2,
                96f,
                8f);

            Assert.That(offset, Is.EqualTo(expectedOffset));
        }

        [TestCase(3, 3)]
        [TestCase(3, 0)]
        [TestCase(1, 1)]
        public void TrailingRow_CompleteOrSingleColumn_HasNoOffset(
            int columns,
            int trailingCellCount)
        {
            var offset = ActionGridLayoutGroup.CalculateTrailingRowOffset(
                ActionGridHorizontalAlignment.Right,
                columns,
                trailingCellCount,
                96f,
                8f);

            Assert.That(offset, Is.Zero);
        }

        [Test]
        public void TrailingRowOffset_IncludesHorizontalSpacing()
        {
            var withoutSpacing = ActionGridLayoutGroup.CalculateTrailingRowOffset(
                ActionGridHorizontalAlignment.Right, 3, 2, 96f, 0f);
            var withSpacing = ActionGridLayoutGroup.CalculateTrailingRowOffset(
                ActionGridHorizontalAlignment.Right, 3, 2, 96f, 8f);

            Assert.That(withoutSpacing, Is.EqualTo(96f));
            Assert.That(withSpacing, Is.EqualTo(104f));
        }

        [Test]
        public void GridAlignment_PreservesFormerSlotAlignmentSerializationName()
        {
            var field = typeof(ActionGridPanel).GetField(
                "gridAlignment",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            var attribute = field?.GetCustomAttributes(
                    typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute),
                    false)
                .Cast<UnityEngine.Serialization.FormerlySerializedAsAttribute>()
                .SingleOrDefault();

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.oldName, Is.EqualTo("slotAlignment"));
        }

        [Test]
        public void RuntimeAlignmentSetters_UpdateIndependentLayoutResponsibilities()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<ActionGridPanel>();
                var grid = instance.GetComponentInChildren<ActionGridLayoutGroup>(true);

                panel.SetGridAlignment(ActionGridHorizontalAlignment.Right);
                Assert.That(panel.GridAlignment, Is.EqualTo(ActionGridHorizontalAlignment.Right));
                Assert.That(panel.IncompleteRowAlignment,
                    Is.EqualTo(ActionGridHorizontalAlignment.Left));
                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.UpperRight));
                Assert.That(grid.IncompleteRowAlignment,
                    Is.EqualTo(ActionGridHorizontalAlignment.Left));

                panel.SetIncompleteRowAlignment(ActionGridHorizontalAlignment.Center);
                Assert.That(panel.GridAlignment, Is.EqualTo(ActionGridHorizontalAlignment.Right));
                Assert.That(panel.IncompleteRowAlignment,
                    Is.EqualTo(ActionGridHorizontalAlignment.Center));
                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.UpperRight));
                Assert.That(grid.IncompleteRowAlignment,
                    Is.EqualTo(ActionGridHorizontalAlignment.Center));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(ScrollbarVisibilityMode.Hidden, ScrollbarSpaceMode.ReserveWhenVisible, false, 0f)]
        [TestCase(ScrollbarVisibilityMode.Hidden, ScrollbarSpaceMode.ReserveAlways, false, 0f)]
        [TestCase(ScrollbarVisibilityMode.Auto, ScrollbarSpaceMode.ReserveWhenVisible, false, 0f)]
        [TestCase(ScrollbarVisibilityMode.Auto, ScrollbarSpaceMode.ReserveWhenVisible, true, 24f)]
        [TestCase(ScrollbarVisibilityMode.Always, ScrollbarSpaceMode.ReserveWhenVisible, true, 24f)]
        [TestCase(ScrollbarVisibilityMode.Always, ScrollbarSpaceMode.Overlay, true, 0f)]
        [TestCase(ScrollbarVisibilityMode.Auto, ScrollbarSpaceMode.ReserveAlways, false, 24f)]
        [TestCase(ScrollbarVisibilityMode.Hidden, ScrollbarSpaceMode.ReserveSymmetricallyAlways, false, 24f)]
        [TestCase(ScrollbarVisibilityMode.Auto, ScrollbarSpaceMode.ReserveSymmetricallyAlways, false, 24f)]
        [TestCase(ScrollbarVisibilityMode.Auto, ScrollbarSpaceMode.ReserveSymmetricallyAlways, true, 24f)]
        [TestCase(ScrollbarVisibilityMode.Always, ScrollbarSpaceMode.ReserveSymmetricallyAlways, true, 24f)]
        public void ScrollbarInset_FollowsVisibilityAndSpaceMode(
            ScrollbarVisibilityMode visibility,
            ScrollbarSpaceMode spaceMode,
            bool overflows,
            float expectedInset)
        {
            var visible = ConfigurableScrollbarController.CalculateVisibility(visibility, overflows);
            var inset = ConfigurableScrollbarController.CalculateReservedInset(
                visibility,
                spaceMode,
                visible,
                16f,
                8f);

            Assert.That(inset, Is.EqualTo(expectedInset));
        }

        [TestCase(ScrollbarSide.Left, 24f, 0f)]
        [TestCase(ScrollbarSide.Right, 0f, -24f)]
        public void RuntimeScrollbarSettings_UpdateViewportOffsets(
            ScrollbarSide side,
            float expectedMinX,
            float expectedMaxX)
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var controller = instance.GetComponentInChildren<ConfigurableScrollbarController>(true);
                var viewport = instance.GetComponentInChildren<ScrollRect>(true).viewport;
                controller.Side = side;
                controller.SpaceMode = ScrollbarSpaceMode.ReserveWhenVisible;
                controller.Visibility = ScrollbarVisibilityMode.Always;

                Assert.That(controller.IsVisible, Is.True);
                Assert.That(controller.ReservedInset, Is.EqualTo(24f));
                Assert.That(viewport.offsetMin.x, Is.EqualTo(expectedMinX));
                Assert.That(viewport.offsetMax.x, Is.EqualTo(expectedMaxX));

                controller.SpaceMode = ScrollbarSpaceMode.Overlay;
                Assert.That(viewport.offsetMin.x, Is.Zero);
                Assert.That(viewport.offsetMax.x, Is.Zero);

                controller.Visibility = ScrollbarVisibilityMode.Hidden;
                Assert.That(controller.IsVisible, Is.False);
                Assert.That(controller.ReservedInset, Is.Zero);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AutoScrollbar_UpdatesVisibilityAndViewportWhenContentOverflowChanges()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var controller = instance.GetComponentInChildren<ConfigurableScrollbarController>(true);
                var scrollRect = instance.GetComponentInChildren<ScrollRect>(true);
                var fitter = scrollRect.content.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.enabled = false;
                controller.Side = ScrollbarSide.Right;
                controller.SpaceMode = ScrollbarSpaceMode.ReserveWhenVisible;
                controller.Visibility = ScrollbarVisibilityMode.Auto;

                scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1f);
                controller.Refresh();
                Assert.That(controller.IsVisible, Is.False);
                Assert.That(controller.ReservedInset, Is.Zero);
                Assert.That(scrollRect.viewport.offsetMax.x, Is.Zero);

                scrollRect.content.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    scrollRect.viewport.rect.height + 100f);
                controller.Refresh();
                Assert.That(controller.IsVisible, Is.True);
                Assert.That(controller.ReservedInset, Is.EqualTo(24f));
                Assert.That(scrollRect.viewport.offsetMax.x, Is.EqualTo(-24f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(ScrollbarSide.Left)]
        [TestCase(ScrollbarSide.Right)]
        public void SymmetricReservation_UsesSameViewportForEitherScrollbarSide(
            ScrollbarSide side)
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var controller = instance.GetComponentInChildren<ConfigurableScrollbarController>(true);
                var scrollRect = instance.GetComponentInChildren<ScrollRect>(true);
                var scrollbar = instance.GetComponentInChildren<Scrollbar>(true);
                controller.Side = side;
                controller.Width = 16f;
                controller.Gap = 8f;
                controller.SpaceMode = ScrollbarSpaceMode.ReserveSymmetricallyAlways;
                controller.Visibility = ScrollbarVisibilityMode.Hidden;

                Assert.That(scrollbar.gameObject.activeSelf, Is.False);
                Assert.That(controller.OppositeScrollbarArea, Is.Not.Null);
                Assert.That(controller.OppositeScrollbarArea.gameObject.activeSelf, Is.True);
                Assert.That(((RectTransform)scrollbar.transform).rect.width, Is.EqualTo(16f));
                Assert.That(controller.OppositeScrollbarArea.rect.width, Is.EqualTo(16f));
                Assert.That(scrollRect.viewport.offsetMin.x, Is.EqualTo(24f));
                Assert.That(scrollRect.viewport.offsetMax.x, Is.EqualTo(-24f));

                controller.Width = 20f;
                Assert.That(((RectTransform)scrollbar.transform).rect.width, Is.EqualTo(20f));
                Assert.That(controller.OppositeScrollbarArea.rect.width, Is.EqualTo(20f));
                controller.Gap = 5f;
                Assert.That(((RectTransform)scrollbar.transform).rect.width, Is.EqualTo(20f));
                Assert.That(controller.OppositeScrollbarArea.rect.width, Is.EqualTo(20f));
                Assert.That(scrollRect.viewport.offsetMin.x, Is.EqualTo(25f));
                Assert.That(scrollRect.viewport.offsetMax.x, Is.EqualTo(-25f));

                var scrollbarRect = (RectTransform)scrollbar.transform;
                if (side == ScrollbarSide.Right)
                {
                    Assert.That(scrollbarRect.anchorMin.x, Is.EqualTo(1f));
                    Assert.That(controller.OppositeScrollbarArea.anchorMin.x, Is.EqualTo(0f));
                }
                else
                {
                    Assert.That(scrollbarRect.anchorMin.x, Is.EqualTo(0f));
                    Assert.That(controller.OppositeScrollbarArea.anchorMin.x, Is.EqualTo(1f));
                }

                controller.Visibility = ScrollbarVisibilityMode.Always;
                Assert.That(scrollbar.gameObject.activeSelf, Is.True);
                Assert.That(scrollRect.viewport.offsetMin.x, Is.EqualTo(25f));
                Assert.That(scrollRect.viewport.offsetMax.x, Is.EqualTo(-25f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void SymmetricReservation_WithoutOppositeArea_KeepsInsetsWithoutException()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var controller = instance.GetComponentInChildren<ConfigurableScrollbarController>(true);
                var scrollRect = instance.GetComponentInChildren<ScrollRect>(true);
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("oppositeScrollbarArea").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex(
                        "ConfigurableScrollbarController.*without an OppositeScrollbarArea reference"));

                Assert.DoesNotThrow(() =>
                {
                    controller.SpaceMode = ScrollbarSpaceMode.ReserveSymmetricallyAlways;
                    controller.Refresh();
                });
                Assert.That(scrollRect.viewport.offsetMin.x, Is.EqualTo(24f));
                Assert.That(scrollRect.viewport.offsetMax.x, Is.EqualTo(-24f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(ScrollbarSide.Left, 24f, -24f)]
        [TestCase(ScrollbarSide.Right, 24f, -24f)]
        public void SymmetricOffsetCalculation_IgnoresScrollbarSide(
            ScrollbarSide side,
            float expectedMin,
            float expectedMax)
        {
            var offsets = ConfigurableScrollbarController.CalculateHorizontalOffsets(
                ScrollbarSpaceMode.ReserveSymmetricallyAlways,
                side,
                16f + 8f);

            Assert.That(offsets.x, Is.EqualTo(expectedMin));
            Assert.That(offsets.y, Is.EqualTo(expectedMax));
        }

        [Test]
        public void Capacity_RejectsOccupiedOverflowByDefault()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<ActionGridPanel>();
                panel.SetEntries(CreateEntries(4), 4);
                var result = panel.SetCapacity(2);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.OverflowCount, Is.EqualTo(2));
                Assert.That(panel.Capacity, Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void RemoveEntry_CompactsEntriesAndKeepsCapacityCells()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<ActionGridPanel>();
                panel.SetEntries(CreateEntries(3), 5);
                Assert.That(panel.RemoveEntry("entry-1"), Is.True);
                Assert.That(panel.EntryCount, Is.EqualTo(2));
                Assert.That(panel.Capacity, Is.EqualTo(5));
                var cells = instance.GetComponentsInChildren<ActionGridCell>(true);
                Assert.That(cells[0].HasEntry, Is.True);
                Assert.That(cells[1].HasEntry, Is.True);
                Assert.That(cells[2].HasEntry, Is.False);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void GeneratedPanel_HasConfigurableScrollbar()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            Assert.That(prefab.GetComponentInChildren<ConfigurableScrollbarController>(true), Is.Not.Null);
        }

        [Test]
        public void GeneratedPanel_ActionGridOwnsContentHeight()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var panel = prefab.GetComponent<ActionGridPanel>();
            var content = prefab.GetComponentInChildren<ScrollRect>(true).content;

            Assert.That(panel.VerticalPlacement,
                Is.EqualTo(ActionGridVerticalPlacement.CenterWhenContentFits));
            Assert.That(content.GetComponent<ContentSizeFitter>(), Is.Null);
        }

        [Test]
        public void VerticalPlacement_CentersFittingContentAndTopsOverflowingContent()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<ActionGridPanel>();
                var scrollRect = instance.GetComponentInChildren<ScrollRect>(true);
                var grid = scrollRect.content.GetComponent<ActionGridLayoutGroup>();

                panel.SetCapacity(4);
                panel.SetVerticalPlacement(ActionGridVerticalPlacement.CenterWhenContentFits);
                Canvas.ForceUpdateCanvases();

                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(scrollRect.content.rect.height,
                    Is.EqualTo(scrollRect.viewport.rect.height).Within(0.5f));

                panel.SetGridAlignment(ActionGridHorizontalAlignment.Left);
                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.MiddleLeft));
                panel.SetGridAlignment(ActionGridHorizontalAlignment.Right);
                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.MiddleRight));
                panel.SetGridAlignment(ActionGridHorizontalAlignment.Center);
                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.MiddleCenter));

                panel.SetCapacity(20);
                Canvas.ForceUpdateCanvases();

                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.UpperCenter));
                Assert.That(scrollRect.content.rect.height,
                    Is.GreaterThan(scrollRect.viewport.rect.height + 0.5f));

                scrollRect.verticalNormalizedPosition = 0f;
                var scrolledPosition = scrollRect.content.anchoredPosition;
                scrolledPosition.y = 80f;
                scrollRect.content.anchoredPosition = scrolledPosition;

                panel.SetCapacity(4);
                Canvas.ForceUpdateCanvases();

                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
                Assert.That(scrollRect.content.anchoredPosition.y, Is.Zero.Within(0.5f));

                panel.SetVerticalPlacement(ActionGridVerticalPlacement.Top);
                Assert.That(grid.childAlignment, Is.EqualTo(TextAnchor.UpperCenter));
                Assert.That(scrollRect.content.rect.height,
                    Is.EqualTo(scrollRect.viewport.rect.height).Within(0.5f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void GeneratedPanel_ScrollViewOuterMarginsPreservePanelCenter()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var scrollRect = instance.GetComponentInChildren<ScrollRect>(true);
                var scrollView = (RectTransform)scrollRect.transform;
                var controller = scrollView.GetComponent<ConfigurableScrollbarController>();

                Assert.That(scrollView.anchorMin.x, Is.EqualTo(0f));
                Assert.That(scrollView.anchorMax.x, Is.EqualTo(1f));
                Assert.That(scrollView.offsetMin.x, Is.EqualTo(18f).Within(0.01f));
                Assert.That(-scrollView.offsetMax.x, Is.EqualTo(18f).Within(0.01f));

                controller.Visibility = ScrollbarVisibilityMode.Hidden;
                controller.SpaceMode = ScrollbarSpaceMode.ReserveSymmetricallyAlways;
                controller.Width = 16f;
                controller.Gap = 8f;
                Canvas.ForceUpdateCanvases();
                controller.Refresh();

                var leftSpace = scrollView.offsetMin.x + scrollRect.viewport.offsetMin.x;
                var rightSpace = -scrollView.offsetMax.x - scrollRect.viewport.offsetMax.x;
                Assert.That(leftSpace, Is.EqualTo(42f).Within(0.01f));
                Assert.That(rightSpace, Is.EqualTo(leftSpace).Within(0.01f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void GeneratedPanel_InitiallyShowsCapacityAsEmptySlots()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<ActionGridPanel>();
                var activeCells = instance.GetComponentsInChildren<ActionGridCell>(false);

                Assert.That(panel.PopulationMode,
                    Is.EqualTo(ActionGridPopulationMode.FillCapacityWithEmptySlots));
                Assert.That(panel.GridAlignment,
                    Is.EqualTo(ActionGridHorizontalAlignment.Center));
                Assert.That(panel.IncompleteRowAlignment,
                    Is.EqualTo(ActionGridHorizontalAlignment.Left));
                var grid = instance.GetComponentInChildren<ActionGridLayoutGroup>(true);
                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.startCorner, Is.EqualTo(GridLayoutGroup.Corner.UpperLeft));
                Assert.That(grid.startAxis, Is.EqualTo(GridLayoutGroup.Axis.Horizontal));
                Assert.That(panel.PackingMode, Is.EqualTo(ActionGridPackingMode.CompactForward));
                var scrollbarController =
                    instance.GetComponentInChildren<ConfigurableScrollbarController>(true);
                Assert.That(scrollbarController.OppositeScrollbarArea, Is.Not.Null);
                Assert.That(
                    scrollbarController.OppositeScrollbarArea.GetComponent<Graphic>(),
                    Is.Null);
                Assert.That(scrollbarController.Visibility, Is.EqualTo(ScrollbarVisibilityMode.Hidden));
                Assert.That(scrollbarController.SpaceMode,
                    Is.EqualTo(ScrollbarSpaceMode.ReserveWhenVisible));
                Assert.That(activeCells.Length, Is.EqualTo(panel.Capacity));
                Assert.That(activeCells, Has.All.Matches<ActionGridCell>(cell => !cell.HasEntry));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void InitialContentCompletion_ResetsOverflowToTopOnlyOnce()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<ActionGridPanel>();
                var scrollRect = instance.GetComponentInChildren<ScrollRect>(true);
                panel.BeginInitialContentSetup();
                panel.SetCapacity(40);
                Canvas.ForceUpdateCanvases();
                scrollRect.content.anchoredPosition = new Vector2(
                    scrollRect.content.anchoredPosition.x, 120f);

                Assert.That(panel.CompleteInitialContentSetup(), Is.True);
                Assert.That(panel.HasAppliedInitialScroll, Is.True);
                Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
                Assert.That(scrollRect.content.anchoredPosition.y, Is.Zero.Within(0.5f));

                scrollRect.verticalNormalizedPosition = 0.35f;
                Canvas.ForceUpdateCanvases();
                panel.SetEntries(CreateEntries(40), 40);
                Canvas.ForceUpdateCanvases();
                Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(0.35f).Within(0.02f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AddingEntry_ReplacesFirstEmptySlotAndPreservesCapacitySlots()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<ActionGridPanel>();
                panel.SetCapacity(4);
                panel.ClearEntries();
                var entry = new ActionGridEntry(
                    "registered-item", ActionGridEntryKind.Item, null, "Registered Item");

                Assert.That(panel.TryAddEntry(entry), Is.True);
                var activeCells = instance.GetComponentsInChildren<ActionGridCell>(false);
                Assert.That(activeCells.Length, Is.EqualTo(4));
                Assert.That(activeCells.Count(cell => cell.HasEntry), Is.EqualTo(1));
                Assert.That(activeCells[0].HasEntry, Is.True);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void ContextMenuPosition_PrefersRightOfAnchor()
        {
            var position = ActionContextMenu.CalculatePosition(
                new Rect(-50f, -20f, 40f, 40f),
                new Vector2(100f, 80f),
                new Rect(-300f, -200f, 600f, 400f),
                10f,
                8f);

            Assert.That(position, Is.EqualTo(new Vector2(50f, 0f)));
        }

        [Test]
        public void ContextMenuPosition_FlipsLeftNearRightEdge()
        {
            var position = ActionContextMenu.CalculatePosition(
                new Rect(240f, -20f, 40f, 40f),
                new Vector2(100f, 80f),
                new Rect(-300f, -200f, 600f, 400f),
                10f,
                8f);

            Assert.That(position, Is.EqualTo(new Vector2(180f, 0f)));
        }

        [Test]
        public void ContextMenuPosition_UsesBelowWhenNeitherHorizontalSideFits()
        {
            var position = ActionContextMenu.CalculatePosition(
                new Rect(-20f, 50f, 40f, 40f),
                new Vector2(260f, 80f),
                new Rect(-150f, -200f, 300f, 400f),
                10f,
                8f);

            Assert.That(position, Is.EqualTo(new Vector2(0f, 0f)));
        }

        [Test]
        public void ContextMenuPosition_ClampsOversizedCandidateInsideBounds()
        {
            var position = ActionContextMenu.CalculatePosition(
                new Rect(80f, 70f, 20f, 20f),
                new Vector2(120f, 100f),
                new Rect(-100f, -80f, 200f, 160f),
                8f,
                10f);

            Assert.That(position.x, Is.InRange(-30f, 30f));
            Assert.That(position.y, Is.InRange(-20f, 20f));
        }

        [Test]
        public void GeneratedPrefabs_HaveRequiredBoundariesAndReferences()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();

            var cell = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab");
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionContextMenu.prefab");
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");

            Assert.That(cell.GetComponent<ActionGridCell>(), Is.Not.Null);
            Assert.That(cell.transform.Find("Border"), Is.Not.Null);
            Assert.That(cell.transform.Find("ContentRoot"), Is.Not.Null);
            Assert.That(cell.transform.Find("ContentRoot/CooldownOverlay"), Is.Not.Null);
            Assert.That(cell.transform.Find("SelectionFrame"), Is.Not.Null);
            Assert.That(menu.GetComponent<ActionContextMenu>(), Is.Not.Null);
            Assert.That(menu.transform.Find("OptionList/OptionTemplate"), Is.Not.Null);
            Assert.That(panel.GetComponent<ActionGridPanel>(), Is.Not.Null);
            Assert.That(panel.GetComponentInChildren<GridLayoutGroup>(true), Is.Not.Null);
            Assert.That(panel.transform.Find("ContextMenuAnchor/ActionContextMenu"), Is.Not.Null);
            var contextAnchor = (RectTransform)panel.transform.Find("ContextMenuAnchor");
            Assert.That(contextAnchor.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(contextAnchor.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void GeneratedDemo_ShowsMixedEntriesAndContextMenuPreview()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            ActionGridPanelDemoBuilder.CreateOrUpdateDemo();

            var data = AssetDatabase.LoadAssetAtPath<ActionGridPanelDemoData>(
                "Assets/TxTRPG/UI/DEMO/ActionGridPanel/ActionGridPanelDemoData.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/DEMO/ActionGridPanel/ActionGridPanelDemo.prefab");

            Assert.That(data, Is.Not.Null);
            var entries = data.CreateEntries();
            Assert.That(entries, Has.All.Matches<ActionGridEntry>(entry =>
                !string.IsNullOrWhiteSpace(entry.IconAssetId)));
            Assert.That(entries, Has.Some.Matches<ActionGridEntry>(entry => entry.Kind == ActionGridEntryKind.Item));
            Assert.That(entries, Has.Some.Matches<ActionGridEntry>(entry => entry.Kind == ActionGridEntryKind.Skill));
            Assert.That(prefab.GetComponent<ActionGridPanelDemoController>(), Is.Not.Null);
            var demoPanel = prefab.GetComponentInChildren<ActionGridPanel>(true);
            Assert.That(demoPanel.VerticalPlacement,
                Is.EqualTo(ActionGridVerticalPlacement.CenterWhenContentFits));
            Assert.That(
                demoPanel.GetComponentInChildren<ActionGridLayoutGroup>(true).childAlignment,
                Is.EqualTo(TextAnchor.MiddleCenter));
            var demoCells = prefab.GetComponentsInChildren<ActionGridCell>(true);
            Assert.That(demoCells.Length, Is.EqualTo(12));
            Assert.That(demoCells.Count(cell => cell.gameObject.name.EndsWith(": Empty")), Is.EqualTo(2));
            var contextMenu = prefab.GetComponentInChildren<ActionContextMenu>(true);
            Assert.That(contextMenu.gameObject.activeSelf, Is.True);
            Assert.That(contextMenu.GetComponentsInChildren<Button>(true).Count(button => button.gameObject.activeSelf),
                Is.GreaterThanOrEqualTo(3));
        }

        private static ActionGridEntry[] CreateEntries(int count)
        {
            return Enumerable.Range(0, count)
                .Select(index => new ActionGridEntry($"entry-{index}", ActionGridEntryKind.Item, null, $"Entry {index}"))
                .ToArray();
        }

    }
}
