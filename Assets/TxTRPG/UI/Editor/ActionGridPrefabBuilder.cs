using TMPro;
using UnityEditor;
using TxTRPG.Editor.Common.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class ActionGridPrefabBuilder
    {
        private const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs";
        private const string CellPath = PrefabFolder + "/ActionGridCell.prefab";
        private const string ContextMenuPath = PrefabFolder + "/ActionContextMenu.prefab";
        private const string PanelPath = PrefabFolder + "/ActionGridPanel.prefab";

        [MenuItem(
            TxTRPGEditorMenuPaths.UiPrefabs + "Rebuild Action Grid",
            false,
            TxTRPGEditorMenuPriorities.Rebuild)]
        public static void CreateOrUpdatePrefabs()
        {
            EnsureFolder(PrefabFolder);
            var cell = BuildCellPrefab();
            var menu = BuildContextMenuPrefab();
            BuildPanelPrefab(cell, menu);
            Debug.Log($"Action grid prefabs created at {PrefabFolder}.");
        }

        private static ActionGridCell BuildCellPrefab()
        {
            var root = CreateUiObject("ActionGridCell");
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(96f, 96f);
                var button = root.AddComponent<Button>();

                var border = CreateImage("Border", root.transform, new Color32(35, 40, 51, 255));
                Stretch((RectTransform)border.transform);
                border.raycastTarget = true;
                var borderOutline = border.gameObject.AddComponent<Outline>();
                borderOutline.effectColor = new Color32(74, 82, 99, 255);
                borderOutline.effectDistance = new Vector2(1f, -1f);
                button.targetGraphic = border;

                var contentRoot = CreateUiObject("ContentRoot", root.transform);
                Stretch((RectTransform)contentRoot.transform, 3f, 3f, 3f, 3f);

                var icon = CreateImage("Icon", contentRoot.transform, new Color32(255, 255, 255, 255));
                Stretch((RectTransform)icon.transform, 10f, 10f, 10f, 10f);
                icon.preserveAspect = true;

                var quantity = CreateText("Quantity", contentRoot.transform, 18f, Color.white, TextAlignmentOptions.BottomRight);
                Stretch((RectTransform)quantity.transform, 4f, 3f, 5f, 4f);
                quantity.fontStyle = FontStyles.Bold;

                var cooldown = CreateImage("CooldownOverlay", contentRoot.transform, new Color(0f, 0f, 0f, 0.68f));
                Stretch((RectTransform)cooldown.transform);
                cooldown.type = Image.Type.Filled;
                cooldown.fillMethod = Image.FillMethod.Vertical;
                cooldown.fillOrigin = (int)Image.OriginVertical.Bottom;
                cooldown.fillAmount = 0f;
                cooldown.gameObject.SetActive(false);

                var disabled = CreateImage("StateOverlay", contentRoot.transform, new Color(0.1f, 0.1f, 0.1f, 0.62f));
                Stretch((RectTransform)disabled.transform);
                disabled.gameObject.SetActive(false);

                var selection = CreateImage("SelectionFrame", root.transform, new Color(1f, 1f, 1f, 0.01f));
                Stretch((RectTransform)selection.transform, -2f, -2f, -2f, -2f);
                var outline = selection.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(231, 171, 91, 255);
                outline.effectDistance = new Vector2(3f, -3f);
                selection.gameObject.SetActive(false);

                var shortcut = CreateText("ShortcutLabel", contentRoot.transform, 14f, new Color32(225, 225, 225, 255), TextAlignmentOptions.TopLeft);
                var shortcutRect = (RectTransform)shortcut.transform;
                shortcutRect.anchorMin = new Vector2(0f, 1f);
                shortcutRect.anchorMax = new Vector2(0f, 1f);
                shortcutRect.pivot = new Vector2(0f, 1f);
                shortcutRect.anchoredPosition = new Vector2(5f, -4f);
                shortcutRect.sizeDelta = new Vector2(38f, 22f);

                var empty = CreateImage("EmptySlot", contentRoot.transform, new Color(1f, 1f, 1f, 0.06f));
                Stretch((RectTransform)empty.transform, 14f, 14f, 14f, 14f);
                empty.gameObject.SetActive(false);

                var cell = root.AddComponent<ActionGridCell>();
                var properties = new SerializedObject(cell);
                properties.FindProperty("button").objectReferenceValue = button;
                properties.FindProperty("icon").objectReferenceValue = icon;
                properties.FindProperty("quantity").objectReferenceValue = quantity;
                properties.FindProperty("cooldownOverlay").objectReferenceValue = cooldown;
                properties.FindProperty("disabledOverlay").objectReferenceValue = disabled.gameObject;
                properties.FindProperty("selectionFrame").objectReferenceValue = selection.gameObject;
                properties.FindProperty("shortcutLabel").objectReferenceValue = shortcut;
                properties.FindProperty("emptySlotVisual").objectReferenceValue = empty.gameObject;
                properties.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, CellPath).GetComponent<ActionGridCell>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ActionContextMenu BuildContextMenuPrefab()
        {
            var root = CreateUiObject("ActionContextMenu");
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(220f, 180f);
                var background = root.AddComponent<Image>();
                background.color = new Color32(24, 28, 36, 250);
                var outline = root.AddComponent<Outline>();
                outline.effectColor = new Color32(107, 115, 132, 255);
                outline.effectDistance = new Vector2(1f, -1f);

                var optionList = CreateUiObject("OptionList", root.transform);
                Stretch((RectTransform)optionList.transform, 8f, 8f, 8f, 8f);
                var layout = optionList.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 5f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var template = CreateUiObject("OptionTemplate", optionList.transform);
                var templateLayout = template.AddComponent<LayoutElement>();
                templateLayout.preferredHeight = 36f;
                var templateImage = template.AddComponent<Image>();
                templateImage.color = new Color32(50, 57, 71, 255);
                var templateButton = template.AddComponent<Button>();
                templateButton.targetGraphic = templateImage;
                var label = CreateText("Label", template.transform, 18f, Color.white, TextAlignmentOptions.MidlineLeft);
                Stretch((RectTransform)label.transform, 10f, 2f, 8f, 2f);
                template.SetActive(false);

                var menu = root.AddComponent<ActionContextMenu>();
                var properties = new SerializedObject(menu);
                properties.FindProperty("optionList").objectReferenceValue = optionList.transform;
                properties.FindProperty("optionPrefab").objectReferenceValue = templateButton;
                properties.ApplyModifiedPropertiesWithoutUndo();

                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, ContextMenuPath).GetComponent<ActionContextMenu>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildPanelPrefab(ActionGridCell cellPrefab, ActionContextMenu contextMenuPrefab)
        {
            var root = CreateUiObject("ActionGridPanel");
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(760f, 520f);
                var background = root.AddComponent<Image>();
                background.color = new Color32(15, 18, 24, 242);

                var header = CreateUiObject("Header", root.transform);
                var headerRect = (RectTransform)header.transform;
                headerRect.anchorMin = new Vector2(0f, 1f);
                headerRect.anchorMax = Vector2.one;
                headerRect.pivot = new Vector2(0.5f, 1f);
                headerRect.sizeDelta = new Vector2(0f, 58f);
                headerRect.anchoredPosition = Vector2.zero;
                var title = CreateText("Title", header.transform, 26f, new Color32(235, 230, 218, 255), TextAlignmentOptions.MidlineLeft);
                Stretch((RectTransform)title.transform, 20f, 4f, 220f, 4f);
                title.text = "Actions";
                title.fontStyle = FontStyles.Bold;
                var tabs = CreateUiObject("CategoryTabs", header.transform);
                var tabsRect = (RectTransform)tabs.transform;
                tabsRect.anchorMin = new Vector2(1f, 0f);
                tabsRect.anchorMax = Vector2.one;
                tabsRect.pivot = new Vector2(1f, 0.5f);
                tabsRect.sizeDelta = new Vector2(200f, 42f);
                tabsRect.anchoredPosition = new Vector2(-12f, -29f);
                tabs.AddComponent<HorizontalLayoutGroup>().spacing = 6f;

                var scrollView = CreateUiObject("Scroll View", root.transform);
                Stretch((RectTransform)scrollView.transform, 18f, 18f, 18f, 66f);
                var oppositeScrollbarArea = CreateUiObject("OppositeScrollbarArea", scrollView.transform);
                var oppositeRect = (RectTransform)oppositeScrollbarArea.transform;
                oppositeRect.anchorMin = new Vector2(0f, 0f);
                oppositeRect.anchorMax = new Vector2(0f, 1f);
                oppositeRect.pivot = new Vector2(0f, 0.5f);
                oppositeRect.anchoredPosition = Vector2.zero;
                oppositeRect.sizeDelta = new Vector2(16f, 0f);
                oppositeScrollbarArea.SetActive(false);
                var viewport = CreateUiObject("Viewport", scrollView.transform);
                Stretch((RectTransform)viewport.transform, 0f, 0f, 18f, 0f);
                var viewportImage = viewport.AddComponent<Image>();
                viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
                viewport.AddComponent<RectMask2D>();

                var content = CreateUiObject("Content", viewport.transform);
                var contentRect = (RectTransform)content.transform;
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = Vector2.one;
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.sizeDelta = Vector2.zero;
                var grid = content.AddComponent<ActionGridLayoutGroup>();
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment = TextAnchor.UpperCenter;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 5;
                grid.cellSize = new Vector2(96f, 96f);
                grid.spacing = new Vector2(8f, 8f);
                grid.padding = new RectOffset(8, 8, 8, 8);
                var scrollbar = BuildScrollbar(scrollView.transform);
                var scrollRect = scrollView.AddComponent<ScrollRect>();
                scrollRect.viewport = viewport.transform as RectTransform;
                scrollRect.content = contentRect;
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 28f;

                var scrollbarController = scrollView.AddComponent<ConfigurableScrollbarController>();
                var scrollbarProperties = new SerializedObject(scrollbarController);
                scrollbarProperties.FindProperty("scrollRect").objectReferenceValue = scrollRect;
                scrollbarProperties.FindProperty("viewport").objectReferenceValue = viewport.transform;
                scrollbarProperties.FindProperty("scrollbar").objectReferenceValue = scrollbar;
                scrollbarProperties.FindProperty("oppositeScrollbarArea").objectReferenceValue =
                    oppositeRect;
                scrollbarProperties.FindProperty("background").objectReferenceValue = scrollbar.GetComponent<Image>();
                scrollbarProperties.FindProperty("handle").objectReferenceValue =
                    scrollbar.handleRect.GetComponent<Image>();
                scrollbarProperties.FindProperty("visibility").enumValueIndex =
                    (int)ScrollbarVisibilityMode.Hidden;
                scrollbarProperties.FindProperty("spaceMode").enumValueIndex =
                    (int)ScrollbarSpaceMode.ReserveWhenVisible;
                scrollbarProperties.ApplyModifiedPropertiesWithoutUndo();

                var emptyState = CreateText("EmptyState", root.transform, 22f, new Color32(150, 156, 169, 255), TextAlignmentOptions.Center);
                Stretch((RectTransform)emptyState.transform, 30f, 30f, 30f, 70f);
                emptyState.text = "No actions available";
                emptyState.gameObject.SetActive(false);

                var contextAnchor = CreateUiObject("ContextMenuAnchor", root.transform);
                var anchorRect = (RectTransform)contextAnchor.transform;
                Stretch(anchorRect);
                var menuObject = (GameObject)PrefabUtility.InstantiatePrefab(contextMenuPrefab.gameObject, contextAnchor.transform);
                var menuRect = (RectTransform)menuObject.transform;
                menuRect.anchorMin = new Vector2(0.5f, 0.5f);
                menuRect.anchorMax = new Vector2(0.5f, 0.5f);
                menuRect.pivot = new Vector2(0.5f, 0.5f);
                menuRect.anchoredPosition = Vector2.zero;
                menuRect.sizeDelta = new Vector2(220f, 180f);
                var menuProperties = new SerializedObject(menuObject.GetComponent<ActionContextMenu>());
                menuProperties.FindProperty("placementBounds").objectReferenceValue = anchorRect;
                menuProperties.ApplyModifiedPropertiesWithoutUndo();
                menuObject.SetActive(false);

                var panel = root.AddComponent<ActionGridPanel>();
                var properties = new SerializedObject(panel);
                properties.FindProperty("gridAlignment").enumValueIndex =
                    (int)ActionGridHorizontalAlignment.Center;
                properties.FindProperty("incompleteRowAlignment").enumValueIndex =
                    (int)ActionGridHorizontalAlignment.Left;
                properties.FindProperty("verticalPlacement").enumValueIndex =
                    (int)ActionGridVerticalPlacement.CenterWhenContentFits;
                properties.FindProperty("populationMode").enumValueIndex =
                    (int)ActionGridPopulationMode.FillCapacityWithEmptySlots;
                properties.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
                properties.FindProperty("viewport").objectReferenceValue = viewport.transform;
                properties.FindProperty("content").objectReferenceValue = contentRect;
                properties.FindProperty("gridLayout").objectReferenceValue = grid;
                properties.FindProperty("scrollRect").objectReferenceValue = scrollRect;
                properties.FindProperty("scrollbarController").objectReferenceValue = scrollbarController;
                properties.FindProperty("emptyState").objectReferenceValue = emptyState.gameObject;
                properties.FindProperty("contextMenu").objectReferenceValue = menuObject.GetComponent<ActionContextMenu>();
                properties.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Scrollbar BuildScrollbar(Transform parent)
        {
            var root = CreateUiObject("Scrollbar", parent);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(16f, 0f);
            var background = root.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.08f);
            var area = CreateUiObject("Sliding Area", root.transform);
            Stretch((RectTransform)area.transform, 3f, 3f, 3f, 3f);
            var handle = CreateImage("Handle", area.transform, new Color32(213, 137, 75, 230));
            Stretch((RectTransform)handle.transform);
            var scrollbar = root.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.transform as RectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = CreateUiObject(name, parent);
            var image = gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            float size,
            Color color,
            TextAlignmentOptions alignment)
        {
            var gameObject = CreateUiObject(name, parent);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static void Stretch(
            RectTransform rect,
            float left = 0f,
            float bottom = 0f,
            float right = 0f,
            float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
