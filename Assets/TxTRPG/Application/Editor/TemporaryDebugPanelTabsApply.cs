using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using TxTRPG.Application.SlotMachine;
using TxTRPG.UI;
using TxTRPG.UI.DebugPanels;
using TxTRPG.UI.Encounters;
using TxTRPG.UI.Shapes;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (candidate-main-scene-debug-panel-tabs-and-slot-debug). Edits the stored assets in place:
    // 1. EncounterDebugPanel.prefab: title "DEBUG", Body/TabBar (이벤트 판정, 슬롯), Body/Pages/EncounterPage holding the
    //    existing Body children (moved, not recreated, so every serialized reference and Scene override stays) and
    //    Body/Pages/SlotPage (ScrollRect, SlotDebugTabView whose height grows down to the parent's bottom edge minus 16),
    //    plus DebugPanelTabGroup on the root. An already restructured Prefab only gets missing page height bounds and
    //    the missing SlotPage height reference (Pages/EncounterPage; slot-debug-tab-page-height-fix).
    // 2. SlotReelDock.prefab: adds SlotReelDockEasedTransition (0.3 s / 0.3 s, InOutBack) and assigns transitionBehaviour.
    // 3. Candidate_MainScene: OverlayLayer/SlotReelDockClip (stretch, RectMask2D) before the debug panel, the existing
    //    SlotReelDock instance moved into it, and Canvas/SlotReelDockDebugController with its references.
    // A step whose target is already in place logs "kept" and writes nothing, so rerunning is safe. It stops without
    // changes in Play Mode, while compiling, in Prefab Stage or with unsaved open Scenes. Saves only the two Prefabs and
    // Candidate_MainScene. Not part of the Prefab rebuild batch (the Prefabs are developer-owned). Retire it once the user
    // has applied and verified the feature.
    public static class TemporaryDebugPanelTabsApply
    {
        // TxTRPGEditorMenuPaths.Application + "Temporary/Apply Candidate Debug Panel Tabs" (this assembly does not
        // reference TxTRPG.Editor.Common).
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Candidate Debug Panel Tabs";
        public const string LogPrefix = "DEBUG_PANEL_TABS_APPLY";
        public const string ScenePath = "Assets/Scenes/Candidate_MainScene.unity";
        public const string DebugPanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/Encounters/EncounterDebugPanel.prefab";
        public const string DockPrefabPath = "Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotReelDock.prefab";
        public const string SlotMachineConfigurationPath = "Assets/TxTRPG/Application/Configuration/TemporarySlotMachineConfiguration.asset";
        public const string ClipName = "SlotReelDockClip";
        public const string ControllerName = "SlotReelDockDebugController";
        public const string Title = "DEBUG";
        public const string EncounterTabLabel = "이벤트 판정";
        public const string SlotTabLabel = "슬롯";
        // Same margin as the panel's top-right placement (TemporaryEncounterTogglesApply.DebugPanelMargin).
        public const float DebugPanelMargin = 16f;

        private static readonly Color ControlColor = new Color32(0x2B, 0x38, 0x5E, 0xFF);
        private static readonly Color TabColor = new Color32(0x1C, 0x27, 0x45, 0xFF);
        private static readonly Color SelectedColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (!CanApply(out var reason)) { Debug.LogWarning($"{LogPrefix}: stopped without changes. {reason}"); return; }
            var summary = new StringBuilder($"{LogPrefix}:\n");
            try
            {
                ApplyDebugPanelPrefab(summary);
                ApplyDockPrefab(summary);
                ApplyScene(summary);
                summary.Append(" done");
                Debug.Log(summary.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError($"{summary}{LogPrefix}: failed. {exception}");
            }
        }

        private static bool CanApply(out string reason)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { reason = "Leave Play Mode first."; return false; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { reason = "Wait for compilation to finish."; return false; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) { reason = "Close the Prefab Stage first."; return false; }
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty) { reason = $"Save or revert the open Scene '{scene.path}' first."; return false; }
            }
            reason = string.Empty;
            return true;
        }

        // ── EncounterDebugPanel.prefab ──

        private static void ApplyDebugPanelPrefab(StringBuilder summary)
        {
            var root = PrefabUtility.LoadPrefabContents(DebugPanelPrefabPath);
            try
            {
                var panel = root.GetComponent<EncounterDebugPanel>() ?? throw new InvalidOperationException("EncounterDebugPanel is missing.");
                if (root.GetComponent<DebugPanelTabGroup>() != null && panel.Body != null && panel.Body.transform.Find("Pages/SlotPage") != null)
                {
                    // Added after the first application: page height bounds (the page grows down to the parent's bottom)
                    // and the height reference (the slot page is never shorter than the encounter page).
                    var existingView = panel.Body.transform.Find("Pages/SlotPage").GetComponent<SlotDebugTabView>();
                    var encounter = panel.Body.transform.Find("Pages/EncounterPage") as RectTransform;
                    var updates = new List<string>();
                    if (existingView != null && existingView.BoundsRoot != (RectTransform)root.transform)
                    {
                        existingView.SetPageBoundsForEditor((RectTransform)root.transform, DebugPanelMargin, SlotDebugTabView.DefaultMinimumPageHeight);
                        updates.Add($"height bounds: panel root, bottom margin {DebugPanelMargin}");
                    }
                    if (existingView != null && encounter != null && existingView.HeightReference != encounter)
                    {
                        existingView.SetHeightReferenceForEditor(encounter);
                        updates.Add("height reference: Pages/EncounterPage");
                    }
                    if (updates.Count == 0)
                    {
                        summary.AppendLine($" kept {DebugPanelPrefabPath}");
                        return;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, DebugPanelPrefabPath, out var boundsSaved);
                    if (!boundsSaved) throw new InvalidOperationException($"Could not save {DebugPanelPrefabPath}.");
                    summary.AppendLine($" updated {DebugPanelPrefabPath} (SlotPage {string.Join(", ", updates)})");
                    return;
                }
                if (!panel.IsValid(out var missing)) throw new InvalidOperationException($"EncounterDebugPanel is incomplete: {missing}.");
                var body = (RectTransform)panel.Body.transform;
                var title = root.transform.Find("Header/Title")?.GetComponent<TMP_Text>() ?? throw new InvalidOperationException("Header/Title is missing.");
                title.text = Title;

                var existing = body.Cast<Transform>().ToList();
                var tabBar = NewUi("TabBar", body);
                var bar = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
                bar.spacing = 6f;
                bar.childControlWidth = bar.childControlHeight = true;
                bar.childForceExpandWidth = true;
                bar.childForceExpandHeight = false;
                var encounterTab = NewTab(tabBar, "EncounterTab", EncounterTabLabel, out var encounterLabel, out var encounterIndicator);
                var slotTabButton = NewTab(tabBar, "SlotTab", SlotTabLabel, out var slotLabel, out var slotIndicator);

                var pages = NewUi("Pages", body);
                Column(pages.gameObject, 0, 0f);
                var encounterPage = NewUi("EncounterPage", pages);
                Column(encounterPage.gameObject, 0, 6f);
                foreach (var child in existing) child.SetParent(encounterPage, false);

                var slotView = BuildSlotPage(pages, slotTabButton);
                slotView.SetPageBoundsForEditor((RectTransform)root.transform, DebugPanelMargin, SlotDebugTabView.DefaultMinimumPageHeight);
                slotView.SetHeightReferenceForEditor(encounterPage);
                slotView.gameObject.SetActive(false);

                var group = root.AddComponent<DebugPanelTabGroup>();
                group.ConfigureForEditor(new[]
                {
                    new DebugPanelTab(encounterTab, encounterLabel, encounterIndicator, encounterPage.gameObject, panel.OccurrenceSlider),
                    new DebugPanelTab(slotTabButton, slotLabel, slotIndicator, slotView.gameObject, slotView.MinusButton)
                }, 0);
                // Saved look of the initial selection.
                encounterIndicator.SetActive(true);
                slotIndicator.SetActive(false);
                encounterLabel.fontStyle |= FontStyles.Bold;

                PrefabUtility.SaveAsPrefabAsset(root, DebugPanelPrefabPath, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {DebugPanelPrefabPath}.");
                summary.AppendLine($" updated {DebugPanelPrefabPath} (title, TabBar, Pages/EncounterPage with {existing.Count} moved children, Pages/SlotPage)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static SlotDebugTabView BuildSlotPage(Transform pages, Selectable navigationUp)
        {
            var page = NewUi("SlotPage", pages);
            var pageLayout = page.gameObject.AddComponent<LayoutElement>();
            pageLayout.minHeight = pageLayout.preferredHeight = 200f;
            var scroll = page.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewport = Stretch(NewUi("Viewport", page));
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = true;
            var content = NewUi("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f);
            content.sizeDelta = Vector2.zero;
            Column(content.gameObject, 0, 6f);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            var countRow = NewUi("ReelCountRow", content);
            Row(countRow.gameObject, 48f);
            var countLabel = Text(NewUi("Label", countRow), "Reel 수", 11f, 14f);
            countLabel.alignment = TextAlignmentOptions.MidlineLeft;
            Flexible(countLabel.gameObject, 1f);
            var minus = NewButton(countRow, "MinusButton", "−", 56f);
            var countText = Text(NewUi("CountText", countRow), "3", 12f, 16f);
            Fixed(countText.gameObject, 40f);
            var plus = NewButton(countRow, "PlusButton", "+", 56f);

            var reelRows = NewUi("ReelRows", content);
            Column(reelRows.gameObject, 0, 6f);
            var template = SliderRow(reelRows, "ReelRowTemplate", "Reel 1", out var templateLabel, out var templateSlider, out var templateValue);
            var row = template.gameObject.AddComponent<SlotDebugReelRow>();
            row.ConfigureForEditor(templateLabel, templateSlider, templateValue);
            template.gameObject.SetActive(false);

            var total = SliderRow(content, "TotalChanceRow", "합계 Critical", out _, out var totalSlider, out var totalValue);

            var buttons = NewUi("Buttons", content);
            Column(buttons.gameObject, 0, 6f);
            var firstRow = NewUi("SpinRow", buttons);
            Row(firstRow.gameObject, 48f);
            var spin = NewButton(firstRow, "SpinButton", "회전", -1f);
            var reset = NewButton(firstRow, "ResetButton", "기본값으로", -1f);
            var secondRow = NewUi("MoveRow", buttons);
            Row(secondRow.gameObject, 48f);
            var show = NewButton(secondRow, "ShowButton", "화면 안으로", -1f);
            var hide = NewButton(secondRow, "HideButton", "화면 밖으로", -1f);

            var result = Text(NewUi("ResultText", content), string.Empty, 14f, 14f);
            result.enableAutoSizing = false;
            result.fontSize = 15f;
            result.alignment = TextAlignmentOptions.TopLeft;
            result.textWrappingMode = TextWrappingModes.Normal;
            result.gameObject.AddComponent<LayoutElement>().minHeight = 40f;

            var view = page.gameObject.AddComponent<SlotDebugTabView>();
            view.ConfigureForEditor(minus, countText, plus, reelRows, row, totalSlider, totalValue, spin, reset, show, hide, result,
                pageLayout, content, navigationUp);
            _ = total;
            return view;
        }

        // Root (layout, Button) > HitArea (input) and VisualRoot (effect target) > Background, SelectedIndicator, Label.
        private static Button NewTab(Transform parent, string name, string labelText, out TMP_Text label, out GameObject indicator)
        {
            var rect = NewUi(name, parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 48f;
            layout.minWidth = 96f;
            layout.flexibleWidth = 1f;
            var hit = Stretch(NewUi("HitArea", rect)).gameObject.AddComponent<UiShapeHitArea>();
            hit.Configure(UiShape.RoundedRectangle, 8f);
            hit.raycastTarget = true;
            var visual = Stretch(NewUi("VisualRoot", rect));
            var background = Stretch(NewUi("Background", visual)).gameObject.AddComponent<UiShapeGraphic>();
            background.Configure(UiShape.RoundedRectangle, 8f, UiShapeDrawMode.Fill, 0f);
            background.color = TabColor;
            background.raycastTarget = false;
            var bar = NewUi("SelectedIndicator", visual);
            bar.anchorMin = new Vector2(.1f, 0f);
            bar.anchorMax = new Vector2(.9f, 0f);
            bar.pivot = new Vector2(.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 4f);
            bar.anchoredPosition = new Vector2(0f, 4f);
            var barShape = bar.gameObject.AddComponent<UiShapeGraphic>();
            barShape.Configure(UiShape.RoundedRectangle, 2f, UiShapeDrawMode.Fill, 0f);
            barShape.color = SelectedColor;
            barShape.raycastTarget = false;
            indicator = bar.gameObject;
            var labelRect = Stretch(NewUi("Label", visual));
            labelRect.offsetMin = new Vector2(6f, 6f);
            labelRect.offsetMax = new Vector2(-6f, -2f);
            label = Text(labelRect, labelText, 11f, 16f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            return button;
        }

        // ── SlotReelDock.prefab ──

        private static void ApplyDockPrefab(StringBuilder summary)
        {
            var root = PrefabUtility.LoadPrefabContents(DockPrefabPath);
            try
            {
                var dock = root.GetComponent<SlotReelDock>() ?? throw new InvalidOperationException("SlotReelDock is missing.");
                var transition = root.GetComponent<SlotReelDockEasedTransition>();
                if (transition != null && dock.TransitionBehaviourForEditor == transition)
                {
                    summary.AppendLine($" kept {DockPrefabPath}");
                    return;
                }
                if (transition == null) transition = root.AddComponent<SlotReelDockEasedTransition>();
                var serialized = new SerializedObject(dock);
                serialized.FindProperty("transitionBehaviour").objectReferenceValue = transition;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DockPrefabPath, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {DockPrefabPath}.");
                summary.AppendLine($" updated {DockPrefabPath} (SlotReelDockEasedTransition assigned)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ── Candidate_MainScene ──

        private static void ApplyScene(StringBuilder summary)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var dock = FindInScene<SlotReelDock>(scene) ?? throw new InvalidOperationException("Candidate_MainScene has no SlotReelDock.");
            var panel = FindInScene<EncounterDebugPanel>(scene) ?? throw new InvalidOperationException("Candidate_MainScene has no EncounterDebugPanel.");
            var slotTab = panel.GetComponentInChildren<SlotDebugTabView>(true) ?? throw new InvalidOperationException("The debug panel has no SlotDebugTabView (apply the Prefab first).");
            var changed = false;

            var dockTransform = (RectTransform)dock.transform;
            if (dockTransform.parent != null && dockTransform.parent.name == ClipName)
                summary.AppendLine($" kept {ClipName}");
            else
            {
                var overlay = dockTransform.parent as RectTransform ?? throw new InvalidOperationException("SlotReelDock has no RectTransform parent.");
                if (overlay.Find(ClipName) != null) throw new InvalidOperationException($"{overlay.name}/{ClipName} exists but does not hold the dock.");
                var clip = Stretch(NewUi(ClipName, overlay));
                clip.gameObject.AddComponent<RectMask2D>();
                var panelIndex = panel.transform.parent == overlay ? panel.transform.GetSiblingIndex() : overlay.childCount;
                clip.SetSiblingIndex(Mathf.Min(dockTransform.GetSiblingIndex(), panelIndex));
                dockTransform.SetParent(clip, false);
                dock.RefreshLayout();
                summary.AppendLine($" added {overlay.name}/{ClipName} and moved SlotReelDock into it");
                changed = true;
            }

            var controller = FindInScene<SlotReelDockDebugController>(scene);
            if (controller != null)
                summary.AppendLine($" kept {ControllerName}");
            else
            {
                var canvas = dock.GetComponentInParent<Canvas>()?.rootCanvas ?? throw new InvalidOperationException("SlotReelDock is not under a Canvas.");
                var slotConfiguration = LoadSingle<TemporarySlotMachineConfiguration>(SlotMachineConfigurationPath);
                var fortuneConfiguration = LoadSingle<TemporaryFortuneCriticalConfiguration>(null);
                var gameObject = new GameObject(ControllerName);
                gameObject.transform.SetParent(canvas.transform, false);
                controller = gameObject.AddComponent<SlotReelDockDebugController>();
                controller.ConfigureForEditor(dock, slotTab, slotConfiguration, fortuneConfiguration, SlotReelDockDebugController.DefaultMaximumReelCount);
                summary.AppendLine($" added Canvas/{ControllerName} ({AssetDatabase.GetAssetPath(slotConfiguration)}, {AssetDatabase.GetAssetPath(fortuneConfiguration)})");
                changed = true;
            }

            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Could not save {ScenePath}.");
            summary.AppendLine($" saved {ScenePath}");
        }

        private static T LoadSingle<T>(string preferredPath) where T : ScriptableObject
        {
            if (!string.IsNullOrEmpty(preferredPath))
            {
                var preferred = AssetDatabase.LoadAssetAtPath<T>(preferredPath);
                if (preferred != null) return preferred;
            }
            var paths = AssetDatabase.FindAssets($"t:{typeof(T).Name}").Select(AssetDatabase.GUIDToAssetPath).Where(path => path.StartsWith("Assets/TxTRPG/Application/")).ToArray();
            if (paths.Length != 1) throw new InvalidOperationException($"Expected one {typeof(T).Name} asset, found {paths.Length}: {string.Join(", ", paths)}");
            return AssetDatabase.LoadAssetAtPath<T>(paths[0]);
        }

        private static T FindInScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

        // ── UI helpers (same look as TemporaryEncounterTogglesApply) ──

        private static RectTransform SliderRow(Transform parent, string name, string labelText, out TMP_Text label, out Slider slider, out TMP_Text value)
        {
            var row = NewUi(name, parent);
            Row(row.gameObject, 36f);
            label = Text(NewUi("Label", row), labelText, 11f, 14f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Fixed(label.gameObject, 92f);
            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = "Slider";
            SetLayerRecursively(sliderObject, LayerMask.NameToLayer("UI"));
            sliderObject.transform.SetParent(row, false);
            Flexible(sliderObject, 1f).minWidth = 80f;
            slider = sliderObject.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 100f; slider.wholeNumbers = true; slider.value = 0f;
            foreach (var image in sliderObject.GetComponentsInChildren<Image>(true))
                image.color = image.name == "Background" ? new Color(1f, 1f, 1f, .15f) : image.name == "Fill" ? SelectedColor : Color.white;
            value = Text(NewUi("Value", row), "0%", 11f, 14f);
            value.alignment = TextAlignmentOptions.MidlineRight;
            Fixed(value.gameObject, 52f);
            return row;
        }

        private static Button NewButton(Transform parent, string name, string labelText, float width)
        {
            var rect = NewUi(name, parent);
            var shape = rect.gameObject.AddComponent<UiShapeGraphic>();
            shape.Configure(UiShape.RoundedRectangle, 8f, UiShapeDrawMode.Fill, 0f);
            shape.color = ControlColor;
            shape.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = shape;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 48f; layout.preferredHeight = 48f;
            if (width > 0f) { layout.minWidth = width; layout.preferredWidth = width; layout.flexibleWidth = 0f; }
            else { layout.minWidth = 96f; layout.flexibleWidth = 1f; }
            var labelRect = Stretch(NewUi("Label", rect));
            labelRect.offsetMin = new Vector2(6f, 2f); labelRect.offsetMax = new Vector2(-6f, -2f);
            var label = Text(labelRect, labelText, 11f, 16f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return button;
        }

        private static void Column(GameObject target, int padding, float spacing)
        {
            var column = target.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(padding, padding, padding, padding);
            column.spacing = spacing;
            column.childControlWidth = true; column.childControlHeight = true;
            column.childForceExpandWidth = true; column.childForceExpandHeight = false;
        }

        private static void Row(GameObject target, float height)
        {
            var row = target.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 6f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = true;
            var layout = target.AddComponent<LayoutElement>();
            layout.minHeight = height; layout.preferredHeight = height;
        }

        private static LayoutElement Flexible(GameObject target, float flexible)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.flexibleWidth = flexible;
            return layout;
        }

        private static void Fixed(GameObject target, float width)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minWidth = width; layout.preferredWidth = width; layout.flexibleWidth = 0f;
        }

        private static RectTransform NewUi(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var rect = (RectTransform)gameObject.transform;
            if (parent != null) rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TextMeshProUGUI Text(RectTransform rect, string value, float minimumSize, float maximumSize)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = minimumSize;
            text.fontSizeMax = maximumSize;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
