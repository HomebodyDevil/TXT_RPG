using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using TxTRPG.Application.Encounters;
using TxTRPG.Gameplay.Encounters;
using TxTRPG.UI;
using TxTRPG.UI.Encounters;
using TxTRPG.UI.Shapes;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (candidate-main-scene-encounter-toggles-and-debug-roll). Creates, only when missing:
    // EncounterElementCatalog.asset (rune / 룬), EncounterRollConfiguration.asset (0.5, 0.5, 1/1/1) and the four Prefabs
    // EncounterElementToggle, EncounterElementToggleBar, EncounterDebugPanel and EncounterResultModal. Then, in
    // Candidate_MainScene, it sets MiddleArea's FlexibleLayoutPanel to a fixed vertical axis (instance override, which the
    // panel syncs to its ContentLayer), places EncounterElementToggleBar (Fixed 64) and a MiddleContentPlaceholder
    // (Weighted 1) in MiddleArea/ContentLayer, EncounterDebugPanel (top right, margin 16) and EncounterResultModal (last
    // sibling) in OverlayLayer, and Canvas/EncounterRollController with all references. Existing assets and Prefabs are
    // never overwritten, and an existing EncounterRollController leaves the Scene untouched, so rerunning is safe. It stops
    // without changes in Play Mode, while compiling, in Prefab Stage, with unsaved open Scenes, or when
    // MiddleArea/ContentLayer has other children or OverlayLayer already has an object with a target name. Saves only what
    // it created and Candidate_MainScene. Not part of the Prefab rebuild batch (one-time application; the Prefabs are
    // developer-owned afterwards). Retire it once the user has applied and verified the feature.
    public static class TemporaryEncounterTogglesApply
    {
        // TxTRPGEditorMenuPaths.Application + "Temporary/Apply Candidate Encounter Toggles" (this assembly does not
        // reference TxTRPG.Editor.Common).
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Candidate Encounter Toggles";
        public const string ScenePath = "Assets/Scenes/Candidate_MainScene.unity";
        public const string ConfigurationFolder = "Assets/TxTRPG/Application/Configuration/Encounters";
        public const string CatalogPath = ConfigurationFolder + "/EncounterElementCatalog.asset";
        public const string RollConfigurationPath = ConfigurationFolder + "/EncounterRollConfiguration.asset";
        public const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs/Encounters";
        public const string TogglePrefabPath = PrefabFolder + "/EncounterElementToggle.prefab";
        public const string ToggleBarPrefabPath = PrefabFolder + "/EncounterElementToggleBar.prefab";
        public const string DebugPanelPrefabPath = PrefabFolder + "/EncounterDebugPanel.prefab";
        public const string ResultModalPrefabPath = PrefabFolder + "/EncounterResultModal.prefab";
        public const string PlaceholderPrefabPath = "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPlaceholder.prefab";
        public const string MiddleAreaPath = "MainArea/ContentLayer/MiddleArea";
        public const string MiddleContentPath = MiddleAreaPath + "/ContentLayer";
        public const string OverlayPath = "OverlayLayer";
        public const string ToggleBarName = "EncounterElementToggleBar";
        public const string PlaceholderName = "MiddleContentPlaceholder";
        public const string DebugPanelName = "EncounterDebugPanel";
        public const string ResultModalName = "EncounterResultModal";
        public const string ControllerName = "EncounterRollController";
        public const string RuneId = "rune";
        public const string RuneName = "룬";
        public const float ToggleBarHeight = 64f;
        public const float DebugPanelWidth = 320f;
        public const float DebugPanelMargin = 16f;
        public const float ResultWindowWidth = 560f;
        public const float ResultWindowHeight = 240f;
        public const float ResultWindowMargin = 16f;

        private static readonly Color ButtonColor = new Color32(0x1C, 0x27, 0x45, 0xFF);
        private static readonly Color SelectedBorderColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
        private static readonly Color PanelColor = new Color32(0x10, 0x15, 0x22, 0xEB);
        private static readonly Color WindowColor = new Color32(0x16, 0x1D, 0x30, 0xFF);
        private static readonly Color ControlColor = new Color32(0x2B, 0x38, 0x5E, 0xFF);

        [MenuItem(MenuPath, false, 3010)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("ENCOUNTER_APPLY: exit Play Mode first. Nothing was changed."); return; }
            if (EditorApplication.isCompiling) { Debug.LogError("ENCOUNTER_APPLY: wait for script compilation to finish. Nothing was changed."); return; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) { Debug.LogError("ENCOUNTER_APPLY: save and close Prefab Stage first. Nothing was changed."); return; }
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                Debug.LogError("ENCOUNTER_APPLY: save or revert the unsaved open Scenes first. Nothing was changed.");
                return;
            }
            var summary = new StringBuilder("ENCOUNTER_APPLY:\n");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var canvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas");
                var middle = canvas != null ? canvas.transform.Find(MiddleAreaPath) : null;
                var middleContent = canvas != null ? canvas.transform.Find(MiddleContentPath) : null;
                var overlay = canvas != null ? canvas.transform.Find(OverlayPath) : null;
                if (middle == null || middleContent == null || overlay == null)
                {
                    Debug.LogError($"ENCOUNTER_APPLY: {ScenePath} needs Canvas/{MiddleContentPath} and Canvas/{OverlayPath}. Nothing was changed.");
                    return;
                }
                var existingController = canvas.transform.Find(ControllerName);
                if (existingController == null)
                {
                    if (middleContent.childCount > 0)
                    {
                        Debug.LogError($"ENCOUNTER_APPLY: {MiddleContentPath} already has children ({string.Join(", ", middleContent.Cast<Transform>().Select(t => t.name))}). Nothing was changed; report this to the developer.");
                        return;
                    }
                    var clash = overlay.Cast<Transform>().Select(t => t.name).Where(n => n == DebugPanelName || n == ResultModalName).ToArray();
                    if (clash.Length > 0)
                    {
                        Debug.LogError($"ENCOUNTER_APPLY: {OverlayPath} already has {string.Join(", ", clash)}. Nothing was changed; report this to the developer.");
                        return;
                    }
                }

                var catalog = EnsureCatalog(summary);
                var configuration = EnsureRollConfiguration(summary);
                var toggle = EnsureTogglePrefab(summary);
                var bar = EnsureToggleBarPrefab(toggle, summary);
                var debugPanel = EnsureDebugPanelPrefab(summary);
                var modal = EnsureResultModalPrefab(summary);
                if (!ValidatePrefabs(out var error)) { Debug.LogError($"{summary} invalid Prefab: {error}. Delete it and rerun to recreate it."); return; }

                if (existingController != null)
                    summary.AppendLine($" kept existing {ControllerName} in {ScenePath}; Scene unchanged");
                else
                {
                    Place(canvas.transform, middle, middleContent, overlay, catalog, configuration, bar, debugPanel, modal);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Could not save {ScenePath}.");
                    summary.AppendLine($" placed the toggle bar, placeholder, debug panel, result modal and {ControllerName}; saved {ScenePath}");
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError($"{summary} FAILED: {exception}");
                return;
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            summary.AppendLine(" validation: passed. Safe to rerun.");
            Debug.Log(summary.ToString());
        }

        public static bool ValidatePrefabs(out string error)
        {
            var toggle = Load<EncounterElementToggleView>(TogglePrefabPath);
            var bar = Load<EncounterElementToggleBar>(ToggleBarPrefabPath);
            var panel = Load<EncounterDebugPanel>(DebugPanelPrefabPath);
            var modal = Load<GameWindowService>(ResultModalPrefabPath);
            string missing = null;
            error = toggle == null ? "EncounterElementToggle.prefab has no EncounterElementToggleView"
                : !toggle.IsValid(out missing) ? $"EncounterElementToggle.prefab is missing {missing}"
                : bar == null ? "EncounterElementToggleBar.prefab has no EncounterElementToggleBar"
                : !bar.IsValid(out missing) ? $"EncounterElementToggleBar.prefab is missing {missing}"
                : bar.Content.childCount > 0 ? "EncounterElementToggleBar.prefab Content must not store toggles"
                : panel == null ? "EncounterDebugPanel.prefab has no EncounterDebugPanel"
                : !panel.IsValid(out missing) ? $"EncounterDebugPanel.prefab is missing {missing}"
                : panel.GetComponent<DevelopmentBuildOnly>() == null ? "EncounterDebugPanel.prefab has no DevelopmentBuildOnly"
                : modal == null || modal.Host == null ? "EncounterResultModal.prefab has no GameWindowService with a host"
                : !modal.HasPage(EncounterRollController.ResultPageId) ? $"EncounterResultModal.prefab has no '{EncounterRollController.ResultPageId}' page"
                : modal.Host.WindowRect == null ? "EncounterResultModal.prefab host has no windowRect"
                : string.Empty;
            return error.Length == 0;
        }

        // ---------- Assets ----------

        private static EncounterElementCatalog EnsureCatalog(StringBuilder summary)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EncounterElementCatalog>(CatalogPath);
            if (catalog != null) { summary.AppendLine($" kept {CatalogPath}"); return catalog; }
            EnsureFolder(ConfigurationFolder);
            catalog = ScriptableObject.CreateInstance<EncounterElementCatalog>();
            catalog.SetEntries(new[] { new EncounterElementCatalog.Entry(RuneId, RuneName) });
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            summary.AppendLine($" created {CatalogPath} ({RuneId} / {RuneName})");
            return catalog;
        }

        private static EncounterRollConfiguration EnsureRollConfiguration(StringBuilder summary)
        {
            var configuration = AssetDatabase.LoadAssetAtPath<EncounterRollConfiguration>(RollConfigurationPath);
            if (configuration != null) { summary.AppendLine($" kept {RollConfigurationPath}"); return configuration; }
            EnsureFolder(ConfigurationFolder);
            configuration = ScriptableObject.CreateInstance<EncounterRollConfiguration>();
            configuration.SetValues(EncounterRollSettings.Default);
            AssetDatabase.CreateAsset(configuration, RollConfigurationPath);
            summary.AppendLine($" created {RollConfigurationPath} (0.5, 0.5, 1/1/1)");
            return configuration;
        }

        // ---------- Prefabs ----------

        private static EncounterElementToggleView EnsureTogglePrefab(StringBuilder summary)
        {
            var existing = Load<EncounterElementToggleView>(TogglePrefabPath);
            if (existing != null) { summary.AppendLine($" kept {TogglePrefabPath}"); return existing; }
            return Save<EncounterElementToggleView>(TogglePrefabPath, summary, () =>
            {
                var root = NewUi("EncounterElementToggle", null);
                root.sizeDelta = new Vector2(120f, 48f);
                var layout = root.gameObject.AddComponent<LayoutElement>();
                layout.minWidth = 96f; layout.preferredWidth = 120f; layout.minHeight = 48f; layout.preferredHeight = 48f;
                var toggle = root.gameObject.AddComponent<Toggle>();
                var view = root.gameObject.AddComponent<EncounterElementToggleView>();

                var hit = Stretch(NewUi("HitArea", root)).gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;

                var visual = Stretch(NewUi("VisualRoot", root));
                var background = Stretch(NewUi("Background", visual)).gameObject.AddComponent<UiShapeGraphic>();
                background.Configure(UiShape.RoundedRectangle, 10f, UiShapeDrawMode.Fill, 0f);
                background.color = ButtonColor;
                background.raycastTarget = false;
                var border = Stretch(NewUi("SelectedBorder", visual)).gameObject.AddComponent<UiShapeGraphic>();
                border.Configure(UiShape.RoundedRectangle, 10f, UiShapeDrawMode.InnerBorder, 4f);
                border.color = SelectedBorderColor;
                border.raycastTarget = false;
                border.gameObject.SetActive(false);

                var checkRect = NewUi("Checkmark", visual);
                checkRect.anchorMin = new Vector2(0f, .5f); checkRect.anchorMax = new Vector2(0f, .5f);
                checkRect.pivot = new Vector2(0f, .5f); checkRect.sizeDelta = new Vector2(20f, 20f);
                checkRect.anchoredPosition = new Vector2(10f, 0f);
                var check = Text(checkRect, "✓", 14f, 22f);
                check.color = SelectedBorderColor;
                check.fontStyle = FontStyles.Bold;
                check.gameObject.SetActive(false);

                var labelRect = Stretch(NewUi("Label", visual));
                labelRect.offsetMin = new Vector2(32f, 4f); labelRect.offsetMax = new Vector2(-10f, -4f);
                var label = Text(labelRect, RuneName, 12f, 20f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;

                toggle.targetGraphic = background;
                toggle.graphic = null;
                toggle.isOn = false;
                toggle.toggleTransition = Toggle.ToggleTransition.None;
                view.ConfigureForEditor(toggle, visual, border.gameObject, check.gameObject, label);
                return root.gameObject;
            });
        }

        private static EncounterElementToggleBar EnsureToggleBarPrefab(EncounterElementToggleView toggle, StringBuilder summary)
        {
            var existing = Load<EncounterElementToggleBar>(ToggleBarPrefabPath);
            if (existing != null) { summary.AppendLine($" kept {ToggleBarPrefabPath}"); return existing; }
            return Save<EncounterElementToggleBar>(ToggleBarPrefabPath, summary, () =>
            {
                var root = Stretch(NewUi("EncounterElementToggleBar", null));
                var bar = root.gameObject.AddComponent<EncounterElementToggleBar>();
                var scrollView = Stretch(NewUi("Scroll View", root));
                var scroll = scrollView.gameObject.AddComponent<ScrollRect>();
                var viewport = Stretch(NewUi("Viewport", scrollView));
                viewport.gameObject.AddComponent<RectMask2D>();
                var hit = viewport.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;

                var content = NewUi("Content", viewport);
                content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, .5f);
                content.offsetMin = content.offsetMax = Vector2.zero;
                var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 8f;
                row.padding = new RectOffset(8, 8, 0, 0);
                row.childAlignment = TextAnchor.MiddleLeft;
                row.childControlWidth = true; row.childControlHeight = true;
                row.childForceExpandWidth = false; row.childForceExpandHeight = false;
                var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

                scroll.content = content;
                scroll.viewport = viewport;
                scroll.horizontal = true;
                scroll.vertical = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 24f;
                bar.ConfigureForEditor(scroll, content, toggle);
                return root.gameObject;
            });
        }

        private static EncounterDebugPanel EnsureDebugPanelPrefab(StringBuilder summary)
        {
            var existing = Load<EncounterDebugPanel>(DebugPanelPrefabPath);
            if (existing != null) { summary.AppendLine($" kept {DebugPanelPrefabPath}"); return existing; }
            return Save<EncounterDebugPanel>(DebugPanelPrefabPath, summary, () =>
            {
                var root = NewUi("EncounterDebugPanel", null);
                root.anchorMin = root.anchorMax = Vector2.one;
                root.pivot = Vector2.one;
                root.sizeDelta = new Vector2(DebugPanelWidth, 0f);
                root.anchoredPosition = new Vector2(-DebugPanelMargin, -DebugPanelMargin);
                var background = root.gameObject.AddComponent<Image>();
                background.color = PanelColor;
                background.raycastTarget = true;
                var panel = root.gameObject.AddComponent<EncounterDebugPanel>();
                root.gameObject.AddComponent<DevelopmentBuildOnly>();
                Column(root.gameObject, 10, 6f);
                root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var header = NewUi("Header", root);
                Row(header.gameObject, 40f);
                var title = Text(NewUi("Title", header), "DEBUG · 이벤트 판정", 12f, 16f);
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.fontStyle = FontStyles.Bold;
                Flexible(title.gameObject, 1f);
                var collapse = NewButton(header, "CollapseButton", EncounterDebugPanel.ExpandedLabel, 72f, out var collapseLabel);

                var body = NewUi("Body", root);
                Column(body.gameObject, 0, 6f);
                var occurrence = SliderRow(body, "OccurrenceRow", "발생 확률", 100f, out var occurrenceValue);
                var inclusion = SliderRow(body, "InclusionRow", "요소 포함", 100f, out var inclusionValue);
                var combat = SliderRow(body, "CombatWeightRow", "전투 가중치", EncounterDebugPanel.MaximumWeight * 10f, out var combatValue);
                var @event = SliderRow(body, "EventWeightRow", "이벤트 가중치", EncounterDebugPanel.MaximumWeight * 10f, out var eventValue);
                var rest = SliderRow(body, "RestWeightRow", "휴식 가중치", EncounterDebugPanel.MaximumWeight * 10f, out var restValue);

                var buttons = NewUi("Buttons", body);
                Row(buttons.gameObject, 48f);
                var roll = NewButton(buttons, "RollButton", "이벤트 발생", -1f, out _);
                var reset = NewButton(buttons, "ResetButton", "기본값으로", -1f, out _);

                var result = Text(NewUi("ResultText", body), string.Empty, 14f, 14f);
                result.enableAutoSizing = false;
                result.fontSize = 15f;
                result.alignment = TextAlignmentOptions.TopLeft;
                result.textWrappingMode = TextWrappingModes.Normal;
                result.gameObject.AddComponent<LayoutElement>().minHeight = 40f;

                panel.ConfigureSlidersForEditor(occurrence, occurrenceValue, inclusion, inclusionValue, combat, combatValue,
                    @event, eventValue, rest, restValue);
                panel.ConfigureControlsForEditor(roll, reset, collapse, collapseLabel, body.gameObject, result);
                return root.gameObject;
            });
        }

        private static GameWindowService EnsureResultModalPrefab(StringBuilder summary)
        {
            var existing = Load<GameWindowService>(ResultModalPrefabPath);
            if (existing != null) { summary.AppendLine($" kept {ResultModalPrefabPath}"); return existing; }
            return Save<GameWindowService>(ResultModalPrefabPath, summary, () =>
            {
                var root = Stretch(NewUi("EncounterResultModal", null));
                var service = root.gameObject.AddComponent<GameWindowService>();

                var hostRect = Stretch(NewUi("ModalWindowHost", root));
                var backdrop = hostRect.gameObject.AddComponent<Image>();
                backdrop.color = new Color(0f, 0f, 0f, .6f);
                backdrop.raycastTarget = true;
                var group = hostRect.gameObject.AddComponent<CanvasGroup>();
                var host = hostRect.gameObject.AddComponent<ModalWindowHost>();

                // Frame lays the window out at min(560, width − 32) × min(240, height − 32), centered. It draws nothing,
                // so every backdrop press still hits the host (outside-close).
                var frame = Stretch(NewUi("WindowFrame", hostRect));
                var frameLayout = frame.gameObject.AddComponent<HorizontalLayoutGroup>();
                var margin = Mathf.RoundToInt(ResultWindowMargin);
                frameLayout.padding = new RectOffset(margin, margin, margin, margin);
                frameLayout.childAlignment = TextAnchor.MiddleCenter;
                frameLayout.childControlWidth = true; frameLayout.childControlHeight = true;
                frameLayout.childForceExpandWidth = false; frameLayout.childForceExpandHeight = false;

                var window = NewUi("Window", frame);
                var windowImage = window.gameObject.AddComponent<UiShapeGraphic>();
                windowImage.Configure(UiShape.RoundedRectangle, 14f, UiShapeDrawMode.Fill, 0f);
                windowImage.color = WindowColor;
                windowImage.raycastTarget = true;
                var windowLayout = window.gameObject.AddComponent<LayoutElement>();
                windowLayout.minWidth = 0f; windowLayout.preferredWidth = ResultWindowWidth;
                windowLayout.minHeight = 0f; windowLayout.preferredHeight = ResultWindowHeight;

                var titleRect = NewUi("Title", window);
                titleRect.anchorMin = new Vector2(0f, 1f); titleRect.anchorMax = Vector2.one; titleRect.pivot = new Vector2(.5f, 1f);
                titleRect.offsetMin = new Vector2(16f, -56f); titleRect.offsetMax = new Vector2(-96f, -8f);
                var title = Text(titleRect, EncounterRollController.ResultTitle, 16f, 24f);
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.fontStyle = FontStyles.Bold;

                var close = NewButton(window, "CloseButton", "닫기", 72f, out _);
                var closeRect = (RectTransform)close.transform;
                closeRect.anchorMin = closeRect.anchorMax = Vector2.one; closeRect.pivot = Vector2.one;
                closeRect.sizeDelta = new Vector2(72f, 48f); closeRect.anchoredPosition = new Vector2(-8f, -8f);
                close.gameObject.AddComponent<ModalCancelForwarder>().ConfigureForEditor(host);

                var loading = Text(Stretch(NewUi("Loading", window)), "불러오는 중…", 14f, 20f);
                loading.gameObject.SetActive(false);

                var errorRoot = Stretch(NewUi("ErrorStateRoot", window));
                errorRoot.offsetMin = new Vector2(16f, 16f); errorRoot.offsetMax = new Vector2(-16f, -64f);
                var errorText = Text(Stretch(NewUi("Error", errorRoot)), string.Empty, 12f, 18f);
                ((RectTransform)errorText.transform).offsetMin = new Vector2(0f, 56f);
                errorText.textWrappingMode = TextWrappingModes.Normal;
                var retry = NewButton(errorRoot, "RetryButton", "다시 시도", 120f, out _);
                var retryRect = (RectTransform)retry.transform;
                retryRect.anchorMin = retryRect.anchorMax = new Vector2(.5f, 0f); retryRect.pivot = new Vector2(.5f, 0f);
                retryRect.sizeDelta = new Vector2(120f, 48f); retryRect.anchoredPosition = Vector2.zero;
                errorRoot.gameObject.SetActive(false);

                var containerRect = Stretch(NewUi("ContentContainer", window));
                containerRect.offsetMin = new Vector2(16f, 16f); containerRect.offsetMax = new Vector2(-16f, -64f);
                var pages = Stretch(NewUi("Pages", containerRect));
                var overlayRoot = Stretch(NewUi("OverlayRoot", window));
                overlayRoot.offsetMin = new Vector2(16f, 16f); overlayRoot.offsetMax = new Vector2(-16f, -64f);
                var container = containerRect.gameObject.AddComponent<ModalContentContainer>();
                container.ConfigureForEditor(pages, overlayRoot, new RectOffset());

                var pageRect = Stretch(NewUi("EncounterResultPage", pages));
                var page = pageRect.gameObject.AddComponent<MessageGameWindowPage>();
                var message = Text(Stretch(NewUi("Message", pageRect)), string.Empty, 18f, 32f);
                message.textWrappingMode = TextWrappingModes.Normal;
                message.overflowMode = TextOverflowModes.Ellipsis;
                page.ConfigureForEditor(EncounterRollController.ResultPageId, close.gameObject, EncounterRollController.ResultTitle);
                page.ConfigureMessageForEditor(message, string.Empty);

                host.ConfigureForEditor(group, title, loading.gameObject, errorText, close, container, errorRoot.gameObject, retry);
                host.ConfigureOutsideCloseForEditor(window);
                service.ConfigureForEditor(host, new GameWindowPage[] { page });
                group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
                return root.gameObject;
            });
        }

        // ---------- Scene ----------

        private static void Place(Transform canvas, Transform middle, Transform middleContent, Transform overlay,
            EncounterElementCatalog catalog, EncounterRollConfiguration configuration, EncounterElementToggleBar barPrefab,
            EncounterDebugPanel debugPrefab, GameWindowService modalPrefab)
        {
            // MiddleArea stacks vertically regardless of MainArea's breakpoint: bar on top, placeholder below.
            var middlePanel = middle.GetComponent<FlexibleLayoutPanel>() ?? throw new InvalidOperationException("MiddleArea has no FlexibleLayoutPanel.");
            if (middlePanel.ContentLayout == null || middlePanel.ContentLayout.FixedAxis != FlexibleLayoutAxis.Vertical ||
                middlePanel.ContentLayout.AxisPolicy != FlexibleLayoutAxisPolicy.Fixed)
            {
                middlePanel.SetAxis(FlexibleLayoutAxis.Vertical);
                PrefabUtility.RecordPrefabInstancePropertyModifications(middlePanel);
                if (middlePanel.ContentLayout != null) PrefabUtility.RecordPrefabInstancePropertyModifications(middlePanel.ContentLayout);
            }

            var bar = (GameObject)PrefabUtility.InstantiatePrefab(barPrefab.gameObject, middleContent);
            bar.name = ToggleBarName;
            Stretch((RectTransform)bar.transform);
            bar.AddComponent<FlexibleLayoutItem>().Configure(FlexibleLayoutSizeMode.Fixed, 1f, ToggleBarHeight);

            var placeholderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPrefabPath)
                ?? throw new InvalidOperationException($"Missing {PlaceholderPrefabPath}.");
            var placeholder = (GameObject)PrefabUtility.InstantiatePrefab(placeholderPrefab, middleContent);
            placeholder.name = PlaceholderName;
            placeholder.SetActive(true);
            Stretch((RectTransform)placeholder.transform);
            var placeholderItem = placeholder.GetComponent<FlexibleLayoutItem>() ?? placeholder.AddComponent<FlexibleLayoutItem>();
            if (placeholderItem.SizeMode != FlexibleLayoutSizeMode.Weighted || !Mathf.Approximately(placeholderItem.Weight, 1f))
            {
                placeholderItem.Configure(FlexibleLayoutSizeMode.Weighted, 1f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(placeholderItem);
            }
            middlePanel.Rebuild();

            var debug = (GameObject)PrefabUtility.InstantiatePrefab(debugPrefab.gameObject, overlay);
            debug.name = DebugPanelName;
            var modal = (GameObject)PrefabUtility.InstantiatePrefab(modalPrefab.gameObject, overlay);
            modal.name = ResultModalName;
            modal.transform.SetAsLastSibling();

            var controllerObject = new GameObject(ControllerName, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            controllerObject.transform.SetParent(canvas, false);
            var controller = controllerObject.AddComponent<EncounterRollController>();
            var service = modal.GetComponent<GameWindowService>();
            var page = modal.GetComponentInChildren<MessageGameWindowPage>(true);
            controller.ConfigureForEditor(catalog, configuration, bar.GetComponent<EncounterElementToggleBar>(), service, page,
                debug.GetComponent<EncounterDebugPanel>());
            EditorUtility.SetDirty(controller);
        }

        // ---------- Helpers ----------

        private static T Load<T>(string path) where T : Component
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return asset != null ? asset.GetComponent<T>() : null;
        }

        private static T Save<T>(string path, StringBuilder summary, Func<GameObject> build) where T : Component
        {
            EnsureFolder(PrefabFolder);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = build();
                SceneManager.MoveGameObjectToScene(root, preview);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
                if (!success) throw new InvalidOperationException($"Could not save {path}.");
                summary.AppendLine($" created {path}");
                return saved.GetComponent<T>();
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static Slider SliderRow(Transform parent, string name, string labelText, float maximum, out TMP_Text value)
        {
            var row = NewUi(name, parent);
            Row(row.gameObject, 36f);
            var label = Text(NewUi("Label", row), labelText, 11f, 14f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Fixed(label.gameObject, 92f);
            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = "Slider";
            SetLayerRecursively(sliderObject, LayerMask.NameToLayer("UI"));
            sliderObject.transform.SetParent(row, false);
            Flexible(sliderObject, 1f).minWidth = 80f;
            var slider = sliderObject.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = maximum; slider.wholeNumbers = true; slider.value = 0f;
            foreach (var image in sliderObject.GetComponentsInChildren<Image>(true))
                image.color = image.name == "Background" ? new Color(1f, 1f, 1f, .15f) : image.name == "Fill" ? SelectedBorderColor : Color.white;
            value = Text(NewUi("Value", row), string.Empty, 11f, 14f);
            value.alignment = TextAlignmentOptions.MidlineRight;
            Fixed(value.gameObject, 52f);
            return slider;
        }

        private static Button NewButton(Transform parent, string name, string labelText, float width, out TMP_Text label)
        {
            var rect = NewUi(name, parent);
            var shape = rect.gameObject.AddComponent<UiShapeGraphic>();
            shape.Configure(UiShape.RoundedRectangle, 8f, UiShapeDrawMode.Fill, 0f);
            shape.color = ControlColor;
            shape.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = shape;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 44f; layout.preferredHeight = 44f;
            if (width > 0f) { layout.minWidth = width; layout.preferredWidth = width; layout.flexibleWidth = 0f; }
            else { layout.minWidth = 96f; layout.flexibleWidth = 1f; }
            var labelRect = Stretch(NewUi("Label", rect));
            labelRect.offsetMin = new Vector2(6f, 2f); labelRect.offsetMax = new Vector2(-6f, -2f);
            label = Text(labelRect, labelText, 11f, 16f);
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

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
