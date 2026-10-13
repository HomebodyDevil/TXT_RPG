using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using TxTRPG.Application.Exploration;
using TxTRPG.Application.Exploration.Events;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.UI.Exploration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (exploration-event-nodes). Partial edits only, safe to rerun:
    // 1. creates the event catalog and the five test events when missing (existing assets are never overwritten),
    // 2. adds the 'event' node weight to ExplorationRunConfiguration.asset when it is missing, writing the confirmed
    //    40/30/30 weights at that moment (the previous values are logged) and links the catalog when it is empty,
    // 3. adds the 'event' entry to ExplorationTreeDefaultStyle.asset when missing,
    // 4. adds ExplorationNodePanel/EventView and ExplorationEventController to TMP_MainScene when missing and fills only
    //    empty references.
    // Nothing is written when everything already matches. It stops when an open Scene has unsaved changes. These are
    // developer-owned assets, so the menu is not registered in the Prefab rebuild batch. Retire it once every working
    // copy has been migrated.
    public static class TemporaryExplorationEventNodesApply
    {
        public const string ApplyMenuPath = "Tools/TxT RPG/Application/Temporary/Apply Exploration Event Nodes";
        public const string ValidateMenuPath = "Tools/TxT RPG/Application/Temporary/Validate Exploration Event Nodes";
        public const string ScenePath = TemporarySlotMachineIntegration.ScenePath;
        public const string ConfigurationPath = "Assets/TxTRPG/Application/Configuration/ExplorationRunConfiguration.asset";
        public const string EventsFolder = "Assets/TxTRPG/Application/Configuration/Events";
        public const string TestEventsFolder = EventsFolder + "/Test";
        public const string CatalogPath = EventsFolder + "/ExplorationEventCatalog.asset";
        public const string StylePath = "Assets/TxTRPG/UI/Styles/ExplorationTreeDefaultStyle.asset";
        public const string EventViewName = "EventView";

        public static readonly (string Id, string File, int Weight)[] TestEvents =
        {
            ("event.test.signpost", "SignpostEvent", 3),
            ("event.test.shrine", "ShrineEvent", 3),
            ("event.test.slot-trial", "SlotTrialEvent", 3),
            ("event.test.workshop", "WorkshopEvent", 3),
            ("event.test.trap", "TrapEvent", 1)
        };

        [MenuItem(ApplyMenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying exploration event nodes.");
            if (EditorApplication.isCompiling) throw new InvalidOperationException("Wait for script compilation to finish.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Save and close Prefab Stage first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or revert the unsaved open Scenes before applying exploration event nodes.");
            var summary = new StringBuilder("EXPLORATION_EVENT_NODES_APPLY:\n");
            var catalog = EnsureCatalogAndEvents(summary);
            EnsureConfiguration(catalog, summary);
            EnsureStyle(summary);
            AssetDatabase.SaveAssets();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                ApplyScene(scene, summary);
                if (scene.isDirty)
                {
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save TMP_MainScene.");
                    summary.AppendLine(" TMP_MainScene saved.");
                }
                else summary.AppendLine(" TMP_MainScene already matches; not saved.");
                Validate(scene);
                summary.AppendLine(" validation: passed. Safe to rerun.");
                Debug.Log(summary.ToString());
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [MenuItem(ValidateMenuPath)]
        public static void ValidateMenu()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Validate(scene);
                Debug.Log("EXPLORATION_EVENT_NODES_VALIDATE: catalog, test events, configuration, style and TMP_MainScene wiring are valid.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        // ---------- Assets ----------

        private static ExplorationEventCatalog EnsureCatalogAndEvents(StringBuilder summary)
        {
            EnsureFolder(EventsFolder);
            EnsureFolder(TestEventsFolder);
            var catalog = AssetDatabase.LoadAssetAtPath<ExplorationEventCatalog>(CatalogPath);
            var catalogCreated = catalog == null;
            if (catalogCreated)
            {
                catalog = ScriptableObject.CreateInstance<ExplorationEventCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                summary.AppendLine($" created {CatalogPath}");
            }
            else summary.AppendLine($" kept {CatalogPath}");
            foreach (var (id, file, weight) in TestEvents)
            {
                var path = $"{TestEventsFolder}/{file}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<ExplorationEventDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<ExplorationEventDefinition>();
                    ExplorationTestEventContent.Configure(definition, id);
                    AssetDatabase.CreateAsset(definition, path);
                    summary.AppendLine($" created {path}");
                }
                else summary.AppendLine($" kept {path}");
                // Only a newly created catalog is filled; an existing catalog is the developer's list.
                if (catalogCreated && !catalog.ContainsForEditor(definition)) catalog.AddForEditor(definition, weight);
            }
            if (catalogCreated) EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void EnsureConfiguration(ExplorationEventCatalog catalog, StringBuilder summary)
        {
            var configuration = AssetDatabase.LoadAssetAtPath<ExplorationRunConfiguration>(ConfigurationPath)
                ?? throw new InvalidOperationException($"{ConfigurationPath} was not found.");
            var changed = false;
            if (!configuration.NodeWeights.Any(entry => entry != null && entry.TypeId == ExplorationNodeTypeIds.Event))
            {
                var previous = string.Join(", ", configuration.NodeWeights.Where(entry => entry != null).Select(entry => $"{entry.TypeId} {entry.Weight}"));
                configuration.ConfigureForEditor(configuration.ChoiceCount, ExplorationRunConfiguration.CreateDefaultWeights());
                summary.AppendLine($" node weights: {previous} -> combat {ExplorationRunConfiguration.DefaultCombatWeight}, " +
                                   $"recovery-upgrade {ExplorationRunConfiguration.DefaultRecoveryUpgradeWeight}, event {ExplorationRunConfiguration.DefaultEventWeight}");
                changed = true;
            }
            else summary.AppendLine(" node weights: 'event' already listed; kept.");
            if (configuration.EventCatalog == null)
            {
                configuration.ConfigureEventCatalogForEditor(catalog);
                summary.AppendLine(" linked the event catalog to ExplorationRunConfiguration.");
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(configuration);
            else summary.AppendLine($" {ConfigurationPath} already matches; not saved.");
        }

        private static void EnsureStyle(StringBuilder summary)
        {
            var style = AssetDatabase.LoadAssetAtPath<ExplorationTreeStyle>(StylePath) ?? throw new InvalidOperationException($"{StylePath} was not found.");
            if (style.types.Any(type => type != null && type.typeId == ExplorationNodeTypeIds.Event))
            {
                summary.AppendLine($" {StylePath} already has 'event'; not saved.");
                return;
            }
            // No dedicated sprite yet: the tree uses fallbackIcon until one is assigned.
            style.types = style.types.Append(new ExplorationTreeStyle.TypeImage { typeId = ExplorationNodeTypeIds.Event, displayName = "이벤트" }).ToArray();
            EditorUtility.SetDirty(style);
            summary.AppendLine($" added 'event' to {StylePath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        // ---------- Scene ----------

        private static void ApplyScene(Scene scene, StringBuilder summary)
        {
            var run = FindSingle<ExplorationRunController>(scene);
            var panel = run.PanelForEditor ?? throw new InvalidOperationException("ExplorationRunController has no panel.");
            var view = panel.transform.Find(EventViewName)?.GetComponent<ExplorationEventView>();
            if (view == null)
            {
                view = ExplorationEventViewBuilder.Build(panel.transform, EventViewName);
                summary.AppendLine($" added {panel.name}/{EventViewName}");
            }
            var cardList = panel.GetComponentInChildren<ExplorationNodeChoiceCardList>(true);
            if (cardList != null && cardList.WindowService != null && view.WindowService == null)
            {
                view.ConfigureWindowServiceForEditor(cardList.WindowService);
                EditorUtility.SetDirty(view);
                summary.AppendLine(" linked the game window service to EventView.");
            }

            var controller = run.GetComponent<ExplorationEventController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<ExplorationEventController>(run.gameObject);
                summary.AppendLine($" added ExplorationEventController to {run.gameObject.name}");
            }
            var slot = run.CombatControllerForEditor as TemporarySlotCombatController;
            var serialized = new SerializedObject(controller);
            var filled = FillIfEmpty(serialized, "view", view) | FillIfEmpty(serialized, "storyPanel", run.StoryForEditor) |
                         FillIfEmpty(serialized, "slotPanel", slot != null ? slot.Panel : null) | FillIfEmpty(serialized, "nodePanel", panel);
            if (filled) { serialized.ApplyModifiedPropertiesWithoutUndo(); summary.AppendLine(" filled empty ExplorationEventController references."); }
            if (run.EventControllerForEditor == null)
            {
                run.ConfigureEventsForEditor(controller);
                EditorUtility.SetDirty(run);
                summary.AppendLine(" linked ExplorationEventController to ExplorationRunController.");
            }
            if (scene.isDirty) EditorSceneManager.MarkSceneDirty(scene);
        }

        private static bool FillIfEmpty(SerializedObject serialized, string field, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"Field '{field}' was not found.");
            if (property.objectReferenceValue != null || value == null) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            var found = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1) throw new InvalidOperationException($"TMP_MainScene must contain exactly one {typeof(T).Name}; found {found.Length}.");
            return found[0];
        }

        // ---------- Validation ----------

        public static void Validate(Scene scene)
        {
            var errors = new List<string>();
            var catalog = AssetDatabase.LoadAssetAtPath<ExplorationEventCatalog>(CatalogPath);
            if (catalog == null) errors.Add($"{CatalogPath} is missing.");
            else
            {
                var skipped = new List<string>();
                if (!catalog.TryBuild(out var runtime, out var error, skipped)) errors.Add($"Catalog: {error}");
                else
                {
                    errors.AddRange(skipped.Select(item => $"Event left out: {item}"));
                    if (!runtime.CanPick) errors.Add("The catalog has no event with a positive weight.");
                    foreach (var (id, _, _) in TestEvents)
                        if (!runtime.TryGet(id, out _)) errors.Add($"Test event '{id}' is not in the catalog runtime list.");
                }
            }
            var configuration = AssetDatabase.LoadAssetAtPath<ExplorationRunConfiguration>(ConfigurationPath);
            if (configuration == null) errors.Add($"{ConfigurationPath} is missing.");
            else
            {
                if (!configuration.Validate(out var reason)) errors.Add($"Configuration: {reason}");
                if (configuration.EventWeight <= 0) errors.Add("The 'event' node weight is missing or 0.");
                if (configuration.EventCatalog == null) errors.Add("ExplorationRunConfiguration has no event catalog.");
            }
            var style = AssetDatabase.LoadAssetAtPath<ExplorationTreeStyle>(StylePath);
            if (style == null || !style.types.Any(type => type != null && type.typeId == ExplorationNodeTypeIds.Event)) errors.Add("The tree style has no 'event' entry.");

            var run = FindSingle<ExplorationRunController>(scene);
            var controller = run.EventControllerForEditor as ExplorationEventController;
            if (controller == null) errors.Add("ExplorationRunController has no ExplorationEventController.");
            else
            {
                var serialized = new SerializedObject(controller);
                foreach (var field in new[] { "view", "storyPanel", "slotPanel", "nodePanel" })
                    if (serialized.FindProperty(field).objectReferenceValue == null) errors.Add($"ExplorationEventController.{field} is empty.");
                if (controller.View != null && controller.View.transform.parent != run.PanelForEditor?.transform) errors.Add("EventView is not a child of ExplorationNodePanel.");
                if (controller.View != null)
                {
                    var view = controller.View;
                    if (view.gameObject.activeSelf) errors.Add("EventView must be inactive in the Scene.");
                    if (view.ChoiceTemplateForEditor == null) errors.Add("EventView has no choice template.");
                    if (view.WindowService == null) errors.Add("EventView has no game window service.");
                    foreach (var graphic in view.GetComponentsInChildren<Graphic>(true))
                        if (graphic.raycastTarget && graphic.GetComponent<Button>() == null && graphic.GetComponent<ScrollRect>() == null && graphic.GetComponent<Mask>() == null && !graphic.name.EndsWith("Viewport", StringComparison.Ordinal))
                            errors.Add($"Decorative graphic '{graphic.name}' blocks raycasts.");
                }
            }
            if (errors.Count > 0) throw new InvalidOperationException("Exploration event nodes are not applied correctly:\n - " + string.Join("\n - ", errors));
        }
    }

    // Builds ExplorationNodePanel/EventView (TMP_MainScene). Anchors and layout groups only; the region matches the card
    // list below the status line.
    public static class ExplorationEventViewBuilder
    {
        private static readonly Color BackgroundColor = new(.105f, .14f, .21f, .98f);
        private static readonly Color ChoiceColor = new(.18f, .25f, .36f, 1f);
        private static readonly Color TitleColor = new(.953f, .965f, .98f, 1f);
        private static readonly Color BodyColor = new(.827f, .863f, .91f, 1f);
        private static readonly Color ReasonColor = new(1f, .82f, .55f, 1f);

        public static ExplorationEventView Build(Transform panel, string name)
        {
            var font = panel.GetComponentsInChildren<TMP_Text>(true).Select(text => text.font).FirstOrDefault(item => item != null);
            var root = CreateRect(name, panel);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(24f, 16f); root.offsetMax = new Vector2(-24f, -82f);

            var visual = Stretch(CreateRect("VisualRoot", root));
            var layout = visual.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14); layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            var background = Stretch(CreateRect("Background", visual));
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            AddImage(background, BackgroundColor, false);

            var title = CreateText("Title", visual, font, 26f, FontStyles.Bold, TitleColor);
            title.gameObject.AddComponent<LayoutElement>().minHeight = 34f;

            var bodyScroll = CreateScroll("BodyScroll", visual, out var bodyContent);
            var bodyElement = bodyScroll.gameObject.AddComponent<LayoutElement>();
            bodyElement.minHeight = 64f; bodyElement.flexibleHeight = 1f;
            var bodyLayout = bodyContent.gameObject.AddComponent<VerticalLayoutGroup>();
            bodyLayout.childControlWidth = bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true; bodyLayout.childForceExpandHeight = false;
            var body = CreateText("Body", bodyContent, font, 20f, FontStyles.Normal, BodyColor);

            var choicesScroll = CreateScroll("ChoicesScroll", visual, out var choicesContent);
            var choicesElement = choicesScroll.gameObject.AddComponent<LayoutElement>();
            choicesElement.minHeight = 56f; choicesElement.preferredHeight = 56f; choicesElement.flexibleHeight = 0f;
            var choicesLayout = choicesContent.gameObject.AddComponent<VerticalLayoutGroup>();
            choicesLayout.spacing = 8f; choicesLayout.childControlWidth = choicesLayout.childControlHeight = true;
            choicesLayout.childForceExpandWidth = true; choicesLayout.childForceExpandHeight = false;
            var template = BuildChoice(choicesContent, font);

            var effect = Stretch(CreateRect("EffectRoot", visual));
            effect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var view = root.gameObject.AddComponent<ExplorationEventView>();
            view.ConfigureForEditor(visual, title, bodyScroll, body, choicesScroll, choicesElement, choicesContent, template);
            root.gameObject.SetActive(false);
            return view;
        }

        private static ExplorationEventChoiceView BuildChoice(RectTransform parent, TMP_FontAsset font)
        {
            var root = CreateRect("ChoiceTemplate", parent);
            // Hit area of the button; invisible but not decorative.
            AddImage(root, new Color(1f, 1f, 1f, 0f), true);
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10); layout.spacing = 2f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            root.gameObject.AddComponent<LayoutElement>().minHeight = 52f;
            var visual = Stretch(CreateRect("VisualRoot", root));
            visual.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var background = AddImage(visual, ChoiceColor, false);
            var label = CreateText("Label", root, font, 20f, FontStyles.Bold, TitleColor);
            var reason = CreateText("Reason", root, font, 16f, FontStyles.Normal, ReasonColor);
            reason.gameObject.SetActive(false);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.selectedColor = new Color(1.3f, 1.2f, .9f, 1f);
            colors.disabledColor = new Color(.55f, .55f, .55f, .9f);
            colors.colorMultiplier = 1.5f;
            button.colors = colors;
            var view = root.gameObject.AddComponent<ExplorationEventChoiceView>();
            view.ConfigureForEditor(button, visual, label, reason);
            root.gameObject.SetActive(false);
            return view;
        }

        private static ScrollRect CreateScroll(string name, RectTransform parent, out RectTransform content)
        {
            var root = CreateRect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            var viewport = Stretch(CreateRect("Viewport", root));
            viewport.gameObject.AddComponent<RectMask2D>();
            // Receives drag and wheel input for scrolling; invisible.
            AddImage(viewport, new Color(1f, 1f, 1f, 0f), true);
            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            return scroll;
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, TMP_FontAsset font, float size, FontStyles style, Color color)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size; text.fontStyle = style; text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static Image AddImage(RectTransform rect, Color color, bool raycast)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = raycast;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }
    }
}
