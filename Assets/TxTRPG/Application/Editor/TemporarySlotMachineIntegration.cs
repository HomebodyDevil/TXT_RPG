using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using TxTRPG.Application.Combat;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Exploration;
using TxTRPG.Application.Players;
using TxTRPG.Application.Runes;
using TxTRPG.Application.SlotMachine;
using TxTRPG.UI;
using TxTRPG.UI.Dice;
using TxTRPG.UI.Exploration;
using TxTRPG.UI.SlotMachine;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (slot-machine-and-rune-combat). Creates the missing slot machine Prefabs and
    // configuration assets, fills the PlayerSessionHost references in AppRoot.prefab, and wires TMP_MainScene: adds
    // SlotMachinePanel next to OwnedDicePanel, disables the dice participants, and points the action button, boundary,
    // covered-panel blocker and exploration at the slot machine. Safe to rerun: existing Prefabs, assets, layout values
    // and Scene objects are reused and never overwritten; only missing or dice-pointing references are changed.
    // Excluded from the registered Prefab rebuild batch because it also changes a Scene and AppRoot. Retire it once every
    // working copy has been migrated.
    public static class TemporarySlotMachineIntegration
    {
        public const string ApplyMenuPath = "Tools/TxT RPG/Application/Temporary/Apply Slot Machine Combat";
        public const string ValidateMenuPath = "Tools/TxT RPG/Application/Temporary/Validate Slot Machine Combat";
        public const string ScenePath = "Assets/Scenes/TMP_MainScene.unity";
        public const string AppRootPath = "Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab";
        public const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs/SlotMachine";
        public const string ReelPrefabPath = PrefabFolder + "/SlotReel.prefab";
        public const string PanelPrefabPath = PrefabFolder + "/SlotMachinePanel.prefab";
        public const string ConfigurationFolder = "Assets/TxTRPG/Application/Configuration";
        public const string SlotConfigurationPath = ConfigurationFolder + "/TemporarySlotMachineConfiguration.asset";
        public const string RuneConfigurationPath = ConfigurationFolder + "/TemporaryRuneConfiguration.asset";
        public const string PanelName = "SlotMachinePanel";
        private const float ContentTopInset = 44f;
        private static readonly Color PanelColor = new(.05f, .09f, .14f, .96f);

        [MenuItem(ApplyMenuPath)]
        public static void Apply()
        {
            CheckEditor();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save or revert unsaved TMP_MainScene changes before applying the slot machine combat.");
            var summary = new StringBuilder("SLOT_MACHINE_APPLY:\n");
            var slotConfiguration = EnsureAsset<TemporarySlotMachineConfiguration>(SlotConfigurationPath, summary);
            var runeConfiguration = EnsureAsset<TemporaryRuneConfiguration>(RuneConfigurationPath, summary);
            var panelPrefab = EnsurePrefabs(summary);
            EnsureAppRoot(slotConfiguration, runeConfiguration, summary);
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                ApplyScene(scene, panelPrefab, summary);
                ValidateScene(scene);
                summary.AppendLine(" validation: passed.");
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save TMP_MainScene.");
                summary.AppendLine(" TMP_MainScene saved. Safe to rerun.");
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
                ValidateAssets();
                ValidateScene(scene);
                Debug.Log("SLOT_MACHINE_VALIDATE: Prefabs, configuration assets, AppRoot references and TMP_MainScene wiring are valid.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        // True when TMP_MainScene already uses the slot machine. The reserved dice Apply menus stop in that case,
        // because rerunning them would point the action button and the exploration back to the dice.
        public static bool IsAppliedTo(Scene scene) =>
            scene.IsValid() && scene.isLoaded && scene.GetRootGameObjects()
                .Any(root => root.GetComponentInChildren<TemporarySlotCombatController>(true) != null);

        public static void ThrowIfApplied(Scene scene, string menuName)
        {
            if (IsAppliedTo(scene))
                throw new InvalidOperationException($"{menuName} is a reserved dice tool. TMP_MainScene uses the slot machine combat " +
                    "(TemporarySlotCombatController is present); running it would reconnect the dice. See DOCS/development/workflows.md " +
                    "(슬롯머신 전투 적용) for the references to change when the dice system is restored.");
        }

        // Checks the loaded TMP_MainScene, or its saved file when it is not loaded (without opening it).
        public static void ThrowIfAppliedToMainScene(string menuName)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded) { ThrowIfApplied(scene, menuName); return; }
            var script = AssetDatabase.FindAssets($"{nameof(TemporarySlotCombatController)} t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => System.IO.Path.GetFileNameWithoutExtension(path) == nameof(TemporarySlotCombatController));
            if (script == null || !System.IO.File.Exists(ScenePath)) return;
            var guid = AssetDatabase.AssetPathToGUID(script);
            if (System.IO.File.ReadAllText(ScenePath).Contains($"guid: {guid}"))
                throw new InvalidOperationException($"{menuName} is a reserved dice tool and TMP_MainScene uses the slot machine combat; " +
                    "running it would reconnect the dice. See DOCS/development/workflows.md (슬롯머신 전투 적용).");
        }

        private static void CheckEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying the slot machine combat.");
            if (EditorApplication.isCompiling) throw new InvalidOperationException("Wait for script compilation to finish.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Save and close Prefab Stage first.");
        }

        private static T EnsureAsset<T>(string path, StringBuilder summary) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { summary.AppendLine($" kept {path}"); return asset; }
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            summary.AppendLine($" created {path} with the default values");
            return asset;
        }

        private static void EnsureAppRoot(TemporarySlotMachineConfiguration slots, TemporaryRuneConfiguration runes, StringBuilder summary)
        {
            var root = PrefabUtility.LoadPrefabContents(AppRootPath);
            try
            {
                var host = root.GetComponentInChildren<PlayerSessionHost>(true) ?? throw new InvalidOperationException("AppRoot.prefab has no PlayerSessionHost.");
                var serialized = new SerializedObject(host);
                var changed = FillIfEmpty(serialized, "temporarySlotMachineConfiguration", slots) |
                    FillIfEmpty(serialized, "temporaryRuneConfiguration", runes);
                if (!changed) { summary.AppendLine(" kept AppRoot PlayerSessionHost references"); return; }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, AppRootPath);
                summary.AppendLine(" filled AppRoot PlayerSessionHost slot machine and rune configuration references");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static bool FillIfEmpty(SerializedObject serialized, string property, UnityEngine.Object value)
        {
            var field = serialized.FindProperty(property) ?? throw new InvalidOperationException($"Field '{property}' is missing.");
            if (field.objectReferenceValue != null) return false;
            field.objectReferenceValue = value;
            return true;
        }

        // ---------- Prefabs (created only when missing; afterwards developer-owned) ----------

        private static GameObject EnsurePrefabs(StringBuilder summary)
        {
            EnsureFolder(PrefabFolder);
            var reel = AssetDatabase.LoadAssetAtPath<GameObject>(ReelPrefabPath);
            if (reel == null)
            {
                reel = CreateReelPrefab();
                summary.AppendLine($" created {ReelPrefabPath}");
            }
            else summary.AppendLine($" kept {ReelPrefabPath}");
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            var created = false;
            var reelCreated = reel.transform.Find("Window") == null;
            if (panel == null)
            {
                panel = CreatePanelPrefab();
                summary.AppendLine($" created {PanelPrefabPath}");
                created = true;
            }
            else summary.AppendLine($" kept {PanelPrefabPath}");
            // New skeletons get the reel machine and summary hierarchy from the panel visual apply (same core as its menu).
            if (created || reelCreated) TemporarySlotMachinePanelVisualApply.ApplyAssetsAndPrefabs(summary);
            foreach (var path in new[] { ReelPrefabPath, PanelPrefabPath })
                if (SyncPrefabTextState(path)) summary.AppendLine($" synchronized the TMP color cache and text style of {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
        }

        // Skeletons only: TemporarySlotMachinePanelVisualApply adds the frame, window, cells and summary hierarchy.
        private static GameObject CreateReelPrefab()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("SlotReel", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, preview);
                ((RectTransform)root.transform).sizeDelta = new Vector2(96f, 180f);
                var element = root.AddComponent<LayoutElement>();
                element.minWidth = 40f; element.flexibleWidth = 1f; element.flexibleHeight = 1f;
                root.AddComponent<SlotReelView>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, ReelPrefabPath);
                return prefab ?? throw new InvalidOperationException("Could not save SlotReel.prefab.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static GameObject CreatePanelPrefab()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(PanelName, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, preview);
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(360f, 280f);
                root.AddComponent<CanvasGroup>();
                root.AddComponent<FlexibleLayoutItem>().Configure(FlexibleLayoutSizeMode.Weighted, .7f, 200f, 200f, 0f);
                var background = Child("Background", rect); Stretch(background);
                Graphic(background, PanelColor);
                var content = Child("Content", rect); Stretch(content);
                var status = Child("StatusText", rect); Stretch(status, 16f, 16f, 16f, 16f);
                Text(status, string.Empty, 16f, TextAlignmentOptions.Center);
                status.gameObject.SetActive(false);
                var effect = Child("EffectRoot", rect); Stretch(effect);
                root.AddComponent<SlotMachinePanel>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
                return prefab ?? throw new InvalidOperationException("Could not save SlotMachinePanel.prefab.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        // TMP keeps a Color32 copy of the font color and the default style hash; text created in a preview scene saves
        // them unsynchronized, and every Scene instance then records them as overrides. Only those two cached values are
        // written, from the text's own color and style, so developer-edited colors are kept.
        private static bool SyncPrefabTextState(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var changed = false;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    changed |= SyncTextState(text);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static bool SyncTextState(TMP_Text text)
        {
            var serialized = new SerializedObject(text);
            var color = serialized.FindProperty("m_fontColor");
            var color32 = serialized.FindProperty("m_fontColor32");
            var style = serialized.FindProperty("m_TextStyleHashCode");
            var changed = false;
            if (color != null && color32 != null && !SameColor32(color32.colorValue, color.colorValue))
            { color32.colorValue = (Color32)color.colorValue; changed = true; }
            var hash = text.textStyle != null ? text.textStyle.hashCode : TMP_Style.NormalStyle.hashCode;
            if (style != null && style.intValue == 0 && hash != 0) { style.intValue = hash; changed = true; }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool SameColor32(Color left, Color right)
        {
            Color32 a = left, b = right;
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        // Removes Scene overrides of the two TMP cache values when they equal the (synchronized) Prefab values.
        private static int RevertRedundantTextOverrides(SlotMachinePanel panel)
        {
            var reverted = 0;
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(text)) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(text);
                if (source == null) continue;
                var instance = new SerializedObject(text);
                var prefab = new SerializedObject(source);
                foreach (var name in new[] { "m_fontColor32", "m_TextStyleHashCode" })
                {
                    var property = instance.FindProperty(name);
                    if (property == null || !property.prefabOverride) continue;
                    var original = prefab.FindProperty(name);
                    var same = name == "m_TextStyleHashCode"
                        ? original.intValue == property.intValue
                        : SameColor32(original.colorValue, property.colorValue);
                    if (!same) continue;
                    PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                    reverted++;
                }
            }
            return reverted;
        }

        // ---------- Scene ----------

        private static void ApplyScene(Scene scene, GameObject panelPrefab, StringBuilder summary)
        {
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            var dicePanel = components.OfType<OwnedDicePanel>().SingleOrDefault()
                ?? throw new InvalidOperationException("TMP_MainScene has no OwnedDicePanel; the slot machine takes its place.");
            var diceRect = (RectTransform)dicePanel.transform;
            var contentLayer = diceRect.parent as RectTransform ?? throw new InvalidOperationException("OwnedDicePanel has no layout parent.");
            var diceController = components.OfType<TemporaryDiceRollMenuController>().Single();
            var diceBinder = components.OfType<OwnedDiceSessionBinder>().SingleOrDefault();
            var exploration = components.OfType<ExplorationRunController>().Single();
            var boundary = components.OfType<BoundaryCommandButton>().Single(button => button.Placement != null);
            var blocker = components.OfType<CoveredPanelInteractionBlocker>().SingleOrDefault();

            // 1. Panel: reuse any existing SlotMachinePanel, wherever it was moved.
            var panel = components.OfType<SlotMachinePanel>().SingleOrDefault();
            if (panel == null)
            {
                var expected = MeasureRect(diceRect);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add slot machine panel");
                instance.name = PanelName;
                instance.transform.SetParent(contentLayer, false);
                instance.transform.SetSiblingIndex(diceRect.GetSiblingIndex() + 1);
                var rect = (RectTransform)instance.transform;
                rect.anchorMin = diceRect.anchorMin; rect.anchorMax = diceRect.anchorMax; rect.pivot = diceRect.pivot;
                rect.sizeDelta = diceRect.sizeDelta; rect.anchoredPosition = diceRect.anchoredPosition;
                var source = dicePanel.GetComponent<FlexibleLayoutItem>();
                var item = instance.GetComponent<FlexibleLayoutItem>() ?? instance.AddComponent<FlexibleLayoutItem>();
                if (source != null) item.Configure(source.SizeMode, source.Weight, source.FixedSize, source.MinimumSize, source.MaximumSize);
                MarkChanged(item);
                MarkChanged(rect);
                panel = instance.GetComponent<SlotMachinePanel>();
                summary.AppendLine($" added SlotMachinePanel after OwnedDicePanel (layout copied: {(source != null ? $"{source.SizeMode} weight {source.Weight} min {source.MinimumSize}" : "none")}, dice panel was {expected.size})");
                // Deactivate the dice panel in the same step, so the measured size can be compared below.
                SetActive(dicePanel.gameObject, false, summary, "OwnedDicePanel");
                var measured = MeasureRect(rect);
                summary.AppendLine($" SlotMachinePanel measures {measured.size} at {measured.position} (OwnedDicePanel was {expected.size} at {expected.position}) in the current Game view");
            }
            else summary.AppendLine(" kept existing SlotMachinePanel and its layout values");
            var revertedTexts = RevertRedundantTextOverrides(panel);
            if (revertedTexts > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                summary.AppendLine($" removed {revertedTexts} redundant TMP color cache/style overrides from the SlotMachinePanel instance");
            }
            var panelRect = (RectTransform)panel.transform;
            var layoutGroup = contentLayer.GetComponent<FlexibleContentLayoutGroup>();
            if (layoutGroup != null && layoutGroup.IncludeInactiveChildren)
                throw new InvalidOperationException("The ContentLayer layout includes inactive children; the inactive OwnedDicePanel would keep its space.");

            // 2. Dice participants: components and panel off, never removed (settings are preserved).
            SetActive(dicePanel.gameObject, false, summary, "OwnedDicePanel");
            SetEnabled(diceController, false, summary);
            if (diceBinder != null) SetEnabled(diceBinder, false, summary);
            var stageCamera = new SerializedObject(dicePanel).FindProperty("stageCamera")?.objectReferenceValue as Camera;
            if (stageCamera != null) SetEnabled(stageCamera, false, summary);

            // 3. Controller next to the dice controller (the GameMenuPanel object stays active).
            var controller = diceController.GetComponent<TemporarySlotCombatController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<TemporarySlotCombatController>(diceController.gameObject);
                summary.AppendLine(" added TemporarySlotCombatController next to the reserved dice controller");
            }
            var dice = new SerializedObject(diceController);
            var slot = new SerializedObject(controller);
            var copied = 0;
            foreach (var name in new[] { "menu", "storyPanel", "combatConfiguration", "enemyPanel", "playerHealthBar", "enemyHealthBar", "nextEnemyActionText" })
            {
                var target = slot.FindProperty(name);
                if (target.objectReferenceValue != null) continue;
                target.objectReferenceValue = dice.FindProperty(name).objectReferenceValue;
                copied++;
            }
            copied += FillIfEmpty(slot, "actionButton", boundary) ? 1 : 0;
            copied += FillIfEmpty(slot, "slotPanel", panel) ? 1 : 0;
            if (copied > 0)
            {
                slot.ApplyModifiedProperties();
                MarkChanged(controller);
                summary.AppendLine($" filled {copied} empty TemporarySlotCombatController references");
            }

            // 4. Boundary button: handler, command and second panel move from the dice to the slot machine.
            var button = new SerializedObject(boundary);
            var handler = button.FindProperty("commandHandlerBehaviour");
            var command = button.FindProperty("commandId");
            if (handler.objectReferenceValue == null || handler.objectReferenceValue == diceController ||
                command.stringValue != TemporarySlotCombatController.SpinCommandId)
            {
                handler.objectReferenceValue = controller;
                command.stringValue = TemporarySlotCombatController.SpinCommandId;
                button.ApplyModifiedProperties();
                MarkChanged(boundary);
                summary.AppendLine($" {boundary.name}: command '{TemporarySlotCombatController.SpinCommandId}' handled by TemporarySlotCombatController");
            }
            var placement = new SerializedObject(boundary.Placement);
            var second = placement.FindProperty("secondPanel");
            if (second.objectReferenceValue == null || second.objectReferenceValue == diceRect)
            {
                second.objectReferenceValue = panelRect;
                placement.ApplyModifiedProperties();
                MarkChanged(boundary.Placement);
                summary.AppendLine($" {boundary.name}: boundary second panel is SlotMachinePanel");
            }

            // 5. Covered panels: the dice panel's CanvasGroup is replaced by the slot machine panel's.
            if (blocker != null)
            {
                var serialized = new SerializedObject(blocker);
                var covered = serialized.FindProperty("coveredPanels");
                var diceGroup = dicePanel.GetComponent<CanvasGroup>();
                var slotGroup = panel.GetComponent<CanvasGroup>() ?? throw new InvalidOperationException("SlotMachinePanel has no CanvasGroup.");
                var hasSlot = false;
                var replaced = false;
                for (var i = 0; i < covered.arraySize; i++)
                {
                    var element = covered.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue == slotGroup) hasSlot = true;
                    else if (diceGroup != null && element.objectReferenceValue == diceGroup && !hasSlot)
                    { element.objectReferenceValue = slotGroup; hasSlot = replaced = true; }
                }
                if (!hasSlot)
                {
                    covered.InsertArrayElementAtIndex(covered.arraySize);
                    covered.GetArrayElementAtIndex(covered.arraySize - 1).objectReferenceValue = slotGroup;
                    replaced = true;
                }
                // A remaining dice entry is removed (it may follow the slot entry after earlier edits).
                for (var i = covered.arraySize - 1; i >= 0; i--)
                    if (diceGroup != null && covered.GetArrayElementAtIndex(i).objectReferenceValue == diceGroup)
                    { covered.DeleteArrayElementAtIndex(i); replaced = true; }
                if (replaced)
                {
                    serialized.ApplyModifiedProperties();
                    MarkChanged(blocker);
                    summary.AppendLine($" {blocker.name}: covered panels use the SlotMachinePanel CanvasGroup instead of OwnedDicePanel");
                }
            }

            // 6. Exploration drives the slot machine controller.
            if (exploration.CombatControllerForEditor != controller)
            {
                Undo.RecordObject(exploration, "Use slot machine combat");
                exploration.ConfigureCombatForEditor(controller);
                MarkChanged(exploration);
                summary.AppendLine(" ExplorationRunController uses TemporarySlotCombatController");
            }
            if (scene.isDirty) EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void ValidateScene(Scene scene)
        {
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            var panel = components.OfType<SlotMachinePanel>().Single();
            var dicePanel = components.OfType<OwnedDicePanel>().Single();
            var controller = components.OfType<TemporarySlotCombatController>().Single();
            var diceController = components.OfType<TemporaryDiceRollMenuController>().Single();
            var boundary = components.OfType<BoundaryCommandButton>().Single(button => button.Placement != null);
            var exploration = components.OfType<ExplorationRunController>().Single();
            Require(panel.transform.parent == dicePanel.transform.parent, "SlotMachinePanel shares the OwnedDicePanel ContentLayer");
            Require(panel.gameObject.activeSelf && !dicePanel.gameObject.activeSelf, "SlotMachinePanel is active and OwnedDicePanel is inactive");
            Require(panel.ReelCount >= 1 && panel.ReelPrefabForEditor != null && panel.ReelAreaForEditor != null, "SlotMachinePanel has reels and a reel prefab");
            Require(panel.GetComponent<CanvasGroup>() != null && panel.GetComponent<FlexibleLayoutItem>() != null, "SlotMachinePanel has a CanvasGroup and a FlexibleLayoutItem");
            Require(controller.isActiveAndEnabled || controller.enabled, "TemporarySlotCombatController is enabled");
            Require(!diceController.enabled, "the reserved dice controller component is disabled");
            var binder = components.OfType<OwnedDiceSessionBinder>().SingleOrDefault();
            Require(binder == null || !binder.enabled, "the reserved OwnedDiceSessionBinder component is disabled");
            Require(controller.ActionButtonForEditor == boundary && controller.Panel == panel && controller.MenuForEditor != null &&
                controller.StoryForEditor != null && controller.CombatConfigurationForEditor != null, "TemporarySlotCombatController references are complete");
            Require(boundary.CommandId == TemporarySlotCombatController.SpinCommandId, "the action button sends temporary-slot.spin");
            var handler = new SerializedObject(boundary).FindProperty("commandHandlerBehaviour").objectReferenceValue;
            Require(handler == controller, "the action button is handled by TemporarySlotCombatController");
            Require(boundary.Placement.SecondPanel == (RectTransform)panel.transform, "the boundary second panel is SlotMachinePanel");
            Require(exploration.CombatControllerForEditor == controller, "exploration uses TemporarySlotCombatController");
            var blocker = components.OfType<CoveredPanelInteractionBlocker>().SingleOrDefault();
            if (blocker != null)
            {
                var covered = new SerializedObject(blocker).FindProperty("coveredPanels");
                var groups = Enumerable.Range(0, covered.arraySize).Select(i => covered.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
                Require(groups.Contains(panel.GetComponent<CanvasGroup>()) && !groups.Contains(dicePanel.GetComponent<CanvasGroup>()),
                    "the node selection overlay covers SlotMachinePanel instead of OwnedDicePanel");
            }
        }

        private static void ValidateAssets()
        {
            Require(AssetDatabase.LoadAssetAtPath<TemporarySlotMachineConfiguration>(SlotConfigurationPath) != null, "TemporarySlotMachineConfiguration.asset exists");
            Require(AssetDatabase.LoadAssetAtPath<TemporaryRuneConfiguration>(RuneConfigurationPath) != null, "TemporaryRuneConfiguration.asset exists");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(ReelPrefabPath)?.GetComponent<SlotReelView>() != null, "SlotReel.prefab exists");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath)?.GetComponent<SlotMachinePanel>() != null, "SlotMachinePanel.prefab exists");
            var root = PrefabUtility.LoadPrefabContents(AppRootPath);
            try
            {
                var host = root.GetComponentInChildren<PlayerSessionHost>(true);
                Require(host != null && host.TemporarySlotMachineConfigurationForEditor != null && host.TemporaryRuneConfigurationForEditor != null,
                    "AppRoot PlayerSessionHost references both slot machine and rune configurations");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static (Vector2 size, Vector2 position) MeasureRect(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)rect.parent);
            return (rect.rect.size, rect.anchoredPosition);
        }

        private static void SetActive(GameObject target, bool value, StringBuilder summary, string label)
        {
            if (target.activeSelf == value) return;
            Undo.RecordObject(target, "Reserve dice panel");
            target.SetActive(value);
            MarkChanged(target);
            summary.AppendLine($" {label}: {(value ? "activated" : "deactivated")}");
        }

        private static void SetEnabled(Behaviour target, bool value, StringBuilder summary)
        {
            if (target.enabled == value) return;
            Undo.RecordObject(target, "Reserve dice participant");
            target.enabled = value;
            MarkChanged(target);
            summary.AppendLine($" {target.GetType().Name} on {target.name}: {(value ? "enabled" : "disabled")}");
        }

        private static void MarkChanged(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            if (target is Component component) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            else if (target is GameObject gameObject) EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        // ---------- Construction helpers ----------

        private static RectTransform Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }

        private static Image Graphic(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(RectTransform rect, string value, float size, TextAlignmentOptions alignment)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.color = new Color(.92f, .95f, .98f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Slot machine combat check failed: {message}.");
        }
    }
}
