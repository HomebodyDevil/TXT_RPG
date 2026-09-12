using UnityEditor;
using TxTRPG.Editor.Common.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class EnemyDisplayPanelPrefabBuilder
    {
        public const string PrefabPath = "Assets/TxTRPG/UI/Prefabs/EnemyDisplayPanel.prefab";
        private const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs";

        [MenuItem(
            TxTRPGEditorMenuPaths.UiPrefabs + "Rebuild Enemy Display Panel",
            false,
            TxTRPGEditorMenuPriorities.Rebuild)]
        public static void CreateOrUpdatePrefab()
        {
            EnsureFolder(PrefabFolder);
            var root = CreateUiObject("EnemyDisplayPanel");
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(760f, 520f);
                var backgroundRenderer = CreateBackground(root.transform);

                var displayRoot = CreateUiObject("EnemyDisplayRoot", root.transform);
                Stretch((RectTransform)displayRoot.transform);
                var backendObject = CreateUiObject("Enemy2DBackend", displayRoot.transform);
                Stretch((RectTransform)backendObject.transform, 24f, 18f, 24f, 18f);

                var viewHost = CreateUiObject("EnemyViewHost", backendObject.transform);
                Stretch((RectTransform)viewHost.transform);
                var poolRoot = CreateUiObject("PoolRoot", backendObject.transform);
                Stretch((RectTransform)poolRoot.transform);

                var template = CreateEnemyView("Enemy2DViewTemplate", poolRoot.transform);
                template.gameObject.SetActive(false);
                poolRoot.SetActive(false);

                var layout = backendObject.AddComponent<ResponsiveHorizontalEnemyLayoutStrategy>();
                var backend = backendObject.AddComponent<Enemy2DDisplayBackend>();
                var backendProperties = new SerializedObject(backend);
                backendProperties.FindProperty("viewPrefab").objectReferenceValue = template;
                backendProperties.FindProperty("viewHost").objectReferenceValue = viewHost.transform;
                backendProperties.FindProperty("poolRoot").objectReferenceValue = poolRoot.transform;
                backendProperties.FindProperty("layoutStrategy").objectReferenceValue = layout;
                backendProperties.ApplyModifiedPropertiesWithoutUndo();

                var foregroundEffectLayer = CreateImage("ForegroundEffectLayer", root.transform);
                foregroundEffectLayer.color = Color.clear;
                foregroundEffectLayer.enabled = false;
                var transitionOverlay = CreateImage("TransitionOverlay", root.transform);
                transitionOverlay.color = Color.clear;
                transitionOverlay.enabled = false;

                var panel = root.AddComponent<EnemyDisplayPanel>();
                var panelProperties = new SerializedObject(panel);
                panelProperties.FindProperty("activeBackend").objectReferenceValue = backend;
                panelProperties.FindProperty("backgroundRenderer").objectReferenceValue = backgroundRenderer;
                panelProperties.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Enemy display prefab created at {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static PanelBackgroundRenderer CreateBackground(Transform parent)
        {
            var layer = CreateUiObject("BackgroundLayer", parent);
            Stretch((RectTransform)layer.transform);
            layer.AddComponent<CanvasGroup>();
            var viewport = CreateUiObject("BackgroundViewport", layer.transform);
            Stretch((RectTransform)viewport.transform);
            var mask = viewport.AddComponent<RectMask2D>();
            var visualRoot = CreateUiObject("BackgroundVisualRoot", viewport.transform);
            Stretch((RectTransform)visualRoot.transform);
            var backgroundA = CreateImage("BackgroundA", visualRoot.transform);
            var backgroundB = CreateImage("BackgroundB", visualRoot.transform);
            var effectOverlay = CreateImage("BackgroundEffectOverlay", visualRoot.transform);
            backgroundB.gameObject.SetActive(false);
            effectOverlay.gameObject.SetActive(false);

            var renderer = layer.AddComponent<PanelBackgroundRenderer>();
            var properties = new SerializedObject(renderer);
            properties.FindProperty("visualRoot").objectReferenceValue = visualRoot.transform;
            properties.FindProperty("backgroundA").objectReferenceValue = backgroundA;
            properties.FindProperty("backgroundB").objectReferenceValue = backgroundB;
            properties.FindProperty("effectOverlay").objectReferenceValue = effectOverlay;
            properties.FindProperty("clipMask").objectReferenceValue = mask;
            properties.ApplyModifiedPropertiesWithoutUndo();
            return renderer;
        }

        private static Enemy2DView CreateEnemyView(string name, Transform parent)
        {
            var viewObject = CreateUiObject(name, parent);
            var viewRect = (RectTransform)viewObject.transform;
            viewRect.anchorMin = viewRect.anchorMax = viewRect.pivot = new Vector2(0.5f, 0.5f);
            viewRect.sizeDelta = new Vector2(180f, 260f);
            var canvasGroup = viewObject.AddComponent<CanvasGroup>();

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

            var targetMarker = CreateImage("TargetMarker", viewObject.transform);
            var targetRect = (RectTransform)targetMarker.transform;
            targetRect.anchorMin = targetRect.anchorMax = new Vector2(0.5f, 1f);
            targetRect.pivot = new Vector2(0.5f, 1f);
            targetRect.anchoredPosition = new Vector2(0f, -5f);
            targetRect.sizeDelta = new Vector2(46f, 7f);
            targetMarker.color = new Color32(255, 203, 76, 255);
            targetMarker.enabled = true;
            targetMarker.gameObject.SetActive(false);

            var defeatedOverlay = CreateImage("DefeatedOverlay", viewObject.transform);
            defeatedOverlay.color = new Color(0.08f, 0.08f, 0.1f, 0.58f);
            defeatedOverlay.enabled = true;
            defeatedOverlay.gameObject.SetActive(false);

            var effectPlayer = viewObject.AddComponent<CharacterEffectPlayer>();
            var view = viewObject.AddComponent<Enemy2DView>();
            var properties = new SerializedObject(view);
            properties.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            properties.FindProperty("frameViewport").objectReferenceValue = frameViewport.transform;
            properties.FindProperty("visualRoot").objectReferenceValue = visualRoot.transform;
            properties.FindProperty("artworkRoot").objectReferenceValue = artworkRoot.transform;
            properties.FindProperty("baseImage").objectReferenceValue = baseImage;
            properties.FindProperty("skinOverlay").objectReferenceValue = skinOverlay;
            properties.FindProperty("effectOverlay").objectReferenceValue = effectOverlay;
            properties.FindProperty("targetMarker").objectReferenceValue = targetMarker.gameObject;
            properties.FindProperty("defeatedOverlay").objectReferenceValue = defeatedOverlay.gameObject;
            properties.FindProperty("effectPlayer").objectReferenceValue = effectPlayer;
            properties.ApplyModifiedPropertiesWithoutUndo();
            return view;
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
