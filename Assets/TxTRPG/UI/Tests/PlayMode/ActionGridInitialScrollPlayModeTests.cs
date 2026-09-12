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
            return cell;
        }

        private static ActionGridEntry[] Entries(int count) => Enumerable.Range(0, count)
            .Select(i => new ActionGridEntry($"entry-{i}", ActionGridEntryKind.Item, null, $"Entry {i}"))
            .ToArray();

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
