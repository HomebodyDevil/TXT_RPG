using UnityEditor;
using TxTRPG.Editor.Common.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class CharacterDisplayPanelPrefabBuilder
    {
        private const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs";
        private const string PrefabPath = PrefabFolder + "/CharacterDisplayPanel.prefab";

        [MenuItem(
            TxTRPGEditorMenuPaths.UiPrefabs + "Rebuild Character Display Panel",
            false,
            TxTRPGEditorMenuPriorities.Rebuild)]
        public static void CreateOrUpdatePrefab()
        {
            EnsureFolder(PrefabFolder);
            CharacterStatusPrefabBuilder.CreateOrUpdatePrefabs();
            var root = CreateUiObject("CharacterDisplayPanel");
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(760f, 520f);

                var backgroundLayer = CreateUiObject("BackgroundLayer", root.transform);
                Stretch((RectTransform)backgroundLayer.transform);
                backgroundLayer.AddComponent<CanvasGroup>();
                var backgroundViewport = CreateUiObject("BackgroundViewport", backgroundLayer.transform);
                Stretch((RectTransform)backgroundViewport.transform);
                var backgroundMask = backgroundViewport.AddComponent<RectMask2D>();
                var backgroundVisualRoot = CreateUiObject("BackgroundVisualRoot", backgroundViewport.transform);
                Stretch((RectTransform)backgroundVisualRoot.transform);
                var backgroundA = CreateImage("BackgroundA", backgroundVisualRoot.transform);
                var backgroundB = CreateImage("BackgroundB", backgroundVisualRoot.transform);
                var backgroundEffect = CreateImage("BackgroundEffectOverlay", backgroundVisualRoot.transform);
                backgroundB.gameObject.SetActive(false);
                backgroundEffect.gameObject.SetActive(false);
                var backgroundRenderer = backgroundLayer.AddComponent<PanelBackgroundRenderer>();
                var backgroundProperties = new SerializedObject(backgroundRenderer);
                backgroundProperties.FindProperty("visualRoot").objectReferenceValue = backgroundVisualRoot.transform;
                backgroundProperties.FindProperty("backgroundA").objectReferenceValue = backgroundA;
                backgroundProperties.FindProperty("backgroundB").objectReferenceValue = backgroundB;
                backgroundProperties.FindProperty("effectOverlay").objectReferenceValue = backgroundEffect;
                backgroundProperties.FindProperty("clipMask").objectReferenceValue = backgroundMask;
                backgroundProperties.ApplyModifiedPropertiesWithoutUndo();

                var displayRoot = CreateUiObject("DisplayRoot", root.transform);
                Stretch((RectTransform)displayRoot.transform);

                var viewObject = CreateUiObject("Character2DView", displayRoot.transform);
                Stretch((RectTransform)viewObject.transform);
                var viewCanvasGroup = viewObject.AddComponent<CanvasGroup>();

                var frameViewport = CreateUiObject("FrameViewport", viewObject.transform);
                Stretch((RectTransform)frameViewport.transform);
                frameViewport.AddComponent<RectMask2D>();

                var visualRoot = CreateUiObject("VisualRoot", frameViewport.transform);
                Stretch((RectTransform)visualRoot.transform);

                var artworkRoot = CreateUiObject("ArtworkRoot", visualRoot.transform);
                Stretch((RectTransform)artworkRoot.transform);

                var baseImage = CreateImage("BaseImage", artworkRoot.transform);
                var skinOverlay = CreateImage("SkinOverlay", artworkRoot.transform);
                var effectOverlay = CreateImage("EffectOverlay", artworkRoot.transform);

                var effectPlayer = viewObject.AddComponent<CharacterEffectPlayer>();
                var view = viewObject.AddComponent<Character2DView>();
                var viewProperties = new SerializedObject(view);
                viewProperties.FindProperty("canvasGroup").objectReferenceValue = viewCanvasGroup;
                viewProperties.FindProperty("animateVisibility").boolValue = false;
                viewProperties.FindProperty("frameViewport").objectReferenceValue = frameViewport.transform;
                viewProperties.FindProperty("visualRoot").objectReferenceValue = visualRoot.transform;
                viewProperties.FindProperty("artworkRoot").objectReferenceValue = artworkRoot.transform;
                viewProperties.FindProperty("baseImage").objectReferenceValue = baseImage;
                viewProperties.FindProperty("skinOverlay").objectReferenceValue = skinOverlay;
                viewProperties.FindProperty("effectOverlay").objectReferenceValue = effectOverlay;
                viewProperties.FindProperty("effectPlayer").objectReferenceValue = effectPlayer;
                viewProperties.ApplyModifiedPropertiesWithoutUndo();

                var foregroundEffectLayer = CreateImage("ForegroundEffectLayer", root.transform);
                foregroundEffectLayer.color = Color.clear;
                foregroundEffectLayer.enabled = false;

                var transitionOverlay = CreateImage("TransitionOverlay", root.transform);
                transitionOverlay.color = Color.clear;
                transitionOverlay.enabled = false;

                var panel = root.AddComponent<CharacterDisplayPanel>();
                var panelProperties = new SerializedObject(panel);
                panelProperties.FindProperty("activeView").objectReferenceValue = view;
                panelProperties.FindProperty("backgroundRenderer").objectReferenceValue = backgroundRenderer;
                panelProperties.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Character display prefab created at {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var imageObject = CreateUiObject(name, parent);
            Stretch((RectTransform)imageObject.transform);
            var image = imageObject.AddComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
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
