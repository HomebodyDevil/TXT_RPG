using System.Linq;
using UnityEditor;
using TxTRPG.Editor.Common.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class CharacterDisplayPanelDemoBuilder
    {
        private const string DemoFolder = "Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel";
        private const string PanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/CharacterDisplayPanel.prefab";
        private const string DemoTexturePath = DemoFolder + "/DemoCharacterTexture.asset";
        private const string AppearancePath = DemoFolder + "/DemoCharacterAppearance.asset";
        private const string DemoDataPath = DemoFolder + "/CharacterDisplayPanelDemoData.asset";
        private const string DemoPrefabPath = DemoFolder + "/CharacterDisplayPanelDemo.prefab";
        private const string BackgroundStylePath = DemoFolder + "/CharacterDisplayBackgroundDemoStyle.asset";

        [MenuItem(
            TxTRPGEditorMenuPaths.UiDemos + "Rebuild Character Display Panel Demo",
            false,
            TxTRPGEditorMenuPriorities.Rebuild)]
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
            Debug.Log($"Character display demo created at {DemoFolder}.");
        }

        private static Sprite CreateOrUpdateDemoSprite()
        {
            const int width = 256;
            const int height = 384;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DemoTexturePath);
            if (texture == null)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "DemoCharacterTexture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                AssetDatabase.CreateAsset(texture, DemoTexturePath);
            }

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                var vertical = y / (float)(height - 1);
                for (var x = 0; x < width; x++)
                {
                    var normalizedX = (x - width * 0.5f) / width;
                    var head = Mathf.Pow(normalizedX / 0.22f, 2f) +
                               Mathf.Pow((vertical - 0.72f) / 0.16f, 2f) <= 1f;
                    var shoulders = vertical < 0.6f &&
                                    Mathf.Abs(normalizedX) < Mathf.Lerp(0.42f, 0.2f, vertical / 0.6f);
                    var silhouette = head || shoulders;
                    var glow = Mathf.Clamp01(1f - Mathf.Abs(normalizedX) * 2.8f) * vertical;
                    pixels[y * width + x] = silhouette
                        ? new Color(0.34f + glow * 0.25f, 0.58f + glow * 0.2f, 0.82f, 1f)
                        : new Color(0f, 0f, 0f, 0f);
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
                sprite.name = "DemoCharacter";
                AssetDatabase.AddObjectToAsset(sprite, texture);
            }

            EditorUtility.SetDirty(sprite);
            return sprite;
        }

        private static CharacterAppearanceDefinition CreateOrUpdateAppearance(Sprite sprite)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterAppearanceDefinition>(AppearancePath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CharacterAppearanceDefinition>();
                AssetDatabase.CreateAsset(definition, AppearancePath);
            }

            var properties = new SerializedObject(definition);
            properties.FindProperty("characterId").stringValue = "demo-character";
            properties.FindProperty("fallbackSprite").objectReferenceValue = sprite;
            properties.FindProperty("fallbackSpriteAssetId").stringValue =
                AddressableAssetEditor.RegisterSprite(
                    sprite,
                    "ui/characters/demo",
                    "Character_Demo");
            var framing = properties.FindProperty("fallbackFraming");
            framing.FindPropertyRelative("preset").enumValueIndex = (int)CharacterFramingPreset.ThighUp;
            framing.FindPropertyRelative("additionalScale").floatValue = 1f;
            framing.FindPropertyRelative("pixelOffset").vector2Value = Vector2.zero;
            properties.FindProperty("variants").arraySize = 0;
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static CharacterDisplayPanelDemoData CreateOrUpdateData(CharacterAppearanceDefinition appearance)
        {
            var data = AssetDatabase.LoadAssetAtPath<CharacterDisplayPanelDemoData>(DemoDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterDisplayPanelDemoData>();
                AssetDatabase.CreateAsset(data, DemoDataPath);
            }

            var properties = new SerializedObject(data);
            properties.FindProperty("appearanceDefinition").objectReferenceValue = appearance;
            properties.FindProperty("characterId").stringValue = "demo-character";
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void BuildDemoPrefab(
            CharacterDisplayPanelDemoData data,
            CharacterAppearanceDefinition appearance,
            PanelBackgroundStyle backgroundStyle)
        {
            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            if (panelPrefab == null)
            {
                throw new UnityException($"Character display panel prefab was not found at {PanelPrefabPath}.");
            }

            var root = new GameObject("CharacterDisplayPanelDemo", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(760f, 520f);
                var panelObject = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, root.transform);
                Stretch((RectTransform)panelObject.transform);
                var panel = panelObject.GetComponent<CharacterDisplayPanel>();
                var backgroundRenderer = panel.BackgroundRenderer;
                var backgroundProperties = new SerializedObject(backgroundRenderer);
                backgroundProperties.FindProperty("initialStyle").objectReferenceValue = backgroundStyle;
                backgroundProperties.ApplyModifiedPropertiesWithoutUndo();
                panel.ApplyBackground(backgroundStyle);
                var view = panelObject.GetComponentInChildren<Character2DView>(true);
                view.SetAppearanceDefinitions(new[] { appearance });

                var presentation = data.ToPresentation();
                var viewProperties = new SerializedObject(view);
                var canvasGroup = (CanvasGroup)viewProperties.FindProperty("canvasGroup").objectReferenceValue;
                Canvas.ForceUpdateCanvases();
                view.UpdatePresentation(presentation);
                canvasGroup.alpha = 1f;

                var loader = root.AddComponent<CharacterDisplayPanelDemoLoader>();
                var loaderProperties = new SerializedObject(loader);
                loaderProperties.FindProperty("target").objectReferenceValue = panel;
                loaderProperties.FindProperty("characterView").objectReferenceValue = view;
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

        private static PanelBackgroundStyle CreateOrUpdateBackgroundStyle()
        {
            var style = AssetDatabase.LoadAssetAtPath<PanelBackgroundStyle>(BackgroundStylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<PanelBackgroundStyle>();
                AssetDatabase.CreateAsset(style, BackgroundStylePath);
            }
            style.Configure(null, new Color32(15, 18, 30, 255),
                FlexibleLayoutBackgroundScaleMode.Stretch, 1f,
                FlexibleLayoutBackgroundOverflowMode.ClipToPanel);
            EditorUtility.SetDirty(style);
            return style;
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
