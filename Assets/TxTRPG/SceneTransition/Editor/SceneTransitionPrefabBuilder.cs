using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.SceneTransition.Editor
{
    public static class SceneTransitionPrefabBuilder
    {
        public const string DefaultProfilePath =
            "Assets/TxTRPG/SceneTransition/Profiles/DefaultSceneTransitionProfile.asset";
        public const string PersistentRootPrefabPath =
            "Assets/TxTRPG/SceneTransition/Resources/TxTRPG/PersistentAppRoot.prefab";

        [MenuItem("Tools/TxT RPG/Rebuild Persistent App Root")]
        public static void CreateOrUpdateAssets()
        {
            EnsureFolder("Assets/TxTRPG/SceneTransition/Profiles");
            EnsureFolder("Assets/TxTRPG/SceneTransition/Resources/TxTRPG");
            var profile = CreateOrUpdateProfile();
            BuildPersistentRoot(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Persistent app root created at {PersistentRootPrefabPath}.");
        }

        private static SceneTransitionProfile CreateOrUpdateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<SceneTransitionProfile>(DefaultProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SceneTransitionProfile>();
                AssetDatabase.CreateAsset(profile, DefaultProfilePath);
            }

            var properties = new SerializedObject(profile);
            properties.FindProperty("color").colorValue = Color.black;
            properties.FindProperty("coverDuration").floatValue = 0.25f;
            properties.FindProperty("revealDuration").floatValue = 0.25f;
            properties.FindProperty("minimumCoveredTime").floatValue = 0.1f;
            properties.FindProperty("readinessTimeout").floatValue = 30f;
            properties.FindProperty("maximumFrameDelta").floatValue = 0.05f;
            properties.FindProperty("useUnscaledTime").boolValue = true;
            properties.FindProperty("blockInput").boolValue = true;
            properties.FindProperty("reducedMotionMode").enumValueIndex =
                (int)ReducedMotionTransitionMode.ShortFade;
            properties.FindProperty("reducedMotionDuration").floatValue = 0.08f;
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void BuildPersistentRoot(SceneTransitionProfile profile)
        {
            var root = new GameObject("PersistentAppRoot");
            try
            {
                var persistentRoot = root.AddComponent<PersistentAppRoot>();
                var loader = root.AddComponent<UnitySceneLoader>();
                var service = root.AddComponent<SceneTransitionService>();

                var canvasObject = CreateUiObject("TransitionCanvas", root.transform);
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760;
                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();

                var blockerObject = CreateUiObject("InputBlocker", canvasObject.transform);
                var blockerImage = blockerObject.AddComponent<Image>();
                blockerImage.color = Color.clear;
                blockerImage.raycastTarget = true;
                var blocker = blockerObject.AddComponent<CanvasGroup>();

                var transitionObject = CreateUiObject("TransitionImage", canvasObject.transform);
                var transitionImage = transitionObject.AddComponent<Image>();
                transitionImage.color = Color.black;
                transitionImage.raycastTarget = false;
                var fade = transitionObject.AddComponent<FadeScreenTransitionEffect>();
                fade.Configure(transitionImage);

                var effectLayer = CreateUiObject("EffectLayer", canvasObject.transform);
                effectLayer.AddComponent<CanvasGroup>().blocksRaycasts = false;

                var loadingIndicator = CreateUiObject("LoadingIndicator", canvasObject.transform);
                loadingIndicator.AddComponent<CanvasGroup>().blocksRaycasts = false;
                loadingIndicator.SetActive(false);

                var errorObject = CreateUiObject("ErrorFallback", canvasObject.transform);
                var errorImage = errorObject.AddComponent<Image>();
                errorImage.color = new Color(0.08f, 0.02f, 0.02f, 0.94f);
                errorImage.raycastTarget = true;
                errorObject.SetActive(false);

                var serviceProperties = new SerializedObject(service);
                serviceProperties.FindProperty("sceneLoaderBehaviour").objectReferenceValue = loader;
                serviceProperties.FindProperty("transitionEffect").objectReferenceValue = fade;
                serviceProperties.FindProperty("defaultProfile").objectReferenceValue = profile;
                serviceProperties.FindProperty("inputBlocker").objectReferenceValue = blocker;
                serviceProperties.FindProperty("errorFallback").objectReferenceValue = errorObject;
                serviceProperties.ApplyModifiedPropertiesWithoutUndo();

                var rootProperties = new SerializedObject(persistentRoot);
                rootProperties.FindProperty("sceneTransitionService").objectReferenceValue = service;
                rootProperties.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PersistentRootPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            var rect = (RectTransform)result.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return result;
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
