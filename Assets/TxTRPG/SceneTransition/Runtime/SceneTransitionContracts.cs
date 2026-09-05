using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace TxTRPG.SceneTransition
{
    public interface ISceneReadySource
    {
        Task WhenReady { get; }
    }

    public interface ISceneLoader
    {
        Task<SceneLoadResult> LoadSceneAsync(
            string sceneName,
            LoadSceneMode loadMode,
            IProgress<float> progress,
            CancellationToken cancellationToken);
    }

    public interface IScreenTransitionEffect
    {
        Task CoverAsync(TransitionContext context, CancellationToken cancellationToken);
        Task RevealAsync(TransitionContext context, CancellationToken cancellationToken);
        void CompleteImmediately();
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
