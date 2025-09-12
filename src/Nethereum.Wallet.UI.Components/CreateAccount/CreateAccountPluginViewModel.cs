using System;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;
using Nethereum.Wallet.UI.Components.AccountList;

namespace Nethereum.Wallet.UI.Components.CreateAccount
{
    public class CreateAccountPluginViewModel : IDashboardPluginViewModel  
    {
        private readonly IComponentLocalizer<CreateAccountPluginViewModel> _localizer;

        public CreateAccountPluginViewModel(IComponentLocalizer<CreateAccountPluginViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string PluginId => "create-account";
        public string DisplayName => _localizer.GetString(CreateAccountPluginLocalizer.Keys.DisplayName);
        public string Description => _localizer.GetString(CreateAccountPluginLocalizer.Keys.Description);
        public string Icon => "add";
        public int SortOrder => 2;
        public bool IsVisible => true;
        public bool IsEnabled => true;

        public bool IsAvailable()
        {
            return true;
        }
    }
}