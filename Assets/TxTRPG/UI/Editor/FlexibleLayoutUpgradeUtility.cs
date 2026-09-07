using System;
using System.Linq;
using TxTRPG.Editor.Common.Menu;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class FlexibleLayoutUpgradeUtility
    {
        private const string BackgroundContentLayerName = "BackgroundContentLayer";

        [MenuItem(
            TxTRPGEditorMenuPaths.UiPrefabs + "Upgrade Selected Flexible Layouts",
            false,
            TxTRPGEditorMenuPriorities.Refresh)]
        public static void UpgradeSelectedFlexibleLayouts()
        {
            var upgraded = 0;
            var prefabPaths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();

            foreach (var path in prefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var changed = false;
                    foreach (var panel in root.GetComponentsInChildren<FlexibleLayoutPanel>(true))
                    {
                        changed |= UpgradePanel(panel, false);
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        upgraded++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var scenePanels = Selection.gameObjects
                .Where(gameObject => !EditorUtility.IsPersistent(gameObject))
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<FlexibleLayoutPanel>(true))
                .Distinct()
                .ToArray();
            foreach (var panel in scenePanels)
            {
                if (UpgradePanel(panel, true)) upgraded++;
            }

            if (upgraded == 0)
            {
                Debug.Log("No selected FlexibleLayoutPanel required an upgrade.");
                return;
            }

            Debug.Log($"Upgraded {upgraded} selected FlexibleLayoutPanel asset(s) or scene instance(s). " +
                      "Scene changes remain unsaved and can be reverted with Undo.");
        }

        public static bool UpgradePanel(FlexibleLayoutPanel panel, bool recordUndo)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            var existing = panel.BackgroundContentRoot != null
                ? panel.BackgroundContentRoot
                : FindDirectChild(panel.transform, BackgroundContentLayerName);
            var layer = existing as RectTransform;
            var createdLayer = layer == null;
            var changed = createdLayer;
            if (createdLayer)
            {
                var layerObject = new GameObject(BackgroundContentLayerName, typeof(RectTransform));
                if (recordUndo) Undo.RegisterCreatedObjectUndo(layerObject, "Upgrade Flexible Layout");
                layer = (RectTransform)layerObject.transform;
                layer.SetParent(panel.transform, false);
                CopyRect(panel.ContentRoot, layer);
            }
            else if (layer.parent != panel.transform)
            {
                throw new InvalidOperationException(
                    "The configured BackgroundContentLayer must be a direct child of FlexibleLayoutPanel.");
            }

            var layoutElement = GetOrAdd<LayoutElement>(layer.gameObject, recordUndo, out var createdLayoutElement);
            changed |= createdLayoutElement;
            if (!layoutElement.ignoreLayout)
            {
                if (recordUndo) Undo.RecordObject(layoutElement, "Upgrade Flexible Layout");
                layoutElement.ignoreLayout = true;
                changed = true;
            }
            var canvasGroup = GetOrAdd<CanvasGroup>(layer.gameObject, recordUndo, out var createdCanvasGroup);
            changed |= createdCanvasGroup;
            if (createdCanvasGroup)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            var mask = GetOrAdd<RectMask2D>(layer.gameObject, recordUndo, out var createdMask);
            changed |= createdMask;
            if (createdMask) mask.enabled = false;
            var layout = GetOrAdd<FlexibleContentLayoutGroup>(layer.gameObject, recordUndo, out var createdLayout);
            changed |= createdLayout;

            var contentIndex = panel.ContentRoot.parent == panel.transform
                ? panel.ContentRoot.GetSiblingIndex()
                : panel.transform.childCount;
            if (layer.GetSiblingIndex() < contentIndex) contentIndex--;
            var targetIndex = Mathf.Clamp(contentIndex, 0, panel.transform.childCount - 1);
            if (layer.GetSiblingIndex() != targetIndex)
            {
                if (recordUndo) Undo.RecordObject(layer, "Upgrade Flexible Layout");
                layer.SetSiblingIndex(targetIndex);
                changed = true;
            }

            var serialized = new SerializedObject(panel);
            var referencesChanged =
                serialized.FindProperty("backgroundContentRoot").objectReferenceValue != layer ||
                serialized.FindProperty("backgroundContentLayout").objectReferenceValue != layout ||
                serialized.FindProperty("backgroundContentMask").objectReferenceValue != mask ||
                serialized.FindProperty("backgroundContentCanvasGroup").objectReferenceValue != canvasGroup;
            if (!changed && !referencesChanged) return false;

            if (recordUndo) Undo.RecordObject(panel, "Upgrade Flexible Layout");
            serialized.FindProperty("backgroundContentRoot").objectReferenceValue = layer;
            serialized.FindProperty("backgroundContentLayout").objectReferenceValue = layout;
            serialized.FindProperty("backgroundContentMask").objectReferenceValue = mask;
            serialized.FindProperty("backgroundContentCanvasGroup").objectReferenceValue = canvasGroup;
            serialized.FindProperty("useContentLayoutSettings").boolValue = true;
            serialized.FindProperty("previousUseContentLayoutSettings").boolValue = true;
            serialized.FindProperty("previousOverrideBackgroundContentMargins").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
            panel.Rebuild();
            return true;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }

        private static T GetOrAdd<T>(GameObject target, bool recordUndo, out bool created)
            where T : Component
        {
            var component = target.GetComponent<T>();
            created = component == null;
            if (!created) return component;
            return recordUndo ? Undo.AddComponent<T>(target) : target.AddComponent<T>();
        }

        private static void CopyRect(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
        }
    }
}
