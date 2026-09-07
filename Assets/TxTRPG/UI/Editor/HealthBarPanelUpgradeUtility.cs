using System;
using System.Linq;
using TxTRPG.Editor.Common.Menu;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class HealthBarPanelUpgradeUtility
    {
        [MenuItem(
            TxTRPGEditorMenuPaths.UiPrefabs + "Upgrade Selected Health Bars",
            false,
            TxTRPGEditorMenuPriorities.Refresh)]
        public static void UpgradeSelectedHealthBars()
        {
            var upgraded = 0;
            var prefabPaths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path) &&
                    path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();
            foreach (var path in prefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var changed = false;
                    foreach (var panel in root.GetComponentsInChildren<HealthBarPanel>(true))
                        changed |= UpgradePanel(panel, false);
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

            foreach (var panel in Selection.gameObjects
                         .Where(gameObject => !EditorUtility.IsPersistent(gameObject))
                         .SelectMany(gameObject => gameObject.GetComponentsInChildren<HealthBarPanel>(true))
                         .Distinct())
            {
                if (UpgradePanel(panel, true)) upgraded++;
            }

            Debug.Log(upgraded == 0
                ? "No selected HealthBarPanel required an upgrade."
                : $"Upgraded {upgraded} HealthBarPanel asset(s) or scene instance(s). " +
                  "Scene changes remain unsaved and support Undo.");
        }

        [MenuItem(
            TxTRPGEditorMenuPaths.UiPrefabs + "Convert Selected Health Bars to Padding Sizing",
            false,
            TxTRPGEditorMenuPriorities.Refresh + 1)]
        public static void ConvertSelectedHealthBarsToPaddingSizing()
        {
            var converted = ProcessSelection(ConvertPanelToPaddingSizing);
            Debug.Log(converted == 0
                ? "No selected HealthBarPanel required padding sizing conversion."
                : $"Converted {converted} HealthBarPanel asset(s) or scene instance(s) to padding sizing. " +
                  "Existing padding, fixed-size fallback values, alignment and offset were preserved.");
        }

        public static bool ConvertPanelToPaddingSizing(HealthBarPanel panel, bool recordUndo)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            var layout = panel.GetComponent<HealthBarLayoutController>();
            var upgraded = false;
            if (layout == null)
            {
                upgraded = UpgradePanel(panel, recordUndo);
                layout = panel.GetComponent<HealthBarLayoutController>();
            }
            if (layout == null ||
                layout.HorizontalSizeMode == HealthBarAxisSizeMode.Stretch &&
                layout.VerticalSizeMode == HealthBarAxisSizeMode.Stretch)
            {
                return upgraded;
            }
            if (recordUndo) Undo.RecordObject(layout, "Convert Health Bar to Padding Sizing");
            layout.SetSizeModes(HealthBarAxisSizeMode.Stretch, HealthBarAxisSizeMode.Stretch);
            EditorUtility.SetDirty(layout);
            return true;
        }

        public static bool UpgradePanel(HealthBarPanel panel, bool recordUndo)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            var root = panel.transform as RectTransform;
            if (root == null) return false;
            var changed = false;
            var layout = panel.GetComponent<HealthBarLayoutController>();
            if (layout == null)
            {
                layout = recordUndo
                    ? Undo.AddComponent<HealthBarLayoutController>(panel.gameObject)
                    : panel.gameObject.AddComponent<HealthBarLayoutController>();
                var background = FindDescendant(root, "BackgroundLayer") ?? root;
                var barRoot = FindDescendant(root, "BarRoot") ??
                              (panel.Slider?.transform.parent as RectTransform);
                layout.Configure(
                    background, barRoot,
                    HealthBarHorizontalAlignment.Center, HealthBarVerticalAlignment.Middle,
                    HealthBarAxisSizeMode.Stretch, HealthBarAxisSizeMode.Stretch,
                    new Vector2(240f, 24f), new RectOffset(12, 12, 12, 12), Vector2.zero);
                changed = true;
            }

            var properties = new SerializedObject(panel);
            changed |= SetIfMissing(properties, "backgroundVisualRoot",
                FindDescendant(root, "BackgroundVisualRoot") ?? FindDescendant(root, "BackgroundLayer"));
            changed |= SetIfMissing(properties, "barVisualRoot",
                FindDescendant(root, "BarVisualRoot") ?? panel.Slider?.transform as RectTransform);
            changed |= SetIfMissing(properties, "fillVisualRoot",
                FindDescendant(root, "FillVisualRoot") ?? panel.Slider?.targetGraphic?.rectTransform ?? panel.Slider?.fillRect);
            changed |= SetIfMissing(properties, "borderVisualRoot", FindDescendant(root, "BorderVisualRoot"));
            changed |= SetIfMissing(properties, "textVisualRoot",
                FindDescendant(root, "TextVisualRoot") ?? FindDescendant(root, "TextLayer"));
            if (!changed) return false;
            if (recordUndo) Undo.RecordObject(panel, "Upgrade Health Bar");
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
            return true;
        }

        private static bool SetIfMissing(SerializedObject properties, string name, RectTransform value)
        {
            var property = properties.FindProperty(name);
            if (property == null || property.objectReferenceValue != null || value == null) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private delegate bool PanelOperation(HealthBarPanel panel, bool recordUndo);

        private static int ProcessSelection(PanelOperation operation)
        {
            var changedCount = 0;
            var prefabPaths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path) &&
                    path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();
            foreach (var path in prefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var changed = root.GetComponentsInChildren<HealthBarPanel>(true)
                        .Aggregate(false, (current, panel) => operation(panel, false) || current);
                    if (!changed) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changedCount++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            foreach (var panel in Selection.gameObjects
                         .Where(gameObject => !EditorUtility.IsPersistent(gameObject))
                         .SelectMany(gameObject => gameObject.GetComponentsInChildren<HealthBarPanel>(true))
                         .Distinct())
            {
                if (operation(panel, true)) changedCount++;
            }
            return changedCount;
        }

        private static RectTransform FindDescendant(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<RectTransform>(true))
                if (child != root && child.name == name) return child;
            return null;
        }
    }
}
