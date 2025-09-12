using Nethereum.Wallet.UI.Components.Core.Configuration;

namespace Nethereum.Wallet.UI.Components.WalletOverview
{
    public class WalletOverviewConfiguration : BaseWalletConfiguration, IComponentConfiguration
    {
        public new string ComponentId { get; set; } = "WalletOverview";
        public bool ShowBalance { get; set; } = true;
        public bool ShowFiatBalance { get; set; } = false;
        public bool ShowQuickActions { get; set; } = true;
        public bool AutoRefreshBalance { get; set; } = false;
        public int AutoRefreshIntervalSeconds { get; set; } = 30;
    }
}