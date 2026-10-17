using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using TxTRPG.UI.Shapes;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (candidate-main-scene-slot-reel-dock). Creates SlotReelDock.prefab when it is missing,
    // copying the reel machine look (Frame, edges, dividers, payline) and the total texts from SlotMachinePanel.prefab as
    // independent objects and nesting three SlotReel.prefab instances, then adds Canvas/OverlayLayer with one
    // SlotReelDock instance to Candidate_MainScene when it has none. SlotMachinePanel.prefab, SlotReel.prefab and
    // TMP_MainScene are only read. An existing SlotReelDock.prefab is never rebuilt: it is validated and, when broken,
    // reported (delete it to recreate it). A file is written only when something is missing, so rerunning is safe; it
    // stops without changes in Play Mode, while compiling, in Prefab Stage or with unsaved open Scenes.
    // Not part of the registered Prefab rebuild batch (it also edits a Scene; the Prefab is developer-owned afterwards).
    // Retire it once the user has applied and verified the dock.
    public static class TemporarySlotReelDockApply
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Slot Reel Dock To Candidate Main Scene";
        public const string DockPrefabPath = "Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotReelDock.prefab";
        public const string ScenePath = "Assets/Scenes/Candidate_MainScene.unity";
        public const string OverlayLayerName = "OverlayLayer";

        // Darker than the panel's amber so the multiplier stays readable on the white card; the "×N" text carries the meaning.
        private static readonly Color MultiplierColor = new Color32(0xB4, 0x53, 0x09, 0xFF);

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("SLOT_REEL_DOCK_APPLY: exit Play Mode first."); return; }
            if (EditorApplication.isCompiling) { Debug.LogError("SLOT_REEL_DOCK_APPLY: wait for script compilation to finish."); return; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) { Debug.LogError("SLOT_REEL_DOCK_APPLY: save and close Prefab Stage first."); return; }
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                Debug.LogError("SLOT_REEL_DOCK_APPLY: save or revert the unsaved open Scenes first. Nothing was changed.");
                return;
            }
            var summary = new StringBuilder("SLOT_REEL_DOCK_APPLY:\n");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DockPrefabPath);
                if (prefab == null)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    prefab = CreatePrefab(summary);
                }
                else
                {
                    summary.AppendLine($" kept {DockPrefabPath}");
                    RepairReelOrder(prefab, summary);
                }
                if (!ValidatePrefab(prefab, out var error)) { Debug.LogError($"{summary} invalid {DockPrefabPath}: {error}. Delete it and rerun to recreate it."); return; }
                PlaceInScene(prefab, summary);
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            summary.AppendLine(" validation: passed. Safe to rerun.");
            Debug.Log(summary.ToString());
        }

        public static bool ValidatePrefab(GameObject prefab, out string error)
        {
            var dock = prefab != null ? prefab.GetComponent<SlotReelDock>() : null;
            if (dock == null) { error = "no SlotReelDock on the root"; return false; }
            if (dock.PresentationRoot == null || dock.ReelMachine == null || dock.ReelArea == null || dock.TotalCard == null)
            { error = "missing structure reference"; return false; }
            if (dock.ReelPrefabForEditor == null || dock.SpinProfileForEditor == null || dock.SkinCatalogForEditor == null)
            { error = "missing reel prefab, spin profile or skin catalog"; return false; }
            if (dock.ReelCount < 1) { error = "no reels"; return false; }
            if (dock.TotalCard.transform.GetSiblingIndex() <= dock.ReelMachine.GetSiblingIndex()) { error = "TotalCard must come after ReelMachine"; return false; }
            var raycast = prefab.GetComponentsInChildren<Graphic>(true).FirstOrDefault(graphic => graphic.raycastTarget);
            if (raycast != null) { error = $"'{raycast.name}' has raycastTarget on"; return false; }
            if (dock.CanvasGroup == null || dock.CanvasGroup.blocksRaycasts || dock.CanvasGroup.interactable) { error = "CanvasGroup must not block raycasts"; return false; }
            error = string.Empty;
            return true;
        }

        // ---------- Prefab ----------

        private static GameObject CreatePrefab(StringBuilder summary)
        {
            var panelRoot = PrefabUtility.LoadPrefabContents(TemporarySlotMachineIntegration.PanelPrefabPath);
            GameObject canvasObject = null;
            try
            {
                var panel = panelRoot.GetComponent<SlotMachinePanel>() ?? throw new InvalidOperationException("SlotMachinePanel.prefab has no SlotMachinePanel.");
                var sourceMachine = Require(panelRoot.transform, "Content/ReelMachine");
                var sourceLabel = Require(panelRoot.transform, "Content/SummaryArea/TotalBox/TotalLabel");
                var sourceValue = Require(panelRoot.transform, "Content/SummaryArea/TotalBox/TotalValue");
                var sourceMultiplier = Require(panelRoot.transform, "Content/SummaryArea/TotalBox/TotalMultiplier");
                var sourceStatus = FindDeep(panelRoot.transform, "StatusText") ?? throw new InvalidOperationException("SlotMachinePanel.prefab has no StatusText.");
                var reelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.ReelPrefabPath);
                var reelPrefab = reelAsset != null ? reelAsset.GetComponent<SlotReelViewBase>() : null;
                if (reelPrefab == null) throw new InvalidOperationException("SlotReel.prefab has no SlotReelViewBase.");

                // A temporary canvas gives the dock a 1920×1080 parent so the saved layout values are meaningful.
                canvasObject = new GameObject("TemporaryCanvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var host = new GameObject("Host", typeof(RectTransform)).GetComponent<RectTransform>();
                host.SetParent(canvasObject.transform, false);
                host.anchorMin = host.anchorMax = new Vector2(.5f, .5f);
                host.sizeDelta = new Vector2(1920f, 1080f);

                var root = new GameObject("SlotReelDock", typeof(RectTransform), typeof(CanvasGroup));
                root.layer = LayerMask.NameToLayer("UI");
                root.transform.SetParent(host, false);
                var group = root.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                var presentation = CreateRect("PresentationRoot", root.transform);

                // Reel machine: an independent copy of the panel's, with SlotReel.prefab instances instead of copied reels.
                var machine = (RectTransform)UnityEngine.Object.Instantiate(sourceMachine.gameObject, presentation, false).transform;
                machine.name = "ReelMachine";
                var reelArea = (RectTransform)Require(machine, "ReelArea");
                foreach (var copied in reelArea.GetComponentsInChildren<SlotReelViewBase>(true).ToArray()) UnityEngine.Object.DestroyImmediate(copied.gameObject);
                var views = new List<SlotReelViewBase>();
                for (var i = 0; i < 3; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(reelAsset, reelArea);
                    instance.name = $"SlotReel_{i + 1}";
                    views.Add(instance.GetComponent<SlotReelViewBase>());
                }
                var dividers = Enumerable.Range(1, 8).Select(i => reelArea.Find($"ReelDivider_{i}")).Where(t => t != null).Cast<RectTransform>().ToList();
                if (dividers.Count == 0) throw new InvalidOperationException("The reel machine copy has no ReelDivider_1.");
                ArrangeReelSiblings(views, dividers);
                var frame = Require(machine, "Frame");
                var paylineRoot = (RectTransform)Require(machine, "PaylineFrame");
                var paylineBorder = Require(paylineRoot, "ProceduralBorder").GetComponent<SlotPaylineFrameGraphic>();
                var paylineImage = Require(paylineRoot, "SkinImage").GetComponent<Image>();

                var card = CreateCard(presentation, machine, sourceLabel, sourceValue, sourceMultiplier);

                var status = (RectTransform)UnityEngine.Object.Instantiate(sourceStatus.gameObject, presentation, false).transform;
                status.name = "StatusText";
                Stretch(status);
                status.gameObject.SetActive(false);
                CreateRect("EffectRoot", presentation);

                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.raycastTarget) graphic.raycastTarget = false;

                var dock = root.AddComponent<SlotReelDock>();
                dock.ConfigureForEditor(group, presentation, machine, reelArea, reelArea.GetComponent<SlotReelAreaLayout>(), reelPrefab, views,
                    dividers, paylineRoot, paylineBorder, paylineImage, card, status.GetComponent<TMP_Text>(), panel.SpinProfileForEditor,
                    panel.SkinCatalog);
                dock.RecordSkinDefaultsForEditor(frame.GetComponent<Graphic>(), Require(frame, "LeftEdge").GetComponent<Graphic>(),
                    Require(frame, "RightEdge").GetComponent<Graphic>(), true);
                dock.RefreshLayout();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(reelArea);
                dock.UpdatePaylineFrameForEditor();
                card.Refresh(true);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, DockPrefabPath, out var saved);
                if (!saved || prefab == null) throw new InvalidOperationException($"Could not save {DockPrefabPath}.");
                summary.AppendLine($" created {DockPrefabPath} (3 nested SlotReel instances, reel look copied from SlotMachinePanel.prefab)");
                return prefab;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panelRoot);
                if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        // Reel, divider, reel, ... (unused dividers after them), as SlotReelMachineCore arranges them.
        private static bool ArrangeReelSiblings(IReadOnlyList<SlotReelViewBase> views, IReadOnlyList<RectTransform> dividers)
        {
            var changed = false;
            var index = 0;
            void Place(Transform target)
            {
                if (target.GetSiblingIndex() != index) { target.SetSiblingIndex(index); changed = true; }
                index++;
            }
            for (var i = 0; i < views.Count; i++)
            {
                Place(views[i].transform);
                if (i < views.Count - 1 && i < dividers.Count) Place(dividers[i]);
            }
            for (var i = Math.Max(0, views.Count - 1); i < dividers.Count; i++) Place(dividers[i]);
            return changed;
        }

        // An earlier run of this menu saved the reels after the dividers; the order is fixed in place (the Prefab GUID and
        // the Scene instance are kept). Nothing is written when the order is already right.
        private static void RepairReelOrder(GameObject prefab, StringBuilder summary)
        {
            var dock = prefab.GetComponent<SlotReelDock>();
            if (dock == null || dock.ReelArea == null) return;
            var area = dock.ReelArea;
            var order = string.Concat(Enumerable.Range(0, area.childCount).Select(i => area.GetChild(i))
                .Where(t => t.gameObject.activeSelf).Select(t => t.GetComponent<SlotReelViewBase>() != null ? "R" : "D"));
            if (order == string.Join("D", Enumerable.Repeat("R", dock.ReelCount))) return;
            var root = PrefabUtility.LoadPrefabContents(DockPrefabPath);
            try
            {
                var contents = root.GetComponent<SlotReelDock>();
                if (ArrangeReelSiblings(contents.ReelViews, contents.ReelDividers))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, DockPrefabPath);
                    summary.AppendLine($" fixed the reel/divider order in {DockPrefabPath} (was {order})");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static SlotTotalCardView CreateCard(RectTransform parent, RectTransform machine, Transform sourceLabel, Transform sourceValue,
            Transform sourceMultiplier)
        {
            var cardRect = CreateRect("TotalCard", parent);
            var visual = CreateRect("VisualRoot", cardRect);
            var fill = CreateRect("Fill", visual).gameObject.AddComponent<UiShapeGraphic>();
            var border = CreateRect("Border", visual).gameObject.AddComponent<UiShapeGraphic>();
            border.gameObject.SetActive(false);
            fill.raycastTarget = false;
            border.raycastTarget = false;
            var content = CreateRect("ContentRoot", cardRect);

            var label = CopyText(sourceLabel, content, "TotalLabel", new Vector2(0f, .65f), Vector2.one, .8f, 10f, 40f, false, string.Empty);
            label.text = "합계";
            var value = CopyText(sourceValue, content, "TotalValue", Vector2.zero, new Vector2(1f, .65f), .9f, 14f, 96f, true, "999");
            value.text = "—";

            var multiplier = (RectTransform)UnityEngine.Object.Instantiate(sourceMultiplier.gameObject, content, false).transform;
            multiplier.name = "TotalMultiplier";
            var multiplierText = multiplier.GetComponentInChildren<TMP_Text>(true);
            if (multiplierText == null) throw new InvalidOperationException("TotalMultiplier has no text.");
            multiplierText.color = MultiplierColor;
            multiplierText.text = "×2";
            multiplier.gameObject.SetActive(false);
            var layout = content.gameObject.AddComponent<SlotTotalMultiplierLayout>();
            layout.ConfigureForEditor(value, value.GetComponent<SlotHeightScaledText>(), multiplier, multiplierText);

            var card = cardRect.gameObject.AddComponent<SlotTotalCardView>();
            card.ConfigureForEditor(machine, visual, fill, border, content, label, value, multiplier, multiplierText, layout);
            return card;
        }

        private static TMP_Text CopyText(Transform source, RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float ratio,
            float minimum, float maximum, bool fitWidth, string widthSample)
        {
            var rect = (RectTransform)UnityEngine.Object.Instantiate(source.gameObject, parent, false).transform;
            rect.name = name;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = rect.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            var scaler = rect.GetComponent<SlotHeightScaledText>() ?? rect.gameObject.AddComponent<SlotHeightScaledText>();
            scaler.ConfigureForEditor(ratio, minimum, maximum);
            scaler.ConfigureWidthFitForEditor(fitWidth, widthSample);
            return text;
        }

        // ---------- Scene ----------

        private static void PlaceInScene(GameObject prefab, StringBuilder summary)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            if (roots.SelectMany(root => root.GetComponentsInChildren<SlotReelDock>(true)).Any())
            {
                summary.AppendLine($" kept the SlotReelDock in {ScenePath}; the Scene was not written");
                return;
            }
            var canvas = roots.Select(root => root.GetComponent<Canvas>()).FirstOrDefault(value => value != null && value.name == "Canvas")
                ?? throw new InvalidOperationException($"{ScenePath} has no root Canvas.");
            var overlay = canvas.transform.Find(OverlayLayerName) as RectTransform;
            if (overlay == null)
            {
                overlay = CreateRect(OverlayLayerName, canvas.transform);
                summary.AppendLine($" added Canvas/{OverlayLayerName}");
            }
            overlay.SetAsLastSibling();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, overlay);
            instance.name = prefab.name;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Could not save {ScenePath}.");
            summary.AppendLine($" added {OverlayLayerName}/SlotReelDock (Prefab instance) to {ScenePath} and saved it");
        }

        // ---------- Helpers ----------

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Transform Require(Transform root, string path) =>
            root.Find(path) ?? throw new InvalidOperationException($"'{root.name}/{path}' was not found.");

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
