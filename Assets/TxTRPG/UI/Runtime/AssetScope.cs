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
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AssetScope));
            }

            var lease = await provider.LoadAsync<T>(assetId, cancellationToken);
            if (disposed)
            {
                lease.Dispose();
                throw new ObjectDisposedException(nameof(AssetScope));
            }

            leases.Add(lease);
            return lease;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (var i = leases.Count - 1; i >= 0; i--)
            {
                leases[i]?.Dispose();
            }

            leases.Clear();
        }
    }
}
