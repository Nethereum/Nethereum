using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;

namespace Nethereum.Wallet.UI.Components.Networks
{
    public partial class NetworkManagementPluginViewModel : ObservableObject, IDashboardPluginViewModel
    {
        private readonly IComponentLocalizer<NetworkManagementPluginViewModel> _localizer;

        public NetworkManagementPluginViewModel(IComponentLocalizer<NetworkManagementPluginViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string PluginId => "network_management";
        
        public string DisplayName => _localizer.GetString(NetworkManagementPluginLocalizer.Keys.DisplayName);
        
        public string Description => _localizer.GetString(NetworkManagementPluginLocalizer.Keys.Description);
        
        public string Icon => "language";
        
        public int SortOrder => 20; // After accounts (10) but before other plugins
        
        public bool IsVisible => true;
        
        public bool IsEnabled => true;

        public bool IsAvailable()
        {
            return true;
        }
    }
}