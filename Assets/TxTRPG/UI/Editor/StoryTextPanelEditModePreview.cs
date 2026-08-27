using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    [InitializeOnLoad]
    public static class StoryTextPanelEditModePreview
    {
        private const string DemoDataPath = "Assets/TxTRPG/UI/Demo/StoryTextPanelDemoData.asset";
        private const string DemoPrefabPath = "Assets/TxTRPG/UI/Demo/StoryTextPanelDemo.prefab";
        private static double nextRefreshTime;

        static StoryTextPanelEditModePreview()
        {
            EditorApplication.update += RefreshOpenPreviews;
        }

        [MenuItem("Tools/TxT RPG/Refresh Story Text Panel Edit Mode Preview")]
        public static void RebuildPreviewPrefab()
        {
            var data = AssetDatabase.LoadAssetAtPath<StoryTextPanelDemoData>(DemoDataPath);
            if (data == null)
            {
                throw new UnityException($"Demo data was not found at {DemoDataPath}.");
            }

            var root = PrefabUtility.LoadPrefabContents(DemoPrefabPath);
            try
            {
                var panel = root.GetComponentInChildren<StoryTextPanel>(true);
                if (panel == null)
                {
                    throw new UnityException("The demo prefab does not contain a StoryTextPanel.");
                }

                RemoveExistingPreviewItems(root);
                var panelProperties = new SerializedObject(panel);
                var messagePrefab = (StoryMessageItem)panelProperties.FindProperty("messagePrefab").objectReferenceValue;
                var content = (RectTransform)panelProperties.FindProperty("content").objectReferenceValue;

                foreach (var entry in data.Entries)
                {
                    var itemObject = (GameObject)PrefabUtility.InstantiatePrefab(messagePrefab.gameObject, content);
                    var item = itemObject.GetComponent<StoryMessageItem>();
                    var message = entry.ToMessage();
                    item.Bind(message);
                    itemObject.AddComponent<StoryTextPanelDemoPreviewItem>();
                }

                RebuildLayoutAndOpacity(panel);
                PrefabUtility.SaveAsPrefabAsset(root, DemoPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log("StoryTextPanel Edit Mode preview refreshed.");
        }

        public static void RebuildLayoutAndOpacity(StoryTextPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            var properties = new SerializedObject(panel);
            var viewport = (RectTransform)properties.FindProperty("viewport").objectReferenceValue;
            var content = (RectTransform)properties.FindProperty("content").objectReferenceValue;
            var scrollRect = (ScrollRect)properties.FindProperty("scrollRect").objectReferenceValue;
            var fadeStart = properties.FindProperty("fadeStartFromBottom").floatValue;
            var minimumOpacity = properties.FindProperty("oldestVisibleOpacity").floatValue;
            var exponent = properties.FindProperty("fadeExponent").floatValue;

            if (viewport == null || content == null || scrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();

            var viewportRect = viewport.rect;
            var previewItems = panel.GetComponentsInChildren<StoryTextPanelDemoPreviewItem>(true);
            foreach (var previewItem in previewItems)
            {
                var item = previewItem.GetComponent<StoryMessageItem>();
                var itemRect = (RectTransform)previewItem.transform;
                var worldCenter = itemRect.TransformPoint(itemRect.rect.center);
                var localCenter = viewport.InverseTransformPoint(worldCenter);
                var normalizedHeight = Mathf.InverseLerp(viewportRect.yMin, viewportRect.yMax, localCenter.y);
                item.SetOpacity(StoryTextPanel.CalculateOpacity(
                    normalizedHeight,
                    fadeStart,
                    minimumOpacity,
                    exponent));
            }
        }

        private static void RefreshOpenPreviews()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = EditorApplication.timeSinceStartup + 0.15d;
            var previewItems = Object.FindObjectsByType<StoryTextPanelDemoPreviewItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var panels = new HashSet<StoryTextPanel>();
            foreach (var previewItem in previewItems)
            {
                var panel = previewItem.GetComponentInParent<StoryTextPanel>();
                if (panel != null)
                {
                    panels.Add(panel);
                }
            }

            foreach (var panel in panels)
            {
                RebuildLayoutAndOpacity(panel);
            }
        }

        private static void RemoveExistingPreviewItems(GameObject root)
        {
            var existingItems = root.GetComponentsInChildren<StoryTextPanelDemoPreviewItem>(true);
            foreach (var item in existingItems)
            {
                Object.DestroyImmediate(item.gameObject);
            }
        }
    }
}
