using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    public static class ActionGridPanelDemoBuilder
    {
        private const string DemoFolder = "Assets/TxTRPG/UI/DEMO/ActionGridPanel";
        private const string PanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab";
        private const string AtlasPath = DemoFolder + "/DemoActionIcons.asset";
        private const string DataPath = DemoFolder + "/ActionGridPanelDemoData.asset";
        private const string PrefabPath = DemoFolder + "/ActionGridPanelDemo.prefab";

        [MenuItem("Tools/TxT RPG/Rebuild Action Grid Demo")]
        public static void CreateOrUpdateDemo()
        {
            EnsureFolder(DemoFolder);
            var icons = CreateOrUpdateIcons();
            var data = CreateOrUpdateData(icons);
            BuildDemoPrefab(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Action grid demo created at {DemoFolder}.");
        }

        private static Sprite[] CreateOrUpdateIcons()
        {
            const int tileSize = 64;
            const int iconCount = 4;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (texture == null)
            {
                texture = new Texture2D(tileSize * iconCount, tileSize, TextureFormat.RGBA32, false)
                {
                    name = "DemoActionIcons",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                AssetDatabase.CreateAsset(texture, AtlasPath);
            }

            var colors = new[]
            {
                new Color32(194, 68, 73, 255),
                new Color32(74, 133, 212, 255),
                new Color32(208, 161, 70, 255),
                new Color32(116, 87, 183, 255)
            };
            var pixels = new Color32[tileSize * iconCount * tileSize];
            for (var y = 0; y < tileSize; y++)
            {
                for (var x = 0; x < tileSize * iconCount; x++)
                {
                    var tile = x / tileSize;
                    var localX = x % tileSize;
                    var centerDistance = Vector2.Distance(
                        new Vector2(localX, y),
                        new Vector2(tileSize * 0.5f, tileSize * 0.5f));
                    var inside = centerDistance < 24f;
                    pixels[y * tileSize * iconCount + x] = inside
                        ? colors[tile]
                        : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().OrderBy(sprite => sprite.name).ToList();
            while (sprites.Count < iconCount)
            {
                var index = sprites.Count;
                var sprite = Sprite.Create(
                    texture,
                    new Rect(index * tileSize, 0f, tileSize, tileSize),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = $"ActionIcon{index}";
                AssetDatabase.AddObjectToAsset(sprite, texture);
                sprites.Add(sprite);
            }

            return sprites.OrderBy(sprite => sprite.name).Take(iconCount).ToArray();
        }

        private static ActionGridPanelDemoData CreateOrUpdateData(Sprite[] icons)
        {
            var data = AssetDatabase.LoadAssetAtPath<ActionGridPanelDemoData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ActionGridPanelDemoData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            var serialized = new SerializedObject(data);
            var entries = serialized.FindProperty("entries");
            entries.arraySize = 10;
            ConfigureEntry(entries.GetArrayElementAtIndex(0), "item-potion", ActionGridEntryKind.Item, icons[0], "Health Potion", 8, true, 0f, "1");
            ConfigureEntry(entries.GetArrayElementAtIndex(1), "skill-frost", ActionGridEntryKind.Skill, icons[1], "Frost Lance", 0, true, 0.35f, "2");
            ConfigureEntry(entries.GetArrayElementAtIndex(2), "equipment-sword", ActionGridEntryKind.Equipment, icons[2], "Sunsteel Sword", 1, true, 0f, "3");
            ConfigureEntry(entries.GetArrayElementAtIndex(3), "skill-void", ActionGridEntryKind.Skill, icons[3], "Void Step", 0, false, 0f, "4");
            ConfigureEntry(entries.GetArrayElementAtIndex(4), "item-elixir", ActionGridEntryKind.Item, icons[0], "Mana Elixir", 3, true, 0f, "5");
            ConfigureEntry(entries.GetArrayElementAtIndex(5), "skill-guard", ActionGridEntryKind.Skill, icons[1], "Arcane Guard", 0, true, 0.7f, "6");
            ConfigureEntry(entries.GetArrayElementAtIndex(6), "equipment-ring", ActionGridEntryKind.Equipment, icons[2], "Amber Ring", 1, true, 0f, "");
            ConfigureEntry(entries.GetArrayElementAtIndex(7), "quest-seal", ActionGridEntryKind.QuestItem, icons[3], "Ancient Seal", 1, false, 0f, "");
            ConfigureEntry(entries.GetArrayElementAtIndex(8), "item-tonic", ActionGridEntryKind.Item, icons[0], "Iron Tonic", 12, true, 0f, "");
            ConfigureEntry(entries.GetArrayElementAtIndex(9), "skill-flare", ActionGridEntryKind.Skill, icons[2], "Solar Flare", 0, true, 0.15f, "");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void ConfigureEntry(
            SerializedProperty property,
            string id,
            ActionGridEntryKind kind,
            Sprite icon,
            string displayName,
            int quantity,
            bool enabled,
            float cooldown,
            string shortcut)
        {
            property.FindPropertyRelative("id").stringValue = id;
            property.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            property.FindPropertyRelative("icon").objectReferenceValue = icon;
            property.FindPropertyRelative("iconAssetId").stringValue = AddressableAssetEditor.RegisterSprite(
                icon,
                "ui/action-icons/demo-atlas",
                "SharedUI");
            property.FindPropertyRelative("displayName").stringValue = displayName;
            property.FindPropertyRelative("description").stringValue = $"Demo description for {displayName}.";
            property.FindPropertyRelative("quantity").intValue = quantity;
            property.FindPropertyRelative("isEnabled").boolValue = enabled;
            property.FindPropertyRelative("cooldownNormalized").floatValue = cooldown;
            property.FindPropertyRelative("shortcutLabel").stringValue = shortcut;
        }

        private static void BuildDemoPrefab(ActionGridPanelDemoData data)
        {
            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            if (panelPrefab == null)
            {
                throw new UnityException($"Action grid panel prefab was not found at {PanelPrefabPath}.");
            }

            var root = new GameObject("ActionGridPanelDemo", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(760f, 520f);
                var panelObject = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, root.transform);
                Stretch((RectTransform)panelObject.transform);
                var panel = panelObject.GetComponent<ActionGridPanel>();
                var panelProperties = new SerializedObject(panel);
                panelProperties.FindProperty("populationMode").enumValueIndex =
                    (int)ActionGridPopulationMode.FillCapacityWithEmptySlots;
                panelProperties.FindProperty("initialCapacity").intValue = 12;
                panelProperties.FindProperty("capacity").intValue = 12;
                panelProperties.ApplyModifiedPropertiesWithoutUndo();
                panel.SetEntries(data.CreateEntries(), 12);
                panel.Select(1, false);

                var title = panelObject.transform.Find("Header/Title")?.GetComponent<TextMeshProUGUI>();
                if (title != null)
                {
                    title.text = "Inventory & Skills";
                }

                var menu = panelObject.GetComponentInChildren<ActionContextMenu>(true);
                menu.Show(
                    new[]
                    {
                        new ActionMenuOption("use", "Use"),
                        new ActionMenuOption("assign", "Assign Shortcut"),
                        new ActionMenuOption("inspect", "Inspect")
                    },
                    null,
                    panelObject.GetComponentsInChildren<ActionGridCell>(true)[1].transform as RectTransform,
                    null);

                var controller = root.AddComponent<ActionGridPanelDemoController>();
                var properties = new SerializedObject(controller);
                properties.FindProperty("target").objectReferenceValue = panel;
                properties.FindProperty("data").objectReferenceValue = data;
                properties.ApplyModifiedPropertiesWithoutUndo();
                PanelStartupPrefabUtility.Configure(root, controller, (RectTransform)panelObject.transform);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
