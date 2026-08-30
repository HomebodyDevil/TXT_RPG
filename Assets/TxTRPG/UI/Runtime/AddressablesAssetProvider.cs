using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TxTRPG.UI
{
    public sealed class AddressablesAssetProvider : IAssetProvider
    {
        public static AddressablesAssetProvider Shared { get; } = new();

        public async Task<AssetLease<T>> LoadAsync<T>(
            string assetId,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                throw new ArgumentException("An Addressables asset ID is required.", nameof(assetId));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var handle = Addressables.LoadAssetAsync<T>(assetId);
            try
            {
                await WaitAsync(handle, cancellationToken);
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    throw new InvalidOperationException($"Addressable asset '{assetId}' could not be loaded.");
                }

                return new AssetLease<T>(handle.Result, assetId, () => Addressables.Release(handle));
            }
            catch
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw;
            }
        }

        private static async Task WaitAsync<T>(
            AsyncOperationHandle<T> handle,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                await handle.Task;
                return;
            }

            var cancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => cancellation.TrySetResult(true));
            if (await Task.WhenAny(handle.Task, cancellation.Task) != handle.Task)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            await handle.Task;
        }
    }
}
