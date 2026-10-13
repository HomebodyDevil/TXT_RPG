using System;
using System.Linq;
using System.Text;
using TMPro;
using TxTRPG.Application.Players;
using TxTRPG.Application.SlotMachine;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (slot-fortune-critical). Creates TemporaryFortuneCriticalConfiguration.asset when it is
    // missing, fills the empty PlayerSessionHost reference in AppRoot.prefab, and adds the multiplier displays when they
    // are missing: SlotReel.prefab FortuneBadge > VisualRoot > Text, and SlotMachinePanel.prefab
    // TotalBox/TotalMultiplier > VisualRoot > Text (recording their default colors). Partial edits only: an existing
    // asset, reference or display object is kept, and a file is written only when something changed, so rerunning is
    // safe. It stops when an open Scene has unsaved changes. Excluded from the registered Prefab rebuild batch for the
    // same reasons as Apply Slot Machine Combat (it also changes AppRoot and a configuration asset). Retire it once every
    // working copy has been migrated.
    public static class TemporarySlotFortuneCriticalApply
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Slot Fortune Critical";
        public const string ConfigurationPath = TemporarySlotMachineIntegration.ConfigurationFolder + "/TemporaryFortuneCriticalConfiguration.asset";
        public const string ReelBadgePath = "FortuneBadge";
        public const string TotalMultiplierPath = "Content/SummaryArea/TotalBox/TotalMultiplier";

        // Amber, readable on the dark reel cells and the dark total box; the text itself carries the meaning.
        private static readonly Color FortuneColor = new Color32(0xFF, 0xC8, 0x3D, 0xFF);

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying Slot Fortune Critical.");
            if (EditorApplication.isCompiling) throw new InvalidOperationException("Wait for script compilation to finish.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Save and close Prefab Stage first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or revert the unsaved open Scenes before applying Slot Fortune Critical.");
            var summary = new StringBuilder("SLOT_FORTUNE_CRITICAL_APPLY:\n");
            // Prefabs are edited with an empty Scene open, as in Apply Slot Machine Panel Skin; the open Scenes are restored.
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try { ApplyAssetsAndPrefabs(summary); }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Validate();
            summary.AppendLine(" validation: passed. Safe to rerun.");
            Debug.Log(summary.ToString());
        }

        public static void ApplyAssetsAndPrefabs(StringBuilder summary)
        {
            var configuration = EnsureConfiguration(summary);
            EnsureAppRoot(configuration, summary);
            EditPrefab(TemporarySlotMachineIntegration.ReelPrefabPath, ApplyReel, summary);
            EditPrefab(TemporarySlotMachineIntegration.PanelPrefabPath, ApplyPanel, summary);
        }

        // ---------- Assets ----------

        private static TemporaryFortuneCriticalConfiguration EnsureConfiguration(StringBuilder summary)
        {
            var configuration = AssetDatabase.LoadAssetAtPath<TemporaryFortuneCriticalConfiguration>(ConfigurationPath);
            if (configuration != null) { summary.AppendLine($" kept {ConfigurationPath}"); return configuration; }
            configuration = ScriptableObject.CreateInstance<TemporaryFortuneCriticalConfiguration>();
            AssetDatabase.CreateAsset(configuration, ConfigurationPath);
            AssetDatabase.SaveAssets();
            summary.AppendLine($" created {ConfigurationPath} with the temporary defaults");
            return configuration;
        }

        private static void EnsureAppRoot(TemporaryFortuneCriticalConfiguration configuration, StringBuilder summary)
        {
            var path = TemporarySlotMachineIntegration.AppRootPath;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var host = root.GetComponentInChildren<PlayerSessionHost>(true) ?? throw new InvalidOperationException("AppRoot.prefab has no PlayerSessionHost.");
                var serialized = new SerializedObject(host);
                var field = serialized.FindProperty("temporaryFortuneCriticalConfiguration")
                    ?? throw new InvalidOperationException("PlayerSessionHost has no temporaryFortuneCriticalConfiguration field.");
                if (field.objectReferenceValue != null) { summary.AppendLine(" kept the AppRoot PlayerSessionHost Fortune Critical reference"); return; }
                field.objectReferenceValue = configuration;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {path}.");
                summary.AppendLine(" filled the AppRoot PlayerSessionHost Fortune Critical reference");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---------- Prefabs ----------

        private static void EditPrefab(string path, Action<GameObject, StringBuilder> edit, StringBuilder summary)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var before = Snapshot(root);
                edit(root, summary);
                if (Snapshot(root) == before) { summary.AppendLine($" {path} already matches; not saved"); return; }
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {path}.");
                summary.AppendLine($" updated {path}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static string Snapshot(GameObject root)
        {
            var builder = new StringBuilder();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                builder.Append(transform.GetSiblingIndex()).Append('/').Append(transform.name).Append(transform.gameObject.activeSelf).Append('\n');
                foreach (var component in transform.GetComponents<Component>())
                    if (component != null) builder.Append(component.GetType().Name).Append(EditorJsonUtility.ToJson(component)).Append('\n');
            }
            return builder.ToString();
        }

        // FortuneBadge goes last under the reel root (outside the Window mask, drawn above the cells). SlotReelView places
        // and sizes it at runtime; the stored rect is only a starting point.
        private static void ApplyReel(GameObject root, StringBuilder summary)
        {
            var view = root.GetComponent<SlotReelView>() ?? throw new InvalidOperationException("SlotReel.prefab has no SlotReelView.");
            var badge = root.transform.Find(ReelBadgePath) as RectTransform;
            if (badge == null)
            {
                var font = view.CellTextsForEditor.FirstOrDefault(t => t != null);
                badge = CreateDisplay(ReelBadgePath, root.transform, font, TextAlignmentOptions.TopRight, out _);
                badge.anchorMin = badge.anchorMax = new Vector2(.5f, .5f);
                badge.pivot = Vector2.one;
                badge.sizeDelta = new Vector2(44f, 28f);
                badge.anchoredPosition = new Vector2(44f, 14f);
                summary.AppendLine(" created SlotReel FortuneBadge > VisualRoot > Text");
            }
            var text = badge.Find("VisualRoot/Text")?.GetComponent<TMP_Text>()
                ?? throw new InvalidOperationException("SlotReel FortuneBadge has no VisualRoot/Text.");
            view.ConfigureFortuneBadgeForEditor(badge, text);
        }

        // TotalMultiplier is placed at runtime by SlotTotalMultiplierLayout (on TotalBox) beside or above the drawn total
        // value glyphs, so it never covers or moves TotalLabel or TotalValue (their anchors are unchanged).
        private static void ApplyPanel(GameObject root, StringBuilder summary)
        {
            var panel = root.GetComponent<SlotMachinePanel>() ?? throw new InvalidOperationException("SlotMachinePanel.prefab has no SlotMachinePanel.");
            var totalBox = root.transform.Find("Content/SummaryArea/TotalBox") ?? throw new InvalidOperationException("SlotMachinePanel.prefab has no TotalBox.");
            var value = totalBox.Find("TotalValue")?.GetComponent<TMP_Text>() ?? throw new InvalidOperationException("TotalBox has no TotalValue text.");
            var multiplier = root.transform.Find(TotalMultiplierPath) as RectTransform;
            if (multiplier == null)
            {
                multiplier = CreateDisplay("TotalMultiplier", totalBox, value, TextAlignmentOptions.TopLeft, out _);
                multiplier.anchorMin = multiplier.anchorMax = new Vector2(.5f, .5f);
                multiplier.pivot = new Vector2(0f, 1f);
                multiplier.sizeDelta = new Vector2(30f, 20f);
                multiplier.anchoredPosition = Vector2.zero;
                summary.AppendLine(" created TotalBox/TotalMultiplier > VisualRoot > Text");
            }
            var text = multiplier.Find("VisualRoot/Text")?.GetComponent<TMP_Text>()
                ?? throw new InvalidOperationException("TotalMultiplier has no VisualRoot/Text.");
            // A first version of this menu sized the text by its own height; the layout component sizes it now.
            var oldScaler = text.GetComponent<SlotHeightScaledText>();
            if (oldScaler != null)
            {
                UnityEngine.Object.DestroyImmediate(oldScaler, true);
                text.alignment = TextAlignmentOptions.TopLeft;
                summary.AppendLine(" removed the height-based font of TotalMultiplier/Text (SlotTotalMultiplierLayout sizes it)");
            }
            var layout = totalBox.GetComponent<SlotTotalMultiplierLayout>();
            if (layout == null)
            {
                layout = totalBox.gameObject.AddComponent<SlotTotalMultiplierLayout>();
                summary.AppendLine(" added SlotTotalMultiplierLayout to TotalBox");
            }
            layout.ConfigureForEditor(value, value.GetComponent<SlotHeightScaledText>(), multiplier, text);
            panel.ConfigureFortuneForEditor(multiplier, text, layout);
            // Nested SlotReel instances take the badge and its references from SlotReel.prefab (saved above).
        }

        // Layout root > VisualRoot (stretched; effects only) > Text (stretched). Created inactive: shown only with a
        // multiplier.
        private static RectTransform CreateDisplay(string name, Transform parent, TMP_Text fontSource, TextAlignmentOptions alignment, out TMP_Text text)
        {
            var root = NewRect(name, parent);
            var visual = NewRect("VisualRoot", root);
            Stretch(visual);
            var textRect = NewRect("Text", visual);
            Stretch(textRect);
            textRect.gameObject.AddComponent<CanvasRenderer>();
            var created = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            created.font = fontSource != null && fontSource.font != null ? fontSource.font : TMP_Settings.defaultFontAsset;
            created.text = "×2";
            created.fontSize = 24f;
            created.enableAutoSizing = false;
            created.fontStyle = FontStyles.Bold;
            created.alignment = alignment;
            created.textWrappingMode = TextWrappingModes.NoWrap;
            created.overflowMode = TextOverflowModes.Overflow;
            created.color = FortuneColor;
            created.raycastTarget = false;
            text = created;
            root.gameObject.SetActive(false);
            return root;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.SetAsLastSibling();
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        // ---------- Validation ----------

        public static void Validate()
        {
            var configuration = AssetDatabase.LoadAssetAtPath<TemporaryFortuneCriticalConfiguration>(ConfigurationPath);
            Require(configuration != null, $"{ConfigurationPath} exists");
            Require(configuration.TryCreateRules(TxTRPG.Gameplay.SlotMachine.FortuneWeightFunctionRegistry.Default, out _, out var reason),
                $"the configuration creates valid rules ({reason})");
            var appRoot = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.AppRootPath);
            var host = appRoot.GetComponentInChildren<PlayerSessionHost>(true);
            Require(host != null && host.TemporaryFortuneCriticalConfigurationForEditor != null, "AppRoot PlayerSessionHost references a Fortune Critical configuration");

            var reel = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.ReelPrefabPath).GetComponent<SlotReelView>();
            RequireDisplay(reel.FortuneBadgeForEditor, reel.FortuneTextForEditor, reel.transform, "SlotReel FortuneBadge");
            Require(reel.FortuneTextSkinForEditor.HasDefaults, "the SlotReel badge default color is recorded");

            var panelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.PanelPrefabPath);
            var panel = panelRoot.GetComponent<SlotMachinePanel>();
            RequireDisplay(panel.TotalMultiplierRootForEditor, panel.TotalMultiplierTextForEditor, panelRoot.transform.Find("Content/SummaryArea/TotalBox"), "TotalMultiplier");
            Require(panel.TotalMultiplierSkinForEditor.HasDefaults, "the total multiplier default color is recorded");
            Require(panel.TotalMultiplierLayoutForEditor != null && panel.TotalMultiplierLayoutForEditor.MultiplierRootForEditor == panel.TotalMultiplierRootForEditor &&
                panel.TotalMultiplierTextForEditor.GetComponent<SlotHeightScaledText>() == null, "SlotTotalMultiplierLayout places the total multiplier");
            foreach (var view in panel.ReelViews.OfType<SlotReelView>())
                RequireDisplay(view.FortuneBadgeForEditor, view.FortuneTextForEditor, view.transform, $"{view.name} FortuneBadge");
            Require(panelRoot.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "no slot machine panel graphic takes input");
        }

        private static void RequireDisplay(RectTransform root, TMP_Text text, Transform expectedParent, string label)
        {
            Require(root != null && text != null, $"{label} is connected");
            Require(root.parent == expectedParent, $"{label} is under {expectedParent.name}");
            Require(text.transform.parent != null && text.transform.parent.name == "VisualRoot" && text.transform.parent.parent == root,
                $"{label} text is {root.name}/VisualRoot/Text");
            Require(!root.gameObject.activeSelf, $"{label} is hidden until a multiplier is shown");
            Require(!text.raycastTarget && !text.enableAutoSizing, $"{label} text takes no input and does not use Auto Size");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Slot Fortune Critical check failed: {message}.");
        }
    }
}
