using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public interface IAssetProvider
    {
        Task<AssetLease<T>> LoadAsync<T>(string assetId, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;
    }
}
