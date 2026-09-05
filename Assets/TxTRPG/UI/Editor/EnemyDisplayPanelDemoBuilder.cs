using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    public static class EnemyDisplayPanelDemoBuilder
    {
        public const string DemoFolder = "Assets/TxTRPG/UI/DEMO/EnemyDisplayPanel";
        public const string DemoPrefabPath = DemoFolder + "/EnemyDisplayPanelDemo.prefab";
        private const string DemoTexturePath = DemoFolder + "/DemoEnemyTexture.asset";
        private const string AppearancePath = DemoFolder + "/DemoEnemyAppearance.asset";
        private const string DemoDataPath = DemoFolder + "/EnemyDisplayPanelDemoData.asset";
        private const string BackgroundStylePath = DemoFolder + "/EnemyDisplayBackgroundDemoStyle.asset";

        [MenuItem("Tools/TxT RPG/Rebuild Enemy Display Panel Demo")]
        public static void CreateOrUpdateDemo()
        {
            EnsureFolder(DemoFolder);
            var sprite = CreateOrUpdateDemoSprite();
            var appearance = CreateOrUpdateAppearance(sprite);
            var data = CreateOrUpdateData(appearance);
            var backgroundStyle = CreateOrUpdateBackgroundStyle();
            BuildDemoPrefab(data, appearance, backgroundStyle);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Enemy display demo created at {DemoFolder}.");
        }

        private static Sprite CreateOrUpdateDemoSprite()
        {
            const int width = 256;
            const int height = 320;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DemoTexturePath);
            if (texture == null)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "DemoEnemyTexture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                AssetDatabase.CreateAsset(texture, DemoTexturePath);
            }

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                var ny = y / (float)(height - 1);
                for (var x = 0; x < width; x++)
                {
                    var nx = (x - width * 0.5f) / width;
                    var body = Mathf.Pow(nx / 0.39f, 2f) +
                               Mathf.Pow((ny - 0.36f) / 0.3f, 2f) <= 1f;
                    var crown = ny > 0.48f && ny < 0.66f && Mathf.Abs(nx) < 0.22f;
                    if (!body && !crown)
                    {
                        pixels[y * width + x] = Color.clear;
                        continue;
                    }

                    var highlight = Mathf.Clamp01(0.7f - nx * 1.5f + ny * 0.2f);
                    pixels[y * width + x] = new Color(
                        0.18f + highlight * 0.12f,
                        0.48f + highlight * 0.25f,
                        0.36f + highlight * 0.18f,
                        1f);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);

            var sprite = AssetDatabase.LoadAllAssetsAtPath(DemoTexturePath).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, width, height),
                    new Vector2(0.5f, 0f),
                    100f);
                sprite.name = "DemoEnemy";
                AssetDatabase.AddObjectToAsset(sprite, texture);
            }
            EditorUtility.SetDirty(sprite);
            return sprite;
        }

        private static EnemyAppearanceDefinition CreateOrUpdateAppearance(Sprite sprite)
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyAppearanceDefinition>(AppearancePath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyAppearanceDefinition>();
                AssetDatabase.CreateAsset(definition, AppearancePath);
            }

            var properties = new SerializedObject(definition);
            properties.FindProperty("enemyId").stringValue = "demo-slime";
            properties.FindProperty("fallbackSprite").objectReferenceValue = sprite;
            properties.FindProperty("fallbackSpriteAssetId").stringValue =
                AddressableAssetEditor.RegisterSprite(sprite, "ui/enemies/demo-slime", "Enemy_Demo");
            var framing = properties.FindProperty("fallbackFraming");
            framing.FindPropertyRelative("preset").enumValueIndex = (int)CharacterFramingPreset.WholeArtwork;
            framing.FindPropertyRelative("additionalScale").floatValue = 1f;
            framing.FindPropertyRelative("pixelOffset").vector2Value = Vector2.zero;
            properties.FindProperty("variants").arraySize = 0;
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static EnemyDisplayPanelDemoData CreateOrUpdateData(EnemyAppearanceDefinition appearance)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyDisplayPanelDemoData>(DemoDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyDisplayPanelDemoData>();
                AssetDatabase.CreateAsset(data, DemoDataPath);
            }

            var properties = new SerializedObject(data);
            properties.FindProperty("appearanceDefinition").objectReferenceValue = appearance;
            var enemies = properties.FindProperty("enemies");
            enemies.arraySize = 3;
            ConfigureEnemy(enemies.GetArrayElementAtIndex(0), "demo-slime-a", 0, false, false);
            ConfigureEnemy(enemies.GetArrayElementAtIndex(1), "demo-slime-b", 1, true, true);
            ConfigureEnemy(enemies.GetArrayElementAtIndex(2), "demo-slime-c", 2, false, false);
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void ConfigureEnemy(
            SerializedProperty property,
            string instanceId,
            int formationIndex,
            bool mirrored,
            bool targeted)
        {
            property.FindPropertyRelative("instanceId").stringValue = instanceId;
            property.FindPropertyRelative("enemyId").stringValue = "demo-slime";
            property.FindPropertyRelative("appearanceId").stringValue = string.Empty;
            property.FindPropertyRelative("poseId").stringValue = string.Empty;
            property.FindPropertyRelative("animationId").stringValue = string.Empty;
            property.FindPropertyRelative("formationIndex").intValue = formationIndex;
            property.FindPropertyRelative("mirrored").boolValue = mirrored;
            property.FindPropertyRelative("targeted").boolValue = targeted;
            property.FindPropertyRelative("defeated").boolValue = false;
        }

        private static PanelBackgroundStyle CreateOrUpdateBackgroundStyle()
        {
            var style = AssetDatabase.LoadAssetAtPath<PanelBackgroundStyle>(BackgroundStylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<PanelBackgroundStyle>();
                AssetDatabase.CreateAsset(style, BackgroundStylePath);
            }
            style.Configure(
                null,
                new Color32(31, 20, 27, 255),
                FlexibleLayoutBackgroundScaleMode.Stretch,
                1f,
                FlexibleLayoutBackgroundOverflowMode.ClipToPanel);
            EditorUtility.SetDirty(style);
            return style;
        }

        private static void BuildDemoPrefab(
            EnemyDisplayPanelDemoData data,
            EnemyAppearanceDefinition appearance,
            PanelBackgroundStyle backgroundStyle)
        {
            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                EnemyDisplayPanelPrefabBuilder.PrefabPath);
            if (panelPrefab == null)
            {
                throw new UnityException(
                    $"Enemy display panel prefab was not found at {EnemyDisplayPanelPrefabBuilder.PrefabPath}.");
            }

            var root = new GameObject("EnemyDisplayPanelDemo", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(760f, 520f);
                var panelObject = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, root.transform);
                Stretch((RectTransform)panelObject.transform);
                var panel = panelObject.GetComponent<EnemyDisplayPanel>();
                var backend = panelObject.GetComponentInChildren<Enemy2DDisplayBackend>(true);
                backend.SetAppearanceDefinitions(new[] { appearance });

                var backgroundProperties = new SerializedObject(panel.BackgroundRenderer);
                backgroundProperties.FindProperty("initialStyle").objectReferenceValue = backgroundStyle;
                backgroundProperties.ApplyModifiedPropertiesWithoutUndo();
                panel.ApplyBackground(backgroundStyle);

                Canvas.ForceUpdateCanvases();
                panel.SetEnemies(data.CreatePresentations());

                var loader = root.AddComponent<EnemyDisplayPanelDemoLoader>();
                var loaderProperties = new SerializedObject(loader);
                loaderProperties.FindProperty("target").objectReferenceValue = panel;
                loaderProperties.FindProperty("backend").objectReferenceValue = backend;
                loaderProperties.FindProperty("data").objectReferenceValue = data;
                loaderProperties.ApplyModifiedPropertiesWithoutUndo();
                PanelStartupPrefabUtility.Configure(root, loader, (RectTransform)panelObject.transform);

                PrefabUtility.SaveAsPrefabAsset(root, DemoPrefabPath);
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
