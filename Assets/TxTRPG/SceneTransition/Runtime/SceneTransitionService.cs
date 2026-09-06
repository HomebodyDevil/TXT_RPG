using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.SceneTransition
{
    public enum SceneTransitionState
    {
        Idle,
        Covering,
        Loading,
        WaitingForScene,
        Revealing,
        Completed,
        Cancelled,
        Failed,
        Activating,
        Initializing,
        Unloading
    }

    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class SceneTransitionService : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour sceneLoaderBehaviour;
        [SerializeField] private ScreenTransitionEffect transitionEffect;
        [SerializeField] private SceneTransitionProfile defaultProfile;
        [SerializeField] private CanvasGroup inputBlocker;
        [SerializeField] private GameObject errorFallback;

        private ISceneLoader sceneLoader;
        private IScreenTransitionEffect effect;
        private CancellationTokenSource activeCancellation;
        private SceneTransitionProfile runtimeDefaultProfile;
        private Scene appScene;
        private Scene currentContentScene;

        public SceneTransitionState State { get; private set; } = SceneTransitionState.Idle;
        public bool IsTransitioning { get; private set; }
        public bool ReduceMotion { get; set; }
        public float Progress { get; private set; }
        public Scene AppScene => appScene;
        public Scene CurrentContentScene => currentContentScene;
        public Task CurrentTransition { get; private set; } = Task.CompletedTask;

        public event Action<SceneTransitionState> StateChanged;
        public event Action<float> ProgressChanged;
        public event Action<Exception> TransitionFailed;

        private void Awake()
        {
            appScene = gameObject.scene;
            sceneLoader = sceneLoaderBehaviour as ISceneLoader;
            effect = transitionEffect;
            SetInputBlocked(false);
            if (errorFallback != null)
            {
                errorFallback.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            activeCancellation?.Cancel();
            activeCancellation?.Dispose();
            activeCancellation = null;
            if (runtimeDefaultProfile != null)
            {
                Destroy(runtimeDefaultProfile);
            }
        }

        public void Configure(
            ISceneLoader loader,
            IScreenTransitionEffect screenEffect,
            SceneTransitionProfile profile,
            CanvasGroup blocker,
            GameObject fallback)
        {
            sceneLoader = loader;
            effect = screenEffect;
            defaultProfile = profile;
            inputBlocker = blocker;
            errorFallback = fallback;
            SetInputBlocked(false);
            if (errorFallback != null)
            {
                errorFallback.SetActive(false);
            }
        }

        public void PrepareForInitialLoad()
        {
            ResolveDependencies();
            var profile = ResolveDefaultProfile();
            effect.SetCoveredImmediately(profile.Color);
            SetInputBlocked(profile.BlockInput);
        }

        public Task LoadInitialContentSceneAsync(
            string scenePath,
            SceneTransitionProfile profile = null,
            CancellationToken cancellationToken = default)
        {
            return StartTransition(scenePath, profile, cancellationToken, true);
        }

        public Task LoadContentSceneAsync(
            string scenePath,
            SceneTransitionProfile profile = null,
            CancellationToken cancellationToken = default)
        {
            return StartTransition(scenePath, profile, cancellationToken, false);
        }

        public Task LoadSceneAsync(
            string scenePath,
            SceneTransitionProfile profile = null,
            CancellationToken cancellationToken = default)
        {
            return LoadContentSceneAsync(scenePath, profile, cancellationToken);
        }

        public void CancelCurrentTransition()
        {
            activeCancellation?.Cancel();
        }

        public void DismissError()
        {
            if (errorFallback != null)
            {
                errorFallback.SetActive(false);
            }
        }

        private Task StartTransition(
            string scenePath,
            SceneTransitionProfile profile,
            CancellationToken cancellationToken,
            bool isInitialLoad)
        {
            if (IsTransitioning)
            {
                throw new InvalidOperationException("A scene transition is already in progress.");
            }

            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("A full Build Settings scene path is required.", nameof(scenePath));
            }

            var actualProfile = profile != null ? profile : ResolveDefaultProfile();
            CurrentTransition = RunTransitionAsync(
                scenePath,
                actualProfile,
                cancellationToken,
                isInitialLoad);
            return CurrentTransition;
        }

        private async Task RunTransitionAsync(
            string scenePath,
            SceneTransitionProfile profile,
            CancellationToken cancellationToken,
            bool isInitialLoad)
        {
            ResolveDependencies();
            IsTransitioning = true;
            Progress = 0f;
            DismissError();
            activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = activeCancellation.Token;
            var completed = false;
            var sourceScene = ResolveCurrentContentScene();
            var destinationScene = default(Scene);
            var context = new TransitionContext(
                sourceScene.IsValid() ? sourceScene.name : string.Empty,
                scenePath,
                profile,
                ReduceMotion,
                new System.Progress<float>(ReportProgress));
            var shouldCover = isInitialLoad || profile.FadeOutEnabled || profile.CoverDuringLoad;
            var coveredAt = Time.realtimeSinceStartupAsDouble;

            SetInputBlocked(profile.BlockInput);
            try
            {
                if (sceneLoader is IScenePathValidator pathValidator)
                {
                    pathValidator.ValidateScenePath(scenePath);
                }
                ValidateDestination(scenePath, sourceScene);
                SetState(SceneTransitionState.Covering);
                if (isInitialLoad || !profile.FadeOutEnabled)
                {
                    if (shouldCover)
                    {
                        effect.SetCoveredImmediately(profile.Color);
                    }
                }
                else
                {
                    await effect.CoverAsync(context, token);
                }
                coveredAt = Time.realtimeSinceStartupAsDouble;

                if (profile.SceneSwapMode == ContentSceneSwapMode.UnloadBeforeLoad &&
                    IsUnloadableContentScene(sourceScene))
                {
                    SetState(SceneTransitionState.Unloading);
                    await sceneLoader.UnloadSceneAsync(sourceScene, token);
                    currentContentScene = default;
                }

                SetState(SceneTransitionState.Loading);
                var result = await sceneLoader.LoadSceneAsync(
                    scenePath,
                    LoadSceneMode.Additive,
                    context.Progress,
                    token);
                destinationScene = result.Scene;
                token.ThrowIfCancellationRequested();

                SetState(SceneTransitionState.Activating);
                if (!sceneLoader.SetActiveScene(destinationScene))
                {
                    throw new InvalidOperationException(
                        $"Scene '{destinationScene.name}' was loaded but could not become active.");
                }
                currentContentScene = destinationScene;

                SetState(SceneTransitionState.Initializing);
                await InitializeSceneAsync(
                    destinationScene,
                    sourceScene,
                    isInitialLoad,
                    context.Progress,
                    profile.ReadinessTimeout,
                    token);

                SetState(SceneTransitionState.WaitingForScene);
                await WaitForSceneReadinessAsync(
                    destinationScene,
                    profile.ReadinessTimeout,
                    token);

                if (profile.SceneSwapMode == ContentSceneSwapMode.LoadThenUnload &&
                    IsUnloadableContentScene(sourceScene))
                {
                    SetState(SceneTransitionState.Unloading);
                    await sceneLoader.UnloadSceneAsync(sourceScene, token);
                }

                Canvas.ForceUpdateCanvases();
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                Canvas.ForceUpdateCanvases();

                if (shouldCover)
                {
                    await WaitForMinimumCoveredTimeAsync(
                        coveredAt,
                        profile.MinimumCoveredTime,
                        profile.MaximumFrameDelta,
                        token);
                }

                SetState(SceneTransitionState.Revealing);
                if (shouldCover && profile.FadeInEnabled)
                {
                    await effect.RevealAsync(context, token);
                }
                else
                {
                    effect.SetRevealedImmediately();
                }

                ReportProgress(1f);
                SetState(SceneTransitionState.Completed);
                completed = true;
            }
            catch (OperationCanceledException)
            {
                await TryRestorePreviousSceneAsync(sourceScene, destinationScene);
                SetState(SceneTransitionState.Cancelled);
                throw;
            }
            catch (Exception exception)
            {
                await TryRestorePreviousSceneAsync(sourceScene, destinationScene);
                SetState(SceneTransitionState.Failed);
                if (errorFallback != null)
                {
                    errorFallback.SetActive(true);
                }
                TransitionFailed?.Invoke(exception);
                throw;
            }
            finally
            {
                if (!completed)
                {
                    effect?.SetRevealedImmediately();
                }

                SetInputBlocked(false);
                IsTransitioning = false;
                activeCancellation?.Dispose();
                activeCancellation = null;
            }
        }

        public static async Task InitializeSceneAsync(
            Scene scene,
            Scene previousScene,
            bool isInitialLoad,
            IProgress<float> progress,
            float timeout,
            CancellationToken cancellationToken)
        {
            ValidateLoadedScene(scene, nameof(scene));
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            var initializers = FindSceneParticipants<ISceneInitializer>(scene);
            initializers.Sort((left, right) =>
                left.InitializationOrder.CompareTo(right.InitializationOrder));
            var context = new SceneInitializationContext(
                scene,
                previousScene,
                isInitialLoad,
                progress);
            var startedAt = Time.realtimeSinceStartupAsDouble;
            foreach (var initializer in initializers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var initialization = initializer.InitializeAsync(context, cancellationToken);
                if (initialization == null)
                {
                    throw new InvalidOperationException(
                        $"{initializer.GetType().FullName}.InitializeAsync returned null.");
                }
                await AwaitWithTimeoutAsync(
                    initialization,
                    timeout,
                    startedAt,
                    scene.name,
                    "initialize",
                    cancellationToken);
            }
        }

        public static async Task WaitForSceneReadinessAsync(
            Scene scene,
            float timeout,
            CancellationToken cancellationToken)
        {
            ValidateLoadedScene(scene, nameof(scene));
            var sources = FindSceneParticipants<ISceneReadySource>(scene);
            var tasks = new List<Task>(sources.Count);
            foreach (var source in sources)
            {
                if (source.WhenReady != null)
                {
                    tasks.Add(source.WhenReady);
                }
            }

            Canvas.ForceUpdateCanvases();
            if (tasks.Count > 0)
            {
                await AwaitWithTimeoutAsync(
                    Task.WhenAll(tasks),
                    timeout,
                    Time.realtimeSinceStartupAsDouble,
                    scene.name,
                    "become ready",
                    cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            Canvas.ForceUpdateCanvases();
        }

        private static List<T> FindSceneParticipants<T>(Scene scene) where T : class
        {
            var participants = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is T participant)
                    {
                        participants.Add(participant);
                    }
                }
            }
            return participants;
        }

        private static async Task AwaitWithTimeoutAsync(
            Task task,
            float timeout,
            double startedAt,
            string sceneName,
            string operation,
            CancellationToken cancellationToken)
        {
            while (!task.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timeout > 0f && Time.realtimeSinceStartupAsDouble - startedAt >= timeout)
                {
                    throw new TimeoutException(
                        $"Scene '{sceneName}' did not {operation} within {timeout:0.###} seconds.");
                }
                await Task.Yield();
            }

            await task;
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task TryRestorePreviousSceneAsync(Scene sourceScene, Scene destinationScene)
        {
            if (sourceScene.IsValid() && sourceScene.isLoaded)
            {
                sceneLoader.SetActiveScene(sourceScene);
                currentContentScene = sourceScene;
            }
            else if (appScene.IsValid() && appScene.isLoaded)
            {
                sceneLoader.SetActiveScene(appScene);
                currentContentScene = default;
            }

            if (destinationScene.IsValid() && destinationScene.isLoaded &&
                destinationScene != sourceScene && destinationScene != appScene)
            {
                try
                {
                    await sceneLoader.UnloadSceneAsync(destinationScene, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    Debug.LogException(cleanupException, this);
                }
            }
        }

        private Scene ResolveCurrentContentScene()
        {
            if (currentContentScene.IsValid() && currentContentScene.isLoaded)
            {
                return currentContentScene;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded && activeScene != appScene)
            {
                currentContentScene = activeScene;
                return activeScene;
            }
            return default;
        }

        private void ValidateDestination(string scenePath, Scene sourceScene)
        {
            var destinationName = Path.GetFileNameWithoutExtension(scenePath.Replace('\\', '/'));
            if (appScene.IsValid() &&
                string.Equals(destinationName, appScene.name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("AppScene cannot be loaded as a content scene.");
            }
            if (sourceScene.IsValid() &&
                string.Equals(destinationName, sourceScene.name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Content scene '{scenePath}' is already active.");
            }

            var loadedDestination = SceneManager.GetSceneByName(destinationName);
            if (loadedDestination.IsValid() && loadedDestination.isLoaded)
            {
                throw new InvalidOperationException($"Scene '{scenePath}' is already loaded.");
            }
        }

        private bool IsUnloadableContentScene(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded && scene != appScene;
        }

        private static void ValidateLoadedScene(Scene scene, string parameterName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("The target scene must be valid and loaded.", parameterName);
            }
        }

        private static async Task WaitForMinimumCoveredTimeAsync(
            double coveredAt,
            float minimumCoveredTime,
            float maximumFrameDelta,
            CancellationToken cancellationToken)
        {
            if (minimumCoveredTime <= 0f)
            {
                return;
            }

            var previous = Time.realtimeSinceStartupAsDouble;
            var elapsed = Mathf.Max(0f, (float)(previous - coveredAt));
            while (elapsed < minimumCoveredTime)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                var now = Time.realtimeSinceStartupAsDouble;
                elapsed += Mathf.Min(Mathf.Max(0f, (float)(now - previous)), maximumFrameDelta);
                previous = now;
            }
        }

        private void ResolveDependencies()
        {
            sceneLoader ??= sceneLoaderBehaviour as ISceneLoader;
            effect ??= transitionEffect;
            if (sceneLoader == null)
            {
                throw new InvalidOperationException("SceneTransitionService requires an ISceneLoader.");
            }
            if (effect == null)
            {
                throw new InvalidOperationException("SceneTransitionService requires an IScreenTransitionEffect.");
            }
        }

        private SceneTransitionProfile ResolveDefaultProfile()
        {
            if (defaultProfile != null)
            {
                return defaultProfile;
            }

            if (runtimeDefaultProfile == null)
            {
                runtimeDefaultProfile = ScriptableObject.CreateInstance<SceneTransitionProfile>();
                runtimeDefaultProfile.hideFlags = HideFlags.HideAndDontSave;
            }
            return runtimeDefaultProfile;
        }

        private void ReportProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            ProgressChanged?.Invoke(Progress);
        }

        private void SetState(SceneTransitionState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private void SetInputBlocked(bool blocked)
        {
            if (inputBlocker == null)
            {
                return;
            }

            inputBlocker.alpha = 0f;
            inputBlocker.interactable = blocked;
            inputBlocker.blocksRaycasts = blocked;
        }
    }
}
