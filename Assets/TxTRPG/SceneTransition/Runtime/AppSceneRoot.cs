using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TxTRPG.SceneTransition
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class AppSceneRoot : MonoBehaviour
    {
        private static AppSceneRoot instance;

        [SerializeField] private SceneTransitionService sceneTransitionService;
        [SerializeField] private bool loadInitialContentOnStart = true;
        [FormerlySerializedAs("initialContentScene")]
        [ScenePath(excludeAppScene: true)]
        [SerializeField] private string initialContentScenePath =
            "Assets/Scenes/TMP_MainScene.unity";

        public static AppSceneRoot Instance => instance;
        public SceneTransitionService SceneTransitions => sceneTransitionService;
        public string InitialContentScenePath => initialContentScenePath;

        [Obsolete("Use InitialContentScenePath instead.")]
        public string InitialContentScene => initialContentScenePath;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError(
                    "More than one AppScene is loaded. The duplicate AppScene root will be removed.",
                    this);
                Destroy(gameObject);
                return;
            }

            instance = this;
            sceneTransitionService ??= GetComponentInChildren<SceneTransitionService>(true);
            if (sceneTransitionService == null)
            {
                Debug.LogError("AppSceneRoot requires a SceneTransitionService.", this);
                enabled = false;
                return;
            }

            sceneTransitionService.PrepareForInitialLoad();
        }

        private async void Start()
        {
            if (!Application.isPlaying || !loadInitialContentOnStart ||
                string.IsNullOrWhiteSpace(initialContentScenePath) || sceneTransitionService == null)
            {
                return;
            }

            try
            {
                await sceneTransitionService.LoadInitialContentSceneAsync(initialContentScenePath);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void Configure(
            SceneTransitionService service,
            string contentScenePath,
            bool loadOnStart = true)
        {
            sceneTransitionService = service;
            initialContentScenePath = contentScenePath ?? string.Empty;
            loadInitialContentOnStart = loadOnStart;
        }
    }
}
