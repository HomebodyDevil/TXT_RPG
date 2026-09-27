using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class ActionGridInitialScrollPlayModeTests
    {
        [UnityTest]
        public IEnumerator DelayedInitialContent_StartsAtTopAndLaterRefreshPreservesUserPosition()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var cellPrefab = CreateCellPrefab();
            try
            {
                var panel = CreatePanel(canvasObject.transform, cellPrefab, out var scrollRect);
                panel.BeginInitialContentSetup();
                panel.gameObject.SetActive(true);
                panel.SetEntries(Entries(40), 40);
                yield return null;
                Canvas.ForceUpdateCanvases();
                scrollRect.content.anchoredPosition = new Vector2(scrollRect.content.anchoredPosition.x, 100f);

                Assert.That(panel.CompleteInitialContentSetup(), Is.True);
                yield return null;
                Assert.That(scrollRect.content.rect.height, Is.GreaterThan(scrollRect.viewport.rect.height));
                Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
                Assert.That(scrollRect.content.anchoredPosition.y, Is.Zero.Within(0.5f));
                var firstCell = panel.GetComponentsInChildren<ActionGridCell>(false)[0];
                var firstBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    scrollRect.viewport, firstCell.transform);
                Assert.That(firstBounds.max.y,
                    Is.EqualTo(scrollRect.viewport.rect.yMax - 8f).Within(1f));

                scrollRect.verticalNormalizedPosition = 0.3f;
                yield return null;
                panel.SetEntries(Entries(40), 40);
                yield return null;
                Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(0.3f).Within(0.03f));
            }
            finally
            {
                Object.Destroy(canvasObject);
                Object.Destroy(cellPrefab);
            }
        }

        [UnityTest]
        public IEnumerator Presets_ResizeKeepsSlotsSelectionNavigationAndStableScroll()
        {
            var canvasObject = new GameObject("PresetCanvas",typeof(RectTransform),typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var prefab=CreateCellPrefab();
            try
            {
                var panel=CreatePanel(canvasObject.transform,prefab,out var scroll);
                panel.ConfigureBehavior(ActionGridPopulationMode.FillCapacityWithEmptySlots,ActionGridPackingMode.PreserveSlots,GridActivationBehavior.SelectOnly);
                panel.gameObject.SetActive(true); panel.SetEntries(Entries(14),14); panel.Select(4,false);
                foreach(var mode in new[]{ActionGridDisplayMode.Balanced,ActionGridDisplayMode.DistributedSpacing,ActionGridDisplayMode.LargeSlots,ActionGridDisplayMode.Manual})
                foreach(var size in new[]{new Vector2(128,200),new Vector2(229,121),new Vector2(610,242),new Vector2(96,144)})
                {
                    panel.SetDisplayMode(mode);
                    var rect=(RectTransform)panel.transform;
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,size.x);
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,size.y);
                    yield return null; Canvas.ForceUpdateCanvases();
                    var columns=panel.CurrentColumns; var height=scroll.content.rect.height;
                    yield return null; yield return null;
                    Assert.That(panel.CurrentColumns,Is.EqualTo(columns)); Assert.That(scroll.content.rect.height,Is.EqualTo(height).Within(.1f));
                    Assert.That(panel.Capacity,Is.EqualTo(14)); Assert.That(panel.VisibleCellCount,Is.EqualTo(14)); Assert.That(panel.SelectedIndex,Is.EqualTo(4));
                    var cells=scroll.content.GetComponentsInChildren<ActionGridCell>();
                    for(var index=0;index<cells.Length;index++) Assert.That(cells[index].Index,Is.EqualTo(index));
                    Assert.That(cells[0].Button.navigation.selectOnDown,Is.EqualTo(cells[columns].Button));
                    if(mode != ActionGridDisplayMode.Manual)
                    {
                        cells[cells.Length-1].OnSelect(null); yield return null;
                        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,cells[cells.Length-1].transform);
                        Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin-.5f));
                        Assert.That(bounds.max.y,Is.LessThanOrEqualTo(scroll.viewport.rect.yMax+.5f));
                    }
                    scroll.verticalNormalizedPosition=.4f;
                    panel.SetEntries(Entries(14),14); yield return null;
                    Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(.4f).Within(.04f));
                }
            }
            finally { Object.Destroy(canvasObject); Object.Destroy(prefab); }
        }

        private static ActionGridPanel CreatePanel(Transform parent, ActionGridCell cellPrefab,
            out ScrollRect scrollRect)
        {
            var root = new GameObject("ActionGridPanel", typeof(RectTransform));
            root.SetActive(false); root.transform.SetParent(parent, false);
            var rootRect = (RectTransform)root.transform; rootRect.sizeDelta = new Vector2(420, 240);
            var viewportObject = new GameObject("Viewport", typeof(RectTransform));
            viewportObject.transform.SetParent(root.transform, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero; viewport.offsetMax = Vector2.zero;
            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(ActionGridLayoutGroup));
            contentObject.transform.SetParent(viewport, false);
            var content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
            scrollRect = root.AddComponent<ScrollRect>(); scrollRect.viewport = viewport;
            scrollRect.content = content; scrollRect.horizontal = false; scrollRect.vertical = true;
            var panel = root.AddComponent<ActionGridPanel>();
            Set(panel, "cellPrefab", cellPrefab); Set(panel, "viewport", viewport);
            Set(panel, "content", content); Set(panel, "gridLayout", contentObject.GetComponent<ActionGridLayoutGroup>());
            Set(panel, "scrollRect", scrollRect); Set(panel, "initialCapacity", 40); Set(panel, "capacity", 40);
            Set(panel, "fixedColumns", 4); Set(panel, "minimumCellSize", new Vector2(72, 72));
            Set(panel, "maximumCellSize", new Vector2(72, 72)); Set(panel, "padding", new RectOffset(8, 8, 8, 8));
            return panel;
        }

        private static ActionGridCell CreateCellPrefab()
        {
            var root = new GameObject("CellPrefab", typeof(RectTransform), typeof(Button));
            root.SetActive(false);
            var cell = root.AddComponent<ActionGridCell>();
            Set(cell, "button", root.GetComponent<Button>());
            foreach (var field in new[] { "disabledOverlay", "selectionFrame", "emptySlotVisual" })
            {
                var visual = new GameObject(field, typeof(RectTransform));
                visual.transform.SetParent(root.transform, false);
                var visualRect = (RectTransform)visual.transform;
                visualRect.anchorMin = Vector2.zero; visualRect.anchorMax = Vector2.one;
                visualRect.offsetMin = Vector2.zero; visualRect.offsetMax = Vector2.zero;
                Set(cell, field, visual);
            }
            return cell;
        }

        private static ActionGridEntry[] Entries(int count) => Enumerable.Range(0, count)
            .Select(i => new ActionGridEntry($"entry-{i}", ActionGridEntryKind.Item, null, $"Entry {i}"))
            .ToArray();

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
