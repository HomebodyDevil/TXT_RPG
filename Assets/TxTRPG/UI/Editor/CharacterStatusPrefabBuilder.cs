using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class CharacterStatusPrefabBuilder
    {
        public const string HealthBarPrefabPath =
            "Assets/TxTRPG/UI/Prefabs/HealthBarPanel.prefab";
        public const string StatusPanelPrefabPath =
            "Assets/TxTRPG/UI/Prefabs/CharacterStatusPanel.prefab";

        public static void CreateOrUpdatePrefabs()
        {
            CreateHealthBarPrefab();
            CreateStatusPanelPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateHealthBarPrefab()
        {
            var root = CreateUiObject("HealthBarPanel");
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(520f, 64f);
                var healthBar = root.AddComponent<HealthBarPanel>();

                var backgroundLayer = CreateUiObject("BackgroundLayer", root.transform);
                Stretch((RectTransform)backgroundLayer.transform);
                var backgroundVisualRoot = CreateUiObject("BackgroundVisualRoot", backgroundLayer.transform);
                Stretch((RectTransform)backgroundVisualRoot.transform);
                var backgroundImage = CreateImage(
                    "Background",
                    backgroundVisualRoot.transform,
                    new Color(0.025f, 0.03f, 0.045f, 0.86f));
                Stretch(backgroundImage.rectTransform);

                var barRoot = CreateUiObject("BarRoot", root.transform);
                var barRootRect = (RectTransform)barRoot.transform;
                var barVisualRoot = CreateUiObject("BarVisualRoot", barRoot.transform);
                Stretch((RectTransform)barVisualRoot.transform);

                var sliderObject = CreateUiObject("Slider", barVisualRoot.transform);
                Stretch((RectTransform)sliderObject.transform);
                var slider = sliderObject.AddComponent<Slider>();
                slider.interactable = false;
                slider.navigation = new Navigation { mode = Navigation.Mode.None };
                slider.transition = Selectable.Transition.None;
                slider.minValue = 0f;
                slider.maxValue = 100f;
                slider.value = 100f;
                slider.wholeNumbers = true;
                slider.direction = Slider.Direction.LeftToRight;

                var sliderBackground = CreateImage(
                    "Background",
                    sliderObject.transform,
                    new Color(0.12f, 0.13f, 0.16f, 1f));
                Stretch(sliderBackground.rectTransform);
                var fillArea = CreateUiObject("Fill Area", sliderObject.transform);
                Stretch((RectTransform)fillArea.transform, 2f);
                var fill = CreateImage(
                    "Fill",
                    fillArea.transform,
                    Color.clear);
                Stretch(fill.rectTransform);
                slider.fillRect = fill.rectTransform;
                var fillVisualRoot = CreateUiObject("FillVisualRoot", fill.transform);
                Stretch((RectTransform)fillVisualRoot.transform);
                var fillImage = CreateImage(
                    "FillImage",
                    fillVisualRoot.transform,
                    new Color(0.24f, 0.78f, 0.42f, 1f));
                Stretch(fillImage.rectTransform);
                slider.targetGraphic = fillImage;

                var barEffect = CreateImage(
                    "BarEffectOverlay",
                    barVisualRoot.transform,
                    Color.clear);
                Stretch(barEffect.rectTransform);
                barEffect.enabled = false;
                var borderVisualRoot = CreateUiObject("BorderVisualRoot", barVisualRoot.transform);
                Stretch((RectTransform)borderVisualRoot.transform);
                var border = CreateImage("Border", borderVisualRoot.transform, Color.clear);
                Stretch(border.rectTransform);
                border.enabled = false;

                var textLayer = CreateUiObject("TextLayer", root.transform);
                Stretch((RectTransform)textLayer.transform);
                var textVisualRoot = CreateUiObject("TextVisualRoot", textLayer.transform);
                Stretch((RectTransform)textVisualRoot.transform, 12f);
                var label = CreateText(
                    "LabelText",
                    textVisualRoot.transform,
                    TextAlignmentOptions.Left);
                SetRect(label.rectTransform, Vector2.zero, new Vector2(0.5f, 1f),
                    Vector2.zero, new Vector2(-4f, 0f));
                var value = CreateText(
                    "ValueText",
                    textVisualRoot.transform,
                    TextAlignmentOptions.Right);
                SetRect(value.rectTransform, new Vector2(0.5f, 0f), Vector2.one,
                    new Vector2(4f, 0f), Vector2.zero);

                var foregroundEffect = CreateImage(
                    "ForegroundEffectLayer",
                    root.transform,
                    Color.clear);
                Stretch(foregroundEffect.rectTransform);
                foregroundEffect.enabled = false;
                var transition = CreateImage(
                    "TransitionOverlay",
                    root.transform,
                    Color.clear);
                Stretch(transition.rectTransform);
                transition.enabled = false;

                var layout = root.AddComponent<HealthBarLayoutController>();
                layout.Configure(
                    (RectTransform)backgroundLayer.transform,
                    barRootRect,
                    HealthBarHorizontalAlignment.Center,
                    HealthBarVerticalAlignment.Middle,
                    HealthBarAxisSizeMode.Stretch,
                    HealthBarAxisSizeMode.Stretch,
                    new Vector2(240f, 24f),
                    new RectOffset(12, 12, 12, 12),
                    Vector2.zero);
                healthBar.Configure(slider, label, value);
                healthBar.ConfigureVisualRoots(
                    (RectTransform)backgroundVisualRoot.transform,
                    (RectTransform)barVisualRoot.transform,
                    (RectTransform)fillVisualRoot.transform,
                    (RectTransform)borderVisualRoot.transform,
                    (RectTransform)textVisualRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(root, HealthBarPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateStatusPanelPrefab()
        {
            var healthBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                HealthBarPrefabPath);
            if (healthBarPrefab == null)
            {
                throw new UnityException(
                    $"Health bar prefab was not created at '{HealthBarPrefabPath}'.");
            }

            var root = CreateUiObject("CharacterStatusPanel");
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(520f, 64f);
                var panel = root.AddComponent<CharacterStatusPanel>();
                var healthBar = (GameObject)PrefabUtility.InstantiatePrefab(
                    healthBarPrefab,
                    root.transform);
                Stretch((RectTransform)healthBar.transform);
                panel.RefreshElements();
                PrefabUtility.SaveAsPrefabAsset(root, StatusPanelPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateUiObject(string name, Transform parent = null)
        {
            var result = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                result.transform.SetParent(parent, false);
            }
            return result;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color)
        {
            var result = CreateUiObject(name, parent);
            var image = result.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            TextAlignmentOptions alignment)
        {
            var result = CreateUiObject(name, parent);
            Stretch((RectTransform)result.transform);
            var text = result.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 18f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
