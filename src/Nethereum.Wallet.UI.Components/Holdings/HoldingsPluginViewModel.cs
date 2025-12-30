using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public partial class HoldingsPluginViewModel : ObservableObject, IDashboardPluginViewModel
    {
        private readonly IComponentLocalizer<HoldingsPluginViewModel> _localizer;

        public HoldingsPluginViewModel(IComponentLocalizer<HoldingsPluginViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string PluginId => "holdings";
        public string DisplayName => _localizer.GetString(HoldingsPluginLocalizer.Keys.DisplayName);
        public string Description => _localizer.GetString(HoldingsPluginLocalizer.Keys.Description);
        public string Icon => "account_balance_wallet";
        public int SortOrder => 15;
        public bool IsVisible => true;
        public bool IsEnabled => true;

        public bool IsAvailable() => true;
    }
}
