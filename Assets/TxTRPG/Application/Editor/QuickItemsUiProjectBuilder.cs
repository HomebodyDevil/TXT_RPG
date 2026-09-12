using System;
using System.Linq;
using TMPro;
using TxTRPG.Application.Items;
using TxTRPG.Content.Items;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    public static class QuickItemsUiProjectBuilder
    {
        public const string ScenePath = "Assets/Scenes/TMP_MainScene.unity";
        public const string ContentFolder = "Assets/TxTRPG/Content/Items";
        public const string CatalogPath = ContentFolder + "/ItemCatalog.asset";
        public const string HealingItemPath = ContentFolder + "/MinorHealingPotion.asset";
        public const string BagIconPath = ContentFolder + "/DefaultBagIcon.asset";
        public const string GameMenuPrefabPath = "Assets/TxTRPG/UI/Prefabs/GameMenuPanel.prefab";
        public const string GameMenuScreenPrefabPath = "Assets/TxTRPG/UI/Prefabs/GameMenuScreen.prefab";
        public const string GameMenuDemoFolder = "Assets/TxTRPG/UI/DEMO/GameMenuPanel";
        public const string GameMenuDemoPrefabPath = GameMenuDemoFolder + "/GameMenuPanelDemo.prefab";
        public const string ModalWindowPrefabPath = "Assets/TxTRPG/UI/Prefabs/ModalWindowHost.prefab";
        public const string InventoryWindowPrefabPath = "Assets/TxTRPG/UI/Prefabs/InventoryWindowPage.prefab";
        private const string ActionGridPrefabPath = "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab";
        private const string GeneratedRootName = "QuickItemsNavigationAndWindows";

        [MenuItem("Tools/TxT RPG/Application/Rebuild Quick Items and Game Windows")]
        public static void Rebuild()
        {
            RefuseUnsavedLoadedScene(ScenePath);
            var catalog = CreateOrUpdateContent();
            CreateOrUpdatePrefabs(catalog);
            ConfigureScene(ScenePath, catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Quick-item navigation and modal windows were rebuilt.");
        }

        [MenuItem("Tools/TxT RPG/UI/Prefabs/Rebuild Game Menu and Modal Windows")]
        public static void RebuildPrefabsOnly()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath)
                ?? throw new InvalidOperationException($"Required existing catalog was not found at '{CatalogPath}'. Prefab-only rebuild does not create content.");
            CreateOrUpdatePrefabs(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Game-menu and modal-window prefabs were rebuilt without changing any Scene.");
        }

        public static void IntegrateInventoryIntoMainScene()
        {
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Integrate inventory into main scene");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            else if (scene.isDirty) throw new InvalidOperationException($"Save or revert unsaved changes in '{ScenePath}' before inventory integration.");
            try
            {
                var roots = scene.GetRootGameObjects();
                var menus = roots.SelectMany(root => root.GetComponentsInChildren<GameMenuPanel>(true)).ToArray();
                if (menus.Length != 1) throw new InvalidOperationException($"Expected exactly one GameMenuPanel in '{ScenePath}', found {menus.Length}.");
                var menu = menus[0];
                var inventoryBinding = menu.Buttons.SingleOrDefault(binding => binding != null && binding.PageId == GamePageIds.Inventory);
                if (inventoryBinding?.Button == null) throw new InvalidOperationException("The existing GameMenuPanel has no valid inventory button binding.");

                var grids = roots.SelectMany(root => root.GetComponentsInChildren<QuickItemGridPresenter>(true))
                    .Select(presenter => presenter.GetComponent<ActionGridPanel>()).Where(grid => grid != null).Distinct().ToArray();
                ActionGridPanel quickGrid;
                if (grids.Length == 1) quickGrid = grids[0];
                else if (grids.Length == 0)
                {
                    var candidates = roots.SelectMany(root => root.GetComponentsInChildren<ActionGridPanel>(true))
                        .Where(grid => grid.GetComponentInParent<InventoryGameWindowPage>(true) == null).Distinct().ToArray();
                    if (candidates.Length != 1)
                        throw new InvalidOperationException($"Expected exactly one non-inventory ActionGridPanel, found {candidates.Length}.");
                    quickGrid = candidates[0];
                    var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath)
                        ?? throw new InvalidOperationException($"Required ItemCatalog was not found at '{CatalogPath}'.");
                    var presenter = Undo.AddComponent<QuickItemGridPresenter>(quickGrid.gameObject);
                    presenter.Configure(quickGrid, catalog);
                    EditorUtility.SetDirty(presenter);
                }
                else throw new InvalidOperationException($"Multiple operating quick-item ActionGridPanels were found ({grids.Length}).");

                var services = roots.SelectMany(root => root.GetComponentsInChildren<GameWindowService>(true)).ToArray();
                GameWindowService service;
                if (services.Length == 0)
                {
                    var modalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModalWindowPrefabPath)
                        ?? throw new InvalidOperationException($"Required modal prefab was not found at '{ModalWindowPrefabPath}'.");
                    var canvas = menu.GetComponentInParent<Canvas>()
                        ?? throw new InvalidOperationException("The existing GameMenuPanel is not under a Canvas.");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(modalPrefab, canvas.transform);
                    instance.name = "GameWindowsOverlay";
                    instance.transform.SetAsLastSibling();
                    service = instance.GetComponent<GameWindowService>();
                    Undo.RegisterCreatedObjectUndo(instance, "Add inventory modal overlay");
                }
                else if (services.Length == 1) service = services[0];
                else throw new InvalidOperationException($"Multiple GameWindowService candidates were found ({services.Length}); integration was not changed.");

                if (service == null || service.Host == null || !service.HasPage(GamePageIds.Inventory) || !service.HasPage(GamePageIds.System))
                    throw new InvalidOperationException("The selected GameWindowService does not contain valid inventory and system pages or a modal host.");
                var inventoryPages = service.Pages.Where(page => page != null && page.PageId == GamePageIds.Inventory).ToArray();
                if (inventoryPages.Length != 1 || inventoryPages[0] is not InventoryGameWindowPage)
                    throw new InvalidOperationException("The selected service must contain exactly one current InventoryGameWindowPage.");

                Undo.RecordObject(menu, "Connect inventory menu");
                if (!menu.BindExternalDependencies(service, quickGrid, true))
                    throw new InvalidOperationException("Could not connect the existing GameMenuPanel to the inventory window service.");
                menu.RefreshExecutionState();
                EditorUtility.SetDirty(menu);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException($"Could not save '{ScenePath}'.");
                Debug.Log($"Integrated inventory window without replacing '{GetHierarchyPath(menu.transform)}'. Start Play Mode from Assets/Scenes/AppScene.unity.");
            }
            catch
            {
                if (!opened) Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
            finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        public static void ApplyMainSceneMenuInputAndSettingsModal()
        {
            UpdateSystemPageInPrefab(ModalWindowPrefabPath);
            UpdateSystemPageInPrefab(GameMenuScreenPrefabPath);
            AssetDatabase.SaveAssets();
            IntegrateInventoryIntoMainScene();
        }

        public static void ApplyModalContentContainerAndGridConfiguration()
        {
            RefuseUnsavedLoadedScene(ScenePath);
            UpdateInventoryGridConfigurationPrefab();
            UpdateModalContentContainerPrefab(ModalWindowPrefabPath);
            UpdateModalContentContainerPrefab(GameMenuScreenPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            IntegrateInventoryIntoMainScene();
            ValidateSavedPrefabs();
        }

        public static void ApplyInventoryErrorLayoutAndFallbacks()
        {
            RefuseUnsavedLoadedScene(ScenePath);
            UpdateInventoryGridConfigurationPrefab();
            UpdateModalContentContainerPrefab(ModalWindowPrefabPath);
            UpdateModalContentContainerPrefab(GameMenuScreenPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            IntegrateInventoryIntoMainScene();
            ValidateSavedPrefabs();
        }

        public static void ApplyTypedModalRequestCoordinator()
        {
            RefuseUnsavedLoadedScene(ScenePath);
            UpdateMenuRequestDefinitions(GameMenuPrefabPath);
            UpdateMenuRequestDefinitions(GameMenuScreenPrefabPath);
            ApplyMenuRequestDefinitionsToScene();
            AssetDatabase.SaveAssets();
            ValidateSavedPrefabs();
        }

        private static void UpdateMenuRequestDefinitions(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null) throw new InvalidOperationException($"Could not load '{prefabPath}'.");
            try
            {
                var menu = contents.GetComponentInChildren<GameMenuPanel>(true)
                    ?? throw new InvalidOperationException($"Prefab '{prefabPath}' has no GameMenuPanel.");
                ConfigureMenuRequestDefinitions(menu);
                EditorUtility.SetDirty(menu);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void ApplyMenuRequestDefinitionsToScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            else if (scene.isDirty) throw new InvalidOperationException($"Save or revert unsaved changes in '{ScenePath}' before modal request integration.");
            try
            {
                var menus = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameMenuPanel>(true)).ToArray();
                if (menus.Length != 1) throw new InvalidOperationException($"Expected exactly one GameMenuPanel in '{ScenePath}', found {menus.Length}.");
                Undo.RecordObject(menus[0], "Configure typed modal requests");
                ConfigureMenuRequestDefinitions(menus[0]);
                EditorUtility.SetDirty(menus[0]);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException($"Could not save '{ScenePath}'.");
            }
            finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void ConfigureMenuRequestDefinitions(GameMenuPanel menu)
        {
            foreach (var binding in menu.Buttons.Where(value => value != null))
            {
                var kind = binding.PageId == GamePageIds.Inventory ? ModalContentKind.ItemGrid : ModalContentKind.CustomContent;
                var title = binding.PageId == GamePageIds.Inventory ? "Bag" :
                    binding.PageId == GamePageIds.System ? "System Settings" : string.Empty;
                binding.ConfigureForEditor(binding.PageId, binding.Button, binding.Visible, binding.View, kind, title, true);
            }
            menu.RefreshExecutionState();
        }

        private static void UpdateInventoryGridConfigurationPrefab()
        {
            var contents = PrefabUtility.LoadPrefabContents(InventoryWindowPrefabPath);
            if (contents == null) throw new InvalidOperationException($"Could not load '{InventoryWindowPrefabPath}'.");
            try
            {
                var page = contents.GetComponent<InventoryGameWindowPage>()
                    ?? throw new InvalidOperationException("Inventory prefab has no InventoryGameWindowPage.");
                var pagination = contents.transform.Find("PaginationControls") as RectTransform
                    ?? throw new InvalidOperationException("Inventory prefab has no PaginationControls.");
                var numbers = pagination.Find("PageNumbers") as RectTransform ?? CreateRect("PageNumbers", pagination);
                numbers.anchorMin = numbers.anchorMax = new Vector2(.5f, .5f);
                numbers.pivot = new Vector2(.5f, .5f);
                numbers.anchoredPosition = Vector2.zero;
                numbers.sizeDelta = new Vector2(210, 38);
                var layout = numbers.GetComponent<HorizontalLayoutGroup>() ?? numbers.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 4; layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false; layout.childControlHeight = false;
                layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
                var template = numbers.Find("PageNumberTemplate")?.GetComponent<Button>();
                if (template == null)
                {
                    template = CreateButton("PageNumberTemplate", numbers, "1", Vector2.zero, new Vector2(28, 32));
                    template.gameObject.AddComponent<LayoutElement>().preferredWidth = 28;
                }
                template.gameObject.SetActive(false);
                var serialized = new SerializedObject(page);
                serialized.FindProperty("pageNumbersRoot").objectReferenceValue = numbers;
                serialized.FindProperty("pageNumberButtonPrefab").objectReferenceValue = template;
                serialized.FindProperty("maximumVisiblePageButtons").intValue = Mathf.Max(1,
                    serialized.FindProperty("maximumVisiblePageButtons").intValue);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(page);
                PrefabUtility.SaveAsPrefabAsset(contents, InventoryWindowPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void UpdateModalContentContainerPrefab(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null) throw new InvalidOperationException($"Could not load '{prefabPath}'.");
            try
            {
                var service = contents.GetComponentInChildren<GameWindowService>(true)
                    ?? throw new InvalidOperationException($"Prefab '{prefabPath}' has no GameWindowService.");
                var host = service.Host ?? throw new InvalidOperationException($"Prefab '{prefabPath}' has no ModalWindowHost.");
                var window = host.transform.Find("Window") as RectTransform
                    ?? throw new InvalidOperationException($"Prefab '{prefabPath}' has no Window.");
                var containerRect = window.Find("ContentContainer") as RectTransform ?? CreateRect("ContentContainer", window);
                Stretch(containerRect, 16, 16, 16, 70);
                var pagesRoot = containerRect.Find("Pages") as RectTransform ?? CreateRect("Pages", containerRect);
                Stretch(pagesRoot, 0, 0, 0, 0);
                var overlayRoot = window.Find("OverlayRoot") as RectTransform ?? CreateRect("OverlayRoot", window);
                Stretch(overlayRoot, 16, 16, 16, 70);
                overlayRoot.SetAsLastSibling();

                foreach (var page in service.Pages.Where(value => value != null))
                {
                    if (page.transform.parent == pagesRoot) continue;
                    page.transform.SetParent(pagesRoot, false);
                    Stretch((RectTransform)page.transform, 0, 0, 0, 0);
                }

                var container = containerRect.GetComponent<ModalContentContainer>()
                    ?? containerRect.gameObject.AddComponent<ModalContentContainer>();
                container.ConfigureForEditor(pagesRoot, overlayRoot, new RectOffset());
                var hostData = new SerializedObject(host);
                hostData.FindProperty("contentContainer").objectReferenceValue = container;
                ConfigureErrorState(window, host, hostData);
                hostData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(host);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void ConfigureErrorState(RectTransform window, ModalWindowHost host, SerializedObject hostData)
        {
            var error = hostData.FindProperty("errorText").objectReferenceValue as TMP_Text
                ?? throw new InvalidOperationException("ModalWindowHost has no Error Text reference.");
            var errorRoot = window.Find("ErrorStateRoot") as RectTransform ?? CreateRect("ErrorStateRoot", window);
            Stretch(errorRoot, 36, 84, 36, 84);
            errorRoot.SetAsLastSibling();
            if (error.transform.parent != errorRoot) error.transform.SetParent(errorRoot, false);
            var errorRect = (RectTransform)error.transform;
            Stretch(errorRect, 12, 58, 12, 12);
            errorRect.localRotation = Quaternion.identity;
            errorRect.localScale = Vector3.one;
            error.alignment = TextAlignmentOptions.TopLeft;
            error.textWrappingMode = TextWrappingModes.Normal;
            error.overflowMode = TextOverflowModes.Overflow;
            error.raycastTarget = false;
            var retry = errorRoot.Find("RetryButton")?.GetComponent<Button>();
            if (retry == null) retry = CreateButton("RetryButton", errorRoot, "Retry", new Vector2(0, 10), new Vector2(120, 40));
            var retryRect = (RectTransform)retry.transform;
            retryRect.anchorMin = retryRect.anchorMax = new Vector2(.5f, 0);
            retryRect.pivot = new Vector2(.5f, 0);
            retryRect.anchoredPosition = new Vector2(0, 8);
            errorRoot.gameObject.SetActive(false);
            hostData.FindProperty("errorStateRoot").objectReferenceValue = errorRoot.gameObject;
            hostData.FindProperty("retryButton").objectReferenceValue = retry;
        }

        private static void UpdateSystemPageInPrefab(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
                throw new InvalidOperationException($"Could not load prefab contents at '{prefabPath}'.");
            try
            {
                var service = contents.GetComponentInChildren<GameWindowService>(true)
                    ?? throw new InvalidOperationException($"Prefab '{prefabPath}' has no GameWindowService.");
                var host = service.Host
                    ?? throw new InvalidOperationException($"Prefab '{prefabPath}' has no ModalWindowHost reference.");
                var systemPages = service.Pages.Where(page => page != null && page.PageId == GamePageIds.System).ToArray();
                if (systemPages.Length != 1 || systemPages[0] is not MessageGameWindowPage system)
                    throw new InvalidOperationException($"Prefab '{prefabPath}' must contain exactly one MessageGameWindowPage for system.");
                if (host.CloseButton == null)
                    throw new InvalidOperationException($"Prefab '{prefabPath}' has no modal close button.");

                var message = system.GetComponentInChildren<TMP_Text>(true);
                system.ConfigureForEditor(GamePageIds.System, host.CloseButton.gameObject, "System Settings");
                system.ConfigureMessageForEditor(message, string.Empty);
                if (message != null)
                {
                    message.text = string.Empty;
                    message.raycastTarget = false;
                    message.gameObject.SetActive(false);
                }
                EditorUtility.SetDirty(system);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static string GetHierarchyPath(Transform target)
        {
            var path = target.name;
            while (target.parent != null) { target = target.parent; path = target.name + "/" + path; }
            return path;
        }

        public static ItemCatalog CreateOrUpdateContent()
        {
            EnsureFolder(ContentFolder);
            var healing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(HealingItemPath) ?? ScriptableObject.CreateInstance<ItemDefinition>();
            healing.ConfigureForEditor("item.minor_healing_potion", "item.minor_healing_potion.name", "item.minor_healing_potion.icon", ItemEffectKind.Healing, 25);
            healing.SetCategoryForEditor("consumable");
            if (!AssetDatabase.Contains(healing)) AssetDatabase.CreateAsset(healing, HealingItemPath);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath) ?? ScriptableObject.CreateInstance<ItemCatalog>();
            catalog.ConfigureForEditor(new[] { healing });
            if (!AssetDatabase.Contains(catalog)) AssetDatabase.CreateAsset(catalog, CatalogPath);
            EditorUtility.SetDirty(healing); EditorUtility.SetDirty(catalog);
            return catalog;
        }

        public static void CreateOrUpdatePrefabs(ItemCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            EnsureFolder(GameMenuDemoFolder);
            var temporaryRoot = new GameObject("QuickItemsUiPrefabSource", typeof(RectTransform));
            temporaryRoot.SetActive(false);
            try
            {
                var menu = BuildMenu(temporaryRoot.transform, null, null);
                PrefabUtility.SaveAsPrefabAsset(menu.gameObject, GameMenuPrefabPath);
                PrefabUtility.SaveAsPrefabAsset(menu.gameObject, GameMenuDemoPrefabPath);
                Object.DestroyImmediate(menu.gameObject);
                var inventoryPage = BuildInventoryPage((RectTransform)temporaryRoot.transform, catalog);
                PrefabUtility.SaveAsPrefabAsset(inventoryPage.gameObject, InventoryWindowPrefabPath);
                Object.DestroyImmediate(inventoryPage.gameObject);
                var modal = BuildModal(temporaryRoot.transform, catalog);
                PrefabUtility.SaveAsPrefabAsset(modal.gameObject, ModalWindowPrefabPath);
                Object.DestroyImmediate(modal.gameObject);

                var screen = CreateRect("GameMenuScreen", temporaryRoot.transform);
                Stretch(screen, 0, 0, 0, 0);
                var menuArea = CreateRect("MenuArea", screen);
                menuArea.anchorMin = new Vector2(0, 0); menuArea.anchorMax = new Vector2(1, 0);
                menuArea.pivot = new Vector2(.5f, 0); menuArea.offsetMin = Vector2.zero;
                menuArea.offsetMax = new Vector2(0, 64);
                var menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameMenuPrefabPath);
                var nestedMenu = (GameObject)PrefabUtility.InstantiatePrefab(menuPrefab, menuArea);
                var menuRect = (RectTransform)nestedMenu.transform; Stretch(menuRect, 0, 0, 0, 0);
                var overlay = CreateRect("OverlayRoot", screen); Stretch(overlay, 0, 0, 0, 0); overlay.SetAsLastSibling();
                var screenModal = BuildModal(overlay, catalog);
                var binder = screen.gameObject.AddComponent<GameMenuCompositionBinder>();
                binder.ConfigureForEditor(nestedMenu.GetComponent<GameMenuPanel>(), screenModal, null, overlay);
                PrefabUtility.SaveAsPrefabAsset(screen.gameObject, GameMenuScreenPrefabPath);

                ValidateSavedPrefabs();
            }
            finally { Object.DestroyImmediate(temporaryRoot); }
        }

        public static void ValidateSavedPrefabs()
        {
            var menuAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GameMenuPrefabPath);
            var menu = menuAsset != null ? menuAsset.GetComponent<GameMenuPanel>() : null;
            if (menu == null)
                throw new InvalidOperationException("Saved GameMenuPanel prefab has no GameMenuPanel component.");
            if (!menu.ValidateInternalConfiguration(out var reason))
                throw new InvalidOperationException($"Saved GameMenuPanel prefab is invalid: {reason}");
            if (menu.WindowService != null || menu.QuickItemGrid != null)
                throw new InvalidOperationException("Base GameMenuPanel prefab must not retain external service or grid references.");
            var screenAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GameMenuScreenPrefabPath);
            var binder = screenAsset != null ? screenAsset.GetComponent<GameMenuCompositionBinder>() : null;
            if (binder == null || binder.Menu == null || binder.WindowService == null || binder.OverlayRoot == null)
                throw new InvalidOperationException("Saved GameMenuScreen prefab has incomplete composition references.");
            var inventoryAsset = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryWindowPrefabPath);
            var inventory = inventoryAsset != null ? inventoryAsset.GetComponent<InventoryGameWindowPage>() : null;
            if (inventory == null || inventoryAsset.GetComponentInChildren<ActionGridPanel>(true) == null ||
                inventoryAsset.transform.Find("CategoryTabs") == null || inventoryAsset.transform.Find("PaginationControls/PageNumbers") == null)
                throw new InvalidOperationException("Saved InventoryWindowPage prefab has incomplete required references.");
            var modalAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModalWindowPrefabPath);
            var modalHost = modalAsset != null ? modalAsset.GetComponent<ModalWindowHost>() : null;
            if (modalHost?.ContentContainer == null)
                throw new InvalidOperationException("Saved modal content container reference is missing.");
            if (!modalHost.ContentContainer.ValidateConfiguration(out var containerReason))
                throw new InvalidOperationException($"Saved modal content container is invalid: {containerReason}");
            if (!inventory.ValidateRequiredReferences(out var inventoryReason))
                throw new InvalidOperationException($"Saved inventory page is invalid: {inventoryReason}");
            foreach (var asset in new[] { inventoryAsset, screenAsset, AssetDatabase.LoadAssetAtPath<GameObject>(ModalWindowPrefabPath) })
                if (asset == null || asset.GetComponentsInChildren<Component>(true).Any(component => component == null))
                    throw new InvalidOperationException($"Saved prefab '{asset?.name ?? "<missing>"}' contains a Missing Script.");
        }
        public static void ConfigureScene(string path, ItemCatalog catalog)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            else if (scene.isDirty) throw new InvalidOperationException($"Save '{path}' before rebuilding quick-item UI.");
            try
            {
                var roots = scene.GetRootGameObjects();
                var grid = roots.SelectMany(root => root.GetComponentsInChildren<ActionGridPanel>(true)).FirstOrDefault()
                    ?? throw new InvalidOperationException("TMP_MainScene has no ActionGridPanel.");
                var existing = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == GeneratedRootName);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
                var oldMenu = roots.SelectMany(root => root.GetComponentsInChildren<GameMenuPanel>(true)).FirstOrDefault();
                if (oldMenu != null) Object.DestroyImmediate(oldMenu.gameObject);

                var presenter = grid.GetComponent<QuickItemGridPresenter>() ?? grid.gameObject.AddComponent<QuickItemGridPresenter>();
                presenter.Configure(grid, catalog);

                var canvas = grid.GetComponentInParent<Canvas>() ?? throw new InvalidOperationException("ActionGridPanel is not inside a Canvas.");
                var root = CreateRect(GeneratedRootName, canvas.transform);
                Stretch(root, 0, 0, 0, 0); root.SetAsLastSibling();
                var modal = BuildModal(root, catalog);
                var menu = BuildMenu(grid.transform.parent, grid, modal);
                menu.transform.SetSiblingIndex(grid.transform.GetSiblingIndex() + 1);
                PrefabUtility.SaveAsPrefabAsset(menu.gameObject, GameMenuPrefabPath);
                PrefabUtility.SaveAsPrefabAsset(modal.gameObject, ModalWindowPrefabPath);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException($"Could not save '{path}'.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void RefuseUnsavedLoadedScene(string path)
        {
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException($"Save '{path}' before rebuilding quick-item UI. No asset was changed.");
        }

        private static GameWindowService BuildModal(Transform parent, ItemCatalog catalog)
        {
            var modalRoot = CreateRect("ModalWindowHost", parent); Stretch(modalRoot, 0, 0, 0, 0);
            var backdrop = modalRoot.gameObject.AddComponent<Image>(); backdrop.color = new Color(0, 0, 0, .72f);
            var group = modalRoot.gameObject.AddComponent<CanvasGroup>();
            var panel = CreateRect("Window", modalRoot); panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f); panel.sizeDelta = new Vector2(640, 520);
            panel.gameObject.AddComponent<Image>().color = new Color(.08f, .09f, .12f, .98f);
            var title = CreateText("Title", panel, "Window", 30, new Vector2(20, -18), new Vector2(-80, 48));
            var close = CreateButton("CloseButton", panel, "Close", new Vector2(-20, -18), new Vector2(96, 42), true);
            var loading = CreateText("Loading", panel, "Loading...", 24, Vector2.zero, new Vector2(260, 50)).gameObject;
            var errorRoot = CreateRect("ErrorStateRoot", panel); Stretch(errorRoot, 36, 84, 36, 84); errorRoot.SetAsLastSibling();
            var error = CreateText("Error", errorRoot, string.Empty, 20, Vector2.zero, Vector2.zero);
            Stretch((RectTransform)error.transform, 12, 58, 12, 12); error.alignment = TextAlignmentOptions.TopLeft;
            error.textWrappingMode = TextWrappingModes.Normal; error.overflowMode = TextOverflowModes.Overflow;
            error.raycastTarget = false; error.color = new Color(1f, .45f, .4f);
            var retry = CreateButton("RetryButton", errorRoot, "Retry", new Vector2(0, 8), new Vector2(120, 40));
            var retryRect = (RectTransform)retry.transform; retryRect.anchorMin = retryRect.anchorMax = new Vector2(.5f, 0); retryRect.pivot = new Vector2(.5f, 0);
            errorRoot.gameObject.SetActive(false);
            var contentContainerRect = CreateRect("ContentContainer", panel); Stretch(contentContainerRect, 16, 16, 16, 70);
            var pagesRoot = CreateRect("Pages", contentContainerRect); Stretch(pagesRoot, 0, 0, 0, 0);
            var overlayRoot = CreateRect("OverlayRoot", panel); Stretch(overlayRoot, 16, 16, 16, 70); overlayRoot.SetAsLastSibling();
            var contentContainer = contentContainerRect.gameObject.AddComponent<ModalContentContainer>();
            contentContainer.ConfigureForEditor(pagesRoot, overlayRoot, new RectOffset());
            var host = modalRoot.gameObject.AddComponent<ModalWindowHost>(); host.ConfigureForEditor(group, title, loading, error, close, contentContainer, errorRoot.gameObject, retry);
            var inventoryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryWindowPrefabPath);
            var inventory = inventoryPrefab != null
                ? ((GameObject)PrefabUtility.InstantiatePrefab(inventoryPrefab, pagesRoot)).GetComponent<InventoryGameWindowPage>()
                : BuildInventoryPage(pagesRoot, catalog);
            var status = BuildMessagePage<StatusGameWindowPage>(pagesRoot, GamePageIds.Status, "Status", null, "Status");
            status.ConfigureForEditor(null, status.GetComponentInChildren<TMP_Text>());
            var system = BuildMessagePage<MessageGameWindowPage>(pagesRoot, GamePageIds.System, string.Empty,
                close.gameObject, "System Settings");
            system.ConfigureMessageForEditor(system.GetComponentInChildren<TMP_Text>(), string.Empty);
            var service = modalRoot.gameObject.AddComponent<GameWindowService>(); service.ConfigureForEditor(host, new GameWindowPage[] { inventory, status, system });
            modalRoot.gameObject.SetActive(true);
            return service;
        }

        private static InventoryGameWindowPage BuildInventoryPage(RectTransform parent, ItemCatalog catalog)
        {
            var root = CreatePageRoot("InventoryPage", parent); var page = root.gameObject.AddComponent<InventoryGameWindowPage>(); page.ConfigureForEditor(GamePageIds.Inventory);
            var tabs = CreateRect("CategoryTabs", root);
            tabs.anchorMin = new Vector2(0, 1); tabs.anchorMax = Vector2.one; tabs.pivot = new Vector2(.5f, 1);
            tabs.offsetMin = new Vector2(8, -56); tabs.offsetMax = new Vector2(-8, -8);
            var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>(); tabLayout.spacing = 8; tabLayout.childForceExpandWidth = false;
            var tabPrefab = CreateButton("CategoryTabTemplate", tabs, "Category", Vector2.zero, new Vector2(132, 40));
            tabPrefab.gameObject.AddComponent<LayoutElement>().preferredWidth = 132; tabPrefab.gameObject.SetActive(false);

            var gridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActionGridPrefabPath)
                ?? throw new InvalidOperationException($"Required ActionGrid prefab was not found at '{ActionGridPrefabPath}'.");
            var gridObject = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, root);
            gridObject.name = "ItemGrid"; var gridRect = (RectTransform)gridObject.transform;
            gridRect.anchorMin = Vector2.zero; gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = new Vector2(8, 62); gridRect.offsetMax = new Vector2(-8, -64);
            var grid = gridObject.GetComponent<ActionGridPanel>();
            var empty = CreateText("EmptyState", root, "No items in this category.", 20, Vector2.zero, new Vector2(360, 48));
            var result = CreateText("Result", root, string.Empty, 16, new Vector2(0, -188), new Vector2(420, 34));
            var pagination = CreateRect("PaginationControls", root);
            pagination.anchorMin = pagination.anchorMax = new Vector2(.5f, 0); pagination.pivot = new Vector2(.5f, 0);
            pagination.anchoredPosition = new Vector2(0, 8); pagination.sizeDelta = new Vector2(520, 46);
            var previous = CreateButton("PreviousPage", pagination, "Previous", new Vector2(-210, 23), new Vector2(92, 38));
            var next = CreateButton("NextPage", pagination, "Next", new Vector2(210, 23), new Vector2(92, 38));
            var pageLabel = CreateText("PageNumber", pagination, "1 / 1", 16, new Vector2(140, 23), new Vector2(72, 38));
            var numbers = CreateRect("PageNumbers", pagination); numbers.anchorMin = numbers.anchorMax = new Vector2(.5f, .5f);
            numbers.sizeDelta = new Vector2(210, 38);
            var numbersLayout = numbers.gameObject.AddComponent<HorizontalLayoutGroup>(); numbersLayout.spacing = 4; numbersLayout.childAlignment = TextAnchor.MiddleCenter;
            numbersLayout.childControlWidth = false; numbersLayout.childControlHeight = false; numbersLayout.childForceExpandWidth = false; numbersLayout.childForceExpandHeight = false;
            var numberTemplate = CreateButton("PageNumberTemplate", numbers, "1", Vector2.zero, new Vector2(28, 32));
            numberTemplate.gameObject.AddComponent<LayoutElement>().preferredWidth = 28; numberTemplate.gameObject.SetActive(false);
            page.ConfigureInventoryForEditor(null, catalog, grid, tabs, tabPrefab, empty, result, previous, next, pageLabel, numbers, numberTemplate);
            return page;
        }

        private static T BuildMessagePage<T>(RectTransform parent, string id, string message,
            GameObject initialFocus = null, string displayTitle = null) where T : GameWindowPage
        {
            var root = CreatePageRoot(id + "Page", parent); var page = root.gameObject.AddComponent<T>();
            page.ConfigureForEditor(id, initialFocus, displayTitle);
            var text = CreateText("Message", root, message, 24, new Vector2(30, -100), new Vector2(-60, 260));
            text.raycastTarget = false;
            text.gameObject.SetActive(!string.IsNullOrEmpty(message));
            return page;
        }

        private static GameMenuPanel BuildMenu(Transform parent, ActionGridPanel grid, GameWindowService service)
        {
            var root = CreateRect("GameMenuPanel", parent);
            root.sizeDelta = new Vector2(0, 64);

            var viewport = CreateRect("Viewport", root);
            Stretch(viewport, 0, 0, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, .5f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<GameMenuLayoutGroup>();
            layout.SetViewport(viewport);
            layout.Configure(GameMenuLayoutMode.HorizontalScroll, new Vector2(150, 48),
                new Vector2(12, 12), new RectOffset(12, 12, 8, 8),
                GameMenuHorizontalAlignment.Center, GameMenuVerticalAlignment.Center,
                GameMenuWrapColumnPolicy.AutoFit, 4);

            var systemView = CreateMenuButton("System", content, "System");
            var inventoryView = CreateMenuButton("Inventory", content, "Bag");
            inventoryView.SetIcon(CreateOrUpdateBagIcon());
            inventoryView.ConfigureDisplay(GameMenuButtonDisplayMode.ImageWithLabel, 8f, Color.white);
            var statusView = CreateMenuButton("Status", content, "Status");
            var system = MakeBinding(GamePageIds.System, systemView, ModalContentKind.CustomContent, "System Settings");
            var inventory = MakeBinding(GamePageIds.Inventory, inventoryView, ModalContentKind.ItemGrid, "Bag");
            var status = MakeBinding(GamePageIds.Status, statusView);

            var scrollbarRect = CreateRect("HorizontalScrollbar", root);
            scrollbarRect.anchorMin = new Vector2(0, 0);
            scrollbarRect.anchorMax = new Vector2(1, 0);
            scrollbarRect.pivot = new Vector2(.5f, 0);
            scrollbarRect.offsetMin = new Vector2(12, 0);
            scrollbarRect.offsetMax = new Vector2(-12, 12);
            var scrollbarBackground = scrollbarRect.gameObject.AddComponent<Image>();
            scrollbarBackground.color = new Color(.08f, .09f, .12f, .75f);
            var slidingArea = CreateRect("Sliding Area", scrollbarRect);
            Stretch(slidingArea, 4, 2, 4, 2);
            var handle = CreateRect("Handle", slidingArea);
            Stretch(handle, 0, 0, 0, 0);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(.45f, .55f, .75f, .9f);
            var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbarRect.gameObject.SetActive(false);

            var scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.horizontalScrollbar = scrollbar;
            scrollRect.scrollSensitivity = 30f;

            var menu = root.gameObject.AddComponent<GameMenuPanel>();
            menu.ConfigureForEditor(service, grid, new[] { system, inventory, status },
                viewport, content, scrollRect, layout, scrollbar);
            root.gameObject.AddComponent<FlexibleLayoutItem>();
            return menu;
        }

        private static GameMenuButtonBinding MakeBinding(string id, GameMenuButtonView view,
            ModalContentKind kind = ModalContentKind.CustomContent, string title = null)
        {
            var binding = new GameMenuButtonBinding();
            binding.ConfigureForEditor(id, view.Button, true, view, kind, title, true);
            return binding;
        }

        private static GameMenuButtonView CreateMenuButton(string name, Transform parent, string label)
        {
            var buttonRoot = CreateRect(name, parent);
            buttonRoot.sizeDelta = new Vector2(150, 48);
            var visualRoot = CreateRect("VisualRoot", buttonRoot);
            Stretch(visualRoot, 0, 0, 0, 0);
            var backgroundRect = CreateRect("Background", visualRoot);
            Stretch(backgroundRect, 0, 0, 0, 0);
            var background = backgroundRect.gameObject.AddComponent<Image>();
            background.color = new Color(.2f, .24f, .32f, 1);
            var iconRect = CreateRect("Icon", visualRoot);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, .5f);
            iconRect.pivot = new Vector2(0, .5f);
            iconRect.anchoredPosition = new Vector2(12, 0);
            iconRect.sizeDelta = new Vector2(28, 28);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.gameObject.SetActive(false);
            var text = CreateText("Label", visualRoot, label, 18, Vector2.zero, Vector2.zero);
            Stretch((RectTransform)text.transform, 12, 6, 12, 6);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            var borderRect = CreateRect("Border", visualRoot);
            Stretch(borderRect, 0, 0, 0, 0);
            var border = borderRect.gameObject.AddComponent<Image>();
            border.color = new Color(.65f, .72f, .9f, .35f);
            border.raycastTarget = false;
            var effectOverlay = CreateRect("EffectOverlay", visualRoot);
            Stretch(effectOverlay, 0, 0, 0, 0);
            var button = buttonRoot.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var view = buttonRoot.gameObject.AddComponent<GameMenuButtonView>();
            view.ConfigureForEditor(button, visualRoot, background, icon, text, border, effectOverlay);
            return view;
        }
        private static Sprite CreateOrUpdateBagIcon()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BagIconPath);
            if (texture == null)
            {
                texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "DefaultBagIconTexture", filterMode = FilterMode.Point };
                var clear = new Color32(0, 0, 0, 0); var ink = new Color32(235, 224, 184, 255);
                var pixels = Enumerable.Repeat(clear, 32 * 32).ToArray();
                for (var y = 6; y <= 23; y++) for (var x = 5; x <= 26; x++)
                    if (x is 5 or 26 || y is 6 or 23) pixels[y * 32 + x] = ink;
                for (var y = 23; y <= 27; y++) for (var x = 11; x <= 20; x++)
                    if (x is 11 or 20 || y == 27) pixels[y * 32 + x] = ink;
                texture.SetPixels32(pixels); texture.Apply(); AssetDatabase.CreateAsset(texture, BagIconPath);
                var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);
                sprite.name = "DefaultBagIcon"; AssetDatabase.AddObjectToAsset(sprite, texture); AssetDatabase.ImportAsset(BagIconPath);
                return sprite;
            }
            return AssetDatabase.LoadAllAssetsAtPath(BagIconPath).OfType<Sprite>().FirstOrDefault();
        }
        private static RectTransform CreatePageRoot(string name, RectTransform parent) { var rect = CreateRect(name, parent); Stretch(rect, 0, 0, 0, 0); rect.gameObject.SetActive(false); return rect; }
        private static RectTransform CreateRect(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static TMP_Text CreateText(string name, Transform parent, string value, float size, Vector2 position, Vector2 dimensions)
        { var rect = CreateRect(name, parent); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions; var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; return text; }
        private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 dimensions, bool upperRight = false)
        { var rect = CreateRect(name, parent); rect.anchorMin = rect.anchorMax = upperRight ? Vector2.one : new Vector2(.5f, .5f); rect.pivot = upperRight ? Vector2.one : new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions; var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.2f, .24f, .32f, 1); var button = rect.gameObject.AddComponent<Button>(); var text = CreateText("Label", rect, label, 18, Vector2.zero, Vector2.zero); Stretch((RectTransform)text.transform, 4, 4, 4, 4); return button; }
        private static void EnsureFolder(string path) { var parts = path.Split('/'); var current = parts[0]; for (var i = 1; i < parts.Length; i++) { var next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; } }
    }
}
