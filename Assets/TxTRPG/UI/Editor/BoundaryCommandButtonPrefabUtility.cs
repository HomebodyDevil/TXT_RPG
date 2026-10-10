using System;
using TMPro;
using TxTRPG.UI.Shapes;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    // Creates BoundaryCommandButton.prefab only when it is missing. Afterwards the stored Prefab is developer-owned and
    // is never regenerated, so this is not a generated-Prefab batch task (see ProjectPrefabRebuildExclusionProvider).
    public static class BoundaryCommandButtonPrefabUtility
    {
        public const string PrefabPath = "Assets/TxTRPG/UI/Prefabs/BoundaryCommandButton.prefab";
        public const string ButtonName = "ActionButton";
        public static readonly Vector2 DefaultButtonSize = new(72f, 72f);

        public static GameObject EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                if (existing.GetComponent<BoundaryCommandButton>() == null)
                    throw new InvalidOperationException($"{PrefabPath} exists but has no BoundaryCommandButton.");
                return existing;
            }
            var root = Build();
            try { return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) ?? throw new InvalidOperationException($"Could not save {PrefabPath}."); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject Build()
        {
            var root = CreateRect("BoundaryCommandButton", null);
            Stretch(root);
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var placement = root.gameObject.AddComponent<PanelBoundaryPlacement>();
            var command = root.gameObject.AddComponent<BoundaryCommandButton>();

            var buttonRect = CreateRect(ButtonName, root);
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(.5f, .5f);
            buttonRect.sizeDelta = DefaultButtonSize;
            var hitRect = CreateRect("HitArea", buttonRect); Stretch(hitRect);
            var hitArea = hitRect.gameObject.AddComponent<UiShapeHitArea>();
            var visual = CreateRect("VisualRoot", buttonRect); Stretch(visual);
            var group = visual.gameObject.AddComponent<CanvasGroup>();
            var backgroundShape = CreateRect("Background", visual).gameObject.AddComponent<UiShapeGraphic>(); Stretch(backgroundShape.rectTransform);
            var backgroundImage = CreateRect("BackgroundImage", visual).gameObject.AddComponent<Image>(); Stretch(backgroundImage.rectTransform);
            var icon = CreateRect("Icon", visual).gameObject.AddComponent<Image>(); Stretch(icon.rectTransform);
            icon.raycastTarget = false; icon.gameObject.SetActive(false);
            var labelRect = CreateRect("Label", visual); Stretch(labelRect, 8f);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "Action"; label.fontSize = 16f; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            var borderShape = CreateRect("Border", visual).gameObject.AddComponent<UiShapeGraphic>(); Stretch(borderShape.rectTransform);
            var borderImage = CreateRect("BorderImage", visual).gameObject.AddComponent<Image>(); Stretch(borderImage.rectTransform);
            var effect = CreateRect("EffectOverlay", visual); Stretch(effect);

            var button = buttonRect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = backgroundShape;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            var menuView = buttonRect.gameObject.AddComponent<GameMenuButtonView>();
            menuView.ConfigureForEditor(button, visual, backgroundImage, icon, label, borderImage, effect);
            menuView.ConfigureDisplay(GameMenuButtonDisplayMode.ImageOnly, 8f, Color.white);
            var shapedView = buttonRect.gameObject.AddComponent<ShapedButtonView>();
            shapedView.ConfigureForEditor(button, hitArea, visual, group, backgroundShape, backgroundImage, borderShape, borderImage, icon, effect);
            shapedView.ApplyAppearance(ShapedButtonAppearance.Default);

            placement.ConfigureForEditor(null, null, buttonRect);
            command.ConfigureForEditor(placement, buttonRect.gameObject, button, shapedView, null, string.Empty, null);
            // Hidden by default: the owner shows it only while its command is available.
            buttonRect.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
        }
    }
}
