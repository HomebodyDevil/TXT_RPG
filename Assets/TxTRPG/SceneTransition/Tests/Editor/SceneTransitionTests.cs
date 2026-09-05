using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.SceneTransition.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.SceneTransition.Tests
{
    public sealed class SceneTransitionTests
    {
        [Test]
        public void GeneratedPersistentRoot_HasRequiredHierarchyAndReferences()
        {
            SceneTransitionPrefabBuilder.CreateOrUpdateAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                SceneTransitionPrefabBuilder.PersistentRootPrefabPath);
            var profile = AssetDatabase.LoadAssetAtPath<SceneTransitionProfile>(
                SceneTransitionPrefabBuilder.DefaultProfilePath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(prefab.GetComponent<PersistentAppRoot>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<SceneTransitionService>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<UnitySceneLoader>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/InputBlocker"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/TransitionImage"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/EffectLayer"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/LoadingIndicator"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/ErrorFallback"), Is.Not.Null);
        }

        [Test]
        public void FailedLoad_AlwaysRevealsScreenAndReleasesInput()
        {
            var fixture = CreateServiceFixture();
            var effect = new RecordingEffect();
            fixture.Service.Configure(
                new FailingLoader(), effect, fixture.Profile, fixture.Blocker, fixture.ErrorFallback);
            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await fixture.Service.LoadSceneAsync("Missing"));
                Assert.That(effect.CompletedImmediately, Is.True);
                Assert.That(fixture.Blocker.blocksRaycasts, Is.False);
                Assert.That(fixture.Blocker.interactable, Is.False);
                Assert.That(fixture.ErrorFallback.activeSelf, Is.True);
                Assert.That(fixture.Service.State, Is.EqualTo(SceneTransitionState.Failed));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public async Task ConcurrentRequest_IsRejectedUntilActiveTransitionFinishes()
        {
            var fixture = CreateServiceFixture();
            var loader = new ControlledLoader();
            fixture.Service.Configure(
                loader, new RecordingEffect(), fixture.Profile, fixture.Blocker, fixture.ErrorFallback);
            try
            {
                var first = fixture.Service.LoadSceneAsync("First");
                Assert.That(fixture.Service.IsTransitioning, Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    fixture.Service.LoadSceneAsync("Second"));

                loader.Complete(SceneManager.GetActiveScene());
                await first;
                Assert.That(fixture.Service.IsTransitioning, Is.False);
                Assert.That(fixture.Service.State, Is.EqualTo(SceneTransitionState.Completed));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public async Task CancelledTransition_AlwaysRevealsScreenAndReleasesInput()
        {
            var fixture = CreateServiceFixture();
            var effect = new RecordingEffect();
            fixture.Service.Configure(
                new CancellableLoader(), effect, fixture.Profile, fixture.Blocker, fixture.ErrorFallback);
            try
            {
                var transition = fixture.Service.LoadSceneAsync("Cancelled");
                fixture.Service.CancelCurrentTransition();

                try
                {
                    await transition;
                    Assert.Fail("The cancelled transition should not complete successfully.");
                }
                catch (OperationCanceledException)
                {
                }
                Assert.That(effect.CompletedImmediately, Is.True);
                Assert.That(fixture.Blocker.blocksRaycasts, Is.False);
                Assert.That(fixture.Blocker.interactable, Is.False);
                Assert.That(fixture.ErrorFallback.activeSelf, Is.False);
                Assert.That(fixture.Service.State, Is.EqualTo(SceneTransitionState.Cancelled));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public async Task SceneReadiness_WaitsForExplicitSignal()
        {
            var root = new GameObject("Ready Source");
            var signal = root.AddComponent<SceneReadySignal>();
            signal.Configure(false);
            try
            {
                var waiting = SceneTransitionService.WaitForSceneReadinessAsync(
                    SceneManager.GetActiveScene(), 1f, CancellationToken.None);
                await Task.Yield();
                Assert.That(waiting.IsCompleted, Is.False);

                signal.MarkReady();
                await waiting;
                Assert.That(signal.IsReady, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ServiceFixture CreateServiceFixture()
        {
            var root = new GameObject("Transition Service Test");
            var service = root.AddComponent<SceneTransitionService>();
            var blockerObject = new GameObject("Blocker", typeof(CanvasGroup));
            blockerObject.transform.SetParent(root.transform, false);
            var errorFallback = new GameObject("Error");
            errorFallback.transform.SetParent(root.transform, false);
            errorFallback.SetActive(false);
            var profile = ScriptableObject.CreateInstance<SceneTransitionProfile>();
            var properties = new SerializedObject(profile);
            properties.FindProperty("minimumCoveredTime").floatValue = 0f;
            properties.FindProperty("readinessTimeout").floatValue = 1f;
            properties.ApplyModifiedPropertiesWithoutUndo();
            return new ServiceFixture(
                root, service, blockerObject.GetComponent<CanvasGroup>(), errorFallback, profile);
        }

        private sealed class ServiceFixture : IDisposable
        {
            public ServiceFixture(
                GameObject root,
                SceneTransitionService service,
                CanvasGroup blocker,
                GameObject errorFallback,
                SceneTransitionProfile profile)
            {
                Root = root;
                Service = service;
                Blocker = blocker;
                ErrorFallback = errorFallback;
                Profile = profile;
            }

            public GameObject Root { get; }
            public SceneTransitionService Service { get; }
            public CanvasGroup Blocker { get; }
            public GameObject ErrorFallback { get; }
            public SceneTransitionProfile Profile { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Profile);
            }
        }

        private sealed class RecordingEffect : IScreenTransitionEffect
        {
            public bool CompletedImmediately { get; private set; }
            public Task CoverAsync(TransitionContext context, CancellationToken cancellationToken) =>
                Task.CompletedTask;
            public Task RevealAsync(TransitionContext context, CancellationToken cancellationToken) =>
                Task.CompletedTask;
            public void CompleteImmediately() => CompletedImmediately = true;
        }

        private sealed class FailingLoader : ISceneLoader
        {
            public Task<SceneLoadResult> LoadSceneAsync(
                string sceneName,
                LoadSceneMode loadMode,
                IProgress<float> progress,
                CancellationToken cancellationToken) =>
                Task.FromException<SceneLoadResult>(new InvalidOperationException("Load failed."));
        }

        private sealed class ControlledLoader : ISceneLoader
        {
            private readonly TaskCompletionSource<SceneLoadResult> completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<SceneLoadResult> LoadSceneAsync(
                string sceneName,
                LoadSceneMode loadMode,
                IProgress<float> progress,
                CancellationToken cancellationToken) => completion.Task;

            public void Complete(Scene scene) => completion.TrySetResult(new SceneLoadResult(scene));
        }

        private sealed class CancellableLoader : ISceneLoader
        {
            public Task<SceneLoadResult> LoadSceneAsync(
                string sceneName,
                LoadSceneMode loadMode,
                IProgress<float> progress,
                CancellationToken cancellationToken)
            {
                var completion = new TaskCompletionSource<SceneLoadResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled());
                return completion.Task;
            }
        }
    }
}
