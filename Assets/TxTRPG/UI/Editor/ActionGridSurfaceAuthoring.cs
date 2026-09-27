using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    // Shared by the existing builder and targeted migration; never rebuilds the panel.
    public static class ActionGridSurfaceAuthoring
    {
        public static ActionGridSurfaceLayout Ensure(ActionGridPanel panel)
        {
            var properties = new SerializedObject(panel);
            var scroll = (ScrollRect)properties.FindProperty("scrollRect").objectReferenceValue;
            var controller = (ConfigurableScrollbarController)properties.FindProperty("scrollbarController").objectReferenceValue;
            var grid = (GridLayoutGroup)properties.FindProperty("gridLayout").objectReferenceValue;
            var header = panel.transform.Find("Header") as RectTransform;
            var surface = scroll.GetComponent<ActionGridSurfaceLayout>() ?? scroll.gameObject.AddComponent<ActionGridSurfaceLayout>();
            var horizontal = scroll.transform.Find("HorizontalScrollbar")?.GetComponent<Scrollbar>();
            if (horizontal == null)
            {
                var root = new GameObject("HorizontalScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
                root.transform.SetParent(scroll.transform, false);
                root.GetComponent<Image>().color = new Color32(30,35,45,255);
                var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                handle.transform.SetParent(root.transform, false);
                var rect = (RectTransform)handle.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                handle.GetComponent<Image>().color = new Color32(115,125,140,255);
                horizontal = root.GetComponent<Scrollbar>(); horizontal.handleRect = rect; horizontal.targetGraphic = handle.GetComponent<Image>();
                horizontal.direction = Scrollbar.Direction.LeftToRight;
                root.SetActive(false);
            }
            Scrollbar vertical = null; RectTransform opposite = null;
            if (controller != null)
            {
                var p = new SerializedObject(controller);
                vertical = (Scrollbar)p.FindProperty("scrollbar").objectReferenceValue;
                opposite = (RectTransform)p.FindProperty("oppositeScrollbarArea").objectReferenceValue;
            }
            surface.InitializeAuthoring(panel, header, (RectTransform)scroll.transform, scroll.viewport, scroll.content, scroll, controller, vertical, opposite, horizontal, grid);
            if (header != null) (header.GetComponent<ActionGridHeaderObserver>() ?? header.gameObject.AddComponent<ActionGridHeaderObserver>()).Bind(surface);
            panel.BindSurfaceLayout(surface);
            EditorUtility.SetDirty(surface); EditorUtility.SetDirty(panel);
            return surface;
        }
    }
}
