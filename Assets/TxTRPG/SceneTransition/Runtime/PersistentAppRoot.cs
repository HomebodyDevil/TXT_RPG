using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.SceneTransition
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class PersistentAppRoot : MonoBehaviour
    {
        private const string ResourcePath = "TxTRPG/PersistentAppRoot";
        private static PersistentAppRoot instance;

        [SerializeField] private SceneTransitionService sceneTransitionService;

        public static PersistentAppRoot Instance => instance;
        public SceneTransitionService SceneTransitions => sceneTransitionService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
        }

        public static PersistentAppRoot EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            var prefab = Resources.Load<PersistentAppRoot>(ResourcePath);
            if (prefab != null)
            {
                return Instantiate(prefab);
            }

            Debug.LogWarning(
                $"Persistent app root resource '{ResourcePath}' was not found. " +
                "A runtime fallback will be created.");
            return CreateRuntimeFallback();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
            sceneTransitionService ??= GetComponentInChildren<SceneTransitionService>(true);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void Configure(SceneTransitionService service)
        {
            sceneTransitionService = service;
        }

        private static PersistentAppRoot CreateRuntimeFallback()
        {
            var root = new GameObject("PersistentAppRoot");
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
            fade.CompleteImmediately();

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

            service.Configure(loader, fade, null, blocker, errorObject);
            persistentRoot.Configure(service);
            return persistentRoot;
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
    }
}
