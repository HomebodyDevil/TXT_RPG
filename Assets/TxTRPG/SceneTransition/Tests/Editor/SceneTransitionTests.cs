using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.SceneTransition.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.SceneTransition.Tests
{
    public sealed class SceneTransitionTests
    {
        [Test]
        public void EditorPlayStartPolicy_TargetsValidatedAppScene()
        {
            Assert.That(EditorPlayStartPolicy.IsEnabled, Is.True);
            Assert.That(EditorPlayStartPolicy.IsConfigured, Is.True);
            Assert.That(EditorSceneManager.playModeStartScene, Is.SameAs(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorPlayStartPolicy.AppScenePath)));
            Assert.That(EditorPlayStartPolicy.TryValidateConfiguration(out var error), Is.True, error);
        }

        [Test]
        public void GeneratedAppScene_HasRequiredHierarchyReferencesAndBuildOrder()
        {
            SceneTransitionPrefabBuilder.CreateOrUpdateAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                SceneTransitionPrefabBuilder.AppRootPrefabPath);
            var appScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SceneTransitionPrefabBuilder.AppScenePath);
            var profile = AssetDatabase.LoadAssetAtPath<SceneTransitionProfile>(
                SceneTransitionPrefabBuilder.DefaultProfilePath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(appScene, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(prefab.GetComponent<AppSceneRoot>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<AppSceneRoot>().InitialContentScenePath,
                Is.EqualTo("Assets/Scenes/TMP_MainScene.unity"));
            Assert.That(prefab.GetComponent<SceneTransitionService>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<UnitySceneLoader>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/InputBlocker"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/TransitionImage"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/EffectLayer"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/LoadingIndicator"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionCanvas/ErrorFallback"), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes[0].path,
                Is.EqualTo(SceneTransitionPrefabBuilder.AppScenePath));
            Assert.That(
                BuildScenePathUtility.TryValidate(
                    prefab.GetComponent<AppSceneRoot>().InitialContentScenePath,
                    true,
                    out var validationError),
                Is.True,
                validationError);
            Assert.DoesNotThrow(AppSceneBuildValidator.ValidateOrThrow);
        }

        [Test]
        public void ScenePathOptions_ExcludeAppSceneAndDisabledScenes()
        {
            using var scope = new BuildSettingsScope();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneTransitionPrefabBuilder.AppScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/TMP_MainScene.unity", true)
            };

            var options = BuildScenePathUtility.GetSelectableScenes(true);
            Assert.That(options.Count, Is.EqualTo(1));
            Assert.That(options[0].Path, Is.EqualTo("Assets/Scenes/TMP_MainScene.unity"));

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneTransitionPrefabBuilder.AppScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/TMP_MainScene.unity", false)
            };

            Assert.That(BuildScenePathUtility.GetSelectableScenes(true), Is.Empty);
        }

        [Test]
        public void LegacySceneName_ResolvesOnlyToUniqueEnabledFullPath()
        {
            using var scope = new BuildSettingsScope();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneTransitionPrefabBuilder.AppScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/TMP_MainScene.unity", true)
            };

            Assert.That(
                BuildScenePathUtility.TryResolveEnabledScenePath(
                    "TMP_MainScene", true, out var resolvedPath),
                Is.True);
            Assert.That(resolvedPath, Is.EqualTo("Assets/Scenes/TMP_MainScene.unity"));
        }

        [Test]
        public void DuplicateBuildScenePath_IsRejected()
        {
            using var scope = new BuildSettingsScope();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneTransitionPrefabBuilder.AppScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/TMP_MainScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/TMP_MainScene.unity", true)
            };

            Assert.That(
                BuildScenePathUtility.TryValidate(
                    "Assets/Scenes/TMP_MainScene.unity", true, out var error),
                Is.False);
            StringAssert.Contains("more than once", error);
            Assert.That(BuildScenePathUtility.GetBuildSettingsErrors(), Is.Not.Empty);
        }

        [Test]
        public void UnitySceneLoader_AcceptsEnabledFullPathAndRejectsLegacyName()
        {
            using var scope = new BuildSettingsScope();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneTransitionPrefabBuilder.AppScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/TMP_MainScene.unity", true)
            };
            var root = new GameObject("Scene Loader Test");
            try
            {
                var loader = root.AddComponent<UnitySceneLoader>();
                Assert.DoesNotThrow(() =>
                    loader.ValidateScenePath("Assets/Scenes/TMP_MainScene.unity"));
                Assert.Throws<ArgumentException>(() =>
                    loader.ValidateScenePath("TMP_MainScene"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InitialLoad_PreparesCoveredScreenAndBlocksInput()
        {
            var fixture = CreateServiceFixture();
            var effect = new RecordingEffect();
            fixture.Service.Configure(
                new FailingLoader(), effect, fixture.Profile, fixture.Blocker, fixture.ErrorFallback);
            try
            {
                fixture.Service.PrepareForInitialLoad();
                Assert.That(effect.CoveredImmediately, Is.True);
                Assert.That(fixture.Blocker.blocksRaycasts, Is.True);
                Assert.That(fixture.Blocker.interactable, Is.True);
            }
            finally
            {
                fixture.Dispose();
            }
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
                    await fixture.Service.LoadContentSceneAsync("Missing"));
                Assert.That(effect.RevealedImmediately, Is.True);
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
        public async Task ContentLoad_UsesAdditiveModeAndRejectsConcurrentRequest()
        {
            var fixture = CreateServiceFixture();
            var loader = new ControlledLoader();
            fixture.Service.Configure(
                loader, new RecordingEffect(), fixture.Profile, fixture.Blocker, fixture.ErrorFallback);
            try
            {
                var first = fixture.Service.LoadContentSceneAsync("First");
                Assert.That(fixture.Service.IsTransitioning, Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    fixture.Service.LoadContentSceneAsync("Second"));

                loader.Complete(SceneManager.GetActiveScene());
                await first;
                Assert.That(loader.LoadMode, Is.EqualTo(LoadSceneMode.Additive));
                Assert.That(loader.SetActiveCallCount, Is.EqualTo(1));
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
                var transition = fixture.Service.LoadContentSceneAsync("Cancelled");
                fixture.Service.CancelCurrentTransition();

                try
                {
                    await transition;
                    Assert.Fail("The cancelled transition should not complete successfully.");
                }
                catch (OperationCanceledException)
                {
                }
                Assert.That(effect.RevealedImmediately, Is.True);
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
        public async Task CancellationAfterLoadThenUnloadCommit_KeepsDestinationScene()
        {
            var fixture = CreateServiceFixture();
            var sourceScene = EditorSceneManager.NewPreviewScene();
            var destinationScene = EditorSceneManager.NewPreviewScene();
            using var cancellation = new CancellationTokenSource();
            try
            {
                fixture.Service.Configure(
                    new CompletedLoader(sourceScene),
                    new RecordingEffect(),
                    fixture.Profile,
                    fixture.Blocker,
                    fixture.ErrorFallback);
                await fixture.Service.LoadInitialContentSceneAsync("Source Scene");

                var loader = new TrackingLoader(destinationScene);
                fixture.Service.Configure(
                    loader,
                    new CancelOnRevealEffect(cancellation),
                    fixture.Profile,
                    fixture.Blocker,
                    fixture.ErrorFallback);

                try
                {
                    await fixture.Service.LoadContentSceneAsync(
                        "Destination Scene",
                        fixture.Profile,
                        cancellation.Token);
                    Assert.Fail("The transition should be cancelled during reveal.");
                }
                catch (OperationCanceledException)
                {
                }

                Assert.That(loader.UnloadedScenes, Is.EqualTo(new[] { sourceScene }));
                Assert.That(fixture.Service.CurrentContentScene, Is.EqualTo(destinationScene));
                Assert.That(loader.LastActivatedScene, Is.EqualTo(destinationScene));
                Assert.That(fixture.Service.State, Is.EqualTo(SceneTransitionState.Cancelled));
                Assert.That(fixture.Blocker.blocksRaycasts, Is.False);
            }
            finally
            {
                fixture.Dispose();
                EditorSceneManager.ClosePreviewScene(destinationScene);
                EditorSceneManager.ClosePreviewScene(sourceScene);
            }
        }

        [Test]
        public async Task FailureAfterLoadThenUnloadCommit_KeepsDestinationScene()
        {
            var fixture = CreateServiceFixture();
            var sourceScene = EditorSceneManager.NewPreviewScene();
            var destinationScene = EditorSceneManager.NewPreviewScene();
            try
            {
                fixture.Service.Configure(
                    new CompletedLoader(sourceScene),
                    new RecordingEffect(),
                    fixture.Profile,
                    fixture.Blocker,
                    fixture.ErrorFallback);
                await fixture.Service.LoadInitialContentSceneAsync("Source Scene");

                var loader = new TrackingLoader(destinationScene);
                fixture.Service.Configure(
                    loader,
                    new ThrowOnRevealEffect(),
                    fixture.Profile,
                    fixture.Blocker,
                    fixture.ErrorFallback);

                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await fixture.Service.LoadContentSceneAsync("Destination Scene"));

                Assert.That(loader.UnloadedScenes, Is.EqualTo(new[] { sourceScene }));
                Assert.That(fixture.Service.CurrentContentScene, Is.EqualTo(destinationScene));
                Assert.That(loader.LastActivatedScene, Is.EqualTo(destinationScene));
                Assert.That(fixture.Service.State, Is.EqualTo(SceneTransitionState.Failed));
                Assert.That(fixture.ErrorFallback.activeSelf, Is.True);
                Assert.That(fixture.Blocker.blocksRaycasts, Is.False);
            }
            finally
            {
                fixture.Dispose();
                EditorSceneManager.ClosePreviewScene(destinationScene);
                EditorSceneManager.ClosePreviewScene(sourceScene);
            }
        }

        [Test]
        public void ScenePathIdentity_DoesNotCollapseEqualFileNames()
        {
            const string first = "Assets/Scenes/ChapterOne/Battle.unity";
            const string second = "Assets/Scenes/ChapterTwo/Battle.unity";

            Assert.That(ScenePathUtility.Equals(first, first.Replace('/', '\\')), Is.True);
            Assert.That(ScenePathUtility.Equals(first, second), Is.False);
        }

        [Test]
        public async Task SceneInitializers_RunInDeclaredOrder()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var firstObject = new GameObject("First");
            var secondObject = new GameObject("Second");
            SceneManager.MoveGameObjectToScene(firstObject, scene);
            SceneManager.MoveGameObjectToScene(secondObject, scene);
            var calls = new List<int>();
            firstObject.AddComponent<TestSceneInitializer>().Configure(20, calls);
            secondObject.AddComponent<TestSceneInitializer>().Configure(10, calls);
            try
            {
                await SceneTransitionService.InitializeSceneAsync(
                    scene,
                    default,
                    true,
                    null,
                    1f,
                    CancellationToken.None);
                Assert.That(calls, Is.EqualTo(new[] { 10, 20 }));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void InitializerFailure_UnloadsFailedInitialContentScene()
        {
            var fixture = CreateServiceFixture();
            var destination = EditorSceneManager.NewPreviewScene();
            var initializerObject = new GameObject("Failing Initializer");
            SceneManager.MoveGameObjectToScene(initializerObject, destination);
            initializerObject.AddComponent<FailingSceneInitializer>();
            var loader = new CompletedLoader(destination);
            fixture.Service.Configure(
                loader, new RecordingEffect(), fixture.Profile, fixture.Blocker, fixture.ErrorFallback);
            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await fixture.Service.LoadInitialContentSceneAsync("Failed Initial Content"));
                Assert.That(loader.UnloadedScene, Is.EqualTo(destination));
                Assert.That(fixture.Service.CurrentContentScene.IsValid(), Is.False);
            }
            finally
            {
                fixture.Dispose();
                EditorSceneManager.ClosePreviewScene(destination);
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

        private sealed class BuildSettingsScope : IDisposable
        {
            private readonly EditorBuildSettingsScene[] originalScenes =
                EditorBuildSettings.scenes;

            public void Dispose()
            {
                EditorBuildSettings.scenes = originalScenes;
            }
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
            public bool CoveredImmediately { get; private set; }
            public bool RevealedImmediately { get; private set; }
            public Task CoverAsync(TransitionContext context, CancellationToken cancellationToken) =>
                Task.CompletedTask;
            public Task RevealAsync(TransitionContext context, CancellationToken cancellationToken) =>
                Task.CompletedTask;
            public void SetCoveredImmediately(Color color) => CoveredImmediately = true;
            public void SetRevealedImmediately() => RevealedImmediately = true;
        }

        private sealed class CancelOnRevealEffect : IScreenTransitionEffect
        {
            private readonly CancellationTokenSource cancellation;

            public CancelOnRevealEffect(CancellationTokenSource cancellation)
            {
                this.cancellation = cancellation;
            }

            public Task CoverAsync(
                TransitionContext context,
                CancellationToken cancellationToken) => Task.CompletedTask;

            public Task RevealAsync(
                TransitionContext context,
                CancellationToken cancellationToken)
            {
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public void SetCoveredImmediately(Color color)
            {
            }

            public void SetRevealedImmediately()
            {
            }
        }

        private sealed class ThrowOnRevealEffect : IScreenTransitionEffect
        {
            public Task CoverAsync(
                TransitionContext context,
                CancellationToken cancellationToken) => Task.CompletedTask;

            public Task RevealAsync(
                TransitionContext context,
                CancellationToken cancellationToken) =>
                Task.FromException(new InvalidOperationException("Reveal failed."));

            public void SetCoveredImmediately(Color color)
            {
            }

            public void SetRevealedImmediately()
            {
            }
        }

        private abstract class TestLoader : ISceneLoader
        {
            public virtual Task UnloadSceneAsync(Scene scene, CancellationToken cancellationToken) =>
                Task.CompletedTask;
            public virtual bool SetActiveScene(Scene scene) => true;
            public abstract Task<SceneLoadResult> LoadSceneAsync(
                string sceneName,
                LoadSceneMode loadMode,
                IProgress<float> progress,
                CancellationToken cancellationToken);
        }

        private sealed class FailingLoader : TestLoader
        {
            public override Task<SceneLoadResult> LoadSceneAsync(
                string sceneName,
                LoadSceneMode loadMode,
                IProgress<float> progress,
                CancellationToken cancellationToken) =>
                Task.FromException<SceneLoadResult>(new InvalidOperationException("Load failed."));
        }

        private sealed class ControlledLoader : TestLoader
        {
            private readonly TaskCompletionSource<SceneLoadResult> completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public LoadSceneMode LoadMode { get; private set; }
            public int SetActiveCallCount { get; private set; }

            public override Task<SceneLoadResult> LoadSceneAsync(
                string sceneName,
                LoadSceneMode loadMode,
                IProgress<float> progress,
                CancellationToken cancellationToken)
            {
                LoadMode = loadMode;
                return completion.Task;
            }

            public override bool SetActiveScene(Scene scene)
            {
                SetActiveCallCount++;
                return true;
            }

            public void Complete(Scene scene) => completion.TrySetResult(new SceneLoadResult(scene));
        }

        private sealed class CompletedLoader : TestLoader
        {
            private readonly Scene destination;

            public CompletedLoader(Scene destination)
            {
                this.destination = destination;
            }

            public Scene UnloadedScene { get; private set; }

            public override Task<SceneLoadResult> LoadSceneAsync(
                string sceneName,
                LoadSceneMode loadMode,
                IProgress<float> progress,
                CancellationToken cancellationToken) =>
                Task.FromResult(new SceneLoadResult(destination));

            public override Task UnloadSceneAsync(
                Scene scene,
                CancellationToken cancellationToken)
            {
                UnloadedScene = scene;
                return Task.CompletedTask;
            }
        }

        private sealed class TrackingLoader : TestLoader
        {
            private readonly Scene destination;

            public TrackingLoader(Scene destination)
            {
                this.destination = destination;
            }

            public List<Scene> UnloadedScenes { get; } = new();
            public Scene LastActivatedScene { get; private set; }

            public override Task<SceneLoadResult> LoadSceneAsync(
                string scenePath,
                LoadSceneMode loadMode,
                IProgress<float> progress,
                CancellationToken cancellationToken) =>
                Task.FromResult(new SceneLoadResult(destination));

            public override Task UnloadSceneAsync(
                Scene scene,
                CancellationToken cancellationToken)
            {
                UnloadedScenes.Add(scene);
                return Task.CompletedTask;
            }

            public override bool SetActiveScene(Scene scene)
            {
                LastActivatedScene = scene;
                return true;
            }
        }

        private sealed class CancellableLoader : TestLoader
        {
            public override Task<SceneLoadResult> LoadSceneAsync(
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

    public sealed class TestSceneInitializer : MonoBehaviour, ISceneInitializer
    {
        private List<int> calls;

        public int InitializationOrder { get; private set; }

        public void Configure(int order, List<int> target)
        {
            InitializationOrder = order;
            calls = target;
        }

        public Task InitializeAsync(
            SceneInitializationContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            calls.Add(InitializationOrder);
            return Task.CompletedTask;
        }
    }

    public sealed class FailingSceneInitializer : MonoBehaviour, ISceneInitializer
    {
        public int InitializationOrder => 0;

        public Task InitializeAsync(
            SceneInitializationContext context,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Initialization failed.");
        }
    }
}
