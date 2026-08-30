using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

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
    }
}
