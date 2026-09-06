using System.Threading.Tasks;

namespace TxTRPG.UI
{
    public interface ICharacterAssetReadySource
    {
        Task WhenAssetsReady { get; }
    }
}
