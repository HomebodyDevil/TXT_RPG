using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.SceneTransition
{
    public interface ISceneReadySource
    {
        Task WhenReady { get; }
    }

    public interface ISceneInitializer
    {
        int InitializationOrder { get; }

        Task InitializeAsync(
            SceneInitializationContext context,
            CancellationToken cancellationToken);
    }

    public interface ISceneLoader
    {
        Task<SceneLoadResult> LoadSceneAsync(
            string scenePath,
            LoadSceneMode loadMode,
            IProgress<float> progress,
            CancellationToken cancellationToken);

        Task UnloadSceneAsync(Scene scene, CancellationToken cancellationToken);

        bool SetActiveScene(Scene scene);
    }

    public interface IScenePathValidator
    {
        void ValidateScenePath(string scenePath);
    }

    public interface IScreenTransitionEffect
    {
        Task CoverAsync(TransitionContext context, CancellationToken cancellationToken);
        Task RevealAsync(TransitionContext context, CancellationToken cancellationToken);
        void SetCoveredImmediately(Color color);
        void SetRevealedImmediately();
    }

    public readonly struct SceneInitializationContext
    {
        public SceneInitializationContext(
            Scene scene,
            Scene previousScene,
            bool isInitialLoad,
            IProgress<float> progress)
        {
            Scene = scene;
            PreviousScene = previousScene;
            IsInitialLoad = isInitialLoad;
            Progress = progress;
        }

        public Scene Scene { get; }
        public Scene PreviousScene { get; }
        public bool IsInitialLoad { get; }
        public IProgress<float> Progress { get; }
    }

    public readonly struct SceneLoadResult
    {
        public SceneLoadResult(Scene scene)
        {
            Scene = scene;
        }

        public Scene Scene { get; }
    }

    public readonly struct TransitionContext
    {
        public TransitionContext(
            string sourceScene,
            string destinationScene,
            SceneTransitionProfile profile,
            bool reduceMotion,
            IProgress<float> progress)
        {
            SourceScene = sourceScene ?? string.Empty;
            DestinationScene = destinationScene ?? string.Empty;
            Profile = profile;
            ReduceMotion = reduceMotion;
            Progress = progress;
        }

        public string SourceScene { get; }
        public string DestinationScene { get; }
        public SceneTransitionProfile Profile { get; }
        public bool ReduceMotion { get; }
        public IProgress<float> Progress { get; }
    }
}
