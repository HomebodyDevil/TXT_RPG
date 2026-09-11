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
        public const string GameMenuPrefabPath = "Assets/TxTRPG/UI/Prefabs/GameMenuPanel.prefab";
        public const string GameMenuScreenPrefabPath = "Assets/TxTRPG/UI/Prefabs/GameMenuScreen.prefab";
        public const string GameMenuDemoFolder = "Assets/TxTRPG/UI/DEMO/GameMenuPanel";
        public const string GameMenuDemoPrefabPath = GameMenuDemoFolder + "/GameMenuPanelDemo.prefab";
        public const string ModalWindowPrefabPath = "Assets/TxTRPG/UI/Prefabs/ModalWindowHost.prefab";
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

        public static ItemCatalog CreateOrUpdateContent()
        {
            EnsureFolder(ContentFolder);
            var healing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(HealingItemPath) ?? ScriptableObject.CreateInstance<ItemDefinition>();
            healing.ConfigureForEditor("item.minor_healing_potion", "item.minor_healing_potion.name", "item.minor_healing_potion.icon", ItemEffectKind.Healing, 25);
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
            var error = CreateText("Error", panel, string.Empty, 20, new Vector2(20, 70), new Vector2(-40, 80)); error.color = new Color(1f, .45f, .4f);
            var host = modalRoot.gameObject.AddComponent<ModalWindowHost>(); host.ConfigureForEditor(group, title, loading, error, close);
            var inventory = BuildInventoryPage(panel, catalog);
            var status = BuildMessagePage<StatusGameWindowPage>(panel, GamePageIds.Status, "Status");
            status.ConfigureForEditor(null, status.GetComponentInChildren<TMP_Text>());
            var system = BuildMessagePage<MessageGameWindowPage>(panel, GamePageIds.System, "System settings are not implemented yet.");
            system.ConfigureMessageForEditor(system.GetComponentInChildren<TMP_Text>(), "System settings are not implemented yet. Close this window to return to the game.");
            var service = modalRoot.gameObject.AddComponent<GameWindowService>(); service.ConfigureForEditor(host, new GameWindowPage[] { inventory, status, system });
            modalRoot.gameObject.SetActive(true);
            return service;
        }

        private static InventoryGameWindowPage BuildInventoryPage(RectTransform parent, ItemCatalog catalog)
        {
            var root = CreatePageRoot("InventoryPage", parent); var page = root.gameObject.AddComponent<InventoryGameWindowPage>(); page.ConfigureForEditor(GamePageIds.Inventory);
            var selection = CreateText("Selection", root, string.Empty, 22, new Vector2(24, -90), new Vector2(-48, 140));
            var result = CreateText("Result", root, string.Empty, 18, new Vector2(24, 180), new Vector2(-48, 60));
            var pi = CreateButton("PreviousItem", root, "Previous Item", new Vector2(90, 20), new Vector2(130, 44));
            var ni = CreateButton("NextItem", root, "Next Item", new Vector2(240, 20), new Vector2(130, 44));
            var ps = CreateButton("PreviousSlot", root, "Previous Slot", new Vector2(390, 20), new Vector2(130, 44));
            var ns = CreateButton("NextSlot", root, "Next Slot", new Vector2(540, 20), new Vector2(130, 44));
            var register = CreateButton("Register", root, "Register", new Vector2(140, 82), new Vector2(130, 44));
            var unregister = CreateButton("Unregister", root, "Unregister", new Vector2(300, 82), new Vector2(130, 44));
            var use = CreateButton("Use", root, "Use", new Vector2(460, 82), new Vector2(130, 44));
            page.ConfigureForEditor(null, catalog, selection, result, pi, ni, ps, ns, register, unregister, use);
            return page;
        }

        private static T BuildMessagePage<T>(RectTransform parent, string id, string message) where T : GameWindowPage
        {
            var root = CreatePageRoot(id + "Page", parent); var page = root.gameObject.AddComponent<T>(); page.ConfigureForEditor(id);
            CreateText("Message", root, message, 24, new Vector2(30, -100), new Vector2(-60, 260)); return page;
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
            var inventoryView = CreateMenuButton("Inventory", content, "Inventory");
            var statusView = CreateMenuButton("Status", content, "Status");
            var system = MakeBinding(GamePageIds.System, systemView);
            var inventory = MakeBinding(GamePageIds.Inventory, inventoryView);
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

        private static GameMenuButtonBinding MakeBinding(string id, GameMenuButtonView view)
        {
            var binding = new GameMenuButtonBinding();
            binding.ConfigureForEditor(id, view.Button, true, view);
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
        private static RectTransform CreatePageRoot(string name, RectTransform parent) { var rect = CreateRect(name, parent); Stretch(rect, 16, 70, 16, 70); rect.gameObject.SetActive(false); return rect; }
        private static RectTransform CreateRect(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static TMP_Text CreateText(string name, Transform parent, string value, float size, Vector2 position, Vector2 dimensions)
        { var rect = CreateRect(name, parent); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions; var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; return text; }
        private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 dimensions, bool upperRight = false)
        { var rect = CreateRect(name, parent); rect.anchorMin = rect.anchorMax = upperRight ? Vector2.one : new Vector2(.5f, .5f); rect.pivot = upperRight ? Vector2.one : new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions; var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.2f, .24f, .32f, 1); var button = rect.gameObject.AddComponent<Button>(); var text = CreateText("Label", rect, label, 18, Vector2.zero, Vector2.zero); Stretch((RectTransform)text.transform, 4, 4, 4, 4); return button; }
        private static void EnsureFolder(string path) { var parts = path.Split('/'); var current = parts[0]; for (var i = 1; i < parts.Length; i++) { var next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; } }
    }
}
