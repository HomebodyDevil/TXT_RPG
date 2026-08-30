using System;
using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class AssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        private Action release;

        public AssetLease(T asset, string assetId, Action releaseAction)
        {
            Asset = asset;
            AssetId = assetId ?? string.Empty;
            release = releaseAction;
        }

        public T Asset { get; }
        public string AssetId { get; }
        public bool IsReleased => release == null;

        public void Dispose()
        {
            var action = release;
            release = null;
            action?.Invoke();
        }
    }
}
