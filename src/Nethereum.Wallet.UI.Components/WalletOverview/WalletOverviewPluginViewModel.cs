using System;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;

namespace Nethereum.Wallet.UI.Components.WalletOverview
{
    public class WalletOverviewPluginViewModel : IDashboardPluginViewModel
    {
        private readonly IComponentLocalizer<WalletOverviewPluginViewModel> _localizer;

        public WalletOverviewPluginViewModel(IComponentLocalizer<WalletOverviewPluginViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string PluginId => "wallet-overview";
        public string DisplayName => _localizer.GetString(WalletOverviewPluginLocalizer.Keys.DisplayName);
        public string Description => _localizer.GetString(WalletOverviewPluginLocalizer.Keys.Description);
        public string Icon => "dashboard";
        public int SortOrder => 0; // First plugin (before account list)
        public bool IsVisible => true;
        public bool IsEnabled => true;

        public bool IsAvailable()
        {
            return true;
        }
    }
}