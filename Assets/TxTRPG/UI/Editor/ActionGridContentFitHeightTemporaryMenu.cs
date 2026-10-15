using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    // TEMPORARY application and measurement menus for ActionGridContentFitHeight.
    // They target the selected ActionGridPanel, never a Scene path or object name, so the same steps can be
    // repeated when the real Main Scene is composed. Apply is safe to rerun: a second run reports "already applied".
    // Retire both menus once the real Main Scene authoring procedure replaces them.
    // Not registered with the production Prefab rebuild batch: this only records Scene instance overrides.
    public static class ActionGridContentFitHeightTemporaryMenu
    {
        private const string Root = "Tools/TxT RPG/UI/Actions/Temporary/";
        private const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;

        [MenuItem(Root + "Apply Content-Fit Height To Selected Actions")]
        public static void ApplyToSelected()
        {
            if (!TryGetTarget(out var panel, out var error)) { Debug.LogError("ACTIONS_FIT_APPLY: FAIL " + error); return; }
            var group = panel.transform.parent.GetComponent<FlexibleContentLayoutGroup>();
            var health = FindSingleHealthBarSibling(panel.transform.parent, out error);
            if (error != null) { Debug.LogError("ACTIONS_FIT_APPLY: FAIL " + error); return; }
            var healthItem = health != null ? health.GetComponent<FlexibleLayoutItem>() : null;
            var canvasSize = RootCanvasSize(panel.transform);
            var fixHealth = healthItem != null && healthItem.SizeMode == FlexibleLayoutSizeMode.Weighted;
            if (fixHealth && (Mathf.Abs(canvasSize.x - ReferenceWidth) > 1f || Mathf.Abs(canvasSize.y - ReferenceHeight) > 1f))
            {
                Debug.LogError($"ACTIONS_FIT_APPLY: FAIL HealthBarPanel must be measured with the root Canvas at {ReferenceWidth}x{ReferenceHeight}; current {canvasSize}. Set the Game View to 1920x1080 and retry.");
                return;
            }

            var existing = panel.GetComponent<ActionGridContentFitHeight>();
            if (existing != null && existing.Panel == panel && !fixHealth)
            {
                Debug.Log("ACTIONS_FIT_APPLY: already applied; no changes. " + Describe(panel));
                return;
            }
            // Changes are only made on a clean Scene so the resulting diff contains this application alone.
            if (panel.gameObject.scene.isDirty)
            {
                Debug.LogError("ACTIONS_FIT_APPLY: FAIL Save or discard unsaved changes in the Scene first.");
                return;
            }

            Undo.SetCurrentGroupName("Apply Content-Fit Height To Actions");
            var undoGroup = Undo.GetCurrentGroup();
            var log = new StringBuilder("ACTIONS_FIT_APPLY: PASS");
            if (existing == null)
            {
                existing = Undo.AddComponent<ActionGridContentFitHeight>(panel.gameObject);
                existing.Initialize(panel, ActionGridContentFitHeight.DefaultGap);
                log.Append($" added ActionGridContentFitHeight(Gap {existing.Gap});");
            }
            else if (existing.Panel != panel)
            {
                Undo.RecordObject(existing, "Bind Content-Fit Height");
                existing.Initialize(panel, existing.Gap);
                log.Append($" bound panel reference, preserved Gap {existing.Gap};");
            }
            else log.Append($" component present, preserved Gap {existing.Gap};");
            EditorUtility.SetDirty(existing);
            PrefabUtility.RecordPrefabInstancePropertyModifications(existing);

            if (fixHealth)
            {
                var measured = Mathf.Round(((RectTransform)health.transform).rect.height);
                Undo.RecordObject(healthItem, "Fix HealthBarPanel height");
                healthItem.Configure(FlexibleLayoutSizeMode.Fixed, healthItem.Weight, measured, measured, healthItem.MaximumSize);
                EditorUtility.SetDirty(healthItem);
                PrefabUtility.RecordPrefabInstancePropertyModifications(healthItem);
                log.Append($" HealthBarPanel Fixed {measured} (measured at {canvasSize}, weight {healthItem.Weight} kept);");
            }
            else if (healthItem != null) log.Append(" HealthBarPanel already Fixed; untouched;");
            else log.Append(" no HealthBarPanel sibling with FlexibleLayoutItem; skipped;");

            Undo.CollapseUndoOperations(undoGroup);
            group.Rebuild();
            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            Debug.Log(log + " Scene left dirty and unsaved for review.");
        }

        [MenuItem(Root + "Measure Content-Fit Height Of Selected Actions")]
        public static void MeasureSelected()
        {
            var panel = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<ActionGridPanel>() : null;
            if (panel == null) { Debug.LogError("ACTIONS_FIT: FAIL Select exactly one ActionGridPanel."); return; }
            Canvas.ForceUpdateCanvases();
            Debug.Log("ACTIONS_FIT: " + Describe(panel));
        }

        // Play Mode sweep over representative Game View sizes. Adds temporary custom Game View sizes and removes them,
        // restoring the previous selection. "phone-safe-area" insets the nearest FlexibleLayoutPanel ancestor like the
        // existing Actions verification and restores its offsets afterwards. Results are logged with ACTIONS_FIT_SWEEP.
        [MenuItem(Root + "Measure Content-Fit Height Across Resolutions (Play Mode)")]
        public static void MeasureAcrossResolutions() => ResolutionSweep.Start();

        private static class ResolutionSweep
        {
            private static readonly (string name, int width, int height)[] Sizes = {
                ("desktop", 1920, 1080), ("ultrawide", 2560, 1080), ("tablet", 1024, 768),
                ("phone-portrait", 390, 844), ("phone-safe-area", 390, 844), ("phone-landscape", 844, 390) };
            private static ActionGridPanel panel;
            private static EditorWindow view;
            private static System.Reflection.PropertyInfo selectedIndex;
            private static object group;
            private static int previousIndex, customIndex, sizeIndex, phase;
            private static double next;
            private static RectTransform insetRoot;
            private static Vector2 savedMin, savedMax;

            public static void Start()
            {
                if (!EditorApplication.isPlaying || group != null) { Debug.LogError("ACTIONS_FIT_SWEEP: FAIL Enter Play Mode and wait for the previous sweep."); return; }
                panel = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<ActionGridPanel>() : null;
                if (panel == null) { Debug.LogError("ACTIONS_FIT_SWEEP: FAIL Select exactly one ActionGridPanel."); return; }
                var layout = panel.transform.parent != null ? panel.transform.parent.GetComponentInParent<FlexibleLayoutPanel>() : null;
                insetRoot = layout != null ? (RectTransform)layout.transform : null;
                if (insetRoot != null) { savedMin = insetRoot.offsetMin; savedMax = insetRoot.offsetMax; }
                var assembly = typeof(EditorWindow).Assembly;
                var viewType = assembly.GetType("UnityEditor.GameView");
                view = EditorWindow.GetWindow(viewType);
                selectedIndex = viewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                previousIndex = (int)selectedIndex.GetValue(view);
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                group = sizesType.GetMethod("GetGroup").Invoke(singleton, new[] { System.Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
                sizeIndex = 0; phase = 0; next = EditorApplication.timeSinceStartup + .5;
                EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayChanged;
            }

            private static void Tick()
            {
                if (EditorApplication.timeSinceStartup < next) return;
                try
                {
                    var entry = Sizes[sizeIndex];
                    if (phase == 0)
                    {
                        if (insetRoot != null)
                        {
                            if (entry.name == "phone-safe-area") { insetRoot.offsetMin = new Vector2(24, 34); insetRoot.offsetMax = new Vector2(-24, -44); }
                            else { insetRoot.offsetMin = savedMin; insetRoot.offsetMax = savedMax; }
                        }
                        var assembly = typeof(EditorWindow).Assembly;
                        var size = System.Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), System.Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), entry.width, entry.height, "Content-fit verification");
                        customIndex = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
                        var index = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null) + customIndex;
                        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                        selectedIndex.SetValue(view, index); view.Repaint();
                        phase = 1; next = EditorApplication.timeSinceStartup + 1.5; return;
                    }
                    if (Screen.width != entry.width || Screen.height != entry.height)
                        throw new System.InvalidOperationException($"Expected {entry.width}x{entry.height}, got {Screen.width}x{Screen.height}.");
                    Canvas.ForceUpdateCanvases();
                    Debug.Log($"ACTIONS_FIT_SWEEP: {entry.name} {entry.width}x{entry.height} {Describe(panel)}");
                    RemoveSize(); sizeIndex++;
                    if (sizeIndex < Sizes.Length) { next = EditorApplication.timeSinceStartup + .3; return; }
                    Finish(); Debug.Log("ACTIONS_FIT_SWEEP: PASS completed");
                }
                catch (System.Exception e) { Finish(); Debug.LogError("ACTIONS_FIT_SWEEP: FAIL " + e); }
            }

            private static void RemoveSize()
            {
                selectedIndex.SetValue(view, previousIndex);
                // RemoveCustomSize takes the total index (built-in sizes first), not the custom-list index.
                var builtin = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtin + customIndex });
                phase = 0;
            }

            private static void PlayChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Finish(); }

            private static void Finish()
            {
                EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayChanged;
                if (group != null && phase != 0) RemoveSize();
                if (insetRoot != null) { insetRoot.offsetMin = savedMin; insetRoot.offsetMax = savedMax; }
                group = null; panel = null; insetRoot = null;
            }
        }

        public static string Describe(ActionGridPanel panel)
        {
            var fit = panel.GetComponent<ActionGridContentFitHeight>();
            var supplying = false; var resolved = float.NaN; var state = ActionGridContentFitState.Disabled;
            // State and ResolvedHeight are the values from the latest parent layout query.
            if (fit != null) { supplying = fit.IsSupplying; state = fit.State; resolved = fit.ResolvedHeight; }
            var viewport = panel.ScrollRect != null ? panel.ScrollRect.viewport : null;
            var b = new StringBuilder();
            b.Append($"canvas={RootCanvasSize(panel.transform)} play={EditorApplication.isPlaying} panel={((RectTransform)panel.transform).rect.height:0.##}");
            b.Append($" viewport={(viewport != null ? viewport.rect.height : float.NaN):0.##} singleRow={panel.IsSingleRow}");
            b.Append($" fit={(fit != null ? fit.enabled.ToString() : "none")} gap={(fit != null ? fit.Gap : float.NaN):0.##} state={state} supplying={supplying} resolved={resolved:0.##}");
            if (viewport != null && TryGetFirstCell(panel, out var cell))
            {
                var v = WorldRect(viewport, viewport); var c = WorldRect(cell, viewport);
                b.Append($" cell={c.height:0.##} cellTopGap={v.yMax - c.yMax:0.##} cellBottomGap={c.yMin - v.yMin:0.##}");
            }
            if (panel.transform.parent != null)
            {
                b.Append(" siblings=[");
                foreach (RectTransform child in panel.transform.parent)
                    if (child.gameObject.activeInHierarchy) b.Append($"{child.name}:{child.rect.height:0.##} ");
                b.Append(']');
            }
            return b.ToString();
        }

        private static bool TryGetTarget(out ActionGridPanel panel, out string error)
        {
            panel = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { error = "Exit Play Mode first."; return false; }
            if (EditorApplication.isCompiling) { error = "Wait for compilation to finish."; return false; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) { error = "Close the Prefab Stage first."; return false; }
            if (Selection.gameObjects.Length != 1 || !Selection.activeGameObject.TryGetComponent(out panel))
            { error = "Select exactly one GameObject with ActionGridPanel."; return false; }
            if (panel.transform.parent == null || panel.transform.parent.GetComponent<FlexibleContentLayoutGroup>() == null)
            { error = "The selected ActionGridPanel's parent must have FlexibleContentLayoutGroup."; return false; }
            if (!panel.IsSingleRow) { error = "The selected ActionGridPanel must use SingleRow flow."; return false; }
            error = null;
            return true;
        }

        private static HealthBarPanel FindSingleHealthBarSibling(Transform parent, out string error)
        {
            HealthBarPanel found = null; error = null;
            foreach (Transform child in parent)
            {
                if (!child.TryGetComponent<HealthBarPanel>(out var health)) continue;
                if (found != null) { error = "More than one HealthBarPanel sibling; fix it manually."; return null; }
                found = health;
            }
            return found;
        }

        private static bool TryGetFirstCell(ActionGridPanel panel, out RectTransform cell)
        {
            cell = null;
            var content = panel.ScrollRect.content;
            for (var i = 0; content != null && i < content.childCount; i++)
                if (content.GetChild(i).gameObject.activeSelf) { cell = (RectTransform)content.GetChild(i); return true; }
            return false;
        }

        private static Rect WorldRect(RectTransform rect, RectTransform space)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = space.InverseTransformPoint(corners[0]); var max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Vector2 RootCanvasSize(Transform t)
        {
            var canvas = t.GetComponentInParent<Canvas>();
            return canvas != null ? ((RectTransform)canvas.rootCanvas.transform).rect.size : Vector2.zero;
        }
    }
}
