using TxTRPG.Application.Players;
using TxTRPG.Content.Items;
using TxTRPG.UI.Windows;

namespace TxTRPG.Application.Items
{
    public sealed class InventoryModalDataProvider : IModalDataProvider
    {
        private readonly PlayerSessionHost configuredHost;

        public InventoryModalDataProvider(PlayerSessionHost host, ItemCatalog itemCatalog)
        {
            configuredHost = host;
            Catalog = itemCatalog;
        }

        public string ProviderKey => "player-inventory";
        public bool IsAvailable => Catalog != null && (configuredHost != null || PlayerSessionHost.Instance != null);
        public ItemCatalog Catalog { get; }
        public PlayerSessionHost ResolveHost() => configuredHost != null ? configuredHost : PlayerSessionHost.Instance;
    }
}
