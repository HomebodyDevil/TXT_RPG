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

        [MenuItem("Tools/TxT RPG/UI/Dice/Temporary/Create Base Dice Assets")]
        public static void CreateBaseAssets()
        {
            CheckEditor();
            EnsureFolder(PrefabFolder);
            EnsureFolder(StyleFolder);
            EnsureLayer();
            var material = AssetDatabase.LoadAssetAtPath<Material>(StyleFolder + "/DiceBody.mat");
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("No supported lit shader is available.");
                material = new Material(shader) { name = "DiceBody", color = new Color(.24f, .54f, .78f) };
                AssetDatabase.CreateAsset(material, StyleFolder + "/DiceBody.mat");
            }
            var shapes = new[]
            {
                EnsureShape("D4", "temporary.d4", Tetrahedron(), material, "카메라를 향한 삼각형 면의 효과와 수치를 읽습니다."),
                EnsureShape("D6", "temporary.d6", Cube(), material, "위쪽이며 카메라에서 보이는 사각형 면을 읽습니다."),
                EnsureShape("D8", "temporary.d8", Octahedron(), material, "위쪽이며 카메라에서 보이는 삼각형 면을 읽습니다.")
            };
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DieShapeCatalog>();
                catalog.ConfigureForEditor(shapes);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StagePath) == null)
                CreateStagePrefab();
            AssetDatabase.SaveAssets();
            ValidateBaseAssets();
            Debug.Log("OWNED_DICE_ASSETS: D4, D6, D8 and DiceStage assets ready. Existing assets were preserved.");
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
            var existing = main.ContentRoot.Find("ExplorationAndDicePanel");
            if (existing != null)
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
            var main = canvas.Find("MainScreenViewport/Main_FlexibleLayoutPanel")?.GetComponent<FlexibleLayoutPanel>();
            var parent = main?.ContentRoot.Find("ExplorationAndDicePanel");
            var nested = parent?.GetComponent<FlexibleLayoutPanel>();
            var tree = nested?.ContentRoot.Find("NodeTreePanel");
            var dice = nested?.ContentRoot.Find("OwnedDicePanel")?.GetComponent<OwnedDicePanel>();
            var service = canvas.GetComponentInChildren<GameWindowService>(true);
            var binder = dice?.GetComponent<OwnedDiceSessionBinder>();
            if (tree == null || dice == null || binder == null || nested.CurrentAxis != FlexibleLayoutAxis.Vertical ||
                service == null || !service.HasPage(OwnedDiceSessionBinder.ResultsPageId) ||
                dice.CurrentTexture != null)
                throw new InvalidOperationException("Owned dice Scene hierarchy or references are incomplete.");
            var controller = canvas.GetComponentInChildren<TemporaryDiceRollMenuController>(true);
            var assigned = new SerializedObject(controller).FindProperty("ownedDiceBinder").objectReferenceValue;
            if (assigned != binder) throw new InvalidOperationException("The combat command is not bound to owned dice.");
            Debug.Log("OWNED_DICE_VALIDATE: Tree, owned panel, result page, and command references are present.");
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
                var storyBefore = story.MessageCount;
                var actionCount = host.TemporaryDiceHistory.CurrentTurns.Count;
                if (!combatController.TryExecute(TemporaryDiceRollMenuController.RollAllCommandId))
                    throw new InvalidOperationException("The dice action was not accepted.");
                if (combatController.TryExecute(TemporaryDiceRollMenuController.RollAllCommandId))
                    throw new InvalidOperationException("A second action was accepted during the roll.");
                var deadline = EditorApplication.timeSinceStartup + 8d;
                while (host.IsTemporaryDiceActionPending && EditorApplication.timeSinceStartup < deadline)
                    await Task.Delay(50);
                if (host.IsTemporaryDiceActionPending) throw new TimeoutException("The accepted action did not complete.");
                if (host.TemporaryDiceHistory.CurrentTurns.Count != actionCount + 1 || story.MessageCount <= storyBefore)
                    throw new InvalidOperationException("The roll did not record exactly one turn and Story output.");
                await Task.Delay(100);
                if (!windows.IsOpen) throw new InvalidOperationException("The automatic result modal did not open.");
                if (combatController.CanExecute(TemporaryDiceRollMenuController.RollAllCommandId, out _))
                    throw new InvalidOperationException("The modal did not block direct action execution.");
                windows.Close();
                await Task.Delay(100);
                binder.Panel.ResultsButton.onClick.Invoke();
                await Task.Delay(100);
                if (!windows.IsOpen) throw new InvalidOperationException("Manual result reopening failed.");
                windows.Close();
                var secondCount = host.TemporaryDiceHistory.CurrentTurns.Count;
                host.TemporaryDicePreferences.AutoShowResults = false;
                try
                {
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
                finally
                {
                    binder.Panel.gameObject.SetActive(true);
                    host.TemporaryDicePreferences.AutoShowResults = true;
                }
                await Task.Delay(100);
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
                Debug.Log("OWNED_DICE_PLAY_VALIDATION: two accepted turns, repeat blocked, hidden-panel fallback, auto-off history, Story, result windows, and narrow scroll passed.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"OWNED_DICE_PLAY_VALIDATION_FAILED: {exception}");
            }
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

        private static void ValidateBaseAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            if (catalog == null || AssetDatabase.LoadAssetAtPath<GameObject>(StagePath) == null ||
                catalog.Find("temporary.d4", 4) == null || catalog.Find("temporary.d6", 6) == null ||
                catalog.Find("temporary.d8", 8) == null)
                throw new InvalidOperationException("Base D4/D6/D8 shape assets and stage must be created first.");
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
                        label.fontSize = 7f;
                        label.alignment = TextAlignmentOptions.Center;
                        label.color = Color.white;
                        label.rectTransform.sizeDelta = new Vector2(3, 1.5f);
                        label.transform.localScale = Vector3.one * .22f;
                        label.transform.localPosition = centers[i] + normals[i] * .035f;
                        var up = Mathf.Abs(Vector3.Dot(normals[i], Vector3.up)) > .95f ? Vector3.forward : Vector3.up;
                        label.transform.localRotation = Quaternion.LookRotation(-normals[i], up);
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
                        label.fontSize = 7f;
                        label.rectTransform.sizeDelta = new Vector2(3, 1.5f);
                        label.transform.localScale = Vector3.one * .22f;
                        label.transform.localPosition = centers[i] + normals[i] * .035f;
                        var up = Mathf.Abs(Vector3.Dot(normals[i], Vector3.up)) > .95f ? Vector3.forward : Vector3.up;
                        label.transform.localRotation = Quaternion.LookRotation(-normals[i], up);
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            var shape = AssetDatabase.LoadAssetAtPath<DieShapeDefinition>(shapePath);
            if (shape == null)
            {
                shape = ScriptableObject.CreateInstance<DieShapeDefinition>();
                var target = name == "D4" ? new Vector3(0f, .35f, -.94f).normalized :
                    new Vector3(0f, .8f, -.6f).normalized;
                var poses = normals.Select(normal => Quaternion.FromToRotation(normal, target).eulerAngles).ToArray();
                shape.ConfigureForEditor(id, prefab, poses, readingRule);
                AssetDatabase.CreateAsset(shape, shapePath);
            }
            return shape;
        }

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

        private static (Vector3[], int[][]) Tetrahedron() =>
            (new[] { new Vector3(1, 1, 1), new Vector3(-1, -1, 1),
                new Vector3(-1, 1, -1), new Vector3(1, -1, -1) },
             new[] { new[] { 0, 1, 2 }, new[] { 0, 3, 1 }, new[] { 0, 2, 3 }, new[] { 1, 3, 2 } });

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

        private static void CreateStagePrefab()
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
                camera.transform.localPosition = new Vector3(0, 0, -20);
                camera.transform.localRotation = Quaternion.identity;
                camera.gameObject.layer = DiceLayer;
                camera.cullingMask = 1 << DiceLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .055f, .085f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = 3f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 100f;
                camera.enabled = false;
                var light = new GameObject("Light", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.transform.localRotation = Quaternion.Euler(38f, -35f, 0);
                light.gameObject.layer = DiceLayer;
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.cullingMask = 1 << DiceLayer;
                PrefabUtility.SaveAsPrefabAsset(root, StagePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
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
