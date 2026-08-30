using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class AssetScope : IDisposable
    {
        private readonly IAssetProvider provider;
        private readonly List<IDisposable> leases = new();
        private readonly object synchronization = new();
        private bool disposed;

        public AssetScope(IAssetProvider assetProvider = null)
        {
            provider = assetProvider ?? AddressablesAssetProvider.Shared;
        }

        public async Task<AssetLease<T>> LoadAsync<T>(
            string assetId,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            lock (synchronization)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(AssetScope));
                }
            }

            var lease = await provider.LoadAsync<T>(assetId, cancellationToken);
            lock (synchronization)
            {
                if (!disposed)
                {
                    leases.Add(lease);
                    return lease;
                }
            }

            lease.Dispose();
            throw new ObjectDisposedException(nameof(AssetScope));
        }

        public void Dispose()
        {
            IDisposable[] ownedLeases;
            lock (synchronization)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                ownedLeases = leases.ToArray();
                leases.Clear();
            }

            for (var i = ownedLeases.Length - 1; i >= 0; i--)
            {
                ownedLeases[i]?.Dispose();
            }
        }
    }
}
