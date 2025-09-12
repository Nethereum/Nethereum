using System;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;

namespace Nethereum.Wallet.UI.Components.AccountList
{
    public class AccountListPluginViewModel : IDashboardPluginViewModel
    {
        private readonly IComponentLocalizer<AccountListPluginViewModel> _localizer;

        public AccountListPluginViewModel(IComponentLocalizer<AccountListPluginViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string PluginId => "account-list";
        public string DisplayName => _localizer.GetString(AccountListPluginLocalizer.Keys.DisplayName);
        public string Description => _localizer.GetString(AccountListPluginLocalizer.Keys.Description);
        public string Icon => "account_circle";
        public int SortOrder => 1;
        public bool IsVisible => true;
        public bool IsEnabled => true;

        public bool IsAvailable()
        {
            return true;
        }
    }
}