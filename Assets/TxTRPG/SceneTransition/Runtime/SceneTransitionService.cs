using System;
using System.Collections.Generic;
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
        Failed
    }

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

        public SceneTransitionState State { get; private set; } = SceneTransitionState.Idle;
        public bool IsTransitioning { get; private set; }
        public bool ReduceMotion { get; set; }
        public float Progress { get; private set; }
        public Task CurrentTransition { get; private set; } = Task.CompletedTask;

        public event Action<SceneTransitionState> StateChanged;
        public event Action<float> ProgressChanged;
        public event Action<Exception> TransitionFailed;

        private void Awake()
        {
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

        public Task LoadSceneAsync(
            string sceneName,
            SceneTransitionProfile profile = null,
            CancellationToken cancellationToken = default)
        {
            if (IsTransitioning)
            {
                throw new InvalidOperationException("A scene transition is already in progress.");
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("A scene name or build path is required.", nameof(sceneName));
            }

            var actualProfile = profile != null ? profile : ResolveDefaultProfile();
            CurrentTransition = RunTransitionAsync(sceneName, actualProfile, cancellationToken);
            return CurrentTransition;
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

        private async Task RunTransitionAsync(
            string sceneName,
            SceneTransitionProfile profile,
            CancellationToken cancellationToken)
        {
            ResolveDependencies();
            IsTransitioning = true;
            Progress = 0f;
            DismissError();
            activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = activeCancellation.Token;
            var completed = false;
            var context = new TransitionContext(
                SceneManager.GetActiveScene().name,
                sceneName,
                profile,
                ReduceMotion,
                new System.Progress<float>(ReportProgress));

            SetInputBlocked(profile.BlockInput);
            try
            {
                SetState(SceneTransitionState.Covering);
                await effect.CoverAsync(context, token);
                var coveredAt = Time.realtimeSinceStartupAsDouble;

                SetState(SceneTransitionState.Loading);
                var result = await sceneLoader.LoadSceneAsync(
                    sceneName,
                    LoadSceneMode.Single,
                    context.Progress,
                    token);

                SetState(SceneTransitionState.WaitingForScene);
                await WaitForSceneReadinessAsync(result.Scene, profile.ReadinessTimeout, token);
                await WaitForMinimumCoveredTimeAsync(
                    coveredAt,
                    profile.MinimumCoveredTime,
                    profile.MaximumFrameDelta,
                    token);

                SetState(SceneTransitionState.Revealing);
                await effect.RevealAsync(context, token);
                ReportProgress(1f);
                SetState(SceneTransitionState.Completed);
                completed = true;
            }
            catch (OperationCanceledException)
            {
                SetState(SceneTransitionState.Cancelled);
                throw;
            }
            catch (Exception exception)
            {
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
                    effect?.CompleteImmediately();
                }

                SetInputBlocked(false);
                IsTransitioning = false;
                activeCancellation?.Dispose();
                activeCancellation = null;
            }
        }

        public static async Task WaitForSceneReadinessAsync(
            Scene scene,
            float timeout,
            CancellationToken cancellationToken)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("The target scene must be valid and loaded.", nameof(scene));
            }

            // Allow Start methods to create or complete readiness sources before discovery.
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            var tasks = new List<Task>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is ISceneReadySource source && source.WhenReady != null)
                    {
                        tasks.Add(source.WhenReady);
                    }
                }
            }

            Canvas.ForceUpdateCanvases();
            if (tasks.Count == 0)
            {
                return;
            }

            var readiness = Task.WhenAll(tasks);
            var startedAt = Time.realtimeSinceStartupAsDouble;
            while (!readiness.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timeout > 0f && Time.realtimeSinceStartupAsDouble - startedAt >= timeout)
                {
                    throw new TimeoutException(
                        $"Scene '{scene.name}' did not become ready within {timeout:0.###} seconds.");
                }
                await Task.Yield();
            }

            await readiness;
            cancellationToken.ThrowIfCancellationRequested();
            Canvas.ForceUpdateCanvases();
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

            var elapsed = 0f;
            var previous = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - coveredAt < minimumCoveredTime)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                var now = Time.realtimeSinceStartupAsDouble;
                elapsed += Mathf.Min(Mathf.Max(0f, (float)(now - previous)), maximumFrameDelta);
                previous = now;
                if (elapsed >= minimumCoveredTime)
                {
                    break;
                }
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
