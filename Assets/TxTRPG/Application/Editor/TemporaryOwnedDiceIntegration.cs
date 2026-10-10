using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Exploration;
using TxTRPG.Application.Players;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.UI;
using TxTRPG.UI.Dice;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // One-time asset seed and selected-Scene integration. Only the generated model labels are synchronized.
    // Excluded from the production Prefab batch: this operation also changes Scene hierarchy and references.
    public static class TemporaryOwnedDiceIntegration
    {
        private const string ScenePath = "Assets/Scenes/TMP_MainScene.unity";
        private const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs/Dice";
        private const string StyleFolder = "Assets/TxTRPG/UI/Styles/Dice";
        private const string CatalogPath = StyleFolder + "/DieShapeCatalog.asset";
        private const string StagePath = PrefabFolder + "/DiceStage.prefab";
        private const string PanelPath = "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab";
        private const int DiceLayer = 30;
        private const float LabelScale = .34f;

        public const string SecondaryD6DefinitionId = TemporaryDiceConfiguration.SecondaryD6Id;
        private const string RetiredD4ShapePath = StyleFolder + "/DiceD4Shape.asset";

        // Rerunnable. Creates missing assets and synchronizes only generator-owned data: model face labels, per-face
        // normals/reading directions/rest poses/collider data of shapes that still use the generated model, the D6
        // alias for the secondary die, catalog cleanup of the retired D4 entry, and the DiceStage camera/floor rig.
        // Developer-owned definition IDs, extra aliases, reading-rule text and replaced models are preserved.
        [MenuItem("Tools/TxT RPG/UI/Dice/Temporary/Create Base Dice Assets")]
        public static void CreateBaseAssets()
        {
            CheckEditor();
            EnsureFolder(PrefabFolder);
            EnsureFolder(StyleFolder);
            EnsureLayer();
            var material = EnsureMaterial("DiceBody", new Color(.24f, .54f, .78f));
            var floorMaterial = EnsureMaterial("DiceFloor", new Color(.1f, .15f, .21f));
            var d6 = EnsureShape("D6", "temporary.d6", Cube(), material, "바닥과 평행하게 위를 향한 사각형 면을 읽습니다.");
            var d8 = EnsureShape("D8", "temporary.d8", Octahedron(), material, "바닥과 평행하게 위를 향한 삼각형 면을 읽습니다.");
            if (d6.EnsureAdditionalDefinitionIdForEditor(SecondaryD6DefinitionId)) EditorUtility.SetDirty(d6);
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DieShapeCatalog>();
                catalog.ConfigureForEditor(new[] { d6, d8 });
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            else
            {
                var retired = AssetDatabase.LoadAssetAtPath<DieShapeDefinition>(RetiredD4ShapePath);
                var kept = catalog.Shapes.Where(shape => shape != null && shape != retired).ToList();
                foreach (var required in new[] { d6, d8 })
                    if (!kept.Contains(required)) kept.Add(required);
                if (!kept.SequenceEqual(catalog.Shapes))
                {
                    catalog.ConfigureForEditor(kept);
                    EditorUtility.SetDirty(catalog);
                }
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StagePath) == null) CreateStagePrefab(floorMaterial);
            else EnsureStageRig(floorMaterial);
            AssetDatabase.SaveAssets();
            ValidateBaseAssets();
            Debug.Log("OWNED_DICE_ASSETS: D6, D8 (with D6 secondary alias) and DiceStage rig ready. Developer-owned values were preserved.");
        }

        [MenuItem("Tools/TxT RPG/UI/Dice/Temporary/Validate Base Dice Assets")]
        public static void ValidateBaseAssetsMenu()
        {
            ValidateBaseAssets();
            Debug.Log("OWNED_DICE_ASSETS_VALIDATE: Catalog shapes, face data, collider proxies and DiceStage rig are valid.");
        }

        [MenuItem("Tools/TxT RPG/UI/Dice/Temporary/Apply Owned Dice To TMP Main Scene")]
        public static void ApplyScene()
        {
            CheckEditor();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved TMP_MainScene in Edit Mode without unsaved changes.");
            ValidateBaseAssets();
            var canvas = scene.GetRootGameObjects().Single(root => root.name == "Canvas").transform;
            var main = canvas.Find("MainScreenViewport/Main_FlexibleLayoutPanel")?.GetComponent<FlexibleLayoutPanel>()
                ?? throw new InvalidOperationException("The existing responsive main panel is missing.");
            var tree = main.ContentRoot.Find("NodeTreePanel") as RectTransform;
            // Any existing OwnedDicePanel means the dice UI was already applied, wherever its layout panels have since
            // been moved or renamed. Only validate; never create a second panel because a panel name was not found.
            if (canvas.GetComponentsInChildren<OwnedDicePanel>(true).Length > 0)
            {
                ValidateScene();
                Debug.Log("OWNED_DICE_APPLY: Already present; saved authoring values were preserved.");
                return;
            }
            if (tree == null || tree.GetComponent<FlexibleLayoutItem>() == null)
                throw new InvalidOperationException("The existing NodeTreePanel and size policy must be present.");
            var service = canvas.GetComponentInChildren<GameWindowService>(true)
                ?? throw new InvalidOperationException("The shared GameWindowService is missing.");
            var combat = canvas.GetComponentInChildren<TemporaryDiceRollMenuController>(true)
                ?? throw new InvalidOperationException("The existing dice command is missing.");
            if (service.HasPage(OwnedDiceSessionBinder.ResultsPageId))
                throw new InvalidOperationException("A result page already exists without an OwnedDicePanel; inspect it first.");
            var responsive = canvas.GetComponentInChildren<ResponsiveMainScreen>(true)
                ?? throw new InvalidOperationException("The responsive main screen is missing.");
            var mainCamera = scene.GetRootGameObjects().Single(root => root.name == "Main Camera").GetComponent<Camera>();
            if (mainCamera == null) throw new InvalidOperationException("The authored main camera is missing.");

            var original = tree.GetComponent<FlexibleLayoutItem>();
            var originalMode = original.SizeMode;
            var originalWeight = original.Weight;
            var originalFixed = original.FixedSize;
            var originalMinimum = original.MinimumSize;
            var originalMaximum = original.MaximumSize;
            var sibling = tree.GetSiblingIndex();
            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            var parent = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, scene);
            Undo.RegisterCreatedObjectUndo(parent, "Add exploration and dice panel");
            Undo.SetTransformParent(parent.transform, main.ContentRoot, "Wrap existing exploration tree");
            parent.name = "ExplorationAndDicePanel";
            parent.transform.SetSiblingIndex(sibling);
            var parentRect = (RectTransform)parent.transform;
            Stretch(parentRect);
            var nested = parent.GetComponent<FlexibleLayoutPanel>();
            nested.SetAxis(FlexibleLayoutAxis.Vertical);
            nested.SetSpacing(8f);
            nested.SetPadding(0, 0, 0, 0);
            nested.UseAuthoredContentOffsets();
            Stretch(nested.ContentRoot);
            parent.transform.Find("BackgroundLayer")?.gameObject.SetActive(false);
            parent.transform.Find("ForegroundLayer")?.gameObject.SetActive(false);
            var parentItem = parent.GetComponent<FlexibleLayoutItem>() ?? Undo.AddComponent<FlexibleLayoutItem>(parent);
            parentItem.Configure(originalMode, originalWeight, originalFixed, originalMinimum, originalMaximum);
            Undo.SetTransformParent(tree, nested.ContentRoot, "Keep existing tree under new parent");
            Stretch(tree);
            original.Configure(FlexibleLayoutSizeMode.Weighted, 2f, originalFixed, 360f, 0f);

            var stagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagePath);
            var stage = (GameObject)PrefabUtility.InstantiatePrefab(stagePrefab, scene);
            Undo.RegisterCreatedObjectUndo(stage, "Add isolated dice stage");
            stage.name = "DiceStage";
            stage.transform.position = new Vector3(10000f, 0f, 0f);
            var stageCamera = stage.transform.Find("Camera").GetComponent<Camera>();
            var models = stage.transform.Find("Models");
            Undo.RecordObject(mainCamera, "Exclude dice layer from main camera");
            mainCamera.cullingMask &= ~(1 << DiceLayer);
            EditorUtility.SetDirty(mainCamera);

            var diceRoot = Rect("OwnedDicePanel", nested.ContentRoot);
            Undo.RegisterCreatedObjectUndo(diceRoot.gameObject, "Add owned dice panel");
            diceRoot.gameObject.AddComponent<FlexibleLayoutItem>().Configure(FlexibleLayoutSizeMode.Weighted,
                1f, 200f, 200f, 0f);
            diceRoot.gameObject.AddComponent<Image>().color = new Color(.05f, .09f, .14f, .96f);
            var title = Rect("Title", diceRoot); Stretch(title, 12, 0, 142, 0);
            title.anchorMin = new Vector2(0, 1); title.anchorMax = new Vector2(1, 1);
            title.pivot = new Vector2(.5f, 1); title.sizeDelta = new Vector2(-154, 40);
            Text(title, "보유 주사위", 18, TextAlignmentOptions.MidlineLeft);
            var results = Button("ResultsButton", diceRoot, "결과 보기");
            var resultRect = (RectTransform)results.transform;
            resultRect.anchorMin = resultRect.anchorMax = new Vector2(1, 1);
            resultRect.pivot = new Vector2(1, 1);
            resultRect.sizeDelta = new Vector2(120, 44);
            resultRect.anchoredPosition = new Vector2(-10, -8);
            var viewport = Rect("DiceViewport", diceRoot); Stretch(viewport, 12, 40, 12, 56);
            var raw = viewport.gameObject.AddComponent<RawImage>();
            raw.color = Color.white; raw.raycastTarget = false;
            var status = Rect("EmptyOrErrorState", diceRoot); Stretch(status, 12, 6, 12, 0);
            status.anchorMin = new Vector2(0, 0); status.anchorMax = new Vector2(1, 0);
            status.pivot = new Vector2(.5f, 0); status.sizeDelta = new Vector2(-24, 34);
            var statusText = Text(status, "보유 주사위를 준비 중입니다.", 13, TextAlignmentOptions.Center);
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            var ownedPanel = Undo.AddComponent<OwnedDicePanel>(diceRoot.gameObject);
            ownedPanel.ConfigureForEditor(raw, statusText, results, stageCamera, models, catalog);

            var pagesRoot = service.Host.ContentContainer.ContentRoot;
            var page = CreateResultPage(pagesRoot, service);
            var allPages = service.Pages.ToList();
            allPages.Add(page);
            Undo.RecordObject(service, "Register dice result page");
            service.ConfigureForEditor(service.Host, allPages);
            EditorUtility.SetDirty(service);
            PrefabUtility.RecordPrefabInstancePropertyModifications(service);
            var binder = Undo.AddComponent<OwnedDiceSessionBinder>(diceRoot.gameObject);
            binder.ConfigureForEditor(ownedPanel, service, page);
            Undo.RecordObject(combat, "Bind dice result presentation");
            combat.ConfigureOwnedDiceForEditor(binder);
            EditorUtility.SetDirty(combat);
            PrefabUtility.RecordPrefabInstancePropertyModifications(combat);

            var responsiveSettings = new SerializedObject(responsive);
            var panels = responsiveSettings.FindProperty("panels");
            var heights = responsiveSettings.FindProperty("narrowHeights");
            if (panels.arraySize != 3 || heights.arraySize != 3 ||
                panels.GetArrayElementAtIndex(0).objectReferenceValue != original)
                throw new InvalidOperationException("ResponsiveMainScreen no longer has the expected tree allocation.");
            panels.GetArrayElementAtIndex(0).objectReferenceValue = parentItem;
            heights.GetArrayElementAtIndex(0).floatValue = Mathf.Max(620f,
                heights.GetArrayElementAtIndex(0).floatValue + 260f);
            responsiveSettings.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(responsive);
            main.Rebuild(); nested.Rebuild();
            EditorSceneManager.MarkSceneDirty(scene);
            ValidateScene();
            Debug.Log("OWNED_DICE_APPLY: Existing tree wrapped, 3D stage and shared result page connected. Scene is unsaved for review.");
        }

        [MenuItem("Tools/TxT RPG/UI/Dice/Temporary/Validate Owned Dice Scene")]
        public static void ValidateScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open TMP_MainScene.");
            var canvas = scene.GetRootGameObjects().Single(root => root.name == "Canvas").transform;
            // The structure is identified by components, not panel names, so renamed or moved layout panels still validate.
            var dicePanels = canvas.GetComponentsInChildren<OwnedDicePanel>(true);
            if (dicePanels.Length != 1)
                throw new InvalidOperationException($"Expected exactly one OwnedDicePanel, found {dicePanels.Length}.");
            var dice = dicePanels[0];
            var contentLayer = dice.transform.parent;
            var nested = contentLayer != null ? contentLayer.GetComponentInParent<FlexibleLayoutPanel>(true) : null;
            if (nested == null || nested.ContentRoot != contentLayer || nested.CurrentAxis != FlexibleLayoutAxis.Vertical)
                throw new InvalidOperationException("OwnedDicePanel must be a direct child of a vertical FlexibleLayoutPanel ContentLayer.");
            var enemy = canvas.GetComponentInChildren<EnemyDisplayPanel>(true);
            if (enemy == null || DirectChildOf(contentLayer, enemy.transform) == null)
                throw new InvalidOperationException("EnemyDisplayPanel must share the OwnedDicePanel ContentLayer.");
            var service = canvas.GetComponentInChildren<GameWindowService>(true);
            var binder = dice.GetComponent<OwnedDiceSessionBinder>();
            if (binder == null || service == null || !service.HasPage(OwnedDiceSessionBinder.ResultsPageId) ||
                dice.CurrentTexture != null)
                throw new InvalidOperationException("Owned dice Scene hierarchy or references are incomplete.");
            var controller = canvas.GetComponentInChildren<TemporaryDiceRollMenuController>(true);
            var assigned = new SerializedObject(controller).FindProperty("ownedDiceBinder").objectReferenceValue;
            if (assigned != binder) throw new InvalidOperationException("The combat command is not bound to owned dice.");
            var action = controller.ActionButtonForEditor;
            if (action == null || action.CommandId != TemporaryDiceRollMenuController.RollAllCommandId || action.Button == null ||
                action.Placement == null || action.Placement.FirstPanel == null || action.Placement.SecondPanel == null ||
                action.Placement.Target != action.Button.transform || !action.TryGetComponent<LayoutElement>(out var actionLayout) || !actionLayout.ignoreLayout ||
                action.GetComponent<FlexibleLayoutItem>() != null || action.transform.parent != action.Placement.SecondPanel.parent)
                throw new InvalidOperationException("The boundary combat action button is missing or its references are incomplete.");
            var menu = canvas.GetComponentInChildren<GameMenuPanel>(true);
            if (menu.Buttons.Any(item => item.ActionKind == GameMenuButtonActionKind.Command && item.CommandId == TemporaryDiceRollMenuController.RollAllCommandId) ||
                menu.Content.Find(TemporaryDiceMenuBuilder.ButtonName) != null)
                throw new InvalidOperationException("The legacy game-menu action button or binding is still present.");
            if (action.Placement.SecondPanel != (RectTransform)dice.transform)
                throw new InvalidOperationException("The boundary combat action button must use OwnedDicePanel as its second panel.");
            Debug.Log($"OWNED_DICE_VALIDATE: Owned panel in {nested.name}, result page, command references, and the boundary action button are present.");
        }

        private static Transform DirectChildOf(Transform parent, Transform descendant)
        {
            for (var current = descendant; current != null; current = current.parent)
                if (current.parent == parent) return current;
            return null;
        }

        [MenuItem("Tools/TxT RPG/UI/Dice/Temporary/Validate Play Flow")]
        public static async void ValidatePlayFlow()
        {
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
                var host = PlayerSessionHost.Instance ?? throw new InvalidOperationException("The player session is missing.");
                var runController = UnityEngine.Object.FindAnyObjectByType<ExplorationRunController>();
                var combatController = UnityEngine.Object.FindAnyObjectByType<TemporaryDiceRollMenuController>();
                var binder = UnityEngine.Object.FindAnyObjectByType<OwnedDiceSessionBinder>();
                var story = UnityEngine.Object.FindAnyObjectByType<StoryTextPanel>();
                var windows = UnityEngine.Object.FindAnyObjectByType<GameWindowService>();
                if (runController == null || combatController == null || binder == null || story == null || windows == null)
                    throw new InvalidOperationException("Runtime dice, exploration, or window components are missing.");
                var run = runController.Run ?? throw new InvalidOperationException("The exploration run is not ready.");
                if (run.Phase != ExplorationPhase.AwaitingChoice)
                    throw new InvalidOperationException("Validation requires a fresh awaiting-choice exploration run.");
                var method = typeof(ExplorationRunController).GetMethod("SelectChoice",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(string), typeof(string) }, null);
                if (method == null) throw new InvalidOperationException("The exploration choice handler is missing.");
                var choice = run.CurrentChoices.FirstOrDefault(item => item.TypeId == ExplorationNodeTypeIds.Combat);
                for (var attempts = 0; choice == null && attempts < 20; attempts++)
                {
                    var recovery = run.CurrentChoices.FirstOrDefault(item => item.TypeId == ExplorationNodeTypeIds.RecoveryUpgrade);
                    if (recovery == null) break;
                    method.Invoke(runController, new object[] { run.CurrentChoiceSetId, recovery.Id });
                    var continueField = typeof(ExplorationRunController).GetField("continueButton",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var continueButton = continueField?.GetValue(runController) as Button;
                    if (continueButton == null) throw new InvalidOperationException("The recovery placeholder cannot continue.");
                    continueButton.onClick.Invoke();
                    choice = run.CurrentChoices.FirstOrDefault(item => item.TypeId == ExplorationNodeTypeIds.Combat);
                }
                if (choice == null) throw new InvalidOperationException("No combat node appeared within 20 choice sets.");
                method.Invoke(runController, new object[] { run.CurrentChoiceSetId, choice.Id });
                if (!combatController.IsReady || combatController.Combat == null)
                    throw new InvalidOperationException("Selecting the combat card did not start combat.");
                var actionButton = combatController.ActionButtonForEditor ?? throw new InvalidOperationException("The boundary action button is not connected.");
                await Task.Yield();
                if (!actionButton.IsShown || !actionButton.IsInteractable)
                    throw new InvalidOperationException($"The boundary action button is not shown and enabled during combat: {actionButton.UnavailableReason}");
                var storyBefore = story.MessageCount;
                var actionCount = host.TemporaryDiceHistory.CurrentTurns.Count;
                if (!combatController.TryExecute(TemporaryDiceRollMenuController.RollAllCommandId))
                    throw new InvalidOperationException("The dice action was not accepted.");
                if (!actionButton.IsShown || actionButton.IsInteractable)
                    throw new InvalidOperationException("The boundary action button must stay visible but disabled during the roll.");
                if (combatController.TryExecute(TemporaryDiceRollMenuController.RollAllCommandId))
                    throw new InvalidOperationException("A second action was accepted during the roll.");
                // Poll every frame: the turn and Story must appear only after the dice stopped and the result hold ended.
                var deadline = EditorApplication.timeSinceStartup + 8d;
                var appliedAt = -1f;
                while (host.IsTemporaryDiceActionPending && EditorApplication.timeSinceStartup < deadline)
                {
                    if (appliedAt < 0f && (host.TemporaryDiceHistory.CurrentTurns.Count != actionCount || story.MessageCount != storyBefore))
                        appliedAt = Time.realtimeSinceStartup;
                    await Task.Yield();
                }
                if (appliedAt < 0f) appliedAt = Time.realtimeSinceStartup;
                if (host.IsTemporaryDiceActionPending) throw new TimeoutException("The accepted action did not complete.");
                if (host.TemporaryDiceHistory.CurrentTurns.Count != actionCount + 1 || story.MessageCount <= storyBefore)
                    throw new InvalidOperationException("The roll did not record exactly one turn and Story output.");
                CheckShownFaces(binder.Panel, host, "physics throw");
                var timeline = binder.Panel.LastRollTimeline;
                var hold = binder.Panel.ThrowSettingsForEditor.ResultRevealDelay;
                if (timeline == null || timeline.SettledAt <= 0f || appliedAt < timeline.SettledAt + hold - 0.02f)
                    throw new InvalidOperationException($"The result was applied {appliedAt - (timeline?.SettledAt ?? 0f):F2}s after the dice stopped (hold {hold:F2}s).");
                Debug.Log($"OWNED_DICE_PLAY_VALIDATION: result applied {appliedAt - timeline.SettledAt:F2}s after the dice stopped " +
                    $"({appliedAt - timeline.StartedAt:F2}s after acceptance), offscreen at swap (any edge)={timeline.HiddenAtSwap}, " +
                    $"recall edges={string.Join(",", timeline.RecallEdges)} ({timeline.RecallResolution}), " +
                    $"throw delay={timeline.ThrowStartedAt - timeline.RecallEndedAt:F2}s");
                if (timeline.UsedThrow && !timeline.HiddenAtSwap)
                    throw new InvalidOperationException($"A participant was visible at the swap: {timeline.SwapDetail}");
                var outcome = binder.Panel.LastThrowOutcome;
                Debug.Log($"OWNED_DICE_PLAY_VALIDATION: throw seed={outcome?.Seed} attempts={outcome?.Attempts} " +
                    $"simulateMs={outcome?.ElapsedMilliseconds:F1} placementFallback={binder.Panel.LastRollUsedPlacementFallback}");
                await Task.Delay(100);
                if (!windows.IsOpen) throw new InvalidOperationException("The automatic result modal did not open.");
                if (combatController.CanExecute(TemporaryDiceRollMenuController.RollAllCommandId, out _))
                    throw new InvalidOperationException("The modal did not block direct action execution.");
                if (actionButton.IsInteractable) throw new InvalidOperationException("The boundary action button stayed enabled while the result modal was open.");
                windows.Close();
                await Task.Delay(100);
                binder.Panel.ResultsButton.onClick.Invoke();
                await Task.Delay(100);
                if (!windows.IsOpen) throw new InvalidOperationException("Manual result reopening failed.");
                windows.Close();
                if (!combatController.Combat.IsComplete)
                {
                    host.TemporaryDicePreferences.AutoShowResults = false;
                    host.TemporaryDicePreferences.ReduceMotion = true;
                    try
                    {
                        var reducedCount = host.TemporaryDiceHistory.CurrentTurns.Count;
                        if (!combatController.TryExecute(TemporaryDiceRollMenuController.RollAllCommandId))
                            throw new InvalidOperationException("The reduced-motion dice action was not accepted.");
                        deadline = EditorApplication.timeSinceStartup + 8d;
                        while (host.IsTemporaryDiceActionPending && EditorApplication.timeSinceStartup < deadline)
                            await Task.Delay(50);
                        if (host.TemporaryDiceHistory.CurrentTurns.Count != reducedCount + 1)
                            throw new InvalidOperationException("The reduced-motion roll did not record exactly one turn.");
                        CheckShownFaces(binder.Panel, host, "reduced motion");
                    }
                    finally
                    {
                        host.TemporaryDicePreferences.ReduceMotion = false;
                        host.TemporaryDicePreferences.AutoShowResults = true;
                    }
                }
                else Debug.LogWarning("OWNED_DICE_PLAY_VALIDATION: combat ended early; reduced-motion and hidden-panel rolls were skipped.");
                if (!combatController.Combat.IsComplete)
                {
                    // Forced timeout: the panel is stopped at the accepted faces before the text fallback and the result.
                    host.TemporaryDicePreferences.AutoShowResults = false;
                    combatController.SetRollTimeoutOverrideForEditor(0.5f);
                    try
                    {
                        var timeoutCount = host.TemporaryDiceHistory.CurrentTurns.Count;
                        if (!combatController.TryExecute(TemporaryDiceRollMenuController.RollAllCommandId))
                            throw new InvalidOperationException("The timeout dice action was not accepted.");
                        deadline = EditorApplication.timeSinceStartup + 8d;
                        var restingWhenApplied = true;
                        while (host.IsTemporaryDiceActionPending && EditorApplication.timeSinceStartup < deadline)
                        {
                            if (host.TemporaryDiceHistory.CurrentTurns.Count != timeoutCount && binder.Panel.IsRolling) restingWhenApplied = false;
                            await Task.Yield();
                        }
                        if (host.IsTemporaryDiceActionPending || host.TemporaryDiceHistory.CurrentTurns.Count != timeoutCount + 1)
                            throw new InvalidOperationException("The timed-out roll did not record exactly one turn.");
                        if (!restingWhenApplied || binder.Panel.IsRolling)
                            throw new InvalidOperationException("The timed-out roll applied its result while the dice were still moving.");
                        CheckShownFaces(binder.Panel, host, "forced timeout");
                        await Task.Delay(300);
                        if (host.TemporaryDiceHistory.CurrentTurns.Count != timeoutCount + 1)
                            throw new InvalidOperationException("The timed-out roll applied its result twice.");
                        CheckShownFaces(binder.Panel, host, "forced timeout after late frames");
                        Debug.Log("OWNED_DICE_PLAY_VALIDATION: forced timeout stopped the dice at the accepted faces before applying once.");
                    }
                    finally
                    {
                        combatController.SetRollTimeoutOverrideForEditor(0f);
                        host.TemporaryDicePreferences.AutoShowResults = true;
                    }
                }
                var secondCount = host.TemporaryDiceHistory.CurrentTurns.Count;
                var runHiddenCheck = !combatController.Combat.IsComplete;
                host.TemporaryDicePreferences.AutoShowResults = false;
                try
                {
                    if (!runHiddenCheck) throw new OperationCanceledException();
                    if (!combatController.TryExecute(TemporaryDiceRollMenuController.RollAllCommandId))
                        throw new InvalidOperationException("The second dice action was not accepted.");
                    binder.Panel.gameObject.SetActive(false);
                    deadline = EditorApplication.timeSinceStartup + 8d;
                    while (host.IsTemporaryDiceActionPending && EditorApplication.timeSinceStartup < deadline)
                        await Task.Delay(50);
                    if (host.IsTemporaryDiceActionPending ||
                        host.TemporaryDiceHistory.CurrentTurns.Count != secondCount + 1)
                        throw new InvalidOperationException("The hidden dice panel prevented the accepted result from completing once.");
                    if (windows.IsOpen) throw new InvalidOperationException("Automatic results ignored the disabled setting.");
                }
                catch (OperationCanceledException) { }
                finally
                {
                    binder.Panel.gameObject.SetActive(true);
                    host.TemporaryDicePreferences.AutoShowResults = true;
                }
                await Task.Delay(100);
                if (runHiddenCheck) CheckShownFaces(binder.Panel, host, "re-enabled after hidden roll");
                binder.Panel.ResultsButton.onClick.Invoke();
                await Task.Delay(100);
                if (!windows.IsOpen) throw new InvalidOperationException("Results were lost while automatic display was off.");
                windows.Close();
                var screen = UnityEngine.Object.FindAnyObjectByType<ResponsiveMainScreen>();
                var viewport = screen?.PageScroll?.viewport;
                if (viewport == null) throw new InvalidOperationException("Responsive page viewport is missing.");
                var oldMin = viewport.anchorMin;
                var oldMax = viewport.anchorMax;
                var oldSize = viewport.sizeDelta;
                var oldPosition = viewport.anchoredPosition;
                try
                {
                    viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
                    viewport.sizeDelta = new Vector2(390f, 844f);
                    viewport.anchoredPosition = Vector2.zero;
                    await Task.Delay(150);
                    var panelHeight = ((RectTransform)binder.Panel.transform).rect.height;
                    var pageHeight = screen.PageScroll.content.rect.height;
                    if (panelHeight < 190f || pageHeight <= viewport.rect.height ||
                        binder.Panel.CurrentTexture == null || binder.Panel.DisplayCount != host.TemporaryDice.Count)
                        throw new InvalidOperationException($"Narrow layout failed: dice={panelHeight:F1}, page={pageHeight:F1}, viewport={viewport.rect.height:F1}.");
                    if (!binder.Panel.AreAllDiceVisibleAndSeparated(out var narrowDetail))
                        throw new InvalidOperationException($"Narrow layout dice placement failed: {narrowDetail}");
                }
                finally
                {
                    viewport.anchorMin = oldMin;
                    viewport.anchorMax = oldMax;
                    viewport.sizeDelta = oldSize;
                    viewport.anchoredPosition = oldPosition;
                }
                await Task.Delay(100);
                binder.Panel.ResultsButton.onClick.Invoke();
                await Task.Delay(100);
                if (!windows.IsOpen) throw new InvalidOperationException("The result window did not reopen for visual inspection.");
                if (!binder.Panel.AreAllDiceVisibleAndSeparated(out var restoredDetail))
                    throw new InvalidOperationException($"Restored layout dice placement failed: {restoredDetail}");
                Debug.Log("OWNED_DICE_PLAY_VALIDATION: physics throw faces, reduced motion, repeat blocked, hidden-panel fallback, auto-off history, Story, result windows, and narrow scroll passed.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"OWNED_DICE_PLAY_VALIDATION_FAILED: {exception}");
            }
        }

        private static void CheckShownFaces(OwnedDicePanel panel, PlayerSessionHost host, string stage)
        {
            var turn = host.TemporaryDiceHistory.CurrentTurns.Last();
            foreach (var roll in turn.Rolls)
                if (!panel.TryGetUpFace(roll.Id, out var face) || face != roll.Result.FaceIndex)
                    throw new InvalidOperationException($"{stage}: {roll.Id} shows face {face + 1}, accepted face {roll.Result.FaceIndex + 1}.");
            if (!panel.AreAllDiceVisibleAndSeparated(out var detail))
                throw new InvalidOperationException($"{stage}: {detail}");
        }

        private static DiceResultsGameWindowPage CreateResultPage(Transform pagesRoot, GameWindowService service)
        {
            var root = Rect("DiceResultsPage", pagesRoot); Stretch(root);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Add dice results page");
            var page = Undo.AddComponent<DiceResultsGameWindowPage>(root.gameObject);
            var scrollRoot = Rect("ResultsScroll", root); Stretch(scrollRoot, 10, 62, 10, 8);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            var viewport = Rect("Viewport", scrollRoot); Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(.055f, .075f, .11f, .95f);
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 240);
            var text = Text(content, "아직 굴림 결과가 없습니다.", 18, TextAlignmentOptions.TopLeft);
            text.margin = new Vector4(12, 12, 12, 12);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            var confirm = Button("ConfirmButton", root, "확인");
            var confirmRect = (RectTransform)confirm.transform;
            confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(.5f, 0);
            confirmRect.pivot = new Vector2(.5f, 0);
            confirmRect.sizeDelta = new Vector2(160, 48);
            confirmRect.anchoredPosition = new Vector2(0, 8);
            page.ConfigureForEditor(OwnedDiceSessionBinder.ResultsPageId, confirm.gameObject, "주사위 결과");
            page.ConfigureForEditor(text, scroll, confirm, service);
            root.gameObject.SetActive(false);
            return page;
        }

        public static void ValidateBaseAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            if (catalog == null || AssetDatabase.LoadAssetAtPath<GameObject>(StagePath) == null)
                throw new InvalidOperationException("The shape catalog and DiceStage must be created first.");
            var problems = new List<string>();
            if (!catalog.TryValidate(out var diagnostics)) problems.AddRange(diagnostics);
            foreach (var (id, count) in new[] { ("temporary.d6", 6), (SecondaryD6DefinitionId, 6), ("temporary.d8", 8) })
                if (catalog.Find(id, count) == null) problems.Add($"No presentable {count}-face shape for '{id}'.");
            foreach (var shape in catalog.Shapes)
                if (shape != null && !TryValidateFaceLayout(shape, out var error)) problems.Add($"{shape.name}: {error}");
            var stage = AssetDatabase.LoadAssetAtPath<GameObject>(StagePath).GetComponent<DiceStageRig>();
            if (stage == null || !stage.IsComplete) problems.Add("DiceStage has no complete DiceStageRig (Camera, Models, Floor).");
            if (problems.Count > 0)
                throw new InvalidOperationException("Base dice assets are invalid:\n" + string.Join("\n", problems));
        }

        // Face index i must be the same face in the shape data, the mesh face order and the Face{i+1} label.
        public static bool TryValidateFaceLayout(DieShapeDefinition shape, out string error)
        {
            error = string.Empty;
            var model = shape.ModelPrefab != null ? shape.ModelPrefab.GetComponent<DieModelView>() : null;
            var filter = shape.ModelPrefab != null ? shape.ModelPrefab.GetComponent<MeshFilter>() : null;
            if (model == null || filter == null || filter.sharedMesh == null) { error = "Model, DieModelView or mesh is missing."; return false; }
            if (model.LabelCount != shape.FaceCount || shape.FaceNormals.Count != shape.FaceCount)
            { error = "Label, face-data and face counts differ."; return false; }
            var mesh = filter.sharedMesh;
            if (mesh.vertexCount % shape.FaceCount != 0) { error = "The mesh does not have one vertex block per face."; return false; }
            var perFace = mesh.vertexCount / shape.FaceCount;
            var meshNormals = mesh.normals;
            for (var i = 0; i < shape.FaceCount; i++)
            {
                var normal = shape.FaceNormals[i];
                if (Vector3.Dot(meshNormals[i * perFace], normal) < .999f) { error = $"Mesh face {i + 1} does not match face data."; return false; }
                var label = model.LabelTransform(i);
                if (label == null || Vector3.Dot(label.localPosition.normalized, normal) < .99f ||
                    Vector3.Dot(label.localRotation * Vector3.up, shape.FaceReadingUps[i]) < .999f)
                { error = $"Face{i + 1} label does not match face data."; return false; }
                if (Vector3.Dot(shape.PoseFor(i) * normal, Vector3.up) < .999f ||
                    Vector3.Dot(shape.PoseFor(i) * shape.FaceReadingUps[i], Vector3.forward) < .999f)
                { error = $"Rest pose {i + 1} does not put the face up with its label readable."; return false; }
            }
            return true;
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            var path = StyleFolder + $"/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No supported lit shader is available.");
            material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static DieShapeDefinition EnsureShape(string name, string id, (Vector3[], int[][]) geometry,
            Material material, string readingRule)
        {
            var meshPath = StyleFolder + $"/Dice{name}Mesh.asset";
            var prefabPath = PrefabFolder + $"/Dice{name}.prefab";
            var shapePath = StyleFolder + $"/Dice{name}Shape.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            var normals = new Vector3[geometry.Item2.Length];
            var centers = new Vector3[normals.Length];
            if (mesh == null)
            {
                mesh = BuildMesh(geometry.Item1, geometry.Item2, normals, centers);
                mesh.name = $"Dice{name}Mesh";
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            else CalculateFaces(geometry.Item1, geometry.Item2, normals, centers);
            var readingUps = normals.Select(normal => LabelRotation(normal) * Vector3.up).ToArray();
            var poses = normals.Select((normal, i) =>
                DieFaceGeometry.RestPose(normal, readingUps[i], Vector3.up, Vector3.forward).eulerAngles).ToArray();
            var restHeight = normals.Select((normal, i) => Vector3.Dot(centers[i], normal)).Min();
            var boundingRadius = geometry.Item1.Max(point => point.magnitude);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var root = new GameObject($"Dice{name}", typeof(MeshFilter), typeof(MeshRenderer), typeof(DieModelView));
                try
                {
                    root.layer = DiceLayer;
                    root.GetComponent<MeshFilter>().sharedMesh = mesh;
                    root.GetComponent<MeshRenderer>().sharedMaterial = material;
                    var labels = new TMP_Text[normals.Length];
                    for (var i = 0; i < labels.Length; i++)
                    {
                        var label = new GameObject($"Face{i + 1}").AddComponent<TextMeshPro>();
                        label.transform.SetParent(root.transform, false);
                        label.gameObject.layer = DiceLayer;
                        label.text = $"{i + 1}";
                        label.font = TMP_Settings.defaultFontAsset;
                        label.fontSize = 11f;
                        label.alignment = TextAlignmentOptions.Center;
                        label.color = Color.white;
                        label.rectTransform.sizeDelta = new Vector2(3, 1.5f);
                        label.transform.localScale = Vector3.one * LabelScale;
                        label.transform.localPosition = centers[i] + normals[i] * .035f;
                        label.transform.localRotation = LabelRotation(normals[i]);
                        labels[i] = label;
                    }
                    root.GetComponent<DieModelView>().ConfigureForEditor(labels);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            else
            {
                // These model Prefabs are generator-owned. Keep their face text facing outward.
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var labels = contents.GetComponentsInChildren<TextMeshPro>(true);
                    if (labels.Length != normals.Length)
                        throw new InvalidOperationException($"{prefabPath} has an unexpected face-label count.");
                    for (var i = 0; i < labels.Length; i++)
                    {
                        var label = contents.transform.Find($"Face{i + 1}")?.GetComponent<TextMeshPro>();
                        if (label == null) throw new InvalidOperationException($"Face{i + 1} is missing from {prefabPath}.");
                        label.fontSize = 11f;
                        label.rectTransform.sizeDelta = new Vector2(3, 1.5f);
                        label.transform.localScale = Vector3.one * LabelScale;
                        label.transform.localPosition = centers[i] + normals[i] * .035f;
                        label.transform.localRotation = LabelRotation(normals[i]);
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            var shape = AssetDatabase.LoadAssetAtPath<DieShapeDefinition>(shapePath);
            if (shape == null)
            {
                shape = ScriptableObject.CreateInstance<DieShapeDefinition>();
                shape.ConfigureForEditor(id, prefab, poses, readingRule);
                shape.ConfigureGeneratedDataForEditor(poses, normals, readingUps, mesh, restHeight, boundingRadius);
                AssetDatabase.CreateAsset(shape, shapePath);
            }
            else if (shape.ModelPrefab == prefab)
            {
                // Face data is generator-owned only while the shape still uses the generated model.
                shape.ConfigureGeneratedDataForEditor(poses, normals, readingUps, mesh, restHeight, boundingRadius);
                EditorUtility.SetDirty(shape);
            }
            else Debug.LogWarning($"OWNED_DICE_ASSETS: {shapePath} uses a developer model; its face data was not regenerated.");
            return shape;
        }

        // Labels face outward; their up vector is the face's reading direction stored in the shape data.
        private static Quaternion LabelRotation(Vector3 normal) =>
            Quaternion.LookRotation(-normal, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > .95f ? Vector3.forward : Vector3.up);

        private static Mesh BuildMesh(Vector3[] points, int[][] polygons, Vector3[] normals, Vector3[] centers)
        {
            CalculateFaces(points, polygons, normals, centers);
            var vertices = new List<Vector3>();
            var meshNormals = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < polygons.Length; i++)
            {
                var polygon = polygons[i];
                var first = vertices.Count;
                var normal = normals[i];
                var winding = Vector3.Dot(Vector3.Cross(points[polygon[1]] - points[polygon[0]],
                    points[polygon[2]] - points[polygon[0]]), normal) > 0f;
                for (var j = 0; j < polygon.Length; j++)
                {
                    vertices.Add(points[polygon[winding ? j : polygon.Length - 1 - j]]);
                    meshNormals.Add(normal);
                }
                for (var j = 1; j < polygon.Length - 1; j++)
                { triangles.Add(first); triangles.Add(first + j); triangles.Add(first + j + 1); }
            }
            var mesh = new Mesh { vertices = vertices.ToArray(), normals = meshNormals.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CalculateFaces(Vector3[] points, int[][] polygons, Vector3[] normals, Vector3[] centers)
        {
            for (var i = 0; i < polygons.Length; i++)
            {
                var polygon = polygons[i];
                var center = Vector3.zero;
                foreach (var index in polygon) center += points[index];
                center /= polygon.Length;
                var normal = Vector3.Cross(points[polygon[1]] - points[polygon[0]],
                    points[polygon[2]] - points[polygon[0]]).normalized;
                if (Vector3.Dot(normal, center) < 0f) normal = -normal;
                normals[i] = normal;
                centers[i] = center;
            }
        }

        private static (Vector3[], int[][]) Cube() =>
            (new[] { new Vector3(-1, -1, -1), new Vector3(1, -1, -1),
                new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
                new Vector3(-1, -1, 1), new Vector3(1, -1, 1),
                new Vector3(1, 1, 1), new Vector3(-1, 1, 1) },
             new[] { new[] { 1, 5, 6, 2 }, new[] { 4, 0, 3, 7 },
                new[] { 3, 2, 6, 7 }, new[] { 4, 5, 1, 0 },
                new[] { 5, 4, 7, 6 }, new[] { 0, 1, 2, 3 } });

        private static (Vector3[], int[][]) Octahedron() =>
            (new[] { new Vector3(0, 1.3f, 0), new Vector3(0, -1.3f, 0),
                new Vector3(1.3f, 0, 0), new Vector3(0, 0, 1.3f),
                new Vector3(-1.3f, 0, 0), new Vector3(0, 0, -1.3f) },
             new[] { new[] { 0, 2, 3 }, new[] { 0, 3, 4 },
                new[] { 0, 4, 5 }, new[] { 0, 5, 2 },
                new[] { 1, 3, 2 }, new[] { 1, 4, 3 },
                new[] { 1, 5, 4 }, new[] { 1, 2, 5 } });

        private static void CreateStagePrefab(Material floorMaterial)
        {
            var root = new GameObject("DiceStage");
            try
            {
                root.layer = DiceLayer;
                var models = new GameObject("Models");
                models.transform.SetParent(root.transform, false);
                models.layer = DiceLayer;
                var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.gameObject.layer = DiceLayer;
                camera.cullingMask = 1 << DiceLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .055f, .085f, 1f);
                camera.enabled = false;
                var light = new GameObject("Light", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.transform.localRotation = Quaternion.Euler(38f, -35f, 0);
                light.gameObject.layer = DiceLayer;
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.cullingMask = 1 << DiceLayer;
                ConfigureStageRig(root, floorMaterial);
                PrefabUtility.SaveAsPrefabAsset(root, StagePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // Generator-owned stage parts: tilted orthographic camera defaults, visible floor quad and the rig references.
        // The panel recomputes camera and floor placement at runtime from the viewport aspect and dice count.
        private static void EnsureStageRig(Material floorMaterial)
        {
            var contents = PrefabUtility.LoadPrefabContents(StagePath);
            try
            {
                ConfigureStageRig(contents, floorMaterial);
                PrefabUtility.SaveAsPrefabAsset(contents, StagePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void ConfigureStageRig(GameObject root, Material floorMaterial)
        {
            var models = root.transform.Find("Models") ?? throw new InvalidOperationException("DiceStage/Models is missing.");
            var camera = root.transform.Find("Camera")?.GetComponent<Camera>() ??
                throw new InvalidOperationException("DiceStage/Camera is missing.");
            models.localPosition = Vector3.zero;
            models.localRotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            camera.transform.localPosition = -(Quaternion.Euler(55f, 0f, 0f) * Vector3.forward) * 60f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 150f;
            var floor = root.transform.Find("Floor");
            if (floor == null)
            {
                floor = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                floor.SetParent(root.transform, false);
            }
            floor.gameObject.layer = DiceLayer;
            floor.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = floor.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = floorMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            floor.localPosition = new Vector3(0f, -.01f, 0f);
            floor.localRotation = Quaternion.Euler(90f, 0f, 0f);
            floor.localScale = new Vector3(20f, 14f, 1f);
            var rig = root.GetComponent<DiceStageRig>() ?? root.AddComponent<DiceStageRig>();
            rig.ConfigureForEditor(camera, models, floor);
        }

        private static void EnsureLayer()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var serialized = new SerializedObject(asset);
            var layer = serialized.FindProperty("layers").GetArrayElementAtIndex(DiceLayer);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != "DicePresentation")
                throw new InvalidOperationException($"Layer {DiceLayer} is already owned by '{layer.stringValue}'.");
            if (layer.stringValue == "DicePresentation") return;
            layer.stringValue = "DicePresentation";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void CheckEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Use Edit Mode after compilation completes.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f,
            float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static TMP_Text Text(RectTransform rect, string value, float size, TextAlignmentOptions alignment)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(string name, Transform parent, string label)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.16f, .28f, .4f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var textRect = Rect("Label", rect);
            Stretch(textRect, 4, 2, 4, 2);
            Text(textRect, label, 17, TextAlignmentOptions.Center);
            return button;
        }
    }
}
