using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TxTRPG.UI.Tests
{
    public sealed class AssetManagementTests
    {
        [Test]
        public async Task AssetScope_DisposesEveryLoadedLease()
        {
            var provider = new FakeProvider();
            var scope = new AssetScope(provider);

            await scope.LoadAsync<ScriptableObject>("first");
            await scope.LoadAsync<ScriptableObject>("second");
            scope.Dispose();
            scope.Dispose();

            Assert.That(provider.ReleaseCount, Is.EqualTo(2));
            Object.DestroyImmediate(provider.Asset);
        }

        [Test]
        public async Task AssetScope_DisposeDuringLoad_ReleasesLateLease()
        {
            var provider = new DeferredProvider();
            var scope = new AssetScope(provider);
            var load = scope.LoadAsync<ScriptableObject>("deferred");

            scope.Dispose();
            provider.Complete();

            try
            {
                await load;
                Assert.Fail("A load completed after its AssetScope had been disposed.");
            }
            catch (ObjectDisposedException)
            {
            }
            Assert.That(provider.ReleaseCount, Is.EqualTo(1));
            Object.DestroyImmediate(provider.Asset);
        }

        [Test]
        public async Task ActionGrid_DeduplicatesIconsAndContinuesAfterIndividualFailure()
        {
            ActionGridPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var instance = Object.Instantiate(prefab);
            var provider = new RecordingSpriteProvider("missing");
            try
            {
                var panel = instance.GetComponent<ActionGridPanel>();
                panel.SetAssetProvider(provider);
                panel.SetEntries(new[]
                {
                    Entry("one", "shared"),
                    Entry("two", "missing"),
                    Entry("three", "shared"),
                    Entry("four", "valid")
                }, 4);
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex(
                        "Action grid icon 'missing' could not be loaded:.*"));

                await panel.LoadIconsAsync();

                Assert.That(provider.Requests.Count(id => id == "shared"), Is.EqualTo(1));
                Assert.That(provider.Requests.Count(id => id == "missing"), Is.EqualTo(1));
                Assert.That(provider.Requests.Count(id => id == "valid"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                provider.Dispose();
            }
        }

        [Test]
        public void AddressableRegistration_UsesRequestedGroupAndStableAddress()
        {
            const string path = "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab";
            const string address = "ui/tests/flexible-layout-panel";
            try
            {
                var registered = AddressableAssetEditor.Register(path, address, "Gameplay_Common");
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));

                Assert.That(registered, Is.EqualTo(address));
                Assert.That(entry, Is.Not.Null);
                Assert.That(entry.address, Is.EqualTo(address));
                Assert.That(entry.parentGroup.Name, Is.EqualTo("Gameplay_Common"));
            }
            finally
            {
                AddressableAssetEditor.Register(
                    path,
                    "ui/prefabs/flexible-layout-panel",
                    "Gameplay_Common");
            }
        }

        [Test]
        public void AddressableSettings_HaveNoDuplicateOrMissingEntries()
        {
            AddressableAssetEditor.RegisterUiAssets();
            Assert.That(AddressableAssetEditor.ValidateSettings(), Is.Empty);
        }

        private sealed class FakeProvider : IAssetProvider
        {
            public ScriptableObject Asset { get; } = ScriptableObject.CreateInstance<TestAsset>();
            public int ReleaseCount { get; private set; }

            public Task<AssetLease<T>> LoadAsync<T>(
                string assetId,
                CancellationToken cancellationToken = default)
                where T : Object
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new AssetLease<T>(Asset as T, assetId, () => ReleaseCount++));
            }
        }

        private sealed class TestAsset : ScriptableObject
        {
        }

        private static ActionGridEntry Entry(string id, string iconAssetId)
        {
            return new ActionGridEntry(
                id,
                ActionGridEntryKind.Item,
                null,
                id,
                iconAssetId: iconAssetId);
        }

        private sealed class DeferredProvider : IAssetProvider
        {
            private readonly TaskCompletionSource<bool> completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public ScriptableObject Asset { get; } = ScriptableObject.CreateInstance<TestAsset>();
            public int ReleaseCount { get; private set; }
            public void Complete() => completion.TrySetResult(true);

            public async Task<AssetLease<T>> LoadAsync<T>(
                string assetId,
                CancellationToken cancellationToken = default)
                where T : Object
            {
                await completion.Task;
                return new AssetLease<T>(Asset as T, assetId, () => ReleaseCount++);
            }
        }

        private sealed class RecordingSpriteProvider : IAssetProvider, IDisposable
        {
            private readonly string failingId;
            private readonly Texture2D texture = new(2, 2);
            private readonly Sprite sprite;
            public RecordingSpriteProvider(string failingId)
            {
                this.failingId = failingId;
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            }
            public List<string> Requests { get; } = new();

            public Task<AssetLease<T>> LoadAsync<T>(
                string assetId,
                CancellationToken cancellationToken = default)
                where T : Object
            {
                Requests.Add(assetId);
                if (assetId == failingId) throw new InvalidOperationException("Expected test failure.");
                return Task.FromResult(new AssetLease<T>(sprite as T, assetId, () => { }));
            }

            public void Dispose()
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
