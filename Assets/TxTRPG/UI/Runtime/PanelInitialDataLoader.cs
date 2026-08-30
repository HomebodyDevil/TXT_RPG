using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public abstract class PanelInitialDataLoader : MonoBehaviour
    {
        public abstract bool HasInitialData { get; }
        public abstract Task<PanelLoadResult> LoadAndApplyAsync(CancellationToken cancellationToken);
    }
}
